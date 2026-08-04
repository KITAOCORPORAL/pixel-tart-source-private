using System.Windows;
using System.Windows.Threading;
using System.Net.Http;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Core.Services.FileOperations;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant;

public partial class App : Application
{
    private SingleInstanceManager? _singleInstance;
    private FileLogService? _logService;
    private MainViewModel? _mainViewModel;
    private IAppearanceService? _appearanceService;
    private ApplicationCompositionRoot? _compositionRoot;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new AppDataMigrationService().MigrateLegacyData();
        _logService = new FileLogService();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var assemblyName = typeof(App).Assembly.GetName().Name ?? "RAWSelectionAssistant";
        _singleInstance = new SingleInstanceManager($"{assemblyName}-96AFD8F1-7EF9-4D10-AFA9-18C6BE383E17");
        if (!_singleInstance.TryAcquire())
        {
            Shutdown();
            return;
        }
        _singleInstance.ActivationRequested += ActivateMainWindow;

        try
        {
            _compositionRoot = await ApplicationCompositionRoot.CreateAsync();
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
                new WorkCalendarViewModel(_compositionRoot.ShootBookingService, _compositionRoot.ProjectRepository, _compositionRoot.BookingDocumentWorkflowService, dialogService));

            await _mainViewModel.InitializeAsync();
            var window = new MainWindow { DataContext = _mainViewModel };
            window.ApplySavedBounds(_mainViewModel.Settings);
            MainWindow = window;
            window.Show();
            _logService.Info($"{Branding.ProductName}已启动。");
        }
        catch (Exception ex)
        {
            _logService.Error("应用程序启动失败。", ex);
            MessageBox.Show("软件启动失败，已记录详细信息。请重新打开；如果仍然失败，请提供日志文件。", Branding.ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
            _singleInstance.Dispose();
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _mainViewModel?.SaveSettingsAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logService?.Error("退出时保存设置失败。", ex);
        }

        _singleInstance?.Dispose();
        _appearanceService?.Dispose();
        _logService?.Info("应用程序已退出。");
        base.OnExit(e);
    }

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
        MessageBox.Show("程序遇到问题，但已记录详细信息。请重试当前操作。", Branding.ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        if (MainWindow is null || !MainWindow.IsVisible)
        {
            _singleInstance?.Dispose();
            Shutdown(-1);
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) =>
        _logService?.Error("发生未处理的程序异常。", e.ExceptionObject as Exception);

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logService?.Error("发生未观察到的后台任务异常。", e.Exception);
        e.SetObserved();
    }
}
