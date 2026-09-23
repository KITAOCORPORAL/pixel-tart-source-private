using System.Text.Json;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class PhotographyAcceptanceManifestTests
{
    [TestMethod]
    public void SameRunManifest_ExcludesHistoricalRc12AndHasCurrentSource()
    {
        var root = Root();
        var manifestPath = Path.Combine(root, "PHOTOGRAPHY_ACCEPTANCE_MANIFEST.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        Assert.AreEqual("photo-20260923T075548Z-52be8b76", manifest.RootElement.GetProperty("run_id").GetString());
        Assert.AreEqual("52be8b76e738c1defae504e0941cb843e0caa695", manifest.RootElement.GetProperty("product_source_sha").GetString());
        Assert.AreEqual("PASS", manifest.RootElement.GetProperty("product_development_gate").GetString());
        Assert.AreEqual("PENDING", manifest.RootElement.GetProperty("release_hardware_gate").GetString());
        Assert.AreEqual("EXCLUDED", manifest.RootElement.GetProperty("historical_rc12").GetString());
    }

    [TestMethod]
    public void SameRunArtifacts_ExistAndManifestDigestIsStable()
    {
        var root = Root();
        var manifestPath = Path.Combine(root, "PHOTOGRAPHY_ACCEPTANCE_MANIFEST.json");
        Assert.IsTrue(File.Exists(manifestPath));
        Assert.IsTrue(File.Exists(Path.Combine(root, "PHOTOGRAPHY_ACCEPTANCE_MANIFEST.md")));
        Assert.IsTrue(File.Exists(Path.Combine(root, "events.jsonl")));
        Assert.AreEqual("C03408085AB8C7205C1F85BE21B91EB540A74E90519BB8E265339CB50D997CFF", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(manifestPath))));
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent;
        Assert.IsNotNull(directory);
        return Path.Combine(directory!.FullName, "docs", "evidence", "photography", "photo-20260923T075548Z-52be8b76");
    }
}
