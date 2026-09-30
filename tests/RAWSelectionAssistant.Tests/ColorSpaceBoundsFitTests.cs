using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ColorSpaceBoundsFitTests
{
    private static ColorSpaceVisualizationModel Model(double x, double y, double offsetX = 0, double offsetY = 0)
    {
        var points = new[] { (-x, -y), (x, -y), (x, y), (-x, y) }
            .Select(p => new ColorSpacePoint(new((p.Item2 + offsetY + 1) / 2, (p.Item1 + offsetX) * .4, .05), new(128, 128, 128), 0, 0)).ToArray();
        var cloud = new ColorSpaceCloud(2, 2, 4, 4, points, "fit", new());
        return new(cloud, cloud, cloud, [], ColorCloudMode.Source, ColorSpaceSamplingTier.Preview, "fit");
    }
    private static ColorSpaceCamera Front => new(0, 0, 2.4, 0, 0);
    private static ColorSpaceCamera Check(ColorSpaceVisualizationModel model, double w, double h, ColorSpaceCamera? camera = null)
    {
        var fit = ColorSpaceProjection.Fit(model, camera ?? Front, w, h);
        foreach (var p in model.VisibleClouds.SelectMany(c => ColorSpaceProjection.Project(c, fit, w, h)))
        {
            Assert.IsTrue(double.IsFinite(p.X) && double.IsFinite(p.Y));
            Assert.IsTrue(p.X >= w * ColorSpaceProjection.FitPadding - 1e-8 && p.X <= w * (1 - ColorSpaceProjection.FitPadding) + 1e-8, $"X={p.X}, width={w}");
            Assert.IsTrue(p.Y >= h * ColorSpaceProjection.FitPadding - 1e-8 && p.Y <= h * (1 - ColorSpaceProjection.FitPadding) + 1e-8, $"Y={p.Y}, height={h}");
        }
        return fit;
    }
    [TestMethod] public void WideModelFitsLandscapeViewport() => Check(Model(1, .2), 900, 400);
    [TestMethod] public void TallModelFitsLandscapeViewport() => Check(Model(.2, 1), 900, 400);
    [TestMethod] public void WideModelFitsPortraitViewport() => Check(Model(1, .2), 400, 900);
    [TestMethod] public void TallModelFitsPortraitViewport() => Check(Model(.2, 1), 400, 900);
    [TestMethod] public void RotatedModelStillFits() => Check(Model(1, 1), 400, 900, new(127, 58, .5, .9, -.7));
    [TestMethod] public void FitAddsPadding()
    {
        var model = Model(1, 1); var fit = Check(model, 600, 600);
        var points = ColorSpaceProjection.Project(model.Source, fit, 600, 600);
        Assert.AreEqual(600 * ColorSpaceProjection.FitPadding, points.Min(p => p.X), 1e-8);
        Assert.AreEqual(600 * (1 - ColorSpaceProjection.FitPadding), points.Max(p => p.Y), 1e-8);
    }
    [TestMethod] public void FitDoesNotProduceNaN() => Check(Model(.5, .5), 600, 400, new(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN));
    [TestMethod] public void FitDoesNotProduceInfinity() => Check(Model(.5, .5), 600, 400, new(double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity));
    [TestMethod] public void EmptyModelIsSafe()
    {
        var model = Model(0, 0); model = model with { Source = model.Source with { Points = [] } };
        var fit = Check(model, 400, 300); Assert.IsTrue(double.IsFinite(fit.Distance));
    }
    [TestMethod] public void SinglePointModelIsSafe()
    {
        var model = Model(0, 0, .4, .3);
        model = model with { Source = model.Source with { Points = [model.Source.Points[0]] } };
        var fit = Check(model, 400, 300);
        var point = ColorSpaceProjection.Project(model.Source, fit, 400, 300).Single();
        Assert.AreEqual(200, point.X, 1e-8); Assert.AreEqual(150, point.Y, 1e-8);
    }
    [TestMethod] public void ResizeThenFitUsesNewViewport()
    {
        var model = Model(1, .2); var a = Check(model, 900, 400); var b = Check(model, 400, 900, a);
        Assert.AreNotEqual(a.Distance, b.Distance);
    }
    [TestMethod] public void ResetReturnsCanonicalCamera() => Assert.AreEqual(ColorSpaceCamera.Default, Check(Model(1, .2), 400, 900).Reset());
    [TestMethod] public void FitPreservesExpectedOrientation()
    {
        var start = new ColorSpaceCamera(27, 38, 2.4, .2, .4); var fit = Check(Model(.7, .3), 800, 400, start);
        Assert.AreEqual(start.Yaw, fit.Yaw); Assert.AreEqual(start.Pitch, fit.Pitch);
        Assert.AreNotEqual(ColorSpaceCamera.Default, fit);
    }
    [TestMethod] public void ProjectionMaintainsAspectRatio()
    {
        var cloud = Model(.5, .5).Source;
        var points = ColorSpaceProjection.Project(cloud, Front, 900, 300);
        Assert.AreEqual(points.Max(p => p.X) - points.Min(p => p.X), points.Max(p => p.Y) - points.Min(p => p.Y), 1e-9);
    }
    [TestMethod] public void FitIncludesOnlyVisibleClouds()
    {
        var small = Model(.1, .1); var wide = Model(1, 1);
        var sourceOnly = Check(small with { Reference = wide.Source }, 800, 400);
        var overlay = Check(small with { Reference = wide.Source, Mode = ColorCloudMode.Overlay }, 800, 400);
        Assert.IsGreaterThan(sourceOnly.Distance, overlay.Distance);
    }
    [TestMethod] public void InvalidViewportDoesNotClaimFit()
    {
        var state = ColorSpaceRendererContract.Create(Model(1, 1));
        foreach (var dimensions in new[] { (0d, 100d), (double.NaN, 100d), (100d, double.PositiveInfinity) })
            Assert.AreSame(state, state.Fit(dimensions.Item1, dimensions.Item2));
    }
    [TestMethod] public void InvalidGestureDoesNotPoisonCamera()
    {
        var camera = ColorSpaceCamera.Default;
        Assert.AreEqual(camera, camera.Pan(double.NaN, 1)); Assert.AreEqual(camera, camera.Zoom(double.PositiveInfinity));
        Assert.AreEqual(camera, camera.Rotate(1, double.NaN));
    }

    [TestMethod] public void RotatedFullGamutCornerMatrixFitsAllViewports()
    {
        var points = (from l in new[] { 0d, 1d }
                      from a in new[] { -.4, .4 }
                      from b in new[] { -.4, .4 }
                      select new ColorSpacePoint(new(l, a, b), new(128, 128, 128), 0, 0)).ToArray();
        var model = Model(1, 1);
        model = model with { Source = model.Source with { Points = points } };
        foreach (var (width, height) in new[] { (1180d, 720d), (1600d, 920d), (1920d, 1080d), (200d, 900d), (900d, 200d), (220d, 220d) })
        foreach (var yaw in new[] { -179d, -35d, 0d, 45d, 127d, 179d })
        foreach (var pitch in new[] { -89d, -30d, 0d, 18d, 65d, 89d })
            Check(model, width, height, new(yaw, pitch, .5, 10, -10));
    }

    [TestMethod] public void FitUsesRendererVisibleModeAndModeChangeInvalidatesFit()
    {
        var small = Model(.1, .1); var wide = Model(1, 1);
        var state = ColorSpaceRendererContract.Create(small with { Reference = wide.Source }).Fit(400, 900);
        var sourceDistance = state.Camera.Distance;
        state = state.WithMode(ColorCloudMode.Reference);
        Assert.IsFalse(state.IsFit);
        Assert.AreSame(wide.Source, state.VisibleClouds.Single());
        state = state.Fit(400, 900);
        Assert.IsGreaterThan(sourceDistance, state.Camera.Distance);
        foreach (var point in ColorSpaceProjection.Project(state.VisibleClouds.Single(), state.Camera, 400, 900))
            Assert.IsTrue(point.X >= 40 - 1e-8 && point.X <= 360 + 1e-8 && point.Y >= 90 - 1e-8 && point.Y <= 810 + 1e-8);
    }

    [TestMethod] public void PanUsesProjectionScaleAndScreenYDirection()
    {
        var cloud = Model(.3, .5).Source;
        foreach (var (width, height) in new[] { (900d, 300d), (300d, 900d) })
        foreach (var distance in new[] { .5, 2.4, 10 })
        {
            var camera = new ColorSpaceCamera(45, 25, distance, .2, -.4);
            var before = ColorSpaceProjection.Project(cloud, camera, width, height);
            var after = ColorSpaceProjection.Project(cloud, ColorSpaceProjection.PanByDisplayDelta(camera, 37, 19, width, height), width, height);
            for (var i = 0; i < before.Count; i++)
            {
                Assert.AreEqual(37, after[i].X - before[i].X, 1e-8);
                Assert.AreEqual(19, after[i].Y - before[i].Y, 1e-8);
            }
        }
    }

    [TestMethod] public void NonFinitePointsAreExcludedWithoutChangingPointIdentity()
    {
        var model = Model(.4, .6);
        var valid = model.Source.Points[0];
        model = model with { Source = model.Source with { Points = [valid with { Lab = new(double.NaN, 0, 0) }, valid] } };
        var fit = Check(model, 300, 600);
        var projected = ColorSpaceProjection.Project(model.Source, fit, 300, 600).Single();
        Assert.AreEqual(1, projected.PointIndex);
    }

    [TestMethod] public void RepeatedZoomUsesActualProjectionLimits()
    {
        var camera = Front;
        for (var i = 0; i < 200; i++) camera = camera.Zoom(1.12);
        Assert.AreEqual(ColorSpaceProjection.MinimumDistance, camera.Distance);
        for (var i = 0; i < 200; i++) camera = camera.Zoom(.89);
        Assert.AreEqual(ColorSpaceProjection.MaximumDistance, camera.Distance);
        Check(Model(1, 1), 400, 900, camera);
    }
}
