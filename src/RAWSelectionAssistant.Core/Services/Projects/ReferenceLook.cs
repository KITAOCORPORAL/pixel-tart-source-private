using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.Core.Services.Projects;

public sealed record ReferenceLookParameters(double MatchStrength = 100, double ToneStrength = 50,
    double ColorStrength = 70, double ContrastStrength = 50, double SaturationStrength = 50,
    double SkinProtection = 60, double HighlightProtection = 70, double NeutralProtection = 65,
    bool KeepOriginalTone = false)
{
    public void Validate()
    {
        if (new[] { MatchStrength, ToneStrength, ColorStrength, ContrastStrength, SaturationStrength, SkinProtection, HighlightProtection, NeutralProtection }
            .Any(value => !double.IsFinite(value) || value < 0 || value > 100)) throw new ArgumentException("Look strengths must be between 0 and 100.");
    }
}
public sealed record ReferenceLookSource(Guid LibraryId, Guid? AssetId, string Name, string SourcePath,
    string ContentHash, double Weight, AssetVisualAnalysisResult Analysis, string Kind = "Asset", Guid? ContainerId = null,
    ReferencePixelStatistics? PixelStatistics = null);
public sealed record ReferenceLook(Guid ReferenceLookId, string Name, Guid? ProjectId,
    IReadOnlyList<ReferenceLookSource> ReferenceSources, ReferenceLookParameters Parameters,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version = 1,
    PixelTartFilmSettings? Film = null, int AlgorithmVersion = 1)
{
    public ReferenceLook Normalize()
    {
        Parameters.Validate();
        Film?.Validate();
        if (AlgorithmVersion is not (1 or 2)) throw new ArgumentException("Unsupported reference algorithm version.");
        if (AlgorithmVersion == 2)
            foreach (var source in ReferenceSources.Where(x => x.Weight > 0))
                (source.PixelStatistics ?? throw new ArgumentException("Pixel statistics are required for the corrected reference algorithm.")).Validate();
        if (ReferenceLookId == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Version != 1 || ReferenceSources.Count == 0 ||
            ReferenceSources.Any(source => !double.IsFinite(source.Weight) || source.Weight < 0 || source.Analysis.Palette.Count == 0 ||
                source.Analysis.HistogramLuma.Length != 256)) throw new ArgumentException("Look identity, references and weights must be valid.");
        var sum = ReferenceSources.Sum(source => source.Weight);
        if (!double.IsFinite(sum) || sum <= 0) throw new ArgumentException("At least one reference must have positive weight.");
        return this with { Name = Name.Trim(), ReferenceSources = ReferenceSources.Select(source => source with { Weight = source.Weight / sum }).ToArray() };
    }
}
public sealed record ReferenceToneCurve(IReadOnlyList<double> LuminanceKnots);
public sealed record ReferenceMatchResult(VisualPixelBuffer Preview, ReferenceLookParameters Parameters, ReferenceToneCurve ToneCurve,
    ReferenceDifferenceWarning? DifferenceWarning = null, ColorPipelineDescriptor? Pipeline = null);

public sealed class ReferenceLookTransform
{
    private readonly ReferenceLookParameters _parameters;
    private readonly ReferenceColorTarget _source;
    private readonly ReferenceColorTarget _target;
    private readonly double[] _toneCurve;
    private readonly double _contrast;
    private readonly double _saturation;
    private readonly int _algorithmVersion;

    internal ReferenceLookTransform(ReferenceLookParameters parameters, ReferenceColorTarget source, ReferenceColorTarget target,
        double[] toneCurve, double contrast, double saturation, ColorPipelineDescriptor pipeline, int algorithmVersion = 1)
    {
        _parameters = parameters; _source = source; _target = target; _toneCurve = toneCurve;
        _contrast = contrast; _saturation = saturation; Pipeline = pipeline;
        _algorithmVersion = algorithmVersion;
    }

    public ColorPipelineDescriptor Pipeline { get; }
    public IReadOnlyList<double> ToneCurve => _toneCurve;

