using System.IO;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class InspectorButtonContrastTests
{
    [TestMethod]
    public void PrimaryInspectorActionsUseBorderedSecondaryStyle()
    {
        var document = XDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml")));
        var buttons = document.Descendants().Where(element => element.Name.LocalName == "Button").ToArray();
        Assert.AreEqual("{DynamicResource PixelTart.Button.Secondary}", Attribute(buttons.Single(button => Attribute(button, "AutomationProperties.AutomationId") == "AssetInspectorWorkflowPicker"), "Style"));
        foreach (var label in new[] { "＋ 关联项目", "＋ 关联拍摄" })
            Assert.AreEqual("{DynamicResource PixelTart.Button.Secondary}", Attribute(buttons.Single(button => Attribute(button, "Content") == label), "Style"));
    }

    private static string? Attribute(XElement element, string suffix) => element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.EndsWith(suffix, StringComparison.Ordinal))?.Value;
    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
