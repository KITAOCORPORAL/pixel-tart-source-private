using System.IO;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetContextMenuVisualTests
{
    [TestMethod]
    public void AssetMenuUsesTaskOrderIconsAndExplicitDarkInteractionStates()
    {
        var root = RepositoryRoot();
        var page = XDocument.Parse(File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml")));
        var menu = page.Descendants().Single(element => element.Name.LocalName == "ContextMenu" &&
            element.Attributes().Any(attribute => attribute.Value == "AssetVisualContextMenu"));
        var direct = menu.Elements().Where(element => element.Name.LocalName == "MenuItem").ToArray();

        var sectionHeaders = direct.Where(element => ((string?)element.Attribute("Style") ?? string.Empty).Contains("AssetContextSectionHeader", StringComparison.Ordinal))
            .Select(element => (string?)element.Attribute("Header")).ToArray();
        CollectionAssert.AreEqual(new[] { "查看", "整理" }, sectionHeaders);

        var actionHeaders = direct.Where(element => !sectionHeaders.Contains((string?)element.Attribute("Header")))
            .Select(element => (string?)element.Attribute("Header")).ToArray();
        Assert.IsLessThan(Array.IndexOf(actionHeaders, "移到文件夹"), Array.IndexOf(actionHeaders, "查看大图"));
        Assert.IsLessThan(Array.IndexOf(actionHeaders, "工作流"), Array.IndexOf(actionHeaders, "移到文件夹"));
        Assert.IsLessThan(Array.IndexOf(actionHeaders, "导出"), Array.IndexOf(actionHeaders, "工作流"));
        Assert.IsLessThan(Array.IndexOf(actionHeaders, "管理"), Array.IndexOf(actionHeaders, "导出"));

        foreach (var header in new[] { "查看大图", "快速预览", "默认程序打开", "打开文件位置", "复制文件", "移到文件夹", "添加标签", "评分", "加入灵感板", "导出图片", "复制路径", "归档", "移到回收站" })
        {
            var item = menu.Descendants().Single(element => element.Name.LocalName == "MenuItem" && (string?)element.Attribute("Header") == header &&
                !((string?)element.Attribute("Style") ?? string.Empty).Contains("AssetContextSectionHeader", StringComparison.Ordinal));
            Assert.IsTrue(item.Elements().Any(element => element.Name.LocalName == "MenuItem.Icon"), $"{header} should use the Asset Action icon family.");
        }

        Assert.IsTrue(direct.Single(element => (string?)element.Attribute("Header") == "移到文件夹").Elements().Any(element => element.Name.LocalName == "MenuItem"));
        Assert.IsTrue(direct.Single(element => (string?)element.Attribute("Header") == "添加标签").Elements().Any(element => element.Name.LocalName == "MenuItem"));
        Assert.IsTrue(direct.Single(element => (string?)element.Attribute("Header") == "评分").Elements().Any(element => element.Name.LocalName == "MenuItem"));
        Assert.IsFalse(menu.Descendants().Any(element => ((string?)element.Attribute("Header") ?? string.Empty).Contains("照片查看器", StringComparison.Ordinal)));
        Assert.IsFalse(menu.Descendants().Any(element => ((string?)element.Attribute("Header") ?? string.Empty).Contains("永久删除", StringComparison.Ordinal)));

        var theme = File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant", "Resources", "DesignSystem", "Theme.Dark.xaml"));
        StringAssert.Contains(theme, "MenuItemHoverBrush\" Color=\"#1D2228");
        StringAssert.Contains(theme, "MenuItemOpenedBrush\" Color=\"#36414C");
        StringAssert.Contains(theme, "MenuShortcutBrush\" Color=\"#747C86");

        var template = File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant", "Resources", "DesignSystem", "Components.Foundation.xaml"));
        StringAssert.Contains(template, "ContentSource=\"Icon\"");
        StringAssert.Contains(template, "MenuItemHoverBrush");
        StringAssert.Contains(template, "MenuItemOpenedBrush");
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
