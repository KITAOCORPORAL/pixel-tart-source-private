using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class StudioInteractiveSchedulingTests
{
    [TestMethod]
    public Task ContinuousCurveGesturePublishesProxyFramesBeforeReleaseAndSettlesAtLatestFullFrame() => RunSta(async () =>
    {
        using var editor = new TetherReferenceModeViewModel(allowReferenceManagement: true);
        var pixels = Enumerable.Repeat((byte)120, 1200 * 20 * 3).ToArray();
        var image = BitmapSource.Create(1200, 20, 96, 96, PixelFormats.Rgb24, null, pixels, 3600); image.Freeze();
        editor.Enabled = true; await editor.SetSourceAsync(Guid.NewGuid(), image);
        var frames = new List<int>();
        editor.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(editor.MatchedImage) && editor.MatchedImage is { } frame) frames.Add(frame.PixelWidth); };
        editor.BeginEditTransaction();
        for (var i = 0; i < 35; i++)
        {
            editor.SetCurvePoints("rgb", [new(0, 0), new(.5, .5 + i / 200d), new(1, 1)]);
            await Task.Delay(20);
        }
        Assert.IsGreaterThan(1, frames.Count, "Dragging must not starve preview until all mouse movement stops.");
        Assert.IsTrue(frames.Contains(800), "Interactive work must use the existing lightweight proxy.");
        editor.CommitEditTransaction();
        for (var i = 0; i < 100 && !editor.IsSettled; i++) await Task.Delay(20);
        Assert.IsTrue(editor.IsSettled); Assert.AreEqual(1200, editor.MatchedImage!.PixelWidth);
        Assert.AreEqual(.67, ColorStudioToolProcessor.ReadCurve(editor.ToolNode(ColorStudioNodeType.Curve)!, "rgb")[1].Y, .00001);
        Assert.HasCount(1, Enumerable.Range(0, editor.NativeUndoCount).ToArray());
        editor.UndoAdjustmentCommand.Execute(null); Assert.IsNull(editor.ToolNode(ColorStudioNodeType.Curve));
    });
}
