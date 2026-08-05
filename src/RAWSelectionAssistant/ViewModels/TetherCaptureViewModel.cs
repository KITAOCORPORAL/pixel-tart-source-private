using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Tethering;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed class TetherCaptureViewModel : ObservableObject, IAsyncDisposable
{
    private readonly WatchFolderCameraAdapter _adapter;
    private readonly ITetherSessionRepository _sessionRepository;
    private readonly ITetherAssetRepository _assetRepository;
    private readonly ITetherProxyCache _proxyCache;
    private readonly IDialogService _dialogs;
    private readonly ITetherAnnotationService _annotationService;
    private readonly IPreviewImageLoader _previewLoader;
    private readonly IFullResolutionImageLoader _fullResolutionLoader;
    private readonly IHistogramService _histogramService;
    private readonly IClippingOverlayService _clippingService;
    private readonly IPreviewRequestCoordinator _requestCoordinator;
    private readonly ITetherExifService _exifService;
    private readonly ITetherDisplaySettingsStore _displaySettingsStore;
    private readonly LiveSelectionCoordinator _selectionCoordinator = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<Guid, TetherAssetItemViewModel> _assetIndex = [];
    private readonly HashSet<Guid> _knownReadyAssets = [];
    private readonly HashSet<Task> _backgroundTasks = [];
    private readonly object _backgroundSync = new();
    private readonly DispatcherTimer _blinkTimer;
    private ICameraSession? _activeSession;
    private string _watchDirectory = string.Empty;
    private string _projectDestination = string.Empty;
    private string _backupDestination = string.Empty;
    private bool _importExisting;
    private bool _copyToProject;
    private bool _copyToBackup;
    private bool _verifySha256;
    private bool _isRunning;
    private bool _isBusy;
    private int _existingCandidateCount;
    private int _queueDepth;
    private bool _hasRecoverableSession;
    private string _statusText = "尚未启动。默认 Provider 为 None。";
    private TetherAssetItemViewModel? _selectedAsset;
    private TetherAssetItemViewModel? _compareCandidate;
    private TetherAssetItemViewModel? _selectionBeforeCompare;
    private BitmapSource? _currentImage;
    private BitmapSource? _comparisonPrimaryImage;
    private BitmapSource? _comparisonSecondaryImage;
    private BitmapSource? _clippingOverlay;
    private BitmapSource? _referenceImage;
    private TetherHistogramData? _histogram;
    private TetherExifInfo? _exifInfo;
    private bool _isPreviewLoading;
    private double _previewProgress;
    private string _previewStatus = "选择一张已就绪照片开始监看。";
    private TetherPreviewMode _previewMode = TetherPreviewMode.Fit;
    private TetherCompareMode _compareMode;
    private double _zoom = 1;
    private double _panX;
    private double _panY;
    private bool _autoLatest = true;
    private bool _isCurrentLocked;
    private bool _isFullScreen;
    private bool _isInspectorCollapsed;
    private bool _showInspectorDrawer;
    private bool _showFullPath;
    private bool _highlightWarningEnabled;
    private bool _shadowWarningEnabled;
    private int _highlightThreshold = 250;
    private int _shadowThreshold = 5;
    private bool _showLuminanceHistogram = true;
    private bool _comparisonSyncZoom = true;
    private bool _comparisonSyncPan = true;
    private double _comparisonOpacity = .5;
    private bool _comparisonBlink;
    private bool _blinkVisible = true;
    private bool _referenceVisible;
    private double _referenceOpacity = .45;
    private double _referenceScale = 1;
    private double _referenceOffsetX;
    private double _referenceOffsetY;
    private bool _referenceFlipHorizontal;
    private bool _referenceLocked;
    private string? _referencePath;
    private string _referenceStatus = "未选择参考图。";
    private TetherGuideMode _guideMode;
    private TetherCanvasTone _canvasTone = TetherCanvasTone.DarkGray;
    private TetherAssetFilter _selectedFilter;
    private TetherAssetSort _selectedSort;
    private int _currentRating;
    private string? _currentColorLabel;
    private string? _photographerNote;
    private string? _clientNote;
    private bool _clientFavorite;
    private bool _isRejected;
    private bool _isAnnotationSaving;
    private string _annotationStatus = "标注保存在当前电脑的项目数据库中。";
    private bool _suppressManualSelection;
    private bool _reviewStateActive;

    public TetherCaptureViewModel(
        WatchFolderCameraAdapter adapter,
        ITetherSessionRepository sessionRepository,
        ITetherAssetRepository assetRepository,
        ITetherProxyCache proxyCache,
        IDialogService dialogs,
        ITetherAnnotationService? annotationService = null,
        IPreviewImageLoader? previewLoader = null,
        IFullResolutionImageLoader? fullResolutionLoader = null,
        IHistogramService? histogramService = null,
        IClippingOverlayService? clippingService = null,
        IPreviewRequestCoordinator? requestCoordinator = null,
        ITetherExifService? exifService = null,
        ITetherDisplaySettingsStore? displaySettingsStore = null,
        IPreviewMemoryManager? memoryManager = null,
        TetherColorViewModel? color = null)
    {
        _adapter = adapter;
        _sessionRepository = sessionRepository;
        _assetRepository = assetRepository;
        _proxyCache = proxyCache;
        _dialogs = dialogs;
        _annotationService = annotationService ?? new NullTetherAnnotationService();
        _previewLoader = previewLoader ?? new PreviewImageLoader(proxyCache, assetRepository);
        var effectiveMemory = memoryManager ?? new PreviewMemoryManager();
        _fullResolutionLoader = fullResolutionLoader ?? new FullResolutionImageLoader(assetRepository, effectiveMemory);
        _histogramService = histogramService ?? new HistogramService();
        _clippingService = clippingService ?? new ClippingOverlayService();
        _requestCoordinator = requestCoordinator ?? new PreviewRequestCoordinator();
        _exifService = exifService ?? new TetherExifService();
        _displaySettingsStore = displaySettingsStore ?? new JsonTetherDisplaySettingsStore();
        ColorSettings = color ?? new TetherColorViewModel(dialogs);
        ColorSettings.AttachClientAnnotationHandler(SaveClientAnnotationFromMonitorAsync);
        ColorSettings.AttachAssetImageLoader(LoadAssetImageForClientAsync);

        AssetsView = CollectionViewSource.GetDefaultView(Assets);
        AssetsView.Filter = FilterAsset;
        ApplySort();
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _blinkTimer.Tick += (_, _) => { _blinkVisible = !_blinkVisible; OnPropertyChanged(nameof(ComparisonSecondaryOpacity)); };

        ChooseWatchFolderCommand = new RelayCommand(_ => ChooseWatchFolder(), _ => !IsRunning && !IsBusy);
        PreviewExistingCommand = new RelayCommand(_ => CountExisting(), _ => !IsRunning && Directory.Exists(WatchDirectory));
        ChooseProjectDestinationCommand = new RelayCommand(_ => ChooseProjectDestination(), _ => !IsRunning && !IsBusy);
        ChooseBackupDestinationCommand = new RelayCommand(_ => ChooseBackupDestination(), _ => !IsRunning && !IsBusy);
        StartCommand = new AsyncRelayCommand(_ => StartAsync(), _ => !IsRunning && !IsBusy && Directory.Exists(WatchDirectory));
        StopCommand = new AsyncRelayCommand(_ => StopAsync(), _ => IsRunning && !IsBusy);
        ReconcileCommand = new AsyncRelayCommand(_ => ReconcileAsync(), _ => IsRunning && !IsBusy);
        ClearProxyCacheCommand = new AsyncRelayCommand(_ => ClearProxyCacheAsync(), _ => !IsBusy);
        RevealAssetCommand = new RelayCommand(value => RevealAsset(value as TetherAssetItemViewModel), value => value is TetherAssetItemViewModel);
        PreviousCommand = new RelayCommand(_ => SelectRelative(1), _ => AssetsView.Cast<object>().Any());
        NextCommand = new RelayCommand(_ => SelectRelative(-1), _ => AssetsView.Cast<object>().Any());
        FitCommand = new RelayCommand(_ => SetPreviewMode(TetherPreviewMode.Fit));
        FillCommand = new RelayCommand(_ => SetPreviewMode(TetherPreviewMode.Fill));
        ActualSizeCommand = new AsyncRelayCommand(_ => LoadActualSizeAsync(), _ => SelectedAsset is not null && !IsPreviewLoading && CompareMode == TetherCompareMode.None);
        ResetViewCommand = new RelayCommand(_ => ResetView());
        ToggleLockCommand = new RelayCommand(_ => IsCurrentLocked = !IsCurrentLocked, _ => SelectedAsset is not null);
        UnlockLatestCommand = new RelayCommand(_ => UnlockAndSelectLatest(), _ => NewAssetCount > 0 || IsCurrentLocked);
        ToggleFullScreenCommand = new RelayCommand(_ => IsFullScreen = !IsFullScreen);
        ToggleInspectorCommand = new RelayCommand(_ => ShowInspectorDrawer = !ShowInspectorDrawer);
        SetRatingCommand = new AsyncRelayCommand(SetRatingAsync, _ => SelectedAsset is not null && !IsAnnotationSaving);
        SetColorLabelCommand = new AsyncRelayCommand(SetColorLabelAsync, _ => SelectedAsset is not null && !IsAnnotationSaving);
        SaveNotesCommand = new AsyncRelayCommand(_ => SaveAnnotationAsync(), _ => SelectedAsset is not null && !IsAnnotationSaving);
        ToggleFavoriteCommand = new AsyncRelayCommand(_ => ToggleFavoriteAsync(), _ => SelectedAsset is not null && !IsAnnotationSaving);
        ToggleRejectedCommand = new AsyncRelayCommand(_ => ToggleRejectedAsync(), _ => SelectedAsset is not null && !IsAnnotationSaving);
        SetCompareCandidateCommand = new RelayCommand(value => SetCompareCandidate(value as TetherAssetItemViewModel), value => value is TetherAssetItemViewModel);
        StartSideBySideCommand = new AsyncRelayCommand(_ => StartComparisonAsync(TetherCompareMode.SideBySide), _ => CanStartComparison());
        StartOverlayCommand = new AsyncRelayCommand(_ => StartComparisonAsync(TetherCompareMode.Overlay), _ => CanStartComparison());
        ExitComparisonCommand = new RelayCommand(_ => ExitComparison(), _ => CompareMode != TetherCompareMode.None);
        SwapComparisonCommand = new RelayCommand(_ => SwapComparison(), _ => CompareMode != TetherCompareMode.None);
        UseCompareCandidateAsPrimaryCommand = new RelayCommand(_ => UseCompareCandidateAsPrimary(), _ => CompareMode != TetherCompareMode.None && CompareCandidate is not null);
        ChooseReferenceCommand = new AsyncRelayCommand(_ => ChooseReferenceAsync());
        RelocateReferenceCommand = new AsyncRelayCommand(_ => ChooseReferenceAsync());
        ClearReferenceCommand = new AsyncRelayCommand(_ => ClearReferenceAsync(), _ => ReferenceImage is not null || !string.IsNullOrWhiteSpace(ReferencePath));
        RefreshAnalysisCommand = new AsyncRelayCommand(_ => RefreshAnalysisAsync(), _ => CurrentImage is not null && !IsPreviewLoading);
        TogglePathCommand = new RelayCommand(_ => ShowFullPath = !ShowFullPath);
    }

    public ObservableCollection<TetherAssetItemViewModel> Assets { get; } = [];
    public TetherColorViewModel ColorSettings { get; }
    public ICollectionView AssetsView { get; }
    public IReadOnlyList<TetherChoice<TetherAssetFilter>> FilterOptions { get; } =
    [
        new(TetherAssetFilter.All, "全部"), new(TetherAssetFilter.JpegOnly, "仅JPG"), new(TetherAssetFilter.RawOnly, "仅RAW"),
        new(TetherAssetFilter.Paired, "已配对"), new(TetherAssetFilter.Unpaired, "未配对"), new(TetherAssetFilter.Favorites, "收藏"),
        new(TetherAssetFilter.Rated, "有星级"), new(TetherAssetFilter.Rejected, "已拒绝"), new(TetherAssetFilter.NeedsAttention, "需处理")
    ];
    public IReadOnlyList<TetherChoice<TetherAssetSort>> SortOptions { get; } =
    [
        new(TetherAssetSort.NewestFirst, "最新优先"), new(TetherAssetSort.OldestFirst, "最早优先"), new(TetherAssetSort.FileName, "文件名"),
        new(TetherAssetSort.Rating, "星级"), new(TetherAssetSort.Status, "状态")
    ];
    public IReadOnlyList<TetherChoice<TetherGuideMode>> GuideOptions { get; } =
    [
        new(TetherGuideMode.None, "无"), new(TetherGuideMode.Thirds, "三分法"), new(TetherGuideMode.CenterCross, "中心十字"),
        new(TetherGuideMode.Square, "方形"), new(TetherGuideMode.Ratio4x5, "4:5"), new(TetherGuideMode.Ratio3x4, "3:4"),
        new(TetherGuideMode.Ratio2x3, "2:3"), new(TetherGuideMode.Ratio16x9, "16:9"), new(TetherGuideMode.Ratio9x16, "9:16"), new(TetherGuideMode.SafeArea, "安全边界")
    ];
    public IReadOnlyList<TetherChoice<TetherCanvasTone>> CanvasToneOptions { get; } =
    [
        new(TetherCanvasTone.Black, "黑色"), new(TetherCanvasTone.DarkGray, "深灰"), new(TetherCanvasTone.MidGray, "中灰"), new(TetherCanvasTone.Checkerboard, "PNG棋盘格")
    ];
    public IReadOnlyList<string> ColorLabels { get; } = ["", "红", "黄", "绿", "蓝", "紫"];

    public RelayCommand ChooseWatchFolderCommand { get; }
    public RelayCommand PreviewExistingCommand { get; }
    public RelayCommand ChooseProjectDestinationCommand { get; }
    public RelayCommand ChooseBackupDestinationCommand { get; }
    public AsyncRelayCommand StartCommand { get; }
    public AsyncRelayCommand StopCommand { get; }
    public AsyncRelayCommand ReconcileCommand { get; }
    public AsyncRelayCommand ClearProxyCacheCommand { get; }
    public RelayCommand RevealAssetCommand { get; }
    public RelayCommand PreviousCommand { get; }
    public RelayCommand NextCommand { get; }
    public RelayCommand FitCommand { get; }
    public RelayCommand FillCommand { get; }
    public AsyncRelayCommand ActualSizeCommand { get; }
    public RelayCommand ResetViewCommand { get; }
    public RelayCommand ToggleLockCommand { get; }
    public RelayCommand UnlockLatestCommand { get; }
    public RelayCommand ToggleFullScreenCommand { get; }
    public RelayCommand ToggleInspectorCommand { get; }
    public AsyncRelayCommand SetRatingCommand { get; }
    public AsyncRelayCommand SetColorLabelCommand { get; }
    public AsyncRelayCommand SaveNotesCommand { get; }
    public AsyncRelayCommand ToggleFavoriteCommand { get; }
    public AsyncRelayCommand ToggleRejectedCommand { get; }
    public RelayCommand SetCompareCandidateCommand { get; }
    public AsyncRelayCommand StartSideBySideCommand { get; }
    public AsyncRelayCommand StartOverlayCommand { get; }
    public RelayCommand ExitComparisonCommand { get; }
    public RelayCommand SwapComparisonCommand { get; }
    public RelayCommand UseCompareCandidateAsPrimaryCommand { get; }
    public AsyncRelayCommand ChooseReferenceCommand { get; }
    public AsyncRelayCommand RelocateReferenceCommand { get; }
    public AsyncRelayCommand ClearReferenceCommand { get; }
    public AsyncRelayCommand RefreshAnalysisCommand { get; }
    public RelayCommand TogglePathCommand { get; }

    public string WatchDirectory { get => _watchDirectory; set { if (SetProperty(ref _watchDirectory, value)) { ExistingCandidateCount = 0; RefreshCommands(); } } }
    public string ProjectDestination { get => _projectDestination; set { if (SetProperty(ref _projectDestination, value)) RefreshCommands(); } }
    public string BackupDestination { get => _backupDestination; set { if (SetProperty(ref _backupDestination, value)) RefreshCommands(); } }
    public bool ImportExisting { get => _importExisting; set => SetProperty(ref _importExisting, value); }
    public bool CopyToProject { get => _copyToProject; set { if (SetProperty(ref _copyToProject, value)) { OnPropertyChanged(nameof(CopyStatusText)); RefreshCommands(); } } }
    public bool CopyToBackup { get => _copyToBackup; set { if (SetProperty(ref _copyToBackup, value)) { OnPropertyChanged(nameof(CopyStatusText)); RefreshCommands(); } } }
    public bool VerifySha256 { get => _verifySha256; set => SetProperty(ref _verifySha256, value); }
    public bool IsRunning { get => _isRunning; private set { if (SetProperty(ref _isRunning, value)) { OnPropertyChanged(nameof(ProviderText)); RefreshCommands(); } } }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) RefreshCommands(); } }
    public int ExistingCandidateCount { get => _existingCandidateCount; private set => SetProperty(ref _existingCandidateCount, value); }
    public int QueueDepth { get => _queueDepth; private set => SetProperty(ref _queueDepth, value); }
    public bool HasRecoverableSession { get => _hasRecoverableSession; private set { if (SetProperty(ref _hasRecoverableSession, value)) OnPropertyChanged(nameof(StartButtonText)); } }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string StartButtonText => HasRecoverableSession ? "恢复目录并继续" : "启动看守";
    public string ProviderText => IsRunning ? "Watch Folder" : "None";
    public bool IncludeSubdirectories => false;
    public int ReadyCount => Assets.Count(item => item.Record.ProcessingState is TetherProcessingState.Ready or TetherProcessingState.Copied);
    public int AttentionCount => Assets.Count(item => item.Record.ProcessingState is TetherProcessingState.NeedsAttention or TetherProcessingState.PartiallyCompleted);
    public int DiscoveredCount => Assets.Count;
    public int WaitingStableCount => Assets.Count(item => item.Record.StabilityState is TetherStabilityState.Pending or TetherStabilityState.Probing);
    public int FailedCount => Assets.Count(item => item.Record.ProcessingState == TetherProcessingState.Failed || item.Record.PreviewState == TetherPreviewState.Failed);
    public string CopyStatusText => !CopyToProject && !CopyToBackup ? "复制：关闭" : $"复制：{Assets.Count(item => item.Record.ProcessingState == TetherProcessingState.Copied)} 完成 / {AttentionCount} 需处理";

    public TetherAssetFilter SelectedFilter { get => _selectedFilter; set { if (SetProperty(ref _selectedFilter, value)) AssetsView.Refresh(); } }
    public TetherAssetSort SelectedSort { get => _selectedSort; set { if (SetProperty(ref _selectedSort, value)) ApplySort(); } }
    public TetherAssetItemViewModel? SelectedAsset
    {
        get => _selectedAsset;
        set
        {
            if (!SetProperty(ref _selectedAsset, value)) return;
            if (value is not null && !_suppressManualSelection) _selectionCoordinator.SelectManually(value.Record.Id);
            OnPropertyChanged(nameof(HasSelection));
            RefreshCommands();
            if (value is not null) Track(LoadSelectedAsync(value, _lifetime.Token));
        }
    }
    public bool HasSelection => SelectedAsset is not null;
    public TetherAssetItemViewModel? CompareCandidate { get => _compareCandidate; private set { if (SetProperty(ref _compareCandidate, value)) { OnPropertyChanged(nameof(CompareCandidateText)); RefreshCommands(); } } }
    public string CompareCandidateText => CompareCandidate is null ? "尚未选择第二张" : $"第二张：{CompareCandidate.FileName}";
    public BitmapSource? CurrentImage { get => _currentImage; private set => SetProperty(ref _currentImage, value); }
    public BitmapSource? ComparisonPrimaryImage { get => _comparisonPrimaryImage; private set => SetProperty(ref _comparisonPrimaryImage, value); }
    public BitmapSource? ComparisonSecondaryImage { get => _comparisonSecondaryImage; private set => SetProperty(ref _comparisonSecondaryImage, value); }
    public BitmapSource? ClippingOverlay { get => _clippingOverlay; private set => SetProperty(ref _clippingOverlay, value); }
    public BitmapSource? ReferenceImage { get => _referenceImage; private set => SetProperty(ref _referenceImage, value); }
    public TetherHistogramData? Histogram { get => _histogram; private set => SetProperty(ref _histogram, value); }
    public TetherExifInfo? ExifInfo { get => _exifInfo; private set => SetProperty(ref _exifInfo, value); }
    public bool IsPreviewLoading { get => _isPreviewLoading; private set { if (SetProperty(ref _isPreviewLoading, value)) RefreshCommands(); } }
    public double PreviewProgress { get => _previewProgress; private set => SetProperty(ref _previewProgress, value); }
    public string PreviewStatus { get => _previewStatus; private set => SetProperty(ref _previewStatus, value); }
    public TetherPreviewMode PreviewMode { get => _previewMode; private set { if (SetProperty(ref _previewMode, value)) { OnPropertyChanged(nameof(PreviewStretch)); OnPropertyChanged(nameof(IsActualSize)); } } }
    public Stretch PreviewStretch => PreviewMode switch { TetherPreviewMode.Fill => Stretch.UniformToFill, TetherPreviewMode.ActualSize => Stretch.None, _ => Stretch.Uniform };
    public bool IsActualSize => PreviewMode == TetherPreviewMode.ActualSize;
    public double Zoom { get => _zoom; set { if (SetProperty(ref _zoom, Math.Clamp(value, .1, 16))) OnPropertyChanged(nameof(ComparisonSecondaryZoom)); } }
    public double PanX { get => _panX; set { if (SetProperty(ref _panX, value)) OnPropertyChanged(nameof(ComparisonSecondaryPanX)); } }
    public double PanY { get => _panY; set { if (SetProperty(ref _panY, value)) OnPropertyChanged(nameof(ComparisonSecondaryPanY)); } }
    public bool AutoLatest { get => _autoLatest; set { if (SetProperty(ref _autoLatest, value)) { _selectionCoordinator.AutoLatest = value; Track(SaveDisplaySettingsAsync()); } } }
    public bool IsCurrentLocked { get => _isCurrentLocked; set { if (SetProperty(ref _isCurrentLocked, value)) { _selectionCoordinator.SetLocked(value); OnPropertyChanged(nameof(LockButtonText)); RefreshCommands(); } } }
    public string LockButtonText => IsCurrentLocked ? "已锁定当前" : "锁定当前";
    public int NewAssetCount => _selectionCoordinator.NewAssetCount;
    public string NewAssetText => NewAssetCount > 0 ? $"有{NewAssetCount}张新照片" : "没有未查看的新照片";
    public bool IsFullScreen { get => _isFullScreen; set { if (SetProperty(ref _isFullScreen, value)) OnPropertyChanged(nameof(FullScreenButtonText)); } }
    public string FullScreenButtonText => IsFullScreen ? "退出全屏" : "全屏监看";
    public bool IsInspectorCollapsed { get => _isInspectorCollapsed; set => SetProperty(ref _isInspectorCollapsed, value); }
    public bool ShowInspectorDrawer { get => _showInspectorDrawer; set => SetProperty(ref _showInspectorDrawer, value); }
    public bool ShowFullPath { get => _showFullPath; set => SetProperty(ref _showFullPath, value); }
    public bool HighlightWarningEnabled { get => _highlightWarningEnabled; set { if (SetProperty(ref _highlightWarningEnabled, value)) Track(RefreshClippingAsync()); } }
    public bool ShadowWarningEnabled { get => _shadowWarningEnabled; set { if (SetProperty(ref _shadowWarningEnabled, value)) Track(RefreshClippingAsync()); } }
    public int HighlightThreshold { get => _highlightThreshold; set { if (SetProperty(ref _highlightThreshold, Math.Clamp(value, 1, 255))) { Track(RefreshClippingAsync()); Track(SaveDisplaySettingsAsync()); } } }
    public int ShadowThreshold { get => _shadowThreshold; set { if (SetProperty(ref _shadowThreshold, Math.Clamp(value, 0, 254))) { Track(RefreshClippingAsync()); Track(SaveDisplaySettingsAsync()); } } }
    public bool ShowLuminanceHistogram { get => _showLuminanceHistogram; set => SetProperty(ref _showLuminanceHistogram, value); }
    public TetherCompareMode CompareMode { get => _compareMode; private set { if (SetProperty(ref _compareMode, value)) { OnPropertyChanged(nameof(IsNormalPreview)); OnPropertyChanged(nameof(IsSideBySide)); OnPropertyChanged(nameof(IsOverlayCompare)); RefreshCommands(); } } }
    public bool IsNormalPreview => CompareMode == TetherCompareMode.None;
    public bool IsSideBySide => CompareMode == TetherCompareMode.SideBySide;
    public bool IsOverlayCompare => CompareMode == TetherCompareMode.Overlay;
    public bool ComparisonSyncZoom { get => _comparisonSyncZoom; set { if (SetProperty(ref _comparisonSyncZoom, value)) OnPropertyChanged(nameof(ComparisonSecondaryZoom)); } }
    public bool ComparisonSyncPan { get => _comparisonSyncPan; set { if (SetProperty(ref _comparisonSyncPan, value)) { OnPropertyChanged(nameof(ComparisonSecondaryPanX)); OnPropertyChanged(nameof(ComparisonSecondaryPanY)); } } }
    public double ComparisonSecondaryZoom => ComparisonSyncZoom ? Zoom : 1;
    public double ComparisonSecondaryPanX => ComparisonSyncPan ? PanX : 0;
    public double ComparisonSecondaryPanY => ComparisonSyncPan ? PanY : 0;
    public double ComparisonOpacity { get => _comparisonOpacity; set { if (SetProperty(ref _comparisonOpacity, Math.Clamp(value, 0, 1))) OnPropertyChanged(nameof(ComparisonSecondaryOpacity)); } }
    public bool ComparisonBlink { get => _comparisonBlink; set { if (!SetProperty(ref _comparisonBlink, value)) return; if (value) _blinkTimer.Start(); else { _blinkTimer.Stop(); _blinkVisible = true; } OnPropertyChanged(nameof(ComparisonSecondaryOpacity)); } }
    public double ComparisonSecondaryOpacity => ComparisonBlink ? (_blinkVisible ? 1 : 0) : ComparisonOpacity;
    public bool ReferenceVisible { get => _referenceVisible; set => SetProperty(ref _referenceVisible, value); }
    public double ReferenceOpacity { get => _referenceOpacity; set { if (SetProperty(ref _referenceOpacity, Math.Clamp(value, 0, 1))) Track(SaveDisplaySettingsAsync()); } }
    public double ReferenceScale { get => _referenceScale; set { if (SetProperty(ref _referenceScale, Math.Clamp(value, .1, 8))) { OnPropertyChanged(nameof(ReferenceScaleX)); Track(SaveDisplaySettingsAsync()); } } }
    public double ReferenceScaleX => ReferenceFlipHorizontal ? -ReferenceScale : ReferenceScale;
    public double ReferenceOffsetX { get => _referenceOffsetX; set { if (SetProperty(ref _referenceOffsetX, value)) Track(SaveDisplaySettingsAsync()); } }
    public double ReferenceOffsetY { get => _referenceOffsetY; set { if (SetProperty(ref _referenceOffsetY, value)) Track(SaveDisplaySettingsAsync()); } }
    public bool ReferenceFlipHorizontal { get => _referenceFlipHorizontal; set { if (SetProperty(ref _referenceFlipHorizontal, value)) { OnPropertyChanged(nameof(ReferenceScaleX)); Track(SaveDisplaySettingsAsync()); } } }
    public bool ReferenceLocked { get => _referenceLocked; set { if (SetProperty(ref _referenceLocked, value)) Track(SaveDisplaySettingsAsync()); } }
    public string? ReferencePath { get => _referencePath; private set => SetProperty(ref _referencePath, value); }
    public string ReferenceStatus { get => _referenceStatus; private set => SetProperty(ref _referenceStatus, value); }
    public TetherGuideMode GuideMode { get => _guideMode; set { if (SetProperty(ref _guideMode, value)) Track(SaveDisplaySettingsAsync()); } }
    public TetherCanvasTone CanvasTone { get => _canvasTone; set { if (SetProperty(ref _canvasTone, value)) { OnPropertyChanged(nameof(CanvasBackground)); Track(SaveDisplaySettingsAsync()); } } }
    public Brush CanvasBackground => CanvasTone switch
    {
        TetherCanvasTone.Black => Brushes.Black,
        TetherCanvasTone.MidGray => new SolidColorBrush(Color.FromRgb(88, 88, 88)),
        TetherCanvasTone.Checkerboard => CreateCheckerboardBrush(),
        _ => new SolidColorBrush(Color.FromRgb(34, 36, 40))
    };
    public int CurrentRating { get => _currentRating; set => SetProperty(ref _currentRating, Math.Clamp(value, 0, 5)); }
    public string? CurrentColorLabel { get => _currentColorLabel; set => SetProperty(ref _currentColorLabel, string.IsNullOrWhiteSpace(value) ? null : value); }
    public string? PhotographerNote { get => _photographerNote; set => SetProperty(ref _photographerNote, value); }
    public string? ClientNote { get => _clientNote; set => SetProperty(ref _clientNote, value); }
    public bool ClientFavorite { get => _clientFavorite; set => SetProperty(ref _clientFavorite, value); }
    public bool IsRejected { get => _isRejected; set => SetProperty(ref _isRejected, value); }
    public bool IsAnnotationSaving { get => _isAnnotationSaving; private set { if (SetProperty(ref _isAnnotationSaving, value)) RefreshCommands(); } }
    public string AnnotationStatus { get => _annotationStatus; private set => SetProperty(ref _annotationStatus, value); }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await ColorSettings.InitializeAsync(cancellationToken);
        var recovered = await _adapter.RecoverLatestAsync(cancellationToken);
        if (recovered is not null)
        {
            Attach(recovered);
            await LoadDisplaySettingsAsync(recovered.Session.Id, cancellationToken);
            ApplySnapshot(new(recovered.Session, await LoadAssetsAsync(recovered.Session.Id, cancellationToken), 0, true));
            StatusText = "已恢复上次未停止的看守会话，并按数据库状态继续。";
            return;
        }
        var pending = (await _sessionRepository.ListActiveAsync(cancellationToken)).FirstOrDefault();
        if (pending is not null)
        {
            WatchDirectory = pending.WatchDirectory;
            HasRecoverableSession = true;
            StatusText = "上次看守目录暂时不可访问。源文件和数据库记录均未删除。";
        }
    }

    public async Task StopAsync()
    {
        if (_activeSession is null) return;
        IsBusy = true;
        try
        {
            await _activeSession.StopAsync();
            Detach(_activeSession);
            await _activeSession.DisposeAsync();
            _activeSession = null;
            IsRunning = false;
            StatusText = "看守已停止。已发现文件、标注和复制结果仍然保留。";
        }
        catch (Exception) { StatusText = "停止会话时遇到问题，请在任务中心检查。"; }
        finally { IsBusy = false; }
    }

    public void SelectPrevious() => SelectRelative(1);
    public void SelectNext() => SelectRelative(-1);
    public void AdjustZoom(double delta)
    {
        _selectionCoordinator.HasActiveInteraction = true;
        PreviewMode = TetherPreviewMode.Free;
        Zoom = Math.Clamp(Zoom * (delta > 0 ? 1.12 : .89), .1, 16);
    }
    public void SetPan(double x, double y) { _selectionCoordinator.HasActiveInteraction = true; PanX = x; PanY = y; }
    public void EndCanvasInteraction() => _selectionCoordinator.HasActiveInteraction = false;
    public void ToggleFitActual() { if (IsActualSize) SetPreviewMode(TetherPreviewMode.Fit); else if (ActualSizeCommand.CanExecute(null)) ActualSizeCommand.Execute(null); }
    public void BeginNoteEditing() => _selectionCoordinator.IsEditingNote = true;
    public void EndNoteEditing() => _selectionCoordinator.IsEditingNote = false;

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        _requestCoordinator.CancelCurrent();
        _blinkTimer.Stop();
        foreach (var item in Assets) item.ReleaseThumbnail();
        if (_activeSession is not null)
        {
            Detach(_activeSession);
            await _activeSession.DisposeAsync();
            _activeSession = null;
        }
        Task[] tasks;
        lock (_backgroundSync) tasks = _backgroundTasks.ToArray();
        try { await Task.WhenAll(tasks); } catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { }
        _requestCoordinator.Dispose();
        ColorSettings.Dispose();
        _lifetime.Dispose();
    }

    public void ApplyReviewState(string state, IReadOnlyList<TetherAssetRecord> assets, IReadOnlyDictionary<Guid, TetherAnnotationRecord>? annotations = null)
    {
        _reviewStateActive = true;
        var now = DateTimeOffset.UtcNow;
        var session = new TetherSessionRecord(Guid.Parse("23000000-0000-0000-0000-000000000003"), null, CameraProviderType.WatchFolder, "[合成测试目录]", "[合成测试目录]", TetherSessionState.Running, now, now, true, false, null, false, null, now);
        IsRunning = true;
        ApplySnapshot(new(session, assets, 0, false));
        if (annotations is not null)
            foreach (var pair in annotations) if (_assetIndex.TryGetValue(pair.Key, out var item)) item.ApplyAnnotation(pair.Value);
        SelectedAsset = Assets.FirstOrDefault();
        ColorSettings.ApplyReviewState(state);
        StatusText = state switch
        {
            "TetherEmpty" => "阶段C评审：等待看守文件夹送入第一张照片。",
            "TetherAssets" => "阶段C评审：缩略图、主画布和检查器已联动。",
            "TetherAutoLatest" => "阶段C评审：自动最新已开启。",
            "TetherLocked" => "阶段C评审：当前照片已锁定，新照片不会抢占。",
            "TetherExifHistogram" => "阶段C评审：EXIF 与 RGB 直方图使用当前代理图。",
            "TetherWarnings" => "阶段C评审：高光与阴影警告仅影响显示。",
            "TetherSideBySide" => "阶段C评审：左右并排比较。",
            "TetherOverlayCompare" => "阶段C评审：叠加比较可调透明度。",
            "TetherReference" => "阶段C评审：参考图叠加不会修改源文件。",
            "TetherGuides" => "阶段C评审：构图辅助线仅用于现场监看。",
            "TetherAnnotations" => "阶段C评审：评分、色标和备注保存至现有标注表。",
            "TetherFullscreen" => "阶段C评审：全屏监看保留键盘导航。",
            "TetherDark" => "阶段C评审：深色主题。",
            "TetherLight" => "阶段C评审：浅色主题。",
            "TetherHighContrast" => "阶段C评审：高对比度主题。",
            "TetherCompact1280" => "阶段C评审：1280 宽度使用检查器抽屉。",
            "TetherDpi150" => "阶段C评审：150% 逻辑 DPI 布局。",
            "TetherRawPlaceholder" => "阶段C评审：无配对 JPG 的 RAW 显示安全占位。",
            "LutNone" => "阶段D评审：未选择LUT，输入色彩空间未知。",
            "LutImported" => "阶段D评审：3D LUT已验证并仅关联原位置。",
            "LutStrength50" => "阶段D评审：LUT强度50%，CPU代理后台渲染。",
            "LutBeforeAfter" => "阶段D评审：按住或键盘B临时查看原图。",
            "LutSplitView" => "阶段D评审：原图与LUT结果分屏比较。",
            "LutInvalid" => "阶段D评审：损坏LUT已安全回退原图。",
            "ColorProfileDetected" => "阶段D评审：当前显示器ICC已独立检测。",
            "ColorProfileFallback" => "阶段D评审：无ICC或异常ICC时回退sRGB。",
            "ClientMonitorSelector" => "阶段D评审：选择StableKey显示器并确认隐私后开启。",
            "ClientMonitorFollowMain" => "阶段D评审：客户屏跟随主选中。",
            "ClientMonitorFollowLatest" => "阶段D评审：客户屏跟随最新Ready照片。",
            "ClientMonitorLocked" => "阶段D评审：客户屏独立锁定，新照片只累计提示。",
            "ClientMonitorPrivacy" => "阶段D评审：客户屏默认隐藏文件名、路径和私人备注。",
            "ClientMonitorFavoriteNote" => "阶段D评审：客户收藏和备注复用TetherAnnotations。",
            "ClientMonitorDisconnected" => "阶段D评审：客户屏断开，窗口撤回且接片继续。",
            "ClientMonitorReconnected" => "阶段D评审：客户屏重连后可手动恢复。",
            "MixedDpi" => "阶段D评审：主屏100%与客户屏150%自动化拓扑。",
            _ => StatusText
        };
        switch (state)
        {
            case "TetherLocked": IsCurrentLocked = true; break;
            case "TetherExifHistogram": HighlightWarningEnabled = false; ShadowWarningEnabled = false; Track(LoadReviewAnalysisAsync(false)); break;
            case "TetherWarnings": HighlightThreshold = 220; ShadowThreshold = 30; HighlightWarningEnabled = true; ShadowWarningEnabled = true; Track(LoadReviewAnalysisAsync(true)); break;
            case "TetherSideBySide": CompareCandidate = Assets.Skip(1).FirstOrDefault(); Track(StartReviewComparisonAsync(TetherCompareMode.SideBySide)); break;
            case "TetherOverlayCompare": CompareCandidate = Assets.Skip(1).FirstOrDefault(); Track(StartReviewComparisonAsync(TetherCompareMode.Overlay)); break;
            case "TetherReference": Track(LoadReviewReferenceAsync(assets.Skip(1).FirstOrDefault()?.SourcePath)); break;
            case "TetherGuides": GuideMode = TetherGuideMode.Thirds; break;
            case "TetherAnnotations": CurrentRating = 5; CurrentColorLabel = "绿"; ClientFavorite = true; PhotographerNote = "主光位置确认，保留这一张。"; ClientNote = "客户现场收藏"; break;
            case "TetherFullscreen": IsFullScreen = true; break;
            case "TetherRawPlaceholder": SelectedAsset = Assets.FirstOrDefault(item => item.Record.MediaKind == TetherMediaKind.Raw); break;
        }
    }

    private async Task StartAsync()
    {
        if (CopyToProject && string.IsNullOrWhiteSpace(ProjectDestination)) { _dialogs.ShowError("请先选择项目资料目录，或关闭项目复制。"); return; }
        if (CopyToBackup && string.IsNullOrWhiteSpace(BackupDestination)) { _dialogs.ShowError("请先选择独立备份目录，或关闭独立备份。"); return; }
        IsBusy = true;
        try
        {
            var session = await _adapter.StartAsync(new(WatchDirectory, ImportExisting: ImportExisting, CopyToProject: CopyToProject,
                ProjectDestination: ProjectDestination, CopyToBackup: CopyToBackup, BackupDestination: BackupDestination, VerifySha256: VerifySha256));
            Attach(session);
            Assets.Clear(); _assetIndex.Clear(); _knownReadyAssets.Clear();
            await LoadDisplaySettingsAsync(session.Session.Id, _lifetime.Token);
            StatusText = ImportExisting ? "看守已启动，正在检查顶层已有文件。" : "看守已启动，只接收本次开始后创建的顶层文件。";
        }
        catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusText = "看守未启动。请检查目录状态和复制选项。";
            _dialogs.ShowError(StatusText);
        }
        finally { IsBusy = false; }
    }

    private void Attach(ICameraSession session)
    {
        _activeSession = session;
        HasRecoverableSession = false;
        session.SnapshotChanged += Session_SnapshotChanged;
        WatchDirectory = session.Session.WatchDirectory;
        ProjectDestination = session.Session.ProjectDestination ?? string.Empty;
        BackupDestination = session.Session.BackupDestination ?? string.Empty;
        ImportExisting = session.Session.ImportExisting;
        CopyToProject = session.Session.CopyToProject;
        CopyToBackup = session.Session.CopyToBackup;
        IsRunning = session.Session.State == TetherSessionState.Running;
    }

    private void Detach(ICameraSession session) => session.SnapshotChanged -= Session_SnapshotChanged;

    private void Session_SnapshotChanged(object? sender, TetherSessionSnapshot snapshot)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess()) dispatcher.BeginInvoke(() => ApplySnapshot(snapshot));
        else ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(TetherSessionSnapshot snapshot)
    {
        var incomingIds = snapshot.Assets.Select(asset => asset.Id).ToHashSet();
        foreach (var missing in _assetIndex.Keys.Where(id => !incomingIds.Contains(id)).ToArray())
        {
            var item = _assetIndex[missing]; item.ReleaseThumbnail(); Assets.Remove(item); _assetIndex.Remove(missing); _knownReadyAssets.Remove(missing);
        }

        foreach (var record in snapshot.Assets)
        {
            if (_assetIndex.TryGetValue(record.Id, out var existing)) existing.Update(record, _proxyCache.ResolvePath(record.ProxyCacheKey));
            else
            {
                var item = new TetherAssetItemViewModel(record, _proxyCache.ResolvePath(record.ProxyCacheKey), _previewLoader);
                _assetIndex.Add(record.Id, item); Assets.Add(item);
                if (!_reviewStateActive) Track(LoadAnnotationAsync(item));
            }
        }

        AssetsView.Refresh();
        foreach (var ready in snapshot.Assets.Where(IsReady).OrderBy(asset => asset.ReadyAtUtc ?? asset.FirstSeenAtUtc))
        {
            if (!_knownReadyAssets.Add(ready.Id)) continue;
            ColorSettings.NotifyLatest(ready.Id);
            var selected = _selectionCoordinator.OnReady(ready.Id);
            if (selected.HasValue && _assetIndex.TryGetValue(selected.Value, out var item)) SetSelected(item, false);
        }
        if (SelectedAsset is null) SetSelected(AssetsView.Cast<TetherAssetItemViewModel>().FirstOrDefault(), false);
        QueueDepth = snapshot.QueueDepth;
        IsRunning = snapshot.Session.State == TetherSessionState.Running;
        StatusText = snapshot.Session.State switch
        {
            TetherSessionState.Running when snapshot.ReconciliationPending => "正在进行顶层目录补偿核对。",
            TetherSessionState.Running => "看守运行中。所有文件会先通过稳定检测。",
            TetherSessionState.NeedsAttention => "会话需要处理；不会删除或移动任何源文件。",
            _ => "看守已停止。"
        };
        NotifyCounts();
    }

    private async Task LoadSelectedAsync(TetherAssetItemViewModel item, CancellationToken cancellationToken)
    {
        var request = _requestCoordinator.Begin(item.Record.Id, cancellationToken);
        _fullResolutionLoader.ReleaseExcept(null);
        IsPreviewLoading = true; PreviewProgress = 10; PreviewStatus = "正在加载监看代理图…";
        CurrentImage = null; ClippingOverlay = null; Histogram = null; ExifInfo = TetherExifInfo.Unavailable(item.Record);
        ApplyAnnotationToEditor(item.Annotation);
        try
        {
            var previewTask = _previewLoader.LoadAsync(item.Record, 2048, request.Token);
            var exifTask = _exifService.ReadAsync(item.Record, request.Token);
            var result = await previewTask;
            if (!_requestCoordinator.IsCurrent(item.Record.Id, request.Version)) return;
            CurrentImage = result.Image;
            await ColorSettings.SetSourceAsync(item.Record.Id, item.Record.ProxyCacheKey ?? item.Record.UpdatedAtUtc.ToUnixTimeMilliseconds().ToString(), result.Image, request.Token);
            PreviewMode = TetherPreviewMode.Fit; Zoom = 1; PanX = PanY = 0;
            PreviewProgress = 65;
            PreviewStatus = result.Image is null ? result.Message ?? "预览不可用。" : result.UsedPairedPreview ? "RAW使用配对JPG进行监看。" : "监看代理图 · 最长边2048";
            ExifInfo = await exifTask;
            if (result.Image is not null)
            {
                var histogram = await _histogramService.CalculateAsync(result.Image, true, request.Token);
                if (!_requestCoordinator.IsCurrent(item.Record.Id, request.Version)) return;
                Histogram = histogram;
                await RefreshClippingAsync(request.Token);
            }
            PreviewProgress = 100;
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (_requestCoordinator.IsCurrent(item.Record.Id, request.Version)) IsPreviewLoading = false;
        }
    }

    private async Task LoadReviewAnalysisAsync(bool createClippingOverlay)
    {
        var item = SelectedAsset;
        if (item is null) return;
        ExifInfo = TetherExifInfo.Unavailable(item.Record);
        var exifTask = _exifService.ReadAsync(item.Record, _lifetime.Token);
        var preview = await _previewLoader.LoadAsync(item.Record, 2048, _lifetime.Token);
        if (preview.Image is null) return;
        CurrentImage = preview.Image;
        await ColorSettings.SetSourceAsync(item.Record.Id, item.Record.ProxyCacheKey ?? item.Record.UpdatedAtUtc.ToUnixTimeMilliseconds().ToString(), preview.Image, _lifetime.Token);
        Histogram = await _histogramService.CalculateAsync(preview.Image, true, _lifetime.Token);
        ClippingOverlay = createClippingOverlay
            ? await _clippingService.CreateAsync(preview.Image, true, HighlightThreshold, true, ShadowThreshold, _lifetime.Token)
            : null;
        PreviewProgress = 100;
        IsPreviewLoading = false;
        ExifInfo = await exifTask;
    }

    private async Task LoadActualSizeAsync()
    {
        var item = SelectedAsset;
        if (item is null) return;
        var request = _requestCoordinator.Begin(item.Record.Id, _lifetime.Token);
        _selectionCoordinator.IsActualSize = true;
        IsPreviewLoading = true; PreviewProgress = 5; PreviewStatus = "按需读取源文件，退出后会释放非当前大图…";
        try
        {
            var result = await _fullResolutionLoader.LoadAsync(item.Record, request.Token);
            if (!_requestCoordinator.IsCurrent(item.Record.Id, request.Version)) return;
            if (result.Image is null)
            {
                PreviewStatus = result.Message ?? "100%查看不可用，继续显示监看代理图。";
                _selectionCoordinator.IsActualSize = false;
                return;
            }
            CurrentImage = result.Image; PreviewMode = TetherPreviewMode.ActualSize; Zoom = 1; PanX = PanY = 0; PreviewProgress = 100;
            await ColorSettings.SetSourceAsync(item.Record.Id, item.Record.UpdatedAtUtc.ToUnixTimeMilliseconds().ToString(), result.Image, request.Token);
            PreviewStatus = result.UsedPairedPreview ? "100%查看 · RAW使用配对JPG源文件" : "100%查看 · 源文件流已释放";
            Histogram = await _histogramService.CalculateAsync(result.Image, false, request.Token);
            await RefreshClippingAsync(request.Token);
        }
        catch (OperationCanceledException) { }
        finally { if (_requestCoordinator.IsCurrent(item.Record.Id, request.Version)) IsPreviewLoading = false; }
    }

    private async Task RefreshAnalysisAsync()
    {
        if (CurrentImage is null || SelectedAsset is null) return;
        var request = _requestCoordinator.Begin(SelectedAsset.Record.Id, _lifetime.Token);
        try
        {
            Histogram = await _histogramService.CalculateAsync(CurrentImage, !IsActualSize, request.Token);
            await RefreshClippingAsync(request.Token);
        }
        catch (OperationCanceledException) { }
    }

    private async Task RefreshClippingAsync(CancellationToken cancellationToken = default)
    {
        var image = CurrentImage;
        if (image is null) { ClippingOverlay = null; return; }
        try { ClippingOverlay = await _clippingService.CreateAsync(image, HighlightWarningEnabled, HighlightThreshold, ShadowWarningEnabled, ShadowThreshold, cancellationToken); }
        catch (OperationCanceledException) { }
    }

    private void SetPreviewMode(TetherPreviewMode mode)
    {
        _requestCoordinator.CancelCurrent();
        _selectionCoordinator.IsActualSize = false;
        _fullResolutionLoader.ReleaseExcept(null);
        PreviewMode = mode; Zoom = 1; PanX = PanY = 0;
        if (SelectedAsset is not null) Track(LoadSelectedAsync(SelectedAsset, _lifetime.Token));
    }

    private void ResetView() { Zoom = 1; PanX = PanY = 0; if (PreviewMode == TetherPreviewMode.Free) PreviewMode = TetherPreviewMode.Fit; }

    private void SelectRelative(int delta)
    {
        var items = AssetsView.Cast<TetherAssetItemViewModel>().ToArray();
        if (items.Length == 0) return;
        var index = SelectedAsset is null ? 0 : Array.IndexOf(items, SelectedAsset);
        index = Math.Clamp(index + delta, 0, items.Length - 1);
        SetSelected(items[index], true);
    }

    private void SetSelected(TetherAssetItemViewModel? item, bool manual)
    {
        _suppressManualSelection = !manual;
        try { SelectedAsset = item; if (manual && item is not null) _selectionCoordinator.SelectManually(item.Record.Id); }
        finally { _suppressManualSelection = false; }
    }

    private void UnlockAndSelectLatest()
    {
        var latest = _selectionCoordinator.UnlockAndSelectLatest();
        IsCurrentLocked = false;
        if (latest.HasValue && _assetIndex.TryGetValue(latest.Value, out var item)) SetSelected(item, false);
        OnPropertyChanged(nameof(NewAssetCount)); OnPropertyChanged(nameof(NewAssetText));
    }

    private async Task LoadAnnotationAsync(TetherAssetItemViewModel item)
    {
        try
        {
            var annotation = await _annotationService.GetAsync(item.Record.Id, _lifetime.Token);
            item.ApplyAnnotation(annotation);
            if (SelectedAsset == item) ApplyAnnotationToEditor(annotation);
            AssetsView.Refresh();
        }
        catch (OperationCanceledException) { }
    }

    private void ApplyAnnotationToEditor(TetherAnnotationRecord? annotation)
    {
        CurrentRating = annotation?.Rating ?? 0;
        CurrentColorLabel = annotation?.ColorLabel;
        PhotographerNote = annotation?.PhotographerNote;
        ClientNote = annotation?.ClientNote;
        ClientFavorite = annotation?.ClientFavorite ?? false;
        IsRejected = annotation?.IsRejected ?? false;
        AnnotationStatus = annotation is null ? "尚无标注；保存后仅写入本地数据库。" : $"已于 {annotation.UpdatedAtUtc.ToLocalTime():HH:mm:ss} 保存到本地。";
    }

    private async Task SetRatingAsync(object? parameter)
    {
        if (parameter is string text && int.TryParse(text, out var rating)) CurrentRating = Math.Clamp(rating, 0, 5);
        else if (parameter is int value) CurrentRating = Math.Clamp(value, 0, 5);
        await SaveAnnotationAsync();
    }

    private async Task SetColorLabelAsync(object? parameter)
    {
        CurrentColorLabel = string.IsNullOrWhiteSpace(parameter?.ToString()) ? null : parameter!.ToString();
        await SaveAnnotationAsync();
    }

    private async Task ToggleFavoriteAsync() { ClientFavorite = !ClientFavorite; await SaveAnnotationAsync(); }
    private async Task ToggleRejectedAsync() { IsRejected = !IsRejected; await SaveAnnotationAsync(); }

    private async Task SaveAnnotationAsync()
    {
        var item = SelectedAsset;
        if (item is null) return;
        IsAnnotationSaving = true;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var existing = item.Annotation;
            var annotation = new TetherAnnotationRecord(existing?.Id ?? Guid.NewGuid(), item.Record.Id, CurrentRating, CurrentColorLabel, PhotographerNote,
                existing?.CreatedAtUtc ?? now, now, ClientFavorite, ClientNote, IsRejected);
            var result = await _annotationService.SaveAsync(annotation, item.Record.ProjectId, _lifetime.Token);
            if (!result.Success || result.Annotation is null)
            {
                AnnotationStatus = result.Message ?? "标注未保存。";
                StatusText = "标注保存失败；照片和原有数据库记录保持不变。";
                return;
            }
            item.ApplyAnnotation(result.Annotation);
            ApplyAnnotationToEditor(result.Annotation);
            AnnotationStatus = "标注已保存。备注正文不会写入日志或诊断包。";
            AssetsView.Refresh();
        }
        catch (OperationCanceledException) { AnnotationStatus = "标注保存已取消。"; }
        finally { IsAnnotationSaving = false; }
    }

    private async Task SaveClientAnnotationFromMonitorAsync(bool favorite, string? note)
    {
        if (SelectedAsset is null) throw new InvalidOperationException("当前没有选中的照片。");
        ClientFavorite = favorite;
        ClientNote = note;
        await SaveAnnotationAsync();
        if (AnnotationStatus.Contains("失败", StringComparison.Ordinal)) throw new InvalidOperationException(AnnotationStatus);
    }

    private async Task<BitmapSource?> LoadAssetImageForClientAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var item = Assets.FirstOrDefault(candidate => candidate.Record.Id == assetId);
        if (item is null) return null;
        var result = await _previewLoader.LoadAsync(item.Record, 1600, cancellationToken).ConfigureAwait(false);
        return result.Image;
    }

    private void SetCompareCandidate(TetherAssetItemViewModel? item)
    {
        if (item is null || item == SelectedAsset) return;
        CompareCandidate = CompareCandidate == item ? null : item;
    }

    private bool CanStartComparison() => SelectedAsset is not null && CompareCandidate is not null && SelectedAsset != CompareCandidate && !IsPreviewLoading;

    private async Task StartComparisonAsync(TetherCompareMode mode)
    {
        if (!CanStartComparison()) return;
        await StartComparisonCoreAsync(mode);
    }

    private Task StartReviewComparisonAsync(TetherCompareMode mode) =>
        SelectedAsset is null || CompareCandidate is null || SelectedAsset == CompareCandidate
            ? Task.CompletedTask
            : StartComparisonCoreAsync(mode);

    private async Task StartComparisonCoreAsync(TetherCompareMode mode)
    {
        _selectionBeforeCompare = SelectedAsset;
        _selectionCoordinator.IsComparing = true;
        CompareMode = mode;
        var left = SelectedAsset!; var right = CompareCandidate!;
        var leftResult = await _previewLoader.LoadAsync(left.Record, 2048, _lifetime.Token);
        var rightResult = await _previewLoader.LoadAsync(right.Record, 2048, _lifetime.Token);
        ComparisonPrimaryImage = leftResult.Image; ComparisonSecondaryImage = rightResult.Image;
        PreviewStatus = mode == TetherCompareMode.SideBySide ? "并排比较 · 新照片不会打断" : "重叠比较 · 新照片不会打断";
    }

    private void ExitComparison()
    {
        ComparisonBlink = false; CompareMode = TetherCompareMode.None; ComparisonPrimaryImage = ComparisonSecondaryImage = null;
        _selectionCoordinator.IsComparing = false;
        if (_selectionBeforeCompare is not null) SetSelected(_selectionBeforeCompare, false);
        _selectionBeforeCompare = null;
    }

    private void SwapComparison() => (ComparisonPrimaryImage, ComparisonSecondaryImage) = (ComparisonSecondaryImage, ComparisonPrimaryImage);

    private void UseCompareCandidateAsPrimary()
    {
        if (CompareCandidate is null) return;
        var previous = SelectedAsset;
        SetSelected(CompareCandidate, true);
        CompareCandidate = previous;
        SwapComparison();
    }

    private async Task ChooseReferenceAsync()
    {
        var path = _dialogs.ChooseFiles("选择本地参考图（仅关联原位置）", "图片|*.jpg;*.jpeg;*.png;*.tif;*.tiff|所有文件|*.*", false).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(path)) return;
        await LoadReferenceAsync(path);
    }

    private async Task LoadReferenceAsync(string path)
    {
        try
        {
            ReferenceImage = await BitmapFileLoader.LoadAsync(path, 2048, _lifetime.Token);
            ReferencePath = Path.GetFullPath(path); ReferenceVisible = true; ReferenceStatus = "参考图仅关联原位置，不会写入照片或TetherAssets。";
            await SaveDisplaySettingsAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException)
        {
            ReferenceImage = null; ReferencePath = path; ReferenceVisible = false; ReferenceStatus = "参考图丢失或暂时不可访问，请重新定位。";
        }
    }

    private async Task LoadReviewReferenceAsync(string? path) { if (!string.IsNullOrWhiteSpace(path)) await LoadReferenceAsync(path); }

    private async Task ClearReferenceAsync()
    {
        ReferenceImage = null; ReferencePath = null; ReferenceVisible = false; ReferenceStatus = "参考图引用已清除；本地文件未删除。";
        await SaveDisplaySettingsAsync();
    }

    private async Task LoadDisplaySettingsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var settings = await _displaySettingsStore.LoadAsync(sessionId, cancellationToken);
        AutoLatest = settings.AutoLatest; GuideMode = settings.GuideMode; CanvasTone = settings.CanvasTone;
        HighlightThreshold = settings.HighlightThreshold; ShadowThreshold = settings.ShadowThreshold;
        ReferenceOpacity = settings.ReferenceOpacity; ReferenceScale = settings.ReferenceScale; ReferenceOffsetX = settings.ReferenceOffsetX; ReferenceOffsetY = settings.ReferenceOffsetY;
        ReferenceFlipHorizontal = settings.ReferenceFlipHorizontal; ReferenceLocked = settings.ReferenceLocked; ReferencePath = settings.ReferencePath;
        if (!string.IsNullOrWhiteSpace(settings.ReferencePath)) await LoadReferenceAsync(settings.ReferencePath);
    }

    private Task SaveDisplaySettingsAsync()
    {
        var sessionId = _activeSession?.Session.Id;
        if (!sessionId.HasValue) return Task.CompletedTask;
        return _displaySettingsStore.SaveAsync(new(sessionId.Value, AutoLatest, GuideMode, CanvasTone, HighlightThreshold, ShadowThreshold,
            ReferencePath, ReferenceOpacity, ReferenceScale, ReferenceOffsetX, ReferenceOffsetY, ReferenceFlipHorizontal, ReferenceLocked), _lifetime.Token);
    }

    private async Task ReconcileAsync()
    {
        if (_activeSession is null) return;
        IsBusy = true;
        try { await _activeSession.ReconcileAsync(); StatusText = "顶层目录核对完成。"; }
        catch (Exception) { StatusText = "目录核对未完成，源文件保持不变。"; }
        finally { IsBusy = false; }
    }

    private async Task ClearProxyCacheAsync()
    {
        IsBusy = true;
        try { await _proxyCache.ClearAsync(); StatusText = "联机预览缓存已清理；原文件、100%源图和数据库记录未改变。"; }
        catch (Exception) { StatusText = "部分缓存暂时无法清理；原文件未受影响。"; }
        finally { IsBusy = false; }
    }

    private void ChooseWatchFolder()
    {
        var selected = _dialogs.ChooseFolder("选择看守文件夹", WatchDirectory);
        if (!string.IsNullOrWhiteSpace(selected)) { WatchDirectory = selected; CountExisting(); }
    }
    private void ChooseProjectDestination() { var selected = _dialogs.ChooseFolder("选择项目资料目录", ProjectDestination); if (!string.IsNullOrWhiteSpace(selected)) ProjectDestination = selected; }
    private void ChooseBackupDestination() { var selected = _dialogs.ChooseFolder("选择独立备份目录", BackupDestination); if (!string.IsNullOrWhiteSpace(selected)) BackupDestination = selected; }
    private void CountExisting() { try { ExistingCandidateCount = Directory.EnumerateFiles(WatchDirectory, "*", SearchOption.TopDirectoryOnly).Count(path => WatchFolderPathPolicy.IsCandidate(WatchDirectory, path)); } catch { ExistingCandidateCount = 0; } }
    private void RevealAsset(TetherAssetItemViewModel? item) { if (item is not null) _dialogs.RevealFile(item.Record.SourcePath); }
    private Task<IReadOnlyList<TetherAssetRecord>> LoadAssetsAsync(Guid sessionId, CancellationToken cancellationToken) => _assetRepository.ListBySessionAsync(sessionId, cancellationToken);

    private bool FilterAsset(object value)
    {
        if (value is not TetherAssetItemViewModel item) return false;
        return SelectedFilter switch
        {
            TetherAssetFilter.JpegOnly => item.Record.MediaKind == TetherMediaKind.PreviewImage,
            TetherAssetFilter.RawOnly => item.Record.MediaKind == TetherMediaKind.Raw,
            TetherAssetFilter.Paired => item.Record.PairedAssetId.HasValue,
            TetherAssetFilter.Unpaired => !item.Record.PairedAssetId.HasValue,
            TetherAssetFilter.Favorites => item.ClientFavorite,
            TetherAssetFilter.Rated => item.Rating > 0,
            TetherAssetFilter.Rejected => item.IsRejected,
            TetherAssetFilter.NeedsAttention => item.NeedsAttention,
            _ => true
        };
    }

    private void ApplySort()
    {
        using (AssetsView.DeferRefresh())
        {
            AssetsView.SortDescriptions.Clear();
            var sort = SelectedSort switch
            {
                TetherAssetSort.OldestFirst => new SortDescription(nameof(TetherAssetItemViewModel.CaptureSortTime), ListSortDirection.Ascending),
                TetherAssetSort.FileName => new SortDescription(nameof(TetherAssetItemViewModel.FileName), ListSortDirection.Ascending),
                TetherAssetSort.Rating => new SortDescription(nameof(TetherAssetItemViewModel.Rating), ListSortDirection.Descending),
                TetherAssetSort.Status => new SortDescription(nameof(TetherAssetItemViewModel.StateText), ListSortDirection.Ascending),
                _ => new SortDescription(nameof(TetherAssetItemViewModel.CaptureSortTime), ListSortDirection.Descending)
            };
            AssetsView.SortDescriptions.Add(sort);
        }
    }

    private void RefreshCommands()
    {
        ChooseWatchFolderCommand.RaiseCanExecuteChanged(); PreviewExistingCommand.RaiseCanExecuteChanged();
        ChooseProjectDestinationCommand.RaiseCanExecuteChanged(); ChooseBackupDestinationCommand.RaiseCanExecuteChanged();
        StartCommand.RaiseCanExecuteChanged(); StopCommand.RaiseCanExecuteChanged(); ReconcileCommand.RaiseCanExecuteChanged(); ClearProxyCacheCommand.RaiseCanExecuteChanged();
        PreviousCommand.RaiseCanExecuteChanged(); NextCommand.RaiseCanExecuteChanged(); ActualSizeCommand.RaiseCanExecuteChanged(); ToggleLockCommand.RaiseCanExecuteChanged(); UnlockLatestCommand.RaiseCanExecuteChanged();
        SetRatingCommand.RaiseCanExecuteChanged(); SetColorLabelCommand.RaiseCanExecuteChanged(); SaveNotesCommand.RaiseCanExecuteChanged(); ToggleFavoriteCommand.RaiseCanExecuteChanged(); ToggleRejectedCommand.RaiseCanExecuteChanged();
        StartSideBySideCommand.RaiseCanExecuteChanged(); StartOverlayCommand.RaiseCanExecuteChanged(); ExitComparisonCommand.RaiseCanExecuteChanged(); SwapComparisonCommand.RaiseCanExecuteChanged(); UseCompareCandidateAsPrimaryCommand.RaiseCanExecuteChanged(); ClearReferenceCommand.RaiseCanExecuteChanged(); RefreshAnalysisCommand.RaiseCanExecuteChanged();
    }

    private void NotifyCounts()
    {
        OnPropertyChanged(nameof(ReadyCount)); OnPropertyChanged(nameof(AttentionCount)); OnPropertyChanged(nameof(DiscoveredCount));
        OnPropertyChanged(nameof(WaitingStableCount)); OnPropertyChanged(nameof(FailedCount)); OnPropertyChanged(nameof(CopyStatusText));
        OnPropertyChanged(nameof(NewAssetCount)); OnPropertyChanged(nameof(NewAssetText)); RefreshCommands();
    }

    private void Track(Task task)
    {
        lock (_backgroundSync) _backgroundTasks.Add(task);
        _ = task.ContinueWith(completed => { lock (_backgroundSync) _backgroundTasks.Remove(completed); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private static bool IsReady(TetherAssetRecord asset) => asset.ProcessingState is TetherProcessingState.Ready or TetherProcessingState.Copied;

    private static Brush CreateCheckerboardBrush()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(80, 80, 80)), null, new RectangleGeometry(new Rect(0, 0, 20, 20))));
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(105, 105, 105)), null, new GeometryGroup
        {
            Children = { new RectangleGeometry(new Rect(0, 0, 10, 10)), new RectangleGeometry(new Rect(10, 10, 10, 10)) }
        }));
        var brush = new DrawingBrush(group) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 20, 20), ViewportUnits = BrushMappingMode.Absolute };
        brush.Freeze(); return brush;
    }

    private sealed class NullTetherAnnotationService : ITetherAnnotationService
    {
        public Task<TetherAnnotationRecord?> GetAsync(Guid assetId, CancellationToken cancellationToken = default) => Task.FromResult<TetherAnnotationRecord?>(null);
        public Task<IReadOnlyDictionary<Guid, TetherAnnotationRecord>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<Guid, TetherAnnotationRecord>>(new Dictionary<Guid, TetherAnnotationRecord>());
        public Task<TetherAnnotationSaveResult> SaveAsync(TetherAnnotationRecord annotation, Guid? projectId = null, CancellationToken cancellationToken = default) => Task.FromResult(new TetherAnnotationSaveResult(false, null, "DatabaseUnavailable", "标注服务尚未连接。"));
    }
}

