using System.Security.Cryptography;
using System.Text.Json;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class PhotographyAcceptanceManifestTests
{
    private const string RunId = "photo-20260923T075548Z-52be8b76";
    private const string ProductSource = "52be8b76e738c1defae504e0941cb843e0caa695";

    [TestMethod]
    public void PhotographyEvidenceSameRunTests()
    {
        using var manifest = Read("PHOTOGRAPHY_ACCEPTANCE_MANIFEST.json");
        var root = manifest.RootElement;
        Assert.AreEqual(RunId, root.GetProperty("run_id").GetString());
        Assert.AreEqual(14, root.GetProperty("event_count").GetInt32());
        Assert.AreEqual(14, root.GetProperty("last_event_sequence").GetInt32());
        var events = File.ReadAllLines(Path.Combine(Root(), "events.jsonl"));
        Assert.AreEqual(events.Length, root.GetProperty("event_count").GetInt32());
        for (var index = 0; index < events.Length; index++)
        {
            using var entry = JsonDocument.Parse(events[index]);
            Assert.AreEqual(index + 1, entry.RootElement.GetProperty("sequence").GetInt32());
            Assert.AreEqual(RunId, entry.RootElement.GetProperty("run_id").GetString());
            Assert.AreEqual(ProductSource, entry.RootElement.GetProperty("product_source_sha").GetString());
        }
        using var failed = JsonDocument.Parse(events[11]);
        using var recovered = JsonDocument.Parse(events[12]);
        Assert.AreEqual("run_failed", failed.RootElement.GetProperty("event").GetString());
        Assert.AreEqual("seal_recovery", recovered.RootElement.GetProperty("event").GetString());
        Assert.AreEqual(Hash("events.jsonl"), root.GetProperty("event_digest").GetString());
    }

    [TestMethod]
    public void PhotographyEvidenceProductSourceTests()
    {
        using var manifest = Read("PHOTOGRAPHY_ACCEPTANCE_MANIFEST.json");
        using var input = Read("input-manifest.json");
        using var output = Read("output-manifest.json");
        using var artifacts = Read("artifact-manifest.json");
        foreach (var document in new[] { manifest, input, output, artifacts })
        {
            Assert.AreEqual(ProductSource, document.RootElement.GetProperty("product_source_sha").GetString());
            Assert.AreEqual(RunId, document.RootElement.GetProperty("run_id").GetString());
        }
        Assert.AreEqual("10.0.401", manifest.RootElement.GetProperty("sdk_version").GetString());
        Assert.AreEqual(Hash("input-manifest.json"), manifest.RootElement.GetProperty("input_manifest_hash").GetString());
        Assert.AreEqual(Hash("output-manifest.json"), manifest.RootElement.GetProperty("output_manifest_hash").GetString());
        Assert.AreEqual(Hash("artifact-manifest.json"), manifest.RootElement.GetProperty("artifact_manifest_hash").GetString());
    }

    [TestMethod]
    public void PhotographyEvidenceManifestHashTests() =>
        Assert.AreEqual(File.ReadAllText(Path.Combine(Root(), "PHOTOGRAPHY_ACCEPTANCE_MANIFEST.sha256")).Trim(), Hash("PHOTOGRAPHY_ACCEPTANCE_MANIFEST.json"));

    [TestMethod]
    public void PhotographyEvidenceArtifactsExistTests()
    {
        using var manifest = Read("PHOTOGRAPHY_ACCEPTANCE_MANIFEST.json");
        var artifacts = manifest.RootElement.GetProperty("artifacts");
        Assert.AreEqual(manifest.RootElement.GetProperty("artifact_count").GetInt32(), artifacts.GetArrayLength());
        foreach (var artifact in artifacts.EnumerateArray())
            Assert.IsTrue(File.Exists(Path.Combine(Root(), artifact.GetProperty("path").GetString()!)));
    }

    [TestMethod]
    public void PhotographyEvidenceArtifactDigestTests()
    {
        using var manifest = Read("PHOTOGRAPHY_ACCEPTANCE_MANIFEST.json");
        foreach (var artifact in manifest.RootElement.GetProperty("artifacts").EnumerateArray())
        {
            var relative = artifact.GetProperty("path").GetString()!;
            Assert.AreEqual(RunId, artifact.GetProperty("run_id").GetString());
            Assert.AreEqual(ProductSource, artifact.GetProperty("product_source_sha").GetString());
            Assert.IsFalse(string.IsNullOrWhiteSpace(artifact.GetProperty("producer").GetString()));
            Assert.IsTrue(DateTimeOffset.TryParse(artifact.GetProperty("generated_at").GetString(), out _));
            Assert.AreEqual(Hash(relative), artifact.GetProperty("artifact_sha256").GetString(), relative);
        }
    }

    [TestMethod]
    public void PhotographyEvidenceNoHistoricalRc12MixTests()
    {
        using var manifest = Read("PHOTOGRAPHY_ACCEPTANCE_MANIFEST.json");
        Assert.AreEqual("EXCLUDED", manifest.RootElement.GetProperty("historical_rc12").GetString());
        Assert.AreEqual("PASS_WITH_DOCUMENTED_SEAL_RECOVERY", manifest.RootElement.GetProperty("evidence_consistency").GetString());
        Assert.AreEqual("PASS", manifest.RootElement.GetProperty("product_development_gate").GetString());
        Assert.AreEqual("PENDING", manifest.RootElement.GetProperty("release_hardware_gate").GetString());
        foreach (var artifact in manifest.RootElement.GetProperty("artifacts").EnumerateArray())
            Assert.IsFalse(artifact.GetProperty("path").GetString()!.Contains("rc12", StringComparison.OrdinalIgnoreCase));
    }

    private static JsonDocument Read(string relative) => JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), relative)));
    private static string Hash(string relative) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root(), relative))));

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent;
        Assert.IsNotNull(directory);
        return Path.Combine(directory!.FullName, "docs", "evidence", "photography", RunId);
    }
}
