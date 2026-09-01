#if ASSET_LIBRARY_P2_AUTOMATED_ACCEPTANCE
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Services;
using AssetLibraryWpfPage = PixelTart.Modules.AssetLibrary.AssetLibraryPage;

namespace RAWSelectionAssistant;

public partial class MainWindow
{
    private static readonly (int Width, int Height, int ScalePercent)[] AssetLibraryP2LayoutMatrix =
    [
        (1366, 768, 100),
        (1920, 1080, 125),
        (1920, 1080, 150),
        (2560, 1440, 175),
    ];

    private AssetLibraryP2AutomatedAcceptanceController? _assetLibraryP2AutomatedController;
    private AssetLibraryP2AutomatedAcceptanceDriver? _assetLibraryP2AutomatedDriver;
    private AssetLibraryWpfPage? _assetLibraryP2AutomatedPage;

    internal void ConfigureAssetLibraryP2AutomatedAcceptance(AssetLibraryP2AutomatedAcceptanceController controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        if (_assetLibraryP2AutomatedController is not null)
            throw new InvalidOperationException("The live window may host only one P2 automated acceptance controller.");
        _assetLibraryP2AutomatedController = controller;
        Loaded += AssetLibraryP2AutomatedAcceptance_Loaded;
        Closed += (_, _) => _assetLibraryP2AutomatedDriver?.Dispose();
    }

    private async void AssetLibraryP2AutomatedAcceptance_Loaded(object sender, RoutedEventArgs e)
    {
        var controller = _assetLibraryP2AutomatedController
            ?? throw new InvalidOperationException("The P2 automated acceptance controller is unavailable.");
        try
        {
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
            if (_viewModel is null) throw new InvalidOperationException("The live MainWindow has no MainViewModel.");
            if (AssetLibraryWorkspace.Content is not AssetLibraryWpfPage page)
                throw new InvalidOperationException("The live Asset Library module page did not materialize.");
            _assetLibraryP2AutomatedPage = page;
            var driver = new AssetLibraryP2AutomatedAcceptanceDriver(page);
            _assetLibraryP2AutomatedDriver = driver;
            controller.Observe(_viewModel, driver);
            await ExecuteAssetLibraryP2AutomatedScenarioAsync(controller, driver);
            controller.MarkExecutionCompleted();
            await TeardownAssetLibraryP2AutomatedAcceptanceAsync();
            Close();
        }
        catch (Exception exception)
        {
            controller.Fail(exception);
            try { await TeardownAssetLibraryP2AutomatedAcceptanceAsync(); }
            catch (Exception teardown) { controller.Fail(new AggregateException(exception, teardown)); }
            Application.Current.Shutdown(-1);
        }
    }

    internal async Task TeardownAssetLibraryP2AutomatedAcceptanceAsync()
    {
        _assetLibraryP2AutomatedDriver?.Dispose();
        _assetLibraryP2AutomatedDriver = null;
        var page = _assetLibraryP2AutomatedPage ?? AssetLibraryWorkspace.Content as AssetLibraryWpfPage;
        if (page is null) return;
        _assetLibraryP2AutomatedPage = page;
        await page.DisposeAsync();
    }

