using System.Text.Json;

namespace RAWSelectionAssistant.DpiTests;

[TestClass]
public sealed class AutomatedDpiEvidenceTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string EvidenceRoot = Path.Combine(RepositoryRoot, "artifacts", "automated-dpi-review", "2.0.4");

    [TestMethod]
    [DataRow(125)]
    [DataRow(150)]
    [DataRow(200)]
    public void EachDpiHasEighteenPassingScenarios(int dpiPercent)
    {
        var hashes = LoadArray("AutomatedDpiScreenshotHashes.json");
        var rows = hashes.Where(row => row.GetProperty("DpiPercent").GetInt32() == dpiPercent).ToArray();
        Assert.HasCount(18, rows);
        Assert.IsTrue(rows.All(row => row.GetProperty("Passed").GetBoolean()));
    }

    [TestMethod]
    [DataRow("WorkbenchDarkExpanded")]
    [DataRow("WorkbenchDarkCollapsed")]
    [DataRow("WorkbenchLight")]
    [DataRow("SettingsDialog")]
    [DataRow("ToolboxPopup")]
    [DataRow("ToolboxFullPage")]
    [DataRow("QuickToolsManager")]
    [DataRow("OrganizeEmpty")]
    [DataRow("OrganizeGrouped")]
    [DataRow("OrganizeManifest")]
    [DataRow("CollageEmpty")]
    [DataRow("Collage2x2")]
    [DataRow("CollageVertical")]
    [DataRow("CollageExport")]
    [DataRow("FeedbackDialog")]
    [DataRow("ConfirmationDialog")]
    [DataRow("ContextMenu")]
    [DataRow("Tooltip")]
    public void EveryRequiredScenarioPassesAtAllThreeDpis(string scenario)
    {
        var hashes = LoadArray("AutomatedDpiScreenshotHashes.json");
        var rows = hashes.Where(row => string.Equals(row.GetProperty("Scenario").GetString(), scenario, StringComparison.Ordinal)).ToArray();
        Assert.HasCount(3, rows);
        Assert.IsTrue(rows.All(row => row.GetProperty("Passed").GetBoolean()));
    }

    [TestMethod]
    public void ScreenshotMatrixContainsFiftyFourUniqueHashes()
    {
        var hashes = LoadArray("AutomatedDpiScreenshotHashes.json");
        Assert.HasCount(54, hashes);
        Assert.HasCount(54, hashes.Select(row => row.GetProperty("Sha256").GetString()).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ResultDeclaresAutomatedLogicalSimulationAndNoPhysicalManualTest()
    {
        using var document = LoadDocument("AutomatedDpiResults.json");
        var root = document.RootElement;
        Assert.AreEqual("automated-logical-simulation", root.GetProperty("ValidationMode").GetString());
        Assert.IsFalse(root.GetProperty("PhysicalDpiManuallyTested").GetBoolean());
        Assert.IsTrue(root.GetProperty("AutomatedDpiCompatibilityPassed").GetBoolean());
    }

    [TestMethod]
    public void EveryLayoutResultHasNoBlockingIssue()
    {
        var rows = LoadArray("LayoutBoundsResults.json");
        Assert.HasCount(54, rows);
        Assert.IsTrue(rows.All(row => row.GetProperty("passed").GetBoolean()));
        Assert.IsTrue(rows.All(row => row.GetProperty("layout").GetProperty("BlockingIssueCount").GetInt32() == 0));
    }

    [TestMethod]
    public void ThemeRenderingAndHighContrastStructurePassEveryScenario()
    {
        var rows = LoadArray("ThemeResults.json");
        Assert.HasCount(54, rows);
        foreach (var row in rows)
        {
            var inspection = row.GetProperty("themeInspection");
            Assert.IsTrue(inspection.GetProperty("Passed").GetBoolean());
            Assert.IsTrue(inspection.GetProperty("HighContrastResourceStructurePresent").GetBoolean());
            Assert.AreEqual(0, inspection.GetProperty("MissingBrushResources").GetArrayLength());
            Assert.IsTrue(inspection.GetProperty("ControlStyleRenderChecks").EnumerateObject().All(property => property.Value.GetBoolean()));
        }
    }

    [TestMethod]
    public void IsolatedSourceImagesRemainByteIdentical()
    {
        using var document = LoadDocument("SourceFileIntegrity.json");
        var root = document.RootElement;
        Assert.IsTrue(root.GetProperty("Passed").GetBoolean());
        var before = root.GetProperty("Before").EnumerateArray().ToDictionary(row => row.GetProperty("Name").GetString()!, row => row.GetProperty("Sha256").GetString());
        var after = root.GetProperty("After").EnumerateArray().ToDictionary(row => row.GetProperty("Name").GetString()!, row => row.GetProperty("Sha256").GetString());
        CollectionAssert.AreEquivalent(before.Keys.ToArray(), after.Keys.ToArray());
        Assert.IsTrue(before.All(pair => string.Equals(pair.Value, after[pair.Key], StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void OrganizeActionPanelUsesItsOwnVerticalScrollViewer()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "RAWSelectionAssistant", "Views", "OrganizePhotosView.xaml"));
        StringAssert.Contains(xaml, "Grid.Column=\"2\" Style=\"{StaticResource PanelBorder}\"><ScrollViewer VerticalScrollBarVisibility=\"Auto\"");
    }

    private static JsonElement[] LoadArray(string fileName)
    {
        using var document = LoadDocument(fileName);
        return document.RootElement.EnumerateArray().Select(element => element.Clone()).ToArray();
    }

    private static JsonDocument LoadDocument(string fileName) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(EvidenceRoot, fileName)));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
