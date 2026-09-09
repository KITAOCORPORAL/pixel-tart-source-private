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
        CollectionAssert.AreEqual(new[] { "打开大图预览", "在新窗口打开", "在默认应用打开", "使用其他应用打开 ›", "在文件资源管理器中显示", "打开原文件位置 ›", "用于策划 ›", "加入与整理 ›", "素材编辑 ›", "工作流 ›", "导出 ›" }, headers.Take(11).ToArray());
        Assert.AreEqual(1, document.Descendants().Count(e => e.Name.LocalName == "MenuItem" && (string?)e.Attribute("Header") == "加入灵感托盘"));
        Assert.AreEqual(1, document.Descendants().Count(e => e.Name.LocalName == "MenuItem" && (string?)e.Attribute("Header") == "用其他文件替换"));
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
