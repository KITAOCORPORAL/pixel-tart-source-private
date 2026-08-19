using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Tasks;
using RAWSelectionAssistant.Core.Utilities;

namespace PixelTart.Modules.AssetLibrary;

public sealed class AssetLibraryViewModel : ObservableObject, IAsyncDisposable
{
    private const double MinimumCollectionPaneWidth = 360d;
    private const double PaneSplitterWidth = 6d;
    private readonly IAssetLibraryRepository _repository;
    private readonly IVisualAssetQueryService _visualQuery;
    private readonly IAssetVisualFeatureStore _featureStore;
    private readonly AssetVisualAnalysisService _visualAnalysis;
    private readonly AssetVisualAnalysisBatchProcessor _batchProcessor;
    private readonly TaskOperationBridge _taskOperationBridge;
    private readonly ILogService? _logService;
    private readonly bool _enablePreviewFeatures;
    private readonly AssetVisualAnalysisSelectionCoordinator _analysisCoordinator = new();
    private readonly PreviewImportDiagnosticsWriter _importDiagnostics;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private CancellationTokenSource? _analysisCancellation;
    private CancellationTokenSource? _queryCancellation;
    private CancellationTokenSource? _batchCancellation;
    private long _queryGeneration;
    private long _analysisGeneration;
    private readonly DispatcherTimer _searchDebounce;
    private string _searchText = string.Empty;
    private string _status = "正在准备素材库";
    private string _tagInput = string.Empty;
    private string _folderSearch = string.Empty;
    private string _newFolderName = string.Empty;
    private string _smartFolderName = "精选参考";
    private string _smartRuleValue = "4";
    private string _smartTagValue = "人体";
    private string _smartToneKey = nameof(ToneKeyTendency.Low);
    private string _smartAverageSaturationMaximum = ".30";
    private string _smartDominantHueRange = "80..150";
    private string _smartDominantColor = "";
    private string _smartAnalysisStatus = "Analyzed";
    private string? _nextCursor;
    private AssetItem? _selectedAsset;
    private AssetFolder? _selectedFolder;
    private AssetTag? _selectedTag;
    private SmartFolder? _selectedSmartFolder;
    private AssetVisualAnalysisResult? _analysis;
    private bool _isAnalyzing;
    private int _paletteSize = 5;
    private double _thumbnailWidth = 180;
    private IReadOnlyList<Guid> _lastFolderIds = [];
    private VisualAssetFilter? _visualFilter;
    private VisualResultMode _visualResultMode;
    private bool _isBatchAnalyzing;
    private string _batchStatus = string.Empty;
    private string _batchScope = nameof(VisualBatchScope.Current);
    private string _minimumRatingFilterText = string.Empty;
    private string _addedFromFilterText = string.Empty;
    private string _addedToFilterText = string.Empty;
    private string _visualModeLabel = string.Empty;
    private AssetVisualFeatureSummary? _selectedFeatures;
    private VisualSimilarityQuery? _similarityQuery;
    private VisualAssetQuery? _colorQuery;
    private string _targetColor = "#D75A45";
    private double _colorTolerance = 20;
    private double _minimumVisualValue;
    private double _maximumVisualValue = 1;
    private readonly ObservableCollection<VisualAssetFilter> _visualFilterStack = [];
    private readonly Dictionary<Guid, AssetVisualMatchView> _visualMatchByAsset = [];
    private readonly AssetLibraryWorkspaceSettings _workspaceSettings;
    private bool _isLoading = true;
    private bool _isReady;
    private string _loadErrorMessage = string.Empty;
    private double _viewportWidth = 1280d;
    private bool _isRestoringWorkspace;

    public AssetLibraryViewModel(
        string databasePath,
        TaskOperationBridge taskOperationBridge,
        IReadOnlyList<AssetLibraryModuleDiagnostic>? moduleDiagnostics = null,
        bool enablePreviewFeatures = false,
        AssetLibraryWorkspaceSettings? workspaceSettings = null,
        ILogService? logService = null)
    {
        var database = new AssetLibraryDatabase(databasePath);
        _taskOperationBridge = taskOperationBridge ?? throw new ArgumentNullException(nameof(taskOperationBridge));
        _enablePreviewFeatures = enablePreviewFeatures;
        _logService = logService;
        _workspaceSettings = workspaceSettings ?? new AssetLibraryWorkspaceSettings();
        _workspaceSettings.Normalize();
        _thumbnailWidth = _workspaceSettings.ThumbnailWidth;
        ModuleDiagnostics = enablePreviewFeatures ? moduleDiagnostics ?? [] : [];
        _importDiagnostics = new(Environment.GetEnvironmentVariable("PIXEL_TART_ASSET_LIBRARY_ACCEPTANCE_ROOT")
            ?? Environment.GetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_ROOT"));
        _repository = new SqliteAssetLibraryRepository(database);
        _featureStore = new SqliteAssetVisualAnalysisCache(database);
        _visualAnalysis = new(_featureStore);
        _visualQuery = new SqliteVisualAssetQueryService(database, _featureStore);
        _batchProcessor = new(_visualAnalysis, _featureStore);
        AssetCards.CollectionChanged += (_, _) => _importDiagnostics.RecordCollectionChanged();
        _searchDebounce = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(280) };
        _searchDebounce.Tick += async (_, _) => { _searchDebounce.Stop(); if (IsReady) await RefreshAsync(); };
        RefreshCommand = new(RefreshAsync, () => IsReady); RetryLoadCommand = new(RetryLoadAsync, () => !IsLoading); ImportCommand = new(ImportAsync, () => IsReady); LoadMoreCommand = new(LoadMoreAsync, () => IsReady && _nextCursor is not null);
        NewFolderCommand = new(NewFolderAsync, () => IsReady); NewSubfolderCommand = new(NewSubfolderAsync, () => IsReady && SelectedFolder is not null); BatchFolderCommand = new(BatchFolderAsync, () => IsReady);
        NewTagCommand = new(NewTagAsync, () => IsReady); ApplyTagsCommand = new(ApplyTagsAsync, () => IsReady && SelectedAssets.Count > 0 && !string.IsNullOrWhiteSpace(TagInput));
        AddFolderCommand = new(AddFolderAsync, () => IsReady && SelectedAssets.Count > 0 && SelectedFolder is not null); UndoCommand = new(UndoAsync, () => IsReady && LastUndoToken is not null);
        SaveSmartFolderCommand = new(SaveSmartFolderAsync, () => IsReady); RelinkCommand = new(RelinkAsync, () => IsReady); RateCommand = new AsyncCommand<int>(value => RateSelectedAsync(value), _ => IsReady && SelectedAssets.Count > 0);
        VisualChipCommand = new AsyncCommand<string>(ApplyVisualChipAsync, _ => IsReady); ClearVisualModeCommand = new(ClearVisualModeAsync, () => IsReady && IsTemporaryVisualMode);
        FindSimilarCommand = new(FindSimilarAsync, () => IsReady && SelectedAssets.Count == 1 && SelectedFeatures?.State == AssetVisualFeatureState.Valid);
        AnalyzeSelectionCommand = new(AnalyzeSelectionCanonicalAsync, () => IsReady && SelectedAssets.Count == 1);
        AnalyzeVisibleCommand = new(AnalyzeVisibleAsync, () => IsReady && CanAnalyzeVisible());
        CancelBatchCommand = new(CancelBatchAsync, () => IsReady && IsBatchAnalyzing);
        SearchColorCommand = new(SearchColorAsync, () => IsReady && IsVisualQueryScopeSupported); SearchPaletteColorCommand = new(SearchPaletteColorAsync, _ => IsReady && IsVisualQueryScopeSupported); FindPaletteSimilarCommand = new(FindPaletteSimilarAsync, () => IsReady && SelectedAssets.Count == 1 && Analysis is not null && IsVisualQueryScopeSupported);
        ApplyAdvancedVisualFilterCommand = new(ApplyAdvancedVisualFilterAsync, () => IsReady); RemoveVisualChipCommand = new(RemoveVisualChipAsync, _ => IsReady);
        ToggleOrganizationPaneCommand = new(
            () => IsOrganizationPaneCollapsed = !IsOrganizationPaneCollapsed,
            () => IsOrganizationPaneVisible || IsOrganizationPaneCollapsed && CanShowOrganizationPaneWhenExpanded);
        ToggleInspectorPaneCommand = new(
            () => IsInspectorPaneCollapsed = !IsInspectorPaneCollapsed,
            () => !IsInspectorPinned && (IsInspectorPaneVisible || IsInspectorPaneCollapsed && CanShowInspectorPaneWhenExpanded));
        ToggleInspectorPinCommand = new(() => IsInspectorPinned = !IsInspectorPinned);
    }

