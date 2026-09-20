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
        var document = XDocument.Parse(source);
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var dateStyles = document.Descendants().Where(element => element.Name.LocalName == "Style" && element.Attribute("TargetType")?.Value == "DatePicker").ToArray();
        var globalDateStyle = dateStyles.Single(element => element.Attribute(xaml + "Key") is null);
        Assert.IsFalse(globalDateStyle.Descendants().Any(element => element.Name.LocalName == "ControlTemplate"), "Global date picker must retain its native skeleton.");
        var proposalDateStyle = dateStyles.Single(element => element.Attribute(xaml + "Key")?.Value == "ProposalDatePicker");
        foreach (var part in new[] { "PART_TextBox", "PART_Button", "PART_Popup" })
            Assert.IsTrue(proposalDateStyle.Descendants().Any(element => element.Attribute(xaml + "Name")?.Value == part), "Proposal date picker must preserve native part: " + part);
        Assert.IsTrue(proposalDateStyle.Elements().Any(element => element.Attribute("Property")?.Value == "CalendarStyle" && element.Attribute("Value")?.Value == "{DynamicResource PixelTartCalendarNativeStyle}"));
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
