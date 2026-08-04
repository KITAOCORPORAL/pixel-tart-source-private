namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class UiPolish142Tests
{
    [TestMethod] public void Sidebar_UsesStableNavigationFooterRows() => Contains(MainXaml(), "x:Name=\"SidebarLayout\"", "<RowDefinition Height=\"10\" />", "<RowDefinition Height=\"*\" />", "<RowDefinition Height=\"Auto\" />");
    [TestMethod] public void Sidebar_NavigationScrollsWithoutVisualScrollbar() => Contains(MainXaml(), "x:Name=\"SidebarNavigationScroll\"", "VerticalScrollBarVisibility=\"Hidden\"", "HorizontalScrollBarVisibility=\"Disabled\"");
    [TestMethod] public void Sidebar_EditionCardIsCompact() => Contains(Text("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Status.xaml"), "SidebarEditionCard", "Padding\" Value=\"10", "Margin\" Value=\"6,5", "Height\" Value=\"40");
    [TestMethod] public void Sidebar_DoesNotRepeatBrand() { DoesNotContain(MainXaml(), "<Image Source=\"Assets/AppIcon.ico\"", "本地摄影工具箱"); Contains(MainXaml(), "Title=\"像素蛋挞\""); }
    [TestMethod] public void GlobalStatusBar_IsIndependentFixedRow() => Contains(MainXaml(), "<RowDefinition Height=\"34\" />", "Grid.Row=\"2\" Height=\"34\"", "BorderThickness=\"0,1,0,0\"");
    [TestMethod] public void Workbench_HasBalancedThreeColumnShell() => Contains(MainXaml(), "x:Name=\"WorkbenchShell\"", "MinWidth=\"760\"", "x:Name=\"WorkbenchTaskColumn\" Width=\"320\"");
    [TestMethod] public void Workbench_UsesDenseVerticalRhythm() => Contains(MainXaml(), "<RowDefinition Height=\"106\" />", "<RowDefinition Height=\"230\" />", "x:Name=\"RecentProjectsArea\"");
    [TestMethod] public void EditionUpgrade_IsInsideSidebarFooter() => Contains(MainXaml(), "x:Name=\"EditionStatusArea\"", "Content=\"升级\"", "CommandParameter=\"Activation\"");
    [TestMethod] public void CancelTask_IsLocalWorkflowAction() => Contains(MainXaml(), "x:Name=\"CancelButton\" Content=\"取消当前任务\"", "Style=\"{StaticResource DangerButton}\"");
    [TestMethod] public void ThemesStillProvideSemanticSurfaceResources() { Contains(Text("src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Light.xaml"), "WorkbenchCardBrush", "DividerBrush"); Contains(Text("src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Dark.xaml"), "WorkbenchCardBrush", "DividerBrush"); }
    [TestMethod] public void VersionIs210() => Contains(Text("src/RAWSelectionAssistant.Core/Models/Branding.cs"), "ProductVersion = \"2.1.0\"");
    [TestMethod] public void WinExeRemainsEnabled() => Contains(Text("src/RAWSelectionAssistant/RAWSelectionAssistant.csproj"), "<OutputType>WinExe</OutputType>");

    private static string MainXaml() => Text("src/RAWSelectionAssistant/MainWindow.xaml");
    private static void Contains(string text, params string[] values) { foreach (var value in values) StringAssert.Contains(text, value); }
    private static void DoesNotContain(string text, params string[] values) { foreach (var value in values) Assert.IsFalse(text.Contains(value, StringComparison.Ordinal)); }
    private static string Text(string path) => File.ReadAllText(Path.Combine(Root(), path.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent; return directory?.FullName ?? throw new DirectoryNotFoundException(); }
}
