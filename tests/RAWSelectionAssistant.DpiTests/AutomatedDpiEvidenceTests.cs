using System.Security.Cryptography;
using System.Text.Json;

namespace RAWSelectionAssistant.DpiTests;

[TestClass]
public sealed class AutomatedDpiEvidenceTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string EvidenceRoot = Path.Combine(RepositoryRoot, "artifacts", "rc12-product-visual");
    private static readonly string ManifestPath = Path.Combine(EvidenceRoot, "rc12-product-visual-evidence.json");

    [TestMethod]
    [DataRow(100)]
    [DataRow(125)]
    [DataRow(150)]
    [DataRow(200)]
    public void EachCurrentDpiHasEightPassingProductStates(int dpiPercent)
    {
        using var manifest = LoadManifest();
        var rows = Captures(manifest)
            .Where(row => row.GetProperty("group").GetString() == "dpi-current" && row.GetProperty("dpi_percent").GetInt32() == dpiPercent)
            .ToArray();
        Assert.HasCount(8, rows);
        Assert.IsTrue(rows.All(row => row.GetProperty("passed").GetBoolean()));
        Assert.IsTrue(rows.All(row => row.GetProperty("process_exited_before_next").GetBoolean()));
    }

    [TestMethod]
    [DataRow("MainWindow")]
    [DataRow("AssetLibraryGrid")]
    [DataRow("AssetFilter")]
    [DataRow("AssetContextMenu")]
    [DataRow("AssetViewer")]
    [DataRow("CalendarBookingAssets")]
    [DataRow("AssetInspirationCollection")]
    [DataRow("AssetRecentLibraries")]
    public void EveryRequiredProductStatePassesAtAllFourDpis(string scenario)
    {
        using var manifest = LoadManifest();
        var rows = Captures(manifest)
            .Where(row => row.GetProperty("group").GetString() == "dpi-current" && row.GetProperty("state").GetString() == scenario)
            .ToArray();
        Assert.HasCount(4, rows);
        CollectionAssert.AreEquivalent(new[] { 100, 125, 150, 200 }, rows.Select(row => row.GetProperty("dpi_percent").GetInt32()).ToArray());
    }

    [TestMethod]
    public void CurrentProducerUsesRealApplicationDarkThemeAndProcessIsolation()
    {
        using var manifest = LoadManifest();
        var root = manifest.RootElement;
        Assert.AreEqual("pixel-tart-rc12-product-visual/v1", root.GetProperty("schema").GetString());
        Assert.AreEqual("2.3.0-RC12", root.GetProperty("product_version").GetString());
        Assert.IsTrue(root.GetProperty("real_app_xaml").GetBoolean());
        Assert.IsTrue(root.GetProperty("real_main_window").GetBoolean());
        Assert.IsTrue(root.GetProperty("pixel_tart_dark_theme").GetBoolean());
        Assert.IsTrue(root.GetProperty("process_per_fixture").GetBoolean());
        Assert.IsFalse(root.GetProperty("application_singleton_shared").GetBoolean());
        Assert.IsTrue(root.GetProperty("lifecycle_isolated_per_capture").GetBoolean());
    }

    [TestMethod]
    public void DpiScreenshotsAndMetadataAreCompleteAndByteValid()
    {
        using var manifest = LoadManifest();
        var rows = Captures(manifest).Where(row => row.GetProperty("group").GetString() == "dpi-current").ToArray();
        Assert.HasCount(32, rows);
        foreach (var row in rows)
        {
            var screenshotPath = row.GetProperty("path").GetString()!;
            var metadataPath = row.GetProperty("metadata_path").GetString()!;
            Assert.IsTrue(File.Exists(screenshotPath), screenshotPath);
            Assert.IsTrue(File.Exists(metadataPath), metadataPath);
            Assert.AreEqual(row.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(screenshotPath))));
            using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
            var metadataRoot = metadata.RootElement;
            Assert.AreEqual("automated-logical-simulation", metadataRoot.GetProperty("validationMode").GetString());
            Assert.IsFalse(metadataRoot.GetProperty("physicalDpiManuallyTested").GetBoolean());
            var layout = metadataRoot.GetProperty("layout");
            Assert.AreEqual(0, layout.GetProperty("Overflow").GetArrayLength());
            Assert.AreEqual(0, layout.GetProperty("ZeroSizedInteractive").GetArrayLength());
            Assert.AreEqual(0, layout.GetProperty("TextClipping").GetArrayLength());
            Assert.AreEqual(0, layout.GetProperty("UnexpectedWhiteSurfaces").GetArrayLength());
            Assert.IsTrue(metadataRoot.GetProperty("themeInspection").GetProperty("Passed").GetBoolean());
        }
    }

    [TestMethod]
    public void IsolatedSyntheticSourceImagesRemainByteIdentical()
    {
        using var manifest = LoadManifest();
        var root = manifest.RootElement;
        var before = SourceRows(root.GetProperty("source_before"));
        var after = SourceRows(root.GetProperty("source_after"));
        CollectionAssert.AreEquivalent(before.Keys.ToArray(), after.Keys.ToArray());
        Assert.IsTrue(before.All(pair => pair.Value == after[pair.Key]));
        Assert.IsTrue(root.GetProperty("source_files_unchanged").GetBoolean());
    }

    [TestMethod]
    public void Historical204EvidenceIsNotPartOfTheCurrentProductGate()
    {
        Assert.DoesNotContain(Path.Combine("automated-dpi-review", "2.0.4"), ManifestPath, StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(ManifestPath.EndsWith(Path.Combine("rc12-product-visual", "rc12-product-visual-evidence.json"), StringComparison.OrdinalIgnoreCase));
    }

    private static JsonDocument LoadManifest() => JsonDocument.Parse(File.ReadAllText(ManifestPath));
    private static JsonElement[] Captures(JsonDocument manifest) => manifest.RootElement.GetProperty("captures").EnumerateArray().Select(row => row.Clone()).ToArray();
    private static Dictionary<string, string> SourceRows(JsonElement rows) => rows.EnumerateArray().ToDictionary(
        row => row.GetProperty("name").GetString()!,
        row => $"{row.GetProperty("bytes").GetInt64()}|{row.GetProperty("sha256").GetString()}",
        StringComparer.OrdinalIgnoreCase);

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
