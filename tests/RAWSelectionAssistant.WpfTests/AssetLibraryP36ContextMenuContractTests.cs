using System.Xml.Linq;
using System.IO;
using PixelTart.Modules.AssetLibrary;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP36ContextMenuContractTests
{
    [TestMethod]
    public void RightClickSelectionPromotesUnselectedCardAndPreservesBatchSelection()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var promoted = AssetLibraryContextSelectionPolicy.ResolveSelection([first], second);
        Assert.HasCount(1, promoted);
        Assert.Contains(second, promoted);

        var preserved = AssetLibraryContextSelectionPolicy.ResolveSelection([first, second], second);
        CollectionAssert.AreEquivalent(new[] { first, second }, preserved.ToArray());
    }

    [TestMethod]
    public void ContextMenuUsesSingleHierarchicalInformationArchitecture()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml"));
        var document = XDocument.Parse(xaml);
        var menu = document.Descendants().First(element => element.Name.LocalName == "ContextMenu" &&
            element.Attributes().Any(attribute => attribute.Value == "AssetVisualContextMenu"));
        var headers = menu.Elements().Where(e => e.Name.LocalName == "MenuItem").Select(e => (string?)e.Attribute("Header")).ToArray();
        CollectionAssert.AreEqual(new[] { "查看", "整理", "摄影工作流", "创作", "导出", "生命周期" }, headers);
        Assert.AreEqual(1, document.Descendants().Count(e => e.Name.LocalName == "MenuItem" && (string?)e.Attribute("Header") == "加入灵感托盘"));
        Assert.AreEqual(1, document.Descendants().Count(e => e.Name.LocalName == "MenuItem" && ((string?)e.Attribute("Header") ?? string.Empty).Contains("灵感集", StringComparison.Ordinal)));
        Assert.AreEqual(0, document.Descendants().Count(e => e.Name.LocalName == "MenuItem" && ((string?)e.Attribute("Header") ?? string.Empty).Contains("永久", StringComparison.Ordinal)));
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
