using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Bounded, deterministic settings for the Match V4 foundation.</summary>
public sealed record ReferenceMatchV4Settings(
    int MaximumRepresentativeSamples = 512,
    int SinkhornIterations = 32,
    double Regularization = .035,
    double MaximumLuminanceDisplacement = .12,
    double MaximumChromaDisplacement = .08,
    int ResidualIterations = 2,
    double ResidualStopThreshold = .002,
    int AnalysisResolution = 1024,
    int TileSize = 512,
    int TileOverlap = 8)
{
    public void Validate()
    {
        if (MaximumRepresentativeSamples is < 16 or > 2048) throw new ArgumentOutOfRangeException(nameof(MaximumRepresentativeSamples));
        if (SinkhornIterations is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(SinkhornIterations));
        if (!double.IsFinite(Regularization) || Regularization <= 0 || Regularization > 1) throw new ArgumentOutOfRangeException(nameof(Regularization));
        if (!double.IsFinite(MaximumLuminanceDisplacement) || MaximumLuminanceDisplacement is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(MaximumLuminanceDisplacement));
        if (!double.IsFinite(MaximumChromaDisplacement) || MaximumChromaDisplacement is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(MaximumChromaDisplacement));
        if (ResidualIterations is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(ResidualIterations));
        if (!double.IsFinite(ResidualStopThreshold) || ResidualStopThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(ResidualStopThreshold));
        if (AnalysisResolution < 128 || TileSize < 32 || TileOverlap < 0 || TileOverlap * 2 >= TileSize) throw new ArgumentOutOfRangeException(nameof(TileSize));
    }
}

public enum ReferenceMatchV4BackendKind { Cpu, Gpu }

public sealed record ReferenceLookDecomposition(
    double Exposure,
    double Contrast,
    double BlackPoint,
    double WhitePoint,
    double GlobalChroma,
    double TemperatureTendency,
    double TintTendency,
    IReadOnlyList<double> HueSectorChroma,
    IReadOnlyList<OklabColor> RegionCenters,
    string AlgorithmRevision = "v4-foundation-1");

public sealed record ReferenceMatchV4Result(
    VisualPixelBuffer Pixels,
    ReferenceMatchV4BackendKind Backend,
    bool UsedCpuFallback,
    int RepresentativeSourceCount,
    int RepresentativeReferenceCount,
    int ResidualIterations,
    double ResidualError,
    ReferenceLookDecomposition Decomposition,
    string CacheKey);

/// <summary>Small compute seam. GPU implementations must preserve CPU semantics.</summary>
public interface IColorMatchComputeBackend
{
    ReferenceMatchV4BackendKind Kind { get; }
    bool IsAvailable { get; }
    IReadOnlyList<OklabColor> Map(IReadOnlyList<OklabColor> source, IReadOnlyList<OklabColor> reference,
        ReferenceMatchV4Settings settings, CancellationToken token = default);
}

public sealed class CpuColorMatchComputeBackend : IColorMatchComputeBackend
{
    public ReferenceMatchV4BackendKind Kind => ReferenceMatchV4BackendKind.Cpu;
    public bool IsAvailable => true;