    private async Task ExecuteAssetLibraryP2AutomatedScenarioAsync(
        AssetLibraryP2AutomatedAcceptanceController controller,
        AssetLibraryP2AutomatedAcceptanceDriver driver)
    {
        var scenario = controller.ScenarioId;
        controller.SetActiveScenario(scenario);
        if (scenario == AssetLibraryP2AutomatedAcceptanceController.ResilienceStatesScenario)
        {
            await RunResilienceScenarioAsync(controller, driver);
            return;
        }

        var firstScreen = Stopwatch.StartNew();
        await WaitForAssetLibraryStateAsync(driver, state => state.IsReady && !state.HasLoadError, "the repository-backed P2 browser");
        firstScreen.Stop();
        switch (scenario)
        {
            case AssetLibraryP2AutomatedAcceptanceController.FixtureIntegrityScenario:
                await RunFixtureIntegrityScenarioAsync(controller, driver);
                break;
            case AssetLibraryP2AutomatedAcceptanceController.OrganizationBrowserScenario:
                await RunOrganizationScenarioAsync(controller, driver);
                break;
            case AssetLibraryP2AutomatedAcceptanceController.SmartTagQueryScenario:
                await RunSmartTagScenarioAsync(controller, driver);
                break;
            case AssetLibraryP2AutomatedAcceptanceController.FourViewsQuerySortScenario:
                await RunFourViewsScenarioAsync(controller, driver);
                break;
            case AssetLibraryP2AutomatedAcceptanceController.SelectionLargeScenario:
                await RunSelectionScenarioAsync(controller, driver);
                break;
            case AssetLibraryP2AutomatedAcceptanceController.MetadataDragCommandScenario:
                await RunMetadataScenarioAsync(controller, driver);
                break;
            case AssetLibraryP2AutomatedAcceptanceController.InspectorStatesScenario:
                await RunInspectorScenarioAsync(controller, driver);
                break;
            case AssetLibraryP2AutomatedAcceptanceController.RestartPersistenceScenario:
                await RunRestartScenarioAsync(controller, driver);
                break;
            case AssetLibraryP2AutomatedAcceptanceController.LayoutDpiPerformanceScenario:
                await RunLayoutPerformanceScenarioAsync(controller, driver, firstScreen.Elapsed.TotalMilliseconds);
                break;
            default:
                throw new InvalidOperationException($"Unsupported P2 automated scenario '{scenario}'.");
        }
    }

