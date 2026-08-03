using System.Xml.Linq;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class SidebarInteractionFix202Tests
{
    [TestMethod] public void Requirement27_CollapsedWidth_IsAtLeast56Dip() => Assert.IsGreaterThanOrEqualTo(56d, Metric(nameof(SidebarLayoutMetrics.CollapsedWidth)));
    [TestMethod] public void Requirement28_CollapsedWidth_UsesRecommended60Dip() => Assert.AreEqual(60d, Metric(nameof(SidebarLayoutMetrics.CollapsedWidth)));
    [TestMethod] public void Requirement29_ExpandedWidth_Remains172Dip() => Assert.AreEqual(172d, Metric(nameof(SidebarLayoutMetrics.ExpandedWidth)));
    [TestMethod] public void Requirement30_Widths_UseOneNamedMetricsSource() { Contains(ViewModel(), "SidebarLayoutMetrics.CollapsedWidth", "SidebarLayoutMetrics.ExpandedWidth"); Contains(AppearanceService(), "SidebarLayoutMetrics.CollapsedWidth", "SidebarLayoutMetrics.ExpandedWidth"); }
    [TestMethod] public void Requirement31_DesignTokens_DeclareBothSidebarWidths() => Contains(Tokens(), "x:Key=\"SidebarExpandedWidth\">172", "x:Key=\"SidebarCollapsedWidth\">60");
    [TestMethod] public void Requirement32_SidebarContainer_EnforcesCollapsedMinimum() => Contains(MainXaml(), "MinWidth=\"{DynamicResource SidebarCollapsedWidth}\"", "Padding=\"0,5,0,8\"");
    [TestMethod] public void Requirement33_Navigation_HasNoHorizontalScrollbar() => Contains(MainXaml(), "HorizontalScrollBarVisibility=\"Disabled\"");
    [TestMethod] public void Requirement34_ButtonHeight_IsFortyDip() { Assert.AreEqual(40d, Metric(nameof(SidebarLayoutMetrics.ButtonHeight))); Contains(Navigation(), "SidebarButtonHeight"); }
    [TestMethod] public void Requirement35_ButtonHorizontalMargin_IsSixDip() { Assert.AreEqual(6d, Metric(nameof(SidebarLayoutMetrics.ButtonHorizontalMargin))); Contains(Tokens(), "SidebarButtonMargin\">6,1"); }
    [TestMethod] public void Requirement36_IconContainer_IsTwentyDip() { Assert.AreEqual(20d, Metric(nameof(SidebarLayoutMetrics.IconContainerSize))); Contains(Navigation(), "SidebarIconContainerSize", "SidebarIconColumnWidth"); Contains(Tokens(), "GridLength x:Key=\"SidebarIconColumnWidth\">20"); }
    [TestMethod] public void Requirement37_ActualIcon_IsEighteenDip() { Assert.AreEqual(18d, Metric(nameof(SidebarLayoutMetrics.IconSize))); Contains(Navigation(), "SidebarIconSize"); }
    [TestMethod] public void Requirement38_SelectedIndicator_ReservesThreeDip() { Assert.AreEqual(3d, Metric(nameof(SidebarLayoutMetrics.SelectedIndicatorWidth))); Contains(Tokens(), "SidebarSelectedBorderThickness\">3,0,0,0"); }
    [TestMethod] public void Requirement39_Icons_UseFixedUniformViewbox() => Contains(Navigation(), "<Viewbox Width=\"{DynamicResource SidebarIconSize}\"", "Stretch=\"Uniform\"", "UseLayoutRounding=\"True\"", "SnapsToDevicePixels=\"True\"");
    [TestMethod] public void Requirement40_CollapsedTemplate_RemovesTextColumn() => Contains(Navigation(), "x:Name=\"ExpandedContent\"", "x:Name=\"CollapsedContent\"", "TargetName=\"ExpandedContent\" Property=\"Visibility\" Value=\"Collapsed\"");
    [TestMethod] public void Requirement41_CollapsedIcon_IsCentered() => Contains(Navigation(), "HorizontalAlignment=\"Center\"", "VerticalAlignment=\"Center\" Visibility=\"Collapsed\"");
    [TestMethod] public void Requirement42_NoScaleTransformOrNegativeMargin() { DoesNotContain(Navigation(), "ScaleTransform", "Margin=\"-", "Padding=\"-"); DoesNotContain(SidebarSlice(), "ScaleTransform", "Margin=\"-"); }
    [TestMethod] public void Requirement43_FocusRing_RemainsVisible() => Contains(Navigation(), "x:Name=\"KeyboardFocusRing\"", "IsKeyboardFocused", "Visibility\" Value=\"Visible");
    [TestMethod] public void Requirement44_AllPrimaryEntries_UseStableTemplate() { var sidebar = SidebarSlice(); foreach (var name in new[] { "工作台", "归片工作区", "项目历史", "授权与版本", "设置", "帮助" }) Contains(sidebar, $"Content=\"{name}\""); DoesNotContain(sidebar, "Content=\"本地分片\""); Assert.IsGreaterThanOrEqualTo(Count(sidebar, "SidebarNavButton"), 10); }
    [TestMethod] public void Requirement45_AllFooterEntries_UseDedicatedRows() => Contains(SidebarSlice(), "Grid.Row=\"2\"", "Grid.Row=\"3\"", "Grid.Row=\"4\"", "ToolboxEntry.DisplayName", "Content=\"使用教程\"", "Content=\"问题反馈\"", "IconExpand", "展开侧栏");
    [TestMethod] public void Requirement46_EditionStatus_HidesLongTextWhenCollapsed() => Contains(SidebarSlice(), "x:Name=\"EditionStatusArea\"", "授权服务准备中", "IsSidebarExpanded", "IsSidebarCollapsed");
    [TestMethod] public void Requirement47_AllEntries_HaveTooltipsAndAutomationNames() { var sidebar = SidebarSlice(); Assert.IsGreaterThanOrEqualTo(Count(sidebar, "ToolTip="), 12); Assert.IsGreaterThanOrEqualTo(Count(sidebar, "AutomationProperties.Name="), 12); }
    [TestMethod] public void Requirement48_Settings_UsesUngatedDedicatedCommand() { Contains(MainXaml(), "x:Name=\"SidebarSettingsButton\"", "Command=\"{Binding OpenSettingsCommand}\""); Contains(ViewModel(), "OpenSettingsCommand = new RelayCommand(_ => IsSettingsModalOpen = true)"); }
    [TestMethod] public void Requirement49_ViewAllTools_ClosesPopupAndNavigates() { Contains(MainXaml(), "x:Name=\"ViewAllToolsButton\"", "Click=\"OpenToolboxPage_Click\""); Contains(CodeBehind(), "WorkbenchToolboxPopup.IsOpen = false", "OpenToolboxPageCommand.Execute(null)"); }
    [TestMethod] public void Requirement50_Dpi100To200_KeepsLogicalIconInsideButton() { var collapsedWidth = Metric(nameof(SidebarLayoutMetrics.CollapsedWidth)); var buttonHeight = Metric(nameof(SidebarLayoutMetrics.ButtonHeight)); var iconContainer = Metric(nameof(SidebarLayoutMetrics.IconContainerSize)); foreach (var scale in new[] { 1d, 1.25d, 1.5d, 1.75d, 2d }) { Assert.IsGreaterThanOrEqualTo(56d * scale, collapsedWidth * scale); Assert.IsGreaterThanOrEqualTo(iconContainer * scale, buttonHeight * scale); } XDocument.Parse(MainXaml()); XDocument.Parse(Navigation()); }

    private static string SidebarSlice()
    {
        var xaml = MainXaml();
        var start = xaml.IndexOf("x:Name=\"SidebarContainer\"", StringComparison.Ordinal);
        var end = xaml.IndexOf("<Grid Grid.Column=\"1\">", start, StringComparison.Ordinal);
        return xaml[start..end];
    }

    private static string MainXaml() => Text("src/RAWSelectionAssistant/MainWindow.xaml");
    private static string Navigation() => Text("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Navigation.xaml");
    private static string Tokens() => Text("src/RAWSelectionAssistant/Resources/DesignSystem/DesignTokens.xaml");
    private static string ViewModel() => Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
    private static string AppearanceService() => Text("src/RAWSelectionAssistant/Services/AppearanceService.cs");
    private static string CodeBehind() => Text("src/RAWSelectionAssistant/MainWindow.xaml.cs");
    private static double Metric(string fieldName) => Convert.ToDouble(typeof(SidebarLayoutMetrics).GetField(fieldName)?.GetRawConstantValue());
    private static int Count(string text, string value) { var result = 0; for (var index = 0; (index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length) result++; return result; }
    private static void Contains(string text, params string[] values) { foreach (var value in values) StringAssert.Contains(text, value); }
    private static void DoesNotContain(string text, params string[] values) { foreach (var value in values) Assert.IsFalse(text.Contains(value, StringComparison.Ordinal), $"不应包含：{value}"); }
    private static string Text(string path) => File.ReadAllText(Path.Combine(Root(), path.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent; return directory?.FullName ?? throw new DirectoryNotFoundException(); }
}
