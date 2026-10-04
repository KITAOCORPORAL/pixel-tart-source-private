using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ColorSpace3DViewportTests
{
    [TestMethod]
    public void PreviewMaskIncludesMatchesBeyondTwelveThousandAndClears()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var overlay = new ImageHighlightOverlay { ImageWidth = 200, ImageHeight = 100,
                    PixelIndices = Enumerable.Range(0, 20000).ToArray(), Width = 200, Height = 100 };
                overlay.Measure(new System.Windows.Size(200, 100)); overlay.Arrange(new System.Windows.Rect(0, 0, 200, 100));
                var rendered = new System.Windows.Media.Imaging.RenderTargetBitmap(200, 100, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rendered.Render(overlay);
                var pixel = new byte[4]; rendered.CopyPixels(new System.Windows.Int32Rect(190, 90, 1, 1), pixel, 4, 0);
                Assert.IsGreaterThan(0, pixel[3], "A matching pixel beyond index 12000 must remain visible.");
                overlay.PixelIndices = []; overlay.UpdateLayout();
                rendered = new System.Windows.Media.Imaging.RenderTargetBitmap(200, 100, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rendered.Render(overlay); rendered.CopyPixels(new System.Windows.Int32Rect(190, 90, 1, 1), pixel, 4, 0);
                Assert.AreEqual(0, pixel[3]);
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));
        if (failure is not null) throw failure;
    }
    [TestMethod]
    public void BoundsFitReframesOnResizeAndResetRemainsDistinct()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var points = new[]
                {
                    new ColorSpacePoint(new(.4, -.4, 0), new(255, 0, 0), 0, 0),
                    new ColorSpacePoint(new(.6, .4, 0), new(0, 255, 0), 1, 0)
                };
                var cloud = new ColorSpaceCloud(2, 1, 2, 2, points, "fit", new());
                var model = new ColorSpaceVisualizationModel(cloud, cloud, cloud, [], ColorCloudMode.Source, ColorSpaceSamplingTier.Preview, "fit");
                var view = new ColorSpace3DViewport(); view.SetModel(model);
                view.State = view.State! with { Camera = new(0, 0, 2.4, .3, .2) };
                Arrange(900, 300); view.FitCamera();
                var first = view.State!.Camera; Assert.IsTrue(view.State.IsFit); AssertBounds();
                Arrange(300, 900); AssertBounds();
                // A sphere is isotropic: swapping equal short edges keeps its fit distance.
                Assert.AreEqual(first.Distance, view.State.Camera.Distance, 1e-8);
                Assert.AreSame(model, view.State.Model, "The reference sphere must not rewrite sample coordinates.");
                Assert.AreEqual(0d, view.State.Camera.Yaw);
                view.ResetCamera(); Assert.IsFalse(view.State.IsFit); Assert.AreEqual(ColorSpaceCamera.Default, view.State.Camera);
                var reset = view.State.Camera; Arrange(700, 500); Assert.AreEqual(reset, view.State.Camera);

                void Arrange(double width, double height)
                {
                    view.Width = width; view.Height = height; view.Measure(new System.Windows.Size(width, height));
                    view.Arrange(new System.Windows.Rect(0, 0, width, height)); view.UpdateLayout();
                    var frame = new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
                    System.Windows.Threading.Dispatcher.PushFrame(frame);
                }
                void AssertBounds()
                {
                    foreach (var point in ColorSpaceProjection.Project(cloud, view.State!.Camera, view.ActualWidth, view.ActualHeight))
                    {
                        Assert.IsTrue(point.X >= view.ActualWidth * ColorSpaceProjection.FitPadding - 1e-8 && point.X <= view.ActualWidth * (1 - ColorSpaceProjection.FitPadding) + 1e-8);
                        Assert.IsTrue(point.Y >= view.ActualHeight * ColorSpaceProjection.FitPadding - 1e-8 && point.Y <= view.ActualHeight * (1 - ColorSpaceProjection.FitPadding) + 1e-8);
                    }
                }
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));
        if (failure is not null) throw failure;
    }

    [TestMethod]
    public void ViewportExposesRealModelBoundaryAndCameraActions()
    {
        var cloud = new ColorSpaceCloud(1, 1, 1, 1, [new(new(0.5, 0, 0), new VisualRgb24(128, 128, 128), 0, 0)], "fixture", new());
        var model = new ColorSpaceVisualizationModel(cloud, cloud, cloud, [], ColorCloudMode.Overlay, ColorSpaceSamplingTier.Preview, "fixture");
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var viewport = new ColorSpace3DViewport();
                viewport.SetModel(model); viewport.ResetCamera(); viewport.FitCamera();
                Assert.IsTrue(viewport.IsAvailable); Assert.AreEqual(ColorCloudMode.Overlay, viewport.Mode);
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10))); if (failure is not null) throw failure;
    }
}
