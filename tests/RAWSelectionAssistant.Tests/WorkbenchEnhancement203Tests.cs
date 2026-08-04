using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class WorkbenchEnhancement203Tests
{
    [TestMethod] public void Hero_HasKeyboardAccessibleQuestionHelp() => Contains(MainXaml(), "Content=\"?\"", "AutomationProperties.Name=\"本地分片说明\"", "ToolTip=\"{Binding LocalSplitHelpText}\"");
    [TestMethod] public void Hero_DescriptionIsNotPersistent() { var line = MainXaml().Split('\n').First(value => value.Contains("StartLocalSplitCard")); Assert.IsFalse(line.Contains("导入 TXT、客户选图 JPG 或照片编号，匹配本地 JPG、RAW 及相关文件", StringComparison.Ordinal)); }
    [TestMethod] public void Hero_TooltipTextIsComplete() => Contains(ViewModel(), "导入 TXT、客户选图 JPG 或照片编号，匹配本地 JPG、RAW 及相关文件。");
    [TestMethod] public void DarkTheme_HasInputTokens() => Contains(Dark(), "#1F2227", "#3A3E45", "#F2F3F5", "#1A1D21", "#2B3138", "#26373A");
    [TestMethod] public void Inputs_HaveDarkComboTemplate() => Contains(Inputs(), "ControlTemplate TargetType=\"ComboBox\"", "PART_Popup", "DropdownBackgroundBrush", "InputBackgroundBrush");
    [TestMethod] public void ComboItems_AreThemed() => Contains(Inputs(), "TargetType=\"ComboBoxItem\"", "DropdownHoverBrush", "DropdownSelectedBrush");
    [TestMethod] public void TextPasswordListAndTooltip_AreThemed() => Contains(Inputs(), "TargetType=\"PasswordBox\"", "TargetType=\"ListBoxItem\"", "TargetType=\"ToolTip\"", "TooltipBackgroundBrush");
    [TestMethod] public void LightTheme_HasParallelInputTokens() => Contains(Light(), "InputBackgroundBrush", "DropdownBackgroundBrush", "TooltipBackgroundBrush");
    [TestMethod] public void QuickTools_DefaultsAreCorrect() => CollectionAssert.AreEqual(new[] { "Workflow", "PhotoOrganize", "BatchCompress" }, QuickToolsService.DefaultPinnedTools.ToArray());
    [TestMethod] public void QuickTools_MaximumIsThreeAndToolboxDoesNotConsumeSlot() { var values = QuickToolsService.Normalize(["Workflow", "PhotoOrganize", "BatchCompress", "Toolbox", "Collage"]); Assert.HasCount(QuickToolsService.MaximumPinnedTools, values); CollectionAssert.DoesNotContain(values, "Toolbox"); }
    [TestMethod] public void QuickTools_NormalizesDuplicatesAndUnknowns() => CollectionAssert.AreEqual(new[] { "Collage", "PhotoOrganize" }, QuickToolsService.Normalize(["Collage", "Collage", "bad", "PhotoOrganize"]));
    [TestMethod] public void QuickTools_AllowsEmptyExplicitSelection() => CollectionAssert.AreEqual(Array.Empty<string>(), QuickToolsService.Normalize(Array.Empty<string>()));
    [TestMethod] public async Task QuickTools_PersistAndReload() { using var temp = new TempDirectory(); var service = new SettingsService(new TestLogService(), temp.Combine("settings.json")); var settings = new AppSettings { PinnedQuickTools = ["Collage", "PhotoOrganize", "Toolbox"] }; await service.SaveAsync(settings); var loaded = await service.LoadAsync(); CollectionAssert.AreEqual(settings.PinnedQuickTools, loaded.PinnedQuickTools); }
    [TestMethod] public void QuickTools_ViewModelSupportsToggleAndCapacity() => Contains(ViewModel(), "TogglePinnedToolCommand", "快捷工具已满", "Settings.PinnedQuickTools", "SaveSettingsAsync");
    [TestMethod] public void QuickTools_HomeUpdatesFromPinnedCollection() => Contains(MainXaml(), "ItemsSource=\"{Binding DisplayedPinnedToolboxItems}\"", "WrapPanel ItemWidth=\"116\"");
    [TestMethod] public void Toolbox_RemainsFixedEntry() => Contains(ViewModel(), "工具箱始终可从工作台和侧栏打开", "ToolCatalogItems", "ToolId.Toolbox");
    [TestMethod] public void ToolboxPopup_HasGroupingAndCollage() => Contains(Popup(), "ItemsSource=\"{Binding ToolCatalogItems}\"", "ToolEntryButton", "TogglePinnedToolCommand", "ResourceKeyToGeometryConverter");
    [TestMethod] public void ToolboxFullPage_HasGroupingAndCollage() => Contains(MainXaml(), "ItemsSource=\"{Binding ToolCatalogItems}\"", "UniformGrid Columns=\"3\"", "ToolCatalogCard", "PinActionLabel");
    [TestMethod] public void GroupingPage_IsUsable() => Contains(Text("src/RAWSelectionAssistant/Views/OrganizePhotosView.xaml"), "整理图片", "来源和分组规则", "新建组", "生成并预览操作清单", "执行当前清单");
    [TestMethod] public void CollagePage_IsUsable() { Contains(Text("src/RAWSelectionAssistant/Views/CollageView.xaml"), "拼图", "模板和参数", "背景颜色", "导出重新读取原图"); Contains(Text("src/RAWSelectionAssistant/ViewModels/ToolPageViewModels.cs"), "纵向长图", "横向长图"); }
    [TestMethod] public void Navigation_AllowsNewToolPages() => Contains(ViewModel(), "\"PhotoGrouping\"", "\"Collage\"", "IsPhotoGroupingPage", "IsCollagePage");
    [TestMethod] public void ExistingSettingsAndToolboxFixesRemain() => Contains(MainXaml(), "SidebarSettingsButton", "OpenSettingsCommand", "ViewAllToolsButton", "OpenToolboxPage_Click");
    [TestMethod] public void SidebarCollapseFixRemains() => Contains(MainXaml(), "SidebarNavButton", "IconExpand", "SidebarCollapseButton");
    [TestMethod] public void ReleaseProviderAndWinExeRemain() { Contains(Text("src/RAWSelectionAssistant/appsettings.license.json"), "\"Provider\": \"None\""); Contains(Text("src/RAWSelectionAssistant/RAWSelectionAssistant.csproj"), "<OutputType>WinExe</OutputType>"); Contains(Text("src/RAWSelectionAssistant/App.xaml.cs"), "allowMockProvider: false"); }
    [TestMethod] public void VersionAndInstallerAre220() { Contains(Text("src/RAWSelectionAssistant.Core/Models/Branding.cs"), "2.2.0"); Contains(Text("installer/RAWSelectionAssistant.iss"), "MyAppVersion \"2.2.0\"", "像素蛋挞_Setup_2.2.0_x64"); }

    private static string Popup() { var text = MainXaml(); var start = text.IndexOf("WorkbenchToolboxPopup", StringComparison.Ordinal); var end = text.IndexOf("</Popup>", start, StringComparison.Ordinal); return text[start..end]; }
    private static string MainXaml() => Text("src/RAWSelectionAssistant/MainWindow.xaml");
    private static string ViewModel() => Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
    private static string Inputs() => Text("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Inputs.xaml");
    private static string Dark() => Text("src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Dark.xaml");
    private static string Light() => Text("src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Light.xaml");
    private static void Contains(string text, params string[] values) { foreach (var value in values) StringAssert.Contains(text, value); }
    private static string Text(string path) => File.ReadAllText(Path.Combine(Root(), path.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent; return directory?.FullName ?? throw new DirectoryNotFoundException(); }
}
