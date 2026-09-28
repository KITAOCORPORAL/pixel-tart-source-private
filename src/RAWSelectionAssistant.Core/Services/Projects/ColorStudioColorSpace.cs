using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>A compact sampled point for the future Color Studio 3D view.</summary>
public readonly record struct ColorSpacePoint(
    OklabColor Lab,
    VisualRgb24 PreviewRgb,
    int SourceX,
    int SourceY,
    int Count = 1)
{
    public int SourceIndex(int width) => checked(SourceY * width + SourceX);
}

public sealed record ColorSpaceProxySettings(int MaxPoints = 4096)
{
    public void Validate()
    {
        if (MaxPoints is < 16 or > 262_144) throw new ArgumentOutOfRangeException(nameof(MaxPoints));
    }
}

/// <summary>Immutable, deterministic color distribution suitable for a bounded visualization.</summary>
public sealed record ColorSpaceCloud(
    int SourceWidth,
    int SourceHeight,
    int SourcePixelCount,
    int SampledPixelCount,
    IReadOnlyList<ColorSpacePoint> Points,
    string SourceFingerprint,
    ColorSpaceProxySettings Settings)
{
    public int Count => Points.Count;
}

public enum ColorSpaceMarkerKind { None, PositiveSample, NegativeSample, SelectedCluster }

public enum ColorCloudMode { Source, Reference, Matched, Overlay, Migration }

public enum ColorProtectionState { None, Neutral, Skin, Highlight, Shadow }

public enum ColorSpaceSamplingTier { Preview, Standard, Dense }

public enum ColorSliceAxis { Lightness, A, B }

public readonly record struct ColorSlice(ColorSliceAxis Axis, double Position, double Thickness = .04)
{
    public bool Contains(OklabColor color)
    {
        if (!double.IsFinite(Position) || !double.IsFinite(Thickness) || Thickness < 0) return false;
        var value = Axis switch { ColorSliceAxis.Lightness => color.L, ColorSliceAxis.A => color.A, _ => color.B };
        return Math.Abs(value - Position) <= Thickness;
    }
}

/// <summary>Photo-to-cloud identity used by eyedropper and future range interactions.</summary>
public sealed record ColorSelection(
    ColorSpaceSelection Selection,
    int SourcePixelIndex,
    OklabColor SampledColor,
    IReadOnlyList<int> PointMembership)
{
    public static ColorSelection None => new(ColorSpaceSelection.None, -1, new OklabColor(0, 0, 0), []);
}

public readonly record struct ColorMigrationVector(
    ColorSpacePoint Source,
    ColorSpacePoint Matched,
    OklabColor Delta,
    double Distance,
    double Weight,
    ColorProtectionState Protection);

public sealed record ColorSpaceVisualizationModel(
    ColorSpaceCloud Source,
    ColorSpaceCloud Reference,
    ColorSpaceCloud Matched,
    IReadOnlyList<ColorMigrationVector> MigrationVectors,
    ColorCloudMode Mode,
    ColorSpaceSamplingTier SamplingTier,
    string TransformHash)
{
    public IReadOnlyList<ColorSpaceCloud> VisibleClouds => Mode switch
    {
        ColorCloudMode.Source => [Source],
        ColorCloudMode.Reference => [Reference],
        ColorCloudMode.Matched => [Matched],
        _ => [Source, Reference, Matched]
    };
}

public readonly record struct ColorSpaceSelection(ColorSpaceMarkerKind Kind, int PointIndex)
{
    public static ColorSpaceSelection None => new(ColorSpaceMarkerKind.None, -1);
}

/// <summary>Links an already-mapped working color to a cloud point without owning canvas coordinates.</summary>
public static class ColorSpaceLinking
{
    public static int FindNearest(ColorSpaceCloud cloud, OklabColor color)
    {
        ArgumentNullException.ThrowIfNull(cloud);
        if (cloud.Points.Count == 0) return -1;
        var best = 0; var distance = double.PositiveInfinity;
        for (var index = 0; index < cloud.Points.Count; index++)
        {
            var point = cloud.Points[index].Lab;
            var candidate = SquaredDistance(point, color);
            if (candidate < distance) { distance = candidate; best = index; }
        }
        return best;
    }

    public static IReadOnlyList<ColorSpaceSelection> MarkSamples(ColorSpaceCloud cloud, IEnumerable<VisualRgb24> positive, IEnumerable<VisualRgb24> negative)
    {
        ArgumentNullException.ThrowIfNull(cloud); ArgumentNullException.ThrowIfNull(positive); ArgumentNullException.ThrowIfNull(negative);
        var markers = new List<ColorSpaceSelection>();
        foreach (var sample in positive) { var index = FindNearest(cloud, OklabColorSpace.FromSrgb(sample)); if (index >= 0) markers.Add(new(ColorSpaceMarkerKind.PositiveSample, index)); }
        foreach (var sample in negative) { var index = FindNearest(cloud, OklabColorSpace.FromSrgb(sample)); if (index >= 0) markers.Add(new(ColorSpaceMarkerKind.NegativeSample, index)); }
        return markers;
    }

