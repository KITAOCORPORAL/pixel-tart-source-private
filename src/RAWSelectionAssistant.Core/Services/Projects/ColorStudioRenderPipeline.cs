using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.Core.Services.Projects;

public sealed record ColorStudioRenderResult(VisualPixelBuffer Pixels, IReadOnlyList<VisualPixelBuffer> NodeOutputs, ColorPipelineDescriptor Pipeline,
    IReadOnlyDictionary<Guid, VisualPixelBuffer>? NodeInputs = null, HighBitDepthImageBuffer? ProcessingPixels = null,
    PrecisionTrace? Precision = null);

/// <summary>Headless linear Color Studio renderer. Export callers can use this same chain; no second WPF renderer is introduced.</summary>
public sealed class ColorStudioRenderPipeline
{
    private readonly ReferenceLookMatcher _matcher = new();

    public ColorStudioRenderResult Render(HighBitDepthImageBuffer source, AssetVisualAnalysisResult analysis, ReferenceLook? reference, ColorAdjustmentStack stack, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source); var normalized = stack.Normalize(); var current = source.Clone(); var precisionStages = new List<string>(PrecisionTrace.ProfessionalDefault.Stages);
        var outputs = new List<VisualPixelBuffer>(normalized.Nodes.Count); var inputs = new Dictionary<Guid, VisualPixelBuffer>();
        foreach (var node in normalized.Nodes.Where(item => item.Enabled))
        {
            token.ThrowIfCancellationRequested(); inputs[node.Id] = current.ToVisualRgb24();
            current = ApplyHighPrecision(current, node, reference, analysis, token);
            outputs.Add(current.ToVisualRgb24());
        }
        var descriptor = new ColorPipelineDescriptor(InputInterpretation: "HighPrecision sRGB", WorkingRepresentation: normalized.WorkingSpace, OutputEncoding: "sRGB display adapter");
        return new(current.ToVisualRgb24(), outputs, descriptor, inputs, current, new PrecisionTrace(precisionStages));
    }

    private HighBitDepthImageBuffer ApplyHighPrecision(HighBitDepthImageBuffer source, ColorAdjustmentStackNode node, ReferenceLook? reference, AssetVisualAnalysisResult analysis, CancellationToken token)
    {
        if (node.Type == ColorStudioNodeType.Develop) return ColorStudioDevelop.Apply(source, node, token);
        var values = source.Rgb32.ToArray();
        if (node.Type == ColorStudioNodeType.ReferenceMatch && reference is not null)
        {
            var p = reference.Parameters with
            {
                MatchStrength = Parameter(node, "match_strength", reference.Parameters.MatchStrength),
                ToneStrength = Parameter(node, "tone_strength", reference.Parameters.ToneStrength),
                ColorStrength = Parameter(node, "color_strength", reference.Parameters.ColorStrength),
                ContrastStrength = Parameter(node, "contrast_strength", reference.Parameters.ContrastStrength),
                SaturationStrength = Parameter(node, "saturation_strength", reference.Parameters.SaturationStrength),
                KeepOriginalTone = Parameter(node, "keep_original_tone", reference.Parameters.KeepOriginalTone ? 1 : 0) >= .5,
                SkinProtection = Parameter(node, "skin_protection", reference.Parameters.SkinProtection),
                HighlightProtection = Parameter(node, "highlight_protection", reference.Parameters.HighlightProtection),
                NeutralProtection = Parameter(node, "neutral_protection", reference.Parameters.NeutralProtection)
            };
            if (p.MatchStrength == 0)
                return source.Clone();
            var transform = _matcher.BuildTransform(source, analysis, reference with { Parameters = p });
            for (var i = 0; i < values.Length; i += 3)
            {
                if ((i & 2047) == 0) token.ThrowIfCancellationRequested();
                var mapped = transform.ApplyFloat(values[i], values[i + 1], values[i + 2]);
                values[i] = mapped.R; values[i + 1] = mapped.G; values[i + 2] = mapped.B;
            }
        }
        else if (node.Type == ColorStudioNodeType.Preset)
        {
            var strength = Math.Clamp(Parameter(node, "preset_strength", 1), 0, 1); var exposure = Parameter(node, "exposure", 0) * strength; var contrast = Parameter(node, "contrast", 0) * strength; var saturation = Parameter(node, "saturation", 0) * strength; var scale = Math.Pow(2, exposure);
            for (var i = 0; i < values.Length; i += 3) { if ((i & 2047) == 0) token.ThrowIfCancellationRequested(); var lab = OklabColorSpace.FromSrgb(values[i], values[i + 1], values[i + 2]); var l = Math.Clamp(Math.Clamp((lab.L - .5) * (1 + contrast / 100) + .5, 0, 1) * scale, 0, 1); var rgb = OklabColorSpace.ToSrgbGamutMappedFloat(new(l, lab.A * (1 + saturation / 100), lab.B * (1 + saturation / 100))); values[i] = (float)rgb.R; values[i + 1] = (float)rgb.G; values[i + 2] = (float)rgb.B; }
        }
        else if (node.Type == ColorStudioNodeType.TransitionBlend)
        {
            var amount = Math.Clamp(Parameter(node, "amount", .25), 0, 1); for (var i = 0; i < values.Length; i += 3) { if ((i & 2047) == 0) token.ThrowIfCancellationRequested(); var lab = OklabColorSpace.FromSrgb(values[i], values[i + 1], values[i + 2]); var rgb = OklabColorSpace.ToSrgbGamutMappedFloat(new(lab.L, lab.A * (1 - amount * .12), lab.B * (1 - amount * .12))); values[i] = (float)rgb.R; values[i + 1] = (float)rgb.G; values[i + 2] = (float)rgb.B; }
        }
        else if (node.Type == ColorStudioNodeType.ColorRange)
        {
            var amount = Math.Clamp(Parameter(node, "strength", 100) / 100, 0, 1); var keepL = Parameter(node, "keep_original_luminance", 0) >= .5; var hue = Parameter(node, "hue", 0) * Math.PI / 180; var saturation = Math.Max(0, 1 + Parameter(node, "saturation", 0) / 100); var chroma = Math.Max(0, 1 + Parameter(node, "chroma", 0) / 100); var lightness = Parameter(node, "lightness", 0) / 100;
            for (var i = 0; i < values.Length; i += 3) { if ((i & 2047) == 0) token.ThrowIfCancellationRequested(); var lab = OklabColorSpace.FromSrgb(values[i], values[i + 1], values[i + 2]); var selection = SelectionWeight(lab, node) * amount; if (selection == 0) continue; var angle = hue * selection; var cos = Math.Cos(angle); var sin = Math.Sin(angle); var scale = 1 + (chroma * saturation - 1) * selection; var a = (lab.A * cos - lab.B * sin) * scale; var b = (lab.A * sin + lab.B * cos) * scale; var rgb = OklabColorSpace.ToSrgbGamutMappedFloat(new(keepL ? lab.L : Math.Clamp(lab.L + lightness * selection, 0, 1), a, b)); values[i] = (float)rgb.R; values[i + 1] = (float)rgb.G; values[i + 2] = (float)rgb.B; }
        }
        else if (node.Type == ColorStudioNodeType.Film)
        {
            return PixelTartFilmPipeline.Apply(source, FilmSettings(node), token);
        }
        return new(source.Width, source.Height, values, source.SourceBitDepth, source.WorkingColorSpace, source.Orientation, source.Metadata);
    }

    private static double DeterministicNoise(int x, int y, int seed) { unchecked { var n = x * 374761393 + y * 668265263 + seed * 1442695041; n = (n ^ (n >> 13)) * 1274126177; return ((n ^ (n >> 16)) & 0xFFFF) / 32767.5 - 1; } }

    public ColorStudioRenderResult Render(VisualPixelBuffer source, AssetVisualAnalysisResult analysis, ReferenceLook? reference, ColorAdjustmentStack stack, CancellationToken token = default)
    {
        var normalized = stack.Normalize(); var current = new VisualPixelBuffer(source.Width, source.Height, source.Rgb24.ToArray());
        var outputs = new List<VisualPixelBuffer>(normalized.Nodes.Count);
        var inputs = new Dictionary<Guid, VisualPixelBuffer>();
        foreach (var node in normalized.Nodes.Where(item => item.Enabled))
        {
            token.ThrowIfCancellationRequested();
            inputs[node.Id] = current;
            current = node.Type switch
            {
                ColorStudioNodeType.ReferenceMatch when reference is not null => ApplyReference(current, analysis, reference, node, token),
                ColorStudioNodeType.ColorRange => ApplyColorRange(current, node, token),
                ColorStudioNodeType.Film => ApplyFilm(current, node, token),
                ColorStudioNodeType.TransitionBlend => ApplyTransition(current, node, token),
                ColorStudioNodeType.Preset => ApplyPreset(current, node, token),
                ColorStudioNodeType.Develop => ColorStudioDevelop.Apply(HighBitDepthImageBuffer.FromVisualRgb24(current), node, token).ToVisualRgb24(),
                _ => current
            };
            outputs.Add(current);
        }
        return new(current, outputs, new(WorkingRepresentation: normalized.WorkingSpace), inputs);
    }

    /// <summary>Diagnostic selection preview only; callers must export Render(...).Pixels.</summary>
    public VisualPixelBuffer ShowSelection(VisualPixelBuffer source, ColorAdjustmentStackNode node, CancellationToken token = default)
    {
        if (node.Type != ColorStudioNodeType.ColorRange) throw new ArgumentException("Selection preview requires a color range node.", nameof(node));
        var output = source.Rgb24.ToArray();
        for (var pixel = 0; pixel < source.PixelCount; pixel++)
        {
            if ((pixel & 1023) == 0) token.ThrowIfCancellationRequested();
            var offset = pixel * 3; var rgb = new VisualRgb24(output[offset], output[offset + 1], output[offset + 2]);
            var selected = SelectionWeight(OklabColorSpace.FromSrgb(rgb), node);
            var lab = OklabColorSpace.FromSrgb(rgb);
            var muted = OklabColorSpace.ToSrgbGamutMapped(new(lab.L * .52, lab.A * .22, lab.B * .22));
            output[offset] = Blend(muted.R, rgb.R, selected); output[offset + 1] = Blend(muted.G, rgb.G, selected); output[offset + 2] = Blend(muted.B, rgb.B, selected);
        }
        return new(source.Width, source.Height, output);
    }

    private VisualPixelBuffer ApplyReference(VisualPixelBuffer source, AssetVisualAnalysisResult analysis, ReferenceLook reference, ColorAdjustmentStackNode node, CancellationToken token)
    {
        var p = reference.Parameters with
        {
            MatchStrength = Parameter(node, "match_strength", reference.Parameters.MatchStrength), ToneStrength = Parameter(node, "tone_strength", reference.Parameters.ToneStrength),
            ColorStrength = Parameter(node, "color_strength", reference.Parameters.ColorStrength), ContrastStrength = Parameter(node, "contrast_strength", reference.Parameters.ContrastStrength),
            SaturationStrength = Parameter(node, "saturation_strength", reference.Parameters.SaturationStrength), KeepOriginalTone = Parameter(node, "keep_original_tone", reference.Parameters.KeepOriginalTone ? 1 : 0) >= .5,
            SkinProtection = Parameter(node, "skin_protection", reference.Parameters.SkinProtection), HighlightProtection = Parameter(node, "highlight_protection", reference.Parameters.HighlightProtection),
            NeutralProtection = Parameter(node, "neutral_protection", reference.Parameters.NeutralProtection)
        };
        return _matcher.Match(source, analysis, reference with { Parameters = p }, token).Preview;
    }

    private static VisualPixelBuffer ApplyColorRange(VisualPixelBuffer source, ColorAdjustmentStackNode node, CancellationToken token)
    {
        if (node.Samples.Count == 0) return new(source.Width, source.Height, source.Rgb24.ToArray());
        var amount = Math.Clamp(Parameter(node, "strength", 100) / 100, 0, 1); var keepL = Parameter(node, "keep_original_luminance", 0) >= .5;
        var hue = Parameter(node, "hue", 0) * Math.PI / 180; var saturation = Math.Max(0, 1 + Parameter(node, "saturation", 0) / 100); var chroma = Math.Max(0, 1 + Parameter(node, "chroma", 0) / 100); var lightness = Parameter(node, "lightness", 0) / 100;
        var output = source.Rgb24.ToArray();
        for (var pixel = 0; pixel < source.PixelCount; pixel++)
        {
            if ((pixel & 1023) == 0) token.ThrowIfCancellationRequested(); var offset = pixel * 3; var lab = OklabColorSpace.FromSrgb(new(output[offset], output[offset + 1], output[offset + 2]));
            var selection = SelectionWeight(lab, node) * amount; var angle = hue * selection; var cos = Math.Cos(angle); var sin = Math.Sin(angle); var scale = 1 + (chroma * saturation - 1) * selection;
            var a = (lab.A * cos - lab.B * sin) * scale; var b = (lab.A * sin + lab.B * cos) * scale;
            var result = OklabColorSpace.ToSrgbGamutMapped(new(keepL ? lab.L : Math.Clamp(lab.L + lightness * selection, 0, 1), a, b));
            output[offset] = result.R; output[offset + 1] = result.G; output[offset + 2] = result.B;
        }
        return new(source.Width, source.Height, output);
    }

    private static double SelectionWeight(OklabColor color, ColorAdjustmentStackNode node)
    {
        if (node.Samples.Count == 0) return 0;
        var range = Math.Max(.001, Parameter(node, "range", .12)); var softness = Math.Max(.001, Parameter(node, "softness", .08));
        var positive = node.Samples.Count == 0 ? 0 : node.Samples.Max(sample => Weight(color, sample, range, softness));
        var negative = node.NegativeSamples.Count == 0 ? 0 : node.NegativeSamples.Max(sample => Weight(color, sample, range, softness));
        return Math.Clamp(positive * (1 - negative), 0, 1);
    }

    private static double Weight(OklabColor color, VisualRgb24 sample, double range, double softness)
    {
        var lab = OklabColorSpace.FromSrgb(sample);
        var distance = Math.Sqrt(Math.Pow(color.A - lab.A, 2) + Math.Pow(color.B - lab.B, 2));
        var t = Math.Clamp((distance - range) / softness, 0, 1);
        return 1 - t * t * (3 - 2 * t);
    }

    private static byte Blend(byte muted, byte original, double selected) => (byte)Math.Clamp(Math.Round(muted + (original - muted) * selected), 0, 255);

    private static PixelTartFilmSettings FilmSettings(ColorAdjustmentStackNode node) => node.FilmSettings is { } settings
        ? settings with { Enabled = true }
        : new(true, "PT-W01", Parameter(node, "profile_amount", 70), Parameter(node, "grain_amount", 0), Parameter(node, "grain_size", 35), Parameter(node, "halation_amount", 0), Parameter(node, "bloom_amount", 0), Parameter(node, "vignette_amount", 0));
    private static VisualPixelBuffer ApplyFilm(VisualPixelBuffer source, ColorAdjustmentStackNode node, CancellationToken token) => PixelTartFilmPipeline.Apply(source, FilmSettings(node), token);

    private static VisualPixelBuffer ApplyTransition(VisualPixelBuffer source, ColorAdjustmentStackNode node, CancellationToken token)
    {
        var amount = Math.Clamp(Parameter(node, "amount", .25), 0, 1); var output = source.Rgb24.ToArray();
        for (var pixel = 0; pixel < source.PixelCount; pixel++) { if ((pixel & 2047) == 0) token.ThrowIfCancellationRequested(); var offset = pixel * 3; var lab = OklabColorSpace.FromSrgb(new(output[offset], output[offset + 1], output[offset + 2])); var result = OklabColorSpace.ToSrgbGamutMapped(new(lab.L, lab.A * (1 - amount * .12), lab.B * (1 - amount * .12))); output[offset] = result.R; output[offset + 1] = result.G; output[offset + 2] = result.B; }
        return new(source.Width, source.Height, output);
    }

    private static VisualPixelBuffer ApplyPreset(VisualPixelBuffer source, ColorAdjustmentStackNode node, CancellationToken token)
    {
        var strength = Math.Clamp(Parameter(node, "preset_strength", 1), 0, 1);
        var exposure = Parameter(node, "exposure", 0) * strength;
        var contrast = Parameter(node, "contrast", 0) * strength;
        var saturation = Parameter(node, "saturation", 0) * strength;
        var output = source.Rgb24.ToArray();
        var exposureScale = Math.Pow(2, exposure);
        for (var pixel = 0; pixel < source.PixelCount; pixel++)
        {
            if ((pixel & 2047) == 0) token.ThrowIfCancellationRequested();
            var offset = pixel * 3;
            var lab = OklabColorSpace.FromSrgb(new(output[offset], output[offset + 1], output[offset + 2]));
            var l = Math.Clamp((lab.L - .5) * (1 + contrast / 100) + .5, 0, 1);
            l = Math.Clamp(l * exposureScale, 0, 1);
            var chroma = Math.Max(0, 1 + saturation / 100);
            var result = OklabColorSpace.ToSrgbGamutMapped(new(l, lab.A * chroma, lab.B * chroma));
            output[offset] = result.R; output[offset + 1] = result.G; output[offset + 2] = result.B;
        }
        return new(source.Width, source.Height, output);
    }

    private static double Parameter(ColorAdjustmentStackNode node, string name, double fallback) => node.NumericParameters.TryGetValue(name, out var value) && double.IsFinite(value) ? value : fallback;
}
