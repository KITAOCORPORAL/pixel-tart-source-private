using System.Windows;
using System.Windows.Threading;
using System.Net.Http;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Core.Services.FileOperations;
using RAWSelectionAssistant.Core.Services.Tethering;
using RAWSelectionAssistant.Core.Services.OnlineSelection;
using RAWSelectionAssistant.Core.Services.Tasks;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using PixelTart.Kernel;
using PixelTart.Modules.AssetLibrary;
using PixelTart.Modules.RawTool;
using PixelTart.Modules.OnlineSelection;

namespace RAWSelectionAssistant;

public partial class App : Application
{
    private SingleInstanceManager? _singleInstance;
    private FileLogService? _logService;
    private MainViewModel? _mainViewModel;
    private IAppearanceService? _appearanceService;
    private ApplicationCompositionRoot? _compositionRoot;
    private HttpClient? _weatherHttpClient;
    private WeatherFeatureState? _weatherState;
    private PixelTartModuleRegistry? _moduleRegistry;
#if ASSET_LIBRARY_P1_STATE_ACCEPTANCE
    private AssetLibraryP1AcceptanceStateController? _assetLibraryP1StateController;
#endif
#if ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE
    private AssetLibraryP1AutomatedAcceptanceController? _assetLibraryP1AutomatedController;
#endif
#if ASSET_LIBRARY_P2_AUTOMATED_ACCEPTANCE
    private AssetLibraryP2AutomatedAcceptanceController? _assetLibraryP2AutomatedController;
#endif

    public PixelTartModuleRegistry? ModuleRegistry => _moduleRegistry;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
#if ASSET_LIBRARY_P1_STATE_ACCEPTANCE
        if (!TryAcquireSingleInstance())
        {
            Shutdown();
            return;
        }
        _assetLibraryP1StateController = AssetLibraryP1AcceptanceStateController.TryCreate(AppDataPaths.Root, logService: null);
#endif
#if ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE
        if (!TryAcquireSingleInstance())
        {
            Shutdown();
            return;
        }
        _assetLibraryP1AutomatedController = AssetLibraryP1AutomatedAcceptanceController.TryCreate(AppDataPaths.Root, logService: null);
#endif
#if ASSET_LIBRARY_P2_AUTOMATED_ACCEPTANCE
        if (!TryAcquireSingleInstance())
        {
            Shutdown();
            return;
        }
        _assetLibraryP2AutomatedController = AssetLibraryP2AutomatedAcceptanceController.TryCreate(AppDataPaths.Root, logService: null);
#endif
        new AppDataMigrationService().MigrateLegacyData();
        _logService = new FileLogService();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
#if !ASSET_LIBRARY_P1_STATE_ACCEPTANCE && !ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE && !ASSET_LIBRARY_P2_AUTOMATED_ACCEPTANCE
        if (!TryAcquireSingleInstance())
        {
            Shutdown();
            return;
        }
#endif