    public VisualRgb24 Apply(VisualRgb24 rgb) => _parameters.MatchStrength == 0
        ? rgb : OklabColorSpace.ToSrgbGamutMapped(ApplyCore(OklabColorSpace.FromSrgb(rgb)));

    /// <summary>Applies the same look directly to an encoded float RGB sample.</summary>
    /// <remarks>The values are sRGB-encoded floats in the canonical processing buffer. This overload never constructs a VisualRgb24, so professional RAW processing does not quantize through an 8-bit display adapter.</remarks>
    public (float R, float G, float B) ApplyFloat(float r, float g, float b)
    {
        if (_parameters.MatchStrength == 0) return (r, g, b);
        var transformed = _algorithmVersion == 2
            ? OklabColorSpace.ToSrgbGamutMappedFloat(ApplyCore(OklabColorSpace.FromSrgb(r, g, b)))
            : OklabColorSpace.ToSrgbLinear(ApplyCore(OklabColorSpace.FromSrgb(r, g, b)));
        return ((float)transformed.R, (float)transformed.G, (float)transformed.B);
    }

    private OklabColor ApplyCore(OklabColor perceptual)
    {
        var p = _parameters;
        if (p.MatchStrength == 0 || (p.ToneStrength == 0 && p.ColorStrength == 0)) return perceptual;
        var bin = Math.Clamp((int)Math.Round(perceptual.L * 255), 0, 255); var effectiveTone = p.KeepOriginalTone ? 0 : p.ToneStrength;
        var mappedL = perceptual.L + (_toneCurve[bin] - perceptual.L) * effectiveTone / 100;
        mappedL += (mappedL - .5) * (_contrast - 1) * p.ContrastStrength / 100 * effectiveTone / 100;
        var weights = ReferenceColorTargetBuilder.SmoothZoneWeights(perceptual.L);
        var da = _target.Global.Center.A - _source.Global.Center.A; var db = _target.Global.Center.B - _source.Global.Center.B;
        for (var zone = 0; zone < 3; zone++)
        {
            var confidence = Math.Min(_source.Zones[zone].Confidence, _target.Zones[zone].Confidence);
            da += weights[zone] * confidence * (_target.Zones[zone].Center.A - _source.Zones[zone].Center.A);
            db += weights[zone] * confidence * (_target.Zones[zone].Center.B - _source.Zones[zone].Center.B);
        }
        da = Math.Clamp(da * .55, -.16, .16); db = Math.Clamp(db * .55, -.16, .16);
        var hue = Math.Atan2(perceptual.B, perceptual.A) * 180 / Math.PI; if (hue < 0) hue += 360;
        var skinCandidate = perceptual.L is > .28 and < .9 && hue is > 25 and < 80 && perceptual.Chroma is > .025 and < .22;
        var neutralCandidate = Math.Clamp(1 - perceptual.Chroma / .09, 0, 1);
        var protection = 1 - (_algorithmVersion == 2 ? ReferencePixelStatistics.SkinColorWeight(perceptual) * p.SkinProtection / 100 * .75 : skinCandidate ? p.SkinProtection / 100 * .75 : 0);
        protection *= 1 - neutralCandidate * p.NeutralProtection / 100 * .9;
        protection *= 1 - Math.Clamp((perceptual.L - .78) / .22, 0, 1) * p.HighlightProtection / 100;
        var strength = p.MatchStrength / 100 * protection;
        var toneStrength = _algorithmVersion == 2 ? p.MatchStrength / 100 * (1 - Math.Clamp((perceptual.L - .78) / .22, 0, 1) * p.HighlightProtection / 100) : strength;
        var chromaScale = 1 + (_saturation - 1) * p.SaturationStrength / 100 * p.ColorStrength / 100;
        return new(perceptual.L + (mappedL - perceptual.L) * toneStrength,
            perceptual.A + ((perceptual.A + da * p.ColorStrength / 100) * chromaScale - perceptual.A) * strength,
            perceptual.B + ((perceptual.B + db * p.ColorStrength / 100) * chromaScale - perceptual.B) * strength);
    }
}