    public IReadOnlyList<OklabColor> Map(IReadOnlyList<OklabColor> source, IReadOnlyList<OklabColor> reference,
        ReferenceMatchV4Settings settings, CancellationToken token = default)
    {
        settings.Validate();
        if (source.Count == 0 || reference.Count == 0) return Array.Empty<OklabColor>();
        var n = source.Count; var m = reference.Count;
        var costs = new double[n, m];
        var u = new double[n]; var v = new double[m];
        var kernel = new double[n, m];
        for (var i = 0; i < n; i++)
        {
            if ((i & 31) == 0) token.ThrowIfCancellationRequested();
            for (var j = 0; j < m; j++)
            {
                var dl = source[i].L - reference[j].L;
                var da = source[i].A - reference[j].A;
                var db = source[i].B - reference[j].B;
                costs[i, j] = dl * dl + (da * da + db * db) * 1.35;
                kernel[i, j] = Math.Exp(-costs[i, j] / settings.Regularization);
            }
        }
        var a = 1d / n; var b = 1d / m;
        for (var iteration = 0; iteration < settings.SinkhornIterations; iteration++)
        {
            token.ThrowIfCancellationRequested();
            for (var i = 0; i < n; i++)
            {
                var sum = 0d; for (var j = 0; j < m; j++) sum += kernel[i, j] * Math.Exp(v[j]);
                u[i] = a / Math.Max(1e-12, sum);
            }
            for (var j = 0; j < m; j++)
            {
                var sum = 0d; for (var i = 0; i < n; i++) sum += kernel[i, j] * u[i];
                v[j] = Math.Log(b / Math.Max(1e-12, sum));
            }
        }
        var result = new OklabColor[n];
        for (var i = 0; i < n; i++)
        {
            if ((i & 31) == 0) token.ThrowIfCancellationRequested();
            var total = 0d; var l = 0d; var aa = 0d; var bb = 0d;
            for (var j = 0; j < m; j++)
            {
                var weight = u[i] * kernel[i, j] * Math.Exp(v[j]); total += weight;
                l += weight * reference[j].L; aa += weight * reference[j].A; bb += weight * reference[j].B;
            }
            result[i] = total <= 1e-12 ? reference[i % m] : new(l / total, aa / total, bb / total);
        }
        return result;
    }
}

/// <summary>Explicit seam for a future DirectML/ComputeSharp implementation; never silently claims GPU use.</summary>
public sealed class GpuColorMatchComputeBackend : IColorMatchComputeBackend
{
    public ReferenceMatchV4BackendKind Kind => ReferenceMatchV4BackendKind.Gpu;
    public bool IsAvailable => false;
    public IReadOnlyList<OklabColor> Map(IReadOnlyList<OklabColor> source, IReadOnlyList<OklabColor> reference,
        ReferenceMatchV4Settings settings, CancellationToken token = default) =>
        throw new NotSupportedException("GPU backend is not available on this build; use the CPU fallback.");
}

public static class ReferenceMatchV4Cache
{
    public static string CreateKey(string sourceIdentity, string referenceIdentity, ReferenceMatchV4Settings settings, int algorithmRevision = 1) =>
        $"{sourceIdentity}|{referenceIdentity}|v4|{algorithmRevision}|{settings.MaximumRepresentativeSamples}|{settings.SinkhornIterations}|{settings.Regularization:R}|{settings.AnalysisResolution}|{settings.TileSize}|{settings.TileOverlap}";
}

public readonly record struct ColorMatchTile(int X, int Y, int Width, int Height, int SourceX, int SourceY, int SourceWidth, int SourceHeight);

public static class ColorMatchTilePlanner
{
    public static IReadOnlyList<ColorMatchTile> Plan(int width, int height, ReferenceMatchV4Settings settings)
    {
        settings.Validate();
        var tiles = new List<ColorMatchTile>();
        for (var y = 0; y < height; y += settings.TileSize)
            for (var x = 0; x < width; x += settings.TileSize)
            {
                var w = Math.Min(settings.TileSize, width - x); var h = Math.Min(settings.TileSize, height - y);
                var sx = Math.Max(0, x - settings.TileOverlap); var sy = Math.Max(0, y - settings.TileOverlap);
                var ex = Math.Min(width, x + w + settings.TileOverlap); var ey = Math.Min(height, y + h + settings.TileOverlap);
                tiles.Add(new(x, y, w, h, sx, sy, ex - sx, ey - sy));
            }
        return tiles;
    }
}

public sealed class ReferenceMatchV4Engine
{
    private readonly IColorMatchComputeBackend _cpu;
    private readonly IColorMatchComputeBackend _gpu;