    public static ColorSelection FromPixel(ColorSpaceCloud cloud, int x, int y, VisualPixelBuffer source, double radius = .06)
    {
        ArgumentNullException.ThrowIfNull(cloud); ArgumentNullException.ThrowIfNull(source);
        if (x < 0 || y < 0 || x >= source.Width || y >= source.Height) return ColorSelection.None;
        var pixel = checked(y * source.Width + x);
        var offset = checked(pixel * 3);
        var color = new VisualRgb24(source.Rgb24.Span[offset], source.Rgb24.Span[offset + 1], source.Rgb24.Span[offset + 2]);
        var lab = OklabColorSpace.FromSrgb(color);
        var point = FindNearest(cloud, lab);
        return new(new(ColorSpaceMarkerKind.SelectedCluster, point), pixel, lab, SelectCluster(cloud, point, radius));
    }

    public static IReadOnlyList<int> ToPixelMembership(ColorSpaceCloud cloud, VisualPixelBuffer source, int pointIndex, double radius = .06)
    {
        ArgumentNullException.ThrowIfNull(cloud); ArgumentNullException.ThrowIfNull(source);
        if (pointIndex < 0 || pointIndex >= cloud.Points.Count) return [];
        var center = cloud.Points[pointIndex].Lab;
        var result = new List<int>();
        for (var pixel = 0; pixel < source.PixelCount; pixel++)
        {
            var offset = pixel * 3;
            var lab = OklabColorSpace.FromSrgb(new VisualRgb24(source.Rgb24.Span[offset], source.Rgb24.Span[offset + 1], source.Rgb24.Span[offset + 2]));
            if (SquaredDistance(center, lab) <= radius * radius) result.Add(pixel);
        }
        return result;
    }

    public static IReadOnlyList<int> SelectCluster(ColorSpaceCloud cloud, int pointIndex, double radius = .06)
    {
        ArgumentNullException.ThrowIfNull(cloud);
        if (pointIndex < 0 || pointIndex >= cloud.Points.Count || !double.IsFinite(radius) || radius < 0) return [];
        var center = cloud.Points[pointIndex].Lab; var squared = radius * radius;
        return cloud.Points.Select((point, index) => (point, index)).Where(item => SquaredDistance(item.point.Lab, center) <= squared).Select(item => item.index).ToArray();
    }

    private static double SquaredDistance(OklabColor left, OklabColor right) =>
        Math.Pow(left.L - right.L, 2) + Math.Pow(left.A - right.A, 2) + Math.Pow(left.B - right.B, 2);
}

public static class ColorSpaceSampling
{
    public static ColorSpaceProxySettings Settings(ColorSpaceSamplingTier tier) => tier switch
    {
        ColorSpaceSamplingTier.Preview => new(1024),
        ColorSpaceSamplingTier.Dense => new(16_384),
        _ => new(4096)
    };
}

public static class ColorSpaceProtection
{
    public static ColorProtectionState Classify(OklabColor color, ReferenceMatchV4Settings settings)
    {
        if (color.Chroma <= .08 * Math.Max(.01, settings.NeutralProtection)) return ColorProtectionState.Neutral;
        var hue = Math.Atan2(color.B, color.A) * 180 / Math.PI; if (hue < 0) hue += 360;
        if (color.L is > .28 and < .9 && hue is > 25 and < 80 && color.Chroma is > .025 and < .22) return ColorProtectionState.Skin;
        if (color.L >= .82) return ColorProtectionState.Highlight;
        if (color.L <= .2) return ColorProtectionState.Shadow;
        return ColorProtectionState.None;
    }
}

