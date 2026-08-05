using System.Collections.ObjectModel;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows.Data;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Core.Services.Tasks;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly FileNameNormalizer _normalizer;
    private readonly InputParserService _inputParser;
    private readonly MediaIndexService _indexService;
    private readonly MediaMatchService _matchService;
    private readonly MediaCopyService _copyService;
    private readonly MediaReportService _reportService;
    private readonly SettingsService _settingsService;
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly IClipboardService _clipboardService;
    private readonly OnboardingService _onboardingService;
    private readonly ILicenseService _licenseService;
    private readonly IFeatureGateService _featureGateService;
    private readonly ProjectEntitlementService _projectEntitlementService;
    private readonly ProjectHistoryService _projectHistoryService;
    private readonly OutputPresetService _outputPresetService;
    private readonly BatchProjectService _batchProjectService;
    private readonly IAppearanceService _appearanceService;
    private readonly TaskOperationBridge _taskOperationBridge;
    private readonly IQuickToolsRepository _quickToolsRepository;
    private readonly IMatchDecisionRepository _matchDecisionRepository;
    private readonly WeatherFeatureState? _weatherState;
    private CancellationTokenSource? _operationCancellation;
    private MediaIndexSnapshot _mediaIndex = new();
    private SourceDirectoryEntry? _selectedSource;
    private string _textInput = string.Empty;
    private string _projectName = string.Empty;
    private string _outputBaseDirectory = string.Empty;
    private string _outputFolderName = string.Empty;
    private string _customExtensionsText = string.Empty;
    private string _customExtensionsError = string.Empty;
    private OutputMode _outputMode = OutputMode.ByFileCategory;
    private CollectionCategory _collectionCategory = CollectionCategory.JpegAndRaw;
    private CustomerJpegHandlingMode _customerJpegMode = CustomerJpegHandlingMode.Strict;
    private bool _isBusy;
    private bool _matchCompleted;
    private bool _initialized;
    private string _statusMessage = "准备就绪";
    private string _currentItem = string.Empty;
    private double _progressPercent;
    private long _processedCount;
    private int _totalCount;
    private int _targetFileCount;
    private int _jpegMatchedCount;
    private int _rawMatchedCount;
    private int _completeMatchedCount;
    private int _partialMatchedCount;
    private int _conflictCount;
    private int _notFoundCount;
    private int _copiedCount;
    private int _indexedMediaCount;
    private bool _tutorialCancellationDemoActive;
    private bool _tutorialDetailsViewed;
    private bool _tutorialOutputOpened;
    private string _tutorialOutputDirectoryOverride = string.Empty;
    private NormalWorkspaceSnapshot? _normalWorkspaceSnapshot;
    private string _currentPage = "Workbench";
    private int _currentWorkflowStep = 1;
    private string _licenseKeyInput = "KQGP-";
    private PhotoProjectRecord _currentProject = new();
    private string _searchQuery = string.Empty;
    private bool _onlyShowAttentionItems;
    private MediaSelectionItem? _selectedSelection;
    private string _toastMessage = string.Empty;
    private bool _isToastVisible;
    private CancellationTokenSource? _toastCancellation;
    private bool _exportReportsForCurrentProject;
    private bool _exportCsvForCurrentProject = true;
    private bool _exportJsonForCurrentProject;
    private bool _exportLogForCurrentProject;
    private bool _isSettingsModalOpen;
    private bool _quickToolsCompact;

    public MainViewModel(
        FileNameNormalizer normalizer,
        InputParserService inputParser,
        MediaIndexService indexService,
        MediaMatchService matchService,
        MediaCopyService copyService,
        MediaReportService reportService,
        SettingsService settingsService,
        ILogService logService,
        IDialogService dialogService,
        IClipboardService clipboardService,
        OnboardingService onboardingService,
        ILicenseService licenseService,
        IFeatureGateService featureGateService,
        ProjectEntitlementService projectEntitlementService,
        ProjectHistoryService projectHistoryService,
        OutputPresetService outputPresetService,
        BatchProjectService batchProjectService,
        IAppearanceService appearanceService,
        TaskCenterViewModel taskCenter,
        TaskOperationBridge taskOperationBridge,
        IQuickToolsRepository quickToolsRepository,
        IMatchDecisionRepository matchDecisionRepository,
        WorkCalendarViewModel workCalendarPage,
        WorkbenchScheduleViewModel? workbenchSchedule = null,
        ReminderNotificationCenterViewModel? reminderNotifications = null,
        WeatherFeatureState? weatherState = null,
        TetherCaptureViewModel? tetherPage = null)
    {
        _normalizer = normalizer;
        _inputParser = inputParser;
        _indexService = indexService;
        _matchService = matchService;
        _copyService = copyService;
        _reportService = reportService;
        _settingsService = settingsService;
        _logService = logService;
        _dialogService = dialogService;
        _clipboardService = clipboardService;
        _onboardingService = onboardingService;
        _licenseService = licenseService;
        _featureGateService = featureGateService;
        _projectEntitlementService = projectEntitlementService;
        _projectHistoryService = projectHistoryService;
        _outputPresetService = outputPresetService;
        _batchProjectService = batchProjectService;
        _appearanceService = appearanceService;
        TaskCenter = taskCenter;
        _taskOperationBridge = taskOperationBridge;
        _quickToolsRepository = quickToolsRepository;
        _matchDecisionRepository = matchDecisionRepository;
        _weatherState = weatherState;
        WorkCalendarPage = workCalendarPage;
        WorkbenchSchedule = workbenchSchedule;
        ReminderNotifications = reminderNotifications;
        TetherPage = tetherPage;
        if (ReminderNotifications is not null) ReminderNotifications.OpenBookingRequested += ReminderNotifications_OpenBookingRequested;
        if (WorkbenchSchedule is not null) WorkbenchSchedule.OpenBookingRequested += ReminderNotifications_OpenBookingRequested;
        if (WorkbenchSchedule is not null) WorkbenchSchedule.OpenCalendarRequested += WorkbenchSchedule_OpenCalendarRequested;
        OrganizePhotosPage = new OrganizePhotosViewModel(new OrganizeService(logService), dialogService, taskOperationBridge);
        CollagePage = new CollageViewModel(new CollageExportService(), dialogService, taskOperationBridge);
        _licenseService.LicenseChanged += (_, _) => OnLicenseChanged();

        AddSourceCommand = new RelayCommand(_ => AddSource(), _ => !IsBusy && CanTutorial(TutorialAction.AddSourceDirectory));
        RemoveSourceCommand = new RelayCommand(_ => RemoveSource(), _ => !IsBusy && SelectedSource is not null && CanTutorial(TutorialAction.RemoveSourceDirectory));
        ClearSourcesCommand = new RelayCommand(_ => ClearSources(), _ => !IsBusy && Sources.Count > 0);
        MoveSourceUpCommand = new RelayCommand(_ => MoveSource(-1), _ => !IsBusy && SelectedSource is not null && Sources.IndexOf(SelectedSource) > 0);
        MoveSourceDownCommand = new RelayCommand(_ => MoveSource(1), _ => !IsBusy && SelectedSource is not null && Sources.IndexOf(SelectedSource) >= 0 && Sources.IndexOf(SelectedSource) < Sources.Count - 1);
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutput(), _ => !IsBusy && CanTutorial(TutorialAction.SelectOutputDirectory));
        ParseTextCommand = new RelayCommand(_ => ParseText(), _ => !IsBusy && !string.IsNullOrWhiteSpace(TextInput) && CanTutorial(TutorialAction.ParseNumbers));
        PasteCommand = new RelayCommand(_ => PasteText(), _ => !IsBusy && CanTutorial(TutorialAction.PasteNumbers));
        ClearSelectionsCommand = new RelayCommand(_ => ClearSelections(), _ => !IsBusy && Selections.Count > 0 && CanTutorial(TutorialAction.ClearSelections));
        ScanCommand = new AsyncRelayCommand(_ => ScanAsync(), _ => !IsBusy && Sources.Count > 0 && string.IsNullOrEmpty(CustomExtensionsError) && CanTutorial(TutorialAction.ScanSourceFiles));
        MatchCommand = new AsyncRelayCommand(_ => MatchAsync(), _ => !IsBusy && Selections.Count > 0 && IsCategoryConfigurationValid() && CanTutorial(TutorialAction.MatchFiles));
        CopyCommand = new AsyncRelayCommand(_ => CopyAsync(), _ => CanCopy() && CanTutorial(TutorialAction.CopyMatchedFiles));
        ExportReportCommand = new AsyncRelayCommand(_ => ExportReportAsync(), _ => !IsBusy && Selections.Count > 0 && !string.IsNullOrWhiteSpace(OutputBaseDirectory) && CanTutorial(TutorialAction.ExportReports));
        OpenOutputCommand = new RelayCommand(_ => OpenOutput(), _ => !IsBusy && Directory.Exists(OutputDirectory) && CanTutorial(TutorialAction.OpenOutputDirectory));
        CancelCommand = new RelayCommand(_ => CancelCurrentOperation(), _ => IsBusy && (!IsOnboardingActive || CanTutorial(TutorialAction.CancelSimulatedTask)));
        ClearTaskCommand = new RelayCommand(_ => ClearTask(), _ => !IsBusy && (Selections.Count > 0 || !string.IsNullOrWhiteSpace(TextInput)) && CanTutorial(TutorialAction.ClearCurrentTask));
        ShowDetailsCommand = new RelayCommand(ShowDetails, item => !IsBusy && item is MediaSelectionItem media && media.FormatResults.Count > 0 && CanTutorial(TutorialAction.ViewDetails));
        TutorialPrimaryCommand = new AsyncRelayCommand(_ => TutorialPrimaryAsync(), _ => IsOnboardingActive && ShowTutorialPrimaryAction);
        TutorialBackCommand = new AsyncRelayCommand(_ => TutorialBackAsync(), _ => IsOnboardingActive && TutorialCanGoBack);
        TutorialExitCommand = new RelayCommand(_ => ExitTutorial());
        TutorialRetryCommand = new RelayCommand(_ => TutorialErrorMessage = string.Empty, _ => IsOnboardingActive && !string.IsNullOrWhiteSpace(TutorialErrorMessage));
        TutorialRecreateDataCommand = new AsyncRelayCommand(_ => RecreateTutorialDataAsync(), _ => IsOnboardingActive);
        HelpCommand = new AsyncRelayCommand(_ => ShowHelpAsync(), _ => !IsBusy && !IsOnboardingRequired);
        FeedbackCommand = new RelayCommand(_ => _dialogService.ShowFeedback(), _ => !IsOnboardingRequired);
        NavigateCommand = new RelayCommand(Navigate, _ => !IsBusy && !IsOnboardingRequired);
        OpenSettingsCommand = new RelayCommand(_ => IsSettingsModalOpen = true);
        OpenToolboxPageCommand = new RelayCommand(_ => CurrentPage = "Toolbox");
        TogglePinnedToolCommand = new RelayCommand(parameter => TogglePinnedTool(parameter?.ToString()));
        MovePinnedToolLeftCommand = new RelayCommand(parameter => MovePinnedTool(parameter?.ToString(), -1));
        MovePinnedToolRightCommand = new RelayCommand(parameter => MovePinnedTool(parameter?.ToString(), 1));
        RemovePinnedToolCommand = new RelayCommand(parameter => RemovePinnedTool(parameter?.ToString()));
        ResetQuickToolsCommand = new RelayCommand(_ => ResetQuickTools());
        ManageQuickToolsCommand = new RelayCommand(_ => ManageQuickTools());
        GoToWorkflowStepCommand = new RelayCommand(GoToWorkflowStep, _ => !IsBusy);
        NewProjectCommand = new RelayCommand(_ => StartNewProject(), _ => !IsBusy && !IsOnboardingRequired);
        ContinueProjectCommand = new AsyncRelayCommand(ContinueProjectAsync, _ => !IsBusy && ProjectHistory.Count > 0 && !IsOnboardingRequired);
        ActivateLicenseCommand = new AsyncRelayCommand(_ => ActivateLicenseAsync(), _ => !IsBusy && LicenseKeyFormatter.IsComplete(LicenseKeyInput));
        DeactivateLicenseCommand = new AsyncRelayCommand(_ => DeactivateLicenseAsync(), _ => !IsBusy && IsProEdition);
        ValidateLicenseCommand = new AsyncRelayCommand(_ => ValidateLicenseAsync(), _ => !IsBusy && IsProEdition);
        PurchaseCommand = new RelayCommand(_ => OpenPurchasePage(), _ => !string.IsNullOrWhiteSpace(_licenseService.Configuration.PurchaseUrl));
        SaveOutputPresetCommand = new AsyncRelayCommand(_ => SaveCurrentOutputPresetAsync(), _ => !IsBusy);
        SaveProjectCommand = new AsyncRelayCommand(_ => SaveCurrentProjectAsync(), _ => !IsBusy && !IsOnboardingActive);
        RunBatchCommand = new AsyncRelayCommand(_ => RunBatchAsync(), _ => !IsBusy && ProjectHistory.Count > 0);
        ToggleSidebarCommand = new RelayCommand(_ => ToggleSidebar());
        ToggleCompactDensityCommand = new RelayCommand(_ => SelectedDensity = IsCompactDensity ? InterfaceDensity.Comfortable : InterfaceDensity.Compact);
        SetThemeCommand = new RelayCommand(SetTheme);
        ResetAppearanceCommand = new RelayCommand(_ => ResetAppearance());
        ApplyCustomAccentCommand = new RelayCommand(_ => ApplyCustomAccent());
        OpenLogDirectoryCommand = new RelayCommand(_ => OpenLogDirectory());
        DismissToastCommand = new RelayCommand(_ => DismissToast());
        CloseSettingsCommand = new RelayCommand(_ => IsSettingsModalOpen = false);
        ExitCommand = new RelayCommand(_ => CloseRequested?.Invoke(this, EventArgs.Empty));

        Sources.CollectionChanged += (_, _) => RefreshCommands();
        SelectionView = CollectionViewSource.GetDefaultView(Selections);
        SelectionView.Filter = FilterSelection;
        Selections.CollectionChanged += (_, _) => { UpdateStatistics(); SelectionView.Refresh(); RefreshCommands(); };
        RefreshToolPinState();
        RegenerateOutputFolderName();
    }

    public ObservableCollection<SourceDirectoryEntry> Sources { get; } = [];
    public ObservableCollection<MediaSelectionItem> Selections { get; } = [];
    public ICollectionView SelectionView { get; }
    public ObservableCollection<PhotoProjectRecord> ProjectHistory { get; } = [];
    public TaskCenterViewModel TaskCenter { get; }
    public ObservableCollection<OutputPreset> OutputPresets { get; } = [];
    public bool IsSettingsModalOpen
    {
        get => _isSettingsModalOpen;
        set => SetProperty(ref _isSettingsModalOpen, value);
    }
    public AppSettings Settings { get; private set; } = new();
    public OrganizePhotosViewModel OrganizePhotosPage { get; }
    public CollageViewModel CollagePage { get; }
    public WorkCalendarViewModel WorkCalendarPage { get; }
    public WorkbenchScheduleViewModel? WorkbenchSchedule { get; }
    public ReminderNotificationCenterViewModel? ReminderNotifications { get; }
    public TetherCaptureViewModel? TetherPage { get; }
    public IReadOnlyList<CollectionCategoryOption> CollectionCategories { get; } =
    [
        new(CollectionCategory.JpegOnly, "仅 JPG"),
        new(CollectionCategory.RawOnly, "仅 RAW"),
        new(CollectionCategory.JpegAndRaw, "JPG + RAW（默认）"),
        new(CollectionCategory.Custom, "自定义格式")
    ];
    public IReadOnlyList<OutputModeOption> OutputModes { get; } =
    [
        new(OutputMode.ByFileCategory, "按文件类别输出（JPG / RAW / OTHER）"),
        new(OutputMode.Flat, "全部放入同一文件夹"),
        new(OutputMode.PreserveRelativeStructure, "保留来源相对目录结构")
    ];
    public IReadOnlyList<CustomerJpegModeOption> CustomerJpegModes { get; } =
    [
        new(CustomerJpegHandlingMode.Strict, "严格模式（默认）"),
        new(CustomerJpegHandlingMode.SmartBackup, "智能备用模式（需手动确认）"),
        new(CustomerJpegHandlingMode.AllowCustomerFile, "允许客户文件模式")
    ];
    public IReadOnlyList<SourceDirectoryTypeOption> SourceDirectoryTypes { get; } =
    [
        new(SourceDirectoryType.Jpeg, "JPG 来源目录"),
        new(SourceDirectoryType.Raw, "RAW 来源目录"),
        new(SourceDirectoryType.Mixed, "JPG + RAW 混合目录"),
        new(SourceDirectoryType.Other, "其他格式目录")
    ];
    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(ThemeMode.System, "跟随 Windows"), new(ThemeMode.Light, "浅色"), new(ThemeMode.Dark, "深色")
    ];
    public IReadOnlyList<AccentOption> AccentOptions { get; } =
    [
        new(AccentPreset.System, "Windows 强调色"), new(AccentPreset.KitaoBlue, "蛋挞黄"), new(AccentPreset.MossGreen, "苔藓绿"),
        new(AccentPreset.WineRed, "酒红"), new(AccentPreset.NightPurple, "夜紫"), new(AccentPreset.WarmAmber, "暖琥珀"),
        new(AccentPreset.Graphite, "石墨灰"), new(AccentPreset.Custom, "自定义")
    ];
    public IReadOnlyList<DensityOption> DensityOptions { get; } =
    [
        new(InterfaceDensity.Comfortable, "舒适"), new(InterfaceDensity.Compact, "紧凑")
    ];
    public IReadOnlyList<SidebarOption> SidebarOptions { get; } =
    [
        new(SidebarMode.AlwaysExpanded, "始终展开"), new(SidebarMode.AutoCollapse, "窄窗口自动收起"), new(SidebarMode.Remember, "记住上次状态")
    ];
    public IReadOnlyList<MotionOption> MotionOptions { get; } =
    [
        new(MotionPreference.Normal, "标准动效"), new(MotionPreference.Reduced, "减少动效")
    ];
    public IReadOnlyList<FontScaleOption> FontScaleOptions { get; } =
    [
        new(FontScale.Standard, "标准字号"), new(FontScale.Large, "大字号")
    ];

    public SourceDirectoryEntry? SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (!SetProperty(ref _selectedSource, value)) return;
            OnPropertyChanged(nameof(SelectedSourceDirectoryType));
            RefreshCommands();
        }
    }
    public SourceDirectoryType SelectedSourceDirectoryType
    {
        get => SelectedSource?.DirectoryType ?? SourceDirectoryType.Mixed;
        set
        {
            if (SelectedSource is null || SelectedSource.DirectoryType == value) return;
            SelectedSource.DirectoryType = value;
            InvalidateMediaIndex("照片来源目录用途已变化，请重新扫描索引。", clearMatches: true);
            OnPropertyChanged();
        }
    }
    public string TextInput { get => _textInput; set { if (SetProperty(ref _textInput, value)) RefreshCommands(); } }
    public string ProjectName
    {
        get => _projectName;
        set
        {
            if (!SetProperty(ref _projectName, value)) return;
            RegenerateOutputFolderName();
            if (_initialized && IsOnboardingActive && CanTutorial(TutorialAction.EnterProjectName) && value == Branding.TutorialProjectName)
                _ = AdvanceTutorialAsync(TutorialAction.EnterProjectName, CreateTutorialContext());
        }
    }
    public string OutputBaseDirectory { get => _outputBaseDirectory; set { if (SetProperty(ref _outputBaseDirectory, value)) { OnPropertyChanged(nameof(OutputDirectory)); RefreshCommands(); } } }
    public string OutputFolderName { get => _outputFolderName; private set { if (SetProperty(ref _outputFolderName, value)) OnPropertyChanged(nameof(OutputDirectory)); } }
    public string OutputDirectory => IsOnboardingActive && !string.IsNullOrWhiteSpace(_tutorialOutputDirectoryOverride)
        ? _tutorialOutputDirectoryOverride
        : string.IsNullOrWhiteSpace(OutputBaseDirectory) ? string.Empty : Path.Combine(OutputBaseDirectory, OutputFolderName);
    public OutputMode OutputMode
    {
        get => _outputMode;
        set
        {
            if (!SetProperty(ref _outputMode, value)) return;
            if (_initialized && IsOnboardingActive && CanTutorial(TutorialAction.SelectOutputModes))
                _ = AdvanceTutorialAsync(TutorialAction.SelectOutputModes, CreateTutorialContext());
        }
    }

    public CollectionCategory CollectionCategory
    {
        get => _collectionCategory;
        set
        {
            if (_initialized && !IsOnboardingActive && value == CollectionCategory.Custom &&
                !_featureGateService.HasAccess(LicensedFeature.CustomFileFormats))
            {
                ShowUpgradePrompt(_featureGateService.Check(LicensedFeature.CustomFileFormats).Message);
                return;
            }
            if (!SetProperty(ref _collectionCategory, value)) return;
            OnPropertyChanged(nameof(IsCustomCategory));
            RegenerateOutputFolderName();
            if (_initialized && IsOnboardingActive && CanTutorial(TutorialAction.SelectCollectionCategories))
                _ = AdvanceTutorialAsync(TutorialAction.SelectCollectionCategories, CreateTutorialContext());
            else if (_initialized && !IsOnboardingActive) QueueRematch("归片类别已切换，正在更新匹配结果");
        }
    }

    public bool IsCustomCategory => CollectionCategory == CollectionCategory.Custom;

    public string CustomExtensionsText
    {
        get => _customExtensionsText;
        set
        {
            if (!SetProperty(ref _customExtensionsText, value)) return;
            var parsed = MediaExtensionPolicy.ParseCustomExtensions(value);
            CustomExtensionsError = parsed.ErrorMessage;
            if (!parsed.IsValid) return;
            Settings.CustomExtensions = parsed.Extensions.ToList();
            if (_initialized)
            {
                InvalidateMediaIndex("自定义扩展名已变化，请重新扫描照片来源目录。", clearMatches: true);
            }
        }
    }

    public string CustomExtensionsError { get => _customExtensionsError; private set { if (SetProperty(ref _customExtensionsError, value)) RefreshCommands(); } }

    public CustomerJpegHandlingMode CustomerJpegMode
    {
        get => _customerJpegMode;
        set
        {
            if (_initialized && !IsOnboardingActive && value != CustomerJpegHandlingMode.Strict &&
                !_featureGateService.HasAccess(LicensedFeature.AdvancedJpegQualityAssessment))
            {
                ShowUpgradePrompt(_featureGateService.Check(LicensedFeature.AdvancedJpegQualityAssessment).Message);
                return;
            }
            if (!SetProperty(ref _customerJpegMode, value)) return;
            OnPropertyChanged(nameof(AllowCustomerJpegFallback));
            OnPropertyChanged(nameof(ShowCustomerJpegWarning));
            if (_initialized) QueueRematch("客户 JPG 处理模式已更新，正在重新匹配");
        }
    }
    public bool AllowCustomerJpegFallback
    {
        get => CustomerJpegMode == CustomerJpegHandlingMode.AllowCustomerFile;
        set => CustomerJpegMode = value ? CustomerJpegHandlingMode.AllowCustomerFile : CustomerJpegHandlingMode.Strict;
    }
    public bool ShowCustomerJpegWarning => CustomerJpegMode != CustomerJpegHandlingMode.Strict || IsOnboardingActive && TutorialTarget == TutorialTarget.JpegQualityArea;
    public bool NeedsUpgradeTutorialOffer => _onboardingService.NeedsUpgradeOffer;

    public string CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (!SetProperty(ref _currentPage, value)) return;
            OnPropertyChanged(nameof(IsWorkbenchPage));
            OnPropertyChanged(nameof(IsProjectCenterPage));
            OnPropertyChanged(nameof(IsLocalSplitPage));
            OnPropertyChanged(nameof(IsWorkflowPage));
            OnPropertyChanged(nameof(IsHistoryPage));
            OnPropertyChanged(nameof(IsWorkCalendarPage));
            OnPropertyChanged(nameof(IsTetherPage));
            OnPropertyChanged(nameof(IsActivationPage));
            OnPropertyChanged(nameof(IsSettingsPage));
            OnPropertyChanged(nameof(IsHelpPage));
            OnPropertyChanged(nameof(IsBatchCompressPage));
            OnPropertyChanged(nameof(IsWatermarkPage));
            OnPropertyChanged(nameof(IsDeleteRejectsPage));
            OnPropertyChanged(nameof(IsFtpToolPage));
            OnPropertyChanged(nameof(IsPhotoOrganizePage));
            OnPropertyChanged(nameof(IsBatchRenamePage));
            OnPropertyChanged(nameof(IsBatchConvertPage));
            OnPropertyChanged(nameof(IsPhotoGroupingPage));
            OnPropertyChanged(nameof(IsCollagePage));
            OnPropertyChanged(nameof(IsToolboxPage));
            if (IsWorkbenchPage && WorkbenchSchedule is not null) _ = WorkbenchSchedule.RefreshAsync();
        }
    }
    public bool IsWorkbenchPage => CurrentPage is "Workbench" or "ProjectCenter";
    public bool IsProjectCenterPage => IsWorkbenchPage;
    public bool IsLocalSplitPage => CurrentPage == "LocalSplit";
    public bool IsWorkflowPage => CurrentPage == "Workflow";
    public bool IsHistoryPage => CurrentPage == "History";
    public bool IsWorkCalendarPage => CurrentPage == "WorkCalendar";
    public bool IsTetherPage => CurrentPage == "Tether";
    public bool IsActivationPage => CurrentPage == "Activation";
    public bool IsSettingsPage => CurrentPage == "Settings";
    public bool IsHelpPage => CurrentPage == "Help";
    public bool IsBatchCompressPage => CurrentPage == "BatchCompress";
    public bool IsWatermarkPage => CurrentPage == "Watermark";
    public bool IsDeleteRejectsPage => CurrentPage == "DeleteRejects";
    public bool IsFtpToolPage => CurrentPage == "FtpTool";
    public bool IsPhotoOrganizePage => CurrentPage == "PhotoOrganize";
    public bool IsBatchRenamePage => CurrentPage == "BatchRename";
    public bool IsBatchConvertPage => CurrentPage == "BatchConvert";
    public bool IsPhotoGroupingPage => CurrentPage == "PhotoGrouping";
    public bool IsCollagePage => CurrentPage == "Collage";
    public bool IsToolboxPage => CurrentPage == "Toolbox";
    public ObservableCollection<ToolboxItemViewModel> ToolboxItems { get; } =
        new(ToolRegistry.All.Select(definition => new ToolboxItemViewModel(definition)));
    public IReadOnlyList<ToolboxItemViewModel> ToolCatalogItems =>
        ToolboxItems.Where(item => item.Definition.Id != ToolId.Toolbox).ToList();
    public IReadOnlyList<ToolDefinition> ToolMenuItems => ToolRegistry.All;
    public ToolboxItemViewModel ToolboxEntry => ToolboxItems.Single(item => item.Definition.Id == ToolId.Toolbox);
    public IReadOnlyList<ToolboxItemViewModel> PinnedToolboxItems => Settings.PinnedQuickTools
        .Select(id => ToolboxItems.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)))
        .Where(item => item is not null && item.IsPinned)
        .Cast<ToolboxItemViewModel>()
        .Take(QuickToolsService.MaximumPinnedTools)
        .ToList();
    public IReadOnlyList<ToolboxItemViewModel> DisplayedPinnedToolboxItems => _quickToolsCompact ? PinnedToolboxItems.Take(2).ToList() : PinnedToolboxItems;
    public IReadOnlyList<ToolboxItemViewModel> OverflowPinnedToolboxItems => _quickToolsCompact ? PinnedToolboxItems.Skip(2).ToList() : [];
    public bool IsToolPinned(string id) => ToolRegistry.TryGet(id, out var definition) &&
        Settings.PinnedQuickTools.Contains(definition.SettingsId, StringComparer.OrdinalIgnoreCase);
    public bool CanPinTool(string id) => ToolRegistry.TryGet(id, out var definition) && definition.CanPin &&
        (IsToolPinned(id) || QuickToolsService.Normalize(Settings.PinnedQuickTools).Count < QuickToolsService.MaximumPinnedTools);
    public string LocalSplitHelpText => "导入 TXT、客户选图 JPG 或照片编号，匹配本地 JPG、RAW 及相关文件。";
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (!SetProperty(ref _searchQuery, value)) return;
            SelectionView.Refresh();
        }
    }
    public bool OnlyShowAttentionItems
    {
        get => _onlyShowAttentionItems;
        set
        {
            if (!SetProperty(ref _onlyShowAttentionItems, value)) return;
            SelectionView.Refresh();
        }
    }
    public MediaSelectionItem? SelectedSelection { get => _selectedSelection; set => SetProperty(ref _selectedSelection, value); }
    public bool IsToastVisible { get => _isToastVisible; private set => SetProperty(ref _isToastVisible, value); }
    public string ToastMessage { get => _toastMessage; private set => SetProperty(ref _toastMessage, value); }
    public bool DefaultExportReports
    {
        get => Settings.ReportSettings.DefaultExportEnabled;
        set
        {
            if (Settings.ReportSettings.DefaultExportEnabled == value) return;
            Settings.ReportSettings.DefaultExportEnabled = value;
            OnPropertyChanged();
            _ = SaveSettingsAsync();
        }
    }
    public bool DefaultExportCsv
    {
        get => Settings.ReportSettings.DefaultExportCsv;
        set
        {
            if (Settings.ReportSettings.DefaultExportCsv == value) return;
            Settings.ReportSettings.DefaultExportCsv = value;
            OnPropertyChanged();
            _ = SaveSettingsAsync();
        }
    }
    public bool DefaultExportJson
    {
        get => Settings.ReportSettings.DefaultExportJson;
        set
        {
            if (Settings.ReportSettings.DefaultExportJson == value) return;
            Settings.ReportSettings.DefaultExportJson = value;
            OnPropertyChanged();
            _ = SaveSettingsAsync();
        }
    }
    public bool DefaultExportLog
    {
        get => Settings.ReportSettings.DefaultExportLog;
        set
        {
            if (Settings.ReportSettings.DefaultExportLog == value) return;
            Settings.ReportSettings.DefaultExportLog = value;
            OnPropertyChanged();
            _ = SaveSettingsAsync();
        }
    }
    public bool ExportReportsForCurrentProject
    {
        get => _exportReportsForCurrentProject;
        set
        {
            if (!SetProperty(ref _exportReportsForCurrentProject, value)) return;
            OnPropertyChanged(nameof(ReportSelectionSummary));
        }
    }
    public bool ExportCsvForCurrentProject
    {
        get => _exportCsvForCurrentProject;
        set { if (SetProperty(ref _exportCsvForCurrentProject, value)) OnPropertyChanged(nameof(ReportSelectionSummary)); }
    }
    public bool ExportJsonForCurrentProject
    {
        get => _exportJsonForCurrentProject;
        set { if (SetProperty(ref _exportJsonForCurrentProject, value)) OnPropertyChanged(nameof(ReportSelectionSummary)); }
    }
    public bool ExportLogForCurrentProject
    {
        get => _exportLogForCurrentProject;
        set { if (SetProperty(ref _exportLogForCurrentProject, value)) OnPropertyChanged(nameof(ReportSelectionSummary)); }
    }
    public bool CanExportAdvancedReports => IsOnboardingActive || _featureGateService.HasAccess(LicensedFeature.AdvancedReports);
    public string ReportSelectionSummary => !ExportReportsForCurrentProject
        ? "复制完成后不自动生成报告，可随时手动导出。"
        : CanExportAdvancedReports
            ? $"将自动导出：{string.Join("、", SelectedReportLabels())}"
            : "免费版将自动导出基础 CSV；JSON 与操作日志需专业版。";

    public ThemeMode SelectedTheme
    {
        get => Settings.Appearance.Theme;
        set
        {
            if (Settings.Appearance.Theme == value) return;
            Settings.Appearance.Theme = value;
            ApplyAppearance("主题已更新");
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThemeSummary));
        }
    }
    public AccentPreset SelectedAccent
    {
        get => Settings.Appearance.Accent;
        set
        {
            if (Settings.Appearance.Accent == value) return;
            Settings.Appearance.Accent = value;
            ApplyAppearance("强调色已更新");
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCustomAccent));
            OnPropertyChanged(nameof(AccentPreviewHex));
        }
    }
    public string CustomAccentColor
    {
        get => Settings.Appearance.CustomAccentColor;
        set
        {
            if (Settings.Appearance.CustomAccentColor == value) return;
            Settings.Appearance.CustomAccentColor = value;
            OnPropertyChanged();
        }
    }
    public bool IsCustomAccent => SelectedAccent == AccentPreset.Custom;
    public string AccentPreviewHex => _appearanceService.ResolveAccentHex(Settings.Appearance);
    public InterfaceDensity SelectedDensity
    {
        get => Settings.Appearance.Density;
        set
        {
            if (Settings.Appearance.Density == value) return;
            Settings.Appearance.Density = value;
            OnPropertyChanged(nameof(IsCompactDensity));
            ApplyAppearance("界面密度已更新");
            OnPropertyChanged();
        }
    }
    public bool IsCompactDensity => SelectedDensity == InterfaceDensity.Compact;
    public SidebarMode SelectedSidebarMode
    {
        get => Settings.Appearance.Sidebar;
        set
        {
            if (Settings.Appearance.Sidebar == value) return;
            Settings.Appearance.Sidebar = value;
            if (value == SidebarMode.AlwaysExpanded) Settings.Appearance.SidebarCollapsed = false;
            ApplyAppearance("侧栏行为已更新");
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSidebarCollapsed));
            OnPropertyChanged(nameof(IsSidebarExpanded));
            OnPropertyChanged(nameof(SidebarWidth));
        }
    }
    public MotionPreference SelectedMotion
    {
        get => Settings.Appearance.Motion;
        set
        {
            if (Settings.Appearance.Motion == value) return;
            Settings.Appearance.Motion = value;
            ApplyAppearance("动效偏好已更新");
            OnPropertyChanged();
        }
    }
    public FontScale SelectedFontScale
    {
        get => Settings.Appearance.FontScale;
        set
        {
            if (Settings.Appearance.FontScale == value) return;
            Settings.Appearance.FontScale = value;
            ApplyAppearance("字号已更新");
            OnPropertyChanged();
        }
    }
    public bool IsSidebarCollapsed => Settings.Appearance.SidebarCollapsed;
    public bool IsSidebarExpanded => !IsSidebarCollapsed;
    public double SidebarWidth => IsSidebarCollapsed
        ? SidebarLayoutMetrics.CollapsedWidth
        : SidebarLayoutMetrics.ExpandedWidth;
    public int PendingTaskCount => IsBusy ? 1 : 0;
    public int AttentionCount => ConflictCount + NotFoundCount + PartialMatchedCount;
    public string TaskCenterSummary => IsBusy ? $"正在处理：{StatusMessage}" : "暂无待处理任务";
    public string ThemeSummary => SelectedTheme switch { ThemeMode.Light => "浅色", ThemeMode.Dark => "深色", _ => "跟随系统" };
    public int CurrentWorkflowStep
    {
        get => _currentWorkflowStep;
        private set
        {
            if (!SetProperty(ref _currentWorkflowStep, Math.Clamp(value, 1, 4))) return;
            OnPropertyChanged(nameof(IsWorkflowStepOne));
            OnPropertyChanged(nameof(IsWorkflowStepTwo));
            OnPropertyChanged(nameof(IsWorkflowStepThree));
            OnPropertyChanged(nameof(IsWorkflowStepFour));
            OnPropertyChanged(nameof(WorkflowStepTitle));
        }
    }
    public bool IsWorkflowStepOne => CurrentWorkflowStep == 1;
    public bool IsWorkflowStepTwo => CurrentWorkflowStep == 2;
    public bool IsWorkflowStepThree => CurrentWorkflowStep == 3;
    public bool IsWorkflowStepFour => CurrentWorkflowStep == 4;
    public string WorkflowStepTitle => CurrentWorkflowStep switch
    {
        1 => "第 1 步 · 添加照片来源并建立索引",
        2 => "第 2 步 · 导入客户选片编号",
        3 => "第 3 步 · 匹配并检查结果",
        _ => "第 4 步 · 设置输出、复制与报告"
    };
    public bool IsFreeEdition => !_licenseService.Current.IsPro;
    public bool IsProEdition => _licenseService.Current.IsPro;
    public string EditionLabel => IsProEdition ? "专业版" : "免费版";
    public string EditionActionText => IsProEdition ? "已激活" : "升级专业版";
    public string LicenseStatusMessage => _licenseService.Current.Message;
    public string LicenseDeviceText => $"设备：{_licenseService.Current.DeviceName}  ({_licenseService.Current.DeviceCount}/{_licenseService.Current.MaxDevices})";
    public string LicenseKeySuffixText => string.IsNullOrWhiteSpace(_licenseService.Current.LicenseKeySuffix)
        ? "未激活"
        : $"激活码尾号：{_licenseService.Current.LicenseKeySuffix}";
    public string OfflineLicenseText => _licenseService.Current.OfflineExpiresAt is { } expires
        ? $"离线可用至：{expires.LocalDateTime:yyyy-MM-dd HH:mm}"
        : "尚无离线授权凭据";
    public string LicenseActivatedAtText => _licenseService.Current.ActivatedAt is { } activated
        ? $"激活时间：{activated.LocalDateTime:yyyy-MM-dd HH:mm}"
        : "激活时间：—";
    public string LicenseLastValidatedText => _licenseService.Current.LastValidatedAt is { } validated
        ? $"最近验证：{validated.LocalDateTime:yyyy-MM-dd HH:mm}"
        : "最近验证：—";
    public string LicenseOfflineRemainingText => _licenseService.Current.OfflineRemaining(DateTimeOffset.UtcNow) is { } remaining
        ? remaining <= TimeSpan.Zero ? "离线宽限已结束" : $"离线剩余：{Math.Ceiling(remaining.TotalDays):N0} 天"
        : "离线剩余：—";
    public bool IsProductionLicenseConfigured => _licenseService.Configuration.IsCryptolensConfigured;
    public string ProductionLicenseConfigurationText => IsProductionLicenseConfigured
        ? "生产授权服务参数已配置"
        : "生产授权服务尚未配置；免费版可正常使用";
    public string LicenseKeyInput
    {
        get => _licenseKeyInput;
        set
        {
            var formatted = LicenseKeyFormatter.Normalize(value);
            if (SetProperty(ref _licenseKeyInput, formatted)) ActivateLicenseCommand.RaiseCanExecuteChanged();
        }
    }
    public string CurrentProjectStatus => _currentProject.Status switch
    {
        PhotoProjectStatus.Completed => "已完成",
        PhotoProjectStatus.Matching => "匹配中",
        PhotoProjectStatus.Ready => "可继续",
        PhotoProjectStatus.Failed => "需要检查",
        _ => "草稿"
    };
    public bool IsCurrentProjectReadOnly => !IsOnboardingActive && !IsProEdition &&
        (CountUniqueSelections() > ProjectEntitlementService.FreeSelectionLimit ||
         Sources.Count > ProjectEntitlementService.FreeSourceDirectoryLimit ||
         CollectionCategory == CollectionCategory.Custom);
    public string CurrentProjectAccessText => IsCurrentProjectReadOnly
        ? "此项目使用了专业版能力，当前以安全只读方式打开；升级后可立即继续。"
        : IsProEdition ? "专业版权限已生效" : "免费版：30 个唯一编号、1 个来源目录";

    public bool IsOnboardingActive => _onboardingService.State.IsActive;
    public bool IsOnboardingRequired => _onboardingService.State.IsRequired;
    public TutorialTarget TutorialTarget => _onboardingService.CurrentStep.Target;
    public int TutorialStepNumber => _onboardingService.State.CurrentStep;
    public int TutorialStepCount => _onboardingService.Steps.Count;
    public string TutorialStepProgress => $"第 {TutorialStepNumber} 步，共 {TutorialStepCount} 步";
    public string TutorialTitle => _onboardingService.CurrentStep.Title;
    public string TutorialInstruction => _onboardingService.CurrentStep.Instruction;
    public bool TutorialCanGoBack => _onboardingService.CurrentStep.AllowBack;
    public string TutorialErrorMessage { get => _onboardingService.State.ErrorMessage; private set { _onboardingService.State.ErrorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasTutorialError)); RefreshCommands(); } }
    public bool HasTutorialError => !string.IsNullOrWhiteSpace(TutorialErrorMessage);
    public bool ShowTutorialPrimaryAction => _onboardingService.CurrentStep.RequiredAction is TutorialAction.BeginTutorial or TutorialAction.LoadCustomerSelection or TutorialAction.ViewDetails or TutorialAction.AcknowledgeJpegQuality or TutorialAction.AcknowledgeEditions or TutorialAction.FinishTutorial;
    public string TutorialPrimaryActionLabel => _onboardingService.CurrentStep.RequiredAction switch
    {
        TutorialAction.BeginTutorial => "开始教程",
        TutorialAction.LoadCustomerSelection => "加载教程选片",
        TutorialAction.ViewDetails => "打开第一条匹配明细",
        TutorialAction.AcknowledgeJpegQuality => "我知道了",
        TutorialAction.AcknowledgeEditions => "继续免费使用",
        TutorialAction.FinishTutorial => $"开始使用{Branding.ProductName}",
        _ => string.Empty
    };

    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) { OnPropertyChanged(nameof(PendingTaskCount)); OnPropertyChanged(nameof(TaskCenterSummary)); RefreshCommands(); } } }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string CurrentItem { get => _currentItem; private set => SetProperty(ref _currentItem, value); }
    public double ProgressPercent { get => _progressPercent; private set => SetProperty(ref _progressPercent, value); }
    public long ProcessedCount { get => _processedCount; private set => SetProperty(ref _processedCount, value); }
    public int TotalCount { get => _totalCount; private set => SetProperty(ref _totalCount, value); }
    public int TargetFileCount { get => _targetFileCount; private set => SetProperty(ref _targetFileCount, value); }
    public int JpegMatchedCount { get => _jpegMatchedCount; private set => SetProperty(ref _jpegMatchedCount, value); }
    public int RawMatchedCount { get => _rawMatchedCount; private set => SetProperty(ref _rawMatchedCount, value); }
    public int CompleteMatchedCount { get => _completeMatchedCount; private set => SetProperty(ref _completeMatchedCount, value); }
    public int PartialMatchedCount { get => _partialMatchedCount; private set => SetProperty(ref _partialMatchedCount, value); }
    public int ConflictCount { get => _conflictCount; private set => SetProperty(ref _conflictCount, value); }
    public int NotFoundCount { get => _notFoundCount; private set => SetProperty(ref _notFoundCount, value); }
    public int CopiedCount { get => _copiedCount; private set => SetProperty(ref _copiedCount, value); }
    public int IndexedMediaCount { get => _indexedMediaCount; private set { if (SetProperty(ref _indexedMediaCount, value)) RefreshCommands(); } }

    public RelayCommand AddSourceCommand { get; }
    public RelayCommand RemoveSourceCommand { get; }
    public RelayCommand ClearSourcesCommand { get; }
    public RelayCommand MoveSourceUpCommand { get; }
    public RelayCommand MoveSourceDownCommand { get; }
    public RelayCommand BrowseOutputCommand { get; }
    public RelayCommand ParseTextCommand { get; }
    public RelayCommand PasteCommand { get; }
    public RelayCommand ClearSelectionsCommand { get; }
    public AsyncRelayCommand ScanCommand { get; }
    public AsyncRelayCommand MatchCommand { get; }
    public AsyncRelayCommand CopyCommand { get; }
    public AsyncRelayCommand ExportReportCommand { get; }
    public RelayCommand OpenOutputCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ClearTaskCommand { get; }
    public RelayCommand ShowDetailsCommand { get; }
    public AsyncRelayCommand TutorialPrimaryCommand { get; }
    public AsyncRelayCommand TutorialBackCommand { get; }
    public RelayCommand TutorialExitCommand { get; }
    public RelayCommand TutorialRetryCommand { get; }
    public AsyncRelayCommand TutorialRecreateDataCommand { get; }
    public AsyncRelayCommand HelpCommand { get; }
    public RelayCommand FeedbackCommand { get; }
    public RelayCommand NavigateCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand OpenToolboxPageCommand { get; }
    public RelayCommand TogglePinnedToolCommand { get; }
    public RelayCommand MovePinnedToolLeftCommand { get; }
    public RelayCommand MovePinnedToolRightCommand { get; }
    public RelayCommand RemovePinnedToolCommand { get; }
    public RelayCommand ResetQuickToolsCommand { get; }
    public RelayCommand ManageQuickToolsCommand { get; }
    public RelayCommand GoToWorkflowStepCommand { get; }
    public RelayCommand NewProjectCommand { get; }
    public AsyncRelayCommand ContinueProjectCommand { get; }
    public AsyncRelayCommand ActivateLicenseCommand { get; }
    public AsyncRelayCommand DeactivateLicenseCommand { get; }
    public AsyncRelayCommand ValidateLicenseCommand { get; }
    public RelayCommand PurchaseCommand { get; }
    public AsyncRelayCommand SaveOutputPresetCommand { get; }
    public AsyncRelayCommand SaveProjectCommand { get; }
    public AsyncRelayCommand RunBatchCommand { get; }
    public RelayCommand ToggleSidebarCommand { get; }
    public RelayCommand SetThemeCommand { get; }
    public RelayCommand ResetAppearanceCommand { get; }
    public RelayCommand ApplyCustomAccentCommand { get; }
    public RelayCommand OpenLogDirectoryCommand { get; }
    public RelayCommand DismissToastCommand { get; }
    public RelayCommand CloseSettingsCommand { get; }
    public RelayCommand ExitCommand { get; }
    public RelayCommand ToggleCompactDensityCommand { get; }
    public event EventHandler? TutorialVisualStateChanged;
    public event EventHandler? CloseRequested;
    public event EventHandler? UpgradeTutorialOfferRequested;

    public async Task InitializeAsync()
    {
        Settings = await _settingsService.LoadAsync();
        _weatherState?.Apply(Settings.Weather);
        var databaseQuickTools = await _quickToolsRepository.LoadAsync();
        if (databaseQuickTools.Count > 0)
        {
            Settings.PinnedQuickTools = QuickToolsService.Normalize(databaseQuickTools);
            Settings.QuickToolLayout.OrderedToolIds = Settings.PinnedQuickTools.ToList();
        }
        Settings.PinnedQuickTools = QuickToolsService.Normalize(Settings.QuickToolLayout.OrderedToolIds);
        RefreshToolPinState();
        OnPropertyChanged(nameof(PinnedToolboxItems));
        OnPropertyChanged(nameof(DisplayedPinnedToolboxItems));
        OnPropertyChanged(nameof(OverflowPinnedToolboxItems));
        _appearanceService.Initialize(Settings.Appearance);
        var existingUserDetector = new ExistingUserDetectionService();
        var currentTutorialInProgress = existingUserDetector.IsCurrentTutorialInProgress(
            Settings, _settingsService.WasSettingsFilePresent, _settingsService.WasLegacySettings);
        var existingUser = existingUserDetector.IsExistingUser(
                Settings,
                _settingsService.WasLegacySettings,
                _settingsService.WasSettingsFilePresent,
                File.Exists(AppDataPaths.IndexFile) || File.Exists(Path.Combine(AppDataPaths.IndexDirectory, "media-index.json")),
                Directory.Exists(Settings.RecentOutputDirectory) && Directory.EnumerateFiles(Settings.RecentOutputDirectory, "匹配报告.*", SearchOption.AllDirectories).Any(),
                Directory.Exists(Path.Combine(AppDataPaths.LegacyRoot, "Logs")) &&
                Directory.EnumerateFiles(Path.Combine(AppDataPaths.LegacyRoot, "Logs"), "*.log", SearchOption.TopDirectoryOnly).Any(),
                currentTutorialInProgress);
        await _onboardingService.InitializeAsync(Settings, existingUser);
        var savedSources = Settings.SourceDirectories.Count > 0
            ? Settings.SourceDirectories.OrderBy(x => x.Priority)
            : Settings.RecentRawDirectories.Distinct(StringComparer.OrdinalIgnoreCase)
                .Select((path, priority) => new SourceDirectorySetting(path, SourceDirectoryType.Mixed, priority));
        foreach (var source in savedSources)
        {
            Sources.Add(new SourceDirectoryEntry { Path = source.Path, DirectoryType = source.DirectoryType, Priority = source.Priority });
        }

        _collectionCategory = Settings.DefaultCollectionCategory;
        _customerJpegMode = Settings.CustomerJpegMode ?? CustomerJpegHandlingMode.Strict;
        _customExtensionsText = string.Join(' ', Settings.CustomExtensions);
        OnPropertyChanged(nameof(CollectionCategory));
        OnPropertyChanged(nameof(IsCustomCategory));
        OnPropertyChanged(nameof(AllowCustomerJpegFallback));
        OnPropertyChanged(nameof(CustomerJpegMode));
        OnPropertyChanged(nameof(ShowCustomerJpegWarning));
        OnPropertyChanged(nameof(CustomExtensionsText));
        OutputBaseDirectory = Settings.RecentOutputDirectory;
        ProjectName = Settings.RecentProjectName;
        OutputMode = Settings.OutputMode;
        InitializeCurrentReportOptions();

        _mediaIndex = await _indexService.LoadCacheAsync() ?? new MediaIndexSnapshot();
        var configuredRoots = Sources.Select(x => NormalizePathForComparison(x.Path)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var cachedRoots = _mediaIndex.Files.Select(x => NormalizePathForComparison(x.SourceRoot)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        if (!configuredRoots.SequenceEqual(cachedRoots, StringComparer.OrdinalIgnoreCase)) _mediaIndex = new MediaIndexSnapshot();
        IndexedMediaCount = _mediaIndex.Files.Count;
        ProjectHistory.Clear();
        foreach (var project in await _projectHistoryService.LoadVisibleAsync()) ProjectHistory.Add(project);
        OutputPresets.Clear();
        foreach (var preset in await _outputPresetService.LoadAsync()) OutputPresets.Add(preset);
        if (WorkbenchSchedule is not null) await WorkbenchSchedule.InitializeAsync();
        if (TetherPage is not null) await TetherPage.InitializeAsync();
        if (ProjectHistory.FirstOrDefault() is { } recent) _currentProject = recent;
        RegenerateOutputFolderName();
        _initialized = true;
        if (IsOnboardingActive)
        {
            CurrentPage = "Workflow";
            await RestoreTutorialWorkspaceAsync();
        }
        else
        {
            CurrentPage = "ProjectCenter";
        }
        StatusMessage = IndexedMediaCount > 0
            ? $"已载入综合索引，共 {IndexedMediaCount:N0} 个文件"
            : "请添加照片来源目录并建立索引";
        NotifyTutorialChanged();
        OnLicenseChanged();
        if (_onboardingService.NeedsUpgradeOffer) UpgradeTutorialOfferRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void ReminderNotifications_OpenBookingRequested(object? sender, Guid bookingId)
    {
        CurrentPage = "WorkCalendar";
        await WorkCalendarPage.OpenBookingDetailsAsync(bookingId).ConfigureAwait(true);
    }

    private void WorkbenchSchedule_OpenCalendarRequested(object? sender, EventArgs e) => CurrentPage = "WorkCalendar";

    public async Task HandleDropAsync(string[]? paths, string? text)
    {
        if (IsBusy) return;
        if (IsOnboardingActive && !CanTutorial(TutorialAction.LoadCustomerSelection))
        {
            TutorialErrorMessage = "请先完成当前高亮步骤。";
            return;
        }
        if (IsOnboardingActive && paths is { Length: > 0 } && paths.Any(path => !_onboardingService.IsTutorialPath(path)))
        {
            TutorialErrorMessage = "教程模式只接受内置演示文件，不会读取你的真实照片。";
            return;
        }
        if (paths is { Length: > 0 })
        {
            await RunOperationAsync("读取客户选片", async token =>
            {
                var limited = await _inputParser.ParseDroppedItemsForProjectAsync(paths, Selections, _projectEntitlementService, IsOnboardingActive, CreateProgress(), token);
                AddInputs(limited.Accepted, applyLimit: false);
                if (limited.LimitReached) ShowUpgradePrompt(limited.Message);
                StatusMessage = limited.LimitReached
                    ? $"已加入 {limited.Accepted.Count:N0} 条记录，另有 {limited.Rejected.Count:N0} 条超过免费版上限"
                    : $"已加入 {limited.Accepted.Count:N0} 条客户选片记录";
            });
            if (IsOnboardingActive) await AdvanceTutorialAsync(TutorialAction.LoadCustomerSelection, CreateTutorialContext());
        }
        if (!string.IsNullOrWhiteSpace(text)) AddInputs(_inputParser.ParseText(text).Select(x => new ParsedSelectionInput(x)));
    }

    public void CaptureWindowState(double width, double height, double left, double top)
    {
        Settings.WindowWidth = width;
        Settings.WindowHeight = height;
        Settings.WindowLeft = left;
        Settings.WindowTop = top;
    }

    public async Task SaveSettingsAsync()
    {
        if (_weatherState is not null) Settings.Weather = _weatherState.Snapshot();
        if (IsOnboardingActive)
        {
            await _settingsService.SaveAsync(Settings);
            await _quickToolsRepository.SaveAsync(Settings.PinnedQuickTools);
            return;
        }

        Settings.RecentRawDirectories = Sources.Select(x => x.Path).ToList();
        Settings.SourceDirectories = Sources.Select((source, priority) =>
            new SourceDirectorySetting(source.Path, source.DirectoryType, priority)).ToList();
        Settings.RecentOutputDirectory = OutputBaseDirectory;
        Settings.RecentProjectName = ProjectName;
        Settings.OutputMode = OutputMode;
        Settings.DefaultCollectionCategory = CollectionCategory;
        Settings.CustomerJpegMode = CustomerJpegMode;
        Settings.AllowCustomerJpegFallback = CustomerJpegMode == CustomerJpegHandlingMode.AllowCustomerFile;
        var custom = MediaExtensionPolicy.ParseCustomExtensions(CustomExtensionsText);
        if (custom.IsValid) Settings.CustomExtensions = custom.Extensions.ToList();
        await _settingsService.SaveAsync(Settings);
        await _quickToolsRepository.SaveAsync(Settings.PinnedQuickTools);
    }

    private void AddSource()
    {
        if (IsOnboardingActive)
        {
            var path = _onboardingService.Sandbox.SourceRoot;
            if (!Sources.Any(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                Sources.Add(new SourceDirectoryEntry { Path = path, DirectoryType = SourceDirectoryType.Mixed, Priority = 0 });
            }
            SelectedSource = Sources.First(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
            InvalidateMediaIndex("已加入教程照片来源目录，请继续扫描。", false);
            _ = AdvanceTutorialAsync(TutorialAction.AddSourceDirectory, CreateTutorialContext());
            return;
        }
        var sourceAccess = _projectEntitlementService.CanAddSourceDirectory(Sources.Count);
        if (!sourceAccess.Allowed)
        {
            ShowUpgradePrompt(sourceAccess.Message);
            return;
        }
        var folder = _dialogService.ChooseFolder("选择照片来源目录", Sources.LastOrDefault()?.Path);
        if (folder is null || Sources.Any(x => string.Equals(x.Path, folder, StringComparison.OrdinalIgnoreCase))) return;
        Sources.Add(new SourceDirectoryEntry { Path = folder, DirectoryType = SourceDirectoryType.Mixed, Priority = Sources.Count });
        InvalidateMediaIndex("已添加照片来源目录，请扫描或重新建立索引。", clearMatches: false);
    }

    private void RemoveSource()
    {
        if (SelectedSource is null) return;
        if (IsOnboardingActive)
        {
            _dialogService.ShowInfo("这个按钮可以移除搜索目录，但不会删除硬盘中的照片。教程会保留演示目录供后续步骤使用。");
            _ = AdvanceTutorialAsync(TutorialAction.RemoveSourceDirectory, CreateTutorialContext());
            return;
        }
        Sources.Remove(SelectedSource);
        InvalidateMediaIndex("照片来源目录已变化，请重新扫描索引。", clearMatches: true);
    }

    private void ClearSources()
    {
        Sources.Clear();
        InvalidateMediaIndex("已清空照片来源目录和当前索引。", clearMatches: true);
    }

    private void MoveSource(int offset)
    {
        if (SelectedSource is null) return;
        var oldIndex = Sources.IndexOf(SelectedSource);
        var newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= Sources.Count) return;
        Sources.Move(oldIndex, newIndex);
        for (var index = 0; index < Sources.Count; index++) Sources[index].Priority = index;
        InvalidateMediaIndex("照片来源目录优先级已变化，请重新扫描索引。", clearMatches: true);
        RefreshCommands();
    }

    private void InvalidateMediaIndex(string message, bool clearMatches)
    {
        _mediaIndex = new MediaIndexSnapshot();
        IndexedMediaCount = 0;
        _matchCompleted = false;
        if (clearMatches) ResetMatchResults();
        StatusMessage = message;
        RefreshCommands();
    }

    private void BrowseOutput()
    {
        if (IsOnboardingActive)
        {
            OutputBaseDirectory = _onboardingService.Sandbox.Output;
            StatusMessage = "教程输出目录已设置；所有文件只会写入 Tutorial 沙盒。";
            _ = AdvanceTutorialAsync(TutorialAction.SelectOutputDirectory, CreateTutorialContext());
            return;
        }
        var folder = _dialogService.ChooseFolder("选择输出目录", OutputBaseDirectory);
        if (folder is not null) OutputBaseDirectory = folder;
    }

    private void PasteText()
    {
        if (IsOnboardingActive)
        {
            TextInput = File.ReadAllText(_onboardingService.Sandbox.SelectionText);
            StatusMessage = "教程编号已粘贴到输入框，请点击解析编号。";
            _ = AdvanceTutorialAsync(TutorialAction.PasteNumbers, CreateTutorialContext());
            return;
        }
        var text = _clipboardService.GetText();
        if (string.IsNullOrWhiteSpace(text))
        {
            _dialogService.ShowInfo("剪贴板中没有可解析的文本。");
            return;
        }
        TextInput = text;
        AddInputs(_inputParser.ParseText(text).Select(x => new ParsedSelectionInput(x)));
    }

    private void ParseText()
    {
        var result = _inputParser.ParseTextForProject(TextInput, Selections, _projectEntitlementService, IsOnboardingActive);
        AddInputs(result.Accepted, applyLimit: false);
        if (result.LimitReached) ShowUpgradePrompt(result.Message);
        if (IsOnboardingActive) _ = AdvanceTutorialAsync(TutorialAction.ParseNumbers, CreateTutorialContext());
    }

    private void AddInputs(IEnumerable<ParsedSelectionInput> inputs, bool applyLimit = true)
    {
        var materialized = inputs.Where(x => !string.IsNullOrWhiteSpace(x.OriginalInput)).ToList();
        if (applyLimit)
        {
            var limited = _projectEntitlementService.ApplySelectionLimit(Selections, materialized, IsOnboardingActive);
            materialized = limited.Accepted.ToList();
            if (limited.LimitReached) ShowUpgradePrompt(limited.Message);
        }
        var added = 0;
        foreach (var input in materialized)
        {
            var normalized = _normalizer.Normalize(input.OriginalInput);
            Selections.Add(new MediaSelectionItem
            {
                OriginalInput = input.OriginalInput,
                CustomerInputFilePath = input.CustomerInputFilePath,
                NormalizedName = normalized.ComparisonName,
                NumericId = normalized.NumericId
            });
            added++;
        }
        if (added == 0) return;
        _matchCompleted = false;
        MarkDuplicates();
        StatusMessage = IndexedMediaCount == 0
            ? $"已解析 {added:N0} 条记录；请先建立照片文件索引"
            : $"已解析 {added:N0} 条记录，等待匹配";
        UpdateStatistics();
        OnPropertyChanged(nameof(IsCurrentProjectReadOnly));
        OnPropertyChanged(nameof(CurrentProjectAccessText));
        RefreshCommands();
    }

    private async Task ScanAsync()
    {
        if (!CanModifyCurrentProject()) return;
        await RunOperationAsync("扫描照片文件", async token =>
        {
            var extensions = Settings.EnabledJpegExtensions
                .Concat(Settings.EnabledRawExtensions)
                .Concat(CollectionCategory == CollectionCategory.Custom ? CurrentCustomExtensions() : [])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            for (var index = 0; index < Sources.Count; index++) Sources[index].Priority = index;
            _mediaIndex = await _indexService.ScanAsync(Sources, extensions, CreateProgress(), token);
            IndexedMediaCount = _mediaIndex.Files.Count;
            var jpegCount = _mediaIndex.Files.Count(file => file.Category == FileCategory.Jpeg);
            var rawCount = _mediaIndex.Files.Count(file => file.Category == FileCategory.Raw);
            StatusMessage = $"综合索引已更新：JPG {jpegCount:N0}，RAW {rawCount:N0}，共 {IndexedMediaCount:N0} 个文件；跳过 {_mediaIndex.SkippedDirectoryCount:N0} 个不可访问目录";
            CurrentWorkflowStep = 2;
            await SaveSettingsAsync();
        });
        if (IsOnboardingActive && CanTutorial(TutorialAction.ScanSourceFiles))
        {
            await AdvanceTutorialAsync(TutorialAction.ScanSourceFiles, CreateTutorialContext());
            if (CanTutorial(TutorialAction.CancelSimulatedTask)) await StartTutorialCancellationDemoAsync();
        }
    }

    private async Task MatchAsync()
    {
        if (!CanModifyCurrentProject()) return;
        await RunOperationAsync("匹配选片文件", async token =>
        {
            var decisions = await _matchService.MatchAsync(Selections, _mediaIndex, CreateMatchOptions(), token);
            foreach (var decision in decisions)
            {
                Selections.First(x => x.Id == decision.ItemId).ApplyMatch(decision);
            }
            _matchCompleted = true;
            CurrentWorkflowStep = 3;
            SelectedSelection ??= Selections.FirstOrDefault();
            UpdateStatistics();
            StatusMessage = $"匹配完成：完整 {CompleteMatchedCount:N0}，部分 {PartialMatchedCount:N0}，冲突 {ConflictCount:N0}，未找到 {NotFoundCount:N0}";
            _currentProject.Status = PhotoProjectStatus.Ready;
            await SaveCurrentProjectAsync();
            await _matchDecisionRepository.SaveAsync(_currentProject.Id, Selections.ToList(), token);
        });
        if (IsOnboardingActive) await AdvanceTutorialAsync(TutorialAction.MatchFiles, CreateTutorialContext());
    }

    private async Task CopyAsync()
    {
        if (!CanModifyCurrentProject()) return;
        if (string.IsNullOrWhiteSpace(OutputBaseDirectory))
        {
            _dialogService.ShowError("请先选择输出目录。");
            return;
        }
        await RunOperationAsync("复制已匹配文件", async token =>
        {
            var summary = await _copyService.CopyAsync(Selections, OutputDirectory, OutputMode, CreateProgress(), token);
            foreach (var outcome in summary.Outcomes)
            {
                var item = Selections.First(x => x.Id == outcome.ItemId);
                var result = item.FormatResults.First(x => x.Key == outcome.FormatKey);
                result.Status = outcome.Status;
                result.OutputPath = outcome.DestinationPath;
                result.ErrorMessage = outcome.ErrorMessage;
                result.OperationTime = outcome.OperationTime;
                item.RefreshOverallStatus();
                item.Note = BuildItemNote(item);
            }
            var reportExported = IsOnboardingActive || ExportReportsForCurrentProject;
            if (reportExported)
            {
                await _reportService.ExportAsync(OutputDirectory, CollectionCategory, Selections, token, CreateReportExportOptions());
            }
            UpdateStatistics();
            StatusMessage = $"复制完成：成功 {summary.CopiedCount:N0}，失败 {summary.FailedCount:N0}{(reportExported ? "；报告已导出" : "；未自动导出报告")}";
            _currentProject.Status = PhotoProjectStatus.Completed;
            _currentProject.CompletedAt = DateTimeOffset.UtcNow;
            await SaveCurrentProjectAsync();
            await _matchDecisionRepository.SaveAsync(_currentProject.Id, Selections.ToList(), token);
            OnPropertyChanged(nameof(OutputDirectory));
            RefreshCommands();
        });
        if (IsOnboardingActive)
        {
            Settings.OnboardingTutorialOutputDirectory = OutputDirectory;
            _tutorialOutputDirectoryOverride = OutputDirectory;
            await SaveSettingsAsync();
            await AdvanceTutorialAsync(TutorialAction.CopyMatchedFiles, CreateTutorialContext());
        }
    }

    private async Task ExportReportAsync()
    {
        await RunOperationAsync("导出匹配报告", async token =>
        {
            await _reportService.ExportAsync(OutputDirectory, CollectionCategory, Selections, token, CreateReportExportOptions(manualExport: true));
            StatusMessage = CanExportAdvancedReports ? $"已导出：{string.Join("、", SelectedReportLabels())}" : "免费版基础 CSV 报告已导出";
            RefreshCommands();
        });
        if (IsOnboardingActive) await AdvanceTutorialAsync(TutorialAction.ExportReports, CreateTutorialContext());
    }

    private void ShowDetails(object? parameter)
    {
        if (parameter is not MediaSelectionItem item) return;
        SelectedSelection = item;
        var showAdvancedDetails = IsOnboardingActive ||
            _featureGateService.HasAccess(LicensedFeature.AdvancedJpegQualityAssessment) ||
            _featureGateService.HasAccess(LicensedFeature.AdvancedConflictResolution);
        if (_dialogService.ShowMediaDetails(item, showAdvancedDetails))
        {
            item.RefreshOverallStatus();
            item.Note = BuildItemNote(item);
            _matchCompleted = true;
            UpdateStatistics();
            RefreshCommands();
        }
        if (IsOnboardingActive)
        {
            _tutorialDetailsViewed = true;
            _ = AdvanceTutorialAsync(TutorialAction.ViewDetails, CreateTutorialContext());
        }
    }

    private void OpenOutput()
    {
        if (!Directory.Exists(OutputDirectory)) return;
        try
        {
            Process.Start(new ProcessStartInfo(OutputDirectory) { UseShellExecute = true });
            _tutorialOutputOpened = true;
            if (IsOnboardingActive) _ = AdvanceTutorialAsync(TutorialAction.OpenOutputDirectory, CreateTutorialContext());
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _dialogService.ShowError("无法打开教程输出文件夹，请检查系统策略后重试。");
        }
    }

    private void ClearSelections()
    {
        Selections.Clear();
        _matchCompleted = false;
        StatusMessage = "已清空客户选片记录";
        if (IsOnboardingActive)
        {
            RestoreTutorialSelections();
            _ = AdvanceTutorialAsync(TutorialAction.ClearSelections, CreateTutorialContext());
        }
    }

    private void ClearTask()
    {
        var outputPreserved = !IsOnboardingActive || Directory.Exists(OutputDirectory);
        Selections.Clear();
        TextInput = string.Empty;
        _matchCompleted = false;
        ProgressPercent = 0;
        CurrentItem = string.Empty;
        ProcessedCount = 0;
        InitializeCurrentReportOptions();
        RegenerateOutputFolderName();
        StatusMessage = "已清空当前任务";
        if (IsOnboardingActive) _ = AdvanceTutorialAsync(TutorialAction.ClearCurrentTask, CreateTutorialContext() with { OutputPreserved = outputPreserved });
    }

    public async Task RespondToUpgradeOfferAsync(bool startTutorial)
    {
        if (startTutorial) CaptureNormalWorkspace();
        await _onboardingService.AcceptUpgradeOfferAsync(startTutorial);
        if (startTutorial) await PrepareTutorialWorkspaceAsync(resetProgress: true);
        NotifyTutorialChanged();
    }

    private bool CanTutorial(TutorialAction action) => _onboardingService.CanPerform(action);

    private async Task TutorialPrimaryAsync()
    {
        switch (_onboardingService.CurrentStep.RequiredAction)
        {
            case TutorialAction.BeginTutorial:
                await PrepareTutorialWorkspaceAsync(resetProgress: false);
                await AdvanceTutorialAsync(TutorialAction.BeginTutorial, CreateTutorialContext());
                break;
            case TutorialAction.LoadCustomerSelection:
                await HandleDropAsync([_onboardingService.Sandbox.CustomerJpeg], null);
                break;
            case TutorialAction.ViewDetails:
                if (Selections.FirstOrDefault() is { } firstItem)
                {
                    ShowDetails(firstItem);
                }
                else
                {
                    TutorialErrorMessage = "没有可查看的匹配记录，请返回上一步重新匹配。";
                }
                break;
            case TutorialAction.AcknowledgeJpegQuality:
                await AdvanceTutorialAsync(TutorialAction.AcknowledgeJpegQuality, CreateTutorialContext());
                break;
            case TutorialAction.AcknowledgeEditions:
                await AdvanceTutorialAsync(TutorialAction.AcknowledgeEditions, CreateTutorialContext());
                break;
            case TutorialAction.FinishTutorial:
                var wasReplay = !IsOnboardingRequired;
                await AdvanceTutorialAsync(TutorialAction.FinishTutorial, CreateTutorialContext());
                if (wasReplay)
                {
                    RestoreNormalWorkspace();
                }
                else
                {
                    ClearTutorialWorkspace();
                    StatusMessage = $"欢迎使用{Branding.ProductName}";
                }
                break;
        }
    }

    private async Task TutorialBackAsync()
    {
        await _onboardingService.BackAsync();
        await RestoreTutorialWorkspaceAsync();
        NotifyTutorialChanged();
    }

    private void ExitTutorial()
    {
        if (IsOnboardingRequired)
        {
            _ = SaveSettingsAsync();
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return;
        }
        _onboardingService.ExitReplay();
        RestoreNormalWorkspace();
        NotifyTutorialChanged();
    }

    private async Task RecreateTutorialDataAsync()
    {
        try
        {
            await _onboardingService.ResetTutorialDataAsync();
            await PrepareTutorialWorkspaceAsync(resetProgress: false);
            TutorialErrorMessage = string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            TutorialErrorMessage = "教程示例文件创建失败，请检查磁盘空间后重试。";
        }
    }

    private async Task ShowHelpAsync()
    {
        switch (_dialogService.ShowHelp())
        {
            case HelpAction.ReplayTutorial:
                CaptureNormalWorkspace();
                await _onboardingService.StartReplayAsync();
                await PrepareTutorialWorkspaceAsync(resetProgress: true);
                break;
            case HelpAction.ResetTutorialData:
                if (_dialogService.Confirm("只会重新创建像素蛋挞的教程示例文件，不会删除你的照片。", "重置教程演示数据"))
                {
                    await _onboardingService.ResetTutorialDataAsync();
                    _dialogService.ShowInfo("教程演示数据已重新创建。 ");
                }
                break;
            case HelpAction.DeleteTutorialData:
                if (_dialogService.Confirm("只会删除像素蛋挞创建的教程示例文件，不会删除你的照片。", "删除教程演示数据"))
                {
                    _onboardingService.DeleteTutorialData(_onboardingService.Sandbox.Root);
                    _dialogService.ShowInfo("教程演示数据已删除。以后重新查看教程时会自动创建。 ");
                }
                break;
        }
        NotifyTutorialChanged();
    }

    private async Task PrepareTutorialWorkspaceAsync(bool resetProgress)
    {
        await _onboardingService.EnsureTutorialDataAsync();
        Sources.Clear();
        Selections.Clear();
        TextInput = string.Empty;
        _mediaIndex = new MediaIndexSnapshot();
        IndexedMediaCount = 0;
        CollectionCategory = CollectionCategory.JpegAndRaw;
        OutputMode = OutputMode.ByFileCategory;
        CustomerJpegMode = CustomerJpegHandlingMode.Strict;
        OutputBaseDirectory = _onboardingService.Sandbox.Output;
        ProjectName = string.Empty;
        _tutorialOutputDirectoryOverride = string.Empty;
        _tutorialDetailsViewed = false;
        _tutorialOutputOpened = false;
        if (resetProgress) Settings.OnboardingTutorialOutputDirectory = string.Empty;
        StatusMessage = "教程演示环境已准备好，不会访问你的真实照片。";
        NotifyTutorialChanged();
    }

    private void CaptureNormalWorkspace()
    {
        if (_normalWorkspaceSnapshot is not null) return;
        _normalWorkspaceSnapshot = new NormalWorkspaceSnapshot(
            Sources.ToList(), Selections.ToList(), SelectedSource, _mediaIndex, TextInput,
            ProjectName, OutputBaseDirectory, OutputMode, CollectionCategory, CustomerJpegMode,
            CustomExtensionsText, _matchCompleted, StatusMessage);
    }

    private void RestoreNormalWorkspace()
    {
        var snapshot = _normalWorkspaceSnapshot;
        _normalWorkspaceSnapshot = null;
        if (snapshot is null)
        {
            ClearTutorialWorkspace();
            return;
        }

        Sources.Clear();
        foreach (var source in snapshot.Sources) Sources.Add(source);
        SelectedSource = snapshot.SelectedSource;
        Selections.Clear();
        foreach (var selection in snapshot.Selections) Selections.Add(selection);
        _mediaIndex = snapshot.MediaIndex;
        IndexedMediaCount = _mediaIndex.Files.Count;
        TextInput = snapshot.TextInput;
        OutputBaseDirectory = snapshot.OutputBaseDirectory;
        ProjectName = snapshot.ProjectName;
        OutputMode = snapshot.OutputMode;
        CollectionCategory = snapshot.CollectionCategory;
        CustomerJpegMode = snapshot.CustomerJpegMode;
        CustomExtensionsText = snapshot.CustomExtensionsText;
        _matchCompleted = snapshot.MatchCompleted;
        _tutorialOutputDirectoryOverride = string.Empty;
        StatusMessage = snapshot.StatusMessage;
        UpdateStatistics();
        RefreshCommands();
    }

    private void ClearTutorialWorkspace()
    {
        Sources.Clear();
        Selections.Clear();
        SelectedSource = null;
        TextInput = string.Empty;
        _mediaIndex = new MediaIndexSnapshot();
        IndexedMediaCount = 0;
        OutputBaseDirectory = string.Empty;
        ProjectName = string.Empty;
        _tutorialOutputDirectoryOverride = string.Empty;
        _matchCompleted = false;
        UpdateStatistics();
        RefreshCommands();
    }

    private async Task RestoreTutorialWorkspaceAsync()
    {
        if (!IsOnboardingActive) return;
        await _onboardingService.EnsureTutorialDataAsync();
        var step = TutorialStepNumber;
        if (step >= 3)
        {
            Sources.Clear();
            Sources.Add(new SourceDirectoryEntry { Path = _onboardingService.Sandbox.SourceRoot, DirectoryType = SourceDirectoryType.Mixed, Priority = 0 });
            SelectedSource = Sources[0];
        }
        if (step >= 6)
        {
            var extensions = Settings.EnabledJpegExtensions.Concat(Settings.EnabledRawExtensions).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            _mediaIndex = await _indexService.ScanAsync(Sources, extensions, null, CancellationToken.None);
            IndexedMediaCount = _mediaIndex.Files.Count;
        }
        if (step >= 8) RestoreTutorialSelections(includeAll: step >= 10);
        if (step >= 9)
        {
            TextInput = await File.ReadAllTextAsync(_onboardingService.Sandbox.SelectionText);
        }
        if (step >= 12)
        {
            CollectionCategory = CollectionCategory.JpegAndRaw;
            var decisions = await _matchService.MatchAsync(Selections, _mediaIndex, CreateMatchOptions(), CancellationToken.None);
            foreach (var decision in decisions) Selections.First(x => x.Id == decision.ItemId).ApplyMatch(decision);
            _matchCompleted = true;
            SelectedSelection ??= Selections.FirstOrDefault();
            UpdateStatistics();
        }
        if (step >= 15)
        {
            OutputBaseDirectory = _onboardingService.Sandbox.Output;
        }
        if (step >= 16)
        {
            ProjectName = Branding.TutorialProjectName;
        }
        if (step >= 18 && !string.IsNullOrWhiteSpace(Settings.OnboardingTutorialOutputDirectory) && Directory.Exists(Settings.OnboardingTutorialOutputDirectory))
        {
            _tutorialOutputDirectoryOverride = Settings.OnboardingTutorialOutputDirectory;
            OutputBaseDirectory = _onboardingService.Sandbox.Output;
            OnPropertyChanged(nameof(OutputDirectory));
        }
        if (step == 6 && !_tutorialCancellationDemoActive)
        {
            _ = StartTutorialCancellationDemoAsync();
        }
        if (step == 22)
        {
            Selections.Clear();
            TextInput = string.Empty;
            _matchCompleted = false;
            UpdateStatistics();
        }
        NotifyTutorialChanged();
    }

    private void RestoreTutorialSelections(bool includeAll = true)
    {
        Selections.Clear();
        AddInputs([new ParsedSelectionInput("DSC01234.JPG", _onboardingService.Sandbox.CustomerJpeg)]);
        if (includeAll)
        {
            AddInputs(_inputParser.ParseText("1235、DSC01236.JPG").Select(value => new ParsedSelectionInput(value)));
        }
        MarkDuplicates();
    }

    private async Task StartTutorialCancellationDemoAsync()
    {
        _tutorialCancellationDemoActive = true;
        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        StatusMessage = "教程模拟扫描正在运行，请点击“取消当前任务”";
        RefreshCommands();
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(10), _operationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsBusy = false;
            _tutorialCancellationDemoActive = false;
            _operationCancellation.Dispose();
            _operationCancellation = null;
            RefreshCommands();
        }
    }

    private void CancelCurrentOperation()
    {
        _operationCancellation?.Cancel();
        if (_tutorialCancellationDemoActive)
        {
            StatusMessage = "模拟扫描已安全取消，已完成的教程索引保持不变。";
            _ = AdvanceTutorialAsync(TutorialAction.CancelSimulatedTask, CreateTutorialContext());
        }
    }

    private TutorialActionContext CreateTutorialContext()
    {
        var jpegIndexed = _mediaIndex.Files.Count(file => file.Category == FileCategory.Jpeg);
        var rawIndexed = _mediaIndex.Files.Count(file => file.Category == FileCategory.Raw);
        var copiedJpeg = Selections.SelectMany(item => item.FormatResults).Count(result => result.Category == FileCategory.Jpeg && result.Status == MatchStatus.Copied);
        var copiedRaw = Selections.SelectMany(item => item.FormatResults).Count(result => result.Category == FileCategory.Raw && result.Status == MatchStatus.Copied);
        var reportRoot = Directory.Exists(OutputDirectory) ? OutputDirectory : Settings.OnboardingTutorialOutputDirectory;
        var reportsExist = !string.IsNullOrWhiteSpace(reportRoot) &&
                           File.Exists(Path.Combine(reportRoot, "匹配报告.csv")) &&
                           File.Exists(Path.Combine(reportRoot, "匹配报告.json")) &&
                           File.Exists(Path.Combine(reportRoot, "操作日志.txt"));
        return new TutorialActionContext(
            Sources.Count, jpegIndexed, rawIndexed, Selections.Count, CompleteMatchedCount,
            copiedJpeg, copiedRaw, _tutorialDetailsViewed, reportsExist, _tutorialOutputOpened,
            false, ProjectName, OutputBaseDirectory, CollectionCategory, OutputMode);
    }

    private async Task AdvanceTutorialAsync(TutorialAction action, TutorialActionContext context)
    {
        var result = await _onboardingService.PerformAsync(action, context);
        if (!result.Succeeded) TutorialErrorMessage = result.Message;
        NotifyTutorialChanged();
    }

    private void NotifyTutorialChanged()
    {
        EnsureTutorialPage();
        OnPropertyChanged(nameof(IsOnboardingActive));
        OnPropertyChanged(nameof(IsOnboardingRequired));
        OnPropertyChanged(nameof(TutorialTarget));
        OnPropertyChanged(nameof(TutorialStepNumber));
        OnPropertyChanged(nameof(TutorialStepCount));
        OnPropertyChanged(nameof(TutorialStepProgress));
        OnPropertyChanged(nameof(TutorialTitle));
        OnPropertyChanged(nameof(TutorialInstruction));
        OnPropertyChanged(nameof(TutorialCanGoBack));
        OnPropertyChanged(nameof(TutorialErrorMessage));
        OnPropertyChanged(nameof(HasTutorialError));
        OnPropertyChanged(nameof(ShowTutorialPrimaryAction));
        OnPropertyChanged(nameof(TutorialPrimaryActionLabel));
        OnPropertyChanged(nameof(ShowCustomerJpegWarning));
        OnPropertyChanged(nameof(NeedsUpgradeTutorialOffer));
        RefreshCommands();
        TutorialVisualStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureTutorialPage()
    {
        if (!IsOnboardingActive) return;
        CurrentPage = "Workflow";
        CurrentWorkflowStep = TutorialTarget switch
        {
            TutorialTarget.AddSourceButton or TutorialTarget.RemoveSourceButton or TutorialTarget.CollectionCategorySelector or TutorialTarget.ScanButton or TutorialTarget.CancelButton => 1,
            TutorialTarget.CustomerDropArea or TutorialTarget.PasteButton or TutorialTarget.ParseButton or TutorialTarget.ClearSelectionsButton => 2,
            TutorialTarget.MatchButton or TutorialTarget.ResultsGrid or TutorialTarget.FirstDetailsButton or TutorialTarget.JpegQualityArea => 3,
            TutorialTarget.BrowseOutputButton or TutorialTarget.ProjectNameInput or TutorialTarget.OutputModeSelector or TutorialTarget.CopyButton or TutorialTarget.ExportButton or TutorialTarget.OpenOutputButton or TutorialTarget.ClearTaskButton => 4,
            _ => CurrentWorkflowStep
        };
    }

    public void UpdateSidebarForWidth(double width)
    {
        if (Settings.Appearance.Sidebar != SidebarMode.AutoCollapse) return;
        var collapsed = width < 1100;
        if (Settings.Appearance.SidebarCollapsed == collapsed) return;
        Settings.Appearance.SidebarCollapsed = collapsed;
        _appearanceService.Apply(Settings.Appearance);
        OnPropertyChanged(nameof(IsSidebarCollapsed));
        OnPropertyChanged(nameof(IsSidebarExpanded));
        OnPropertyChanged(nameof(SidebarWidth));
    }

    private void ToggleSidebar()
    {
        if (Settings.Appearance.Sidebar == SidebarMode.AlwaysExpanded)
        {
            Settings.Appearance.Sidebar = SidebarMode.Remember;
            OnPropertyChanged(nameof(SelectedSidebarMode));
        }
        Settings.Appearance.SidebarCollapsed = !Settings.Appearance.SidebarCollapsed;
        ApplyAppearance(Settings.Appearance.SidebarCollapsed ? "侧栏已收起" : "侧栏已展开");
        OnPropertyChanged(nameof(IsSidebarCollapsed));
        OnPropertyChanged(nameof(IsSidebarExpanded));
        OnPropertyChanged(nameof(SidebarWidth));
    }

    private void SetTheme(object? parameter)
    {
        if (parameter is ThemeMode mode) SelectedTheme = mode;
        else if (Enum.TryParse<ThemeMode>(parameter?.ToString(), true, out var parsed)) SelectedTheme = parsed;
    }

    private void ResetAppearance()
    {
        Settings.Appearance = new AppearanceSettings();
        _appearanceService.Apply(Settings.Appearance);
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(SelectedAccent));
        OnPropertyChanged(nameof(CustomAccentColor));
        OnPropertyChanged(nameof(IsCustomAccent));
        OnPropertyChanged(nameof(AccentPreviewHex));
        OnPropertyChanged(nameof(SelectedDensity));
        OnPropertyChanged(nameof(SelectedSidebarMode));
        OnPropertyChanged(nameof(SelectedMotion));
        OnPropertyChanged(nameof(SelectedFontScale));
        OnPropertyChanged(nameof(IsSidebarCollapsed));
        OnPropertyChanged(nameof(IsSidebarExpanded));
        OnPropertyChanged(nameof(SidebarWidth));
        OnPropertyChanged(nameof(ThemeSummary));
        _ = SaveSettingsAsync();
        ShowToast("外观设置已恢复默认");
    }

    private void ApplyCustomAccent()
    {
        if (!AccentColorService.TryParse(CustomAccentColor, out _))
        {
            ShowToast("请输入 #RRGGBB 格式的颜色，例如 #C98220");
            return;
        }
        Settings.Appearance.Accent = AccentPreset.Custom;
        ApplyAppearance("自定义强调色已应用");
        OnPropertyChanged(nameof(SelectedAccent));
        OnPropertyChanged(nameof(IsCustomAccent));
        OnPropertyChanged(nameof(AccentPreviewHex));
    }

    private void ApplyAppearance(string message)
    {
        _appearanceService.Apply(Settings.Appearance);
        _ = SaveSettingsAsync();
        ShowToast(message);
    }

    private bool FilterSelection(object item)
    {
        if (item is not MediaSelectionItem selection) return false;
        if (OnlyShowAttentionItems && selection.OverallStatus is MediaOverallStatus.CompleteMatched or MediaOverallStatus.FullyCopied) return false;
        if (string.IsNullOrWhiteSpace(SearchQuery)) return true;
        var query = SearchQuery.Trim();
        return selection.OriginalInput.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               selection.NormalizedName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               selection.NumericId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               selection.Note.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void OpenLogDirectory()
    {
        AppDataPaths.EnsureCreated();
        Process.Start(new ProcessStartInfo("explorer.exe", AppDataPaths.LogDirectory) { UseShellExecute = true });
    }

    private async void ShowToast(string message)
    {
        _toastCancellation?.Cancel();
        _toastCancellation?.Dispose();
        _toastCancellation = new CancellationTokenSource();
        ToastMessage = message;
        IsToastVisible = true;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), _toastCancellation.Token);
            IsToastVisible = false;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void DismissToast()
    {
        _toastCancellation?.Cancel();
        IsToastVisible = false;
    }

    private void InitializeCurrentReportOptions()
    {
        ExportReportsForCurrentProject = IsOnboardingActive || Settings.ReportSettings.DefaultExportEnabled;
        ExportCsvForCurrentProject = Settings.ReportSettings.DefaultExportCsv;
        ExportJsonForCurrentProject = Settings.ReportSettings.DefaultExportJson;
        ExportLogForCurrentProject = Settings.ReportSettings.DefaultExportLog;
    }

    private ReportExportOptions CreateReportExportOptions(bool manualExport = false)
    {
        if (!CanExportAdvancedReports) return ReportExportOptions.Free;
        var includeCsv = manualExport && !ExportReportsForCurrentProject ? true : ExportCsvForCurrentProject;
        if (!includeCsv && !ExportJsonForCurrentProject && !ExportLogForCurrentProject) includeCsv = true;
        return new ReportExportOptions(includeCsv, ExportJsonForCurrentProject, ExportLogForCurrentProject);
    }

    private IEnumerable<string> SelectedReportLabels()
    {
        if (!CanExportAdvancedReports) return ["CSV"];
        var labels = new List<string>();
        if (ExportCsvForCurrentProject) labels.Add("CSV");
        if (ExportJsonForCurrentProject) labels.Add("JSON");
        if (ExportLogForCurrentProject) labels.Add("操作日志 TXT");
        return labels.Count == 0 ? ["CSV"] : labels;
    }

    private void Navigate(object? parameter)
    {
        var page = parameter?.ToString();
        if (page == "Settings")
        {
            OpenSettingsCommand.Execute(null);
            return;
        }
        if (page is not ("Workbench" or "ProjectCenter" or "LocalSplit" or "Workflow" or "History" or "WorkCalendar" or "Tether" or "Activation" or "Settings" or "Help" or
            "BatchCompress" or "Watermark" or "DeleteRejects" or "FtpTool" or "PhotoOrganize" or "PhotoGrouping" or "Collage" or "BatchRename" or "BatchConvert" or "Toolbox")) return;
        var targetPage = page switch
        {
            "ProjectCenter" => "Workbench",
            "PhotoOrganize" => "PhotoGrouping",
            _ => page
        };
        if (string.Equals(CurrentPage, targetPage, StringComparison.Ordinal)) return;
        var navigationCorrelationId = Guid.NewGuid().ToString("N");
        _logService.Info($"导航请求[{navigationCorrelationId}]：{CurrentPage} -> {targetPage}");
        CurrentPage = targetPage;
    }

    private void TogglePinnedTool(string? id)
    {
        if (!ToolRegistry.TryGet(id, out var definition)) return;
        if (!definition.CanPin)
        {
            ShowToast("工具箱始终可从工作台和侧栏打开，不占快捷位");
            return;
        }
        if (IsToolPinned(definition.SettingsId))
        {
            Settings.PinnedQuickTools.RemoveAll(value => string.Equals(value, definition.SettingsId, StringComparison.OrdinalIgnoreCase));
        }
        else if (QuickToolsService.Normalize(Settings.PinnedQuickTools).Count >= QuickToolsService.MaximumPinnedTools)
        {
            ShowToast("快捷工具已满，请先取消固定一个工具");
            return;
        }
        else
        {
            Settings.PinnedQuickTools.Add(definition.SettingsId);
        }
        Settings.PinnedQuickTools = QuickToolsService.Normalize(Settings.PinnedQuickTools);
        Settings.QuickToolLayout.OrderedToolIds = Settings.PinnedQuickTools.ToList();
        RefreshToolPinState();
        OnPropertyChanged(nameof(PinnedToolboxItems));
        OnPropertyChanged(nameof(DisplayedPinnedToolboxItems));
        OnPropertyChanged(nameof(OverflowPinnedToolboxItems));
        _ = SaveSettingsAsync();
    }

    public void MovePinnedTool(string? id, int offset)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        var moved = QuickToolsService.Move(Settings.PinnedQuickTools, id, offset);
        if (moved.SequenceEqual(Settings.PinnedQuickTools, StringComparer.OrdinalIgnoreCase)) return;
        ApplyQuickToolLayout(moved, "快捷工具顺序已保存");
    }

    public void SetQuickToolsCompact(bool compact)
    {
        if (_quickToolsCompact == compact) return;
        _quickToolsCompact = compact;
        OnPropertyChanged(nameof(DisplayedPinnedToolboxItems));
        OnPropertyChanged(nameof(OverflowPinnedToolboxItems));
    }

    public void MovePinnedToolTo(string? sourceId, string? targetId)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId) || string.Equals(sourceId, targetId, StringComparison.OrdinalIgnoreCase)) return;
        var values = QuickToolsService.Normalize(Settings.PinnedQuickTools);
        var sourceIndex = values.FindIndex(x => string.Equals(x, sourceId, StringComparison.OrdinalIgnoreCase));
        var targetIndex = values.FindIndex(x => string.Equals(x, targetId, StringComparison.OrdinalIgnoreCase));
        if (sourceIndex < 0 || targetIndex < 0) return;
        var value = values[sourceIndex];
        values.RemoveAt(sourceIndex);
        values.Insert(targetIndex, value);
        ApplyQuickToolLayout(values, "快捷工具拖拽顺序已保存");
    }

    private void RemovePinnedTool(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        ApplyQuickToolLayout(QuickToolsService.Remove(Settings.PinnedQuickTools, id), "已从快捷工具移除");
    }

    private void ResetQuickTools() => ApplyQuickToolLayout(QuickToolsService.DefaultPinnedTools, "已恢复默认快捷布局");

    private void ManageQuickTools()
    {
        var result = _dialogService.ManageQuickTools(Settings.PinnedQuickTools);
        if (result is null) return;
        ApplyQuickToolLayout(result, "快捷工具布局已保存");
    }

    private void ApplyQuickToolLayout(IEnumerable<string> toolIds, string message)
    {
        Settings.PinnedQuickTools = QuickToolsService.Normalize(toolIds);
        Settings.QuickToolLayout.SchemaVersion = QuickToolLayout.CurrentSchemaVersion;
        Settings.QuickToolLayout.OrderedToolIds = Settings.PinnedQuickTools.ToList();
        RefreshToolPinState();
        OnPropertyChanged(nameof(PinnedToolboxItems));
        OnPropertyChanged(nameof(DisplayedPinnedToolboxItems));
        OnPropertyChanged(nameof(OverflowPinnedToolboxItems));
        ShowToast(message);
        _ = SaveSettingsAsync();
    }

    private void RefreshToolPinState()
    {
        var normalized = QuickToolsService.Normalize(Settings.PinnedQuickTools);
        Settings.PinnedQuickTools = normalized;
        Settings.QuickToolLayout.OrderedToolIds = normalized.ToList();
        foreach (var item in ToolboxItems)
        {
            item.SetPinned(normalized.Contains(item.Id, StringComparer.OrdinalIgnoreCase));
        }
    }

    private void GoToWorkflowStep(object? parameter)
    {
        if (!int.TryParse(parameter?.ToString(), out var step)) return;
        CurrentPage = "Workflow";
        CurrentWorkflowStep = step;
    }

    private void StartNewProject()
    {
        Selections.Clear();
        Sources.Clear();
        SelectedSource = null;
        TextInput = string.Empty;
        ProjectName = string.Empty;
        OutputBaseDirectory = Settings.RecentOutputDirectory;
        CollectionCategory = CollectionCategory.JpegAndRaw;
        CustomerJpegMode = CustomerJpegHandlingMode.Strict;
        _mediaIndex = new MediaIndexSnapshot();
        IndexedMediaCount = 0;
        _matchCompleted = false;
        _currentProject = new PhotoProjectRecord();
        InitializeCurrentReportOptions();
        CurrentPage = "Workflow";
        CurrentWorkflowStep = 1;
        StatusMessage = "新项目已创建，请先添加照片来源目录。";
        UpdateStatistics();
        OnPropertyChanged(nameof(CurrentProjectStatus));
        OnPropertyChanged(nameof(IsCurrentProjectReadOnly));
        OnPropertyChanged(nameof(CurrentProjectAccessText));
    }

    private async Task ContinueProjectAsync(object? parameter)
    {
        var project = parameter as PhotoProjectRecord ?? ProjectHistory.FirstOrDefault();
        if (project is null) return;
        _currentProject = project;
        Sources.Clear();
        foreach (var path in project.SourceDirectories)
        {
            Sources.Add(new SourceDirectoryEntry { Path = path, DirectoryType = SourceDirectoryType.Mixed, Priority = Sources.Count });
        }
        Selections.Clear();
        AddInputs(project.SelectionInputs.Select(x => new ParsedSelectionInput(x)), applyLimit: false);
        ProjectName = project.Name;
        OutputBaseDirectory = project.OutputBaseDirectory;
        CollectionCategory = project.Category;
        OutputMode = project.OutputMode;
        CustomExtensionsText = string.Join(' ', project.CustomExtensions);
        ExportReportsForCurrentProject = project.ExportReports;
        ExportCsvForCurrentProject = project.ExportCsvReport;
        ExportJsonForCurrentProject = project.ExportJsonReport;
        ExportLogForCurrentProject = project.ExportLogReport;
        CurrentPage = "Workflow";
        CurrentWorkflowStep = project.Status == PhotoProjectStatus.Completed ? 4 : 1;
        StatusMessage = IsCurrentProjectReadOnly ? CurrentProjectAccessText : "项目已载入，可继续工作。";
        OnPropertyChanged(nameof(CurrentProjectStatus));
        await Task.CompletedTask;
    }

    private async Task SaveCurrentProjectAsync()
    {
        _currentProject.Name = string.IsNullOrWhiteSpace(ProjectName) ? "未命名项目" : ProjectName.Trim();
        _currentProject.Category = CollectionCategory;
        _currentProject.OutputMode = OutputMode;
        _currentProject.OutputBaseDirectory = OutputBaseDirectory;
        _currentProject.OutputDirectory = OutputDirectory;
        _currentProject.SourceDirectories = Sources.Select(x => x.Path).ToList();
        _currentProject.SelectionInputs = Selections.Select(x => x.OriginalInput).ToList();
        _currentProject.CustomExtensions = CurrentCustomExtensions().ToList();
        _currentProject.SelectionCount = CountUniqueSelections();
        _currentProject.MatchedFileCount = Selections.Sum(x => x.MatchedFileCount);
        _currentProject.CopiedFileCount = Selections.Sum(x => x.CopiedFileCount);
        _currentProject.Summary = StatusMessage;
        _currentProject.ExportReports = ExportReportsForCurrentProject;
        _currentProject.ExportCsvReport = ExportCsvForCurrentProject;
        _currentProject.ExportJsonReport = ExportJsonForCurrentProject;
        _currentProject.ExportLogReport = ExportLogForCurrentProject;
        await _projectHistoryService.UpsertAsync(_currentProject);
        await ReloadProjectHistoryAsync();
        OnPropertyChanged(nameof(CurrentProjectStatus));
    }

    private async Task ReloadProjectHistoryAsync()
    {
        ProjectHistory.Clear();
        foreach (var project in await _projectHistoryService.LoadVisibleAsync()) ProjectHistory.Add(project);
        ContinueProjectCommand.RaiseCanExecuteChanged();
        RunBatchCommand.RaiseCanExecuteChanged();
    }

    private async Task ActivateLicenseAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _licenseService.ActivateAsync(LicenseKeyInput);
            if (result.Succeeded)
            {
                LicenseKeyInput = "KQGP-";
                _dialogService.ShowInfo("专业版已在本机激活，当前项目无需重新导入即可继续。 ");
            }
            else
            {
                _dialogService.ShowError(result.Message);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeactivateLicenseAsync()
    {
        if (!_dialogService.Confirm("停用后本机将退回免费版，但项目、设置和照片不会被删除。若当前离线，停用失败时会保留现有授权。", "停用本机授权")) return;
        IsBusy = true;
        try
        {
            var result = await _licenseService.DeactivateAsync();
            if (result.Succeeded) _dialogService.ShowInfo(result.Message);
            else _dialogService.ShowError(result.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ValidateLicenseAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _licenseService.ValidateAsync(true);
            if (result.Succeeded) _dialogService.ShowInfo(result.Message);
            else _dialogService.ShowError(result.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenPurchasePage()
    {
        if (!Uri.TryCreate(_licenseService.Configuration.PurchaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http"))
        {
            _dialogService.ShowError("购买页面地址尚未配置。 ");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _dialogService.ShowError("无法打开购买页面，请检查系统默认浏览器。 ");
        }
    }

    private async Task SaveCurrentOutputPresetAsync()
    {
        var preset = new OutputPreset
        {
            Name = string.IsNullOrWhiteSpace(ProjectName) ? "常用输出" : $"{ProjectName} 输出",
            OutputMode = OutputMode,
            FolderNameTemplate = "{Project}_{Category}_{Date}_{Time}"
        };
        var access = await _outputPresetService.SaveAsync(preset);
        if (!access.Allowed)
        {
            ShowUpgradePrompt(access.Message);
            return;
        }
        OutputPresets.Add(preset);
        _dialogService.ShowInfo("输出预设已保存。 ");
    }

    private async Task RunBatchAsync()
    {
        var summary = await _batchProjectService.RunSequentialAsync(ProjectHistory,
            (project, _) => Task.FromResult(new BatchProjectOutcome(project.Id, project.Name, true, "已加入批处理队列")));
        if (!summary.Started) ShowUpgradePrompt(summary.Message);
        else _dialogService.ShowInfo(summary.Message);
    }

    private void OnLicenseChanged()
    {
        OnPropertyChanged(nameof(IsFreeEdition));
        OnPropertyChanged(nameof(IsProEdition));
        OnPropertyChanged(nameof(EditionLabel));
        OnPropertyChanged(nameof(EditionActionText));
        OnPropertyChanged(nameof(LicenseStatusMessage));
        OnPropertyChanged(nameof(LicenseDeviceText));
        OnPropertyChanged(nameof(LicenseKeySuffixText));
        OnPropertyChanged(nameof(OfflineLicenseText));
        OnPropertyChanged(nameof(LicenseActivatedAtText));
        OnPropertyChanged(nameof(LicenseLastValidatedText));
        OnPropertyChanged(nameof(LicenseOfflineRemainingText));
        OnPropertyChanged(nameof(IsProductionLicenseConfigured));
        OnPropertyChanged(nameof(ProductionLicenseConfigurationText));
        OnPropertyChanged(nameof(CanExportAdvancedReports));
        OnPropertyChanged(nameof(ReportSelectionSummary));
        OnPropertyChanged(nameof(IsCurrentProjectReadOnly));
        OnPropertyChanged(nameof(CurrentProjectAccessText));
        if (_initialized) _ = ReloadProjectHistoryAsync();
        RefreshCommands();
    }

    private bool CanModifyCurrentProject()
    {
        if (!IsCurrentProjectReadOnly) return true;
        ShowUpgradePrompt(CurrentProjectAccessText);
        return false;
    }

    private int CountUniqueSelections()
    {
        return Selections
            .Select(item => string.IsNullOrWhiteSpace(item.NumericId) ? $"N:{item.NormalizedName}" : $"I:{item.NumericId}")
            .Where(key => key.Length > 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    private void ShowUpgradePrompt(string message)
    {
        StatusMessage = message;
        _dialogService.ShowInfo($"{message}\n\n你可以继续使用免费版基础功能；如需此能力，请在“授权与版本”页查看专业版说明。购买页面只会在你主动点击时打开。");
        CurrentPage = "Activation";
    }

    private sealed record NormalWorkspaceSnapshot(
        IReadOnlyList<SourceDirectoryEntry> Sources,
        IReadOnlyList<MediaSelectionItem> Selections,
        SourceDirectoryEntry? SelectedSource,
        MediaIndexSnapshot MediaIndex,
        string TextInput,
        string ProjectName,
        string OutputBaseDirectory,
        OutputMode OutputMode,
        CollectionCategory CollectionCategory,
        CustomerJpegHandlingMode CustomerJpegMode,
        string CustomExtensionsText,
        bool MatchCompleted,
        string StatusMessage);

    private void MarkDuplicates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Selections)
        {
            var key = item.NumericId.Length > 0 ? $"N:{item.NumericId}" : $"F:{item.NormalizedName}";
            item.IsDuplicate = !seen.Add(key);
            item.IsSelected = !item.IsDuplicate;
            item.Note = item.IsDuplicate ? "重复输入，实际源文件只复制一次" : string.Empty;
        }
    }

    private void RegenerateOutputFolderName()
    {
        var project = string.IsNullOrWhiteSpace(ProjectName) ? "未命名项目" : ProjectName.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars()) project = project.Replace(invalid, '_');
        var suffix = CollectionCategory switch
        {
            CollectionCategory.JpegOnly => "精选JPG",
            CollectionCategory.RawOnly => "精选RAW",
            _ => "精选文件"
        };
        OutputFolderName = $"{project}_{suffix}_{DateTime.Now:yyyyMMdd_HHmm}";
    }

    private MediaMatchOptions CreateMatchOptions() => new(
        CollectionCategory,
        Settings.EnabledJpegExtensions,
        Settings.EnabledRawExtensions,
        CurrentCustomExtensions(),
        CustomerJpegMode == CustomerJpegHandlingMode.AllowCustomerFile)
    {
        CustomerJpegMode = CustomerJpegMode
    };

    private IReadOnlyList<string> CurrentCustomExtensions()
    {
        var parsed = MediaExtensionPolicy.ParseCustomExtensions(CustomExtensionsText);
        return parsed.IsValid ? parsed.Extensions : [];
    }

    private int ExpectedTargetCount() => CollectionCategory switch
    {
        CollectionCategory.JpegOnly or CollectionCategory.RawOnly => 1,
        CollectionCategory.JpegAndRaw => 2,
        CollectionCategory.Custom => CurrentCustomExtensions().Count,
        _ => 0
    };

    private bool IsCategoryConfigurationValid() => CollectionCategory != CollectionCategory.Custom ||
                                                   string.IsNullOrEmpty(CustomExtensionsError) && CurrentCustomExtensions().Count > 0;

    private void QueueRematch(string message)
    {
        _matchCompleted = false;
        if (Selections.Count > 0 && IsCategoryConfigurationValid() && !IsBusy)
        {
            StatusMessage = message;
            _ = MatchAsync();
        }
        else
        {
            ResetMatchResults();
            StatusMessage = CollectionCategory == CollectionCategory.Custom && CurrentCustomExtensions().Count == 0
                ? "请输入至少一个自定义扩展名"
                : "归片设置已更新，等待匹配";
        }
    }

    private void ResetMatchResults()
    {
        foreach (var item in Selections)
        {
            item.FormatResults.Clear();
            item.OverallStatus = MediaOverallStatus.Waiting;
            item.RaiseComputedProperties();
        }
        UpdateStatistics();
    }

    private static string BuildItemNote(MediaSelectionItem item)
    {
        var notes = new List<string>();
        if (item.IsDuplicate) notes.Add("重复输入，实际源文件只复制一次");
        foreach (var result in item.FormatResults)
        {
            if (result.Category == FileCategory.Jpeg && result.Status == MatchStatus.WaitingManualConfirmation) notes.Add("未找到来源 JPG；客户 JPG 等待手动确认");
            else if (result.Category == FileCategory.Jpeg && result.Status == MatchStatus.NotFound && result.CandidateCount > 0) notes.Add("未找到来源 JPG；客户返回文件未自动采用");
            else if (result.Status == MatchStatus.NotFound) notes.Add($"{result.DisplayName} 未找到");
            else if (result.Status == MatchStatus.Conflict) notes.Add($"{result.DisplayName} 存在冲突");
            else if (result.Status == MatchStatus.CopyFailed) notes.Add($"{result.DisplayName} 复制失败：{result.ErrorMessage}");
            else if (result.UsedCustomerFile) notes.Add($"{result.DisplayName} 使用客户返回文件；原始质量未经确认");
        }
        return string.Join("；", notes);
    }

    private static string NormalizePathForComparison(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return path.Trim(); }
    }

    private bool CanCopy() => !IsBusy && _matchCompleted && Selections
        .Where(x => x.IsSelected)
        .SelectMany(x => x.FormatResults)
        .Any(x => x.SelectedFile is not null && x.Status is MatchStatus.Matched or MatchStatus.ManuallyConfirmed);

    private async Task RunOperationAsync(string stage, Func<CancellationToken, Task> operation)
    {
        if (IsBusy) return;
        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        StatusMessage = stage;
        ProgressPercent = 0;
        try
        {
            var writeRoot = stage.Contains("复制", StringComparison.Ordinal) || stage.Contains("导出", StringComparison.Ordinal)
                ? $"write-root:{OutputDirectory}"
                : string.Empty;
            await _taskOperationBridge.RunAsync(stage, async (context, taskToken) =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(taskToken, _operationCancellation.Token);
                await context.SafeBoundaryAsync("开始", 0, cancellationToken: linked.Token);
                await operation(linked.Token);
                var succeeded = ProcessedCount > 0 ? (int)Math.Min(int.MaxValue, ProcessedCount) : 1;
                var summary = new TaskResultSummary(succeeded, succeeded, 0, 0, 0, 0, 0, 0);
                await context.ReportProgressAsync(100, "完成", CurrentItem, summary, linked.Token);
                return summary;
            }, _currentProject.Id == Guid.Empty ? null : _currentProject.Id, writeRoot, _operationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"{stage}已取消";
            _logService.Info($"用户取消操作：{stage}");
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
            _dialogService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            _logService.Error($"{stage}失败。", ex);
            StatusMessage = $"{stage}失败，请检查路径、文件权限和存储设备。";
            _dialogService.ShowError(StatusMessage);
        }
        finally
        {
            IsBusy = false;
            _operationCancellation.Dispose();
            _operationCancellation = null;
            RefreshCommands();
        }
    }

    private IProgress<OperationProgress> CreateProgress() => new Progress<OperationProgress>(progress =>
    {
        StatusMessage = progress.Stage;
        CurrentItem = progress.CurrentItem;
        ProcessedCount = progress.Processed;
        ProgressPercent = progress.Percent;
    });

    private void UpdateStatistics()
    {
        TotalCount = Selections.Count;
        var allResults = Selections.SelectMany(x => x.FormatResults).ToList();
        TargetFileCount = allResults.Count > 0 ? allResults.Count : Selections.Count * ExpectedTargetCount();
        JpegMatchedCount = allResults.Count(x => x.Category == FileCategory.Jpeg && x.SelectedFile is not null);
        RawMatchedCount = allResults.Count(x => x.Category == FileCategory.Raw && x.SelectedFile is not null);
        CompleteMatchedCount = Selections.Count(x => x.OverallStatus is MediaOverallStatus.CompleteMatched or MediaOverallStatus.FullyCopied);
        PartialMatchedCount = Selections.Count(x => x.OverallStatus is MediaOverallStatus.PartialMatched or MediaOverallStatus.PartiallyCopied or MediaOverallStatus.WaitingConfirmation);
        ConflictCount = Selections.Count(x => x.OverallStatus == MediaOverallStatus.Conflict);
        NotFoundCount = Selections.Count(x => x.OverallStatus == MediaOverallStatus.NotFound);
        OnPropertyChanged(nameof(AttentionCount));
        OnPropertyChanged(nameof(TaskCenterSummary));
        CopiedCount = allResults.Count(x => x.Status == MatchStatus.Copied);
    }

    private void RefreshCommands()
    {
        AddSourceCommand.RaiseCanExecuteChanged();
        RemoveSourceCommand.RaiseCanExecuteChanged();
        ClearSourcesCommand.RaiseCanExecuteChanged();
        MoveSourceUpCommand.RaiseCanExecuteChanged();
        MoveSourceDownCommand.RaiseCanExecuteChanged();
        BrowseOutputCommand.RaiseCanExecuteChanged();
        ParseTextCommand.RaiseCanExecuteChanged();
        PasteCommand.RaiseCanExecuteChanged();
        ClearSelectionsCommand.RaiseCanExecuteChanged();
        ScanCommand.RaiseCanExecuteChanged();
        MatchCommand.RaiseCanExecuteChanged();
        CopyCommand.RaiseCanExecuteChanged();
        ExportReportCommand.RaiseCanExecuteChanged();
        OpenOutputCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        ClearTaskCommand.RaiseCanExecuteChanged();
        ShowDetailsCommand.RaiseCanExecuteChanged();
        TutorialPrimaryCommand.RaiseCanExecuteChanged();
        TutorialBackCommand.RaiseCanExecuteChanged();
        TutorialExitCommand.RaiseCanExecuteChanged();
        TutorialRetryCommand.RaiseCanExecuteChanged();
        TutorialRecreateDataCommand.RaiseCanExecuteChanged();
        HelpCommand.RaiseCanExecuteChanged();
        FeedbackCommand.RaiseCanExecuteChanged();
        NavigateCommand.RaiseCanExecuteChanged();
        GoToWorkflowStepCommand.RaiseCanExecuteChanged();
        NewProjectCommand.RaiseCanExecuteChanged();
        ContinueProjectCommand.RaiseCanExecuteChanged();
        ActivateLicenseCommand.RaiseCanExecuteChanged();
        DeactivateLicenseCommand.RaiseCanExecuteChanged();
        ValidateLicenseCommand.RaiseCanExecuteChanged();
        PurchaseCommand.RaiseCanExecuteChanged();
        SaveOutputPresetCommand.RaiseCanExecuteChanged();
        SaveProjectCommand.RaiseCanExecuteChanged();
        RunBatchCommand.RaiseCanExecuteChanged();
    }

    public sealed record CollectionCategoryOption(CollectionCategory Value, string Label);
    public sealed record OutputModeOption(OutputMode Value, string Label);
    public sealed record CustomerJpegModeOption(CustomerJpegHandlingMode Value, string Label);
    public sealed record SourceDirectoryTypeOption(SourceDirectoryType Value, string Label);
    public sealed record ThemeOption(ThemeMode Value, string Label);
    public sealed record AccentOption(AccentPreset Value, string Label);
    public sealed record DensityOption(InterfaceDensity Value, string Label);
    public sealed record SidebarOption(SidebarMode Value, string Label);
    public sealed record MotionOption(MotionPreference Value, string Label);
    public sealed record FontScaleOption(FontScale Value, string Label);
}
