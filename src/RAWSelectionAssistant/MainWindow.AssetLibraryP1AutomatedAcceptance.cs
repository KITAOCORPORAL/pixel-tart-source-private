#if ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant;

public partial class MainWindow
{
    private static readonly (int Width, int Height, int ScalePercent)[] AssetLibraryP1LayoutMatrix =
    [
        (1366, 768, 100),
        (1920, 1080, 125),
        (1920, 1080, 150),
        (2560, 1440, 175),
    ];

    private AssetLibraryP1AutomatedAcceptanceController? _assetLibraryP1AutomatedController;
    private AssetLibraryP1AutomatedAcceptanceDriver? _assetLibraryP1AutomatedDriver;
    private PixelTart.Modules.AssetLibrary.AssetLibraryPage? _assetLibraryP1AutomatedPage;

    internal void ConfigureAssetLibraryP1AutomatedAcceptance(AssetLibraryP1AutomatedAcceptanceController controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        if (_assetLibraryP1AutomatedController is not null)
            throw new InvalidOperationException("The live window may host only one P1 automated acceptance controller.");
        _assetLibraryP1AutomatedController = controller;
        Loaded += AssetLibraryP1AutomatedAcceptance_Loaded;
        Closed += (_, _) => _assetLibraryP1AutomatedDriver?.Dispose();
    }

    private async void AssetLibraryP1AutomatedAcceptance_Loaded(object sender, RoutedEventArgs e)
    {
        var controller = _assetLibraryP1AutomatedController
            ?? throw new InvalidOperationException("The P1 automated acceptance controller is unavailable.");
        try
        {
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
            if (_viewModel is null)
                throw new InvalidOperationException("The live MainWindow has no MainViewModel.");
            if (AssetLibraryWorkspace.Content is not PixelTart.Modules.AssetLibrary.AssetLibraryPage page)
                throw new InvalidOperationException("The live Asset Library module page did not materialize.");
            _assetLibraryP1AutomatedPage = page;
            var driver = new AssetLibraryP1AutomatedAcceptanceDriver(page);
            _assetLibraryP1AutomatedDriver = driver;
            controller.Observe(_viewModel, driver);
            await ExecuteAssetLibraryP1AutomatedScenarioAsync(controller, driver);
            controller.MarkExecutionCompleted();
            await TeardownAssetLibraryP1AutomatedAcceptanceAsync();
            Close();
        }
        catch (Exception exception)
        {
            controller.Fail(exception);
            try
            {
                await TeardownAssetLibraryP1AutomatedAcceptanceAsync();
            }
            catch (Exception teardownException)
            {
                controller.Fail(new AggregateException("The automated Asset Library page teardown failed.", exception, teardownException));
            }
            Application.Current.Shutdown(-1);
        }
    }

    internal async Task TeardownAssetLibraryP1AutomatedAcceptanceAsync()
    {
        _assetLibraryP1AutomatedDriver?.Dispose();
        _assetLibraryP1AutomatedDriver = null;

        var page = _assetLibraryP1AutomatedPage ?? AssetLibraryWorkspace.Content as PixelTart.Modules.AssetLibrary.AssetLibraryPage;
        if (page is null) return;
        _assetLibraryP1AutomatedPage = page;
        await page.DisposeAsync();
    }

