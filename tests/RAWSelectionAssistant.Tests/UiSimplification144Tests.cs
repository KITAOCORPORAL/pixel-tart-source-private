namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class UiSimplification144Tests
{
    [TestMethod] public void RootLayout_RetainsNativeMenuContentAndStatusRows() => Contains(MainXaml(), "<RowDefinition Height=\"36\" />", "<RowDefinition Height=\"*\" />", "<RowDefinition Height=\"34\" />");
    [TestMethod] public void GlobalBrandImage_IsNotRepeatedAsLargeContentLogo() => DoesNotContain(MainXaml(), "<Image Source=\"Assets/AppIcon.ico\"");
    [TestMethod] public void GlobalIndexBadge_IsRemoved() => DoesNotContain(MainXaml(), "Style=\"{StaticResource TopBarBadge}\"");
    [TestMethod] public void CancelTask_IsAvailableInsideWorkflowActions() => Contains(MainXaml(), "x:Name=\"CancelButton\" Content=\"取消当前任务\"", "Command=\"{Binding CancelCommand}\"");
    [TestMethod] public void EditionArea_IsCompactAndBeforeCollapseToggle() { var xaml = MainXaml(); Assert.IsLessThan(xaml.IndexOf("SidebarCollapseButton", StringComparison.Ordinal), xaml.IndexOf("EditionStatusArea", StringComparison.Ordinal)); Contains(xaml, "Content=\"升级\"", "CommandParameter=\"Activation\""); }
    [TestMethod] public void WorkbenchPrimaryAction_UsesDeepGradient() => Contains(MainXaml(), "x:Name=\"StartLocalSplitCard\"", "WorkbenchHeroBrush", "Content=\"?\"");
    [TestMethod] public void WorkbenchPrimaryAction_HasExplicitTextAndIconColumns() => Contains(MainXaml(), "<ColumnDefinition Width=\"48\" />", "开始本地分片", "IconLocalSplit");
    [TestMethod] public void RecentProjectTile_UsesCoverAndInformationPanel() => Contains(MainXaml(), "RecentProjectTile", "WorkbenchProjectCover.png", "<RowDefinition Height=\"190\" />", "继续处理");
    [TestMethod] public void Workbench_UsesBalancedModuleSpacing() => Contains(MainXaml(), "Margin=\"22,20,20,18\"", "<RowDefinition Height=\"16\" />", "Grid.Column=\"2\" Style=\"{StaticResource DashboardPanelCard}\"");
    [TestMethod] public void Version_Is203() => Contains(Text("src/RAWSelectionAssistant.Core/Models/Branding.cs"), "ProductVersion = \"2.0.3\"");
    [TestMethod] public void Installer_IsNamedFor203() => Contains(Text("installer/RAWSelectionAssistant.iss"), "MyAppVersion \"2.0.3\"", "像素蛋挞_Setup_2.0.3_x64");
    [TestMethod] public void Release_RemainsWinExeSelfContainedX64() => Contains(Text("src/RAWSelectionAssistant/RAWSelectionAssistant.csproj"), "<OutputType>WinExe</OutputType>", "<SelfContained>true</SelfContained>", "<RuntimeIdentifier>win-x64</RuntimeIdentifier>");
    [TestMethod] public void ReleaseLicense_RemainsProviderNone() => Contains(Text("src/RAWSelectionAssistant/appsettings.license.json"), "\"Provider\": \"None\"");
    [TestMethod] public void ReleaseStartup_StillForbidsMockProvider() => Contains(Text("src/RAWSelectionAssistant/App.xaml.cs"), "allowMockProvider: false");

    private static string MainXaml() => Text("src/RAWSelectionAssistant/MainWindow.xaml");
    private static void Contains(string text, params string[] values) { foreach (var value in values) StringAssert.Contains(text, value); }
    private static void DoesNotContain(string text, params string[] values) { foreach (var value in values) Assert.IsFalse(text.Contains(value, StringComparison.Ordinal)); }
    private static string Text(string path) => File.ReadAllText(Path.Combine(Root(), path.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent; return directory?.FullName ?? throw new DirectoryNotFoundException(); }
}
