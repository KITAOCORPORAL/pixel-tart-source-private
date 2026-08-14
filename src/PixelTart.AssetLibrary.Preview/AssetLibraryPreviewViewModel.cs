using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Utilities;

namespace PixelTart.AssetLibrary.Preview;

public sealed class AssetLibraryPreviewViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IAssetLibraryRepository _repository;
    private readonly IVisualAssetQueryService _visualQuery;
    private readonly IAssetVisualFeatureStore _featureStore;
    private readonly AssetVisualAnalysisService _visualAnalysis;
    private readonly AssetVisualAnalysisBatchProcessor _batchProcessor;
    private readonly AssetVisualAnalysisSelectionCoordinator _analysisCoordinator = new();
    private readonly PreviewImportDiagnosticsWriter _importDiagnostics;
    private CancellationTokenSource? _analysisCancellation;
    private CancellationTokenSource? _queryCancellation;
    private CancellationTokenSource? _batchCancellation;
    private long _queryGeneration;
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

    public AssetLibraryPreviewViewModel(string databasePath)
    {
        var database = new AssetLibraryDatabase(databasePath);
        _importDiagnostics = new(Environment.GetEnvironmentVariable("PIXEL_TART_ASSET_LIBRARY_ACCEPTANCE_ROOT"));
        _repository = new SqliteAssetLibraryRepository(database);
        _featureStore = new SqliteAssetVisualAnalysisCache(database);
        _visualAnalysis = new(_featureStore);
        _visualQuery = new SqliteVisualAssetQueryService(database, _featureStore);
        _batchProcessor = new(_visualAnalysis, _featureStore);
        AssetCards.CollectionChanged += (_, _) => _importDiagnostics.RecordCollectionChanged();
        _searchDebounce = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(280) };
        _searchDebounce.Tick += async (_, _) => { _searchDebounce.Stop(); await RefreshAsync(); };
        RefreshCommand = new(RefreshAsync); ImportCommand = new(ImportAsync); LoadMoreCommand = new(LoadMoreAsync, () => _nextCursor is not null);
        NewFolderCommand = new(NewFolderAsync); NewSubfolderCommand = new(NewSubfolderAsync, () => SelectedFolder is not null); BatchFolderCommand = new(BatchFolderAsync);
        NewTagCommand = new(NewTagAsync); ApplyTagsCommand = new(ApplyTagsAsync, () => SelectedAssets.Count > 0 && !string.IsNullOrWhiteSpace(TagInput));
        AddFolderCommand = new(AddFolderAsync, () => SelectedAssets.Count > 0 && SelectedFolder is not null); UndoCommand = new(UndoAsync, () => LastUndoToken is not null);
        SaveSmartFolderCommand = new(SaveSmartFolderAsync); RelinkCommand = new(RelinkAsync); RateCommand = new AsyncCommand<int>(value => RateSelectedAsync(value));
        VisualChipCommand = new AsyncCommand<string>(ApplyVisualChipAsync); ClearVisualModeCommand = new(ClearVisualModeAsync, () => IsTemporaryVisualMode);
        FindSimilarCommand = new(FindSimilarAsync, () => SelectedAssets.Count == 1 && SelectedFeatures?.State == AssetVisualFeatureState.Valid);
        AnalyzeSelectionCommand = new(AnalyzeSelectionCanonicalAsync, () => SelectedAssets.Count == 1);
        AnalyzeVisibleCommand = new(AnalyzeVisibleAsync, () => AssetCards.Count > 0 && !IsBatchAnalyzing);
        CancelBatchCommand = new(CancelBatchAsync, () => IsBatchAnalyzing);
        SearchColorCommand = new(SearchColorAsync, () => IsVisualQueryScopeSupported); SearchPaletteColorCommand = new(SearchPaletteColorAsync, _ => IsVisualQueryScopeSupported); FindPaletteSimilarCommand = new(FindPaletteSimilarAsync, () => SelectedAssets.Count == 1 && Analysis is not null && IsVisualQueryScopeSupported);
        ApplyAdvancedVisualFilterCommand = new(ApplyAdvancedVisualFilterAsync); RemoveVisualChipCommand = new(RemoveVisualChipAsync);
    }

    public ObservableCollection<AssetVisualMatchView> AssetCards { get; } = [];
    public PreviewImportDiagnostics ImportDiagnostics => _importDiagnostics.Snapshot;
    public void UpdateAssetGridDiagnostics(int itemCount, string itemsSourceInstance, bool itemsSourceIsViewModelCollection, string dataContextType) =>
        _importDiagnostics.SetBindingState(itemCount, itemsSourceInstance, itemsSourceIsViewModelCollection, dataContextType, CurrentCollectionDiagnostic);
    public IEnumerable<AssetItem> Assets => AssetCards.Select(card => card.Asset);
    public string GetDisplaySourcePath(AssetItem? asset) => asset is not null && !string.IsNullOrWhiteSpace(asset.ManagedCopyPath) && File.Exists(asset.ManagedCopyPath) ? asset.ManagedCopyPath : asset?.SourcePath ?? string.Empty;
    public string SelectedAssetThumbnailPath => GetDisplaySourcePath(SelectedAsset);
    public ObservableCollection<VisualFilterChipView> ActiveVisualChips { get; } = [];
    public ObservableCollection<VisualSearchHistoryEntry> VisualSearchHistory { get; } = [];
    public ObservableCollection<AssetItem> SelectedAssets { get; } = [];
    public ObservableCollection<AssetFolderTreeItem> FolderTree { get; } = [];
    public ObservableCollection<AssetFolder> Folders { get; } = [];
    public ObservableCollection<AssetFolder> ClassifierFolders { get; } = [];
    public ObservableCollection<AssetFolder> RecentFolders { get; } = [];
    public ObservableCollection<AssetFolder> FavoriteFolders { get; } = [];
    public ObservableCollection<AssetTag> Tags { get; } = [];
    public ObservableCollection<TagGroup> TagGroups { get; } = [];
    public ObservableCollection<SmartFolder> SmartFolders { get; } = [];
    public ObservableCollection<AssetTagUsageSummary> SelectedTagSummary { get; } = [];
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) { _searchDebounce.Stop(); _searchDebounce.Start(); } } }
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
    public int VisibleCount => AssetCards.Count;
    public int SelectionCount => SelectedAssets.Count;
    public bool HasMultipleSelection => SelectionCount > 1;
    public bool HasSingleSelection => SelectionCount == 1;
    public double ThumbnailWidth { get => _thumbnailWidth; set => SetProperty(ref _thumbnailWidth, Math.Clamp(value, 120, 280)); }
    public int PaletteSize { get => _paletteSize; set { if (SetProperty(ref _paletteSize, value) && SelectedAsset is not null) _ = AnalyzeSelectionCanonicalAsync(); } }
    public AssetVisualAnalysisResult? Analysis { get => _analysis; private set { if (SetProperty(ref _analysis, value)) OnPropertyChanged(nameof(AnalysisStatus)); } }
    public bool IsAnalyzing { get => _isAnalyzing; private set { if (SetProperty(ref _isAnalyzing, value)) OnPropertyChanged(nameof(AnalysisStatus)); } }
    public string AnalysisStatus => HasMultipleSelection ? $"已选择 {SelectionCount} 张图片；不会用首张分析冒充整组。" : IsAnalyzing ? "正在分析视觉数据…" : Analysis is null ? "选择一张图片查看本地视觉统计" : Analysis.CacheHit ? "视觉分析已从缓存读取" : "视觉分析完成并缓存";
    public AssetVisualFeatureSummary? SelectedFeatures { get => _selectedFeatures; private set { if (SetProperty(ref _selectedFeatures, value)) { OnPropertyChanged(nameof(FeatureStatus)); FindSimilarCommand.RaiseCanExecuteChanged(); } } }
    public string FeatureStatus => SelectedFeatures is null ? "未分析" : SelectedFeatures.State switch { AssetVisualFeatureState.Valid => "已分析 · 当前", AssetVisualFeatureState.Stale => "分析已过期 · 需要重新分析", AssetVisualFeatureState.Failed => $"分析失败 · {SelectedFeatures.FailureReason}", _ => "未分析" };
    public bool IsTemporaryVisualMode => _visualResultMode != VisualResultMode.None;
    public string VisualModeLabel { get => _visualModeLabel; private set => SetProperty(ref _visualModeLabel, value); }
    public bool IsBatchAnalyzing { get => _isBatchAnalyzing; private set { if (SetProperty(ref _isBatchAnalyzing, value)) RaiseVisualActions(); } }
    public string BatchStatus { get => _batchStatus; private set => SetProperty(ref _batchStatus, value); }
    public string BatchScope { get => _batchScope; set => SetProperty(ref _batchScope, value); }
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
    public AssetFolder? SelectedFolder { get => _selectedFolder; set { if (SetProperty(ref _selectedFolder, value)) { RaiseActions(); _ = RefreshAsync(); } } }
    public AssetTag? SelectedTag { get => _selectedTag; set { if (SetProperty(ref _selectedTag, value)) _ = RefreshAsync(); } }
    public SmartFolder? SelectedSmartFolder { get => _selectedSmartFolder; set { if (SetProperty(ref _selectedSmartFolder, value)) { OnPropertyChanged(nameof(IsVisualQueryScopeSupported)); OnPropertyChanged(nameof(VisualQueryScopeStatus)); SearchColorCommand.RaiseCanExecuteChanged(); SearchPaletteColorCommand.RaiseCanExecuteChanged(); FindPaletteSimilarCommand.RaiseCanExecuteChanged(); _ = RefreshAsync(); } } }
    public AsyncCommand RefreshCommand { get; } public AsyncCommand ImportCommand { get; } public AsyncCommand LoadMoreCommand { get; }
    public AsyncCommand NewFolderCommand { get; } public AsyncCommand NewSubfolderCommand { get; } public AsyncCommand BatchFolderCommand { get; }
    public AsyncCommand NewTagCommand { get; } public AsyncCommand ApplyTagsCommand { get; } public AsyncCommand AddFolderCommand { get; }
    public AsyncCommand UndoCommand { get; } public AsyncCommand SaveSmartFolderCommand { get; } public AsyncCommand RelinkCommand { get; } public AsyncCommand<int> RateCommand { get; }
    public AsyncCommand<string> VisualChipCommand { get; } public AsyncCommand ClearVisualModeCommand { get; } public AsyncCommand FindSimilarCommand { get; }
    public AsyncCommand AnalyzeSelectionCommand { get; } public AsyncCommand AnalyzeVisibleCommand { get; } public AsyncCommand CancelBatchCommand { get; }
    public AsyncCommand SearchColorCommand { get; } public AsyncCommand<string> SearchPaletteColorCommand { get; } public AsyncCommand FindPaletteSimilarCommand { get; }
    public AsyncCommand ApplyAdvancedVisualFilterCommand { get; } public AsyncCommand<string> RemoveVisualChipCommand { get; }

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
        await _repository.InitializeAsync(); await RefreshFilterListsAsync();
        if (Folders.Count == 0) await SeedPreviewStructureAsync();
        await RefreshAsync();
        var journal = await _repository.ListUndoJournalAsync(1); LastUndoToken = journal.FirstOrDefault(x => !x.IsUndone)?.Token; RaiseActions();
    }

    public void SyncSelection(IEnumerable<AssetItem> items)
    {
        var selected = items.DistinctBy(x => x.AssetId).ToArray();
        SelectedAssets.Clear(); foreach (var item in selected) SelectedAssets.Add(item);
        if (selected.Length != 1) { _selectedAsset = selected.FirstOrDefault(); OnPropertyChanged(nameof(SelectedAsset)); _analysisCoordinator.ClearSelection(); Analysis = null; SelectedFeatures = null; IsAnalyzing = false; }
        else if (_selectedAsset?.AssetId != selected[0].AssetId) { _selectedAsset = selected[0]; OnPropertyChanged(nameof(SelectedAsset)); }
        OnPropertyChanged(nameof(SelectedAssetThumbnailPath));
        OnPropertyChanged(nameof(SelectionCount)); OnPropertyChanged(nameof(HasMultipleSelection)); OnPropertyChanged(nameof(HasSingleSelection)); OnPropertyChanged(nameof(AnalysisStatus));
        _ = RefreshSelectionSummaryAsync(); if (selected.Length == 1) { _ = RefreshSelectedFeaturesAsync(selected[0]); _ = AnalyzeSelectionCanonicalAsync(); } RaiseActions(); RaiseVisualActions();
    }

    private async Task RefreshAsync()
    {
        _queryCancellation?.Cancel(); _queryCancellation?.Dispose(); _queryCancellation = new(); var token = _queryCancellation.Token; var generation = Interlocked.Increment(ref _queryGeneration);
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

    private async Task AnalyzeSelectedAsync()
    {
        var asset = SelectedAssets.Count == 1 ? SelectedAssets[0] : null; if (asset is null) return;
        _analysisCancellation?.Cancel(); _analysisCancellation?.Dispose(); _analysisCancellation = new(); var cancellationToken = _analysisCancellation.Token;
        IsAnalyzing = true; Analysis = null;
        try
        {
            var request = await WpfVisualAnalysisDecoder.DecodeAsync(asset, PaletteSize, cancellationToken);
            await _analysisCoordinator.AnalyzeSelectionAsync(asset.AssetId, token => _visualAnalysis.AnalyzeAsync(request, token), result =>
            {
                if (!cancellationToken.IsCancellationRequested && SelectedAssets.Count == 1 && SelectedAssets[0].AssetId == result.AssetId) Analysis = result;
            }, cancellationToken);
            if (!cancellationToken.IsCancellationRequested && SelectedAssets.Count == 1 && SelectedAssets[0].AssetId == asset.AssetId) SelectedFeatures = (await _featureStore.GetFeaturesAsync(asset.AssetId, cancellationToken)).Summary;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException) { Status = ex.Message; }
        finally { if (SelectedAssets.Count == 1 && SelectedAssets[0].AssetId == asset.AssetId) IsAnalyzing = false; }
    }

    private async Task AnalyzeSelectionCanonicalAsync()
    {
        var asset = SelectedAssets.Count == 1 ? SelectedAssets[0] : null; if (asset is null) return;
        _analysisCancellation?.Cancel(); _analysisCancellation?.Dispose(); _analysisCancellation = new(); var token = _analysisCancellation.Token;
        IsAnalyzing = true;
        try
        {
            var result = await _batchProcessor.AnalyzeInteractiveAsync(asset.AssetId, ct => WpfVisualAnalysisDecoder.DecodeAsync(asset, AssetVisualFeatureContract.PaletteSize, ct, AssetVisualFeatureContract.PaletteSort), token);
            if (SelectedAssets.Count == 1 && SelectedAssets[0].AssetId == asset.AssetId) { Analysis = result; SelectedFeatures = (await _featureStore.GetFeaturesAsync(asset.AssetId, token)).Summary; }
            Status = "已重新生成当前素材的 canonical 视觉特征";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception) { Status = $"视觉分析失败：{exception.Message}"; }
        finally { IsAnalyzing = false; RaiseVisualActions(); }
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
        var existing = ActiveVisualChips.FirstOrDefault(item => item.Key == key);
        if (existing is not null) { ActiveVisualChips.Remove(existing); _visualFilterStack.Remove(existing.Filter); }
        ActiveVisualChips.Add(new(key, selected.Label, selected.Filter)); _visualFilterStack.Add(selected.Filter); _visualFilter = CombineVisualFilters(_visualFilterStack);
        UpdateActiveVisualLabel(); AddVisualSearchHistory(VisualSearchKind.Filter, selected.Label, key);
        NotifyVisualMode(); await RefreshAsync();
    }

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
        _visualResultMode = VisualResultMode.None; _visualFilter = null; _visualFilterStack.Clear(); ActiveVisualChips.Clear(); _similarityQuery = null; _colorQuery = null; _visualMatchByAsset.Clear(); VisualModeLabel = string.Empty; NotifyVisualMode(); await RefreshAsync();
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
        var progress = new Progress<VisualAnalysisBatchProgress>(value => BatchStatus = $"视觉批量分析 {value.Completed}/{value.Total} · 成功 {value.Succeeded} · 失败 {value.Failed} · 取消 {value.Cancelled}");
        try
        {
            var selectedIds = SelectedAssets.Select(asset => asset.AssetId).ToHashSet();
            var interactive = assets.Where(asset => selectedIds.Contains(asset.AssetId)).ToArray();
            var background = assets.Where(asset => !selectedIds.Contains(asset.AssetId)).ToArray();
            var succeeded = 0; var failed = 0; var cancelled = 0;
            foreach (var asset in interactive)
            {
                token.ThrowIfCancellationRequested();
                try { await _batchProcessor.AnalyzeInteractiveAsync(asset.AssetId, ct => WpfVisualAnalysisDecoder.DecodeAsync(asset, AssetVisualFeatureContract.PaletteSize, ct, AssetVisualFeatureContract.PaletteSort), token); succeeded++; }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { cancelled++; break; }
                catch { failed++; }
                BatchStatus = $"Interactive {succeeded + failed + cancelled}/{assets.Count} · 成功 {succeeded} · 失败 {failed}";
            }
            if (!token.IsCancellationRequested && background.Length > 0)
            {
                var result = await _batchProcessor.ProcessAsync(background.Select(asset => new VisualAnalysisBatchItem(asset.AssetId, asset.ContentHash, VisualAnalysisPriority.Background, ct => WpfVisualAnalysisDecoder.DecodeAsync(asset, AssetVisualFeatureContract.PaletteSize, ct, AssetVisualFeatureContract.PaletteSort))), progress, token);
                succeeded += result.Succeeded; failed += result.Failed; cancelled += result.CancelledCount;
            }
            BatchStatus = $"批量完成：{assets.Count} 项 · 成功 {succeeded} · 失败 {failed} · 取消 {cancelled}";
            await RefreshAsync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { BatchStatus = "批量分析已取消"; }
        finally { IsBatchAnalyzing = false; }
    }

    private async Task<IReadOnlyList<AssetItem>> ResolveBatchAssetsAsync(CancellationToken cancellationToken)
    {
        if (Enum.TryParse<VisualBatchScope>(BatchScope, true, out var scope))
        {
            if (scope == VisualBatchScope.Selected) return SelectedAssets.ToArray();
            if (scope == VisualBatchScope.Current) return AssetCards.Select(card => card.Asset).ToArray();
        }
        var query = BuildQuery() with { PageSize = 500, FolderId = string.Equals(BatchScope, nameof(VisualBatchScope.Folder), StringComparison.OrdinalIgnoreCase) ? SelectedFolder?.FolderId : BuildQuery().FolderId, SmartFolderId = string.Equals(BatchScope, nameof(VisualBatchScope.Filter), StringComparison.OrdinalIgnoreCase) ? null : BuildQuery().SmartFolderId };
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
    }

    private void SetColorMatches(IEnumerable<VisualAssetMatch> matches)
    {
        AssetCards.Clear(); _visualMatchByAsset.Clear();
        foreach (var match in matches) { var card = new AssetVisualMatchView(match.Asset, null, match.ColorDeltaE); AssetCards.Add(card); _visualMatchByAsset[match.Asset.AssetId] = card; }
    }

    private void SetAssetCards(IEnumerable<AssetItem> assets)
    {
        AssetCards.Clear(); _visualMatchByAsset.Clear();
        foreach (var asset in assets) AssetCards.Add(new(asset));
    }

    private void UpdateActiveVisualLabel() => VisualModeLabel = string.Join(" + ", ActiveVisualChips.Select(chip => chip.Label));

    private void AddVisualSearchHistory(VisualSearchKind kind, string label, string parameters)
    {
        VisualSearchHistory.Insert(0, new(kind, label, parameters, DateTimeOffset.UtcNow));
        while (VisualSearchHistory.Count > 10) VisualSearchHistory.RemoveAt(VisualSearchHistory.Count - 1);
    }

    private void NotifyVisualMode() { OnPropertyChanged(nameof(IsTemporaryVisualMode)); ClearVisualModeCommand.RaiseCanExecuteChanged(); }
    private void RaiseVisualActions() { AnalyzeSelectionCommand.RaiseCanExecuteChanged(); AnalyzeVisibleCommand.RaiseCanExecuteChanged(); CancelBatchCommand.RaiseCanExecuteChanged(); FindSimilarCommand.RaiseCanExecuteChanged(); }

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

    public void ClearFilters() { _selectedFolder = null; _selectedTag = null; _selectedSmartFolder = null; SearchText = string.Empty; OnPropertyChanged(nameof(SelectedFolder)); OnPropertyChanged(nameof(SelectedTag)); OnPropertyChanged(nameof(SelectedSmartFolder)); _ = RefreshAsync(); }
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
    private void RaiseActions() { AddFolderCommand.RaiseCanExecuteChanged(); ApplyTagsCommand.RaiseCanExecuteChanged(); NewSubfolderCommand.RaiseCanExecuteChanged(); UndoCommand.RaiseCanExecuteChanged(); }
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
