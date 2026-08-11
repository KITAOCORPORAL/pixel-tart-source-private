using System.IO;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.BatchCompression;
using RAWSelectionAssistant.Services.BatchCompression;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ProductToolboxWpfTests
{
    [TestMethod]
    public void BatchCompressionModal_CommunicatesSafeOutputContract()
    {
        var source = Text("src/RAWSelectionAssistant/Views/BatchCompressionModal.xaml");
        StringAssert.Contains(source, "独立输出目录");
        StringAssert.Contains(source, "不覆盖、移动或删除源文件");
        StringAssert.Contains(source, "开始压缩");
        StringAssert.Contains(source, "AllowDrop=\"True\"");
        StringAssert.Contains(source, "Width=\"320\"");
        StringAssert.Contains(source, "Av2PrimaryButton");
        StringAssert.Contains(source, "冲突策略：自动编号");
    }

    [TestMethod]
    public void BatchCompressionImplementation_UsesCreateNewAndAutoNumberPolicy()
    {
        var source = Text("src/RAWSelectionAssistant.Core/Services/BatchCompression/BatchCompressionSafeService.cs");
        StringAssert.Contains(source, "FileMode.CreateNew");
        StringAssert.Contains(source, "FileConflictPolicy.AutoNumber");
        StringAssert.Contains(source, "Flush(true)");
        Assert.DoesNotContain("File.Move(item.SourcePath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Delete(item.SourcePath", source, StringComparison.Ordinal);
    }

    [TestMethod]
    public void BatchCompressionEncoder_HasSupportedWpfImplementation()
    {
        Assert.IsInstanceOfType<IBatchCompressionEncoder>(new WpfBatchCompressionEncoder());
    }

    [TestMethod]
    public void RawToJpegTool_HasStableVectorIcon()
    {
        var tool = ProductToolboxPolicy.Get(ToolId.RawToJpeg);
        Assert.AreEqual("ToolIconRawToJpeg", tool.IconResourceKey);
        StringAssert.Contains(Text("src/RAWSelectionAssistant/Resources/DesignSystem/Icons.Tools.xaml"),
            "x:Key=\"ToolIconRawToJpeg\"");
    }

    [TestMethod]
    public void RuntimeQuickTools_UseProductLayoutAsCanonicalLoadSaveContract()
    {
        var viewModel = Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        StringAssert.Contains(viewModel,
            "Settings.PinnedQuickTools = ProductToolboxPolicy.Normalize(Settings.ProductQuickToolLayout.OrderedToolIds);");
        StringAssert.Contains(viewModel,
            "Settings.ProductQuickToolLayout.OrderedToolIds = productQuickTools.ToList();");
        StringAssert.Contains(viewModel, "await _settingsService.SaveAsync(Settings);");
        StringAssert.Contains(viewModel, "await _quickToolsRepository.SaveAsync(productQuickTools);");

        var manager = Text("src/RAWSelectionAssistant/Views/QuickToolsManagerWindow.xaml.cs");
        StringAssert.Contains(manager, "ProductToolboxPolicy.Normalize(currentToolIds)");
        StringAssert.Contains(manager, "_pinned.Count < ProductToolboxPolicy.MaximumPinnedTools");
        StringAssert.Contains(manager, "ResultToolIds=_pinned.Select(x=>x.SettingsId).ToArray()");
    }

    private static string Text(string relativePath) =>
        File.ReadAllText(Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
