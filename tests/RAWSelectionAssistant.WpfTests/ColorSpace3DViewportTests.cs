using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ColorSpace3DViewportTests
{
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
