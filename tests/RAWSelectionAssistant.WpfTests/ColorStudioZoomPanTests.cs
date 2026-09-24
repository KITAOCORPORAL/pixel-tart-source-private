using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ColorStudioZoomPanTests
{
    private static BitmapSource Image(int width = 1000, int height = 800)
    {
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, new byte[width * height * 4], width * 4);
        image.Freeze(); return image;
    }
    private static ColorStudioZoomPanState State(Size? viewport = null)
    { var state = new ColorStudioZoomPanState(); state.Configure(viewport ?? new Size(500, 400), new Size(1000, 800)); return state; }
    [TestMethod]
    public void FitAndActualPixelsTests()
    {
        var state = State(); Assert.AreEqual(.5, state.Zoom);
        state.SetZoom(1); Assert.AreEqual(1000, state.ImageRect(new Rect(0, 0, 500, 400)).Width);
        state.PanBy(new Vector(100, 80)); state.Fit();
        Assert.AreEqual(0, state.PanX); Assert.AreEqual(0, state.PanY); Assert.IsTrue(state.IsFit);
        state.SetZoom(9); Assert.AreEqual(4, state.Zoom);
        state.SetZoom(.1); Assert.AreEqual(.25, state.Zoom);
    }
    [TestMethod]
    public void ZoomAboutCursorPreservesImageCoordinateTests()
    {
        var state = State(); var image = Image(); var point = new Point(170, 135); var size = new Size(500, 400);
        var before = ColorStudioSampleMapping.Map(point, size, "原片", .5, image, image, state)!.Value;
        state.ZoomAbout(point, 2);
        var after = ColorStudioSampleMapping.Map(point, size, "原片", .5, image, image, state)!.Value;
        Assert.AreEqual(before.X, after.X); Assert.AreEqual(before.Y, after.Y);
    }
    [TestMethod]
    public void PanClampUsesImageBoundsTests()
    {
        var state = State(); state.SetZoom(1); state.PanBy(new Vector(9999, -9999));
        Assert.AreEqual(250, state.PanX); Assert.AreEqual(-200, state.PanY);
        state.SetZoom(.25); Assert.AreEqual(0, state.PanX); Assert.AreEqual(0, state.PanY);
    }
    [TestMethod]
    public void EyedropperZoomPanSplitAndProxyAlignmentTests()
    {
        var state = State(); state.SetZoom(1); state.PanBy(new Vector(50, 30));
        var original = Image(); var proxy = Image(500, 400); var point = new Point(350, 200);
        var a = ColorStudioSampleMapping.Map(point, new Size(500, 400), "原片", .5, original, proxy, state)!.Value;
        var b = ColorStudioSampleMapping.Map(point, new Size(500, 400), "左右对比", .5, original, proxy, state)!.Value;
        Assert.AreSame(proxy, b.Image); Assert.AreEqual(a.X / 2, b.X); Assert.AreEqual(a.Y / 2, b.Y);
        Assert.AreEqual(550, a.X); Assert.AreEqual(370, a.Y);
    }
    [TestMethod]
    public void EyedropperLinkedSideBySideTests()
    {
        var size = new Size(1001, 400); var state = State(); state.SetZoom(1.5); state.PanBy(new Vector(-80, 20));
        var original = Image(); var matched = Image();
        var left = ColorStudioSampleMapping.Map(new Point(180, 120), size, "并排对比", .5, original, matched, state)!.Value;
        var right = ColorStudioSampleMapping.Map(new Point(681, 120), size, "并排对比", .5, original, matched, state)!.Value;
        Assert.AreSame(original, left.Image); Assert.AreSame(matched, right.Image);
        Assert.AreEqual(left.X, right.X); Assert.AreEqual(left.Y, right.Y);
        Assert.IsNull(ColorStudioSampleMapping.Map(new Point(500.5, 120), size, "并排对比", .5, original, matched, state));
    }
    [TestMethod]
    public void EyedropperZoomPanLetterboxAndOutsideRejectTests()
    {
        var state = State(); state.SetZoom(.25); var image = Image();
        Assert.IsNull(ColorStudioSampleMapping.Map(new Point(10, 10), new Size(500, 400), "原片", .5, image, null, state));
        state.SetZoom(4);
        Assert.IsNull(ColorStudioSampleMapping.Map(new Point(-1, 100), new Size(500, 400), "原片", .5, image, null, state));
    }
}
