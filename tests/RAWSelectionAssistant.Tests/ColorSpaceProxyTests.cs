using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ColorSpaceProxyTests
{
    [TestMethod]
    public void ProxyIsDeterministicBoundedAndRetainsSourceRelationship()
    {
        var source = Fixture(320, 180);
        var settings = new ColorSpaceProxySettings(256);
        var first = ColorSpaceProxyBuilder.Build(source, settings);
        var second = ColorSpaceProxyBuilder.Build(source, settings);
        Assert.IsLessThanOrEqualTo(256, first.Count);
        Assert.HasCount(first.Points.Count, second.Points);
        CollectionAssert.AreEqual(first.Points.ToArray(), second.Points.ToArray());
        Assert.IsTrue(first.Points.All(point => point.SourceX is >= 0 and < 320 && point.SourceY is >= 0 and < 180 && point.Count > 0));
        Assert.AreEqual(57_600, first.SourcePixelCount);
    }

    [TestMethod]
    public void ProxyUsesSameOklabImplementationAndCancellationIsObserved()
    {
        var source = Fixture(800, 600);
        var first = ColorSpaceProxyBuilder.Build(source, new(1024));
        var rgb = first.Points[0].PreviewRgb;
        var lab = OklabColorSpace.FromSrgb(rgb);
        Assert.AreEqual(lab, first.Points[0].Lab);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => ColorSpaceProxyBuilder.Build(source, new(1024), cancelled.Token));
    }

    [TestMethod]
    public void CacheIncludesIdentityVersionSettingsAndEvictsOldEntries()
    {
        var source = Fixture(40, 20); var cache = new ColorSpaceProxyCache(2); var settings = new ColorSpaceProxySettings(64);
        var a = new ColorSpaceProxyCacheKey("asset-a", "v1", settings, "source");
        var b = new ColorSpaceProxyCacheKey("asset-b", "v1", settings, "source");
        var c = new ColorSpaceProxyCacheKey("asset-c", "v1", settings, "source");
        var first = cache.GetOrAdd(a, () => ColorSpaceProxyBuilder.Build(source, settings));
        Assert.AreSame(first, cache.GetOrAdd(a, () => throw new InvalidOperationException()));
        cache.GetOrAdd(b, () => ColorSpaceProxyBuilder.Build(source, settings));
        cache.GetOrAdd(c, () => ColorSpaceProxyBuilder.Build(source, settings));
        Assert.AreEqual(2, cache.Count);
        Assert.IsFalse(cache.TryGet(a, out _));
        Assert.IsTrue(cache.TryGet(c, out _));
    }

    private static VisualPixelBuffer Fixture(int width, int height)
    {
        var bytes = new byte[width * height * 3];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var offset = (y * width + x) * 3; bytes[offset] = (byte)((x * 13 + y * 3) % 256);
            bytes[offset + 1] = (byte)((x * 5 + y * 17) % 256); bytes[offset + 2] = (byte)((x * 7 + y * 11) % 256);
        }
        return new(width, height, bytes);
    }
}
