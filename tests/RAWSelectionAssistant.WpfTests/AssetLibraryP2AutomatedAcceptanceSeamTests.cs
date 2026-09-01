using System.IO;
using System.Text.Json;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP2AutomatedAcceptanceSeamTests
{
    private static readonly string[] Scenarios =
    [
        "fixture-integrity/v1", "organization-browser/v1", "smart-tag-query/v1",
        "four-views-query-sort/v1", "selection-large/v1", "metadata-drag-command/v1",
        "inspector-states/v1", "resilience-states/v1", "restart-persistence/v1",
        "layout-dpi-performance/v1"
    ];

    [TestMethod]
    public void DebugOnlyBuildFlagFlowsThroughBothProjects()
    {
        foreach (var relative in new[] { "src/RAWSelectionAssistant/RAWSelectionAssistant.csproj", "src/PixelTart.Modules.AssetLibrary/PixelTart.Modules.AssetLibrary.csproj" })
        {
            var document = XDocument.Load(Path(relative));
            var constants = document.Descendants("DefineConstants").Where(node => node.Value.Contains("ASSET_LIBRARY_P2_AUTOMATED_ACCEPTANCE", StringComparison.Ordinal)).ToArray();
            Assert.HasCount(1, constants, relative);
            var condition = constants[0].Attribute("Condition")?.Value ?? string.Empty;
            ContainsAll(condition, "ModularHarnessDevPreview", "AssetLibraryP2AutomatedAcceptance", "AcceptanceBuild", "Configuration", "Debug");
        }
        var appProject = Read("src/RAWSelectionAssistant/RAWSelectionAssistant.csproj");
        ContainsAll(appProject, "ValidateAssetLibraryP2AutomatedAcceptanceBuild", "cannot be combined with a P1 acceptance seam", "IncludeSourceRevisionInInformationalVersion");
    }

    [TestMethod]
    public void AppStartupAndExitOwnTheP2ControllerWithoutChangingP1()
    {
        var app = Read("src/RAWSelectionAssistant/App.xaml.cs");
        ContainsAll(app,
            "#if ASSET_LIBRARY_P2_AUTOMATED_ACCEPTANCE", "AssetLibraryP2AutomatedAcceptanceController.TryCreate",
            "ApplyStartRoute(_mainViewModel)", "ConfigureAssetLibraryP2AutomatedAcceptance",
            "TeardownAssetLibraryP2AutomatedAcceptanceAsync", "FinalizeOnApplicationExit",
            "assetLibraryP1StateController = _assetLibraryP2AutomatedController");
        ContainsAll(app, "AssetLibraryP1AutomatedAcceptanceController.TryCreate", "ConfigureAssetLibraryP1AutomatedAcceptance");
    }

    [TestMethod]
    public void ControllerOwnsExactP2ScenariosAndRepositoryFixtureBootstrap()
    {
        var controller = Read("src/RAWSelectionAssistant/Services/AssetLibraryP2AutomatedAcceptanceController.cs");
        foreach (var scenario in Scenarios) StringAssert.Contains(controller, $"\"{scenario}\"");
        ContainsAll(controller, "File.Copy(fixtureDatabase, _databasePath, overwrite: false)",
            "BeforeRepositoryInitializationAsync", "SqliteAssetLibraryRepository", "assetCount != 512",
            "activeCount != 500", "archivedCount != 12", "RestartPersistenceScenario");
    }

    [TestMethod]
    public void ControllerWritesEveryRequiredEvidenceCategory()
    {
        var controller = Read("src/RAWSelectionAssistant/Services/AssetLibraryP2AutomatedAcceptanceController.cs");
        foreach (var directory in new[] { "bounds", "queries", "selections", "views", "commands", "inspectors", "performance", "databases", "screenshots" })
            StringAssert.Contains(controller, $"\"{directory}\"");
        ContainsAll(controller, "previous_event_hash", "event_hash", "previous_summary_hash", "summary_hash",
            "process_session_id", "executable_sha256", "application_sha256", "asset_module_sha256");
    }

    [TestMethod]
    public void MainWindowRunsAllTenRealWpfScenarios()
    {
        var window = Read("src/RAWSelectionAssistant/MainWindow.AssetLibraryP2AutomatedAcceptance.cs");
        foreach (var symbol in new[]
                 {
                     "FixtureIntegrityScenario", "OrganizationBrowserScenario", "SmartTagQueryScenario", "FourViewsQuerySortScenario",
                     "SelectionLargeScenario", "MetadataDragCommandScenario", "InspectorStatesScenario", "ResilienceStatesScenario",
                     "RestartPersistenceScenario", "LayoutDpiPerformanceScenario"
                 }) StringAssert.Contains(window, symbol);
        ContainsAll(window, "AssetLibraryWorkspace.Content is not AssetLibraryWpfPage", "CaptureFrameAsync", "RenderTargetBitmap",
            "CaptureVisibleBounds", "CaptureBrowserSnapshot", "WriteJsonArtifact", "MarkScenarioCompleted");
    }

    [TestMethod]
    public void FourViewsSelectionDragInspectorAndPerformanceAreMeasured()
    {
        var window = Read("src/RAWSelectionAssistant/MainWindow.AssetLibraryP2AutomatedAcceptance.cs");
        ContainsAll(window,
            "AssetLibraryViewMode.Grid", "AssetLibraryViewMode.Masonry", "AssetLibraryViewMode.Justified", "AssetLibraryViewMode.List",
            "SelectFirstAssetsAsync(100)", "DropSelectionOnFirstFolderAsync", "UndoAndRedoAsync",
            "InspectorMode != \"query\"", "InspectorMode != \"single\"", "InspectorMode != \"multiple\"",
            "firstScreenMs > 1500", "TotalMilliseconds > 250", "TotalMilliseconds > 350", "TotalMilliseconds > 750", "TotalMilliseconds > 100");
    }

    [TestMethod]
    public void FourDpiLayoutsAreSimulatedWithoutDisplayWrites()
    {
        var window = Read("src/RAWSelectionAssistant/MainWindow.AssetLibraryP2AutomatedAcceptance.cs");
        foreach (var value in new[] { "(1366, 768, 100)", "(1920, 1080, 125)", "(1920, 1080, 150)", "(2560, 1440, 175)" })
            StringAssert.Contains(window, value);
        ContainsAll(window, "real_display_settings_changed = false", "has_overflow = false");
        Assert.IsFalse(window.Contains("ChangeDisplaySettings", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DriverUsesPublicWpfAndCommandSeamsNotDesktopOrReflection()
    {
        var driver = Read("src/PixelTart.Modules.AssetLibrary/AssetLibraryP2AutomatedAcceptanceDriver.cs");
        ContainsAll(driver, "_assetGrid.SelectedItems.Add", "SwitchViewCommand.Execute", "SortBrowserCommand.Execute",
            "PreviewDropAsync", "ExecuteDropAsync", "P2UndoCommand.Execute", "P2RedoCommand.Execute",
            "VirtualizingPanel.GetIsVirtualizing", "CaptureVisibleBounds");
        foreach (var forbidden in new[] { "SendInput", "SetForegroundWindow", "AutomationElement", "InvokePattern", "BindingFlags", "GetField(", "GetProperty(" })
            Assert.IsFalse(driver.Contains(forbidden, StringComparison.Ordinal), forbidden);
    }

    [TestMethod]
    public void ContractAndImplementationScenarioOrdersMatch()
    {
        using var contract = JsonDocument.Parse(Read("tools/AssetLibraryP2AutomatedAcceptance/automated-acceptance-contract.json"));
        CollectionAssert.AreEqual(Scenarios, contract.RootElement.GetProperty("required_scenario_order").EnumerateArray().Select(item => item.GetString()).ToArray());
        var runner = Read("tools/AssetLibraryP2AutomatedAcceptance/Invoke-P2AssetLibraryAutomatedAcceptance.ps1");
        var primaryStart = runner.IndexOf("$primaryScenarios = @(", StringComparison.Ordinal);
        var primaryEnd = runner.IndexOf("$sessions =", primaryStart, StringComparison.Ordinal);
        var primaryBlock = runner[primaryStart..primaryEnd];
        var offsets = Scenarios.Select(scenario => primaryBlock.IndexOf($"'{scenario}'", StringComparison.Ordinal)).ToArray();
        Assert.IsTrue(offsets.All(offset => offset >= 0));
        CollectionAssert.AreEqual(offsets.Order().ToArray(), offsets);
    }

    private static void ContainsAll(string text, params string[] values)
    {
        foreach (var value in values) StringAssert.Contains(text, value);
    }
    private static string Read(string relative) => File.ReadAllText(Path(relative));
    private static string Path(string relative) => System.IO.Path.Combine(RepositoryRoot(), relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
    private static string RepositoryRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null && !File.Exists(System.IO.Path.Combine(cursor.FullName, "RAWSelectionAssistant.sln"))) cursor = cursor.Parent;
        return cursor?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
