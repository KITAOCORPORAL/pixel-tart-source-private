using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.Projects;

public enum ColorStudioNodeType { ReferenceMatch, ColorRange, Film, TransitionBlend }

public sealed record ColorAdjustmentStackNode(
    Guid Id,
    ColorStudioNodeType Type,
    string Name,
    bool Enabled = true,
    IReadOnlyDictionary<string, double>? NumericParameters = null,
    IReadOnlyList<VisualRgb24>? Samples = null,
    PixelTartFilmSettings? FilmSettings = null)
{
    public IReadOnlyDictionary<string, double> NumericParameters { get; init; } = NumericParameters ?? new Dictionary<string, double>();
    public IReadOnlyList<VisualRgb24> Samples { get; init; } = Samples ?? Array.Empty<VisualRgb24>();

    public ColorAdjustmentStackNode Normalize()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("Color Studio nodes require an id and name.");
        if (NumericParameters.Any(item => string.IsNullOrWhiteSpace(item.Key) || !double.IsFinite(item.Value))) throw new ArgumentException("Color Studio node parameters must be finite.");
        FilmSettings?.Validate();
        return this with { Name = Name.Trim(), NumericParameters = new Dictionary<string, double>(NumericParameters), Samples = Samples.ToArray() };
    }

    public ColorAdjustmentStackNode AddSample(VisualRgb24 sample) => this with { Samples = [.. Samples, sample] };

    public ColorAdjustmentStackNode RemoveSampleAt(int index)
    {
        if ((uint)index >= (uint)Samples.Count) throw new ArgumentOutOfRangeException(nameof(index));
        return this with { Samples = Samples.Where((_, position) => position != index).ToArray() };
    }
}

public sealed record ColorAdjustmentStack(IReadOnlyList<ColorAdjustmentStackNode> Nodes, int Version = 2, string WorkingSpace = "OKLabD65")
{
    public ColorAdjustmentStack Normalize()
    {
        if (Version != 2 || !string.Equals(WorkingSpace, "OKLabD65", StringComparison.Ordinal)) throw new ArgumentException("Unsupported Color Studio stack version.");
        var nodes = (Nodes ?? Array.Empty<ColorAdjustmentStackNode>()).Select(node => node.Normalize()).ToArray();
        if (nodes.Length == 0 || nodes.Select(node => node.Id).Distinct().Count() != nodes.Length) throw new ArgumentException("Color Studio stack must contain unique nodes.");
        return this with { Nodes = nodes };
    }

    /// <summary>Replace only chosen nodes; preserve each target's unselected nodes and their order.</summary>
    public ColorAdjustmentStack SyncSelectedFrom(ColorAdjustmentStack source, IReadOnlySet<Guid> selectedIds)
    {
        var target = Normalize(); var incoming = source.Normalize();
        var selected = incoming.Nodes.Where(node => selectedIds.Contains(node.Id)).ToArray();
        var replacements = selected.ToDictionary(node => node.Id);
        var existing = target.Nodes.Select(node => replacements.GetValueOrDefault(node.Id, node)).ToList();
        var known = existing.Select(node => node.Id).ToHashSet();
        foreach (var node in selected) if (known.Add(node.Id)) existing.Add(node);
        return target with { Nodes = existing };
    }
}

public sealed record ColorStudioSchemeV2(Guid Id, string Name, ColorAdjustmentStack Stack, DateTimeOffset UpdatedAtUtc, int Version = 2)
{
    public ColorStudioSchemeV2 Normalize() => this with { Name = string.IsNullOrWhiteSpace(Name) ? throw new ArgumentException("Scheme name is required.") : Name.Trim(), Stack = Stack.Normalize() };
}

public static class ColorStudioLegacyMigration
{
    public static ColorStudioSchemeV2 Migrate(ReferenceLook look, Guid? id = null)
    {
        look = look.Normalize();
        var nodes = new List<ColorAdjustmentStackNode>
        {
            new(Guid.NewGuid(), ColorStudioNodeType.ReferenceMatch, "参考仿色", true, new Dictionary<string, double>
            {
                ["match_strength"] = look.Parameters.MatchStrength, ["tone_strength"] = look.Parameters.ToneStrength,
                ["color_strength"] = look.Parameters.ColorStrength, ["contrast_strength"] = look.Parameters.ContrastStrength,
                ["saturation_strength"] = look.Parameters.SaturationStrength, ["keep_original_tone"] = look.Parameters.KeepOriginalTone ? 1 : 0
            })
        };
        if (look.Film is not null) nodes.Add(new(Guid.NewGuid(), ColorStudioNodeType.Film, "胶片", look.Film.Enabled, new Dictionary<string, double>
        {
            ["profile_amount"] = look.Film.ProfileAmount, ["grain_amount"] = look.Film.GrainAmount, ["grain_size"] = look.Film.GrainSize,
            ["halation_amount"] = look.Film.HalationAmount, ["bloom_amount"] = look.Film.BloomAmount, ["vignette_amount"] = look.Film.VignetteAmount
        }, FilmSettings: look.Film));
        return new ColorStudioSchemeV2(id ?? Guid.NewGuid(), look.Name, new ColorAdjustmentStack(nodes), DateTimeOffset.UtcNow).Normalize();
    }
}

public static class ColorStudioSchemeSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static string Serialize(ColorStudioSchemeV2 scheme) => JsonSerializer.Serialize(scheme.Normalize(), Options);
    public static ColorStudioSchemeV2 Deserialize(string json) => (JsonSerializer.Deserialize<ColorStudioSchemeV2>(json, Options) ?? throw new InvalidDataException("Color Studio scheme is empty.")).Normalize();
    public static string ComputeHash(ColorStudioSchemeV2 scheme) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(scheme))));
}
