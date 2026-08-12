using System.IO;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version230Rc2VisibleFeatureAuditTests
{
    [TestMethod]
    public void Watermark_ExportAndInputActionsAreExplicitlyPreviewDisabled()
    {
        var source = Text("src/RAWSelectionAssistant/MainWindow.xaml");
        StringAssert.Contains(source, "Text=\"预览功能\"");
        StringAssert.Contains(source, "Content=\"导出功能开发中\"");
        StringAssert.Contains(source, "当前版本仅支持水印布局预览，批量导出尚未开放。");
        StringAssert.Contains(source, "AutomationProperties.HelpText=\"此功能仍在开发中\"");
        StringAssert.Contains(source, "ToolTipService.ShowOnDisabled=\"True\"");
        StringAssert.Contains(source, "Content=\"添加照片\" Style=\"{StaticResource SecondaryButton}\" IsEnabled=\"False\"");
    }

    [TestMethod]
    public void Watermark_IsNotProductionOrDefaultPinned()
    {
        var definition = ToolRegistry.Get(ToolId.Watermark);
        Assert.AreEqual(FeatureAvailability.Preview, definition.Availability);
        Assert.IsFalse(ProductToolboxPolicy.ProductionCatalog.Any(item => item.Id == ToolId.Watermark));
        Assert.DoesNotContain(definition.SettingsId, ProductToolboxPolicy.DefaultPinnedTools);
    }

    [TestMethod]
    public void VisibleFeatureAudit_DeclaresRequiredColumnsAndWatermarkPolicy()
    {
        var source = Text("docs/audit/PixelTart_VisibleFeatureAudit_RC2.md");
        StringAssert.Contains(source, "| Surface | Control | State | Real Action | Error Handling | Installed Verified |");
        StringAssert.Contains(source, "| 批量水印 | 批量导出 | PreviewDisabled |");
        StringAssert.Contains(source, "Zero Dead Control");
    }

    private static string Text(string relativePath)
    {
        var path = Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path);
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