/// <summary>Deterministic D65 OKLab global + smooth tonal-zone distribution mapping over a color-managed display proxy.
/// monitoring pixels. No file IO, sensor rendering, or semantic skin claims.</summary>
public sealed class ReferenceLookMatcher
{
    public ReferenceLookTransform BuildTransform(VisualPixelBuffer source, AssetVisualAnalysisResult analysis, ReferenceLook look, CancellationToken token = default)
    {
        look = look.Normalize();
        if (look.AlgorithmVersion == 2) return BuildCorrectedTransform(ReferencePixelStatistics.FromPixels(source, token), look);
        var targetHistogram = Enumerable.Range(0, 256).Select(bin => look.ReferenceSources.Sum(reference =>
            reference.Weight * reference.Analysis.HistogramLuma[bin] / Math.Max(1d, reference.Analysis.HistogramLuma.Sum(value => (double)value)))).ToArray();
        var curve = ReferenceToneMapper.BuildMonotonicQuantileCurve(analysis.HistogramLuma, targetHistogram);
        var sourceTarget = ReferenceColorTargetBuilder.FromPixels(source); var target = ReferenceColorTargetBuilder.FromLook(look);
        var sourceSpan = Math.Max(.06, analysis.ContrastMetric);
        var targetSpan = look.ReferenceSources.Sum(reference => reference.Weight * reference.Analysis.ContrastMetric);
        var contrast = Math.Clamp(targetSpan / sourceSpan, .8, 1.2);
        var targetSaturation = look.ReferenceSources.Sum(reference => reference.Weight * reference.Analysis.AverageSaturation);
        var saturation = Math.Clamp(targetSaturation / Math.Max(.08, analysis.AverageSaturation), .7, 1.3);
        return new(look.Parameters, sourceTarget, target, curve, contrast, saturation, new());
    }

    public ReferenceLookTransform BuildTransform(HighBitDepthImageBuffer source, AssetVisualAnalysisResult analysis, ReferenceLook look, CancellationToken token = default, ReadOnlyMemory<byte> alpha = default)
    {
        look = look.Normalize();
        if (look.AlgorithmVersion == 2) return BuildCorrectedTransform(ReferencePixelStatistics.FromPixels(source, token, alpha), look);
        var targetHistogram = Enumerable.Range(0, 256).Select(bin => look.ReferenceSources.Sum(reference =>
            reference.Weight * reference.Analysis.HistogramLuma[bin] / Math.Max(1d, reference.Analysis.HistogramLuma.Sum(value => (double)value)))).ToArray();
        var curve = ReferenceToneMapper.BuildMonotonicQuantileCurve(analysis.HistogramLuma, targetHistogram);
        var sourceTarget = ReferenceColorTargetBuilder.FromPixels(source); var target = ReferenceColorTargetBuilder.FromLook(look);
        var sourceSpan = Math.Max(.06, analysis.ContrastMetric);
        var targetSpan = look.ReferenceSources.Sum(reference => reference.Weight * reference.Analysis.ContrastMetric);
        var contrast = Math.Clamp(targetSpan / sourceSpan, .8, 1.2);
        var targetSaturation = look.ReferenceSources.Sum(reference => reference.Weight * reference.Analysis.AverageSaturation);
        var saturation = Math.Clamp(targetSaturation / Math.Max(.08, analysis.AverageSaturation), .7, 1.3);
        return new(look.Parameters, sourceTarget, target, curve, contrast, saturation, new("HighPrecision sRGB", "OKLabD65", "sRGB float"));
    }

