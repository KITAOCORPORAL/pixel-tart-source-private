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

    public ColorStudioRenderResult Render(HighBitDepthImageBuffer source, AssetVisualAnalysisResult analysis, ReferenceLook? reference, ColorAdjustmentStack stack, CancellationToken token = default, bool captureNodeDiagnostics = true)
    {
        ArgumentNullException.ThrowIfNull(source); var normalized = stack.Normalize(); var current = source.Clone(); var precisionStages = new List<string>(PrecisionTrace.ProfessionalDefault.Stages);
        var outputs = new List<VisualPixelBuffer>(normalized.Nodes.Count); var inputs = new Dictionary<Guid, VisualPixelBuffer>();
        foreach (var node in normalized.Nodes.Where(item => item.Enabled))
        {
            token.ThrowIfCancellationRequested(); if (captureNodeDiagnostics) inputs[node.Id] = current.ToVisualRgb24();
            current = ApplyHighPrecision(current, node, reference, analysis, token);
            if (captureNodeDiagnostics) outputs.Add(current.ToVisualRgb24());
        }
        var descriptor = new ColorPipelineDescriptor(InputInterpretation: "HighPrecision sRGB", WorkingRepresentation: normalized.WorkingSpace, OutputEncoding: "sRGB display adapter");
        return new(current.ToVisualRgb24(), outputs, descriptor, inputs, current, new PrecisionTrace(precisionStages));
    }

    private HighBitDepthImageBuffer ApplyHighPrecision(HighBitDepthImageBuffer source, ColorAdjustmentStackNode node, ReferenceLook? reference, AssetVisualAnalysisResult analysis, CancellationToken token)
    {
        if (ColorStudioToolCatalog.IsTool(node.Type)) return ColorStudioToolProcessor.Apply(source, node, token);
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
            if (exposure == 0 && contrast == 0 && saturation == 0) return source.Clone();
            for (var i = 0; i < values.Length; i += 3) { if ((i & 2047) == 0) token.ThrowIfCancellationRequested(); var lab = OklabColorSpace.FromSrgb(values[i], values[i + 1], values[i + 2]); var l = Math.Clamp(Math.Clamp((lab.L - .5) * (1 + contrast / 100) + .5, 0, 1) * scale, 0, 1); var rgb = OklabColorSpace.ToSrgbGamutMappedFloat(new(l, lab.A * (1 + saturation / 100), lab.B * (1 + saturation / 100))); values[i] = (float)rgb.R; values[i + 1] = (float)rgb.G; values[i + 2] = (float)rgb.B; }
        }
        else if (node.Type == ColorStudioNodeType.TransitionBlend)
        {
            if (Parameter(node, "amount", .25) == 0) return source.Clone();
            var amount = Math.Clamp(Parameter(node, "amount", .25), 0, 1); for (var i = 0; i < values.Length; i += 3) { if ((i & 2047) == 0) token.ThrowIfCancellationRequested(); var lab = OklabColorSpace.FromSrgb(values[i], values[i + 1], values[i + 2]); var rgb = OklabColorSpace.ToSrgbGamutMappedFloat(new(lab.L, lab.A * (1 - amount * .12), lab.B * (1 - amount * .12))); values[i] = (float)rgb.R; values[i + 1] = (float)rgb.G; values[i + 2] = (float)rgb.B; }
        }
        else if (node.Type == ColorStudioNodeType.ColorRange)
        {
            var amount = Math.Clamp(Parameter(node, "strength", 100) / 100, 0, 1); var keepL = Parameter(node, "keep_original_luminance", 0) >= .5; var hue = Parameter(node, "hue", 0) * Math.PI / 180; var saturation = Math.Max(0, 1 + Parameter(node, "saturation", 0) / 100); var chroma = Math.Max(0, 1 + Parameter(node, "chroma", 0) / 100); var lightness = Parameter(node, "lightness", 0) / 100;
            if (amount == 0 || node.Samples.Count == 0 || hue == 0 && saturation == 1 && chroma == 1 && (lightness == 0 || keepL)) return source.Clone();
            for (var i = 0; i < values.Length; i += 3)
            {
                if ((i & 2047) == 0) token.ThrowIfCancellationRequested();
                var lab = OklabColorSpace.FromSrgb(values[i], values[i + 1], values[i + 2]); var selection = SelectionWeight(lab, node) * amount;
                if (selection == 0) continue;
                var angle = hue * selection; var cos = Math.Cos(angle); var sin = Math.Sin(angle);
                var scale = 1 + (chroma * saturation - 1) * selection;
                if (Parameter(node, "range_version", 1) >= 2)
                {
                    var neutral = Math.Clamp(lab.Chroma / .04, 0, 1); neutral = neutral * neutral * (3 - 2 * neutral);
                    var targetChroma = Math.Max(0, lab.Chroma * saturation + Math.Clamp(Parameter(node, "chroma", 0), -100, 100) / 100 * .1 * neutral);
                    scale = lab.Chroma <= 1e-9 ? 1 : 1 + (targetChroma / lab.Chroma - 1) * selection;
                }
                var a = (lab.A * cos - lab.B * sin) * scale; var b = (lab.A * sin + lab.B * cos) * scale;
                var rgb = OklabColorSpace.ToSrgbGamutMappedFloat(new(keepL ? lab.L : Math.Clamp(lab.L + lightness * selection, 0, 1), a, b));
                values[i] = (float)rgb.R; values[i + 1] = (float)rgb.G; values[i + 2] = (float)rgb.B;
            }
        }
        else if (node.Type == ColorStudioNodeType.Film)
        {
            return PixelTartFilmPipeline.Apply(source, FilmSettings(node), token);
        }
        return new(source.Width, source.Height, values, source.SourceBitDepth, source.WorkingColorSpace, source.Orientation, source.Metadata);
    }

    private static double DeterministicNoise(int x, int y, int seed) { unchecked { var n = x * 374761393 + y * 668265263 + seed * 1442695041; n = (n ^ (n >> 13)) * 1274126177; return ((n ^ (n >> 16)) & 0xFFFF) / 32767.5 - 1; } }

    public ColorStudioRenderResult Render(VisualPixelBuffer source, AssetVisualAnalysisResult analysis, ReferenceLook? reference, ColorAdjustmentStack stack, CancellationToken token = default, bool captureNodeDiagnostics = true)
    {
        var normalized = stack.Normalize();
        if (normalized.ProcessingVersion == 2 || normalized.Nodes.Any(node => ColorStudioToolCatalog.IsTool(node.Type) || Parameter(node, "range_version", 1) >= 2))
            return Render(HighBitDepthImageBuffer.FromVisualRgb24(source), analysis, reference, normalized, token, captureNodeDiagnostics);
        // Version 1 keeps the historical per-node RGB24 quantization of saved JPEG/PNG schemes.
        // New precision is explicit state, so merely opening an old project cannot alter its pixels.
        var current = new VisualPixelBuffer(source.Width, source.Height, source.Rgb24.ToArray());
        var outputs = new List<VisualPixelBuffer>(normalized.Nodes.Count);
        var inputs = new Dictionary<Guid, VisualPixelBuffer>();
        foreach (var node in normalized.Nodes.Where(item => item.Enabled))
        {
            token.ThrowIfCancellationRequested(); if (captureNodeDiagnostics) inputs[node.Id] = current;
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
            if (captureNodeDiagnostics) outputs.Add(current);
        }
        return new(current, outputs, new(WorkingRepresentation: normalized.WorkingSpace), inputs);
    }
    /// <summary>Diagnostic selection preview only; callers must export Render(...).Pixels.</summary>
    public static byte[] SelectionWeights(VisualPixelBuffer source, ColorAdjustmentStackNode node, CancellationToken token = default)
    {
        if (node.Type != ColorStudioNodeType.ColorRange) throw new ArgumentException("Selection requires a color range node.");
        var weights = new byte[source.PixelCount];
        for (var pixel = 0; pixel < source.PixelCount; pixel++)
        {
            if ((pixel & 2047) == 0) token.ThrowIfCancellationRequested();
            if (!source.Alpha.IsEmpty && source.Alpha.Span[pixel] == 0) continue;
            var offset = pixel * 3; var color = OklabColorSpace.FromSrgb(new VisualRgb24(source.Rgb24.Span[offset], source.Rgb24.Span[offset + 1], source.Rgb24.Span[offset + 2]));
            weights[pixel] = (byte)Math.Clamp(Math.Round(255 * SelectionWeight(color, node)), 0, 255);
        }
        return weights;
    }

    /// <summary>Same membership as the float processing node, before output quantization.</summary>
    public static byte[] SelectionWeights(HighBitDepthImageBuffer source, ColorAdjustmentStackNode node, CancellationToken token = default)
    {
        if (node.Type != ColorStudioNodeType.ColorRange) throw new ArgumentException("Selection requires a color range node.");
        var weights = new byte[source.PixelCount];
        for (var pixel = 0; pixel < source.PixelCount; pixel++)
        {
            if ((pixel & 2047) == 0) token.ThrowIfCancellationRequested();
            var offset = pixel * 3;
            var color = OklabColorSpace.FromSrgb(source.Rgb32.Span[offset], source.Rgb32.Span[offset + 1], source.Rgb32.Span[offset + 2]);
            weights[pixel] = (byte)Math.Clamp(Math.Round(255 * SelectionWeight(color, node)), 0, 255);
        }
        return weights;
    }

    /// <summary>Diagnostic rendered mask only; production view uses independent SelectionWeights overlay.</summary>
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
        var weight = Math.Clamp(positive * (1 - negative), 0, 1);
        if (Parameter(node, "range_version", 1) >= 2)
        {
            var low = Math.Clamp(Parameter(node, "lightness_min", 0), 0, 1); var high = Math.Clamp(Parameter(node, "lightness_max", 1), 0, 1);
            if (low > high) throw new ArgumentException("颜色范围明度下限不能大于上限。");
            var feather = Math.Clamp(Parameter(node, "lightness_feather", .05), .001, .5);
            static double Fade(double distance, double softness) { var t = Math.Clamp(distance / softness, 0, 1); return 1 - t * t * (3 - 2 * t); }
            if (color.L < low) weight *= Fade(low - color.L, feather);
            if (color.L > high) weight *= Fade(color.L - high, feather);
        }
        return weight;
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
