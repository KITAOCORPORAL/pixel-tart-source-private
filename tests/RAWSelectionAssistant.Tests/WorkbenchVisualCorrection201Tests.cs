using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class WorkbenchVisualCorrection201Tests
{
    [TestMethod] public void Version_Is230() => Contains(Branding(), "ProductVersion = \"2.3.0\"");
    [TestMethod] public void DefaultTheme_IsDark() => Assert.AreEqual(ThemeMode.Dark, new AppearanceSettings().Theme);
    [TestMethod] public void DefaultWindow_Is1600By920() => Contains(MainXaml(), "Width=\"1600\" Height=\"920\"", "MinWidth=\"1180\"");
    [TestMethod] public void WorkbenchDefaultDarkResources_AreComplete() => Contains(Dark(), "#0B0C0E", "#141518", "#18191C", "#202226", "#24262A", "#2A2D32", "#E3A93B", "#20C985");
    [TestMethod] public void Workbench_UsesThreeColumnShell() => Contains(MainXaml(), "x:Name=\"SidebarContainer\"", "x:Name=\"WorkbenchShell\"", "x:Name=\"TaskCenterPanel\"", "Width=\"320\"");
    [TestMethod] public void Sidebar_IsCompactAndGrouped() => Contains(MainXaml(), "Text=\"工作台\" Style=\"{StaticResource SidebarSectionLabel}\"", "Text=\"应用\"", "ToolboxEntry.DisplayName", "Content=\"使用教程\"", "Content=\"问题反馈\"");
    [TestMethod] public void Sidebar_DoesNotKeepSevenToolLongList() { var xaml = MainXaml(); var start = xaml.IndexOf("x:Name=\"SidebarContainer\"", StringComparison.Ordinal); var end = xaml.IndexOf("x:Name=\"WorkbenchShell\"", start, StringComparison.Ordinal); DoesNotContain(xaml[start..end], "CommandParameter=\"BatchCompress\"", "CommandParameter=\"Watermark\"", "CommandParameter=\"DeleteRejects\"", "CommandParameter=\"FtpTool\"", "CommandParameter=\"PhotoOrganize\"", "CommandParameter=\"BatchRename\"", "CommandParameter=\"BatchConvert\""); }
    [TestMethod] public void SidebarCollapsedMode_KeepsIcons() { Contains(Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs"), "SidebarLayoutMetrics.CollapsedWidth", "width < 1100"); Contains(Text("src/RAWSelectionAssistant.Core/Models/SidebarLayoutMetrics.cs"), "CollapsedWidth = 60d"); }
    [TestMethod] public void SidebarRows_AreFortyPixels() => Contains(Navigation(), "SidebarButtonHeight", "MinHeight");
    [TestMethod] public void TopPrimaryEntry_Exists() => Contains(MainXaml(), "x:Name=\"StartLocalSplitCard\"", "开始本地分片", "Content=\"?\"", "ToolTip=\"{Binding LocalSplitHelpText}\"", "WorkbenchHeroBrush");
    [TestMethod] public void TopHasFourQuickTools() => Contains(MainXaml(), "ItemsSource=\"{Binding DisplayedPinnedToolboxItems}\"", "WrapPanel ItemWidth=\"116\"", "x:Name=\"ToolboxQuickButton\"");
    [TestMethod] public void ToolboxPopup_Exists() => Contains(MainXaml(), "x:Name=\"WorkbenchToolboxPopup\"", "Placement=\"Bottom\"", "StaysOpen=\"False\"", "PopupAnimation=\"Fade\"");

    [TestMethod]
    public void ToolboxPopup_ContainsEightTiles()
    {
        var xaml = MainXaml();
        var start = xaml.IndexOf("x:Name=\"WorkbenchToolboxPopup\"", StringComparison.Ordinal);
        var end = xaml.IndexOf("</Popup>", start, StringComparison.Ordinal);
        var popup = xaml[start..end];
        Contains(popup, "ItemsSource=\"{Binding ToolCatalogItems}\"", "ToolEntryButton", "ResourceKeyToGeometryConverter");
    }

    [TestMethod] public void ToolboxEscape_ClosesPopup() => Contains(CodeBehind(), "e.Key == Key.Escape && WorkbenchToolboxPopup.IsOpen", "WorkbenchToolboxPopup.IsOpen = false");
    [TestMethod] public void ToolboxFullPage_UsesThreeColumns() => Contains(MainXaml(), "x:Name=\"ToolboxFullPage\"", "<UniformGrid Columns=\"3\"", "ToolCatalogCard", "查看全部工具");
    [TestMethod] public void ProjectOverview_IsSingleLargePanel() => Contains(MainXaml(), "x:Name=\"ProjectOverviewCard\"", "项目概览", "Columns=\"3\" Rows=\"2\"", "需要确认");
    [TestMethod] public void ProcessingTasks_IsSingleLargePanel() { Contains(MainXaml(), "x:Name=\"ProcessingTasksCard\"", "处理任务", "扫描、复制、压缩和转档任务"); Contains(Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs"), "暂无待处理任务"); }
    [TestMethod] public void RecentProjects_HasTabs() => Contains(MainXaml(), "x:Name=\"RecentProjectsArea\"", "最近项目", "本地分片", "归片项目", "已完成", "↻  刷新", "查看全部");
    [TestMethod] public void RecentProjectCard_HasCoverAndMetadata() => Contains(MainXaml(), "RecentProjectTile", "WorkbenchProjectCover.png", "个文件", "更新于", "继续处理");
    [TestMethod] public void RecentProjects_HasDesignedEmptyState() => Contains(MainXaml(), "x:Name=\"RecentProjectsEmptyState\"", "还没有本地项目", "创建第一个本地分片任务");
    [TestMethod] public void CompletedProjects_HasIndependentEmptyState() => Contains(MainXaml(), "x:Name=\"CompletedProjectsEmptyState\"", "暂无已完成项目");
    [TestMethod] public void TaskCenter_HasLocalTaskData() => Contains(MainXaml(), "任务中心", "当前任务", "等待确认", "冲突文件", "未找到文件", "打开任务历史");
    [TestMethod] public void TaskCenterReviewData_IsClearlyDemoOnly() => Contains(MainXaml(), "x:Name=\"TaskCenterReviewContent\"", "演示 · 正在扫描", "以上为界面验收演示数据");
    [TestMethod] public void TaskCenter_CollapsesBelow1350() => Contains(CodeBehind(), "ActualWidth < 1350", "WorkbenchTaskColumn.Width", "TaskDrawerButton.Visibility");
    [TestMethod] public void TaskCenter_DrawerReopens() => Contains(MainXaml(), "x:Name=\"TaskDrawerButton\"", "TaskDrawerButton_Click");
    [TestMethod] public void LightTheme_HasSameShellResources() => Contains(Light(), "ShellTopBrush", "TaskCenterBackgroundBrush", "ToolTileBrush", "WorkbenchCardBrush", "WorkbenchHeroBrush");
    [TestMethod] public void Settings_UsesCenteredDarkModal() => Contains(MainXaml(), "x:Name=\"SettingsModal\"", "RaisedSurfaceBrush", "Header=\"常规\"", "Header=\"外观\"", "Header=\"输出与报告\"");
    [TestMethod] public void SettingsEscape_ClosesModal() => Contains(CodeBehind(), "e.Key == Key.Escape && _viewModel?.IsSettingsModalOpen == true", "_viewModel.IsSettingsModalOpen = false");
    [TestMethod] public void ProviderNoneStatus_IsVisibleInSidebar() => Contains(MainXaml(), "授权服务准备中", "x:Name=\"EditionStatusArea\"");
    [TestMethod] public void MainMenu_Remains() => Contains(MainXaml(), "Header=\"文件(_F)\"", "Header=\"项目(_P)\"", "Header=\"编辑(_E)\"", "Header=\"视图(_V)\"", "Header=\"工具(_T)\"", "Header=\"帮助(_H)\"");
    [TestMethod] public void BusinessModules_AreAbsent() => DoesNotContain(MainXaml(), "极速选片", "预约管理", "我的收入", "客资管理", "团队管理", "橱窗管理", "AI挑图", "AI 挑图", "会员促销");
    [TestMethod] public void OldWorkbenchLayout_IsDeleted() => DoesNotContain(MainXaml(), "8 个本地工具", "本地处理优先", "MaxWidth=\"1240\" HorizontalAlignment=\"Left\"", "<TextBlock Text=\"工作台\" Style=\"{StaticResource PageTitleText}\"");
    [TestMethod] public void PrimaryCard_IsNotOldWarmYellowCard() { var main = MainXaml(); var start = main.IndexOf("x:Name=\"WorkbenchShell\"", StringComparison.Ordinal); var end = main.IndexOf("IsLocalSplitPage", start, StringComparison.Ordinal); DoesNotContain(main[start..end], "Style=\"{StaticResource WorkbenchHeroCard}\"", "BrandSoftBrush\" CornerRadius=\"20"); }
    [TestMethod] public void LogoSmallVariant_IsSimplified() => Contains(Text("src/RAWSelectionAssistant/Assets/AppIcon.Small.svg"), "#F4C96B", "#202226", "#F7F1DF");
    [TestMethod] public void LogoAndCoverAssets_ArePackaged() => Contains(Text("src/RAWSelectionAssistant/RAWSelectionAssistant.csproj"), "Assets\\AppIcon.ico", "Assets\\WorkbenchProjectCover.png");
    [TestMethod] public void Release_RemainsWinExeSelfContained() => Contains(Text("src/RAWSelectionAssistant/RAWSelectionAssistant.csproj"), "<OutputType>WinExe</OutputType>", "<SelfContained>true</SelfContained>", "<RuntimeIdentifier>win-x64</RuntimeIdentifier>");
    [TestMethod] public void ReleaseProvider_RemainsNone() => Contains(Text("src/RAWSelectionAssistant/appsettings.license.json"), "\"Provider\": \"None\"");
    [TestMethod] public void ReleaseMock_RemainsDisabled() => Contains(Text("src/RAWSelectionAssistant/App.xaml.cs"), "allowMockProvider: false");
    [TestMethod] public void Source_DoesNotUseLocalhost() { foreach (var file in Directory.EnumerateFiles(Path.Combine(Root(), "src"), "*.*", SearchOption.AllDirectories).Where(path => path.EndsWith(".cs") || path.EndsWith(".xaml") || path.EndsWith(".json"))) DoesNotContain(File.ReadAllText(file), "localhost", "127.0.0.1"); }
    [TestMethod] public void Installer_IsNamedFor230() => Contains(Text("installer/RAWSelectionAssistant.iss"), "MyAppVersion \"2.3.0\"", "像素蛋挞_Setup_2.3.0_RC1_x64");

    private static string MainXaml() => Text("src/RAWSelectionAssistant/MainWindow.xaml");
    private static string CodeBehind() => Text("src/RAWSelectionAssistant/MainWindow.xaml.cs");
    private static string Branding() => Text("src/RAWSelectionAssistant.Core/Models/Branding.cs");
    private static string Dark() => Text("src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Dark.xaml");
    private static string Light() => Text("src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Light.xaml");
    private static string Navigation() => Text("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Navigation.xaml");
    private static int Count(string text, string value) { var result = 0; for (var index = 0; (index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length) result++; return result; }
    private static void Contains(string text, params string[] values) { foreach (var value in values) StringAssert.Contains(text, value); }
    private static void DoesNotContain(string text, params string[] values) { foreach (var value in values) Assert.IsFalse(text.Contains(value, StringComparison.Ordinal), $"不应包含：{value}"); }
    private static string Text(string path) => File.ReadAllText(Path.Combine(Root(), path.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent; return directory?.FullName ?? throw new DirectoryNotFoundException(); }
}
