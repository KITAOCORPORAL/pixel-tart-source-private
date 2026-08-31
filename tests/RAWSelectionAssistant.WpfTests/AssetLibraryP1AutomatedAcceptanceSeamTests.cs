using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP1AutomatedAcceptanceSeamTests
{
    private static readonly string[] ExpectedScenarioOrder =
    [
        "first-empty/v1",
        "loading-error-retry-recovered/v1",
        "organization-splitter/v1",
        "inspector-splitter/v1",
        "pane-collapse-expand/v1",
        "thumbnail-slider/v1",
        "selection-navigation-restart/v1",
        "navigation-ime/v1",
        "layout-dpi-buttons/v1",
    ];

    private static readonly (int Width, int Height, int ScalePercent)[] ExpectedDpiMatrix =
    [
        (1366, 768, 100),
        (1920, 1080, 125),
        (1920, 1080, 150),
        (2560, 1440, 175),
    ];

    private static readonly string[] ExpectedRestartScenarios =
    [
        "pane-collapse-expand/v1",
        "thumbnail-slider/v1",
        "selection-navigation-restart/v1",
    ];

    [TestMethod]
    public void AutomatedBuildFlagIsRestrictedToDebugDevPreviewAndRejectedByProductBuilds()
    {
        var appProject = XDocument.Load(Path("src/RAWSelectionAssistant/RAWSelectionAssistant.csproj"));
        var moduleProject = XDocument.Load(Path("src/PixelTart.Modules.AssetLibrary/PixelTart.Modules.AssetLibrary.csproj"));

        AssertCompileGuard(appProject, "RAWSelectionAssistant");
        AssertCompileGuard(moduleProject, "PixelTart.Modules.AssetLibrary");
        var appGuard = appProject.Descendants("DefineConstants").Single(element =>
            element.Value.Contains("ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE", StringComparison.Ordinal));
        var moduleGuard = moduleProject.Descendants("DefineConstants").Single(element =>
            element.Value.Contains("ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE", StringComparison.Ordinal));
        Assert.AreEqual(Attribute(appGuard, "Condition"), Attribute(moduleGuard, "Condition"));

        var validationTarget = appProject.Descendants("Target").Single(element =>
            Attribute(element, "Name") == "ValidateAssetLibraryP1AutomatedAcceptanceBuild");
        Assert.AreEqual("PrepareForBuild", Attribute(validationTarget, "BeforeTargets"));
        StringAssert.Contains(Attribute(validationTarget, "Condition"), "'$(AssetLibraryP1AutomatedAcceptance)' == 'true'");

        var errors = validationTarget.Descendants("Error")
            .Select(error => (Condition: Attribute(error, "Condition"), Text: Attribute(error, "Text")))
            .ToArray();
        Assert.IsTrue(errors.Any(error => error.Condition.Contains("'$(ModularHarnessDevPreview)' != 'true'", StringComparison.Ordinal)));
        Assert.IsTrue(errors.Any(error => error.Condition.Contains("'$(AcceptanceBuild)' == 'true'", StringComparison.Ordinal)));
        Assert.IsTrue(errors.Any(error => error.Condition.Contains("'$(Configuration)' != 'Debug'", StringComparison.Ordinal)));
        Assert.IsTrue(errors.Any(error => error.Condition.Contains("'$(AssetLibraryP1StateAcceptance)' == 'true'", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void AutomatedRunnerNeverEnablesTheHistoricalManualStateSeam()
    {
        var runner = Text("tools/AssetLibraryP1AutomatedAcceptance/Invoke-P1AssetLibraryAutomatedAcceptance.ps1");

        ContainsAll(
            runner,
            "-p:ModularHarnessDevPreview=true",
            "-p:InputRoutingDiagnostics=true",
            "-p:AssetLibraryP1AutomatedAcceptance=true",
            "Assert-CleanCommit",
            "Assert-NoDevPreview",
            "Invoke-DryRun",
            "ready-for-automated-run");
        Assert.DoesNotContain("-p:AssetLibraryP1StateAcceptance=true", runner, StringComparison.Ordinal);
        Assert.IsFalse(
            new Regex("PIXEL_TART_ASSET_LIBRARY_P1_STATE_ACCEPTANCE\\s*=\\s*['\"](?:1|true)['\"]", RegexOptions.IgnoreCase)
                .IsMatch(runner));
    }

    [TestMethod]
    public void RuntimeRequiresAbsoluteRunPlanAndRuntimeRootsWithBoundHeadAndProcessIdentity()
    {
        var runtime = AutomatedRuntimeSource();

        ContainsAll(
            runtime,
            "PIXEL_TART_P1_AUTOMATED_ACCEPTANCE",
            "PIXEL_TART_P1_AUTOMATED_RUN_ROOT",
            "PIXEL_TART_P1_AUTOMATED_PLAN_PATH",
            "PIXEL_TART_P1_AUTOMATED_SOURCE_HEAD",
            "PIXEL_TART_ACCEPTANCE_ROOT",
            "Path.IsPathFullyQualified",
            "Path.GetFullPath",
            "Path.GetRelativePath",
            "PixelTart_ModularHarness_V1_DevPreview",
            "pixel-tart-p1-automated-plan/v2",
            "scenario_ids",
            "run_id");
        Assert.IsTrue(
            runtime.Contains("Environment.ProcessId", StringComparison.Ordinal) ||
            runtime.Contains("Process.GetCurrentProcess()", StringComparison.Ordinal),
            "Runtime evidence must bind itself to the actual application process.");
        Assert.IsTrue(
            runtime.Contains("^[0-9a-f]{40}$", StringComparison.Ordinal) ||
            runtime.Contains("{40}", StringComparison.Ordinal),
            "The runtime must reject malformed or non-lowercase source HEAD values.");
        ContainsAll(
            runtime,
            "ExpectedProcessName",
            "string.Equals(processName, ExpectedProcessName, StringComparison.Ordinal)");
    }

    [TestMethod]
    public void AutomatedContractHasFourHonestyMarkersAndExactlyNineOrderedScenarios()
    {
        using var contract = JsonDocument.Parse(Text(
            "tools/AssetLibraryP1AutomatedAcceptance/automated-acceptance-contract.json"));
        var root = contract.RootElement;

        Assert.AreEqual("automated", root.GetProperty("validation_mode").GetString());
        Assert.AreEqual("waived", root.GetProperty("owner_manual_ux_smoke").GetString());
        Assert.IsFalse(root.GetProperty("manual_evidence_claimed").GetBoolean());
        Assert.AreEqual("captured", root.GetProperty("automated_capture_status").GetString());
        var historicalGate = root.TryGetProperty("historical_manual_gate_a", out var gateA)
            ? gateA
            : root.GetProperty("historical_manual_gate");
        Assert.AreEqual("not_closed_superseded_as_release_blocker", historicalGate.GetString());

        CollectionAssert.AreEqual(
            ExpectedScenarioOrder,
            root.GetProperty("required_scenario_order").EnumerateArray().Select(item => item.GetString()).ToArray());

        var dpi = root.GetProperty("required_dpi_matrix").EnumerateArray()
            .Select(item => (
                item.GetProperty("width").GetInt32(),
                item.GetProperty("height").GetInt32(),
                item.GetProperty("scale_percent").GetInt32()))
            .ToArray();
        CollectionAssert.AreEqual(ExpectedDpiMatrix, dpi);

        var readability = root.GetProperty("button_readability");
        Assert.AreEqual(27, readability.GetProperty("required_button_count").GetInt32());
        CollectionAssert.AreEqual(
            new[] { "normal", "hover", "pressed", "focus", "disabled" },
            readability.GetProperty("required_states").EnumerateArray().Select(item => item.GetString()).ToArray());
        CollectionAssert.AreEqual(
            new[] { "dark", "light", "high-contrast" },
            readability.GetProperty("required_themes").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.AreEqual(4.5, readability.GetProperty("minimum_text_contrast").GetDouble());
        Assert.AreEqual(3.0, readability.GetProperty("minimum_non_text_contrast").GetDouble());
    }

    [TestMethod]
    public void RuntimeImplementsEveryFixedScenarioInsteadOfGeneratingGenericPassEvidence()
    {
        var runtime = AutomatedRuntimeSource();
        foreach (var scenario in ExpectedScenarioOrder) StringAssert.Contains(runtime, scenario);

        ContainsAll(
            runtime,
            "SqliteAssetLibraryRepository",
            "SELECT MAX(Version) FROM AssetLibrarySchemaInfo;",
            "RetryAssetLibraryLoad",
            "AssetOrganizationSplitter",
            "AssetInspectorSplitter",
            "ToggleAssetOrganizationPane",
            "ToggleAssetInspectorPane",
            "AssetThumbnailSizeSlider",
            "AssetGrid",
            "AssetLibrarySearch",
            "NavigateCommand",
            "CaptureState",
            "CaptureVisibleBounds",
            "CaptureRealizedButtons");

        StringAssert.Contains(runtime, "PrimaryNavigationPolicy.OrderedPages");
        var policy = Text("src/RAWSelectionAssistant.Core/Models/AssetLibraryWorkspaceSettings.cs");
        foreach (var route in new[]
                 {
                     "Workbench", "AssetLibrary", "Workflow", "WorkCalendar", "Tether", "Finance", "History",
                 })
            StringAssert.Contains(policy, $"public const string {route} = \"{route}\"");
        var orderedPages = Slice(policy, "public static IReadOnlyList<string> OrderedPages", "public static bool IsPrimaryPage");
        foreach (var route in new[]
                 {
                     "Workbench", "AssetLibrary", "Workflow", "WorkCalendar", "Tether", "Finance", "History",
                 })
            StringAssert.Contains(orderedPages, route);
    }

    [TestMethod]
    public void NineScenarioImplementationsMeasureRealTransitionsBeforeRecordingTheirChecks()
    {
        var window = Text("src/RAWSelectionAssistant/MainWindow.AssetLibraryP1AutomatedAcceptance.cs");

        var firstEmpty = Slice(
            window,
            "case AssetLibraryP1AutomatedAcceptanceController.FirstEmptyScenario:",
            "case AssetLibraryP1AutomatedAcceptanceController.RetryScenario:");
        ContainsAll(
            firstEmpty,
            "ReleaseInitialLoadingBarrier",
            "state.IsReady",
            "state.EmptyStateVisible",
            "first_empty_state");

        var retry = Slice(
            window,
            "case AssetLibraryP1AutomatedAcceptanceController.RetryScenario:",
            "default:");
        ContainsAll(
            retry,
            "state.HasLoadError",
            "state.LoadAttempt == 1",
            "IncrementRetryCommandCount",
            "ExecuteRetryCommandAsync",
            "state.IsReady && state.LoadAttempt == 2",
            "\"retry-command\"",
            "RetryAssetLibraryLoad");

        var splitter = Slice(
            window,
            "private async Task ExerciseSplitterAsync",
            "private async Task ExecuteSelectionScenarioAsync");
        foreach (var transition in new[] { "middle", "minimum", "maximum", "boundary-no-op", "decrease", "increase" })
            StringAssert.Contains(splitter, $"\"splitter-{transition}\"");
        ContainsAll(
            splitter,
            "DragOrganizationSplitterAsync",
            "DragInspectorSplitterAsync",
            "AdjustOrganizationSplitterByKeyboardAsync",
            "AdjustInspectorSplitterByKeyboardAsync",
            "persisted_splitter_width",
            "throw new InvalidOperationException");

        var readyScenarios = Slice(
            window,
            "private async Task ExecuteReadyAssetLibraryScenarioAsync",
            "private async Task EnsureSyntheticFixtureImportedAsync");
        Assert.HasCount(2, Regex.Matches(readyScenarios, @"driver\.ToggleOrganizationPaneAsync\s*\(", RegexOptions.CultureInvariant));
        Assert.HasCount(2, Regex.Matches(readyScenarios, @"driver\.ToggleInspectorPaneAsync\s*\(", RegexOptions.CultureInvariant));
        ContainsAll(
            readyScenarios,
            "restoredCollapsed.OrganizationCollapsed",
            "restoredCollapsed.InspectorCollapsed",
            "same_pane_state_after_restart",
            "AdjustThumbnailByKeyboardAsync(Key.Right)",
            "after.ThumbnailPersistedWidth <= before.ThumbnailPersistedWidth",
            "RequireDoubleScenarioCheck",
            "same_thumbnail_width_after_restart");

        var selection = Slice(
            window,
            "private async Task ExecuteSelectionScenarioAsync",
            "private async Task ExecuteNavigationAndCompositionScenarioAsync");
        ContainsAll(
            selection,
            "SelectFirstAssetAsync",
            "PrimaryNavigationPolicy.Workbench",
            "PrimaryNavigationPolicy.AssetLibrary",
            "same_selection_after_route_return",
            "RequireStringScenarioCheck",
            "same_selection_after_restart");

        var navigation = Slice(
            window,
            "private async Task ExecuteNavigationAndCompositionScenarioAsync",
            "private async Task ExecuteLayoutMatrixScenarioAsync");
        ContainsAll(
            navigation,
            "PrimaryNavigationPolicy.OrderedPages",
            "NavigateCommand.CanExecute",
            "NavigateCommand.Execute",
            "ComposeSearchTextAsync",
            "ClearSearchThroughEditingCommandAsync",
            "AssetLibraryRoute",
            "routes",
            "chinese_ime_control_path",
            "search_cleared_and_returned");

        var layout = Slice(
            window,
            "private async Task ExecuteLayoutMatrixScenarioAsync",
            "private async Task CaptureAssetLibraryP1AutomatedFrameAsync");
        ContainsAll(
            layout,
            "AssetLibraryP1LayoutMatrix",
            "CaptureRealizedButtons",
            "simulated-layout-dpi",
            "real_display_settings_changed");
    }

    [TestMethod]
    public void AutomatedControllerIsWiredIntoRealApplicationWindowModuleAndExitLifecycles()
    {
        var app = Text("src/RAWSelectionAssistant/App.xaml.cs");
        ContainsAll(
            app,
            "#if ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE",
            "AssetLibraryP1AutomatedAcceptanceController.TryCreate(AppDataPaths.Root",
            "_assetLibraryP1AutomatedController?.ApplyStartRoute(_mainViewModel)",
            "_assetLibraryP1AutomatedController.BindWindow(window)",
            "window.ConfigureAssetLibraryP1AutomatedAcceptance(_assetLibraryP1AutomatedController)",
            "assetLibraryP1StateController = _assetLibraryP1AutomatedController",
            "enableAssetLibraryPreview = false",
            "assetLibraryDemoDirectory = null",
            "e.ApplicationExitCode",
            "_assetLibraryP1AutomatedController?.FinalizeOnApplicationExit(");

        var mainWindow = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(Path("src/RAWSelectionAssistant"), "MainWindow*.cs", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(File.ReadAllText));
        ContainsAll(
            mainWindow,
            "ConfigureAssetLibraryP1AutomatedAcceptance",
            "new AssetLibraryP1AutomatedAcceptanceDriver",
            ".Observe(",
            ".ReleaseInitialLoadingBarrier(",
            ".MarkExecutionCompleted(",
            "Application.Current.Shutdown");
    }

    [TestMethod]
    public void PublicSeamUsesRealWpfRoutesAndExecutesEachBoundButtonCommandOnlyOnce()
    {
        var driver = Text(
            "src/PixelTart.Modules.AssetLibrary/AssetLibraryP1AutomatedAcceptanceDriver.cs");
        ContainsAll(
            driver,
            "#if ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE",
            "public sealed class AssetLibraryP1AutomatedAcceptanceDriver",
            "AssetLibraryPage page",
            "new DragStartedEventArgs",
            "Thumb.DragStartedEvent",
            "new DragDeltaEventArgs",
            "Thumb.DragDeltaEvent",
            "new DragCompletedEventArgs",
            "Thumb.DragCompletedEvent",
            "new KeyEventArgs",
            "Keyboard.PreviewKeyDownEvent",
            "Keyboard.KeyDownEvent",
            "Keyboard.PreviewKeyUpEvent",
            "Keyboard.KeyUpEvent",
            "TextCompositionManager.StartComposition",
            "EditingCommands.Delete.Execute",
            "Selector.SelectionChangedEvent",
            "AutomationProperties.GetAutomationId");

        var commandHelper = Slice(
            driver,
            "private async Task ExecuteBoundButtonCommandAsync",
            "private async Task RaiseSplitterDragAsync");
        StringAssert.Contains(commandHelper, "await RaiseKeyboardRouteAsync(button, Key.Space)");
        Assert.DoesNotContain(
            "command.Execute(",
            commandHelper,
            StringComparison.Ordinal,
            "Button.Click already invokes the bound WPF command; a second Execute would make Retry run twice.");

        Assert.DoesNotContain("_viewModel.RetryLoadCommand.Execute", driver, StringComparison.Ordinal);
        Assert.DoesNotContain("_viewModel.ToggleOrganizationPaneCommand.Execute", driver, StringComparison.Ordinal);
        Assert.DoesNotContain("_viewModel.ToggleInspectorPaneCommand.Execute", driver, StringComparison.Ordinal);
    }

    [TestMethod]
    public void AutomatedSourcesContainNoReflectionDesktopInjectionUiAutomationInvokeOrDirectStateBypass()
    {
        var automatedCSharp = AutomatedCSharpSource();
        var forbiddenTokens = new[]
        {
            "System.Reflection",
            "BindingFlags.",
            "GetField(",
            "GetFields(",
            "GetMethod(",
            "GetMethods(",
            "AutomationElement",
            "InvokePattern",
            "GetCurrentPattern",
            "SendInput(",
            "SetCursorPos(",
            "SetForegroundWindow",
            "SwitchToThisWindow",
            "ChangeDisplaySettings",
            "ChangeDisplaySettingsEx",
            "SetDisplayConfig",
            "SetProcessDpiAwareness",
            "INSERT INTO AssetItems",
            "UPDATE AssetItems",
            "DELETE FROM AssetItems",
            "_viewModel.OrganizationPaneWidth =",
            "_viewModel.InspectorPaneWidth =",
            "_viewModel.ThumbnailWidth =",
        };
        foreach (var forbidden in forbiddenTokens)
            Assert.DoesNotContain(forbidden, automatedCSharp, StringComparison.OrdinalIgnoreCase, forbidden);

        foreach (var forbiddenCall in new[] { "mouse_event", "keybd_event" })
            Assert.IsFalse(
                Regex.IsMatch(automatedCSharp, $@"\b{Regex.Escape(forbiddenCall)}\s*\(", RegexOptions.IgnoreCase),
                forbiddenCall);

        Assert.IsFalse(
            new Regex(@"ColumnDefinitions\s*\[[^\]]+\]\s*\.Width\s*=", RegexOptions.IgnoreCase).IsMatch(automatedCSharp),
            "The driver must not substitute direct Grid column mutation for the routed splitter path.");
        Assert.IsFalse(
            new Regex(@"(?:workspace|app|assetLibrary)Settings\s*\.\s*(?:OrganizationPaneWidth|InspectorPaneWidth|ThumbnailWidth)\s*=", RegexOptions.IgnoreCase)
                .IsMatch(automatedCSharp),
            "The driver must not write settings directly.");
    }

    [TestMethod]
    public void EvidenceUsesLiveWpfRenderingBoundsAndFourExplicitlySimulatedDpiLayouts()
    {
        var runtime = AutomatedRuntimeSource();
        ContainsAll(
            runtime,
            "RenderTargetBitmap",
            "PngBitmapEncoder",
            "CaptureVisibleBounds",
            "CaptureRealizedButtons",
            "visibleBounds.IsEmpty",
            "visibleBounds.Width <= 0.5",
            "visibleBounds.Height <= 0.5",
            "simulated_layout",
            "real_display_settings_changed",
            "bounds",
            "screenshot");

        foreach (var item in ExpectedDpiMatrix)
        {
            StringAssert.Contains(runtime, item.Width.ToString(System.Globalization.CultureInfo.InvariantCulture));
            StringAssert.Contains(runtime, item.Height.ToString(System.Globalization.CultureInfo.InvariantCulture));
            StringAssert.Contains(runtime, item.ScalePercent.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        Assert.DoesNotContain("ChangeDisplaySettings", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SetDisplayConfig", runtime, StringComparison.OrdinalIgnoreCase);

        var captureMethod = Slice(
            runtime,
            "private async Task CaptureAssetLibraryP1AutomatedFrameAsync",
            "private static async Task WaitForAssetLibraryStateAsync");
        ContainsAll(
            captureMethod,
            "controller.RunId",
            "controller.SourceHead",
            "controller.ExecutableSha256",
            "controller.AssetModuleSha256",
            "simulated_layout",
            "width = physicalWidth",
            "height = physicalHeight",
            "scale_percent");
        Assert.IsFalse(
            Regex.IsMatch(captureMethod, @"\bclipped\s*=\s*false\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            "Clipping evidence must come from live WPF geometry, not a constant false declaration.");
        Assert.IsFalse(
            Regex.IsMatch(captureMethod, @"\boverlapped\s*=\s*false\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            "Overlap evidence must come from live WPF geometry, not a constant false declaration.");
    }

    [TestMethod]
    public void ThreePersistenceScenariosRestartTheSameIsolatedRootAcrossDifferentPidAndHwnd()
    {
        var runner = Text("tools/AssetLibraryP1AutomatedAcceptance/Invoke-P1AssetLibraryAutomatedAcceptance.ps1");
        var validator = Text("tools/AssetLibraryP1AutomatedAcceptance/Test-P1AssetLibraryAutomatedEvidence.ps1");
        var controller = Text("src/RAWSelectionAssistant/Services/AssetLibraryP1AutomatedAcceptanceController.cs");
        var window = Text("src/RAWSelectionAssistant/MainWindow.AssetLibraryP1AutomatedAcceptance.cs");

        ContainsAll(
            runner,
            "runtime\\$scenarioDirectory",
            "scenario_root = $scenarioRoot",
            "Invoke-AppPhase 'primary'",
            "Invoke-AppPhase 'restart'");
        foreach (var scenario in ExpectedRestartScenarios)
            StringAssert.Contains(runner, scenario);
        var restartList = Slice(runner, "$restartScenarios = @(", "foreach ($restartScenario in $restartScenarios)");
        Assert.HasCount(
            3,
            Regex.Matches(restartList, "['\"][^'\"]+/v1['\"]", RegexOptions.CultureInvariant),
            "Exactly the collapse, thumbnail, and selection persistence scenarios must be in the restart list.");
        var restartLoop = Slice(runner, "foreach ($restartScenario in $restartScenarios)", "$displayAfter");
        ContainsAll(restartLoop, "Invoke-AppPhase 'restart'", "@($restartScenario)");

        ContainsAll(
            validator,
            "restart_hwnd",
            "restart PID",
            "restart HWND",
            "same_selection_after_restart");
        Assert.IsTrue(
            Regex.IsMatch(
                validator,
                @"\$restartPid\s*-gt\s*0\s*-and\s*\$restartPid\s*-ne\s*\$scenarioPid",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            "The validator must require every restart process id to be positive and different from its primary process id.");
        Assert.IsTrue(
            validator.Contains("restart HWND was reused", StringComparison.OrdinalIgnoreCase) &&
            validator.Contains("StringComparison]::OrdinalIgnoreCase", StringComparison.OrdinalIgnoreCase),
            "The validator must reject a restart that reuses the primary window handle.");
        StringAssert.Contains(validator, "scenario root differs");
        foreach (var scenario in ExpectedRestartScenarios)
            StringAssert.Contains(validator, scenario);

        ContainsAll(
            controller,
            "CollapseScenario",
            "ThumbnailScenario",
            "SelectionScenario",
            "IsRestartPhase",
            "priorScenarioRoot",
            "_primaryPid",
            "Environment.ProcessId");
        foreach (var scenario in ExpectedRestartScenarios)
            StringAssert.Contains(controller, scenario);

        ContainsAll(
            window,
            "same_pane_state_after_restart",
            "same_thumbnail_width_after_restart",
            "same_selection_after_restart");

        var boundsPhaseValidation = Slice(
            validator,
            "foreach($relative in $boundsRefs)",
            "$database=Value $scenario 'database'");
        ContainsAll(
            boundsPhaseValidation,
            "artifactMap",
            "phase",
            "restart_pid",
            "restart_hwnd");

        var databaseValidation = Slice(
            validator,
            "$database=Value $scenario 'database'",
            "$imports=@(");
        StringAssert.Contains(
            databaseValidation,
            "evidence_paths",
            "Restart scenarios retain both primary and restart database snapshots; every indexed snapshot must be referenced and validated.");
    }

    [TestMethod]
    public void OnlySelectionPrimaryMayImportTheRunOwnedSyntheticFixture()
    {
        var window = Text("src/RAWSelectionAssistant/MainWindow.AssetLibraryP1AutomatedAcceptance.cs");
        var controller = Text("src/RAWSelectionAssistant/Services/AssetLibraryP1AutomatedAcceptanceController.cs");
        var validator = Text("tools/AssetLibraryP1AutomatedAcceptance/Test-P1AssetLibraryAutomatedEvidence.ps1");

        var scenarioDispatch = Slice(
            window,
            "private async Task ExecuteAssetLibraryP1AutomatedScenarioAsync",
            "private async Task ExecuteReadyAssetLibraryScenarioAsync");
        Assert.HasCount(
            1,
            Regex.Matches(scenarioDispatch, @"EnsureSyntheticFixtureImportedAsync\s*\(", RegexOptions.CultureInvariant),
            "The synthetic import route must appear only in the selection primary branch.");
        var guardedImport = Regex.Match(
            scenarioDispatch,
            @"if\s*\((?<condition>[\s\S]*?)\)\s*await\s+EnsureSyntheticFixtureImportedAsync",
            RegexOptions.CultureInvariant);
        Assert.IsTrue(guardedImport.Success, "The synthetic import must be guarded immediately at its only call site.");
        var importCondition = guardedImport.Groups["condition"].Value;
        ContainsAll(importCondition, "SelectionScenario", "IsRestartPhase");
        Assert.IsTrue(
            Regex.IsMatch(importCondition, @"!\s*controller\.IsRestartPhase", RegexOptions.CultureInvariant),
            "The only import call must reject restart phases regardless of condition ordering.");

        var importMethod = Slice(controller, "internal void RecordImport", "internal ArtifactState WriteArtifact");
        ContainsAll(
            importMethod,
            "SelectionScenario",
            "IsRestartPhase",
            "throw new InvalidOperationException",
            "source_kind = \"synthetic-run-fixture\"",
            "synthetic = true",
            "application_import_route = true",
            "user_source = false");

        ContainsAll(
            validator,
            "selection-navigation-restart/v1",
            "Synthetic import count differs",
            "import contamination",
            "synthetic-run-fixture",
            "user_source");
    }

    [TestMethod]
    public void EventAndSummaryStreamsAreCreateNewAppendOnlyAndIndependentlyHashChained()
    {
        var runtime = AutomatedRuntimeSource();
        var validator = Text("tools/AssetLibraryP1AutomatedAcceptance/Test-P1AssetLibraryAutomatedEvidence.ps1");

        ContainsAll(
            runtime,
            "events.ndjson",
            "summary.ndjson",
            "previous_event_hash",
            "event_hash",
            "previous_summary_hash",
            "summary_hash",
            "File.AppendAllText");
        Assert.IsGreaterThanOrEqualTo(
            2,
            Regex.Matches(runtime, @"File\.AppendAllText\s*\(", RegexOptions.CultureInvariant).Count,
            "Both the event and summary NDJSON journals must use append-only writes.");
        Assert.IsFalse(
            new Regex(@"(?:events|summary)\.ndjson[^\r\n]*(?:WriteAllText|Create\b|Truncate)", RegexOptions.IgnoreCase)
                .IsMatch(runtime),
            "Append-only evidence streams must never be overwritten or truncated.");

        ContainsAll(
            validator,
            "events.ndjson",
            "summary.ndjson",
            "previous_event_hash",
            "event_hash",
            "previous_summary_hash",
            "summary_hash");
        Assert.IsTrue(
            validator.Contains("Get-Sha256", StringComparison.Ordinal) ||
            validator.Contains("SHA256", StringComparison.Ordinal),
            "The validator must recompute the chain instead of trusting the driver result.");

        var summaryJournalValidation = Slice(
            validator,
            "$summaryJournalPath=",
            "$lastSequence=0");
        ContainsAll(
            summaryJournalValidation,
            "run_id",
            "source_head",
            "scenario_id",
            "scenario_root",
            "phase",
            "pid",
            "hwnd",
            "executable_sha256",
            "application_sha256",
            "asset_module_sha256");
    }

    [TestMethod]
    public void ApplicationAndRunnerSafetyCleanupEvidenceAreValidatedAtTheirRealLifecycleBoundaries()
    {
        using var contract = JsonDocument.Parse(Text(
            "tools/AssetLibraryP1AutomatedAcceptance/automated-acceptance-contract.json"));
        var safetyFields = contract.RootElement.GetProperty("safety_zero_fields").EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        var controller = Text("src/RAWSelectionAssistant/Services/AssetLibraryP1AutomatedAcceptanceController.cs");
        var runner = Text("tools/AssetLibraryP1AutomatedAcceptance/Invoke-P1AssetLibraryAutomatedAcceptance.ps1");
        var validator = Text("tools/AssetLibraryP1AutomatedAcceptance/Test-P1AssetLibraryAutomatedEvidence.ps1");

        foreach (var field in safetyFields)
        {
            StringAssert.Contains(controller, field, $"Application summary safety is missing contract field '{field}'.");
            StringAssert.Contains(runner, field, $"Runner manifest safety is missing contract field '{field}'.");
        }

        ContainsAll(
            controller,
            "shutdown_requested",
            "application_exit_hook_reached",
            "residual_process_check_owner",
            "database_wal_present",
            "database_shm_present");
        ContainsAll(
            validator,
            "Value $summary 'process_cleanup'",
            "shutdown_requested",
            "application_exit_hook_reached",
            "residual_process_check_owner",
            "database_wal_present",
            "database_shm_present",
            "Value $manifest 'process_cleanup'",
            "devpreview_get_process_count_after",
            "devpreview_cim_count_after",
            "dotnet_residual_pid_count",
            "db_sidecar_count_after",
            "environment_residual_count",
            "display_settings_unchanged");
        Assert.IsFalse(
            Regex.IsMatch(
                validator,
                @"foreach\s*\(\s*\$\w+\s+in\s+@\(\s*\$summary\s*,\s*\$manifest\s*\)\s*\)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            "The application cannot prove its own post-exit process cleanup; only the independent runner manifest can.");
    }

    [TestMethod]
    public void IndependentValidatorFailClosesAllRequiredIdentitySafetyAndMutationNegatives()
    {
        using var contract = JsonDocument.Parse(Text(
            "tools/AssetLibraryP1AutomatedAcceptance/automated-acceptance-contract.json"));
        var root = contract.RootElement;
        var negatives = root.GetProperty("required_negative_fixtures").EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        CollectionAssert.AreEqual(
            new[]
            {
                "missing-screenshot",
                "mutated-hash",
                "wrong-scenario-order",
                "retry-twice",
                "direct-width-mutation",
                "direct-settings-mutation",
                "cross-run-splice",
                "wrong-pid-or-hwnd",
                "dll-hash-mismatch",
                "database-not-v6",
                "import-contamination",
                "uncleared-process",
                "dpi-overflow",
                "runner-session-splice",
                "process-session-splice",
                "pre-cleanup-audit-hash",
                "cleanup-path-splice",
                "build-log-hash",
                "sealed-binary-mutation",
                "application-hash-mismatch",
                "sealed-application-mutation",
                "sealed-dependency-mutation",
                "binary-tree-manifest-mismatch",
            },
            negatives);

        var validator = Text("tools/AssetLibraryP1AutomatedAcceptance/Test-P1AssetLibraryAutomatedEvidence.ps1");
        ContainsAll(
            validator,
            "manual_evidence_claimed",
            "automated_capture_status",
            "required_scenario_order",
            "RetryAssetLibraryLoad",
            "safety_zero_fields",
            "direct_mutation",
            "run_id",
            "pid",
            "hwnd",
            "executable_sha256",
            "application_sha256",
            "asset_module_sha256",
            "schema_version",
            "SQLite format 3",
            "imports",
            "devpreview_get_process_count_after",
            "devpreview_cim_count_after",
            "db_sidecar_count_after",
            "required_dpi_matrix");
    }

    [TestMethod]
    public void IndependentValidatorReauditsAllButtonRolesStatesContrastsAndThemesFromSource()
    {
        var validator = Text("tools/AssetLibraryP1AutomatedAcceptance/Test-P1AssetLibraryAutomatedEvidence.ps1");
        var sourceAudit = Slice(
            validator,
            "function Test-AssetLibraryButtonReadabilitySource",
            "try {");

        ContainsAll(
            validator,
            "AssetLibraryPage.xaml",
            "Theme.Dark.xaml",
            "Theme.Light.xaml",
            "Theme.HighContrast.xaml",
            "AssetLibraryPrimaryButton",
            "AssetLibrarySecondaryButton",
            "AssetLibraryChipButton",
            "AssetLibraryIconButton",
            "AssetLibraryPaletteSwatchButton",
            "IsMouseOver",
            "IsPressed",
            "IsKeyboardFocused",
            "IsEnabled",
            "4.5",
            "3.0",
            "SHA256");
        Assert.IsTrue(
            validator.Contains("Get-ContrastRatio", StringComparison.Ordinal) ||
            validator.Contains("Contrast-Ratio", StringComparison.Ordinal) ||
            validator.Contains("relative luminance", StringComparison.OrdinalIgnoreCase),
            "The independent validator must calculate contrast from XAML colors, not trust driver summary ratios.");
        Assert.IsTrue(
            Regex.IsMatch(
                validator,
                "(?:Descendants|SelectNodes|Select-Xml|\\.Button\\b|LocalName\\s*-eq\\s*['\"]Button['\"])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            "The validator must enumerate the 27 real XAML Button elements itself.");
        Assert.IsTrue(
            Regex.IsMatch(validator, @"(?:Count\s*-eq\s*27|Same\s+\$[^\r\n]*Count\s+27)", RegexOptions.IgnoreCase),
            "The independent source audit must require exactly 27 Asset Library buttons.");

        Assert.IsTrue(
            Regex.IsMatch(
                validator,
                @"Test-AssetLibraryButtonReadabilitySource\s+(?:\(|\$)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            "The validator must actually invoke its independent source audit; declaring a helper is insufficient.");

        var themeLoopIndex = sourceAudit.LastIndexOf("foreach($themePath", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, themeLoopIndex, "The independent audit must inspect every declared theme source.");
        var themeAudit = sourceAudit[themeLoopIndex..];
        ContainsAll(
            themeAudit,
            "ContentBackgroundBrush",
            "WorkbenchCardBrush",
            "AssetLibraryButtonFocusOuterColor",
            "AssetLibraryButtonFocusInnerColor",
            "SystemColors.WindowColor",
            "3.0");
        Assert.IsTrue(
            themeAudit.Contains("Get-ContrastRatio", StringComparison.Ordinal) ||
            themeAudit.Contains("Get-RelativeLuminance", StringComparison.Ordinal) ||
            themeAudit.Contains("relative luminance", StringComparison.OrdinalIgnoreCase),
            "Theme names alone are not an audit: dark, light, and high-contrast focus visibility must be calculated independently.");
        Assert.IsTrue(
            themeAudit.Contains("[Math]::Max", StringComparison.Ordinal) ||
            themeAudit.Contains("[Math]::Min", StringComparison.Ordinal),
            "The two complementary focus rails must be evaluated against each theme surface.");
    }

    [TestMethod]
    public void EveryAutomatedManifestEvidenceScreenshotAndBoundsRecordCarriesHonestIdentity()
    {
        var contract = Text("tools/AssetLibraryP1AutomatedAcceptance/automated-acceptance-contract.json");
        var runtime = AutomatedRuntimeSource();
        var validator = Text("tools/AssetLibraryP1AutomatedAcceptance/Test-P1AssetLibraryAutomatedEvidence.ps1");

        foreach (var text in new[] { contract, runtime, validator })
            ContainsAll(
                text,
                "validation_mode",
                "owner_manual_ux_smoke",
                "manual_evidence_claimed",
                "automated_capture_status");

        ContainsAll(
            runtime,
            "run_id",
            "source_head",
            "pid",
            "hwnd",
            "executable_sha256",
            "application_sha256",
            "asset_module_sha256",
            "scenario_id");
        ContainsAll(
            validator,
            "run_id",
            "source_head",
            "pid",
            "hwnd",
            "executable_sha256",
            "application_sha256",
            "asset_module_sha256",
            "scenario_id");
    }

    [TestMethod]
    public void AggregateEvidenceAcceptsNullableSelectionPidsAndManifestReplacementUsesARealBackupPath()
    {
        var controller = Text("src/RAWSelectionAssistant/Services/AssetLibraryP1AutomatedAcceptanceController.cs");
        var runner = Text("tools/AssetLibraryP1AutomatedAcceptance/Invoke-P1AssetLibraryAutomatedAcceptance.ps1");
        var validator = Text("tools/AssetLibraryP1AutomatedAcceptance/Test-P1AssetLibraryAutomatedEvidence.ps1");

        ContainsAll(
            controller,
            "primaryPid.ValueKind == JsonValueKind.Number",
            "restartPid.ValueKind == JsonValueKind.Number");
        ContainsAll(
            runner,
            "$backup = \"$Path.bak\"",
            "[IO.File]::Replace($temporary, $Path, $backup)",
            "[IO.File]::Delete($backup)",
            "failed in the application",
            "pixel-tart-p1-run-owned-binary-snapshot/v2",
            "New-BinarySnapshot",
            "build_source_executable_sha256",
            "build_source_application_sha256",
            "application_product_version",
            "asset_module_product_version");
        Assert.DoesNotContain("[IO.File]::Replace($temporary, $Path, $null)", runner, StringComparison.Ordinal);

        var driver = Text("src/PixelTart.Modules.AssetLibrary/AssetLibraryP1AutomatedAcceptanceDriver.cs");
        StringAssert.Contains(driver, "TextCompositionManager.StartComposition(composition)");
        Assert.DoesNotContain("TextCompositionManager.CompleteComposition(composition)", driver, StringComparison.Ordinal);
        ContainsAll(
            driver,
            "JsonPropertyName(\"button_identity\")",
            "JsonPropertyName(\"role\")",
            "JsonPropertyName(\"theme\")",
            "JsonPropertyName(\"state\")",
            "JsonPropertyName(\"surface_resource_key\")",
            "JsonPropertyName(\"surface_color\")",
            "JsonPropertyName(\"background_color\")",
            "JsonPropertyName(\"foreground_color\")",
            "JsonPropertyName(\"border_color\")",
            "JsonPropertyName(\"non_text_reference_color\")",
            "JsonPropertyName(\"focus_outer_color\")",
            "JsonPropertyName(\"focus_inner_color\")",
            "JsonPropertyName(\"text_contrast\")",
            "JsonPropertyName(\"non_text_contrast\")",
            "JsonPropertyName(\"focus_contrast\")",
            "JsonPropertyName(\"focus_visible\")",
            "JsonPropertyName(\"text_contrast_applicable\")",
            "JsonPropertyName(\"non_text_contrast_applicable\")",
            "JsonPropertyName(\"live_wpf_button_instance\")",
            "JsonPropertyName(\"source_declaration_probe\")",
            "JsonPropertyName(\"template_applied\")",
            "JsonPropertyName(\"state_resolution\")",
            "VisualTreeHelper.GetChildrenCount(probe) > 0");
        ContainsAll(
            validator,
            "?mode=ro&immutable=1");
    }

    private static void AssertCompileGuard(XDocument project, string projectName)
    {
        var define = project.Descendants("DefineConstants").SingleOrDefault(element =>
            element.Value.Contains("ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE", StringComparison.Ordinal));
        Assert.IsNotNull(define, $"{projectName} must compile the seam behind a dedicated symbol.");
        var condition = Attribute(define, "Condition");
        ContainsAll(
            condition,
            "'$(ModularHarnessDevPreview)' == 'true'",
            "'$(AssetLibraryP1AutomatedAcceptance)' == 'true'",
            "'$(AcceptanceBuild)' != 'true'",
            "'$(Configuration)' == 'Debug'");
        Assert.DoesNotContain("'$(Configuration)' != 'Release'", condition, StringComparison.Ordinal);
    }

    private static string AutomatedRuntimeSource()
    {
        var files = Directory.EnumerateFiles(Path("src"), "*AutomatedAcceptance*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.IsGreaterThanOrEqualTo(2, files.Length,
            "The public module seam and the DevPreview runtime orchestrator must be separate sources.");
        return string.Join(Environment.NewLine, files.Select(File.ReadAllText));
    }

    private static string AutomatedCSharpSource() => AutomatedRuntimeSource();

    private static string Slice(string text, string start, string end)
    {
        var startIndex = text.IndexOf(start, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, startIndex, start);
        var endIndex = text.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        Assert.IsGreaterThan(startIndex, endIndex, end);
        return text[startIndex..endIndex];
    }

    private static void ContainsAll(string text, params string[] expected)
    {
        foreach (var value in expected) StringAssert.Contains(text, value);
    }

    private static string Attribute(XElement element, string name) =>
        element.Attribute(name)?.Value ?? string.Empty;

    private static string Text(string relativePath) => File.ReadAllText(Path(relativePath));

    private static string Path(string relativePath) => System.IO.Path.Combine(
        FindRepositoryRoot(),
        relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(System.IO.Path.Combine(directory.FullName, "src")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
