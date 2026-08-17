using System.Security.Cryptography;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

public static class VisualAnalysisEngine
{
    private const int PaletteSampleLimit = 16_384;

    public static AssetVisualAnalysisResult Analyze(AssetVisualAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        if (request.PaletteSize is not (3 or 5 or 7)) throw new ArgumentOutOfRangeException(nameof(request.PaletteSize));
        if (!request.PixelsConvertedToAnalysisProfile) throw new InvalidOperationException("Pixel buffer must be converted to the declared analysis profile before analysis.");
        var bytes = request.Pixels.Rgb24.Span;
        var histR = new uint[256]; var histG = new uint[256]; var histB = new uint[256]; var histLuma = new uint[256];
        var zoneCounts = new long[5];
        var lumaValues = new byte[request.Pixels.PixelCount];
        var saturationValues = new double[request.Pixels.PixelCount];
        double lumaSum = 0; double saturationSum = 0; double lightnessSum = 0; double warmCoolSum = 0; double warmCoolWeight = 0; double hueX = 0; double hueY = 0; double hueWeight = 0;
        for (var pixel = 0; pixel < request.Pixels.PixelCount; pixel++)
        {
            if ((pixel & 0x3fff) == 0) cancellationToken.ThrowIfCancellationRequested();
            var offset = pixel * 3; var r = bytes[offset]; var g = bytes[offset + 1]; var b = bytes[offset + 2];
            histR[r]++; histG[g]++; histB[b]++;
            var luma = (byte)Math.Clamp((int)Math.Round(255 * LinearLuma(r, g, b)), 0, 255);
            histLuma[luma]++; lumaValues[pixel] = luma; lumaSum += luma;
            zoneCounts[luma < 32 ? 0 : luma < 80 ? 1 : luma < 176 ? 2 : luma < 224 ? 3 : 4]++;
            var hsl = RgbToHsl(r, g, b); saturationValues[pixel] = hsl.S; saturationSum += hsl.S; lightnessSum += hsl.L;
            if (hsl.S >= 0.08)
            {
                var radians = hsl.H * Math.PI / 180; hueX += Math.Cos(radians) * hsl.S; hueY += Math.Sin(radians) * hsl.S; hueWeight += hsl.S;
                var warm = hsl.H >= 330 || hsl.H <= 90; var cool = hsl.H is >= 150 and <= 270;
                if (warm || cool) { warmCoolSum += (warm ? 1 : -1) * hsl.S; warmCoolWeight += hsl.S; }
            }
        }
        Array.Sort(lumaValues);
        Array.Sort(saturationValues);
        var count = request.Pixels.PixelCount;
        var averageLuma = lumaSum / count;
        var medianLuma = Percentile(lumaValues, 0.5);
        var p1 = Percentile(lumaValues, 0.01); var p5 = Percentile(lumaValues, 0.05); var p95 = Percentile(lumaValues, 0.95); var p99 = Percentile(lumaValues, 0.99);
        var contrastMetric = (p95 - p5) / 255.0; var spanMetric = (p99 - p1) / 255.0;
        var blackClip = (histLuma[0] + histLuma[1] + histLuma[2]) / (double)count;
        var whiteClip = (histLuma[253] + histLuma[254] + histLuma[255]) / (double)count;
        var palette = ExtractPalette(request.Pixels, request.PaletteSize, request.ContentHash, cancellationToken);
        palette = request.PaletteSort switch
        {
            PaletteSortMode.Lightness => palette.OrderBy(x => x.Lab.L).ToArray(),
            PaletteSortMode.Hue => palette.OrderBy(x => x.Hue).ToArray(),
            _ => palette.OrderByDescending(x => x.Weight).ToArray()
        };
        var averageSaturation = saturationSum / count;
        var hueVectorMagnitude = hueWeight > 0 ? Math.Sqrt(hueX * hueX + hueY * hueY) / hueWeight : 0;
        double? averageHue = hueVectorMagnitude >= .01 ? NormalizeHue(Math.Atan2(hueY, hueX) * 180 / Math.PI) : null;
        var materialPalette = palette.Where(color => color.Weight >= AssetVisualFeatureContract.MinimumPaletteWeight && color.Saturation >= AssetVisualFeatureContract.MinimumChromaticSaturation && Math.Sqrt(color.Lab.A * color.Lab.A + color.Lab.B * color.Lab.B) >= AssetVisualFeatureContract.MinimumChromaticLabChroma).OrderByDescending(color => color.Weight).ThenBy(color => color.Hex, StringComparer.Ordinal).ToArray();
        var dominantHue = materialPalette.FirstOrDefault()?.Hue ?? 0;
        var secondaryHue = materialPalette.Skip(1).FirstOrDefault()?.Hue;
        var warmCoolMetric = warmCoolWeight > 0 ? warmCoolSum / warmCoolWeight : 0;
        var denominator = (double)count;
        return new AssetVisualAnalysisResult(
            request.AssetId, request.ContentHash, AssetVisualAnalysisResult.CurrentVersion, request.PaletteSize, request.PaletteSort,
            request.AnalysisSource, request.SourceProfile, request.AnalysisProfile,
            palette, ClassifyHarmony(palette, averageSaturation), histR, histG, histB, histLuma,
            new(zoneCounts[0] / denominator, zoneCounts[1] / denominator, zoneCounts[2] / denominator, zoneCounts[3] / denominator, zoneCounts[4] / denominator),
            averageLuma, medianLuma, blackClip, whiteClip, contrastMetric,
            contrastMetric < VisualClassificationThresholds.LowContrastMaximum ? ContrastTendency.Low : contrastMetric < VisualClassificationThresholds.MediumContrastMaximum ? ContrastTendency.Medium : ContrastTendency.High,
            spanMetric, spanMetric < VisualClassificationThresholds.NarrowLuminanceSpanMaximum ? LuminanceSpanTendency.Narrow : spanMetric < VisualClassificationThresholds.MediumLuminanceSpanMaximum ? LuminanceSpanTendency.Medium : LuminanceSpanTendency.Wide,
            medianLuma < VisualClassificationThresholds.LowToneMedianMaximum ? ToneKeyTendency.Low : medianLuma > VisualClassificationThresholds.HighToneMedianMinimum ? ToneKeyTendency.High : ToneKeyTendency.Mid,
            averageSaturation, dominantHue, averageSaturation < VisualClassificationThresholds.LowSaturationMaximum ? SaturationTendency.Low : averageSaturation < VisualClassificationThresholds.MediumSaturationMaximum ? SaturationTendency.Medium : SaturationTendency.High,
            warmCoolMetric, Math.Abs(warmCoolMetric) < VisualClassificationThresholds.NeutralWarmCoolMagnitudeMaximum ? WarmCoolTendency.Neutral : warmCoolMetric > 0 ? WarmCoolTendency.Warm : WarmCoolTendency.Cool,
            DateTimeOffset.UtcNow)
        {
            SourceContentHash = request.SourceContentHash,
            PreviousSourceContentHash = request.PreviousSourceContentHash,
            SecondaryHue = secondaryHue,
            AverageHue = averageHue,
            MedianSaturation = Percentile(saturationValues, .5),
            AverageLightness = lightnessSum / count,
            HistogramLumaSignature = HistogramSignature(histLuma),
            PaletteSignature = string.Join("|", palette.Select(color => $"{color.Hex}:{color.Weight:F6}")),
            HasDominantChromaticColor = materialPalette.Length > 0
        };
    }

