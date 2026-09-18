using System.IO;
using System.Xml.Linq;
using static RAWSelectionAssistant.WpfTests.StageVPlanningFile;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class PlanningLayoutTests
{
    [TestMethod]
    public void PlanningUsesPhotographerFirstThreeColumnProjectFlow()
    {
        var xaml=Read("src/RAWSelectionAssistant/Views/PlanningCenterView.xaml");
        foreach(var token in new[]{"x:Name=\"ShotColumn\" Width=\"260\"","MinWidth=\"520\"","x:Name=\"InspectorColumn\" Width=\"320\"","拍摄清单","视觉方向","策划摘要","进入联机拍摄"})StringAssert.Contains(xaml,token);
        Assert.DoesNotContain("DataGrid",xaml,StringComparison.Ordinal);Assert.DoesNotContain("Planning Center",xaml,StringComparison.Ordinal);
    }

    [TestMethod]
    public void PlanningKeepsImagesUniformAndSupports1080pCompression()
    {
        var xaml=Read("src/RAWSelectionAssistant/Views/PlanningCenterView.xaml");
        StringAssert.Contains(xaml,"Stretch=\"Uniform\"");StringAssert.Contains(xaml,"MinWidth=\"210\"");StringAssert.Contains(xaml,"MaxWidth=\"310\"");StringAssert.Contains(xaml,"MinWidth=\"280\"");StringAssert.Contains(xaml,"MaxWidth=\"380\"");
    }

    [TestMethod]
    public void PlanningImageFirstLayoutUsesHeroAndResponsiveReferenceStrip()
    {
        var xaml=Read("src/RAWSelectionAssistant/Views/PlanningCenterView.xaml");
        StringAssert.Contains(xaml,"Height=\"330\"");
        StringAssert.Contains(xaml,"当前参考");
        StringAssert.Contains(xaml,"ItemsSource=\"{Binding ReferencesView}\"");
        StringAssert.Contains(xaml,"Width=\"150\"");
        StringAssert.Contains(xaml,"Background=\"{DynamicResource OverlayBrush}\"");
    }

    [TestMethod]
    public void PlanningInspectorDefaultsToViewModeAndExposesExplicitEditAction()
    {
        var xaml=Read("src/RAWSelectionAssistant/Views/PlanningCenterView.xaml");
        var vm=Read("src/RAWSelectionAssistant/ViewModels/PlanningCenterViewModel.cs");
        StringAssert.Contains(xaml,"ToggleInspectorEditCommand");
        StringAssert.Contains(xaml,"InverseBooleanToVisibilityConverter");
        StringAssert.Contains(vm,"IsInspectorEditing");
        StringAssert.Contains(vm,"InspectorEditText");
    }

    [TestMethod]
    public void PlanningPopupsAndMenusUseProductThemeResources()
    {
        var xaml=Read("src/RAWSelectionAssistant/Views/PlanningCenterView.xaml");
        StringAssert.Contains(xaml,"Style=\"{StaticResource Av2ContextMenu}\"");
        StringAssert.Contains(xaml,"Style=\"{StaticResource Av2ContextMenuItem}\"");
        StringAssert.Contains(xaml,"DialogCornerRadius");
    }
}

[TestClass]
public sealed class PlanningInteractionTests
{
    [TestMethod]
    public void PlanningHasSelectionQuickPreviewKeyboardAndContextMenus()
    {
        var xaml=Read("src/RAWSelectionAssistant/Views/PlanningCenterView.xaml");var code=Read("src/RAWSelectionAssistant/Views/PlanningCenterView.xaml.cs");
        foreach(var token in new[]{"SelectedItem=\"{Binding SelectedShot}\"","ReferenceCard_MouseLeftButtonDown","ContextMenu","查看大图","固定参考","从当前拍摄移除","IsQuickPreviewOpen"})StringAssert.Contains(xaml,token);
        foreach(var token in new[]{"Key.Up","Key.Down","Key.Space","Key.D","Key.Delete","Key.Escape","e.OriginalSource is TextBox"})StringAssert.Contains(code,token);
    }

    [TestMethod]
    public void PlanningNavigationIsProjectFlowNotPrimarySidebarEntry()
    {
        var main=Read("src/RAWSelectionAssistant/MainWindow.xaml");var vm=Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        StringAssert.Contains(main,"<views:PlanningCenterView");StringAssert.Contains(vm,"OpenPlanningAsync");StringAssert.Contains(vm,"ApplyExecutionContextAsync");
        Assert.DoesNotContain("CommandParameter=\"Planning\"",main,StringComparison.Ordinal);
    }
}

[TestClass]
public sealed class PlanningLanguageAndAccessibilityTests
{
    [TestMethod]
    public void PlanningUserFacingLanguageIsChineseAndHasNoForbiddenProductTerms()
    {
        var xaml=Read("src/RAWSelectionAssistant/Views/PlanningCenterView.xaml");
        var document=XDocument.Parse(xaml);
        var visibleText=string.Join("\n",document.Descendants().SelectMany(element=>new[]{"Text","Content","Header","ToolTip"}.Select(name=>element.Attribute(name)?.Value)).Where(value=>value is not null));
        foreach(var forbidden in new[]{"Planning Center","Project Brief","Asset Relationship","Execution Payload","Current Shot","Storyboard","Styling","Lighting","Pose","Preview","Pin"})Assert.DoesNotContain(forbidden,visibleText,StringComparison.OrdinalIgnoreCase);
        foreach(var required in new[]{"策划摘要","项目配色","项目色彩方案","项目灵感板","项目自由画布","灯位","姿势","分镜","造型"})StringAssert.Contains(xaml+Read("src/RAWSelectionAssistant.Core/Services/Projects/ProjectShotExecution.cs"),required);
    }

    [TestMethod]
    public void PlanningInteractiveControlsHaveAccessibleNames()
    {
        var document=XDocument.Parse(Read("src/RAWSelectionAssistant/Views/PlanningCenterView.xaml"));var interactive=new HashSet<string>(StringComparer.Ordinal){"Button","CheckBox","ComboBox","Slider","TextBox"};
        var unnamed=document.Descendants().Where(element=>interactive.Contains(element.Name.LocalName)).Where(element=>string.IsNullOrWhiteSpace(element.Attribute("AutomationProperties.Name")?.Value)&&element.Parent?.Name.LocalName!="ContextMenu").Select(element=>$"{element.Name.LocalName}:{element.Attribute("Content")?.Value??"unnamed"}").ToArray();
        Assert.HasCount(0,unnamed,string.Join(Environment.NewLine,unnamed));
    }

    [TestMethod]
    public void PlanningUsesExistingDarkThemeTokensInsteadOfLiteralColors()
    {
        var xaml=Read("src/RAWSelectionAssistant/Views/PlanningCenterView.xaml");
        Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(xaml,"#[0-9A-Fa-f]{3,8}"));foreach(var token in new[]{"PanelBorder","SurfaceSecondaryBrush","SurfaceElevatedBrush","CardCornerRadius","DialogCornerRadius","GhostButton","PrimaryButton"})StringAssert.Contains(xaml,token);
    }
}

file static class StageVPlanningFile
{
    public static string Read(string relative)=>File.ReadAllText(Path.Combine(Root(),relative.Replace('/',Path.DirectorySeparatorChar)));
    private static string Root(){for(var directory=new DirectoryInfo(AppContext.BaseDirectory);directory is not null;directory=directory.Parent)if(File.Exists(Path.Combine(directory.FullName,"RAWSelectionAssistant.sln")))return directory.FullName;throw new DirectoryNotFoundException();}
}
