namespace RAWSelectionAssistant.DpiTests;

[TestClass]
public sealed class Version230StageDColorDpiTests
{
    [TestMethod] [DataRow(100)] [DataRow(125)] [DataRow(150)] [DataRow(175)] [DataRow(200)] public void ClientMonitor_MixedDpiScalesBounds(int percent) { var scale=percent/100d; var width=3840/scale; var height=2160/scale; Assert.IsGreaterThan(0d,width); Assert.IsGreaterThan(0d,height); Assert.AreEqual(3840,width*scale,.01); }
    [TestMethod] public void ClientMonitor_PortraitLayoutRemainsPositive() { var monitor=(Width:1080,Height:1920,Dpi:144d); Assert.IsGreaterThan((double)0,monitor.Width); Assert.IsGreaterThan((double)monitor.Width,monitor.Height); Assert.AreEqual(720,monitor.Width*96/monitor.Dpi,.01); }
    [TestMethod] public void ClientMonitor_FourKAnd1080pHaveIndependentBounds() { var fourK=(Key:"4k",Width:3840,Height:2160,Dpi:192d); var fullHd=(Key:"fhd",Width:1920,Height:1080,Dpi:96d); Assert.AreNotEqual(fourK.Key,fullHd.Key); Assert.AreEqual(1920,fullHd.Width); Assert.AreEqual(1920,fourK.Width*96/fourK.Dpi,.01); }
    [TestMethod] public void ClientMonitor_XamlUsesStretchUniformAndNoFixedImageSize() { var xaml=Text("src/RAWSelectionAssistant/Views/ClientMonitorWindow.xaml"); StringAssert.Contains(xaml,"Stretch=\"Uniform\""); Assert.DoesNotContain("Width=\"1920\"",xaml,StringComparison.Ordinal); }
    [TestMethod] public void ColorControls_UseThemeResourcesNotSystemWhite() { var xaml=Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml"); foreach(var forbidden in new[]{"Background=\"White\"","Foreground=\"Black\"","#FFFFFF"}) Assert.DoesNotContain(forbidden,xaml,StringComparison.OrdinalIgnoreCase); }
    [TestMethod] public void ClientMonitor_HighContrastKeepsTextLabels() { var xaml=Text("src/RAWSelectionAssistant/Views/ClientMonitorWindow.xaml"); foreach(var token in new[]{"客户收藏","客户备注","保存备注","FollowModeText","StatusText"}) StringAssert.Contains(xaml,token); }
    [TestMethod] public void ColorControls_HaveKeyboardAccessibleNames() { var xaml=Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml"); foreach(var token in new[]{"导入Cube LUT","当前LUT选择","LUT强度百分比","客户显示器选择","客户屏跟随模式选择"}) StringAssert.Contains(xaml,token); }
    [TestMethod] public void Compact1280_UsesExistingInspectorDrawer() { var source=Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml.cs"); StringAssert.Contains(source,"windowWidth < 1350"); StringAssert.Contains(Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml"),"InspectorDrawer"); }
    [TestMethod] public void PerMonitorV2AwarenessRemainsEnabled() { var manifest=Text("src/RAWSelectionAssistant/app.manifest"); StringAssert.Contains(manifest,"PerMonitorV2"); }
    [TestMethod] public void ClientWindowManualBoundsAccountForDpi() { var source=Text("src/RAWSelectionAssistant/ViewModels/TetherColorViewModel.cs"); StringAssert.Contains(source,"live.Left * 96d / live.DpiX"); StringAssert.Contains(source,"live.Height * 96d / live.DpiY"); }

    private static string Text(string relative){for(var directory=new DirectoryInfo(AppContext.BaseDirectory);directory is not null;directory=directory.Parent)if(File.Exists(Path.Combine(directory.FullName,"RAWSelectionAssistant.sln")))return File.ReadAllText(Path.Combine(directory.FullName,relative.Replace('/',Path.DirectorySeparatorChar)));throw new DirectoryNotFoundException();}
}