    private async Task ExecuteAssetLibraryP1AutomatedScenarioAsync(
        AssetLibraryP1AutomatedAcceptanceController controller,
        AssetLibraryP1AutomatedAcceptanceDriver driver)
    {
        var scenarioId = controller.ScenarioId;
        controller.SetActiveScenario(scenarioId);
        switch (scenarioId)
        {
            case AssetLibraryP1AutomatedAcceptanceController.FirstEmptyScenario:
                await CaptureAssetLibraryP1AutomatedFrameAsync(controller, driver, scenarioId, "loading", 1366, 768, 1);
                controller.ReleaseInitialLoadingBarrier();
                await WaitForAssetLibraryStateAsync(driver, state => state.IsReady && state.EmptyStateVisible, "real first-empty ready state");
                var firstEmpty = driver.CaptureState();
                await CaptureAssetLibraryP1AutomatedFrameAsync(controller, driver, scenarioId, "first-empty", 1366, 768, 1);
                controller.RecordScenarioCheck(scenarioId, "first_empty_state", firstEmpty);
                controller.RecordScenarioCheck(scenarioId, "attempt", firstEmpty.LoadAttempt);
                controller.RecordScenarioCheck(scenarioId, "final_state", "ready");
                controller.RecordScenarioCheck(scenarioId, "asset_count", firstEmpty.VisibleAssetCount);
                controller.RecordScenarioCheck(scenarioId, "empty_state", firstEmpty.EmptyStateVisible);
                controller.MarkScenarioCompleted(scenarioId);
                break;

            case AssetLibraryP1AutomatedAcceptanceController.RetryScenario:
                await CaptureAssetLibraryP1AutomatedFrameAsync(controller, driver, scenarioId, "loading", 1366, 768, 1);
                controller.ReleaseInitialLoadingBarrier();
                await WaitForAssetLibraryStateAsync(driver, state => state.HasLoadError && state.LoadAttempt == 1, "recoverable error state");
                var attemptOneError = driver.CaptureState();
                await CaptureAssetLibraryP1AutomatedFrameAsync(controller, driver, scenarioId, "error", 1366, 768, 1);
                var beforeRetry = driver.CaptureState();
                controller.IncrementRetryCommandCount();
                await driver.ExecuteRetryCommandAsync();
                await WaitForAssetLibraryStateAsync(driver, state => state.IsReady && state.LoadAttempt == 2, "single Retry recovery");
                var afterRetry = driver.CaptureState();
                controller.RecordAction(scenarioId, "retry-command", "RetryAssetLibraryLoad", beforeRetry, afterRetry, afterRetry);
                controller.RecordScenarioCheck(scenarioId, "attempt1_state", attemptOneError.HasLoadError ? "error" : "invalid");
                controller.RecordScenarioCheck(scenarioId, "attempt2_repository_query", afterRetry.LoadAttempt == 2 && afterRetry.IsReady);
                controller.RecordScenarioCheck(scenarioId, "attempt2_final_state", afterRetry.IsReady ? "ready" : "invalid");
                controller.RecordScenarioCheck(scenarioId, "asset_count", afterRetry.VisibleAssetCount);
                await CaptureAssetLibraryP1AutomatedFrameAsync(controller, driver, scenarioId, "recovered", 1366, 768, 1);
                controller.MarkScenarioCompleted(scenarioId);
                break;

            default:
                await WaitForAssetLibraryStateAsync(driver, state => state.IsReady, "real repository ready state");
                if (controller.ScenarioId == AssetLibraryP1AutomatedAcceptanceController.SelectionScenario &&
                    !controller.IsRestartPhase)
                    await EnsureSyntheticFixtureImportedAsync(controller, driver);
                await ExecuteReadyAssetLibraryScenarioAsync(controller, driver);
                break;
        }
    }