    public ReferenceMatchV4Engine(IColorMatchComputeBackend? cpu = null, IColorMatchComputeBackend? gpu = null)
    { _cpu = cpu ?? new CpuColorMatchComputeBackend(); _gpu = gpu ?? new GpuColorMatchComputeBackend(); }

    public ReferenceMatchV4Result Match(VisualPixelBuffer source, VisualPixelBuffer reference,
        string sourceIdentity = "source", string referenceIdentity = "reference", ReferenceMatchV4Settings? settings = null,
        bool preferGpu = true, CancellationToken token = default)
    {
        settings ??= new(); settings.Validate();
        var sourceSamples = Sample(source, settings.MaximumRepresentativeSamples, token);
        var referenceSamples = Sample(reference, settings.MaximumRepresentativeSamples, token);
        var backend = preferGpu && _gpu.IsAvailable ? _gpu : _cpu;
        var mapped = backend.Map(sourceSamples, referenceSamples, settings, token);
        var fallback = preferGpu && backend.Kind == ReferenceMatchV4BackendKind.Cpu;
        var global = WeightedDelta(sourceSamples, mapped);
        var regionDeltas = BuildRegionDeltas(sourceSamples, mapped);
        var output = new byte[source.Rgb24.Length]; var residual = 0d; var completedResidual = 0;
        for (var iteration = 0; iteration <= settings.ResidualIterations; iteration++)
        {
            token.ThrowIfCancellationRequested();
            var pixels = iteration == 0 ? source.Rgb24.Span : output.AsSpan();
            var sumError = 0d;
            for (var index = 0; index < source.PixelCount; index++)
            {
                if ((index & 1023) == 0) token.ThrowIfCancellationRequested();
                var offset = index * 3; var lab = OklabColorSpace.FromSrgb(new(pixels[offset], pixels[offset + 1], pixels[offset + 2]));
                var zone = Math.Clamp((int)(lab.L * 3), 0, 2); var delta = BlendDelta(global, regionDeltas[zone], SmoothRegionWeight(lab.L, zone));
                var protection = Protection(lab);
                var l = Math.Clamp(lab.L + Math.Clamp(delta.L, -settings.MaximumLuminanceDisplacement, settings.MaximumLuminanceDisplacement) * protection, 0, 1);
                var a = lab.A + Math.Clamp(delta.A, -settings.MaximumChromaDisplacement, settings.MaximumChromaDisplacement) * protection;
                var b = lab.B + Math.Clamp(delta.B, -settings.MaximumChromaDisplacement, settings.MaximumChromaDisplacement) * protection;
                var rgb = OklabColorSpace.ToSrgbGamutMapped(new(l, a, b)); output[offset] = rgb.R; output[offset + 1] = rgb.G; output[offset + 2] = rgb.B;
                sumError += Math.Abs(delta.L) + Math.Sqrt(delta.A * delta.A + delta.B * delta.B);
            }
            residual = sumError / Math.Max(1, source.PixelCount); completedResidual = iteration;
            if (iteration == settings.ResidualIterations || residual <= settings.ResidualStopThreshold) break;
            global = new(global.L * .35, global.A * .35, global.B * .35);
            regionDeltas = regionDeltas.Select(delta => new OklabColor(delta.L * .35, delta.A * .35, delta.B * .35)).ToArray();
        }
        var decomposition = Decompose(sourceSamples, referenceSamples);
        return new(new(source.Width, source.Height, output), backend.Kind, fallback, sourceSamples.Count, referenceSamples.Count,
            completedResidual, residual, decomposition, ReferenceMatchV4Cache.CreateKey(sourceIdentity, referenceIdentity, settings));
    }