    public static ColorDerivatives Derive(VisualRgb24 color)
    {
        var hsl = RgbToHsl(color.R, color.G, color.B);
        VisualRgb24 Rotate(double angle, double? lightness = null) => HslToRgb(NormalizeHue(hsl.H + angle), hsl.S, lightness ?? hsl.L);
        return new(Rotate(180), [Rotate(-30), Rotate(30)], [Rotate(120), Rotate(240)], [Rotate(150), Rotate(210)], [Rotate(0, .25), Rotate(0, .4), Rotate(0, .6), Rotate(0, .75)]);
    }

    public static VisualLab ToLab(VisualRgb24 color)
    {
        static double Linear(byte value) { var x = value / 255.0; return x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4); }
        var r = Linear(color.R); var g = Linear(color.G); var b = Linear(color.B);
        var x = (r * .4124564 + g * .3575761 + b * .1804375) / .95047;
        var y = r * .2126729 + g * .7151522 + b * .0721750;
        var z = (r * .0193339 + g * .1191920 + b * .9503041) / 1.08883;
        static double F(double value) => value > .008856 ? Math.Cbrt(value) : 7.787 * value + 16.0 / 116;
        var fx = F(x); var fy = F(y); var fz = F(z);
        return new(116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    private static double LinearLuma(byte red, byte green, byte blue)
    {
        static double Linear(byte value) { var x = value / 255.0; return x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4); }
        return .2126 * Linear(red) + .7152 * Linear(green) + .0722 * Linear(blue);
    }