        try
        {
            _compositionRoot = await ApplicationCompositionRoot.CreateAsync();
            _moduleRegistry = CreateModuleRegistry(_compositionRoot.OperationBridge);
            await _moduleRegistry.InitializeAsync();
            await _moduleRegistry.ActivateAllAsync();
            var normalizer = new FileNameNormalizer();
            var inputParser = new InputParserService(_logService);
            var licenseConfiguration = new LicenseConfigurationService().Load();
            var licenseProvider = LicenseProviderFactory.Create(
                licenseConfiguration,
                new HttpClient { Timeout = TimeSpan.FromSeconds(20) },
                _logService,
                allowMockProvider: false);
            var licenseService = new LicenseService(
                licenseConfiguration,
                licenseProvider,
                new DpapiLicenseStorageService(_logService),
                new DeviceFingerprintService(),
                _logService);
            await licenseService.InitializeAsync();
            var featureGateService = new FeatureGateService(licenseService);
            var projectEntitlementService = new ProjectEntitlementService(normalizer, featureGateService);
            var projectHistoryService = new ProjectHistoryService(featureGateService, _logService, repository: _compositionRoot.ProjectRepository);
            var outputPresetService = new OutputPresetService(featureGateService, _logService);
            var batchProjectService = new BatchProjectService(featureGateService);
            var jpegMetadataService = new JpegMetadataService(_logService);
            var jpegAssessmentService = new JpegQualityAssessmentService();
            var legacyIndex = Path.Combine(AppDataPaths.LegacyRoot, "Indexes", "media-index.json");
            var indexPath = File.Exists(Path.Combine(AppDataPaths.IndexDirectory, "media-index.json"))
                ? Path.Combine(AppDataPaths.IndexDirectory, "media-index.json")
                : File.Exists(legacyIndex) ? legacyIndex : null;
            var indexService = new MediaIndexService(normalizer, _logService, cacheFilePath: indexPath, jpegMetadataService: jpegMetadataService, featureGateService: featureGateService, repository: _compositionRoot.MediaIndexRepository);
            var matchService = new MediaMatchService(normalizer, jpegMetadataService, jpegAssessmentService, featureGateService);
            var copyService = new MediaCopyService(_logService, _compositionRoot.FileOperationExecutor, new FileConflictResolver());
            var reportService = new MediaReportService(_logService);
            var legacySettings = Path.Combine(AppDataPaths.LegacyRoot, "settings.json");
            var settingsPath = File.Exists(AppDataPaths.SettingsFile)
                ? AppDataPaths.SettingsFile
                : File.Exists(legacySettings) ? legacySettings : null;
            var settingsService = new SettingsService(_logService, settingsPath);
            var startupSettings = await settingsService.LoadAsync();
            _weatherState = new WeatherFeatureState();
            _weatherState.Apply(startupSettings.Weather);
            _weatherHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var weatherOptions = new OpenMeteoOptions(
                startupSettings.Weather.WeatherApiBaseUrl,
                startupSettings.Weather.GeocodingApiBaseUrl,
                string.IsNullOrWhiteSpace(startupSettings.Weather.ApiKey) ? null : startupSettings.Weather.ApiKey);
            var weatherService = new WeatherForecastService(
                new OpenMeteoWeatherProvider(_weatherHttpClient, weatherOptions),
                new OpenMeteoGeocodingProvider(_weatherHttpClient, weatherOptions),
                new JsonWeatherCacheStore(), _weatherState,
                _compositionRoot.NotificationCenter, _compositionRoot.AuditLog);
            var currentLocationService = new WindowsCurrentLocationService();
            var tutorialDataService = new TutorialDataService();
            var onboardingService = new OnboardingService(settingsService, tutorialDataService, _logService);
            var feedbackService = new FeedbackService(
                new FeedbackRequestBuilder().Build(),
                new WpfFeedbackClipboard(),
                new ShellFeedbackMailLauncher(),
                _logService);
            var dialogService = new WpfDialogService(feedbackService);
            var clipboardService = new WpfClipboardService();
            _appearanceService = new AppearanceService();

            var calendarViewModel = new WorkCalendarViewModel(
                _compositionRoot.ShootBookingService,
                _compositionRoot.ProjectRepository,
                _compositionRoot.BookingDocumentWorkflowService,
                dialogService,
                _compositionRoot.BookingReminderService,
                _compositionRoot.BookingReminderScheduler,
                weatherService,
                _weatherState,
                new JsonCalendarAvailabilityStore(),
                _compositionRoot.BookingPeopleService,
                _compositionRoot.FinanceService,
                currentLocationService,
                _compositionRoot.BookingWorkflowService);
            var workbenchSchedule = new WorkbenchCalendarSummaryViewModel(_compositionRoot.WorkbenchScheduleService, _compositionRoot.ShootBookingService as IBookingChangeNotifier, weatherService: weatherService);
            var reminderNotifications = new ReminderNotificationCenterViewModel(
                _compositionRoot.BookingReminderNotificationService,
                _compositionRoot.NotificationCenter,
                _compositionRoot.BookingReminderService);
            var tetherProxyCache = new TetherProxyCacheService();
            var tetherPairing = new TetherPairingService(_compositionRoot.TetherAssetRepository);
            var watchFolderAdapter = new WatchFolderCameraAdapter(
                _compositionRoot.TetherSessionRepository,
                _compositionRoot.TetherAssetRepository,
                new FileStabilityProbe(),
                tetherPairing,
                tetherProxyCache,
                _compositionRoot.TetherTransferService,
                _compositionRoot.AuditLog,
                _compositionRoot.NotificationCenter);
            var tetherPage = new TetherCaptureViewModel(
                watchFolderAdapter,
                _compositionRoot.TetherSessionRepository,
                _compositionRoot.TetherAssetRepository,
                tetherProxyCache,
                dialogService,
                new TetherAnnotationService(
                    _compositionRoot.TetherAnnotationRepository,
                    _compositionRoot.AuditLog,
                    _compositionRoot.NotificationCenter));
            var financePage = new FinanceViewModel(
                _compositionRoot.FinanceService,
                dialogService,
                _compositionRoot.ProjectRepository,
                _compositionRoot.ShootBookingService);
            var onlineSelectionPage = new OnlineSelectionViewModel(
                OnlineSelectionProviderFactory.CreateDefault(),
                new JsonSelectionWorkspaceStore(AppDataPaths.OnlineSelectionWorkspaceFile),
                new SelectionResultSyncService(normalizer),
                _compositionRoot.SelectionProxyJpegService,
                Path.Combine(AppDataPaths.OnlineSelectionDirectory, "Proxies"),
                dialogService);

            _mainViewModel = new MainViewModel(
                normalizer,
                inputParser,
                indexService,
                matchService,
                copyService,
                reportService,
                settingsService,
                _logService,
                dialogService,
                clipboardService,
                onboardingService,
                licenseService,
                featureGateService,
                projectEntitlementService,
                projectHistoryService,
                outputPresetService,
                batchProjectService,
                _appearanceService,
                _compositionRoot.TaskCenter,
                _compositionRoot.OperationBridge,
                _compositionRoot.QuickToolsRepository,
                _compositionRoot.MatchDecisionRepository,
                calendarViewModel,
                workbenchSchedule,
                reminderNotifications,
                _weatherState,
                tetherPage,
                financePage,
                onlineSelectionPage: onlineSelectionPage,
                rawToJpegPage: new RawToJpegViewModel(_compositionRoot.RawToJpegCoordinator, dialogService),
                batchCompressionPage: new BatchCompressionViewModel(_compositionRoot.BatchCompressionCoordinator, dialogService));

            calendarViewModel.FinanceRequested += async (_, request) =>
            {
                _mainViewModel.NavigateCommand.Execute("Finance");
                await financePage.OpenForBookingAsync(request.BookingId, request.Kind);
            };

            await _mainViewModel.InitializeAsync();
#if ASSET_LIBRARY_P1_STATE_ACCEPTANCE
            _assetLibraryP1StateController?.ApplyAcceptanceStartRoute(_mainViewModel);
#endif
#if ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE
            _assetLibraryP1AutomatedController?.ApplyStartRoute(_mainViewModel);
#endif
#if ASSET_LIBRARY_P2_AUTOMATED_ACCEPTANCE
            _assetLibraryP2AutomatedController?.ApplyStartRoute(_mainViewModel);
#endif
            await reminderNotifications.InitializeAsync();
            var window = new MainWindow { DataContext = _mainViewModel };
            window.ApplySavedBounds(_mainViewModel.Settings);
            MainWindow = window;
#if ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE
            if (_assetLibraryP1AutomatedController is not null)
            {
                _assetLibraryP1AutomatedController.BindWindow(window);
                window.ConfigureAssetLibraryP1AutomatedAcceptance(_assetLibraryP1AutomatedController);
            }
#endif
#if ASSET_LIBRARY_P2_AUTOMATED_ACCEPTANCE
            if (_assetLibraryP2AutomatedController is not null)
            {
                _assetLibraryP2AutomatedController.BindWindow(window);
                window.ConfigureAssetLibraryP2AutomatedAcceptance(_assetLibraryP2AutomatedController);
            }
#endif
            window.Show();
            await _compositionRoot.BookingReminderScheduler.StartAsync();
            _logService.Info($"{Branding.ProductName}已启动。");
        }
        catch (Exception ex)
        {
            _logService.Error("应用程序启动失败。", ex);
#if ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE
            _assetLibraryP1AutomatedController?.Fail(ex);
#elif ASSET_LIBRARY_P2_AUTOMATED_ACCEPTANCE
            _assetLibraryP2AutomatedController?.Fail(ex);
#else
            ThemedMessageDialog.Show(null, Branding.ProductName, "软件启动失败，已记录详细信息。请重新打开；如果仍然失败，请提供日志文件。", ThemedMessageKind.Error);
#endif
            _singleInstance?.Dispose();
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _compositionRoot?.BookingReminderScheduler.StopAsync().GetAwaiter().GetResult();
            _mainViewModel?.SaveSettingsAsync().GetAwaiter().GetResult();
            if (_mainViewModel?.TetherPage is not null) _mainViewModel.TetherPage.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logService?.Error("退出时保存设置失败。", ex);
#if ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE
            _assetLibraryP1AutomatedController?.Fail(ex);
#endif
#if ASSET_LIBRARY_P2_AUTOMATED_ACCEPTANCE
            _assetLibraryP2AutomatedController?.Fail(ex);
#endif
        }

