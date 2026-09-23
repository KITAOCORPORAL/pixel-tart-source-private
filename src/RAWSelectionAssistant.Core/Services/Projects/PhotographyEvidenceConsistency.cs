using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RAWSelectionAssistant.Core.Services.Projects;

public sealed record PhotographyEvidenceArtifact([property: JsonPropertyName("path")] string RelativePath, string Sha256, long Bytes);

public sealed record PhotographyEvidenceManifest(
    string RunId,
    string ProductSourceSha,
    string SdkVersion,
    string FixtureVersion,
    string InputManifestHash,
    string OutputManifestHash,
    int EventCount,
    string EventDigest,
    IReadOnlyList<PhotographyEvidenceArtifact> Artifacts)
{
    public void Validate(string root)
    {
        if (string.IsNullOrWhiteSpace(RunId) || string.IsNullOrWhiteSpace(ProductSourceSha) || string.IsNullOrWhiteSpace(SdkVersion) || string.IsNullOrWhiteSpace(FixtureVersion) || string.IsNullOrWhiteSpace(InputManifestHash) || string.IsNullOrWhiteSpace(OutputManifestHash) || string.IsNullOrWhiteSpace(EventDigest)) throw new InvalidDataException("Photography evidence provenance is incomplete.");
        if (EventCount <= 0 || Artifacts.Count == 0) throw new InvalidDataException("Photography evidence provenance has no events or artifacts.");
        if (Artifacts.Select(item => item.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Artifacts.Count) throw new InvalidDataException("Duplicate photography evidence artifact.");
        foreach (var artifact in Artifacts)
        {
            if (string.IsNullOrWhiteSpace(artifact.RelativePath) || Path.IsPathRooted(artifact.RelativePath) || artifact.RelativePath.Split('/', '\\').Contains("..")) throw new InvalidDataException("Invalid photography evidence artifact path.");
            var path = Path.Combine(root, artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) throw new FileNotFoundException($"Missing photography evidence artifact: {artifact.RelativePath}", path);
            var bytes = new FileInfo(path).Length;
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            if (bytes != artifact.Bytes || !string.Equals(hash, artifact.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"Photography evidence artifact hash mismatch: {artifact.RelativePath}");
        }
        Verify("input-manifest.json", InputManifestHash);
        Verify("output-manifest.json", OutputManifestHash);
        Verify("events.jsonl", EventDigest);
        var events = File.ReadLines(Path.Combine(root, "events.jsonl")).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        if (events.Length != EventCount) throw new InvalidDataException("Photography evidence event count mismatch.");
        foreach (var name in new[] { "input-manifest.json", "output-manifest.json" })
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, name)));
            if (document.RootElement.GetProperty("run_id").GetString() != RunId || document.RootElement.GetProperty("product_source_sha").GetString() != ProductSourceSha)
                throw new InvalidDataException($"Photography evidence identity mismatch: {name}");
        }

        void Verify(string name, string expected)
        {
            if (!string.Equals(HashFile(Path.Combine(root, name)), expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"Photography evidence digest mismatch: {name}");
        }
    }

    public static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    public static string HashText(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static PhotographyEvidenceManifest Deserialize(string json) => JsonSerializer.Deserialize<PhotographyEvidenceManifest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidDataException("Photography evidence manifest is empty.");
}

public static class PhotographyEvidenceRunValidator
{
    public static void Validate(string root)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        using var artifactManifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "artifact-manifest.json")));
        var identity = manifest.RootElement;
        var artifacts = artifactManifest.RootElement;
        if (artifacts.GetProperty("run_id").GetString() != identity.GetProperty("run_id").GetString() ||
            artifacts.GetProperty("product_source_sha").GetString() != identity.GetProperty("product_source_sha").GetString())
            throw new InvalidDataException("Photography artifact manifest identity mismatch.");
        var provenance = new PhotographyEvidenceManifest(
            identity.GetProperty("run_id").GetString()!, identity.GetProperty("product_source_sha").GetString()!,
            identity.GetProperty("sdk_version").GetString()!, identity.GetProperty("fixture_version").GetString()!,
            identity.GetProperty("input_manifest_hash").GetString()!, identity.GetProperty("output_manifest_hash").GetString()!,
            identity.GetProperty("event_count").GetInt32(), identity.GetProperty("event_digest").GetString()!,
            artifacts.GetProperty("artifacts").Deserialize<PhotographyEvidenceArtifact[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
        provenance.Validate(root);
        foreach (var artifact in provenance.Artifacts)
        {
            if (artifact.RelativePath is not ("input-manifest.json" or "output-manifest.json" or "events.jsonl"))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, artifact.RelativePath)));
                if (document.RootElement.GetProperty("run_id").GetString() != provenance.RunId ||
                    document.RootElement.GetProperty("product_source_sha").GetString() != provenance.ProductSourceSha)
                    throw new InvalidDataException($"Photography artifact identity mismatch: {artifact.RelativePath}");
            }
        }
    }
}
