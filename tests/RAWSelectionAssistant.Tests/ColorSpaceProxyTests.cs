using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ColorSpaceProxyTests
{
    [TestMethod]
    public void CameraHasStablePhotographicDefaultAndBoundedInteraction()
    {
        var camera = new ColorSpaceCameraState();
        Assert.AreEqual(ColorSpaceCameraState.DefaultYaw, camera.Yaw);
        Assert.AreEqual(ColorSpaceCameraState.DefaultPitch, camera.Pitch);
        Assert.AreEqual(ColorSpaceCameraState.DefaultDistance, camera.Distance);
        camera.Orbit(900, 200); camera.Zoom(100);
        Assert.IsTrue(camera.Yaw is >= -180 and <= 180);
        Assert.AreEqual(89, camera.Pitch);
        Assert.AreEqual(0.5, camera.Distance);
        camera.Reset();
        Assert.AreEqual(ColorSpaceCameraState.DefaultYaw, camera.Yaw);
        Assert.AreEqual(ColorSpaceCameraState.DefaultPitch, camera.Pitch);
    }

    [TestMethod]
    public void LabProjectionUsesNormalizedAxes()
    {
        var coordinate = ColorSpaceCoordinate.FromLab(new OklabColor(.75, .2, -.1));
        Assert.AreEqual(.5, coordinate.X, 1e-12);
        Assert.AreEqual(.5, coordinate.Y, 1e-12);
        Assert.AreEqual(-.25, coordinate.Z, 1e-12);
    }

    [TestMethod]
    public void LinkingFindsNearestPointAndSeparatesSampleMarkers()
    {
        var source = new VisualPixelBuffer(2, 1, new byte[] { 255, 0, 0, 0, 0, 255 });
        var cloud = ColorSpaceProxyBuilder.Build(source, new(16));
        var redIndex = ColorSpaceLinking.FindNearest(cloud, OklabColorSpace.FromSrgb(new(255, 0, 0)));
        var blueIndex = ColorSpaceLinking.FindNearest(cloud, OklabColorSpace.FromSrgb(new(0, 0, 255)));
        Assert.AreNotEqual(redIndex, blueIndex);
        var markers = ColorSpaceLinking.MarkSamples(cloud, [new(255, 0, 0)], [new(0, 0, 255)]);
        Assert.IsTrue(markers.Any(marker => marker.Kind == ColorSpaceMarkerKind.PositiveSample && marker.PointIndex == redIndex));
        Assert.IsTrue(markers.Any(marker => marker.Kind == ColorSpaceMarkerKind.NegativeSample && marker.PointIndex == blueIndex));
        Assert.IsTrue(ColorSpaceLinking.SelectCluster(cloud, redIndex, .001).Contains(redIndex));
    }

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