    public static double DeltaE(VisualLab left, VisualLab right) => Math.Sqrt(Math.Pow(left.L - right.L, 2) + Math.Pow(left.A - right.A, 2) + Math.Pow(left.B - right.B, 2));

    private static IReadOnlyList<DominantColor> ExtractPalette(VisualPixelBuffer pixels, int k, string seedText, CancellationToken cancellationToken)
    {
        var total = pixels.PixelCount; var sampleCount = Math.Min(total, PaletteSampleLimit); var step = Math.Max(1, total / sampleCount); var bytes = pixels.Rgb24.Span;
        var samples = new List<(VisualRgb24 Rgb, VisualLab Lab)>(sampleCount);
        var seed = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seedText ?? string.Empty));
        var start = BitConverter.ToUInt32(seed, 0) % (uint)step;
        for (var pixel = (int)start; pixel < total && samples.Count < sampleCount; pixel += step)
        {
            var offset = pixel * 3; var rgb = new VisualRgb24(bytes[offset], bytes[offset + 1], bytes[offset + 2]); samples.Add((rgb, ToLab(rgb)));
        }
        var centroids = new List<VisualLab> { samples[(int)(BitConverter.ToUInt32(seed, 4) % (uint)samples.Count)].Lab };
        while (centroids.Count < k)
        {
            var candidate = samples.OrderByDescending(x => centroids.Min(c => DeltaE(x.Lab, c))).ThenBy(x => x.Rgb.R).ThenBy(x => x.Rgb.G).ThenBy(x => x.Rgb.B).First().Lab;
            if (centroids.Any(x => DeltaE(x, candidate) < .01)) break;
            centroids.Add(candidate);
        }
        var assignment = new int[samples.Count];
        for (var iteration = 0; iteration < 20; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var index = 0; index < samples.Count; index++) assignment[index] = Closest(samples[index].Lab, centroids);
            var next = new List<VisualLab>(centroids.Count);
            for (var cluster = 0; cluster < centroids.Count; cluster++)
            {
                var members = Enumerable.Range(0, samples.Count).Where(x => assignment[x] == cluster).Select(x => samples[x].Lab).ToArray();
                next.Add(members.Length == 0 ? centroids[cluster] : new(members.Average(x => x.L), members.Average(x => x.A), members.Average(x => x.B)));
            }
            var movement = centroids.Zip(next).Max(x => DeltaE(x.First, x.Second)); centroids = next;
            if (movement < .5) break;
        }
        var merged = new List<(VisualLab Lab, List<int> Samples)>();
        for (var cluster = 0; cluster < centroids.Count; cluster++)
        {
            var members = Enumerable.Range(0, samples.Count).Where(x => assignment[x] == cluster).ToList(); if (members.Count == 0) continue;
            var existing = merged.FindIndex(x => DeltaE(x.Lab, centroids[cluster]) < 6);
            if (existing >= 0) merged[existing].Samples.AddRange(members); else merged.Add((centroids[cluster], members));
        }
        var colors = new List<DominantColor>();
        foreach (var cluster in merged)
        {
            var r = (byte)Math.Round(cluster.Samples.Average(x => samples[x].Rgb.R)); var g = (byte)Math.Round(cluster.Samples.Average(x => samples[x].Rgb.G)); var b = (byte)Math.Round(cluster.Samples.Average(x => samples[x].Rgb.B)); var rgb = new VisualRgb24(r, g, b); var hsl = RgbToHsl(r, g, b);
            colors.Add(new(rgb, ToLab(rgb), hsl.H, hsl.S, hsl.L, cluster.Samples.Count / (double)samples.Count, $"#{r:X2}{g:X2}{b:X2}"));
        }
        var residual = 1 - colors.Sum(x => x.Weight);
        if (colors.Count > 0 && Math.Abs(residual) > 1e-9) { var max = colors.IndexOf(colors.MaxBy(x => x.Weight)!); colors[max] = colors[max] with { Weight = colors[max].Weight + residual }; }
        return colors;
    }

    private static ColorHarmonyTendency ClassifyHarmony(IReadOnlyList<DominantColor> colors, double saturation)
    {
        if (saturation < .12) return ColorHarmonyTendency.LowSaturationNeutral;
        var material = colors.Where(x => x.Weight >= .08 && x.Saturation >= .12).Select(x => x.Hue).Order().ToArray();
        if (material.Length <= 1 || CircularSpan(material) < 18) return ColorHarmonyTendency.Monochrome;
        if (CircularSpan(material) <= 60) return ColorHarmonyTendency.Analogous;
        if (material.Length >= 3 && HasTriad(material)) return ColorHarmonyTendency.Triadic;
        if (material.Any(a => material.Any(b => AngularDistance(a, b) is >= 150 and <= 210))) return ColorHarmonyTendency.Complementary;
        if (material.Length >= 3 && material.Any(a => material.Count(b => AngularDistance(a, b) is >= 135 and <= 225) >= 2)) return ColorHarmonyTendency.SplitComplementary;
        return ColorHarmonyTendency.Mixed;
    }

    private static bool HasTriad(IReadOnlyList<double> hues)
    {
        for (var a = 0; a < hues.Count; a++) for (var b = a + 1; b < hues.Count; b++) for (var c = b + 1; c < hues.Count; c++)
        {
            var gaps = new[] { AngularDistance(hues[a], hues[b]), AngularDistance(hues[b], hues[c]), AngularDistance(hues[c], hues[a]) };
            if (gaps.All(x => x is >= 95 and <= 145)) return true;
        }
        return false;
    }

    private static int Closest(VisualLab sample, IReadOnlyList<VisualLab> centroids) { var best = 0; var distance = double.MaxValue; for (var index = 0; index < centroids.Count; index++) { var current = DeltaE(sample, centroids[index]); if (current < distance) { best = index; distance = current; } } return best; }
    private static double Percentile(IReadOnlyList<byte> sorted, double percentile) => sorted[(int)Math.Round((sorted.Count - 1) * percentile)];
    private static double Percentile(IReadOnlyList<double> sorted, double percentile) => sorted[(int)Math.Round((sorted.Count - 1) * percentile)];
    private static string HistogramSignature(uint[] histogram)
    {
        var bytes = new byte[histogram.Length * sizeof(uint)];
        Buffer.BlockCopy(histogram, 0, bytes, 0, bytes.Length);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
    private static double CircularSpan(IReadOnlyList<double> hues) { if (hues.Count < 2) return 0; var sorted = hues.Order().ToArray(); var largestGap = Enumerable.Range(0, sorted.Length).Max(i => i == sorted.Length - 1 ? 360 - sorted[i] + sorted[0] : sorted[i + 1] - sorted[i]); return 360 - largestGap; }
    private static double AngularDistance(double a, double b) { var distance = Math.Abs(a - b) % 360; return distance > 180 ? 360 - distance : distance; }
    private static double NormalizeHue(double hue) => (hue % 360 + 360) % 360;

    private static (double H, double S, double L) RgbToHsl(byte r, byte g, byte b)
    {
        var red = r / 255.0; var green = g / 255.0; var blue = b / 255.0; var max = Math.Max(red, Math.Max(green, blue)); var min = Math.Min(red, Math.Min(green, blue)); var delta = max - min; var light = (max + min) / 2;
        if (delta <= 1e-9) return (0, 0, light);
        var saturation = delta / (1 - Math.Abs(2 * light - 1));
        var hue = max == red ? 60 * (((green - blue) / delta) % 6) : max == green ? 60 * ((blue - red) / delta + 2) : 60 * ((red - green) / delta + 4);
        return (NormalizeHue(hue), saturation, light);
    }

    private static VisualRgb24 HslToRgb(double hue, double saturation, double lightness)
    {
        var c = (1 - Math.Abs(2 * lightness - 1)) * saturation; var x = c * (1 - Math.Abs((hue / 60) % 2 - 1)); var m = lightness - c / 2;
        var tuple = hue switch { < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x), < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x) };
        return new((byte)Math.Round((tuple.Item1 + m) * 255), (byte)Math.Round((tuple.Item2 + m) * 255), (byte)Math.Round((tuple.Item3 + m) * 255));
    }
}