    public ObservableCollection<AssetVisualMatchView> AssetCards { get; } = [];
    public IReadOnlyList<AssetLibraryModuleDiagnostic> ModuleDiagnostics { get; }
    public bool IsPreviewDiagnosticsEnabled => _enablePreviewFeatures && ModuleDiagnostics.Count > 0;
    public bool IsReady { get => _isReady; private set { if (SetProperty(ref _isReady, value)) RaiseWorkspaceCommandStates(); } }
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetProperty(ref _isLoading, value)) return;
            NotifyContentState();
            RetryLoadCommand.RaiseCanExecuteChanged();
        }
    }
    public string LoadErrorMessage { get => _loadErrorMessage; private set { if (SetProperty(ref _loadErrorMessage, value)) NotifyContentState(); } }
    public bool HasLoadError => !string.IsNullOrWhiteSpace(LoadErrorMessage);
    public bool HasAssetCards => AssetCards.Count > 0;
    public bool IsEmptyStateVisible => !IsLoading && !HasLoadError && !HasAssetCards;
    public bool HasActiveQuery => !string.IsNullOrWhiteSpace(SearchText) || SelectedFolder is not null || SelectedTag is not null || SelectedSmartFolder is not null || IsTemporaryVisualMode;
    public string EmptyStateTitle => HasActiveQuery ? "没有符合当前条件的素材" : "素材库还是空的";
    public string EmptyStateDescription => HasActiveQuery ? "清除搜索或筛选后重试，现有素材和文件不会被修改。" : "导入文件引用以开始整理；默认不会移动、改名或删除源文件。";
    public double OrganizationPaneWidth => _workspaceSettings.OrganizationPaneWidth;
    public double InspectorPaneWidth => _workspaceSettings.InspectorPaneWidth;
    public bool IsOrganizationPaneCollapsed
    {
        get => _workspaceSettings.OrganizationPaneCollapsed;
        set
        {
            if (_workspaceSettings.OrganizationPaneCollapsed == value) return;
            _workspaceSettings.OrganizationPaneCollapsed = value;
            NotifyWorkspaceLayout();
        }
    }
    public bool IsInspectorPaneCollapsed
    {
        get => _workspaceSettings.InspectorPaneCollapsed;
        set
        {
            if (_workspaceSettings.InspectorPaneCollapsed == value) return;
            _workspaceSettings.InspectorPaneCollapsed = value;
            NotifyWorkspaceLayout();
        }
    }
    public bool IsInspectorPinned
    {
        get => _workspaceSettings.InspectorPinned;
        set
        {
            if (_workspaceSettings.InspectorPinned == value) return;
            _workspaceSettings.InspectorPinned = value;
            if (value) _workspaceSettings.InspectorPaneCollapsed = false;
            NotifyWorkspaceLayout();
            ToggleInspectorPaneCommand.RaiseCanExecuteChanged();
        }
    }
    private bool CanFitOrganizationPane => _viewportWidth >= MinimumCollectionPaneWidth + PaneSplitterWidth + OrganizationPaneWidth;
    private bool CanFitInspectorPane => _viewportWidth >= MinimumCollectionPaneWidth + PaneSplitterWidth + InspectorPaneWidth;
    private bool CanFitBothPanes => _viewportWidth >= MinimumCollectionPaneWidth + (2d * PaneSplitterWidth) + OrganizationPaneWidth + InspectorPaneWidth;
    private bool CanShowOrganizationPaneWhenExpanded =>
        IsInspectorPaneCollapsed || !IsInspectorPinned ? CanFitOrganizationPane : CanFitBothPanes;
    private bool CanShowInspectorPaneWhenExpanded =>
        IsOrganizationPaneCollapsed || IsInspectorPinned ? CanFitInspectorPane : CanFitBothPanes;
    public bool IsOrganizationPaneVisible => !IsOrganizationPaneCollapsed && CanShowOrganizationPaneWhenExpanded;
    public bool IsInspectorPaneVisible => !IsInspectorPaneCollapsed && CanShowInspectorPaneWhenExpanded;
    public GridLength OrganizationPaneColumnWidth => IsOrganizationPaneVisible ? new(OrganizationPaneWidth) : new(0);
    public double OrganizationPaneMinimumWidth => IsOrganizationPaneVisible ? 180d : 0d;
    public double OrganizationPaneMaximumWidth => IsOrganizationPaneVisible ? 420d : 0d;
    public GridLength OrganizationSplitterColumnWidth => IsOrganizationPaneVisible ? new(6) : new(0);
    public GridLength InspectorPaneColumnWidth => IsInspectorPaneVisible ? new(InspectorPaneWidth) : new(0);
    public double InspectorPaneMinimumWidth => IsInspectorPaneVisible ? 260d : 0d;
    public double InspectorPaneMaximumWidth => IsInspectorPaneVisible ? 520d : 0d;
    public GridLength InspectorSplitterColumnWidth => IsInspectorPaneVisible ? new(6) : new(0);
    public string OrganizationPaneToggleLabel => IsOrganizationPaneVisible
        ? "收起组织栏"
        : IsOrganizationPaneCollapsed ? "展开组织栏" : "组织栏（窗口过窄）";
    public string InspectorPaneToggleLabel => IsInspectorPaneVisible
        ? "收起检查器"
        : IsInspectorPaneCollapsed ? "展开检查器" : "检查器（窗口过窄）";
    public string InspectorPinLabel => IsInspectorPinned ? "取消固定检查器" : "固定检查器";
    public PreviewImportDiagnostics ImportDiagnostics => _importDiagnostics.Snapshot;
    public void UpdateAssetGridDiagnostics(int itemCount, string itemsSourceInstance, bool itemsSourceIsViewModelCollection, string dataContextType) =>
        _importDiagnostics.SetBindingState(itemCount, itemsSourceInstance, itemsSourceIsViewModelCollection, dataContextType, CurrentCollectionDiagnostic);
    public IEnumerable<AssetItem> Assets => AssetCards.Select(card => card.Asset);
    public string GetDisplaySourcePath(AssetItem? asset) => asset is not null && !string.IsNullOrWhiteSpace(asset.ManagedCopyPath) && File.Exists(asset.ManagedCopyPath) ? asset.ManagedCopyPath : asset?.SourcePath ?? string.Empty;
    public string SelectedAssetThumbnailPath => GetDisplaySourcePath(SelectedAsset);
    public ObservableCollection<VisualFilterChipView> ActiveVisualChips { get; } = [];
    public ObservableCollection<VisualSearchHistoryEntry> VisualSearchHistory { get; } = [];
    public ObservableCollection<AssetItem> SelectedAssets { get; } = [];
    public event EventHandler? SelectionRestoreRequested;
    public ObservableCollection<AssetFolderTreeItem> FolderTree { get; } = [];
    public ObservableCollection<AssetFolder> Folders { get; } = [];
    public ObservableCollection<AssetFolder> ClassifierFolders { get; } = [];
    public ObservableCollection<AssetFolder> RecentFolders { get; } = [];
    public ObservableCollection<AssetFolder> FavoriteFolders { get; } = [];
    public ObservableCollection<AssetTag> Tags { get; } = [];
    public ObservableCollection<TagGroup> TagGroups { get; } = [];
    public ObservableCollection<SmartFolder> SmartFolders { get; } = [];
    public ObservableCollection<AssetTagUsageSummary> SelectedTagSummary { get; } = [];
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value)) return;
            _workspaceSettings.SearchText = value;
            NotifyContentState();
            if (_isRestoringWorkspace) return;
            _searchDebounce.Stop();
            _searchDebounce.Start();
        }
    }
    public string TagInput { get => _tagInput; set { if (SetProperty(ref _tagInput, value)) ApplyTagsCommand.RaiseCanExecuteChanged(); } }
    public string FolderSearch { get => _folderSearch; set { if (SetProperty(ref _folderSearch, value)) RefreshClassifierFolders(); } }
    public string NewFolderName { get => _newFolderName; set => SetProperty(ref _newFolderName, value); }
    public string SmartFolderName { get => _smartFolderName; set => SetProperty(ref _smartFolderName, value); }
    public string SmartRuleValue { get => _smartRuleValue; set => SetProperty(ref _smartRuleValue, value); }
    public string SmartTagValue { get => _smartTagValue; set => SetProperty(ref _smartTagValue, value); }
    public string SmartToneKey { get => _smartToneKey; set => SetProperty(ref _smartToneKey, value); }
    public string SmartAverageSaturationMaximum { get => _smartAverageSaturationMaximum; set => SetProperty(ref _smartAverageSaturationMaximum, value); }
    public string SmartDominantHueRange { get => _smartDominantHueRange; set => SetProperty(ref _smartDominantHueRange, value); }
    public string SmartDominantColor { get => _smartDominantColor; set => SetProperty(ref _smartDominantColor, value); }
    public string SmartAnalysisStatus { get => _smartAnalysisStatus; set => SetProperty(ref _smartAnalysisStatus, value); }
    public IReadOnlyList<string> SmartAnalysisStatusOptions { get; } = ["Analyzed", "NotAnalyzed", "Stale", "Failed"];
    public string SmartBuilderExplanation => string.Join(" AND ", new[]
    {
        string.IsNullOrWhiteSpace(SmartTagValue) ? null : $"标签={SmartTagValue}",
        string.IsNullOrWhiteSpace(SmartToneKey) ? null : $"影调={SmartToneKey}",
        string.IsNullOrWhiteSpace(SmartAverageSaturationMaximum) ? null : $"平均饱和度≤{SmartAverageSaturationMaximum}",
        string.IsNullOrWhiteSpace(SmartDominantHueRange) ? null : $"主色Hue={SmartDominantHueRange}",
        string.IsNullOrWhiteSpace(SmartDominantColor) ? null : $"主色={SmartDominantColor}",
        string.IsNullOrWhiteSpace(SmartRuleValue) ? null : $"评分≥{SmartRuleValue}",
        string.IsNullOrWhiteSpace(SmartAnalysisStatus) ? null : $"视觉状态={SmartAnalysisStatus}"
    }.Where(value => value is not null)!);
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public void SetForegroundError(string message)
    {
        IsLoading = false;
        LoadErrorMessage = message;
        Status = message;
    }
    public void SetStatusMessage(string message) => Status = message;
    public int VisibleCount => AssetCards.Count;
    public int SelectionCount => SelectedAssets.Count;
    public bool HasSelection => SelectionCount > 0;
    public bool IsSelectionEmpty => SelectionCount == 0;
    public bool HasMultipleSelection => SelectionCount > 1;
    public bool HasSingleSelection => SelectionCount == 1;
    public double ThumbnailWidth
    {
        get => _thumbnailWidth;
        set
        {
            var normalized = Math.Clamp(value, 120, 280);
            if (!SetProperty(ref _thumbnailWidth, normalized)) return;
            _workspaceSettings.ThumbnailWidth = normalized;
            OnPropertyChanged(nameof(ThumbnailItemWidth));
            OnPropertyChanged(nameof(ThumbnailItemHeight));
            OnPropertyChanged(nameof(ThumbnailCardHeight));
        }
    }
    public double ThumbnailItemWidth => ThumbnailWidth + 8d;
    public double ThumbnailItemHeight => ThumbnailWidth + 44d;
    public double ThumbnailCardHeight => ThumbnailWidth + 36d;
    public int PaletteSize { get => _paletteSize; set { if (SetProperty(ref _paletteSize, value) && SelectedAsset is not null) _ = AnalyzeSelectionCanonicalAsync(); } }
    public AssetVisualAnalysisResult? Analysis { get => _analysis; private set { if (SetProperty(ref _analysis, value)) { OnPropertyChanged(nameof(AnalysisStatus)); FindPaletteSimilarCommand.RaiseCanExecuteChanged(); } } }
    public bool IsAnalyzing { get => _isAnalyzing; private set { if (SetProperty(ref _isAnalyzing, value)) OnPropertyChanged(nameof(AnalysisStatus)); } }
    public string AnalysisStatus => HasMultipleSelection ? $"已选择 {SelectionCount} 张图片；不会用首张分析冒充整组。" : IsAnalyzing ? "正在分析视觉数据…" : Analysis is null ? "选择一张图片查看本地视觉统计" : Analysis.CacheHit ? "视觉分析已从缓存读取" : "视觉分析完成并缓存";
    public AssetVisualFeatureSummary? SelectedFeatures { get => _selectedFeatures; private set { if (SetProperty(ref _selectedFeatures, value)) { OnPropertyChanged(nameof(FeatureStatus)); FindSimilarCommand.RaiseCanExecuteChanged(); } } }
    public string FeatureStatus => SelectedFeatures is null ? "未分析" : SelectedFeatures.State switch { AssetVisualFeatureState.Valid => "已分析 · 当前", AssetVisualFeatureState.Stale => "分析已过期 · 需要重新分析", AssetVisualFeatureState.Failed => $"分析失败 · {SelectedFeatures.FailureReason}", _ => "未分析" };
    public bool IsTemporaryVisualMode => _visualResultMode != VisualResultMode.None;
    public string VisualModeLabel { get => _visualModeLabel; private set => SetProperty(ref _visualModeLabel, value); }
    public bool IsBatchAnalyzing { get => _isBatchAnalyzing; private set { if (SetProperty(ref _isBatchAnalyzing, value)) RaiseVisualActions(); } }
    public string BatchStatus { get => _batchStatus; private set => SetProperty(ref _batchStatus, value); }
    public string BatchScope { get => _batchScope; set { if (SetProperty(ref _batchScope, value)) RefreshBatchScopeAvailability(); } }
    public IReadOnlyList<string> BatchScopeOptions { get; } = [nameof(VisualBatchScope.Current), nameof(VisualBatchScope.Selected), nameof(VisualBatchScope.Folder), nameof(VisualBatchScope.Filter)];
    public string BatchTaskState => IsBatchAnalyzing ? "分析中" : string.IsNullOrWhiteSpace(BatchStatus) ? "等待" : BatchStatus.Contains("取消", StringComparison.Ordinal) ? "取消" : BatchStatus.Contains("失败", StringComparison.Ordinal) ? "失败" : BatchStatus.Contains("完成", StringComparison.Ordinal) ? "完成" : BatchStatus;
    public string MinimumRatingFilterText { get => _minimumRatingFilterText; set => SetProperty(ref _minimumRatingFilterText, value); }
    public string AddedFromFilterText { get => _addedFromFilterText; set => SetProperty(ref _addedFromFilterText, value); }
    public string AddedToFilterText { get => _addedToFilterText; set => SetProperty(ref _addedToFilterText, value); }
    public bool IsVisualQueryScopeSupported => SelectedSmartFolder is null && string.IsNullOrWhiteSpace(FileNameRegexFilterText);
    public string VisualQueryScopeStatus => IsVisualQueryScopeSupported ? "视觉查询会叠加当前搜索、文件夹、标签与评分/日期范围。" : "已保存 Smart Folder 或正则范围暂不支持视觉查询；请先清除该范围。";
    public string FileNameRegexFilterText { get; set; } = string.Empty;
    public string TargetColor { get => _targetColor; set => SetProperty(ref _targetColor, value); }
    public double ColorTolerance { get => _colorTolerance; set => SetProperty(ref _colorTolerance, Math.Clamp(value, 1, 100)); }
    public double MinimumVisualValue { get => _minimumVisualValue; set => SetProperty(ref _minimumVisualValue, Math.Clamp(value, 0, 1)); }
    public double MaximumVisualValue { get => _maximumVisualValue; set => SetProperty(ref _maximumVisualValue, Math.Clamp(value, 0, 1)); }
    public AssetLibraryUndoToken? LastUndoToken { get; private set; }
    public AssetItem? SelectedAsset { get => _selectedAsset; set { if (SetProperty(ref _selectedAsset, value)) { SyncSelection(value is null ? [] : [value]); } } }
    public AssetFolder? SelectedFolder { get => _selectedFolder; set { if (SetProperty(ref _selectedFolder, value)) { _workspaceSettings.SelectedFolderId = value?.FolderId; NotifyContentState(); RaiseActions(); RefreshBatchScopeAvailability(); if (!_isRestoringWorkspace) _ = RefreshAsync(); } } }
    public AssetTag? SelectedTag { get => _selectedTag; set { if (SetProperty(ref _selectedTag, value)) { _workspaceSettings.SelectedTagId = value?.TagId; NotifyContentState(); if (!_isRestoringWorkspace) _ = RefreshAsync(); } } }
    public SmartFolder? SelectedSmartFolder { get => _selectedSmartFolder; set { if (SetProperty(ref _selectedSmartFolder, value)) { _workspaceSettings.SelectedSmartFolderId = value?.SmartFolderId; NotifyContentState(); OnPropertyChanged(nameof(IsVisualQueryScopeSupported)); OnPropertyChanged(nameof(VisualQueryScopeStatus)); SearchColorCommand.RaiseCanExecuteChanged(); SearchPaletteColorCommand.RaiseCanExecuteChanged(); FindPaletteSimilarCommand.RaiseCanExecuteChanged(); if (!_isRestoringWorkspace) _ = RefreshAsync(); } } }
    public AsyncCommand RefreshCommand { get; } public AsyncCommand RetryLoadCommand { get; } public AsyncCommand ImportCommand { get; } public AsyncCommand LoadMoreCommand { get; }
    public AsyncCommand NewFolderCommand { get; } public AsyncCommand NewSubfolderCommand { get; } public AsyncCommand BatchFolderCommand { get; }
    public AsyncCommand NewTagCommand { get; } public AsyncCommand ApplyTagsCommand { get; } public AsyncCommand AddFolderCommand { get; }
    public AsyncCommand UndoCommand { get; } public AsyncCommand SaveSmartFolderCommand { get; } public AsyncCommand RelinkCommand { get; } public AsyncCommand<int> RateCommand { get; }
    public AsyncCommand<string> VisualChipCommand { get; } public AsyncCommand ClearVisualModeCommand { get; } public AsyncCommand FindSimilarCommand { get; }
    public AsyncCommand AnalyzeSelectionCommand { get; } public AsyncCommand AnalyzeVisibleCommand { get; } public AsyncCommand CancelBatchCommand { get; }
    public AsyncCommand SearchColorCommand { get; } public AsyncCommand<string> SearchPaletteColorCommand { get; } public AsyncCommand FindPaletteSimilarCommand { get; }
    public AsyncCommand ApplyAdvancedVisualFilterCommand { get; } public AsyncCommand<string> RemoveVisualChipCommand { get; }
    public AssetCommand ToggleOrganizationPaneCommand { get; }
    public AssetCommand ToggleInspectorPaneCommand { get; }
    public AssetCommand ToggleInspectorPinCommand { get; }

    public void UpdateViewportWidth(double width)
    {
        var normalized = double.IsFinite(width) ? Math.Max(0d, width) : 0d;
        if (Math.Abs(_viewportWidth - normalized) < 0.5d) return;
        _viewportWidth = normalized;
        NotifyWorkspaceLayout();
    }

    public void UpdatePaneWidths(double organizationPaneWidth, double inspectorPaneWidth)
    {
        if (IsOrganizationPaneVisible && double.IsFinite(organizationPaneWidth) && organizationPaneWidth > 0)
            _workspaceSettings.OrganizationPaneWidth = Math.Clamp(organizationPaneWidth, 180d, 420d);
        if (IsInspectorPaneVisible && double.IsFinite(inspectorPaneWidth) && inspectorPaneWidth > 0)
            _workspaceSettings.InspectorPaneWidth = Math.Clamp(inspectorPaneWidth, 260d, 520d);
        NotifyWorkspaceLayout();
    }

    private void NotifyWorkspaceLayout()
    {
        OnPropertyChanged(nameof(OrganizationPaneWidth));
        OnPropertyChanged(nameof(InspectorPaneWidth));
        OnPropertyChanged(nameof(IsOrganizationPaneCollapsed));
        OnPropertyChanged(nameof(IsInspectorPaneCollapsed));
        OnPropertyChanged(nameof(IsInspectorPinned));
        OnPropertyChanged(nameof(IsOrganizationPaneVisible));
        OnPropertyChanged(nameof(IsInspectorPaneVisible));
        OnPropertyChanged(nameof(OrganizationPaneColumnWidth));
        OnPropertyChanged(nameof(OrganizationPaneMinimumWidth));
        OnPropertyChanged(nameof(OrganizationPaneMaximumWidth));
        OnPropertyChanged(nameof(OrganizationSplitterColumnWidth));
        OnPropertyChanged(nameof(InspectorPaneColumnWidth));
        OnPropertyChanged(nameof(InspectorPaneMinimumWidth));
        OnPropertyChanged(nameof(InspectorPaneMaximumWidth));
        OnPropertyChanged(nameof(InspectorSplitterColumnWidth));
        OnPropertyChanged(nameof(OrganizationPaneToggleLabel));
        OnPropertyChanged(nameof(InspectorPaneToggleLabel));
        OnPropertyChanged(nameof(InspectorPinLabel));
        ToggleOrganizationPaneCommand.RaiseCanExecuteChanged();
        ToggleInspectorPaneCommand.RaiseCanExecuteChanged();
    }

    private void NotifyContentState()
    {
        OnPropertyChanged(nameof(HasLoadError));
        OnPropertyChanged(nameof(HasAssetCards));
        OnPropertyChanged(nameof(IsEmptyStateVisible));
        OnPropertyChanged(nameof(HasActiveQuery));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateDescription));
    }

    public async Task ExecuteVisualContextActionAsync(AssetItem asset, VisualContextAction action)
    {
        SyncSelection([asset]);
        await RefreshSelectedFeaturesAsync(asset);
        if (action == VisualContextAction.Analyze || SelectedFeatures?.State != AssetVisualFeatureState.Valid || Analysis is null)
            await AnalyzeSelectionCanonicalAsync();
        if (action == VisualContextAction.Palette)
            await FindPaletteSimilarAsync();
        else if (action == VisualContextAction.Similarity)
            await FindSimilarAsync();
    }

    public async Task InitializeAsync()
    {
        await _initializationGate.WaitAsync();
        try
        {
            if (IsReady) return;
            _searchDebounce.Stop();
            _queryCancellation?.Cancel();
            IsLoading = true;
            LoadErrorMessage = string.Empty;
            await _repository.InitializeAsync();
            await RefreshFilterListsAsync();
            if (_enablePreviewFeatures && Folders.Count == 0) await SeedPreviewStructureAsync();
            RestoreWorkspaceQuery();
            await RefreshAsync();
            var journal = await _repository.ListUndoJournalAsync(1);
            LastUndoToken = journal.FirstOrDefault(x => !x.IsUndone)?.Token;
            IsReady = true;
            RaiseActions();
        }
        catch (Exception exception)
        {
            IsReady = false;
            _logService?.Error("素材库初始化失败。", exception);
            SetForegroundError("素材库加载失败。请检查数据目录权限后重试。");
        }
        finally
        {
            _initializationGate.Release();
            RaiseWorkspaceCommandStates();
        }
    }

    private Task RetryLoadAsync() => IsReady ? RefreshAsync() : InitializeAsync();

    private void RestoreWorkspaceQuery()
    {
        _isRestoringWorkspace = true;
        try
        {
            _searchText = _workspaceSettings.SearchText;
            _selectedFolder = Folders.FirstOrDefault(item => item.FolderId == _workspaceSettings.SelectedFolderId);
            _selectedTag = Tags.FirstOrDefault(item => item.TagId == _workspaceSettings.SelectedTagId);
            _selectedSmartFolder = SmartFolders.FirstOrDefault(item => item.SmartFolderId == _workspaceSettings.SelectedSmartFolderId);
            _workspaceSettings.SelectedFolderId = _selectedFolder?.FolderId;
            _workspaceSettings.SelectedTagId = _selectedTag?.TagId;
            _workspaceSettings.SelectedSmartFolderId = _selectedSmartFolder?.SmartFolderId;
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedFolder));
            OnPropertyChanged(nameof(SelectedTag));
            OnPropertyChanged(nameof(SelectedSmartFolder));
            OnPropertyChanged(nameof(IsVisualQueryScopeSupported));
            OnPropertyChanged(nameof(VisualQueryScopeStatus));
            SearchColorCommand.RaiseCanExecuteChanged();
            SearchPaletteColorCommand.RaiseCanExecuteChanged();
            FindPaletteSimilarCommand.RaiseCanExecuteChanged();
            NotifyContentState();
            RaiseActions();
            RefreshBatchScopeAvailability();
        }
        finally
        {
            _isRestoringWorkspace = false;
        }
    }

    public void SyncSelection(IEnumerable<AssetItem> items)
    {
        var selected = items.DistinctBy(x => x.AssetId).ToArray();
        var previousSingleAssetId = SelectedAssets.Count == 1 ? SelectedAssets[0].AssetId : (Guid?)null;
        var nextSingleAssetId = selected.Length == 1 ? selected[0].AssetId : (Guid?)null;
        if (previousSingleAssetId != nextSingleAssetId)
        {
            Interlocked.Increment(ref _analysisGeneration);
            _analysisCancellation?.Cancel();
            _analysisCoordinator.ClearSelection();
            Analysis = null;
            SelectedFeatures = null;
            IsAnalyzing = false;
        }
        SelectedAssets.Clear(); foreach (var item in selected) SelectedAssets.Add(item);
        _workspaceSettings.SelectedAssetId = nextSingleAssetId;
        if (selected.Length != 1) { _selectedAsset = selected.FirstOrDefault(); OnPropertyChanged(nameof(SelectedAsset)); _analysisCoordinator.ClearSelection(); Analysis = null; SelectedFeatures = null; IsAnalyzing = false; }
        else if (_selectedAsset?.AssetId != selected[0].AssetId) { _selectedAsset = selected[0]; OnPropertyChanged(nameof(SelectedAsset)); }
        OnPropertyChanged(nameof(SelectedAssetThumbnailPath));
        OnPropertyChanged(nameof(SelectionCount)); OnPropertyChanged(nameof(HasSelection)); OnPropertyChanged(nameof(IsSelectionEmpty)); OnPropertyChanged(nameof(HasMultipleSelection)); OnPropertyChanged(nameof(HasSingleSelection)); OnPropertyChanged(nameof(AnalysisStatus));
        _ = RefreshSelectionSummaryAsync(); if (selected.Length == 1) { _ = RefreshSelectedFeaturesAsync(selected[0]); _ = AnalyzeSelectionCanonicalAsync(); } RaiseActions(); RaiseVisualActions();
    }

    private async Task RefreshAsync()
    {
        _queryCancellation?.Cancel(); _queryCancellation?.Dispose(); _queryCancellation = new(); var token = _queryCancellation.Token; var generation = Interlocked.Increment(ref _queryGeneration);
        IsLoading = true;
        LoadErrorMessage = string.Empty;
        try
        {
            if (_visualResultMode == VisualResultMode.Filter && _visualFilter is not null)
            {
                var visual = await _visualQuery.QueryAsync(new(BuildQuery(), _visualFilter, 120), token);
                if (generation != Volatile.Read(ref _queryGeneration)) return;
                SetAssetCards(visual.Items.Select(item => item.Asset)); _nextCursor = visual.NextCursor;
                _importDiagnostics.SetViewState(visual.TotalCount, AssetCards.Count, 0);
                Status = $"临时视觉结果 · 共 {visual.TotalCount:N0} 个，当前显示 {AssetCards.Count:N0} 个";
            }
            else if (_visualResultMode == VisualResultMode.Similarity && _similarityQuery is not null)
            {
                var matches = await _visualQuery.FindSimilarAsync(_similarityQuery with { Scope = BuildQuery() }, token);
                if (generation != Volatile.Read(ref _queryGeneration)) return;
                SetSimilarityMatches(matches); Status = $"临时相似结果 · {matches.Count} 项"; _nextCursor = null;
            }
            else if (_visualResultMode == VisualResultMode.Color && _colorQuery is not null)
            {
                var matches = await _visualQuery.SearchByColorAsync(_colorQuery with { Scope = BuildQuery() }, token);
                if (generation != Volatile.Read(ref _queryGeneration)) return;
                SetColorMatches(matches); Status = $"临时颜色结果 · {matches.Count} 项 · DeltaE76"; _nextCursor = null;
            }
            else
            {
                var page = await _repository.QueryAsync(BuildQuery(), token);
                if (generation != Volatile.Read(ref _queryGeneration)) return;
                SetAssetCards(page.Items); _nextCursor = page.NextCursor;
                _importDiagnostics.SetViewState(page.TotalCount, AssetCards.Count, 0);
                Status = page.RegexError is null ? $"共 {page.TotalCount:N0} 个素材，当前显示 {AssetCards.Count:N0} 个" : $"筛选错误：{page.RegexError}";
            }
            OnPropertyChanged(nameof(VisibleCount)); LoadMoreCommand.RaiseCanExecuteChanged();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (generation != Volatile.Read(ref _queryGeneration)) return;
            _logService?.Error("素材库查询失败。", exception);
            LoadErrorMessage = "无法加载当前素材集合。请检查数据目录权限或稍后重试。";
            Status = LoadErrorMessage;
        }
        finally
        {
            if (generation == Volatile.Read(ref _queryGeneration))
            {
                IsLoading = false;
                NotifyContentState();
            }
        }
    }

    private async Task LoadMoreAsync()
    {
        if (_nextCursor is null) return;
        if (_visualResultMode == VisualResultMode.Filter && _visualFilter is not null)
        {
            var visual = await _visualQuery.QueryAsync(new(BuildQuery(), _visualFilter, 120, _nextCursor)); foreach (var item in visual.Items) AssetCards.Add(new(item.Asset)); _nextCursor = visual.NextCursor;
        }
        else { var page = await _repository.QueryAsync(BuildQuery(_nextCursor)); foreach (var asset in page.Items) AssetCards.Add(new(asset)); _nextCursor = page.NextCursor; }
        Status = $"已加载 {AssetCards.Count:N0} 个素材"; OnPropertyChanged(nameof(VisibleCount)); LoadMoreCommand.RaiseCanExecuteChanged();
    }

    private async Task ImportAsync()
    {
        _importDiagnostics.Snapshot.ImportCommandEntered = true;
        _importDiagnostics.Save();
        string[] selected = [];
        try
        {
            var dialog = new OpenFileDialog { Multiselect = true, Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.tif;*.tiff;*.arw;*.cr2;*.cr3;*.nef;*.raf;*.dng|All files|*.*" };
            if (dialog.ShowDialog() != true) return;
            selected = dialog.FileNames.Where(IsSupportedReferencePath).ToArray();
            _importDiagnostics.Snapshot.PickerAccepted = true;
            _importDiagnostics.SetSource("file-picker", selected.Length, CountExtensions(selected));
            _importDiagnostics.Snapshot.RepositoryAssetCountBefore = (await _repository.QueryAsync(new(PageSize: 1))).TotalCount;
            _importDiagnostics.Snapshot.ImportServiceEntered = true;
            _importDiagnostics.Save();
            var result = await _repository.ImportAsync(selected.Select(path => new AssetImportRequest(path, ComputeContentHash: true)));
            _importDiagnostics.Snapshot.ImportedCount = result.ImportedCount;
            _importDiagnostics.Snapshot.SkippedCount = result.SkippedCount;
            _importDiagnostics.Snapshot.FailedCount = result.MissingCount;
            _importDiagnostics.Snapshot.RepositoryAssetCountAfter = (await _repository.QueryAsync(new(PageSize: 1))).TotalCount;
            _importDiagnostics.Save();
            Status = result.Cancelled ? "导入已取消" : $"已索引 {result.ImportedCount:N0} 项，跳过重复 {result.SkippedCount:N0} 项；未修改源文件。";
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            _importDiagnostics.Snapshot.FailedCount = Math.Max(1, selected.Length);
            _importDiagnostics.Save();
            Status = $"导入失败（{exception.GetType().Name}）；未修改源文件。";
        }
    }

    public async Task ImportDemoDirectoryAsync(string directory)
    {
        _importDiagnostics.Snapshot.ImportCommandEntered = true;
        string[] files = [];
        try
        {
            files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Where(IsSupportedReferencePath).ToArray();
            _importDiagnostics.Snapshot.PickerAccepted = files.Length > 0;
            _importDiagnostics.SetSource("synthetic-directory-recursive", files.Length, CountExtensions(files));
            if (files.Length == 0) { _importDiagnostics.Snapshot.FailedCount++; _importDiagnostics.Save(); return; }
            _importDiagnostics.Snapshot.RepositoryAssetCountBefore = (await _repository.QueryAsync(new(PageSize: 1))).TotalCount;
            _importDiagnostics.Snapshot.ImportServiceEntered = true;
            _importDiagnostics.Save();
            var result = await _repository.ImportAsync(files.Select(path => new AssetImportRequest(path, ComputeContentHash: true)));
            _importDiagnostics.Snapshot.ImportedCount = result.ImportedCount;
            _importDiagnostics.Snapshot.SkippedCount = result.SkippedCount;
            _importDiagnostics.Snapshot.FailedCount = result.MissingCount;
            _importDiagnostics.Snapshot.RepositoryAssetCountAfter = (await _repository.QueryAsync(new(PageSize: 1))).TotalCount;
            _importDiagnostics.Save();
            Status = $"合成测试图库：新索引 {result.ImportedCount}，已存在 {result.SkippedCount}；未修改源文件。";
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            _importDiagnostics.Snapshot.FailedCount = Math.Max(1, files.Length);
            _importDiagnostics.Save();
            Status = $"合成图库导入失败（{exception.GetType().Name}）；未修改源文件。";
        }
    }

    private static bool IsSupportedReferencePath(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".arw", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cr2", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cr3", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".nef", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".raf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".dng", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, int> CountExtensions(IEnumerable<string> paths) => paths
        .GroupBy(path => Path.GetExtension(path).ToLowerInvariant())
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

    private async Task NewFolderAsync() { var name = string.IsNullOrWhiteSpace(NewFolderName) ? UniqueName("新建文件夹", Folders.Select(x => x.Name)) : NewFolderName.Trim(); await _repository.SaveFolderAsync(new(Guid.NewGuid(), null, name)); NewFolderName = string.Empty; await RefreshFilterListsAsync(); Status = $"已创建文件夹：{name}"; }
    private async Task NewSubfolderAsync() { var name = string.IsNullOrWhiteSpace(NewFolderName) ? UniqueName("子文件夹", Folders.Where(x => x.ParentFolderId == SelectedFolder!.FolderId).Select(x => x.Name)) : NewFolderName.Trim(); await _repository.SaveFolderAsync(new(Guid.NewGuid(), SelectedFolder!.FolderId, name)); NewFolderName = string.Empty; await RefreshFilterListsAsync(); Status = $"已创建子文件夹：{SelectedFolder.Name} / {name}"; }
    private async Task BatchFolderAsync() { var result = await _repository.BatchCreateFoldersAsync("人体/身体\n人体/宗教\n参考/白棚\n参考/黑色\n灯光/硬光"); await RefreshFilterListsAsync(); Status = $"批量创建 {result.Created.Count} 个层级文件夹"; }
    private async Task NewTagAsync() { var tags = await _repository.BatchCreateTagsAsync(string.IsNullOrWhiteSpace(TagInput) ? "新标签" : TagInput); TagInput = string.Empty; await RefreshFilterListsAsync(); Status = $"已创建/找到 {tags.Count} 个标签"; }
    private async Task ApplyTagsAsync() { var tags = await _repository.BatchCreateTagsAsync(TagInput); var result = await _repository.AddTagsAsync(SelectedAssets.Select(x => x.AssetId), tags.Select(x => x.TagId)); LastUndoToken = result.UndoToken; Status = $"已为 {SelectionCount} 项添加 {tags.Count} 个标签"; TagInput = string.Empty; await RefreshFilterListsAsync(); await RefreshSelectionSummaryAsync(); RaiseActions(); }
    private async Task AddFolderAsync() { await ApplyFoldersAsync([SelectedFolder!.FolderId]); }

    public async Task ApplyFoldersAsync(IEnumerable<Guid> folderIds)
    {
        var ids = folderIds.Distinct().ToArray(); if (ids.Length == 0 || SelectedAssets.Count == 0) return;
        var result = await _repository.AddToFoldersAsync(SelectedAssets.Select(x => x.AssetId), ids);
        foreach (var folderId in ids) { var folder = Folders.FirstOrDefault(x => x.FolderId == folderId); if (folder is not null) { RecentFolders.Remove(folder); RecentFolders.Insert(0, folder); while (RecentFolders.Count > 6) RecentFolders.RemoveAt(RecentFolders.Count - 1); } }
        _lastFolderIds = ids; LastUndoToken = result.UndoToken; Status = $"已添加 {result.ChangedCount} 项 membership 到 {ids.Length} 个文件夹"; RaiseActions();
    }

    public async Task RepeatLastFolderMembershipAsync() { if (_lastFolderIds.Count == 0) { Status = "Shift+D：尚无上一次文件夹分类"; return; } await ApplyFoldersAsync(_lastFolderIds); Status = $"已重复上次分类：{_lastFolderIds.Count} 个文件夹"; }
    public async Task RateSelectedAsync(int rating) { rating = Math.Clamp(rating, 0, 5); var result = await _repository.UpdateAssetsMetadataAsync(SelectedAssets.Select(asset => asset.AssetId), rating: rating); LastUndoToken = result.UndoToken; Status = $"已将 {SelectionCount} 项评分设为 {rating}"; RaiseActions(); }
    private async Task UndoAsync() { if (LastUndoToken is null) return; Status = await _repository.UndoAsync(LastUndoToken) ? "已撤销上一项素材库操作" : "撤销记录已失效"; LastUndoToken = null; await RefreshFilterListsAsync(); RaiseActions(); }

    private async Task SaveSmartFolderAsync()
    {
        var folder = new SmartFolder(Guid.NewGuid(), string.IsNullOrWhiteSpace(SmartFolderName) ? "新智能文件夹" : SmartFolderName.Trim(), SmartFolderLogic.And);
        var rules = new List<SmartFolderRule>();
        if (!string.IsNullOrWhiteSpace(SmartTagValue)) rules.Add(new(Guid.NewGuid(), folder.SmartFolderId, SmartFolderField.Tag, SmartFolderOperator.Equals, SmartTagValue.Trim()));
        if (Enum.TryParse<ToneKeyTendency>(SmartToneKey, true, out var tone)) rules.Add(new(Guid.NewGuid(), folder.SmartFolderId, SmartFolderField.VisualToneKey, SmartFolderOperator.Equals, tone.ToString()));
        if (!string.IsNullOrWhiteSpace(SmartAnalysisStatus)) rules.Add(new(Guid.NewGuid(), folder.SmartFolderId, SmartFolderField.VisualAnalysisStatus, SmartFolderOperator.Equals, SmartAnalysisStatus.Trim()));
        if (double.TryParse(SmartAverageSaturationMaximum, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var saturationMaximum)) rules.Add(new(Guid.NewGuid(), folder.SmartFolderId, SmartFolderField.VisualAverageSaturation, SmartFolderOperator.LessThanOrEqual, saturationMaximum.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (!string.IsNullOrWhiteSpace(SmartDominantHueRange)) rules.Add(new(Guid.NewGuid(), folder.SmartFolderId, SmartFolderField.VisualDominantHue, SmartFolderOperator.InRange, SmartDominantHueRange.Trim()));
        if (!string.IsNullOrWhiteSpace(SmartDominantColor)) rules.Add(new(Guid.NewGuid(), folder.SmartFolderId, SmartFolderField.VisualDominantColor, SmartFolderOperator.Equals, SmartDominantColor.Trim()));
        if (int.TryParse(SmartRuleValue, out var rating)) rules.Add(new(Guid.NewGuid(), folder.SmartFolderId, SmartFolderField.Rating, SmartFolderOperator.GreaterThanOrEqual, Math.Clamp(rating, 0, 5).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (rules.Count == 0) { Status = "请至少填写一条 Smart Folder 条件"; return; }
        await _repository.SaveSmartFolderAsync(folder, rules); await RefreshFilterListsAsync(); Status = $"已保存智能文件夹：{folder.Name} · {SmartBuilderExplanation}";
        OnPropertyChanged(nameof(SmartBuilderExplanation));
    }

    private async Task RelinkAsync() { var dialog = new OpenFolderDialog { Title = "选择素材新的根目录", Multiselect = false }; if (dialog.ShowDialog() != true) return; var result = await _repository.RelinkMissingAssetsAsync(new(dialog.FolderName)); Status = $"已重新连接 {result.RelinkedCount} 项，仍缺失 {result.StillMissingCount} 项"; await RefreshAsync(); }

    private async Task AnalyzeSelectionCanonicalAsync()
    {
        var asset = SelectedAssets.Count == 1 ? SelectedAssets[0] : null; if (asset is null) return;
        var generation = Interlocked.Increment(ref _analysisGeneration);
        _analysisCancellation?.Cancel(); _analysisCancellation?.Dispose(); _analysisCancellation = new(); var token = _analysisCancellation.Token;
        IsAnalyzing = true;
        try
        {
            var result = await _batchProcessor.AnalyzeInteractiveAsync(asset.AssetId, ct => WpfVisualAnalysisDecoder.DecodeAsync(asset, AssetVisualFeatureContract.PaletteSize, ct, AssetVisualFeatureContract.PaletteSort), token);
            if (!IsCurrentAnalysis(asset, generation)) return;
            var features = (await _featureStore.GetFeaturesAsync(asset.AssetId, token)).Summary;
            if (!IsCurrentAnalysis(asset, generation)) return;
            Analysis = result;
            SelectedFeatures = features;
            Status = "已重新生成当前素材的 canonical 视觉特征";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception) when (IsCurrentAnalysis(asset, generation)) { Status = $"视觉分析失败：{exception.Message}"; }
        finally { if (IsCurrentAnalysis(asset, generation)) { IsAnalyzing = false; RaiseVisualActions(); } }
    }

    private async Task ApplyVisualChipAsync(string? chip)
    {
        _visualResultMode = VisualResultMode.Filter;
        (VisualAssetFilter Filter, string Label) selected = chip switch
        {
            "Valid" => (new(State: AssetVisualFeatureState.Valid), "已分析"),
            "NotAnalyzed" => (new(State: AssetVisualFeatureState.NotAnalyzed), "未分析"),
            "Stale" => (new(State: AssetVisualFeatureState.Stale), "分析已过期"),
            "Red" => (new(DominantHue: new VisualHueRange(345, 15)), "主色：红"),
            "Green" => (new(DominantHue: new VisualHueRange(80, 160)), "主色：绿"),
            "Blue" => (new(DominantHue: new VisualHueRange(200, 260)), "主色：蓝"),
            "Neutral" => (new(Harmony: ColorHarmonyTendency.LowSaturationNeutral), "主色：中性色"),
            "LowSaturation" => (new(Saturation: SaturationTendency.Low), "低饱和"),
            "MediumSaturation" => (new(Saturation: SaturationTendency.Medium), "中饱和"),
            "LowKey" => (new(ToneKey: ToneKeyTendency.Low), "低调"),
            "MidKey" => (new(ToneKey: ToneKeyTendency.Mid), "中间调"),
            "HighKey" => (new(ToneKey: ToneKeyTendency.High), "高调"),
            "LowContrast" => (new(Contrast: ContrastTendency.Low), "低对比"),
            "MediumContrast" => (new(Contrast: ContrastTendency.Medium), "中对比"),
            "HighContrast" => (new(Contrast: ContrastTendency.High), "高对比"),
            "HighSaturation" => (new(Saturation: SaturationTendency.High), "高饱和"),
            "Warm" => (new(WarmCool: WarmCoolTendency.Warm), "暖色倾向"),
            "NeutralWarmCool" => (new(WarmCool: WarmCoolTendency.Neutral), "中性冷暖"),
            "Cool" => (new(WarmCool: WarmCoolTendency.Cool), "冷色倾向"),
            "NarrowSpan" => (new(MaximumLumaSpread: VisualClassificationThresholds.NarrowLuminanceSpanMaximum), "窄亮度跨度"),
            "MediumSpan" => (new(MinimumLumaSpread: VisualClassificationThresholds.NarrowLuminanceSpanMaximum, MaximumLumaSpread: VisualClassificationThresholds.MediumLuminanceSpanMaximum), "中亮度跨度"),
            "WideSpan" => (new(MinimumLumaSpread: VisualClassificationThresholds.MediumLuminanceSpanMaximum), "宽亮度跨度"),
            "LowBlackClip" => (new(MaximumBlackClipRatio: .02), "暗部剪切少"),
            "LowWhiteClip" => (new(MaximumWhiteClipRatio: .02), "高光剪切少"),
            _ => (new(State: AssetVisualFeatureState.Valid), "已分析")
        };
        var key = chip ?? "Valid";
        var exclusiveGroup = GetVisualChipGroup(key);
        if (exclusiveGroup is not null)
        {
            foreach (var peer in ActiveVisualChips.Where(item => GetVisualChipGroup(item.Key) == exclusiveGroup).ToArray())
            {
                ActiveVisualChips.Remove(peer);
                _visualFilterStack.Remove(peer.Filter);
            }
        }
        var existing = ActiveVisualChips.FirstOrDefault(item => item.Key == key);
        if (existing is not null) { ActiveVisualChips.Remove(existing); _visualFilterStack.Remove(existing.Filter); }
        ActiveVisualChips.Add(new(key, selected.Label, selected.Filter)); _visualFilterStack.Add(selected.Filter); _visualFilter = CombineVisualFilters(_visualFilterStack);
        UpdateActiveVisualLabel(); AddVisualSearchHistory(VisualSearchKind.Filter, selected.Label, key);
        NotifyVisualMode(); await RefreshAsync();
    }

    private static string? GetVisualChipGroup(string key) => key switch
    {
        "Valid" or "NotAnalyzed" or "Stale" => "state",
        "Red" or "Green" or "Blue" or "Neutral" => "hue",
        "LowSaturation" or "MediumSaturation" or "HighSaturation" => "saturation",
        "LowKey" or "MidKey" or "HighKey" => "tone",
        "LowContrast" or "MediumContrast" or "HighContrast" => "contrast",
        "Warm" or "NeutralWarmCool" or "Cool" => "warm-cool",
        "NarrowSpan" or "MediumSpan" or "WideSpan" => "luma-span",
        _ => null
    };

    private async Task RemoveVisualChipAsync(string? key)
    {
        var chip = ActiveVisualChips.FirstOrDefault(item => item.Key == key); if (chip is null) return;
        ActiveVisualChips.Remove(chip); _visualFilterStack.Remove(chip.Filter);
        _visualFilter = _visualFilterStack.Count == 0 ? null : CombineVisualFilters(_visualFilterStack);
        _visualResultMode = _visualFilter is null ? VisualResultMode.None : VisualResultMode.Filter;
        UpdateActiveVisualLabel(); NotifyVisualMode(); await RefreshAsync();
    }

    private async Task ClearVisualModeAsync()
    {
        ResetVisualModeState();
        await RefreshAsync();
    }

    private void ResetVisualModeState()
    {
        _visualResultMode = VisualResultMode.None;
        _visualFilter = null;
        _visualFilterStack.Clear();
        ActiveVisualChips.Clear();
        _similarityQuery = null;
        _colorQuery = null;
        _visualMatchByAsset.Clear();
        VisualModeLabel = string.Empty;
        NotifyVisualMode();
    }

    private async Task FindSimilarAsync()
    {
        var asset = SelectedAssets.Count == 1 ? SelectedAssets[0] : null; if (asset is null) return;
        _queryCancellation?.Cancel(); _queryCancellation?.Dispose(); _queryCancellation = new(); var token = _queryCancellation.Token; var generation = Interlocked.Increment(ref _queryGeneration);
        try
        {
            _similarityQuery = new(asset.AssetId, BuildQuery(), 100);
            var matches = await _visualQuery.FindSimilarAsync(_similarityQuery, token);
            if (generation != Volatile.Read(ref _queryGeneration)) return;
            _visualResultMode = VisualResultMode.Similarity; VisualModeLabel = $"与 {asset.DisplayName} 相似"; _visualFilter = null;
            SetSimilarityMatches(matches); _nextCursor = null; Status = $"临时相似结果 · {matches.Count} 项 · {matches.FirstOrDefault()?.Scores.Explanation}"; NotifyVisualMode(); OnPropertyChanged(nameof(VisibleCount));
            AddVisualSearchHistory(VisualSearchKind.Similarity, VisualModeLabel, asset.AssetId.ToString("D"));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (NotSupportedException exception) { Status = $"当前范围不能用于视觉相似查询：{exception.Message}"; }
    }

    private async Task SearchColorAsync()
    {
        if (!TryParseRgb(TargetColor, out var color)) { Status = "目标颜色格式错误，请输入 #RRGGBB"; return; }
        _queryCancellation?.Cancel(); _queryCancellation?.Dispose(); _queryCancellation = new(); var token = _queryCancellation.Token; var generation = Interlocked.Increment(ref _queryGeneration);
        try
        {
            var filter = new VisualAssetFilter(PaletteColor: VisualAnalysisEngine.ToLab(color), MaximumDeltaE: ColorTolerance);
            _colorQuery = new(BuildQuery(), filter, 100);
            var matches = await _visualQuery.SearchByColorAsync(_colorQuery, token);
            if (generation != Volatile.Read(ref _queryGeneration)) return;
            _visualResultMode = VisualResultMode.Color; _visualFilter = null; VisualModeLabel = $"颜色 {TargetColor} · ΔE76≤{ColorTolerance:F0}";
            SetColorMatches(matches); _nextCursor = null; Status = $"临时颜色结果 · {matches.Count} 项 · DeltaE76"; NotifyVisualMode(); OnPropertyChanged(nameof(VisibleCount));
            AddVisualSearchHistory(VisualSearchKind.Color, VisualModeLabel, $"{TargetColor}|{ColorTolerance:F2}");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (NotSupportedException exception) { Status = $"当前范围不能用于颜色查询：{exception.Message}"; }
    }

    private Task SearchPaletteColorAsync(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Task.CompletedTask;
        TargetColor = hex; return SearchColorAsync();
    }

    private async Task FindPaletteSimilarAsync()
    {
        var asset = SelectedAssets.Count == 1 ? SelectedAssets[0] : null;
        if (asset is null || Analysis is null) return;
        _queryCancellation?.Cancel(); _queryCancellation?.Dispose(); _queryCancellation = new(); var token = _queryCancellation.Token; var generation = Interlocked.Increment(ref _queryGeneration);
        try
        {
            _similarityQuery = new(asset.AssetId, BuildQuery(), 100, VisualSimilarityMode.Palette);
            var matches = await _visualQuery.FindSimilarAsync(_similarityQuery, token);
            if (generation != Volatile.Read(ref _queryGeneration)) return;
            _visualResultMode = VisualResultMode.Similarity; _visualFilter = null; VisualModeLabel = $"与 {asset.DisplayName} 配色相近";
            SetSimilarityMatches(matches); _nextCursor = null; Status = $"临时配色相似结果 · {matches.Count} 项 · Top 5 Palette + Weight"; NotifyVisualMode(); OnPropertyChanged(nameof(VisibleCount));
            AddVisualSearchHistory(VisualSearchKind.Palette, VisualModeLabel, asset.AssetId.ToString("D"));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (NotSupportedException exception) { Status = $"当前范围不能用于配色查询：{exception.Message}"; }
    }

    private async Task ApplyAdvancedVisualFilterAsync()
    {
        _visualResultMode = VisualResultMode.Filter;
        var filter = new VisualAssetFilter(MinimumContrast: MinimumVisualValue, MaximumContrast: MaximumVisualValue);
        var existing = ActiveVisualChips.FirstOrDefault(item => item.Key == "AdvancedContrast");
        if (existing is not null) { ActiveVisualChips.Remove(existing); _visualFilterStack.Remove(existing.Filter); }
        var label = $"对比 {MinimumVisualValue:F2}–{MaximumVisualValue:F2}";
        ActiveVisualChips.Add(new("AdvancedContrast", label, filter)); _visualFilterStack.Add(filter); _visualFilter = CombineVisualFilters(_visualFilterStack);
        UpdateActiveVisualLabel(); AddVisualSearchHistory(VisualSearchKind.Filter, label, $"contrast:{MinimumVisualValue:F3}..{MaximumVisualValue:F3}"); NotifyVisualMode(); await RefreshAsync();
    }

    private static bool TryParseRgb(string value, out VisualRgb24 color)
    {
        color = default; var text = value.Trim().TrimStart('#');
        if (text.Length != 6 || !byte.TryParse(text[..2], System.Globalization.NumberStyles.HexNumber, null, out var red) || !byte.TryParse(text[2..4], System.Globalization.NumberStyles.HexNumber, null, out var green) || !byte.TryParse(text[4..], System.Globalization.NumberStyles.HexNumber, null, out var blue)) return false;
        color = new(red, green, blue); return true;
    }

    private static VisualAssetFilter CombineVisualFilters(IEnumerable<VisualAssetFilter> filters)
    {
        var items = filters.ToArray();
        return new(
            State: items.Select(item => item.State).LastOrDefault(value => value is not null),
            DominantHue: items.Select(item => item.DominantHue).LastOrDefault(value => value is not null),
            Harmony: items.Select(item => item.Harmony).LastOrDefault(value => value is not null),
            ToneKey: items.Select(item => item.ToneKey).LastOrDefault(value => value is not null),
            Contrast: items.Select(item => item.Contrast).LastOrDefault(value => value is not null),
            Saturation: items.Select(item => item.Saturation).LastOrDefault(value => value is not null),
            WarmCool: items.Select(item => item.WarmCool).LastOrDefault(value => value is not null),
            MinimumAverageLuma: items.Max(item => item.MinimumAverageLuma), MaximumAverageLuma: items.Min(item => item.MaximumAverageLuma),
            MinimumContrast: items.Max(item => item.MinimumContrast), MaximumContrast: items.Min(item => item.MaximumContrast),
            MinimumAverageSaturation: items.Max(item => item.MinimumAverageSaturation), MaximumAverageSaturation: items.Min(item => item.MaximumAverageSaturation),
            MinimumMedianSaturation: items.Max(item => item.MinimumMedianSaturation), MaximumMedianSaturation: items.Min(item => item.MaximumMedianSaturation),
            MinimumLumaSpread: items.Max(item => item.MinimumLumaSpread), MaximumLumaSpread: items.Min(item => item.MaximumLumaSpread),
            MinimumShadowRatio: items.Max(item => item.MinimumShadowRatio), MinimumHighlightRatio: items.Max(item => item.MinimumHighlightRatio),
            MaximumBlackClipRatio: items.Min(item => item.MaximumBlackClipRatio), MaximumWhiteClipRatio: items.Min(item => item.MaximumWhiteClipRatio),
            MinimumWarmCoolMetric: items.Max(item => item.MinimumWarmCoolMetric), MaximumWarmCoolMetric: items.Min(item => item.MaximumWarmCoolMetric));
    }

    private async Task AnalyzeVisibleAsync()
    {
        _batchCancellation?.Cancel(); _batchCancellation?.Dispose(); _batchCancellation = new(); var token = _batchCancellation.Token;
        var assets = await ResolveBatchAssetsAsync(token);
        if (assets.Count == 0) { BatchStatus = "没有可分析的素材"; return; }
        IsBatchAnalyzing = true;
        try
        {
            await _taskOperationBridge.RunAsync(
                "素材库 · 批量视觉分析",
                async (context, engineToken) =>
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, engineToken);
                    var operationToken = linked.Token;
                    var selectedIds = SelectedAssets.Select(asset => asset.AssetId).ToHashSet();
                    var interactive = assets.Where(asset => selectedIds.Contains(asset.AssetId)).ToArray();
                    var background = assets.Where(asset => !selectedIds.Contains(asset.AssetId)).ToArray();
                    var succeeded = 0; var failed = 0; var cancelled = 0;

                    await context.ReportProgressAsync(0, "视觉分析排队", null,
                        new TaskResultSummary(assets.Count, 0, 0, 0, 0, 0, 0, 0), operationToken);

                    foreach (var asset in interactive)
                    {
                        operationToken.ThrowIfCancellationRequested();
                        try
                        {
                            await _batchProcessor.AnalyzeInteractiveAsync(asset.AssetId,
                                ct => WpfVisualAnalysisDecoder.DecodeAsync(asset, AssetVisualFeatureContract.PaletteSize, ct, AssetVisualFeatureContract.PaletteSort), operationToken);
                            succeeded++;
                        }
                        catch (OperationCanceledException) when (operationToken.IsCancellationRequested) { cancelled++; throw; }
                        catch { failed++; }

                        var completed = succeeded + failed + cancelled;
                        var summary = new TaskResultSummary(assets.Count, succeeded, failed, 0, cancelled, 0, 0, 0);
                        BatchStatus = $"Interactive {completed}/{assets.Count} · 成功 {succeeded} · 失败 {failed}";
                        await context.ReportProgressAsync(completed * 100d / assets.Count, "优先分析当前选择", asset.DisplayName, summary, operationToken);
                    }

                    if (background.Length > 0)
                    {
                        var progress = new Progress<VisualAnalysisBatchProgress>(value =>
                        {
                            var completed = interactive.Length + value.Completed;
                            var currentSucceeded = succeeded + value.Succeeded;
                            var currentFailed = failed + value.Failed;
                            var currentCancelled = cancelled + value.Cancelled;
                            BatchStatus = $"视觉批量分析 {completed}/{assets.Count} · 成功 {currentSucceeded} · 失败 {currentFailed} · 取消 {currentCancelled}";
                            var summary = new TaskResultSummary(assets.Count, currentSucceeded, currentFailed, 0, currentCancelled, 0, 0, 0);
                            _ = context.ReportProgressAsync(completed * 100d / assets.Count, "本地视觉分析", null, summary, CancellationToken.None);
                        });
                        var result = await _batchProcessor.ProcessAsync(background.Select(asset =>
                            new VisualAnalysisBatchItem(asset.AssetId, asset.ContentHash, VisualAnalysisPriority.Background,
                                ct => WpfVisualAnalysisDecoder.DecodeAsync(asset, AssetVisualFeatureContract.PaletteSize, ct, AssetVisualFeatureContract.PaletteSort))), progress, operationToken);
                        succeeded += result.Succeeded; failed += result.Failed; cancelled += result.CancelledCount;
                    }

                    var finalSummary = new TaskResultSummary(assets.Count, succeeded, failed, 0, cancelled, 0, 0, 0);
                    BatchStatus = $"批量完成：{assets.Count} 项 · 成功 {succeeded} · 失败 {failed} · 取消 {cancelled}";
                    await context.ReportProgressAsync(100, "视觉分析完成", null, finalSummary, CancellationToken.None);
                    return finalSummary;
                },
                inputSnapshot: $"asset-library scope={BatchScope}; count={assets.Count}",
                cancellationToken: CancellationToken.None);

            await RefreshAsync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            BatchStatus = "批量分析已取消";
        }
        catch (Exception exception)
        {
            BatchStatus = $"批量分析失败：{exception.Message}";
        }
        finally
        {
            IsBatchAnalyzing = false;
        }
    }

    private async Task<IReadOnlyList<AssetItem>> ResolveBatchAssetsAsync(CancellationToken cancellationToken)
    {
        if (!HasBatchScopePrerequisites(out var scope, out var error))
        {
            BatchStatus = error!;
            return [];
        }
        if (scope == VisualBatchScope.Selected) return SelectedAssets.ToArray();
        if (scope == VisualBatchScope.Current) return AssetCards.Select(card => card.Asset).ToArray();
        if (scope == VisualBatchScope.Filter && _visualResultMode is VisualResultMode.Similarity or VisualResultMode.Color)
            return AssetCards.Select(card => card.Asset).DistinctBy(asset => asset.AssetId).ToArray();
        if (scope == VisualBatchScope.Filter && _visualResultMode == VisualResultMode.Filter && _visualFilter is not null)
        {
            var filtered = new List<AssetItem>();
            string? visualCursor = null;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = await _visualQuery.QueryAsync(new VisualAssetQuery(BuildQuery() with { Cursor = null }, _visualFilter, 200, visualCursor), cancellationToken);
                filtered.AddRange(page.Items.Select(item => item.Asset));
                visualCursor = page.NextCursor;
            } while (visualCursor is not null);
            return filtered.DistinctBy(asset => asset.AssetId).ToArray();
        }
        var query = BuildQuery() with
        {
            PageSize = 500,
            FolderId = scope == VisualBatchScope.Folder ? SelectedFolder!.FolderId : BuildQuery().FolderId
        };
        var result = new List<AssetItem>(); string? cursor = null;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await _repository.QueryAsync(query with { Cursor = cursor }, cancellationToken);
            result.AddRange(page.Items); cursor = page.NextCursor;
        } while (cursor is not null);
        return result.DistinctBy(asset => asset.AssetId).ToArray();
    }

    private Task CancelBatchAsync() { _batchCancellation?.Cancel(); BatchStatus = "正在取消批量视觉分析…"; return Task.CompletedTask; }

    private void SetSimilarityMatches(IEnumerable<VisualSimilarityMatch> matches)
    {
        AssetCards.Clear(); _visualMatchByAsset.Clear();
        foreach (var match in matches) { var card = new AssetVisualMatchView(match.Asset, match.Scores, null); AssetCards.Add(card); _visualMatchByAsset[match.Asset.AssetId] = card; }
        ReconcileSelectionWithVisibleCards();
        NotifyContentState();
    }

    private void SetColorMatches(IEnumerable<VisualAssetMatch> matches)
    {
        AssetCards.Clear(); _visualMatchByAsset.Clear();
        foreach (var match in matches) { var card = new AssetVisualMatchView(match.Asset, null, match.ColorDeltaE); AssetCards.Add(card); _visualMatchByAsset[match.Asset.AssetId] = card; }
        ReconcileSelectionWithVisibleCards();
        NotifyContentState();
    }

    private void SetAssetCards(IEnumerable<AssetItem> assets)
    {
        AssetCards.Clear(); _visualMatchByAsset.Clear();
        foreach (var asset in assets) AssetCards.Add(new(asset));
        ReconcileSelectionWithVisibleCards();
        NotifyContentState();
        RefreshBatchScopeAvailability();
    }

    private void ReconcileSelectionWithVisibleCards()
    {
        var selectedAssetId = SelectedAssets.Count == 1
            ? SelectedAssets[0].AssetId
            : _workspaceSettings.SelectedAssetId;
        var visibleCard = selectedAssetId is null
            ? null
            : AssetCards.FirstOrDefault(card => card.Asset.AssetId == selectedAssetId.Value);

        if (visibleCard is null)
        {
            if (SelectedAssets.Count > 0 || _workspaceSettings.SelectedAssetId is not null)
                SyncSelection([]);
        }
        else if (SelectedAssets.Count != 1 || SelectedAssets[0].AssetId != visibleCard.Asset.AssetId)
        {
            SyncSelection([visibleCard.Asset]);
        }

        SelectionRestoreRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateActiveVisualLabel() => VisualModeLabel = string.Join(" + ", ActiveVisualChips.Select(chip => chip.Label));

    private void AddVisualSearchHistory(VisualSearchKind kind, string label, string parameters)
    {
        VisualSearchHistory.Insert(0, new(kind, label, parameters, DateTimeOffset.UtcNow));
        while (VisualSearchHistory.Count > 10) VisualSearchHistory.RemoveAt(VisualSearchHistory.Count - 1);
    }

    private void NotifyVisualMode() { OnPropertyChanged(nameof(IsTemporaryVisualMode)); NotifyContentState(); ClearVisualModeCommand.RaiseCanExecuteChanged(); }
    private void RaiseVisualActions() { AnalyzeSelectionCommand.RaiseCanExecuteChanged(); RefreshBatchScopeAvailability(); CancelBatchCommand.RaiseCanExecuteChanged(); FindSimilarCommand.RaiseCanExecuteChanged(); }

    private void RaiseWorkspaceCommandStates()
    {
        RefreshCommand.RaiseCanExecuteChanged();
        RetryLoadCommand.RaiseCanExecuteChanged();
        ImportCommand.RaiseCanExecuteChanged();
        LoadMoreCommand.RaiseCanExecuteChanged();
        NewFolderCommand.RaiseCanExecuteChanged();
        NewSubfolderCommand.RaiseCanExecuteChanged();
        BatchFolderCommand.RaiseCanExecuteChanged();
        NewTagCommand.RaiseCanExecuteChanged();
        ApplyTagsCommand.RaiseCanExecuteChanged();
        AddFolderCommand.RaiseCanExecuteChanged();
        UndoCommand.RaiseCanExecuteChanged();
        SaveSmartFolderCommand.RaiseCanExecuteChanged();
        RelinkCommand.RaiseCanExecuteChanged();
        RateCommand.RaiseCanExecuteChanged();
        VisualChipCommand.RaiseCanExecuteChanged();
        ClearVisualModeCommand.RaiseCanExecuteChanged();
        FindSimilarCommand.RaiseCanExecuteChanged();
        AnalyzeSelectionCommand.RaiseCanExecuteChanged();
        AnalyzeVisibleCommand.RaiseCanExecuteChanged();
        CancelBatchCommand.RaiseCanExecuteChanged();
        SearchColorCommand.RaiseCanExecuteChanged();
        SearchPaletteColorCommand.RaiseCanExecuteChanged();
        FindPaletteSimilarCommand.RaiseCanExecuteChanged();
        ApplyAdvancedVisualFilterCommand.RaiseCanExecuteChanged();
        RemoveVisualChipCommand.RaiseCanExecuteChanged();
    }

    private bool IsCurrentAnalysis(AssetItem asset, long generation) =>
        generation == Volatile.Read(ref _analysisGeneration) &&
        SelectedAssets.Count == 1 &&
        SelectedAssets[0].AssetId == asset.AssetId;

    private bool CanAnalyzeVisible() => AssetCards.Count > 0 && !IsBatchAnalyzing && HasBatchScopePrerequisites(out _, out _);

    private bool HasBatchScopePrerequisites(out VisualBatchScope scope, out string? error)
    {
        if (!Enum.TryParse(BatchScope, true, out scope))
        {
            error = "批量分析未开始：未知分析范围";
            return false;
        }
        if (scope == VisualBatchScope.Selected && SelectedAssets.Count == 0)
        {
            error = "批量分析未开始：请先选择素材";
            return false;
        }
        if (scope == VisualBatchScope.Folder && SelectedFolder is null)
        {
            error = "批量分析未开始：请先选择文件夹";
            return false;
        }
        error = null;
        return true;
    }

    private void RefreshBatchScopeAvailability()
    {
        AnalyzeVisibleCommand.RaiseCanExecuteChanged();
        if (!HasBatchScopePrerequisites(out _, out var error)) BatchStatus = error!;
        else if (BatchStatus.StartsWith("批量分析未开始：", StringComparison.Ordinal)) BatchStatus = string.Empty;
    }

    private async Task RefreshSelectionSummaryAsync() { SelectedTagSummary.Clear(); if (SelectedAssets.Count == 0) return; foreach (var item in await _repository.GetTagUsageSummaryAsync(SelectedAssets.Select(x => x.AssetId))) SelectedTagSummary.Add(item); }
    private async Task RefreshSelectedFeaturesAsync(AssetItem asset)
    {
        try { var features = await _featureStore.GetFeaturesAsync(asset.AssetId); if (SelectedAssets.Count == 1 && SelectedAssets[0].AssetId == asset.AssetId) SelectedFeatures = features.Summary; }
        catch (KeyNotFoundException) { if (SelectedAssets.Count == 1 && SelectedAssets[0].AssetId == asset.AssetId) SelectedFeatures = null; }
    }
    private async Task RefreshFilterListsAsync()
    {
        Folders.Clear(); foreach (var folder in await _repository.ListFoldersAsync()) Folders.Add(folder); RefreshClassifierFolders();
        FolderTree.Clear(); foreach (var node in await _repository.GetFolderTreeAsync()) FolderTree.Add(node);
        Tags.Clear(); foreach (var tag in await _repository.ListTagsAsync()) Tags.Add(tag); TagGroups.Clear(); foreach (var group in await _repository.ListTagGroupsAsync()) TagGroups.Add(group);
        SmartFolders.Clear(); foreach (var folder in await _repository.ListSmartFoldersAsync()) SmartFolders.Add(folder);
        FavoriteFolders.Clear(); foreach (var folder in Folders.Where(x => !string.IsNullOrWhiteSpace(x.Color)).Take(6)) FavoriteFolders.Add(folder);
    }
    private void RefreshClassifierFolders() { ClassifierFolders.Clear(); foreach (var folder in Folders.Where(x => string.IsNullOrWhiteSpace(FolderSearch) || x.Name.Contains(FolderSearch, StringComparison.OrdinalIgnoreCase))) ClassifierFolders.Add(folder); }
    private async Task SeedPreviewStructureAsync() { await _repository.BatchCreateFoldersAsync("人体/身体\n人体/宗教\n参考/白棚\n参考/黑色\n灯光/硬光\n灯光/柔光"); var groups = new[] { new TagGroup(Guid.NewGuid(), "人物"), new TagGroup(Guid.NewGuid(), "视觉"), new TagGroup(Guid.NewGuid(), "概念") }; foreach (var group in groups) await _repository.SaveTagGroupAsync(group); await _repository.BatchCreateTagsAsync("身体,宗教,凝视", groups[2].TagGroupId); await _repository.BatchCreateTagsAsync("红,蓝,绿色", groups[1].TagGroupId); await RefreshFilterListsAsync(); }

    public void ClearFilters()
    {
        _searchDebounce.Stop();
        ResetVisualModeState();
        _selectedFolder = null;
        _selectedTag = null;
        _selectedSmartFolder = null;
        _searchText = string.Empty;
        _workspaceSettings.SelectedFolderId = null;
        _workspaceSettings.SelectedTagId = null;
        _workspaceSettings.SelectedSmartFolderId = null;
        _workspaceSettings.SearchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SelectedFolder));
        OnPropertyChanged(nameof(SelectedTag));
        OnPropertyChanged(nameof(SelectedSmartFolder));
        OnPropertyChanged(nameof(IsVisualQueryScopeSupported));
        OnPropertyChanged(nameof(VisualQueryScopeStatus));
        NotifyContentState();
        RaiseActions();
        RefreshBatchScopeAvailability();
        _ = RefreshAsync();
    }
    public void FocusFolderClassifier() => Status = "F：快速分类器已打开；↑↓选择、Space多选、Enter确认、Esc关闭";
    private AssetLibraryQuery BuildQuery(string? cursor = null)
    {
        int? minimumRating = int.TryParse(MinimumRatingFilterText, out var rating) ? Math.Clamp(rating, 0, 5) : null;
        DateTimeOffset? addedFrom = DateTimeOffset.TryParse(AddedFromFilterText, out var from) ? from : null;
        DateTimeOffset? addedTo = DateTimeOffset.TryParse(AddedToFilterText, out var to) ? to : null;
        return new(SearchText, SelectedFolder?.FolderId, SelectedTag?.TagId, MinimumRating: minimumRating, FileNameRegex: string.IsNullOrWhiteSpace(FileNameRegexFilterText) ? null : FileNameRegexFilterText, SmartFolderId: SelectedSmartFolder?.SmartFolderId, PageSize: 120, Cursor: cursor, AddedFrom: addedFrom, AddedTo: addedTo);
    }
    private string CurrentCollectionDiagnostic => _visualResultMode switch
    {
        VisualResultMode.Filter => "VisualFilter",
        VisualResultMode.Similarity => "VisualSimilarity",
        VisualResultMode.Color => "ColorSearch",
        _ when SelectedSmartFolder is not null => "SmartFolder",
        _ when SelectedFolder is not null => "Folder",
        _ when SelectedTag is not null => "Tag",
        _ => "AllAssets"
    };
    private static string UniqueName(string seed, IEnumerable<string> names) { var set = names.ToHashSet(StringComparer.OrdinalIgnoreCase); if (!set.Contains(seed)) return seed; for (var i = 2; ; i++) if (!set.Contains($"{seed} {i}")) return $"{seed} {i}"; }
    private void RaiseActions() { AddFolderCommand.RaiseCanExecuteChanged(); ApplyTagsCommand.RaiseCanExecuteChanged(); NewSubfolderCommand.RaiseCanExecuteChanged(); UndoCommand.RaiseCanExecuteChanged(); RateCommand.RaiseCanExecuteChanged(); }
    public ValueTask DisposeAsync() { _searchDebounce.Stop(); _analysisCancellation?.Cancel(); _analysisCancellation?.Dispose(); _queryCancellation?.Cancel(); _queryCancellation?.Dispose(); _batchCancellation?.Cancel(); _batchCancellation?.Dispose(); _analysisCoordinator.ClearSelection(); return _repository.DisposeAsync(); }

    private enum VisualResultMode { None, Filter, Similarity, Color }
}

public enum VisualBatchScope { Current, Selected, Folder, Filter }
public enum VisualContextAction { Analyze, Palette, Similarity }

public sealed record AssetVisualMatchView(AssetItem Asset, VisualSimilarityScores? Scores, double? ColorDeltaE)
{
    public AssetVisualMatchView(AssetItem asset) : this(asset, null, null) { }
    public string ThumbnailPath => !string.IsNullOrWhiteSpace(Asset.ManagedCopyPath) && File.Exists(Asset.ManagedCopyPath) ? Asset.ManagedCopyPath : Asset.SourcePath;
    public bool HasDetail => Scores is not null || ColorDeltaE is not null;
    public string Detail => Scores is not null ? $"相似 {Scores.Overall:F0} · 色 {Scores.Color:F0} · 调 {Scores.Tone:F0} · 对 {Scores.Contrast:F0} · 饱 {Scores.Saturation:F0}" : ColorDeltaE is not null ? $"ΔE76 {ColorDeltaE:F1}" : string.Empty;
}

public sealed record VisualFilterChipView(string Key, string Label, VisualAssetFilter Filter);
public enum VisualSearchKind { Filter, Color, Palette, Similarity }
public sealed record VisualSearchHistoryEntry(VisualSearchKind Kind, string Label, string Parameters, DateTimeOffset CreatedAt);
