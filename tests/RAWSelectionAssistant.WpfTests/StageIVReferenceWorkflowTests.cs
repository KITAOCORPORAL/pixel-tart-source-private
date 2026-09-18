using System.IO;
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

    private static string Root(){for(var directory=new DirectoryInfo(AppContext.BaseDirectory);directory is not null;directory=directory.Parent)if(File.Exists(Path.Combine(directory.FullName,"RAWSelectionAssistant.sln")))return directory.FullName;throw new DirectoryNotFoundException();}
}
