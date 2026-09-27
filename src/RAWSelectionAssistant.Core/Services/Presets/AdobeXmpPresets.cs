using System.Security.Cryptography;
using System.Xml.Linq;

namespace RAWSelectionAssistant.Core.Services.Presets;

public enum PresetFieldSupport { Exact, Approximate, Unsupported }

public sealed record PresetFieldMapping(string SourceName, string? CanonicalName, PresetFieldSupport Support, double? Value, string? RawValue);

public sealed record AdobeXmpPreset(
    Guid Id,
    string Name,
    string SourcePath,
    string SourceHash,
    IReadOnlyList<PresetFieldMapping> Fields,
    DateTimeOffset ImportedAtUtc,
    string SourceVersion = "xmp-v1")
{
    public IReadOnlyDictionary<string, double> NumericParameters => Fields.Where(x => x.CanonicalName is not null && x.Value is not null)
        .GroupBy(x => x.CanonicalName!, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Last().Value!.Value, StringComparer.OrdinalIgnoreCase);
    public bool IsCompatible => Fields.Any(x => x.Support is PresetFieldSupport.Exact or PresetFieldSupport.Approximate);
}

public static class AdobeXmpPresetParser
{
    private static readonly IReadOnlyDictionary<string, (string Canonical, PresetFieldSupport Support)> Known = new Dictionary<string, (string, PresetFieldSupport)>(StringComparer.OrdinalIgnoreCase)
    {
        ["Exposure2012"] = ("exposure", PresetFieldSupport.Exact), ["Contrast2012"] = ("contrast", PresetFieldSupport.Exact),
        ["Highlights2012"] = ("highlights", PresetFieldSupport.Exact), ["Shadows2012"] = ("shadows", PresetFieldSupport.Exact),
        ["Whites2012"] = ("whites", PresetFieldSupport.Exact), ["Blacks2012"] = ("blacks", PresetFieldSupport.Exact),
        ["Temperature"] = ("temperature", PresetFieldSupport.Approximate), ["Tint"] = ("tint", PresetFieldSupport.Approximate),
        ["Texture"] = ("texture", PresetFieldSupport.Approximate), ["Clarity2012"] = ("clarity", PresetFieldSupport.Approximate),
        ["Dehaze"] = ("dehaze", PresetFieldSupport.Approximate), ["Vibrance"] = ("vibrance", PresetFieldSupport.Approximate),
        ["Saturation"] = ("saturation", PresetFieldSupport.Approximate), ["GrainAmount"] = ("grain", PresetFieldSupport.Approximate),
        ["PostCropVignetteAmount"] = ("vignette", PresetFieldSupport.Approximate),
        ["ToneCurvePV2012"] = ("tone_curve", PresetFieldSupport.Unsupported), ["ColorGradeShadowHue"] = ("color_grading", PresetFieldSupport.Unsupported),
        ["CameraCalibration"] = ("calibration", PresetFieldSupport.Unsupported), ["LutProfileReference"] = ("lut_profile", PresetFieldSupport.Unsupported)
    };

    public static async Task<AdobeXmpPreset> ParseFileAsync(string path, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path); var fullPath = Path.GetFullPath(path);
        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var memory = new MemoryStream(); await stream.CopyToAsync(memory, token).ConfigureAwait(false); token.ThrowIfCancellationRequested();
        var bytes = memory.ToArray(); return Parse(fullPath, bytes);
    }

    public static AdobeXmpPreset Parse(string path, ReadOnlyMemory<byte> content)
    {
        var document = XDocument.Parse(System.Text.Encoding.UTF8.GetString(content.Span), LoadOptions.PreserveWhitespace);
        var fields = new List<PresetFieldMapping>();
        foreach (var attribute in document.Descendants().Attributes())
        {
            if (!Known.TryGetValue(attribute.Name.LocalName, out var mapping)) continue;
            var raw = attribute.Value.Trim(); double? value = double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
            fields.Add(new(attribute.Name.LocalName, mapping.Canonical, value is null ? PresetFieldSupport.Unsupported : mapping.Support, value, raw));
        }
        var name = document.Descendants().Attributes().FirstOrDefault(a => a.Name.LocalName is "PresetName" or "Name")?.Value;
        return new(Guid.NewGuid(), string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(path) : name.Trim(), Path.GetFullPath(path), Convert.ToHexString(SHA256.HashData(content.Span)), fields, DateTimeOffset.UtcNow);
    }
}

public sealed class AdobeXmpPresetStore(string directory)
{
    public async Task SaveAsync(AdobeXmpPreset preset, CancellationToken token = default)
    {
        Directory.CreateDirectory(directory); var path = Path.Combine(directory, preset.Id.ToString("N") + ".json");
        var json = System.Text.Json.JsonSerializer.Serialize(preset, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var temporary = path + ".tmp"; await File.WriteAllTextAsync(temporary, json, token).ConfigureAwait(false); File.Move(temporary, path, true);
    }

    public async Task<IReadOnlyList<AdobeXmpPreset>> LoadAsync(CancellationToken token = default)
    {
        if (!Directory.Exists(directory)) return [];
        var result = new List<AdobeXmpPreset>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        { token.ThrowIfCancellationRequested(); try { var value = System.Text.Json.JsonSerializer.Deserialize<AdobeXmpPreset>(await File.ReadAllTextAsync(path, token).ConfigureAwait(false)); if (value is not null) result.Add(value); } catch (System.Text.Json.JsonException) { } }
        return result.OrderByDescending(x => x.ImportedAtUtc).ToArray();
    }
}

public sealed record AdobeXmpImportProgress(int Completed, int Total, AdobeXmpPreset? Preset, string? Error);

public sealed class AdobeXmpImportService(AdobeXmpPresetStore store)
{
    public async Task<IReadOnlyList<AdobeXmpPreset>> ImportAsync(IEnumerable<string> paths, IProgress<AdobeXmpImportProgress>? progress = null, CancellationToken token = default)
    {
        var files = paths.Where(path => string.Equals(Path.GetExtension(path), ".xmp", StringComparison.OrdinalIgnoreCase)).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var imported = new List<AdobeXmpPreset>();
        for (var index = 0; index < files.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            try { var preset = await AdobeXmpPresetParser.ParseFileAsync(files[index], token).ConfigureAwait(false); await store.SaveAsync(preset, token).ConfigureAwait(false); imported.Add(preset); progress?.Report(new(index + 1, files.Length, preset, null)); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            { progress?.Report(new(index + 1, files.Length, null, error.Message)); }
        }
        return imported;
    }
}

public static class PresetStrengthInterpolator
{
    public static IReadOnlyDictionary<string, double> Interpolate(IReadOnlyDictionary<string, double> before, IReadOnlyDictionary<string, double> preset, double strength)
    {
        strength = Math.Clamp(strength, 0, 1); var keys = before.Keys.Concat(preset.Keys).Distinct(StringComparer.OrdinalIgnoreCase); var output = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys) { var a = before.TryGetValue(key, out var av) ? av : 0; var b = preset.TryGetValue(key, out var bv) ? bv : 0; output[key] = a + (b - a) * strength; }
        return output;
    }
}
