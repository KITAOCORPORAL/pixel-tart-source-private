using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.Projects;

public sealed record ReferenceLookParameters(double MatchStrength = 100, double ToneStrength = 50,
    double ColorStrength = 70, double ContrastStrength = 50, double SaturationStrength = 50,
    double SkinProtection = 60, double HighlightProtection = 70)
{
    public void Validate()
    {
        if (new[] { MatchStrength, ToneStrength, ColorStrength, ContrastStrength, SaturationStrength, SkinProtection, HighlightProtection }
            .Any(value => !double.IsFinite(value) || value < 0 || value > 100)) throw new ArgumentException("Look strengths must be between 0 and 100.");
    }
}
public sealed record ReferenceLookSource(Guid LibraryId, Guid? AssetId, string Name, string SourcePath,
    string ContentHash, double Weight, AssetVisualAnalysisResult Analysis, string Kind = "Asset", Guid? ContainerId = null);
public sealed record ReferenceLook(Guid ReferenceLookId, string Name, Guid? ProjectId,
    IReadOnlyList<ReferenceLookSource> ReferenceSources, ReferenceLookParameters Parameters,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version = 1)
{
    public ReferenceLook Normalize()
    {
        Parameters.Validate();
        if (ReferenceLookId == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Version != 1 || ReferenceSources.Count == 0 ||
            ReferenceSources.Any(source => !double.IsFinite(source.Weight) || source.Weight < 0 || source.Analysis.Palette.Count == 0 ||
                source.Analysis.HistogramLuma.Length != 256)) throw new ArgumentException("Look identity, references and weights must be valid.");
        var sum = ReferenceSources.Sum(source => source.Weight);
        if (!double.IsFinite(sum) || sum <= 0) throw new ArgumentException("At least one reference must have positive weight.");
        return this with { Name = Name.Trim(), ReferenceSources = ReferenceSources.Select(source => source with { Weight = source.Weight / sum }).ToArray() };
    }
}
public sealed record ReferenceToneCurve(IReadOnlyList<double> LuminanceKnots);
public sealed record ReferenceMatchResult(VisualPixelBuffer Preview, ReferenceLookParameters Parameters, ReferenceToneCurve ToneCurve);

/// <summary>Deterministic D65 CIELAB palette-distribution mapping over normalized sRGB
/// monitoring pixels. No file IO, sensor rendering, or semantic skin claims.</summary>
public sealed class ReferenceLookMatcher
{
    public ReferenceMatchResult Match(VisualPixelBuffer source, AssetVisualAnalysisResult analysis, ReferenceLook look, CancellationToken token = default)
    {
        look = look.Normalize(); var p = look.Parameters;
        var targetHistogram = Enumerable.Range(0, 256).Select(bin => look.ReferenceSources.Sum(reference =>
            reference.Weight * reference.Analysis.HistogramLuma[bin] / Math.Max(1d, reference.Analysis.HistogramLuma.Sum(value => (double)value)))).ToArray();
        var sourceHistogram = analysis.HistogramLuma.Select(value => (double)value).ToArray();
        var curve = ToneCurve(sourceHistogram, targetHistogram);
        var output = source.Rgb24.ToArray();
        if (p.MatchStrength == 0 || (p.ToneStrength == 0 && p.ColorStrength == 0))
            return new(new(source.Width, source.Height, output), p, new(curve));
        var targetPalette = look.ReferenceSources.SelectMany(reference => reference.Analysis.Palette.Select(color => (color, Weight: color.Weight * reference.Weight))).ToArray();
        var sourceMeanA = analysis.Palette.Sum(color => color.Lab.A * color.Weight);
        var sourceMeanB = analysis.Palette.Sum(color => color.Lab.B * color.Weight);
        var targetMeanA = targetPalette.Sum(item => item.color.Lab.A * item.Weight);
        var targetMeanB = targetPalette.Sum(item => item.color.Lab.B * item.Weight);
        var sourceSpan = Math.Max(.06, analysis.ContrastMetric);
        var targetSpan = look.ReferenceSources.Sum(reference => reference.Weight * reference.Analysis.ContrastMetric);
        var contrast = Math.Clamp(targetSpan / sourceSpan, .8, 1.2);
        var targetSaturation = look.ReferenceSources.Sum(reference => reference.Weight * reference.Analysis.AverageSaturation);
        var saturation = Math.Clamp(targetSaturation / Math.Max(.08, analysis.AverageSaturation), .7, 1.3);
        for (var pixel = 0; pixel < source.PixelCount; pixel++)
        {
            if ((pixel & 1023) == 0) token.ThrowIfCancellationRequested();
            var i = pixel * 3; var rgb = new VisualRgb24(output[i], output[i + 1], output[i + 2]);
            var lab = VisualAnalysisEngine.ToLab(rgb);
            var luma = LToY(lab.L); var bin = Math.Clamp((int)Math.Round(luma * 255), 0, 255);
            var toneL = YToL(curve[bin]);
            // ToneStrength gates all lightness/contrast changes; 0 cannot alter tone.
            var mappedL = lab.L + (toneL - lab.L) * p.ToneStrength / 100;
            mappedL += (mappedL - 50) * (contrast - 1) * p.ContrastStrength / 100 * p.ToneStrength / 100;
            var nearest = analysis.Palette.MinBy(color => Math.Pow(color.Lab.A - lab.A, 2) + Math.Pow(color.Lab.B - lab.B, 2))!;
            var target = targetPalette.MinBy(item => Math.Pow(item.color.Lab.A - targetMeanA - (nearest.Lab.A - sourceMeanA), 2) +
                Math.Pow(item.color.Lab.B - targetMeanB - (nearest.Lab.B - sourceMeanB), 2));
            // Separate neutral/WB shift from creative residual, and bound both.
            var da = Math.Clamp((targetMeanA - sourceMeanA) * .15 + (target.color.Lab.A - targetMeanA - nearest.Lab.A + sourceMeanA) * .6, -18, 18);
            var db = Math.Clamp((targetMeanB - sourceMeanB) * .15 + (target.color.Lab.B - targetMeanB - nearest.Lab.B + sourceMeanB) * .6, -18, 18);
            var skinCandidate = lab.L is > 25 and < 90 && lab.A is > 4 and < 32 && lab.B is > 7 and < 40;
            var protection = 1 - (skinCandidate ? p.SkinProtection / 100 * .75 : 0);
            protection *= 1 - Math.Clamp((lab.L - 80) / 20, 0, 1) * p.HighlightProtection / 100;
            var strength = p.MatchStrength / 100 * protection;
            var chromaScale = 1 + (saturation - 1) * p.SaturationStrength / 100 * p.ColorStrength / 100;
            var mapped = new VisualLab(lab.L + (mappedL - lab.L) * strength,
                lab.A + ((lab.A + da * p.ColorStrength / 100) * chromaScale - lab.A) * strength,
                lab.B + ((lab.B + db * p.ColorStrength / 100) * chromaScale - lab.B) * strength);
            var transformed = FromLab(mapped); output[i] = transformed.R; output[i + 1] = transformed.G; output[i + 2] = transformed.B;
        }
        return new(new(source.Width, source.Height, output), p, new(curve));
    }

