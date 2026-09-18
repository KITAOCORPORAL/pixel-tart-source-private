using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.Projects;

public sealed record ColorPipelineDescriptor(
    string InputInterpretation = "DisplayReferred",
    string WorkingRepresentation = "OKLabD65",
    string OutputEncoding = "sRGB",
    bool InputAlreadyColorManaged = true,
    int Version = 1);

public readonly record struct OklabColor(double L, double A, double B)
{
    public double Chroma => Math.Sqrt(A * A + B * B);
}

/// <summary>Public OKLab definition (D65) plus perceptual chroma compression for sRGB output.</summary>
public static class OklabColorSpace
{
    public static OklabColor FromSrgb(VisualRgb24 value)
    {
        var r = Decode(value.R / 255d); var g = Decode(value.G / 255d); var b = Decode(value.B / 255d);
        var l = .4122214708 * r + .5363325363 * g + .0514459929 * b;
        var m = .2119034982 * r + .6806995451 * g + .1073969566 * b;
        var s = .0883024619 * r + .2817188376 * g + .6299787005 * b;
        var l3 = Math.Cbrt(l); var m3 = Math.Cbrt(m); var s3 = Math.Cbrt(s);
        return new(.2104542553 * l3 + .7936177850 * m3 - .0040720468 * s3,
            1.9779984951 * l3 - 2.4285922050 * m3 + .4505937099 * s3,
            .0259040371 * l3 + .7827717662 * m3 - .8086757660 * s3);
    }

    public static VisualRgb24 ToSrgb(OklabColor value) => Encode(ToLinear(value));

    public static VisualRgb24 ToSrgbGamutMapped(OklabColor value)
    {
        var linear = ToLinear(value);
        if (InGamut(linear)) return Encode(linear);
        var low = 0d; var high = 1d;
        for (var iteration = 0; iteration < 22; iteration++)
        {
            var scale = (low + high) / 2;
            if (InGamut(ToLinear(value with { A = value.A * scale, B = value.B * scale }))) low = scale; else high = scale;
        }
        return Encode(ToLinear(value with { A = value.A * low, B = value.B * low }));
    }

    public static (double R, double G, double B) ToLinear(OklabColor value)
    {
        var l = value.L + .3963377774 * value.A + .2158037573 * value.B;
        var m = value.L - .1055613458 * value.A - .0638541728 * value.B;
        var s = value.L - .0894841775 * value.A - 1.2914855480 * value.B;
        l *= l * l; m *= m * m; s *= s * s;
        return (4.0767416621 * l - 3.3077115913 * m + .2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - .3413193965 * s,
            -.0041960863 * l - .7034186147 * m + 1.7076147010 * s);
    }

    private static bool InGamut((double R, double G, double B) value) => value.R is >= 0 and <= 1 && value.G is >= 0 and <= 1 && value.B is >= 0 and <= 1;
    private static double Decode(double value) => value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
    private static VisualRgb24 Encode((double R, double G, double B) value)
    {
        static byte Channel(double linear)
        {
            linear = Math.Clamp(linear, 0, 1);
            var encoded = linear <= .0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1 / 2.4) - .055;
            return (byte)Math.Clamp(Math.Round(encoded * 255), 0, 255);
        }
        return new(Channel(value.R), Channel(value.G), Channel(value.B));
    }
}

public sealed record ReferenceZoneStatistic(OklabColor Center, double ChromaSpread, double EffectiveSamples, double Confidence);
public sealed record ReferenceColorTarget(ReferenceZoneStatistic Global, IReadOnlyList<ReferenceZoneStatistic> Zones, double AverageLuminance);

public static class ReferenceToneMapper
{
    public static double[] BuildMonotonicQuantileCurve(IReadOnlyList<uint> source, IReadOnlyList<double> target, int maximumShift = 32)
    {
        if (source.Count != 256 || target.Count != 256) throw new ArgumentException("Tone histograms must contain 256 bins.");
        static double[] Cdf(IEnumerable<double> values) { var sum = Math.Max(1e-12, values.Sum()); var running = 0d; return values.Select(value => running += value / sum).ToArray(); }
        var sourceCdf = Cdf(source.Select(value => (double)value)); var targetCdf = Cdf(target);
        var curve = new double[256]; var targetIndex = 0;
        for (var bin = 0; bin < 256; bin++)
        {
            while (targetIndex < 255 && targetCdf[targetIndex] < sourceCdf[bin]) targetIndex++;
            var bounded = Math.Clamp(targetIndex, bin - maximumShift, bin + maximumShift);
            var candidate = Math.Clamp(bin + (bounded - bin) * .65, 0, 255) / 255d;
            curve[bin] = Math.Max(bin == 0 ? 0 : curve[bin - 1], candidate);
        }
        return curve;
    }
}