public sealed record TetherChoice<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

public sealed class TetherAssetItemViewModel : ObservableObject
{
    private readonly IPreviewImageLoader _previewLoader;
    private CancellationTokenSource? _thumbnailCancellation;
    private TetherAssetRecord _record;
    private string? _proxyPath;
    private BitmapSource? _thumbnail;
    private TetherAnnotationRecord? _annotation;

    public TetherAssetItemViewModel(TetherAssetRecord record, string? proxyPath, IPreviewImageLoader previewLoader)
    {
        _record = record; _proxyPath = proxyPath; _previewLoader = previewLoader;
    }

    public TetherAssetRecord Record => _record;
    public string? ProxyPath => _proxyPath;
    public BitmapSource? Thumbnail { get => _thumbnail; private set => SetProperty(ref _thumbnail, value); }
    public TetherAnnotationRecord? Annotation => _annotation;
    public string FileName => Record.FileName;
    public bool IsRaw => Record.MediaKind == TetherMediaKind.Raw;
    public string MediaKindText => Record.MediaKind == TetherMediaKind.Raw ? "RAW" : Record.Extension.TrimStart('.').ToUpperInvariant();
    public DateTimeOffset CaptureSortTime => Record.ModifiedAtUtc ?? Record.ReadyAtUtc ?? Record.FirstSeenAtUtc;
    public string CaptureTimeText => CaptureSortTime.ToLocalTime().ToString("HH:mm:ss");
    public int Rating => Annotation?.Rating ?? 0;
    public string RatingText => Rating == 0 ? "未评级" : new string('★', Rating);
    public string? ColorLabel => Annotation?.ColorLabel;
    public bool ClientFavorite => Annotation?.ClientFavorite ?? false;
    public bool IsRejected => Annotation?.IsRejected ?? false;
    public bool NeedsAttention => Record.ProcessingState is TetherProcessingState.NeedsAttention or TetherProcessingState.PartiallyCompleted || Record.StabilityState is TetherStabilityState.Missing or TetherStabilityState.Inaccessible or TetherStabilityState.TimedOut;
    public string StateText => Record.ProcessingState switch
    {
        TetherProcessingState.Copied => "已安全复制", TetherProcessingState.PartiallyCompleted => "部分完成", TetherProcessingState.NeedsAttention => "需要处理",
        TetherProcessingState.Ready => "已就绪", _ => Record.StabilityState == TetherStabilityState.Probing ? "稳定检测中" : Record.ProcessingState.ToString()
    };
    public string PairText => Record.PairedAssetId.HasValue ? "JPG/RAW 已配对" : "未配对";

