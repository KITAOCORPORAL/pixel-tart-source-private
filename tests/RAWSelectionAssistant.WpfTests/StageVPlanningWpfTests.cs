using System.IO;
using System.Xml.Linq;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class PlanningWorkspaceLayoutTests
{
    internal static string Read(string file)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return File.ReadAllText(Path.Combine(directory.FullName, file));
        throw new DirectoryNotFoundException();
    }
    [TestMethod]
    public void TwoColumnDocumentWorkspaceReplacesPermanentShotInspector()
    {
        var xaml = Read("src/RAWSelectionAssistant/Views/PlanningCenterView.xaml");
        StringAssert.Contains(xaml, "DocumentListColumn\" Width=\"280\"");
        StringAssert.Contains(xaml, "DocumentContent"); StringAssert.Contains(xaml, "ContextDrawer");
        Assert.DoesNotContain("InspectorColumn", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DataGrid", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("TabControl", xaml, StringComparison.Ordinal);
    }
    [TestMethod]
    public void SevenContentModulesHaveFixedChineseOrder()
    {
        CollectionAssert.AreEqual(new[] { "文字", "参考图", "情绪板", "镜头清单", "灯光图", "服化道", "文件" }, PlanningCenterViewModel.ContentPages.ToArray());
    }
    [TestMethod]
    public void DocumentPresentationUsesReadingWidthUniformImagesAndOptInEditing()
    {
        var code = Read("src/RAWSelectionAssistant/Views/PlanningCenterView.xaml.cs");
        foreach (var value in new[] { "? 940 : 1320", "Stretch.Uniform", "RenderText(_vm.IsDocumentEditing)", "RenderShots(true)", "IsPreviewMode", "Av2ContextMenu", "Av2ContextMenuItem" }) StringAssert.Contains(code, value);
        Assert.DoesNotContain("Stretch.UniformToFill", code, StringComparison.Ordinal);
    }
    [TestMethod]
    public void KeyboardGuardProtectsTextAndImeAndSupportsFlushPreviewSearch()
    {
        var code = Read("src/RAWSelectionAssistant/Views/PlanningCenterView.xaml.cs");
        foreach (var value in new[] { "TextBoxBase", "Key.ImeProcessed", "Key.S", "Key.P", "Key.F", "Key.Escape", "Key.Delete", "FlushAsync" }) StringAssert.Contains(code, value);
    }
    [TestMethod]
    public void XamlControlsHaveChineseAccessibleNamesAndThemeResources()
    {
        var xaml = Read("src/RAWSelectionAssistant/Views/PlanningCenterView.xaml");
        var document = XDocument.Parse(xaml);
        foreach (var control in document.Descendants().Where(element => element.Name.LocalName is "Button" or "TextBox" or "ListBox"))
            Assert.IsFalse(string.IsNullOrWhiteSpace(control.Attribute("AutomationProperties.Name")?.Value));
        foreach (var value in new[] { "SurfaceSecondaryBrush", "SurfaceElevatedBrush", "AccentBrush", "GhostButton", "PrimaryButton" }) StringAssert.Contains(xaml, value);
    }
    [TestMethod]
    public void PrimaryEntryAndSharedTetherBridgeRemain()
    {
        StringAssert.Contains(Read("src/RAWSelectionAssistant/MainWindow.xaml"), "CommandParameter=\"Planning\"");
        StringAssert.Contains(Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs"), "ApplyExecutionContextAsync");
        StringAssert.Contains(Read("src/RAWSelectionAssistant/ViewModels/PlanningCenterViewModel.cs"), "PlanningExecutionContextService");
    }
}