public static class ReferenceColorTargetBuilder
{
    public static ReferenceColorTarget FromLook(ReferenceLook look)
    {
        look = look.Normalize();
        var samples = look.ReferenceSources.SelectMany(reference => reference.Analysis.Palette.Select(color =>
            (Color: OklabColorSpace.FromSrgb(color.Rgb), Weight: reference.Weight * color.Weight))).ToArray();
        var zones = Enumerable.Range(0, 3).Select(zone => Build(samples, zone)).ToArray();
        var global = Build(samples, null);
        var luminance = look.ReferenceSources.Sum(reference => reference.Weight * reference.Analysis.AverageLuma / 255d);
        return new(global, zones, luminance);
    }

    public static ReferenceColorTarget FromPixels(VisualPixelBuffer pixels)
    {
        var samples = new List<(OklabColor Color, double Weight)>(pixels.PixelCount);
        for (var pixel = 0; pixel < pixels.PixelCount; pixel++)
        {
            var offset = pixel * 3; samples.Add((OklabColorSpace.FromSrgb(new(pixels.Rgb24.Span[offset], pixels.Rgb24.Span[offset + 1], pixels.Rgb24.Span[offset + 2])), 1));
        }
        return new(Build(samples, null), Enumerable.Range(0, 3).Select(zone => Build(samples, zone)).ToArray(), samples.Average(sample => sample.Color.L));
    }

    public static double[] SmoothZoneWeights(double lightness)
    {
        static double Smooth(double a, double b, double x) { var t = Math.Clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); }
        var shadow = 1 - Smooth(.18, .52, lightness); var highlight = Smooth(.52, .86, lightness); var mid = Math.Max(0, 1 - shadow - highlight);
        var total = Math.Max(1e-12, shadow + mid + highlight); return [shadow / total, mid / total, highlight / total];
    }

    private static ReferenceZoneStatistic Build(IEnumerable<(OklabColor Color, double Weight)> source, int? zone)
    {
        var weighted = source.Select(sample =>
        {
            var zoneWeight = zone is null ? 1 : SmoothZoneWeights(sample.Color.L)[zone.Value];
            return (sample.Color, Weight: sample.Weight * zoneWeight);
        }).Where(sample => sample.Weight > 1e-12).ToArray();
        var sum = weighted.Sum(sample => sample.Weight);
        if (sum <= 1e-12) return new(new(.5, 0, 0), 0, 0, 0);
        var center = new OklabColor(weighted.Sum(sample => sample.Color.L * sample.Weight) / sum,
            weighted.Sum(sample => sample.Color.A * sample.Weight) / sum, weighted.Sum(sample => sample.Color.B * sample.Weight) / sum);
        var spread = Math.Sqrt(weighted.Sum(sample => Math.Pow(sample.Color.Chroma - center.Chroma, 2) * sample.Weight) / sum);
        var effective = sum * sum / Math.Max(1e-12, weighted.Sum(sample => sample.Weight * sample.Weight));
        return new(center, spread, effective, Math.Clamp(effective / 12d, 0, 1));
    }
}

public sealed record ReferenceDifferenceWarning(bool ToneDifferenceLarge, bool ColorDifferenceLarge)
{
    public string? UserMessage => ToneDifferenceLarge
        ? "参考图与当前照片的影调差异较大，可降低影调强度或保持原片影调。"
        : ColorDifferenceLarge ? "当前参考的色彩分布差异较大，建议适当降低仿色强度。" : null;
}

public static class ReferenceDifferenceAnalyzer
{
    public static ReferenceDifferenceWarning Compare(ReferenceColorTarget source, ReferenceColorTarget target) => new(
        Math.Abs(source.AverageLuminance - target.AverageLuminance) > .24,
        Math.Sqrt(Math.Pow(source.Global.Center.A - target.Global.Center.A, 2) + Math.Pow(source.Global.Center.B - target.Global.Center.B, 2)) > .11);
}