    public void Update(TetherAssetRecord record, string? proxyPath)
    {
        _record = record; _proxyPath = proxyPath;
        foreach (var property in new[] { nameof(Record), nameof(ProxyPath), nameof(FileName), nameof(IsRaw), nameof(MediaKindText), nameof(CaptureSortTime), nameof(CaptureTimeText), nameof(StateText), nameof(PairText), nameof(NeedsAttention) }) OnPropertyChanged(property);
    }

    public void ApplyAnnotation(TetherAnnotationRecord? annotation)
    {
        _annotation = annotation;
        foreach (var property in new[] { nameof(Annotation), nameof(Rating), nameof(RatingText), nameof(ColorLabel), nameof(ClientFavorite), nameof(IsRejected) }) OnPropertyChanged(property);
    }

    public async Task LoadThumbnailAsync()
    {
        if (Thumbnail is not null) return;
        _thumbnailCancellation?.Cancel(); _thumbnailCancellation?.Dispose(); _thumbnailCancellation = new();
        try
        {
            var result = await _previewLoader.LoadAsync(Record, 280, _thumbnailCancellation.Token);
            if (!_thumbnailCancellation.IsCancellationRequested) Thumbnail = result.Image;
        }
        catch (OperationCanceledException) { }
    }

    public void ReleaseThumbnail()
    {
        _thumbnailCancellation?.Cancel(); _thumbnailCancellation?.Dispose(); _thumbnailCancellation = null; Thumbnail = null;
    }
}