    private async Task ExecuteReadyAssetLibraryScenarioAsync(
        AssetLibraryP1AutomatedAcceptanceController controller,
        AssetLibraryP1AutomatedAcceptanceDriver driver)
    {
        var scenarioId = controller.ScenarioId;
        switch (scenarioId)
        {
            case AssetLibraryP1AutomatedAcceptanceController.OrganizationSplitterScenario:
                await ExerciseSplitterAsync(controller, driver, scenarioId, organization: true);
                break;
            case AssetLibraryP1AutomatedAcceptanceController.InspectorSplitterScenario:
                await ExerciseSplitterAsync(controller, driver, scenarioId, organization: false);
                break;
            case AssetLibraryP1AutomatedAcceptanceController.CollapseScenario:
            {
                if (controller.IsRestartPhase)
                {
                    var restoredCollapsed = driver.CaptureState();
                    if (!restoredCollapsed.OrganizationCollapsed || !restoredCollapsed.InspectorCollapsed)
                        throw new InvalidOperationException("The collapsed pane state did not restore after process restart.");
                    await driver.ToggleOrganizationPaneAsync();
                    await driver.ToggleInspectorPaneAsync();
                    var expanded = driver.CaptureState();
                    controller.RecordAction(scenarioId, "pane-state-restored-after-process-restart", "ToggleAssetOrganizationPane|ToggleAssetInspectorPane", restoredCollapsed, expanded, expanded);
                    controller.RecordScenarioCheck(scenarioId, "same_pane_state_after_restart", true);
                    controller.RecordScenarioCheck(scenarioId, "same_persisted_state_after_restart", true);
                    await CaptureAssetLibraryP1AutomatedFrameAsync(controller, driver, scenarioId, "restart-expanded", 1366, 768, 1);
                    controller.MarkScenarioCompleted(scenarioId);
                    break;
                }
                var original = driver.CaptureState();
                await driver.ToggleOrganizationPaneAsync();
                var organizationCollapsed = driver.CaptureState();
                await driver.ToggleInspectorPaneAsync();
                var inspectorCollapsed = driver.CaptureState();
                if (!organizationCollapsed.OrganizationCollapsed || !inspectorCollapsed.InspectorCollapsed)
                    throw new InvalidOperationException("The real bound pane buttons did not persist both collapsed states.");
                controller.RecordAction(scenarioId, "pane-collapse-through-bound-buttons", "ToggleAssetOrganizationPane|ToggleAssetInspectorPane", original, new { organizationCollapsed, inspectorCollapsed }, inspectorCollapsed);
                controller.RecordScenarioCheck(scenarioId, "persisted_pane_state", "organization-collapsed|inspector-collapsed");
                await CaptureAssetLibraryP1AutomatedFrameAsync(controller, driver, scenarioId, "primary-collapsed", 1366, 768, 1);
                controller.MarkScenarioCompleted(scenarioId);
                break;
            }
            case AssetLibraryP1AutomatedAcceptanceController.ThumbnailScenario:
            {
                if (controller.IsRestartPhase)
                {
                    var restored = driver.CaptureState();
                    var expectedWidth = controller.RequireDoubleScenarioCheck(scenarioId, "persisted_thumbnail_width");
                    if (restored.ThumbnailPersistedWidth < restored.ThumbnailSliderMinimum ||
                        restored.ThumbnailPersistedWidth > restored.ThumbnailSliderMaximum ||
                        Math.Abs(restored.ThumbnailPersistedWidth - expectedWidth) > 0.01)
                        throw new InvalidOperationException("The thumbnail width did not restore to the primary persisted value after process restart.");
                    controller.RecordAction(scenarioId, "thumbnail-width-restored-after-process-restart", "AssetThumbnailSizeSlider", null, restored, restored.ThumbnailPersistedWidth);
                    controller.RecordScenarioCheck(scenarioId, "same_thumbnail_width_after_restart", true);
                    controller.RecordScenarioCheck(scenarioId, "same_persisted_state_after_restart", true);
                    await CaptureAssetLibraryP1AutomatedFrameAsync(controller, driver, scenarioId, "restart-restored", 1366, 768, 1);
                    controller.MarkScenarioCompleted(scenarioId);
                    break;
                }
                var before = driver.CaptureState();
                await driver.AdjustThumbnailByKeyboardAsync(Key.Right);
                var after = driver.CaptureState();
                if (before.ThumbnailPersistedWidth < before.ThumbnailSliderMinimum ||
                    after.ThumbnailPersistedWidth <= before.ThumbnailPersistedWidth ||
                    after.ThumbnailPersistedWidth > after.ThumbnailSliderMaximum)
                    throw new InvalidOperationException("The real thumbnail slider did not increase through its keyboard route.");
                controller.RecordAction(scenarioId, "thumbnail-slider-keyboard-route", "AssetThumbnailSizeSlider", before, after, after.ThumbnailPersistedWidth);
                controller.RecordScenarioCheck(scenarioId, "persisted_thumbnail_width", after.ThumbnailPersistedWidth);
                controller.RecordScenarioCheck(scenarioId, "thumbnail_live_range", new
                {
                    minimum = after.ThumbnailSliderMinimum,
                    maximum = after.ThumbnailSliderMaximum,
                    before = before.ThumbnailPersistedWidth,
                    after = after.ThumbnailPersistedWidth,
                    keyboard_increase_count = 1,
                });
                await CaptureAssetLibraryP1AutomatedFrameAsync(controller, driver, scenarioId, "slider-adjusted", 1366, 768, 1);
                controller.MarkScenarioCompleted(scenarioId);
                break;
            }
            case AssetLibraryP1AutomatedAcceptanceController.SelectionScenario:
                await ExecuteSelectionScenarioAsync(controller, driver);
                break;
            case AssetLibraryP1AutomatedAcceptanceController.NavigationImeScenario:
                await ExecuteNavigationAndCompositionScenarioAsync(controller, driver);
                break;
            case AssetLibraryP1AutomatedAcceptanceController.LayoutDpiButtonsScenario:
                await ExecuteLayoutMatrixScenarioAsync(controller, driver);
                break;
            default:
                throw new InvalidOperationException($"Unsupported P1 automated scenario '{scenarioId}'.");
        }
    }

