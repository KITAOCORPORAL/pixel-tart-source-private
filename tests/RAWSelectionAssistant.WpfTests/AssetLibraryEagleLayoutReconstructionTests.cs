using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryEagleLayoutReconstructionTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [TestMethod]
    public void ShellKeepsToolbarSidebarGalleryInspectorAndIconFirstActions()
    {
        var document = PageDocument();
        Assert.IsNotNull(AutomationElement(document, "AssetBrowserToolbar"));
        Assert.IsNotNull(AutomationElement(document, "AssetOrganizationPane"));
        Assert.IsNotNull(AutomationElement(document, "AssetCollectionPane"));
        Assert.IsNotNull(AutomationElement(document, "AssetInspectorPane"));

        foreach (var id in new[]
        {
            "AssetLibraryColorFilter", "AssetLibraryTagFilter", "AssetLibraryRatingFilter", "AssetLibraryDateFilter",
            "AssetLibrarySortMenu", "AssetLibraryImport", "AssetLibraryMore"
        })
        {
            var button = AutomationElement(document, id);
            Assert.AreEqual(Presentation + "Button", button.Name, id);
            Assert.IsTrue(string.IsNullOrWhiteSpace((string?)button.Attribute("Content")), $"{id} must remain icon first.");
            Assert.IsFalse(string.IsNullOrWhiteSpace((string?)button.Attribute("ToolTip")), $"{id} needs a tooltip.");
        }
    }

    [TestMethod]
    public void SidebarAndMasonryDefaultExposeEagleCollectionStructure()
    {
        var root = RepositoryRoot();
        var browser = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryViewModel.P2Browser.cs"));
        foreach (var label in new[] { "全部素材", "未分类", "未标签", "最近添加", "收藏" }) StringAssert.Contains(browser, $"\"{label}\"");

        var page = File.ReadAllText(PagePath(root));
        foreach (var label in new[] { "文件夹", "智能文件夹", "标签分组" }) StringAssert.Contains(page, $"Text=\"{label}\"");
        StringAssert.Contains(page, "Binding IsActive");

        var settings = File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant.Core", "Models", "AssetLibraryWorkspaceSettings.cs"));
        StringAssert.Contains(settings, "ViewMode { get; set; } = AssetLibraryViewMode.Masonry");
    }

    [TestMethod]
    public void GalleryPreservesPhotoAspectAndKeepsPrimaryMetadataMinimal()
    {
        var document = PageDocument();
        foreach (var key in new[] { "AssetGridCardTemplate", "AssetMasonryCardTemplate", "AssetJustifiedCardTemplate" })
        {
            var template = document.Descendants(Presentation + "DataTemplate").Single(node => XamlKey(node) == key);
            Assert.AreEqual("Uniform", (string?)template.Descendants(Presentation + "Image").First().Attribute("Stretch"));
            Assert.IsTrue(template.Descendants(Presentation + "TextBlock").Any(node => ((string?)node.Attribute("Text") ?? string.Empty).Contains("DisplayName", StringComparison.Ordinal)));
            Assert.IsTrue(template.Descendants(Presentation + "TextBlock").Any(node => ((string?)node.Attribute("Text") ?? string.Empty).Contains("DimensionsText", StringComparison.Ordinal)));
        }
    }

    [TestMethod]
    public void SelectionInspectorAndCenteredHighResolutionLoupeAreWired()
    {
        var root = RepositoryRoot();
        var document = PageDocument();
        foreach (var id in new[]
        {
            "AssetInspectorPreview", "AssetInspectorRating", "AssetInspectorColorLabels", "AssetInspectorComment",
            "AssetInspectorUrl", "AssetInspectorTags", "AssetInspectorFolderSummary", "AssetInspectorExport"
        }) Assert.IsNotNull(AutomationElement(document, id), id);

        var inspectorTabs = document.Descendants(Presentation + "TabControl").Single(node => AutomationId(node) == "VisualAnalysisTabs");
        Assert.AreEqual("{StaticResource AssetInspectorTabItem}", (string?)inspectorTabs.Attribute("ItemContainerStyle"));

        var popup = AutomationElement(document, "AssetQuickLoupePopup");
        Assert.AreEqual("Center", (string?)popup.Attribute("Placement"));
        var code = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.cs"));
        StringAssert.Contains(code, "ActualWidth * 0.5d");
        StringAssert.Contains(code, "ActualHeight * 0.55d");
        StringAssert.Contains(code, "ContextQuickPreview_Click");
        StringAssert.Contains(code, "QuickLoupePopup_MouseLeave");

        var viewModel = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryViewModel.cs"));
        StringAssert.Contains(viewModel, "singleMaterialized is not null && IsInspectorPaneCollapsed");
        Assert.DoesNotContain("singleMaterialized is not null && IsInspectorPaneCollapsed && CanShowInspectorPaneWhenExpanded", viewModel, StringComparison.Ordinal);
    }

    [TestMethod]
    public void MenuAndCreativeBoardUseProductFacingInformationArchitecture()
    {
        var document = PageDocument();
        var menu = document.Descendants(Presentation + "ContextMenu").Single(node => AutomationId(node) == "AssetVisualContextMenu");
        var groups = menu.Elements(Presentation + "MenuItem")
            .Where(node => ((string?)node.Attribute("Style") ?? string.Empty).Contains("AssetContextSectionHeader", StringComparison.Ordinal))
            .Select(node => (string?)node.Attribute("Header")).ToArray();
        CollectionAssert.AreEqual(new[] { "查看", "整理" }, groups);
        foreach (var group in new[] { "工作流", "导出", "管理" })
            Assert.IsTrue(menu.Elements(Presentation + "MenuItem").Single(node => (string?)node.Attribute("Header") == group).Elements(Presentation + "MenuItem").Any());
        foreach (var label in new[] { "快速预览", "关联项目…", "关联拍摄…", "加入灵感板", "复制路径", "归档" })
            Assert.IsTrue(menu.Descendants(Presentation + "MenuItem").Any(node => (string?)node.Attribute("Header") == label), label);

        var board = AutomationElement(document, "InspirationBoardPanel");
        Assert.AreEqual("Creative Board", AutomationName(board));
        Assert.IsTrue(board.Descendants(Presentation + "TextBlock").Any(node => (string?)node.Attribute("Text") == "灵感收藏"));
        Assert.IsTrue(board.Descendants(Presentation + "Image").All(node => (string?)node.Attribute("Stretch") != "UniformToFill"));
    }

    [TestMethod]
    public void ShellUsesThemeTokensInsteadOfHardcodedHexColors()
    {
        var root = RepositoryRoot();
        var page = File.ReadAllText(PagePath(root));
        var filter = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetQueryComposerView.xaml"));
        Assert.IsFalse(Regex.IsMatch(page + filter, "(?:Foreground|Background|BorderBrush|Stroke|Fill)=\"#[0-9A-Fa-f]{6,8}\""));
        StringAssert.Contains(page, "Brush.Background");
        StringAssert.Contains(page, "Brush.Surface.Elevated");
        StringAssert.Contains(page, "Brush.Text.Primary");
        StringAssert.Contains(page, "Brush.Text.Secondary");
    }

    private static XDocument PageDocument() => XDocument.Load(PagePath(RepositoryRoot()));
    private static XElement AutomationElement(XDocument document, string id) =>
        document.Descendants().Single(node => AutomationId(node) == id);
    private static string AutomationId(XElement element) => element.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName.EndsWith(".AutomationId", StringComparison.Ordinal))?.Value ?? string.Empty;
    private static string AutomationName(XElement element) => element.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName.EndsWith(".Name", StringComparison.Ordinal))?.Value ?? string.Empty;
    private static string XamlKey(XElement element) => element.Attribute(XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml") + "Key")?.Value ?? string.Empty;
    private static string PagePath(string root) => Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml");

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