/// <summary>Builds bounded visualization data from the same Core OKLab and V4 pixel semantics.</summary>
public static class ColorSpaceVisualizationBuilder
{
    public static ColorSpaceVisualizationModel Build(
        VisualPixelBuffer source, VisualPixelBuffer reference,
        MatchV4ResolvedTransform transform,
        ColorSpaceSamplingTier tier = ColorSpaceSamplingTier.Standard,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(transform);
        transform = transform.Normalize();
        var sourceCloud = ColorSpaceProxyBuilder.Build(source, ColorSpaceSampling.Settings(tier), token);
        var referenceCloud = ColorSpaceProxyBuilder.Build(reference, ColorSpaceSampling.Settings(tier), token);
        var values = new float[checked(source.PixelCount * 3)];
        for (var i = 0; i < values.Length; i++) values[i] = source.Rgb24.Span[i] / 255f;
        MatchV4PixelApplication.Apply(values, transform, token);
        var matchedPixels = new byte[values.Length];
        for (var i = 0; i < values.Length; i++) matchedPixels[i] = (byte)Math.Clamp(Math.Round(values[i] * 255), 0, 255);
        var matchedCloud = ColorSpaceProxyBuilder.Build(new VisualPixelBuffer(source.Width, source.Height, matchedPixels), ColorSpaceSampling.Settings(tier), token);
        var vectors = new List<ColorMigrationVector>(sourceCloud.Points.Count);
        var settings = transform.Settings;
        for (var i = 0; i < sourceCloud.Points.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var original = sourceCloud.Points[i];
            var matched = matchedCloud.Points[i];
            var delta = new OklabColor(matched.Lab.L - original.Lab.L, matched.Lab.A - original.Lab.A, matched.Lab.B - original.Lab.B);
            vectors.Add(new(original, matched, delta, Math.Sqrt(delta.L * delta.L + delta.A * delta.A + delta.B * delta.B), 1, ColorSpaceProtection.Classify(original.Lab, settings)));
        }
        return new(sourceCloud, referenceCloud, matchedCloud, vectors, ColorCloudMode.Overlay, tier, transform.TransformHash);
    }
}

public readonly record struct ColorSpaceProxyCacheKey(
    string AssetIdentity,
    string ImageVersion,
    ColorSpaceProxySettings Settings,
    string RelevantColorState)
{
    public override string ToString() => $"{AssetIdentity}|{ImageVersion}|{Settings.MaxPoints}|{RelevantColorState}";
}

public static class ColorSpaceProxyBuilder
{
    public static ColorSpaceCloud Build(VisualPixelBuffer source, ColorSpaceProxySettings? settings = null, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        settings ??= new();
        settings.Validate();
        var maxPoints = Math.Min(settings.MaxPoints, source.PixelCount);
        var aspect = source.Width / (double)source.Height;
        var columns = Math.Clamp((int)Math.Ceiling(Math.Sqrt(maxPoints * aspect)), 1, source.Width);
        var rows = Math.Clamp((int)Math.Ceiling(maxPoints / (double)columns), 1, source.Height);
        while (columns * rows > maxPoints)
        {
            if (columns >= rows && columns > 1) columns--;
            else if (rows > 1) rows--;
            else break;
        }

        var points = new List<ColorSpacePoint>(columns * rows);
        for (var row = 0; row < rows; row++)
        {
            token.ThrowIfCancellationRequested();
            var y = Math.Min(source.Height - 1, (int)(((row + .5) * source.Height) / rows));
            for (var column = 0; column < columns; column++)
            {
                if ((column & 127) == 0) token.ThrowIfCancellationRequested();
                var x = Math.Min(source.Width - 1, (int)(((column + .5) * source.Width) / columns));
                var offset = checked((y * source.Width + x) * 3);
                var rgb = new VisualRgb24(source.Rgb24.Span[offset], source.Rgb24.Span[offset + 1], source.Rgb24.Span[offset + 2]);
                points.Add(new(OklabColorSpace.FromSrgb(rgb), rgb, x, y));
            }
        }
        return new(source.Width, source.Height, source.PixelCount, points.Count, points,
            VisualAnalysisFingerprint.Compute(source), settings);
    }
}

/// <summary>Bounded, thread-safe cache. The key includes asset identity, image version and relevant color state.</summary>
public sealed class ColorSpaceProxyCache
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly Dictionary<ColorSpaceProxyCacheKey, (ColorSpaceCloud Cloud, LinkedListNode<ColorSpaceProxyCacheKey> Node)> _items = [];
    private readonly LinkedList<ColorSpaceProxyCacheKey> _lru = [];

    public ColorSpaceProxyCache(int capacity = 4)
    {
        if (capacity is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public int Count { get { lock (_gate) return _items.Count; } }

    public bool TryGet(ColorSpaceProxyCacheKey key, out ColorSpaceCloud? cloud)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(key, out var value)) { cloud = null; return false; }
            _lru.Remove(value.Node); _lru.AddFirst(value.Node); cloud = value.Cloud; return true;
        }
    }

    public ColorSpaceCloud GetOrAdd(ColorSpaceProxyCacheKey key, Func<ColorSpaceCloud> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        lock (_gate)
        {
            if (_items.TryGetValue(key, out var existing)) { _lru.Remove(existing.Node); _lru.AddFirst(existing.Node); return existing.Cloud; }
        }
        var created = factory();
        lock (_gate)
        {
            if (_items.TryGetValue(key, out var raced)) { _lru.Remove(raced.Node); _lru.AddFirst(raced.Node); return raced.Cloud; }
            var node = _lru.AddFirst(key); _items[key] = (created, node);
            while (_items.Count > _capacity && _lru.Last is { } last) { _items.Remove(last.Value); _lru.RemoveLast(); }
            return created;
        }
    }

    public void Clear() { lock (_gate) { _items.Clear(); _lru.Clear(); } }
}