        _singleInstance?.Dispose();
        _mainViewModel?.WorkbenchSchedule?.Dispose();
        _mainViewModel?.ReminderNotifications?.Dispose();
        if (_compositionRoot is not null) _compositionRoot.BookingReminderScheduler.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _weatherHttpClient?.Dispose();
        _appearanceService?.Dispose();
#if ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE
        try
        {
            if (MainWindow is RAWSelectionAssistant.MainWindow automatedWindow)
                automatedWindow.TeardownAssetLibraryP1AutomatedAcceptanceAsync().GetAwaiter().GetResult();
            _moduleRegistry?.ShutdownAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logService?.Error("自动验收退出时关闭模块失败。", ex);
            _assetLibraryP1AutomatedController?.Fail(ex);
        }
        _assetLibraryP1AutomatedController?.FinalizeOnApplicationExit(e.ApplicationExitCode);
#elif ASSET_LIBRARY_P2_AUTOMATED_ACCEPTANCE
        try
        {
            if (MainWindow is RAWSelectionAssistant.MainWindow automatedWindow)
                automatedWindow.TeardownAssetLibraryP2AutomatedAcceptanceAsync().GetAwaiter().GetResult();
            _moduleRegistry?.ShutdownAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logService?.Error("P2 自动验收退出时关闭模块失败。", ex);
            _assetLibraryP2AutomatedController?.Fail(ex);
        }
        _assetLibraryP2AutomatedController?.FinalizeOnApplicationExit(e.ApplicationExitCode);
#else
        _moduleRegistry?.ShutdownAsync().GetAwaiter().GetResult();
#endif
        _logService?.Info("应用程序已退出。");
        base.OnExit(e);
    }

    private bool TryAcquireSingleInstance()
    {
        var assemblyName = typeof(App).Assembly.GetName().Name ?? "RAWSelectionAssistant";
        _singleInstance = new SingleInstanceManager($"{assemblyName}-96AFD8F1-7EF9-4D10-AFA9-18C6BE383E17");
        if (!_singleInstance.TryAcquire()) return false;
        _singleInstance.ActivationRequested += ActivateMainWindow;
        return true;
    }

    private PixelTartModuleRegistry CreateModuleRegistry(TaskOperationBridge taskOperationBridge)
    {
        var registry = new PixelTartModuleRegistry();
        registry.Capabilities.Register(new("core.navigation", "pixel-tart.kernel", "kernel/v1"));
        registry.Capabilities.Register(new("core.task-center", "pixel-tart.kernel", "kernel/v1"));
        registry.Capabilities.Register(new("core.settings", "pixel-tart.kernel", "kernel/v1"));
        registry.Capabilities.Register(new("core.file-safety", "pixel-tart.kernel", "kernel/v1"));
#if MODULAR_HARNESS_DEV_PREVIEW
        var enableAssetLibraryPreview = true;
        var assetLibraryDemoDirectory = Environment.GetEnvironmentVariable("PIXEL_TART_ASSET_LIBRARY_DEMO_DIR");
#else
        const bool enableAssetLibraryPreview = false;
        string? assetLibraryDemoDirectory = null;
#endif
        IAssetLibraryLoadStateController? assetLibraryP1StateController = null;
#if ASSET_LIBRARY_P1_STATE_ACCEPTANCE
        assetLibraryP1StateController = _assetLibraryP1StateController?.HasStateScenario == true
            ? _assetLibraryP1StateController
            : null;
        if (assetLibraryP1StateController is not null)
        {
            enableAssetLibraryPreview = false;
            assetLibraryDemoDirectory = null;
        }
#endif
#if ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE
        assetLibraryP1StateController = _assetLibraryP1AutomatedController;
        if (assetLibraryP1StateController is not null)
        {
            enableAssetLibraryPreview = false;
            assetLibraryDemoDirectory = null;
        }
#endif
#if ASSET_LIBRARY_P2_AUTOMATED_ACCEPTANCE
        assetLibraryP1StateController = _assetLibraryP2AutomatedController;
        if (assetLibraryP1StateController is not null)
        {
            enableAssetLibraryPreview = false;
            assetLibraryDemoDirectory = null;
        }
#endif
        registry.Register(new AssetLibraryModule(() =>
        {
            IReadOnlyList<AssetLibraryModuleDiagnostic> diagnostics = enableAssetLibraryPreview ? BuildModuleDiagnostics(registry) : [];
            var workspaceSettings = _mainViewModel?.Settings.AssetLibraryWorkspace ?? new AssetLibraryWorkspaceSettings();
            return new PixelTart.Modules.AssetLibrary.AssetLibraryPage(
                Path.Combine(AppDataPaths.DataDirectory, "asset-library-v16.db"),
                taskOperationBridge,
                diagnostics,
                enableAssetLibraryPreview,
                assetLibraryDemoDirectory,
                workspaceSettings,
                _logService,
                assetLibraryP1StateController);
        }));
        registry.Register(new RawToolModule());
        registry.Register(new OnlineSelectionModule());
        return registry;
    }

    private static IReadOnlyList<AssetLibraryModuleDiagnostic> BuildModuleDiagnostics(PixelTartModuleRegistry registry) =>
        registry.Modules
            .OrderBy(module => module.Manifest.NavigationOrder)
            .Select(module =>
            {
                var manifest = module.Manifest;
                var state = registry.Diagnostics.FirstOrDefault(item => item.ModuleId.Equals(manifest.ModuleId, StringComparison.OrdinalIgnoreCase))?.State;
                var automationId = manifest.ModuleId switch
                {
                    AssetLibraryModule.ModuleId => "AssetLibraryModuleDiagnostic",
                    RawToolModule.ModuleId => "RawToolModuleDiagnostic",
                    OnlineSelectionModule.ModuleId => "OnlineSelectionModuleDiagnostic",
                    _ => "ModuleDiagnostic"
                };
                var text = $"{manifest.DisplayName} · {manifest.ModuleType} · route={manifest.Route ?? "-"} · state={state} · provides={string.Join(", ", manifest.Provides)}";
                return new AssetLibraryModuleDiagnostic(automationId, text);
            })
            .ToArray();

    private void ActivateMainWindow()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (MainWindow is null)
            {
                return;
            }

            if (MainWindow.WindowState == WindowState.Minimized)
            {
                MainWindow.WindowState = WindowState.Normal;
            }

            MainWindow.Show();
            MainWindow.Activate();
            MainWindow.Topmost = true;
            MainWindow.Topmost = false;
            MainWindow.Focus();
            _singleInstance?.AcknowledgeActivation();
        });
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logService?.Error("发生未处理的界面异常。", e.Exception);
#if ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE
        _assetLibraryP1AutomatedController?.Fail(e.Exception);
        e.Handled = true;
        Shutdown(-1);
