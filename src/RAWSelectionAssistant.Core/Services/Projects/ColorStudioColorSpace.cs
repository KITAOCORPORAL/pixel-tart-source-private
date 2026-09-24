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
