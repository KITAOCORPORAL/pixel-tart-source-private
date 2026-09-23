using RAWSelectionAssistant.Core.Services.Projects;
using System.Text.Json;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class PhotographyEvidenceConsistencyTests
{
    private const string ExpectedRunId = "photo-closure-20260923-b37ab5a3";
    private const string ExpectedSourceSha = "b37ab5a3224907fc352f8d6f0697c9082d922839";

    [TestMethod]
    public void PhotographyEvidenceProductShaAndRunId_AreCurrent()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(EvidenceRoot(), "manifest.json")));
        Assert.AreEqual(ExpectedRunId, document.RootElement.GetProperty("run_id").GetString());
        Assert.AreEqual(ExpectedSourceSha, document.RootElement.GetProperty("product_source_sha").GetString());
        Assert.AreEqual("10.0.401", document.RootElement.GetProperty("sdk_version").GetString());
    }

    [TestMethod]
    public void PhotographyEvidenceManifestHashesAndArtifacts_AreConsistent()
    {
        var root = EvidenceRoot();
        var manifest = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(root, "manifest.json")));
        var provenance = new PhotographyEvidenceManifest(
            manifest.GetProperty("run_id").GetString()!,
            manifest.GetProperty("product_source_sha").GetString()!,
            manifest.GetProperty("sdk_version").GetString()!,
            manifest.GetProperty("fixture_version").GetString()!,
            manifest.GetProperty("input_manifest_hash").GetString()!,
            manifest.GetProperty("output_manifest_hash").GetString()!,
            manifest.GetProperty("event_count").GetInt32(),
            manifest.GetProperty("event_digest").GetString()!,
            JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(root, "artifact-manifest.json"))).GetProperty("artifacts")
                .Deserialize<PhotographyEvidenceArtifact[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
        provenance.Validate(root);
        PhotographyEvidenceRunValidator.Validate(root);
    }

    [TestMethod]
    public void PhotographyEvidenceDigest_AgreesWithEventCount()
    {
        var root = EvidenceRoot();
        var lines = File.ReadLines(Path.Combine(root, "events.jsonl")).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        Assert.HasCount(11, lines);
        Assert.AreEqual("45642D4F317771EEE5BA07A3A7A7FE8787FA63E95B0CF4FC5D2269B8AEE8E8EE", PhotographyEvidenceManifest.HashFile(Path.Combine(root, "events.jsonl")));
    }

    private static string EvidenceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent;
        Assert.IsNotNull(directory);
        return Path.Combine(directory!.FullName, "docs", "evidence", "photography", "20260923-b37ab5a3");
    }
}