    private static List<OklabColor> Sample(VisualPixelBuffer pixels, int maximum, CancellationToken token)
    {
        var count = Math.Min(maximum, pixels.PixelCount); var result = new List<OklabColor>(count);
        var step = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((double)pixels.PixelCount / count)));
        for (var y = 0; y < pixels.Height && result.Count < maximum; y += step)
            for (var x = 0; x < pixels.Width && result.Count < maximum; x += step)
            { token.ThrowIfCancellationRequested(); var i = (y * pixels.Width + x) * 3; result.Add(OklabColorSpace.FromSrgb(new(pixels.Rgb24.Span[i], pixels.Rgb24.Span[i + 1], pixels.Rgb24.Span[i + 2]))); }
        return result;
    }
    private static OklabColor WeightedDelta(IReadOnlyList<OklabColor> source, IReadOnlyList<OklabColor> mapped) =>
        new(mapped.Average((c, i) => c.L - source[i].L), mapped.Average((c, i) => c.A - source[i].A), mapped.Average((c, i) => c.B - source[i].B));
    private static OklabColor[] BuildRegionDeltas(IReadOnlyList<OklabColor> source, IReadOnlyList<OklabColor> mapped)
    {
        var regions = new OklabColor[3];
        for (var zone = 0; zone < regions.Length; zone++)
        {
            var sourceRegion = new List<OklabColor>(); var mappedRegion = new List<OklabColor>();
            for (var i = 0; i < source.Count; i++)
                if (Math.Clamp((int)(source[i].L * 3), 0, 2) == zone) { sourceRegion.Add(source[i]); mappedRegion.Add(mapped[i]); }
            regions[zone] = sourceRegion.Count == 0 ? new(.0, .0, .0) : WeightedDelta(sourceRegion, mappedRegion);
        }
        return regions;
    }
    private static OklabColor BlendDelta(OklabColor global, OklabColor local, double weight) => new(global.L * (1 - weight) + local.L * weight, global.A * (1 - weight) + local.A * weight, global.B * (1 - weight) + local.B * weight);
    private static double SmoothRegionWeight(double l, int zone) => zone switch { 0 => 1 - Math.Clamp((l - .2) / .2, 0, 1), 2 => Math.Clamp((l - .6) / .2, 0, 1), _ => 1 };
    private static double Protection(OklabColor c)
    {
        var neutral = Math.Clamp(1 - c.Chroma / .08, 0, 1); var hue = Math.Atan2(c.B, c.A) * 180 / Math.PI; if (hue < 0) hue += 360;
        var skin = c.L is > .28 and < .9 && hue is > 25 and < 80 && c.Chroma is > .025 and < .22;
        var highlight = Math.Clamp((c.L - .82) / .18, 0, 1); var shadow = Math.Clamp((.2 - c.L) / .2, 0, 1);
        return Math.Clamp(1 - neutral * .82 - (skin ? .28 : 0) - highlight * .62 - shadow * Math.Clamp(c.Chroma / .1, 0, 1) * .5, .08, 1);
    }
    private static ReferenceLookDecomposition Decompose(IReadOnlyList<OklabColor> source, IReadOnlyList<OklabColor> reference)
    {
        var sourceL = source.Average(c => c.L); var refL = reference.Average(c => c.L); var sourceC = source.Average(c => c.Chroma); var refC = reference.Average(c => c.Chroma);
        var sectors = Enumerable.Range(0, 8).Select(sector => reference.Where(c => ((Math.Atan2(c.B, c.A) * 180 / Math.PI + 360) % 360) / 45 >= sector && ((Math.Atan2(c.B, c.A) * 180 / Math.PI + 360) % 360) / 45 < sector + 1).Select(c => c.Chroma).DefaultIfEmpty(0).Average()).ToArray();
        return new(refL - sourceL, 1, 0, 1, refC - sourceC, 0, 0, sectors, reference.Take(3).ToArray());
    }
}

file static class EnumerableExtensions
{
    public static double Average<T>(this IEnumerable<T> source, Func<T, int, double> selector)
    { var values = source.ToArray(); return values.Length == 0 ? 0 : values.Select((value, index) => selector(value, index)).Average(); }
}