    private static double[] ToneCurve(double[] source, double[] target)
    {
        static double[] Cdf(double[] values) { var sum = Math.Max(1e-10, values.Sum()); var running = 0d; return values.Select(value => running += value / sum).ToArray(); }
        static int Quantile(double[] cdf, double q) { var index = Array.FindIndex(cdf, value => value >= q); return index < 0 ? 255 : index; }
        var s = Cdf(source); var t = Cdf(target);
        var sb = Quantile(s, .02); var sw = Quantile(s, .98); var sm = Quantile(s, .5);
        var tb = Quantile(t, .02); var tw = Quantile(t, .98); var tm = Quantile(t, .5);
        var span = Math.Max(1, sw - sb); var targetSpan = Math.Max(1, tw - tb);
        return Enumerable.Range(0, 256).Select(bin =>
        {
            var quantile = Quantile(t, s[bin]);
            var normalized = sm + (quantile - tm) * span / (double)targetSpan;
            // A dark reference is not an instruction to underexpose the current image.
            return Math.Clamp(bin + Math.Clamp(normalized - bin, -24, 24) * .5, 0, 255) / 255;
        }).ToArray();
    }
    private static double LToY(double l) => l > 8 ? Math.Pow((l + 16) / 116, 3) : l / 903.3;
    private static double YToL(double y) => y > .008856 ? 116 * Math.Cbrt(y) - 16 : 903.3 * y;
    public static VisualRgb24 FromLab(VisualLab lab)
    {
        var fy = (lab.L + 16) / 116; var fx = fy + lab.A / 500; var fz = fy - lab.B / 200;
        static double Inverse(double f) => f * f * f > .008856 ? f * f * f : (f - 16d / 116) / 7.787;
        var x = .95047 * Inverse(fx); var y = Inverse(fy); var z = 1.08883 * Inverse(fz);
        static byte Encode(double v) => (byte)Math.Clamp(Math.Round(255 * (v <= .0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - .055)), 0, 255);
        return new(Encode(3.2404542 * x - 1.5371385 * y - .4985314 * z), Encode(-.969266 * x + 1.8760108 * y + .041556 * z), Encode(.0556434 * x - .2040259 * y + 1.0572252 * z));
    }
}
