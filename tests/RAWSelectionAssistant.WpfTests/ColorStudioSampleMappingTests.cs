using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ColorStudioSampleMappingTests
{
    [TestMethod]
    public void EyedropperOriginalViewMappingTests() => Check("原片", 100, 50, false, 50, 25);

    [TestMethod]
    public void EyedropperMatchedViewMappingTests() => Check("仿色结果", 100, 50, true, 50, 25);

    [TestMethod]
    public void EyedropperSplitViewMappingTests()
    {
        Check("左右对比", 40, 50, false, 20, 25);
        Check("左右对比", 160, 50, true, 80, 25);
    }

    [TestMethod]
    public void EyedropperSideBySideMappingTests()
    {
        Check("并排对比", 50, 50, false, 50, 25);
        Check("并排对比", 150, 50, true, 49, 25);
    }

    [TestMethod]
    public void EyedropperLetterboxRejectTests()
    {
        var image = Image(100, 50);
        Assert.IsNull(ColorStudioSampleMapping.Map(new Point(100, 5), new Size(200, 200), "原片", .5, image, null));
    }

    [TestMethod]
    public void ColorStudioParameterValueColorTests()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent;
        Assert.IsNotNull(directory);
        var xaml = File.ReadAllText(Path.Combine(directory!.FullName, "src", "RAWSelectionAssistant", "Views", "ReferenceColorWorkspaceView.xaml"));
        StringAssert.Contains(xaml, "x:Key=\"ParameterValue\"");
        StringAssert.Contains(xaml, "Value=\"{DynamicResource TextValueBrush}\"");
        Assert.IsFalse(xaml.Contains("#B31F2725", StringComparison.Ordinal));
    }

    private static void Check(string mode, double x, double y, bool matched, int expectedX, int expectedY)
    {
        var original = Image(100, 50); var result = Image(100, 50);
        var mapped = ColorStudioSampleMapping.Map(new Point(x, y), new Size(200, 100), mode, .5, original, result);
        Assert.IsNotNull(mapped);
        Assert.AreSame(matched ? result : original, mapped.Value.Image);
        Assert.AreEqual(expectedX, mapped.Value.X);
        Assert.AreEqual(expectedY, mapped.Value.Y);
    }

    private static BitmapSource Image(int width, int height)
    {
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, new byte[width * height * 4], width * 4);
        image.Freeze(); return image;
    }
}