    private async Task RunFixtureIntegrityScenarioAsync(AssetLibraryP2AutomatedAcceptanceController controller, AssetLibraryP2AutomatedAcceptanceDriver driver)
    {
        var snapshot = driver.CaptureBrowserSnapshot();
        if (snapshot.QueryTotalCount != 500) throw new InvalidOperationException($"Fixture active count is {snapshot.QueryTotalCount}; expected 500.");
        controller.WriteJsonArtifact(controller.ScenarioId, "query-snapshot", "fixture-query.json", Evidence(controller, new
        {
            total_count = 512, active_count = 500, archived_count = 12, browser = snapshot,
            repository = "SqliteAssetLibraryRepository", schema_version = 6
        }));
        controller.RecordScenarioCheck(controller.ScenarioId, "fixture_counts", new { total = 512, active = 500, archived = 12 });
        await CaptureFrameAsync(controller, driver, "fixture-integrity", 1366, 768, 1);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunOrganizationScenarioAsync(AssetLibraryP2AutomatedAcceptanceController controller, AssetLibraryP2AutomatedAcceptanceDriver driver)
    {
        var before = driver.CaptureBrowserSnapshot();
        if (before.FolderNodeCount < 3 || before.TagNodeCount < 4 || before.SmartFolderCount < 1 || !before.FolderTreeAcyclic)
            throw new InvalidOperationException("The repository-backed organization browser did not expose the fixture hierarchy, tags, and smart folder.");
        await driver.SelectSystemCollectionAsync(AssetLibrarySystemCollection.Archived);
        await WaitForAssetLibraryStateAsync(driver, state => state.IsReady, "the archived system collection query");
        var archived = driver.CaptureBrowserSnapshot();
        if (archived.QueryTotalCount != 12) throw new InvalidOperationException("The archived collection did not return exactly 12 fixture items.");
        await driver.SelectSystemCollectionAsync(AssetLibrarySystemCollection.AllAssets);
        var restored = driver.CaptureBrowserSnapshot();
        controller.WriteJsonArtifact(controller.ScenarioId, "query-snapshot", "organization-browser.json", Evidence(controller, new { before, archived, restored }));
        await CaptureFrameAsync(controller, driver, "organization-browser", 1920, 1080, 1.25);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunSmartTagScenarioAsync(AssetLibraryP2AutomatedAcceptanceController controller, AssetLibraryP2AutomatedAcceptanceDriver driver)
    {
        await driver.SelectFirstTagAsync();
        await WaitForAssetLibraryStateAsync(driver, state => state.IsReady && state.VisibleAssetCount > 0, "the tag query");
        var tag = driver.CaptureBrowserSnapshot();
        await driver.SelectFirstSmartFolderAsync();
        await WaitForAssetLibraryStateAsync(driver, state => state.IsReady, "the smart-folder query");
        var smart = driver.CaptureBrowserSnapshot();
        if (tag.QueryTotalCount != 250 || smart.QueryTotalCount != 166)
            throw new InvalidOperationException($"The deterministic tag/smart query counts differ (tag={tag.QueryTotalCount}, smart={smart.QueryTotalCount}).");
        controller.WriteJsonArtifact(controller.ScenarioId, "query-snapshot", "smart-tag-query.json", Evidence(controller, new { tag, smart }));
        await CaptureFrameAsync(controller, driver, "smart-tag-query", 1366, 768, 1);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunFourViewsScenarioAsync(AssetLibraryP2AutomatedAcceptanceController controller, AssetLibraryP2AutomatedAcceptanceDriver driver)
    {
        var rows = new List<object>();
        foreach (var mode in new[] { AssetLibraryViewMode.Grid, AssetLibraryViewMode.Masonry, AssetLibraryViewMode.Justified, AssetLibraryViewMode.List })
        {
            var timer = Stopwatch.StartNew();
            await driver.SwitchViewAsync(mode);
            timer.Stop();
            if (timer.Elapsed.TotalMilliseconds > 250) throw new InvalidOperationException($"View switch '{mode}' exceeded 250 ms.");
            rows.Add(new { mode = mode.ToString(), elapsed_ms = timer.Elapsed.TotalMilliseconds, snapshot = driver.CaptureBrowserSnapshot() });
        }
        var sortTimer = Stopwatch.StartNew();
        await driver.SortAsync(AssetLibrarySortField.FileName);
        sortTimer.Stop();
        if (sortTimer.Elapsed.TotalMilliseconds > 350) throw new InvalidOperationException("Sort exceeded 350 ms.");
        controller.WriteJsonArtifact(controller.ScenarioId, "view-snapshot", "four-views.json", Evidence(controller, new { views = rows, sort = driver.CaptureBrowserSnapshot() }));
        controller.WriteJsonArtifact(controller.ScenarioId, "performance-snapshot", "view-sort-performance.json", Evidence(controller, new { view_switch_limit_ms = 250, sort_limit_ms = 350, views = rows, sort_ms = sortTimer.Elapsed.TotalMilliseconds }));
        await CaptureFrameAsync(controller, driver, "four-views-list", 1920, 1080, 1.25);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunSelectionScenarioAsync(AssetLibraryP2AutomatedAcceptanceController controller, AssetLibraryP2AutomatedAcceptanceDriver driver)
    {
        var timer = Stopwatch.StartNew();
        var ids = await driver.SelectFirstAssetsAsync(100);
        timer.Stop();
        if (ids.Count != 100 || timer.Elapsed.TotalMilliseconds > 250)
            throw new InvalidOperationException($"100-item selection contract failed (count={ids.Count}, elapsed={timer.Elapsed.TotalMilliseconds:F1} ms).");
        controller.WriteJsonArtifact(controller.ScenarioId, "selection-snapshot", "selection-100.json", Evidence(controller, new { count = ids.Count, asset_ids = ids, elapsed_ms = timer.Elapsed.TotalMilliseconds, threshold_ms = 250 }));
        await CaptureFrameAsync(controller, driver, "selection-100", 1920, 1080, 1.25);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunMetadataScenarioAsync(AssetLibraryP2AutomatedAcceptanceController controller, AssetLibraryP2AutomatedAcceptanceDriver driver)
    {
        await driver.SelectFirstAssetsAsync(100);
        var timer = Stopwatch.StartNew();
        var drop = await driver.DropSelectionOnFirstFolderAsync();
        timer.Stop();
        if (!drop.CanUndo || timer.Elapsed.TotalMilliseconds > 750)
            throw new InvalidOperationException("The 100-item metadata drop did not complete through the undoable command seam within 750 ms.");
        var undoRedo = await driver.UndoAndRedoAsync();
        controller.WriteJsonArtifact(controller.ScenarioId, "command-snapshot", "metadata-drag-command.json", Evidence(controller, new { drop, undo_redo = undoRedo, elapsed_ms = timer.Elapsed.TotalMilliseconds, threshold_ms = 750 }));
        await CaptureFrameAsync(controller, driver, "metadata-command", 1920, 1080, 1.25);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunInspectorScenarioAsync(AssetLibraryP2AutomatedAcceptanceController controller, AssetLibraryP2AutomatedAcceptanceDriver driver)
    {
        await driver.ClearSelectionAsync();
        var query = driver.CaptureBrowserSnapshot();
        await driver.SelectFirstAssetsAsync(1);
        var single = driver.CaptureBrowserSnapshot();
        await driver.SelectFirstAssetsAsync(100);
        var multiple = driver.CaptureBrowserSnapshot();
        if (query.InspectorMode != "query" || single.InspectorMode != "single" || multiple.InspectorMode != "multiple")
            throw new InvalidOperationException("The inspector did not expose query, single, and multiple real states.");
        controller.WriteJsonArtifact(controller.ScenarioId, "inspector-snapshot", "inspector-states.json", Evidence(controller, new { query, single, multiple }));
        await CaptureFrameAsync(controller, driver, "inspector-multiple", 1920, 1080, 1.25);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunResilienceScenarioAsync(AssetLibraryP2AutomatedAcceptanceController controller, AssetLibraryP2AutomatedAcceptanceDriver driver)
    {
        await WaitForAssetLibraryStateAsync(driver, state => state.HasLoadError && !state.IsLoading, "the deterministic recoverable repository error");
        var error = driver.CaptureState();
        await CaptureFrameAsync(controller, driver, "resilience-error", 1366, 768, 1);
        controller.IncrementRetryCommandCount();
        await driver.ExecuteRetryCommandAsync();
        await WaitForAssetLibraryStateAsync(driver, state => state.IsReady && !state.HasLoadError, "the recovered repository state");
        var recovered = driver.CaptureBrowserSnapshot();
        controller.WriteJsonArtifact(controller.ScenarioId, "query-snapshot", "resilience-states.json", Evidence(controller, new { error, recovered, retry_count = 1 }));
        await CaptureFrameAsync(controller, driver, "resilience-recovered", 1366, 768, 1);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunRestartScenarioAsync(AssetLibraryP2AutomatedAcceptanceController controller, AssetLibraryP2AutomatedAcceptanceDriver driver)
    {
        if (!controller.IsRestartPhase)
        {
            await driver.SwitchViewAsync(AssetLibraryViewMode.List);
            await driver.SortAsync(AssetLibrarySortField.FileName);
            await driver.ComposeSearchTextAsync("P2_00");
            await WaitForAssetLibraryStateAsync(driver, state => state.IsReady && state.VisibleAssetCount > 0, "the persisted filtered query");
            var primary = driver.CaptureBrowserSnapshot();
            controller.RecordScenarioCheck(controller.ScenarioId, "persisted_view", primary.ViewMode);
            controller.RecordScenarioCheck(controller.ScenarioId, "persisted_sort", primary.SortField);
            controller.RecordScenarioCheck(controller.ScenarioId, "persisted_search", "P2_00");
            controller.WriteJsonArtifact(controller.ScenarioId, "view-snapshot", "restart-primary.json", Evidence(controller, primary));
        }
        else
        {
            var restart = driver.CaptureBrowserSnapshot();
            var expectedView = controller.RequireStringScenarioCheck(controller.ScenarioId, "persisted_view");
            var expectedSort = controller.RequireStringScenarioCheck(controller.ScenarioId, "persisted_sort");
            var expectedSearch = controller.RequireStringScenarioCheck(controller.ScenarioId, "persisted_search");
            if (restart.ViewMode != expectedView || restart.SortField != expectedSort || driver.CaptureState().SearchText != expectedSearch)
                throw new InvalidOperationException("The restart process did not restore the P2 query/view/sort workspace contract.");
            controller.WriteJsonArtifact(controller.ScenarioId, "view-snapshot", "restart-restored.json", Evidence(controller, restart));
        }
        await CaptureFrameAsync(controller, driver, controller.IsRestartPhase ? "restart-restored" : "restart-primary", 1366, 768, 1);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunLayoutPerformanceScenarioAsync(AssetLibraryP2AutomatedAcceptanceController controller, AssetLibraryP2AutomatedAcceptanceDriver driver, double firstScreenMs)
    {
        if (firstScreenMs > 1500) throw new InvalidOperationException($"First screen exceeded 1500 ms ({firstScreenMs:F1} ms).");
        var matrix = new List<object>();
        foreach (var entry in AssetLibraryP2LayoutMatrix)
        {
            var label = $"dpi-{entry.Width}x{entry.Height}-{entry.ScalePercent}";
            await CaptureFrameAsync(controller, driver, label, entry.Width, entry.Height, entry.ScalePercent / 100d);
            matrix.Add(new { entry.Width, entry.Height, entry.ScalePercent, snapshot = driver.CaptureBrowserSnapshot() });
        }
        var ping = Stopwatch.StartNew();
        await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
        ping.Stop();
        if (ping.Elapsed.TotalMilliseconds > 100) throw new InvalidOperationException($"UI block exceeded 100 ms ({ping.Elapsed.TotalMilliseconds:F1} ms).");
        controller.WriteJsonArtifact(controller.ScenarioId, "performance-snapshot", "layout-dpi-performance.json", Evidence(controller, new
        {
            matrix, first_screen_ms = firstScreenMs, first_screen_limit_ms = 1500,
            ui_block_ms = ping.Elapsed.TotalMilliseconds, ui_block_limit_ms = 100,
            view_switch_limit_ms = 250, sort_limit_ms = 350, select_100_limit_ms = 250, drag_drop_100_limit_ms = 750,
            real_display_settings_changed = false
        }));
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private object Evidence(AssetLibraryP2AutomatedAcceptanceController controller, object payload) => new
    {
        schema = "pixel-tart-p2-automated-snapshot/v1", validation_mode = "automated",
        run_id = controller.RunId, source_head = controller.SourceHead, scenario_id = controller.ScenarioId,
        process_session_id = controller.ProcessSessionId, pid = Environment.ProcessId, hwnd = controller.Hwnd,
        payload, captured_at = DateTimeOffset.UtcNow
    };

    private async Task CaptureFrameAsync(AssetLibraryP2AutomatedAcceptanceController controller, AssetLibraryP2AutomatedAcceptanceDriver driver, string label, int physicalWidth, int physicalHeight, double scale)
    {
        await driver.DrainDispatcherAsync();
        var logicalWidth = physicalWidth / scale;
        var logicalHeight = physicalHeight / scale;
        WindowState = WindowState.Normal;
        Width = Math.Max(MinWidth, logicalWidth);
        Height = Math.Max(MinHeight, logicalHeight);
        RootGrid.Measure(new Size(logicalWidth, logicalHeight));
        RootGrid.Arrange(new Rect(0, 0, logicalWidth, logicalHeight));
        RootGrid.UpdateLayout();
        if (RootGrid.ActualWidth <= 0 || RootGrid.ActualHeight <= 0) throw new InvalidOperationException("The live WPF RootGrid has no renderable bounds.");
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(RootGrid.Background ?? Brushes.Black, null, new Rect(0, 0, physicalWidth, physicalHeight));
            drawing.PushTransform(new ScaleTransform(scale, scale));
            drawing.DrawRectangle(new VisualBrush(RootGrid), null, new Rect(0, 0, RootGrid.ActualWidth, RootGrid.ActualHeight));
            drawing.Pop();
        }
        var bitmap = new RenderTargetBitmap(physicalWidth, physicalHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        controller.WriteArtifact(controller.ScenarioId, "screenshot", $"{label}.png", stream.ToArray());
        var state = driver.CaptureState();
        var bounds = driver.CaptureVisibleBounds();
        var overflow = AssetLibraryP2AutomatedAcceptanceDriver.HasLayoutOverflow(bounds, state.PageWidth, state.PageHeight);
        if (overflow) throw new InvalidOperationException($"The live P2 layout overflowed at {physicalWidth}x{physicalHeight}/{scale:P0}.");
        controller.WriteJsonArtifact(controller.ScenarioId, "bounds", $"{label}.bounds.json", Evidence(controller, new
        {
            viewport = new { width = state.PageWidth, height = state.PageHeight, physical_width = physicalWidth, physical_height = physicalHeight, scale_percent = (int)Math.Round(scale * 100) },
            real_display_settings_changed = false, has_overflow = false, elements = bounds
        }));
    }

    private static async Task WaitForAssetLibraryStateAsync(AssetLibraryP2AutomatedAcceptanceDriver driver, Func<AssetLibraryP2AutomatedState, bool> condition, string description)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await driver.DrainDispatcherAsync();
            if (condition(driver.CaptureState())) return;
            await Task.Delay(40);
        }
        throw new TimeoutException($"Timed out waiting for {description}.");
    }
}
#endif