    public ReferenceMatchResult Match(VisualPixelBuffer source, AssetVisualAnalysisResult analysis, ReferenceLook look, CancellationToken token = default)
    {
        look = look.Normalize(); var p = look.Parameters;
        // Identity must not depend on analyzable target pixels (e.g. transparent
        // inputs). Do not enter tone/color/statistical processing at zero strength.
        if (look.AlgorithmVersion == 2 && (p.MatchStrength == 0 || (p.ToneStrength == 0 && p.ColorStrength == 0)))
            return new(new(source.Width, source.Height, source.Rgb24.ToArray(), source.Alpha), p,
                new(Enumerable.Range(0, 256).Select(bin => bin / 255d).ToArray()),
                Pipeline: new("DisplayReferred sRGB", "OKLabD65", "sRGB", Version: 2));
        var targetHistogram = Enumerable.Range(0, 256).Select(bin => look.ReferenceSources.Sum(reference =>
            reference.Weight * reference.Analysis.HistogramLuma[bin] / Math.Max(1d, reference.Analysis.HistogramLuma.Sum(value => (double)value)))).ToArray();
        var corrected = look.AlgorithmVersion == 2 ? BuildTransform(source, analysis, look, token) : null;
        var curve = corrected is not null
            ? corrected.ToneCurve
            : ReferenceToneMapper.BuildMonotonicQuantileCurve(analysis.HistogramLuma, targetHistogram);
        var output = source.Rgb24.ToArray();
        if (p.MatchStrength == 0 || (p.ToneStrength == 0 && p.ColorStrength == 0))
            return new(new(source.Width, source.Height, output, source.Alpha), p, new(curve), Pipeline: new());
        var sourceTarget = ReferenceColorTargetBuilder.FromPixels(source); var target = ReferenceColorTargetBuilder.FromLook(look);
        var warning = ReferenceDifferenceAnalyzer.Compare(sourceTarget, target);
        var transform = corrected ?? BuildTransform(source, analysis, look, token);
        for (var pixel = 0; pixel < source.PixelCount; pixel++)
        {
            if ((pixel & 1023) == 0) token.ThrowIfCancellationRequested();
            var i = pixel * 3; var transformed = transform.Apply(new(output[i], output[i + 1], output[i + 2]));
            output[i] = transformed.R; output[i + 1] = transformed.G; output[i + 2] = transformed.B;
        }
        return new(new(source.Width, source.Height, output, source.Alpha), p, new(transform.ToneCurve), warning, transform.Pipeline);
    }
    private static ReferenceLookTransform BuildCorrectedTransform(ReferencePixelStatistics source, ReferenceLook look)
    {
        var target = ReferencePixelStatistics.Combine(look);
        // Same OKLab L domain at statistics, curve construction and application. Keep
        // the established bounded shift until real-photo comparisons justify a change.
        var sourceCounts = source.LightnessHistogram.Select(x => (uint)Math.Round(x * 65536 / source.LightnessHistogram.Sum())).ToArray();
        var curve = ReferenceToneMapper.BuildMonotonicQuantileCurve(sourceCounts, target.LightnessHistogram);
        var contrast = Math.Clamp(target.Span() / Math.Max(.06, source.Span()), .8, 1.2);
        var saturation = Math.Clamp(target.Colors.Global.Center.Chroma / Math.Max(.08, source.Colors.Global.Center.Chroma), .7, 1.3);
        return new(look.Parameters, source.Colors, target.Colors, curve, contrast, saturation, new("DisplayReferred sRGB", "OKLabD65", "sRGB", Version: 2), 2);
    }
    public static VisualRgb24 FromLab(VisualLab lab)
    {
        var fy = (lab.L + 16) / 116; var fx = fy + lab.A / 500; var fz = fy - lab.B / 200;
        static double Inverse(double f) => f * f * f > .008856 ? f * f * f : (f - 16d / 116) / 7.787;
        var x = .95047 * Inverse(fx); var y = Inverse(fy); var z = 1.08883 * Inverse(fz);
        static byte Encode(double v) => (byte)Math.Clamp(Math.Round(255 * (v <= .0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - .055)), 0, 255);
        return new(Encode(3.2404542 * x - 1.5371385 * y - .4985314 * z), Encode(-.969266 * x + 1.8760108 * y + .041556 * z), Encode(.0556434 * x - .2040259 * y + 1.0572252 * z));
    }
}