    private async Task EnsureSyntheticFixtureImportedAsync(
        AssetLibraryP1AutomatedAcceptanceController controller,
        AssetLibraryP1AutomatedAcceptanceDriver driver)
    {
        var fixtureRoot = controller.FixtureRoot
            ?? throw new InvalidOperationException("This P1 automated scenario requires an isolated synthetic fixture root.");
        var before = driver.CaptureState();
        await driver.ImportSyntheticFixtureAsync(fixtureRoot);
        await WaitForAssetLibraryStateAsync(driver, state => state.IsReady && state.VisibleAssetCount > 0, "synthetic fixture import");
        var after = driver.CaptureState();
        controller.RecordImport("public-application-import-seam", after.VisibleAssetCount, fixtureRoot);
        controller.RecordAction(controller.ScenarioId, "synthetic-fixture-import-completed", "AssetLibraryPage", before, after, after.VisibleAssetCount);
    }

    private async Task ExerciseSplitterAsync(
        AssetLibraryP1AutomatedAcceptanceController controller,
        AssetLibraryP1AutomatedAcceptanceDriver driver,
        string scenarioId,
        bool organization)
    {
        var automationId = organization ? "AssetOrganizationSplitter" : "AssetInspectorSplitter";
        static double Width(AssetLibraryP1AutomatedState state, bool isOrganization) =>
            isOrganization ? state.OrganizationPersistedWidth : state.InspectorPersistedWidth;

        var beforeMiddle = driver.CaptureState();
        if (organization) await driver.DragOrganizationSplitterAsync(36);
        else await driver.DragInspectorSplitterAsync(-36);
        var middle = driver.CaptureState();
        if (Math.Abs(Width(middle, organization) - Width(beforeMiddle, organization)) < 0.01)
            throw new InvalidOperationException("The real splitter middle drag did not change its persisted width.");
        controller.RecordAction(scenarioId, "splitter-middle", automationId, beforeMiddle, middle, Width(middle, organization));

        if (organization) await driver.DragOrganizationSplitterAsync(-10000);
        else await driver.DragInspectorSplitterAsync(10000);
        var minimum = driver.CaptureState();
        if (Width(minimum, organization) >= Width(middle, organization))
            throw new InvalidOperationException("The real splitter did not reach its minimum boundary.");
        controller.RecordAction(scenarioId, "splitter-minimum", automationId, middle, minimum, Width(minimum, organization));

        if (organization) await driver.DragOrganizationSplitterAsync(-10000);
        else await driver.DragInspectorSplitterAsync(10000);
        var boundaryNoOp = driver.CaptureState();
        if (Math.Abs(Width(boundaryNoOp, organization) - Width(minimum, organization)) > 0.01)
            throw new InvalidOperationException("The real splitter changed when driven beyond its minimum boundary.");
        controller.RecordAction(scenarioId, "splitter-boundary-no-op", automationId, minimum, boundaryNoOp, Width(boundaryNoOp, organization));

        if (organization) await driver.DragOrganizationSplitterAsync(10000);
        else await driver.DragInspectorSplitterAsync(-10000);
        var maximum = driver.CaptureState();
        if (Width(maximum, organization) <= Width(minimum, organization))
            throw new InvalidOperationException("The real splitter did not reach its maximum boundary.");
        controller.RecordAction(scenarioId, "splitter-maximum", automationId, boundaryNoOp, maximum, Width(maximum, organization));

        if (organization) await driver.AdjustOrganizationSplitterByKeyboardAsync(Key.Left);
        else await driver.AdjustInspectorSplitterByKeyboardAsync(Key.Right);
        var decreased = driver.CaptureState();
        if (Width(decreased, organization) >= Width(maximum, organization))
            throw new InvalidOperationException("The real splitter keyboard decrease route did not decrease its persisted width.");
        controller.RecordAction(scenarioId, "splitter-decrease", automationId, maximum, decreased, Width(decreased, organization));

        if (organization) await driver.AdjustOrganizationSplitterByKeyboardAsync(Key.Right);
        else await driver.AdjustInspectorSplitterByKeyboardAsync(Key.Left);
        var increased = driver.CaptureState();
        if (Width(increased, organization) <= Width(decreased, organization))
            throw new InvalidOperationException("The real splitter keyboard increase route did not increase its persisted width.");
        controller.RecordAction(scenarioId, "splitter-increase", automationId, decreased, increased, Width(increased, organization));
        controller.RecordScenarioCheck(scenarioId, "persisted_splitter_width", Width(increased, organization));
        await CaptureAssetLibraryP1AutomatedFrameAsync(controller, driver, scenarioId, "boundaries", 1920, 1080, 1.25);
        controller.MarkScenarioCompleted(scenarioId);
    }

