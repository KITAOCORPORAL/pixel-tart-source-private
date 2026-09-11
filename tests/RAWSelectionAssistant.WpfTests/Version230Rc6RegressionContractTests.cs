using System.IO;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version230Rc6RegressionContractTests
{
    [TestMethod]
    public void Calendar_UsesIndependentSemanticDimensions()
    {
        var source = Read("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Inputs.xaml");
        StringAssert.Contains(source, "x:Key=\"PixelTartCalendarDayButtonStyle\"");
        StringAssert.Contains(source, "MinWidth\" Value=\"40\"");
        StringAssert.Contains(source, "MinHeight\" Value=\"36\"");
        StringAssert.Contains(source, "MinWidth\" Value=\"320\"");
        StringAssert.Contains(source, "MinHeight\" Value=\"330\"");
        StringAssert.Contains(source, "MinWidth=\"294\" MinHeight=\"252\"");
        StringAssert.Contains(source, "<RowDefinition Height=\"252\" />");
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
