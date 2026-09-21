using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

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
    string ContentHash, double Weight, AssetVisualAnalysisResult Analysis, string Kind = "Asset", Guid? ContainerId = null);
public sealed record ReferenceLook(Guid ReferenceLookId, string Name, Guid? ProjectId,
    IReadOnlyList<ReferenceLookSource> ReferenceSources, ReferenceLookParameters Parameters,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version = 1,
    PixelTartFilmSettings? Film = null)
{
    public ReferenceLook Normalize()
    {
        Parameters.Validate();
        Film?.Validate();
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

    internal ReferenceLookTransform(ReferenceLookParameters parameters, ReferenceColorTarget source, ReferenceColorTarget target,
        double[] toneCurve, double contrast, double saturation, ColorPipelineDescriptor pipeline)
    {
        _parameters = parameters; _source = source; _target = target; _toneCurve = toneCurve;
        _contrast = contrast; _saturation = saturation; Pipeline = pipeline;
    }

    public ColorPipelineDescriptor Pipeline { get; }
    public IReadOnlyList<double> ToneCurve => _toneCurve;

    public VisualRgb24 Apply(VisualRgb24 rgb)
    {
        var p = _parameters;
        if (p.MatchStrength == 0 || (p.ToneStrength == 0 && p.ColorStrength == 0)) return rgb;
        var perceptual = OklabColorSpace.FromSrgb(rgb);
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
        var protection = 1 - (skinCandidate ? p.SkinProtection / 100 * .75 : 0);
        protection *= 1 - neutralCandidate * p.NeutralProtection / 100 * .9;
        protection *= 1 - Math.Clamp((perceptual.L - .78) / .22, 0, 1) * p.HighlightProtection / 100;
        var strength = p.MatchStrength / 100 * protection;
        var chromaScale = 1 + (_saturation - 1) * p.SaturationStrength / 100 * p.ColorStrength / 100;
        return OklabColorSpace.ToSrgbGamutMapped(new(perceptual.L + (mappedL - perceptual.L) * strength,
            perceptual.A + ((perceptual.A + da * p.ColorStrength / 100) * chromaScale - perceptual.A) * strength,
            perceptual.B + ((perceptual.B + db * p.ColorStrength / 100) * chromaScale - perceptual.B) * strength));
    }
}

/// <summary>Deterministic D65 OKLab global + smooth tonal-zone distribution mapping over a color-managed display proxy.
/// monitoring pixels. No file IO, sensor rendering, or semantic skin claims.</summary>
public sealed class ReferenceLookMatcher
{
    public ReferenceLookTransform BuildTransform(VisualPixelBuffer source, AssetVisualAnalysisResult analysis, ReferenceLook look)
    {
        look = look.Normalize();
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

    public ReferenceMatchResult Match(VisualPixelBuffer source, AssetVisualAnalysisResult analysis, ReferenceLook look, CancellationToken token = default)
    {
        look = look.Normalize(); var p = look.Parameters;
        var targetHistogram = Enumerable.Range(0, 256).Select(bin => look.ReferenceSources.Sum(reference =>
            reference.Weight * reference.Analysis.HistogramLuma[bin] / Math.Max(1d, reference.Analysis.HistogramLuma.Sum(value => (double)value)))).ToArray();
        var curve = ReferenceToneMapper.BuildMonotonicQuantileCurve(analysis.HistogramLuma, targetHistogram);
        var output = source.Rgb24.ToArray();
        if (p.MatchStrength == 0 || (p.ToneStrength == 0 && p.ColorStrength == 0))
            return new(new(source.Width, source.Height, output), p, new(curve), Pipeline: new());
        var sourceTarget = ReferenceColorTargetBuilder.FromPixels(source); var target = ReferenceColorTargetBuilder.FromLook(look);
        var warning = ReferenceDifferenceAnalyzer.Compare(sourceTarget, target);
        var transform = BuildTransform(source, analysis, look);
        for (var pixel = 0; pixel < source.PixelCount; pixel++)
        {
            if ((pixel & 1023) == 0) token.ThrowIfCancellationRequested();
            var i = pixel * 3; var transformed = transform.Apply(new(output[i], output[i + 1], output[i + 2]));
            output[i] = transformed.R; output[i + 1] = transformed.G; output[i + 2] = transformed.B;
        }
        return new(new(source.Width, source.Height, output), p, new(curve), warning, transform.Pipeline);
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
