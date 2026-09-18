using System.IO;
using System.Text.RegularExpressions;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class StageIVReferenceWorkflowTests
{
    [TestMethod]
    public async Task AccordionAndInspectorWidthSurviveStoreReopen()
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-StageIV",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var path=Path.Combine(root,"preferences.json");
        try
        {
            var expected=new TetherWorkspacePreferences(444,false,true,false,true,true,false,true,true,true);
            await new TetherWorkspacePreferenceStore(path).SaveAsync(expected);var actual=await new TetherWorkspacePreferenceStore(path).LoadAsync();Assert.AreEqual(expected,actual);
        }
        finally{Directory.Delete(root,true);}
    }

    [TestMethod]
    public void TetherXamlContainsReferencePickerWeightsAndProjectSelection()
    {
        var text=File.ReadAllText(Path.Combine(Root(),"src/RAWSelectionAssistant/Views/TetherCaptureView.xaml"))+
            File.ReadAllText(Path.Combine(Root(),"src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs"));
        foreach(var value in new[]{"选择参考来源","项目色彩方案","灵感板","自由画布","素材库","最近使用","导入本地参考图","参考权重滑块","选择联机会话关联项目"})StringAssert.Contains(text,value);
    }

    [TestMethod]
    public void TetherXamlContainsZoneQuickPreviewPinnedSecondDisplayAndNextCapture()
    {
        var text=File.ReadAllText(Path.Combine(Root(),"src/RAWSelectionAssistant/Views/TetherCaptureView.xaml"))+
            File.ReadAllText(Path.Combine(Root(),"src/RAWSelectionAssistant/ViewModels/TetherCaptureViewModel.cs"));
        foreach(var value in new[]{"影调分区零到十","快速预览拍摄参考","固定参考","打开第二屏拍摄参考窗口","下一张拍摄规则工具组","命名预览","剩余空间"})StringAssert.Contains(text,value);
    }

    [TestMethod]
    public void TetherSessionAdjustmentDoesNotMutateColorSchemeUntilExplicitSave()
    {
        var viewModel=File.ReadAllText(Path.Combine(Root(),"src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs"));
        var setter=Regex.Match(viewModel,@"private void SetParameter\(.*?\n    \}",RegexOptions.Singleline).Value;
        var keepTone=Regex.Match(viewModel,@"public bool KeepOriginalTone .*?\n    private double",RegexOptions.Singleline).Value;
        Assert.DoesNotContain("_store.SaveAsync",setter,"现场 Slider 不得隐式保存色彩方案。");
        Assert.DoesNotContain("_store.SaveAsync",keepTone,"保持原片影调不得隐式保存色彩方案。");
        StringAssert.Contains(viewModel,"SaveCurrentAdjustmentCommand");
        StringAssert.Contains(viewModel,"RestoreSchemeCommand");
    }

    [TestMethod]
    public void TetherAdvancedReferenceEditorExposesExplicitRestoreAndSave()
    {
        var xaml=File.ReadAllText(Path.Combine(Root(),"src/RAWSelectionAssistant/Views/TetherCaptureView.xaml"));
        foreach(var value in new[]{"高级调整","SessionAdjustmentText","恢复色彩方案","保存当前调整为色彩方案"}) StringAssert.Contains(xaml,value);
    }

    [TestMethod]
    public void StandaloneReferenceColorToolAndTetherRoundTripEntryExist()
    {
        var product=File.ReadAllText(Path.Combine(Root(),"src/RAWSelectionAssistant.Core/Models/ProductToolboxPolicy.cs"));
        var main=File.ReadAllText(Path.Combine(Root(),"src/RAWSelectionAssistant/ViewModels/MainViewModel.cs"));
        var tether=File.ReadAllText(Path.Combine(Root(),"src/RAWSelectionAssistant/Views/TetherCaptureView.xaml"));
        var workspace=File.ReadAllText(Path.Combine(Root(),"src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml"));
        foreach(var value in new[]{"参考仿色","ToolIconReferenceColor","ReferenceColor"}) StringAssert.Contains(product,value);
        StringAssert.Contains(main,"AcceptContextAsync"); StringAssert.Contains(main,"NavigateToSurface(\"ReferenceColor\")");
        StringAssert.Contains(tether,"打开完整参考仿色");
        foreach(var value in new[]{"选择目标图片","仿色参数","保存为色彩方案","导出 3D LUT","TetherReferenceSplitView"}) StringAssert.Contains(workspace,value);
    }

    private static string Root(){for(var directory=new DirectoryInfo(AppContext.BaseDirectory);directory is not null;directory=directory.Parent)if(File.Exists(Path.Combine(directory.FullName,"RAWSelectionAssistant.sln")))return directory.FullName;throw new DirectoryNotFoundException();}
}
