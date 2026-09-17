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
        CollectionAssert.AreEqual(new[]
        {
            "查看", "查看大图", "快速预览", "默认程序打开", "打开文件位置", "复制文件", "用于创作",
            "整理", "移到文件夹", "添加标签", "颜色标记", "评分",
            "项目", "关联项目…", "关联拍摄…", "处理状态",
            "灵感", "加入灵感板", "导出", "导出图片", "复制路径",
            "管理", "从当前位置移除", "归档", "恢复归档", "移到回收站", "从回收站恢复"
        }, headers);
        Assert.AreEqual(1, document.Descendants().Count(e => e.Name.LocalName == "MenuItem" && (string?)e.Attribute("Header") == "临时收集"));
        Assert.AreEqual(2, document.Descendants().Count(e => e.Name.LocalName == "MenuItem" && ((string?)e.Attribute("Header") ?? string.Empty).Contains("灵感板", StringComparison.Ordinal)));
        Assert.AreEqual(0, document.Descendants().Count(e => e.Name.LocalName == "MenuItem" && ((string?)e.Attribute("Header") ?? string.Empty).Contains("永久", StringComparison.Ordinal)));
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