    private async Task ExecuteSelectionScenarioAsync(
        AssetLibraryP1AutomatedAcceptanceController controller,
        AssetLibraryP1AutomatedAcceptanceDriver driver)
    {
        if (controller.IsRestartPhase)
        {
            var restored = driver.CaptureState();
            if (restored.SelectedAssetIds.Count != 1)
                throw new InvalidOperationException("The real Asset Grid did not restore exactly one persisted selection after restart.");
            var expectedAssetId = controller.RequireStringScenarioCheck(controller.ScenarioId, "selected_asset_id");
            if (!string.Equals(restored.SelectedAssetIds.Single(), expectedAssetId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The real Asset Grid restored a different asset selection after process restart.");
            controller.RecordAction(controller.ScenarioId, "selection-restored-after-real-process-restart", "AssetGrid", null, restored, restored.SelectedAssetIds);
            controller.RecordScenarioCheck(controller.ScenarioId, "same_selection_after_restart", true);
            controller.RecordScenarioCheck(controller.ScenarioId, "same_persisted_state_after_restart", true);
            await CaptureAssetLibraryP1AutomatedFrameAsync(controller, driver, controller.ScenarioId, "restart-restored", 1366, 768, 1);
            controller.MarkScenarioCompleted(controller.ScenarioId);
            return;
        }

        var before = driver.CaptureState();
        var selectedId = await driver.SelectFirstAssetAsync();
        var selected = driver.CaptureState();
        if (_viewModel is null) throw new InvalidOperationException("The live MainViewModel is unavailable.");
        _viewModel.NavigateCommand.Execute(PrimaryNavigationPolicy.Workbench);
        await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
        if (!_viewModel.NavigateCommand.CanExecute(AssetLibraryP1AutomatedAcceptanceController.AssetLibraryRoute))
            throw new InvalidOperationException("The Asset Library primary route alias cannot execute.");
        _viewModel.NavigateCommand.Execute(AssetLibraryP1AutomatedAcceptanceController.AssetLibraryRoute);
        await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
        if (!string.Equals(_viewModel.CurrentPage, PrimaryNavigationPolicy.AssetLibrary, StringComparison.Ordinal))
            throw new InvalidOperationException("The Asset Library route alias did not resolve to the live Asset Library page.");
        var returned = driver.CaptureState();
        if (!returned.SelectedAssetIds.Contains(selectedId, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selection did not survive a real primary navigation round trip.");
        controller.RecordAction(controller.ScenarioId, "selection-persisted-before-process-restart", "AssetGrid", before, new { selected, returned }, selectedId);
        controller.RecordScenarioCheck(controller.ScenarioId, "selected_asset_id", selectedId);
        controller.RecordScenarioCheck(controller.ScenarioId, "same_selection_after_route_return", true);
        await CaptureAssetLibraryP1AutomatedFrameAsync(controller, driver, controller.ScenarioId, "primary-selected", 1366, 768, 1);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task ExecuteNavigationAndCompositionScenarioAsync(
        AssetLibraryP1AutomatedAcceptanceController controller,
        AssetLibraryP1AutomatedAcceptanceDriver driver)
    {
        if (_viewModel is null) throw new InvalidOperationException("The live MainViewModel is unavailable.");
        var observedRoutes = new List<string>();
        foreach (var route in PrimaryNavigationPolicy.OrderedPages)
        {
            if (!_viewModel.NavigateCommand.CanExecute(route))
                throw new InvalidOperationException($"The live primary navigation command cannot execute '{route}'.");
            _viewModel.NavigateCommand.Execute(route);
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
            if (!string.Equals(_viewModel.CurrentPage, route, StringComparison.Ordinal))
                throw new InvalidOperationException($"The live primary navigation route '{route}' did not become current.");
            observedRoutes.Add(_viewModel.CurrentPage);
            controller.RecordAction(controller.ScenarioId, "primary-navigation-command", "PrimaryNavigation", null, route, _viewModel.CurrentPage);
        }
        if (!_viewModel.NavigateCommand.CanExecute(AssetLibraryP1AutomatedAcceptanceController.AssetLibraryRoute))
            throw new InvalidOperationException("The Asset Library primary route alias cannot execute.");
        _viewModel.NavigateCommand.Execute(AssetLibraryP1AutomatedAcceptanceController.AssetLibraryRoute);
        await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
        if (!string.Equals(_viewModel.CurrentPage, PrimaryNavigationPolicy.AssetLibrary, StringComparison.Ordinal))
            throw new InvalidOperationException("The Asset Library route alias did not resolve to the live Asset Library page.");
        await driver.ComposeSearchTextAsync("像素蛋挞素材");
        var composed = driver.CaptureState();
        await driver.ClearSearchThroughEditingCommandAsync();
        var cleared = driver.CaptureState();
        controller.RecordAction(controller.ScenarioId, "wpf-text-composition-and-editing-command", "AssetLibrarySearch", null, composed, cleared);
        var contractRoutes = observedRoutes
            .Select(route => string.Equals(route, PrimaryNavigationPolicy.AssetLibrary, StringComparison.Ordinal)
                ? AssetLibraryP1AutomatedAcceptanceController.AssetLibraryRoute
                : route)
            .ToArray();
        var searchClearedAndReturned = string.Equals(_viewModel.CurrentPage, PrimaryNavigationPolicy.AssetLibrary, StringComparison.Ordinal) &&
                                       composed.SearchText == "像素蛋挞素材" &&
                                       cleared.SearchText.Length == 0;
        if (!searchClearedAndReturned)
            throw new InvalidOperationException("The real navigation, Chinese text-composition, clear, and return route did not complete.");
        controller.RecordScenarioCheck(controller.ScenarioId, "ordered_primary_routes", observedRoutes);
        controller.RecordScenarioCheck(controller.ScenarioId, "routes", contractRoutes);
        controller.RecordScenarioCheck(controller.ScenarioId, "asset_library_route_alias", AssetLibraryP1AutomatedAcceptanceController.AssetLibraryRoute);
        controller.RecordScenarioCheck(controller.ScenarioId, "chinese_ime_control_path", true);
        controller.RecordScenarioCheck(controller.ScenarioId, "search_cleared_and_returned", true);
        await CaptureAssetLibraryP1AutomatedFrameAsync(controller, driver, controller.ScenarioId, "navigation-ime", 1366, 768, 1);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task ExecuteLayoutMatrixScenarioAsync(
        AssetLibraryP1AutomatedAcceptanceController controller,
        AssetLibraryP1AutomatedAcceptanceDriver driver)
    {
        foreach (var (width, height, scalePercent) in AssetLibraryP1LayoutMatrix)
        {
            var scale = scalePercent / 100d;
            var label = $"{width}x{height}-{scale:0.00}";
            await CaptureAssetLibraryP1AutomatedFrameAsync(controller, driver, controller.ScenarioId, label, width, height, scale);
        }
        var visibleButtons = driver.CaptureRealizedButtons();
        if (visibleButtons.Count == 0)
            throw new InvalidOperationException("The live Asset Library layout did not realize any WPF buttons.");

        var buttonMatrix = new List<AssetLibraryP1AutomatedButtonReadabilityState>(27 * 5 * 3);
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var themeIndex = dictionaries
            .Select((dictionary, index) => new { dictionary, index })
            .FirstOrDefault(item => item.dictionary.Source?.OriginalString.Contains("DesignSystem/Theme.", StringComparison.OrdinalIgnoreCase) == true)
            ?.index ?? -1;
        if (themeIndex < 0)
            throw new InvalidOperationException("The live application has no replaceable design-system theme dictionary.");
        var originalTheme = dictionaries[themeIndex];
        try
        {
            foreach (var (name, fileName) in new[]
                     {
                         ("dark", "Dark"),
                         ("light", "Light"),
                         ("high-contrast", "HighContrast"),
                     })
            {
                dictionaries[themeIndex] = new ResourceDictionary
                {
                    Source = new Uri($"Resources/DesignSystem/Theme.{fileName}.xaml", UriKind.Relative),
                };
                RootGrid.InvalidateVisual();
                await driver.DrainDispatcherAsync();
                buttonMatrix.AddRange(driver.CaptureButtonReadabilityMatrix(name));
            }
        }
        finally
        {
            dictionaries[themeIndex] = originalTheme;
            RootGrid.InvalidateVisual();
            await driver.DrainDispatcherAsync();
        }

        var buttonCount = buttonMatrix.Select(item => item.ButtonIdentity).Distinct(StringComparer.Ordinal).Count();
        if (buttonCount != 27 || buttonMatrix.Count != 27 * 5 * 3)
            throw new InvalidOperationException($"The live WPF button matrix is incomplete: {buttonCount} buttons, {buttonMatrix.Count} state records.");
        if (buttonMatrix.Any(item => !item.LiveWpfButtonInstance || !item.SourceDeclarationProbe || !item.TemplateApplied))
            throw new InvalidOperationException("The live WPF button matrix contains an unrealized, unbound, or unapplied-template probe.");
        var minimumTextContrast = buttonMatrix.Where(item => item.TextContrastApplicable).Min(item => item.TextContrast!.Value);
        var minimumNonTextContrast = buttonMatrix.Where(item => item.NonTextContrastApplicable).Min(item => item.NonTextContrast!.Value);
        var focusVisibleAllThemes = buttonMatrix
            .Where(item => string.Equals(item.State, "focus", StringComparison.Ordinal))
            .All(item => item.FocusVisible);

        controller.RecordScenarioCheck(controller.ScenarioId, "matrix_kind", "simulated-layout-dpi");
        controller.RecordScenarioCheck(controller.ScenarioId, "real_display_settings_changed", false);
        controller.RecordScenarioCheck(controller.ScenarioId, "realized_button_count", buttonCount);
        controller.RecordScenarioCheck(controller.ScenarioId, "visible_button_instance_count", visibleButtons.Count);
        controller.RecordScenarioCheck(controller.ScenarioId, "visible_button_instances", visibleButtons);
        controller.RecordScenarioCheck(controller.ScenarioId, "button_state_matrix_schema", "pixel-tart-p1-live-wpf-button-state-matrix/v1");
        controller.RecordScenarioCheck(controller.ScenarioId, "button_state_record_count", buttonMatrix.Count);
        controller.RecordScenarioCheck(controller.ScenarioId, "button_state_matrix", buttonMatrix);
        controller.RecordScenarioCheck(controller.ScenarioId, "minimum_text_contrast", minimumTextContrast);
        controller.RecordScenarioCheck(controller.ScenarioId, "minimum_non_text_contrast", minimumNonTextContrast);
        controller.RecordScenarioCheck(controller.ScenarioId, "focus_visible_all_themes", focusVisibleAllThemes);
        controller.RecordScenarioCheck(controller.ScenarioId, "theme_application_mode", "ephemeral-live-resource-dictionary-restored");
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task CaptureAssetLibraryP1AutomatedFrameAsync(
        AssetLibraryP1AutomatedAcceptanceController controller,
        AssetLibraryP1AutomatedAcceptanceDriver driver,
        string scenarioId,
        string label,
        int physicalWidth,
        int physicalHeight,
        double scale)
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
        if (RootGrid.ActualWidth <= 0 || RootGrid.ActualHeight <= 0)
            throw new InvalidOperationException("The live WPF RootGrid has no renderable bounds.");

        var composition = new DrawingVisual();
        using (var drawing = composition.RenderOpen())
        {
            drawing.DrawRectangle(RootGrid.Background ?? Brushes.Black, null, new Rect(0, 0, physicalWidth, physicalHeight));
            drawing.PushTransform(new ScaleTransform(scale, scale));
            drawing.DrawRectangle(new VisualBrush(RootGrid), null, new Rect(0, 0, RootGrid.ActualWidth, RootGrid.ActualHeight));
            drawing.Pop();
        }
        var bitmap = new RenderTargetBitmap(physicalWidth, physicalHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(composition);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        controller.WriteArtifact(scenarioId, "screenshot", $"{label}.png", stream.ToArray());

        var bounds = driver.CaptureVisibleBounds();
        var pageState = driver.CaptureState();
        var viewport = new { width = pageState.PageWidth, height = pageState.PageHeight, physical_width = physicalWidth, physical_height = physicalHeight, scale, scale_percent = (int)Math.Round(scale * 100) };
        var simulatedLayout = new { width = physicalWidth, height = physicalHeight, scale_percent = (int)Math.Round(scale * 100) };
        var hasOverflow = bounds.Any(item =>
            item.X < -0.01 ||
            item.Y < -0.01 ||
            item.X + item.Width > pageState.PageWidth + 0.01 ||
            item.Y + item.Height > pageState.PageHeight + 0.01 ||
            item.Clipped ||
            item.Overlapped);
        controller.WriteJsonArtifact(
            scenarioId,
            "bounds",
            $"{label}.bounds.json",
            new
            {
                schema = "pixel-tart-p1-automated-bounds/v1",
                validation_mode = "automated",
                owner_manual_ux_smoke = "waived",
                manual_evidence_claimed = false,
                automated_capture_status = "captured",
                historical_manual_gate = AssetLibraryP1AutomatedAcceptanceController.HistoricalManualGate,
                run_id = controller.RunId,
                source_head = controller.SourceHead,
                scenario_id = scenarioId,
                scenario_root = controller.ScenarioRoot,
                phase = controller.Phase,
                process_session_id = controller.ProcessSessionId,
                pid = Environment.ProcessId,
                hwnd = controller.Hwnd,
                executable_path = controller.ExecutablePath,
                executable_sha256 = controller.ExecutableSha256,
                asset_module_path = controller.AssetModulePath,
                asset_module_sha256 = controller.AssetModuleSha256,
                real_display_settings_changed = false,
                viewport,
                simulated_layout = simulatedLayout,
                elements = bounds.Select(item => new
                {
                    identity = item.Identity,
                    element_type = item.ElementType,
                    parent_identity = item.ParentIdentity,
                    depth = item.Depth,
                    visibility = item.Visibility,
                    x = item.X,
                    y = item.Y,
                    width = item.Width,
                    height = item.Height,
                    visible_rect = new
                    {
                        x = item.VisibleX,
                        y = item.VisibleY,
                        width = item.VisibleWidth,
                        height = item.VisibleHeight,
                    },
                    item.IsEnabled,
                    item.Focusable,
                    must_fit = item.MustFit,
                    clipped = item.Clipped,
                    overlapped = item.Overlapped,
                }).ToArray(),
                has_overflow = hasOverflow,
                captured_at = DateTimeOffset.UtcNow,
            });
    }

    private static async Task WaitForAssetLibraryStateAsync(
        AssetLibraryP1AutomatedAcceptanceDriver driver,
        Func<AssetLibraryP1AutomatedState, bool> condition,
        string description)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await driver.DrainDispatcherAsync();
            if (condition(driver.CaptureState())) return;
            await Task.Delay(50);
        }
        throw new TimeoutException($"Timed out waiting for {description}.");
    }
}
#endif