#elif ASSET_LIBRARY_P2_AUTOMATED_ACCEPTANCE
        _assetLibraryP2AutomatedController?.Fail(e.Exception);
        e.Handled = true;
        Shutdown(-1);
#else
        ThemedMessageDialog.Show(Current?.MainWindow, Branding.ProductName, "程序遇到问题，但已记录详细信息。请重试当前操作。", ThemedMessageKind.Error);
        e.Handled = true;
        if (MainWindow is null || !MainWindow.IsVisible)
        {
            _singleInstance?.Dispose();
            Shutdown(-1);
        }
#endif
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception
            ?? new InvalidOperationException("The automated process raised a non-Exception AppDomain failure.");
        _logService?.Error("发生未处理的程序异常。", exception);
#if ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE
        _assetLibraryP1AutomatedController?.Fail(exception);
#endif
#if ASSET_LIBRARY_P2_AUTOMATED_ACCEPTANCE
        _assetLibraryP2AutomatedController?.Fail(exception);
#endif
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logService?.Error("发生未观察到的后台任务异常。", e.Exception);
#if ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE
        _assetLibraryP1AutomatedController?.Fail(e.Exception);
        e.SetObserved();
        Shutdown(-1);
#elif ASSET_LIBRARY_P2_AUTOMATED_ACCEPTANCE
        _assetLibraryP2AutomatedController?.Fail(e.Exception);
        e.SetObserved();
        Shutdown(-1);
#else
        e.SetObserved();
#endif
    }
}
