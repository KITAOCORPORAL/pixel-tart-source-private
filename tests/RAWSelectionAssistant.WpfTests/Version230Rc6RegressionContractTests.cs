using System.IO;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version230Rc6RegressionContractTests
{
    [TestMethod]
    public void Calendar_PreservesNativeLayoutSkeleton()
    {
        var source = Read("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Inputs.xaml");
        StringAssert.Contains(source, "<Style TargetType=\"DatePicker\">");
        StringAssert.Contains(source, "x:Key=\"PixelTartCalendarNativeStyle\"");
        StringAssert.Contains(source, "<Style.Resources>");
        StringAssert.Contains(source, "<Style TargetType=\"Button\" />");
        Assert.IsFalse(source.Contains("PixelTartCalendarItemStyle", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("PixelTartCalendarDayButtonStyle", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("PixelTartCalendarButtonStyle", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("PART_MonthView", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("<ControlTemplate TargetType=\"DatePicker\"", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("<ControlTemplate TargetType=\"CalendarDayButton\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SurfaceHeader_SeparatesTitleAndCloseColumns()
    {
        var document = XDocument.Parse(Read("src/RAWSelectionAssistant/Views/SurfaceHeader.xaml"));
        var grid = document.Descendants().Single(element => element.Name.LocalName == "Grid");
        Assert.IsTrue(grid.Descendants().Any(element => element.Name.LocalName == "ColumnDefinition" && (element.Attribute("Width")?.Value ?? string.Empty) == "*"));
        Assert.IsTrue(grid.Descendants().Any(element => element.Name.LocalName == "ColumnDefinition" && (element.Attribute("Width")?.Value ?? string.Empty) == "48"));
        StringAssert.Contains(Read("src/RAWSelectionAssistant/Views/SurfaceHeader.xaml"), "Grid.Column=\"1\"");
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("RAWSelectionAssistant.sln was not found.");
    }
}
