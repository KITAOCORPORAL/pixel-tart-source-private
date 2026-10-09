using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Deterministic display-referred OKLab statistics; not scene-linear RAW or semantic skin detection.</summary>
public sealed record ReferencePixelStatistics(double[] LightnessHistogram, ReferenceColorTarget Colors,
    int SampleCount, double AlphaWeight, int Version = 1)
{
    public void Validate()
    {
        if (Version != 1 || LightnessHistogram.Length != 256 || SampleCount < 1 || !double.IsFinite(AlphaWeight) || AlphaWeight <= 0 ||
            LightnessHistogram.Any(x => !double.IsFinite(x) || x < 0) || LightnessHistogram.Sum() <= 0 || Colors.Zones.Count != 3)
            throw new ArgumentException("Reference pixel statistics are empty or invalid.");
        foreach (var zone in Colors.Zones.Append(Colors.Global))
            if (new[] { zone.Center.L, zone.Center.A, zone.Center.B, zone.ChromaSpread, zone.EffectiveSamples, zone.Confidence }.Any(x => !double.IsFinite(x)))
                throw new ArgumentException("Reference color statistics must be finite.");
    }
    public static ReferencePixelStatistics FromPixels(VisualPixelBuffer image, CancellationToken token = default) => Build(image.PixelCount, i =>
    {
        var offset = i * 3;
        return (OklabColorSpace.FromSrgb(new VisualRgb24(image.Rgb24.Span[offset], image.Rgb24.Span[offset + 1], image.Rgb24.Span[offset + 2])),
            image.Alpha.IsEmpty ? 1d : image.Alpha.Span[i] / 255d);
    }, token);
    public static ReferencePixelStatistics FromPixels(HighBitDepthImageBuffer image, CancellationToken token = default, ReadOnlyMemory<byte> alpha = default)
    {
        if (!alpha.IsEmpty && alpha.Length != image.PixelCount) throw new ArgumentException("Alpha must contain one value per pixel.");
        return Build(image.PixelCount, i =>
        {
            var offset = i * 3;
            return (OklabColorSpace.FromSrgb(image.Rgb32.Span[offset], image.Rgb32.Span[offset + 1], image.Rgb32.Span[offset + 2]), alpha.IsEmpty ? 1d : alpha.Span[i] / 255d);
        }, token);
    }
    private static ReferencePixelStatistics Build(int pixels, Func<int, (OklabColor Color, double Alpha)> read, CancellationToken token)
    {
        // Stratified pixel positions bound memory/work while representing the whole proxy.
        var count = Math.Min(pixels, 65536); var histogram = new double[256];
        var accumulators = Enumerable.Range(0, 4).Select(_ => new Accumulator()).ToArray(); var valid = 0;
        for (var sample = 0; sample < count; sample++)
        {
            if ((sample & 1023) == 0) token.ThrowIfCancellationRequested();
            var (color, alpha) = read((int)((long)sample * pixels / count));
            if (alpha <= 0 || !double.IsFinite(color.L) || !double.IsFinite(color.A) || !double.IsFinite(color.B)) continue;
            valid++; histogram[Math.Clamp((int)Math.Round(color.L * 255), 0, 255)] += alpha;
            accumulators[0].Add(color, alpha);
            var weights = ReferenceColorTargetBuilder.SmoothZoneWeights(color.L);
            for (var zone = 0; zone < 3; zone++) accumulators[zone + 1].Add(color, alpha * weights[zone]);
        }
        if (valid == 0) throw new ArgumentException("No visible finite pixels for reference matching.");
        var global = accumulators[0].Result();
        return new(histogram, new(global, accumulators.Skip(1).Select(x => x.Result()).ToArray(), global.Center.L), valid, accumulators[0].Weight);
    }
    private sealed class Accumulator
    {
        public double Weight; private double _squaredWeight, _l, _a, _b, _c, _c2;
        public void Add(OklabColor color, double weight)
        { Weight += weight; _squaredWeight += weight * weight; _l += color.L * weight; _a += color.A * weight; _b += color.B * weight; _c += color.Chroma * weight; _c2 += color.Chroma * color.Chroma * weight; }
        public ReferenceZoneStatistic Result()
        {
            if (Weight < 1e-12) return new(new(.5, 0, 0), 0, 0, 0);
            var effective = Weight * Weight / Math.Max(1e-12, _squaredWeight);
            return new(new(_l / Weight, _a / Weight, _b / Weight), Math.Sqrt(Math.Max(0, _c2 / Weight - Math.Pow(_c / Weight, 2))), effective, Math.Clamp(effective / 12, 0, 1));
        }
    }
    public static ReferencePixelStatistics Combine(ReferenceLook look)
    {
        var sources = look.Normalize().ReferenceSources.Where(x => x.Weight > 0).ToArray();
        var histogram = Enumerable.Range(0, 256).Select(bin => sources.Sum(x => x.Weight * x.PixelStatistics!.LightnessHistogram[bin] / x.PixelStatistics.LightnessHistogram.Sum())).ToArray();
        ReferenceZoneStatistic CombineZone(Func<ReferencePixelStatistics, ReferenceZoneStatistic> select)
        {
            double Mean(Func<ReferenceZoneStatistic, double> field) => sources.Sum(x => x.Weight * field(select(x.PixelStatistics!)));
            return new(new(Mean(x => x.Center.L), Mean(x => x.Center.A), Mean(x => x.Center.B)), Mean(x => x.ChromaSpread), Mean(x => x.EffectiveSamples), Mean(x => x.Confidence));
        }
        var global = CombineZone(x => x.Colors.Global);
        return new(histogram, new(global, Enumerable.Range(0, 3).Select(zone => CombineZone(x => x.Colors.Zones[zone])).ToArray(), global.Center.L),
            sources.Sum(x => x.PixelStatistics!.SampleCount), sources.Sum(x => x.Weight * x.PixelStatistics!.AlphaWeight));
    }
    public double Span()
    {
        double Quantile(double p) { var sum = LightnessHistogram.Sum(); var cumulative = 0d; for (var i = 0; i < 256; i++) { cumulative += LightnessHistogram[i]; if (cumulative >= sum * p) return i / 255d; } return 1; }
        return Quantile(.95) - Quantile(.05);
    }
    public static double SkinColorWeight(OklabColor color)
    {
        static double Smooth(double low, double high, double x) { var t = Math.Clamp((x - low) / (high - low), 0, 1); return t * t * (3 - 2 * t); }
        var hue = Math.Atan2(color.B, color.A) * 180 / Math.PI; if (hue < 0) hue += 360;
        return Smooth(15, 30, hue) * (1 - Smooth(70, 90, hue)) * Smooth(.12, .32, color.L) * (1 - Smooth(.85, .98, color.L)) * Smooth(.01, .035, color.Chroma) * (1 - Smooth(.18, .25, color.Chroma));
    }
}
