using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class FeedbackSidebar143Tests
{
    [TestMethod] public void HelpMenu_ContainsFeedbackEntry() { Contains(MainXaml(), "Header=\"建议与问题反馈(_F)\"", "InputGestureText=\"Ctrl+Shift+F\""); Contains(Text("src/RAWSelectionAssistant/MainWindow.xaml.cs"), "ModifierKeys.Control | ModifierKeys.Shift", "FeedbackCommand.Execute(null)"); }
    [TestMethod] public void FeedbackCommand_OpensInternalDialog() { Contains(Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs"), "FeedbackCommand", "_dialogService.ShowFeedback()"); Contains(Text("src/RAWSelectionAssistant/Services/WpfDialogService.cs"), "new FeedbackDialog", "ShowDialog()"); }
    [TestMethod] public void FeedbackDialog_ShowsSupportEmail() { Contains(Text("src/RAWSelectionAssistant/Views/FeedbackDialog.xaml"), "作者邮箱", "EmailTextBox"); Assert.AreEqual("3183483929@qq.com", new FeedbackRequestBuilder().Build("Windows test").EmailAddress); }
    [TestMethod] public void CopyEmail_WritesExactAddress() { var clipboard = new FakeClipboard(); new FeedbackService(Request(), clipboard, new FakeLauncher(), new TestLogService()).CopyEmail(); Assert.AreEqual("3183483929@qq.com", clipboard.Text); }
    [TestMethod] public void Mailto_UsesCorrectRecipient() => StringAssert.StartsWith(Request().MailtoUri, "mailto:3183483929@qq.com?");
    [TestMethod] public void Mailto_UsesCorrectSubject() => Assert.AreEqual("像素蛋挞建议与问题反馈", Request().Subject);
    [TestMethod] public void MailBody_ContainsSoftwareVersion() => StringAssert.Contains(Request().Body, $"软件版本：{Branding.ProductVersion}");
    [TestMethod] public void MailBody_ContainsWindowsVersion() => StringAssert.Contains(Request().Body, "Windows版本：Windows 11 测试版");
    [TestMethod] public void MailBody_DoesNotContainPhotoPath() => DoesNotContain(Request().Body, "C:\\照片", "D:\\项目", "照片完整路径");
    [TestMethod] public void MailBody_DoesNotContainActivationSecrets() => DoesNotContain(Request().Body, "激活码", "设备指纹", "授权Token", "日志内容");
    [TestMethod] public void MissingMailClient_DoesNotThrow() => new FeedbackService(Request(), new FakeClipboard(), new FakeLauncher(true), new TestLogService()).ComposeEmail();
    [TestMethod] public void MissingMailClient_CopiesEmail() { var clipboard = new FakeClipboard(); var result = new FeedbackService(Request(), clipboard, new FakeLauncher(true), new TestLogService()).ComposeEmail(); Assert.AreEqual(Branding.SupportEmail, clipboard.Text); Assert.IsTrue(result.EmailCopied); }
    [TestMethod] public void SidebarExpanded_ShowsNavigationContainer() => Contains(MainXaml(), "x:Name=\"SidebarContainer\"", "Width=\"{Binding SidebarWidth}");
    [TestMethod] public void SidebarCollapsed_KeepsAllNavigationIcons() { Contains(MainXaml(), "SidebarContainer", "SidebarNavButton", "Tag=\"{StaticResource Icon"); Contains(Text("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Navigation.xaml"), "x:Name=\"CollapsedContent\""); }
    [TestMethod] public void SidebarCollapsed_HidesAllSectionLabels() => Contains(MainXaml(), "Visibility=\"{Binding IsSidebarExpanded", "Text=\"工作\" Style=\"{StaticResource SidebarSectionLabel}\"", "Text=\"工具\" Style=\"{StaticResource SidebarSectionLabel}\"", "Text=\"系统\" Style=\"{StaticResource SidebarSectionLabel}\"");
    [TestMethod] public void SidebarCollapsed_UsesCompactWidth() { Contains(Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs"), "SidebarLayoutMetrics.CollapsedWidth"); Contains(Text("src/RAWSelectionAssistant.Core/Models/SidebarLayoutMetrics.cs"), "CollapsedWidth = 60d"); }
    [TestMethod] public void MainContent_UsesReleasedSidebarSpace() => Contains(MainXaml(), "<ColumnDefinition Width=\"Auto\" />", "<ColumnDefinition Width=\"*\" />", "Grid.Column=\"1\"");
    [TestMethod] public void CollapsedState_KeepsToggleInsideSidebar() => Contains(MainXaml(), "x:Name=\"SidebarCollapseButton\"", "ToggleSidebarCommand");
    [TestMethod] public void CollapseButton_HasCompleteMetrics() => Contains(MainXaml(), "x:Name=\"SidebarCollapseButton\"", "BasedOn=\"{StaticResource SidebarNavButton}\"", "MinWidth=\"0\"", "AutomationProperties.Name\" Value=\"展开侧栏\"");
    [TestMethod] public void CollapseButton_IsAboveIndependentStatusBar() { var xaml = MainXaml(); Assert.IsLessThan(xaml.IndexOf("Grid.Row=\"2\" Height=\"34\"", StringComparison.Ordinal), xaml.IndexOf("SidebarCollapseButton", StringComparison.Ordinal)); }
    [TestMethod] public void CtrlB_TogglesSidebar() { Contains(MainXaml(), "Header=\"显示侧边栏(_B)\"", "Key=\"B\" Modifiers=\"Control\" Command=\"{Binding ToggleSidebarCommand}\"", "InputGestureText=\"Ctrl+B\""); Contains(Text("src/RAWSelectionAssistant/MainWindow.xaml.cs"), "e.Key == Key.B", "ToggleSidebarCommand.Execute(null)"); }
    [TestMethod] public async Task SidebarState_CanBeSaved() { using var temp = new TempDirectory(); var service = new SettingsService(new TestLogService(), temp.Combine("settings.json")); await service.SaveAsync(new AppSettings { Appearance = new AppearanceSettings { SidebarCollapsed = true } }); Assert.IsTrue((await service.LoadAsync()).Appearance.SidebarCollapsed); }
    [TestMethod] public async Task SidebarState_RestoresAfterRestart() { using var temp = new TempDirectory(); var path = temp.Combine("settings.json"); await new SettingsService(new TestLogService(), path).SaveAsync(new AppSettings { Appearance = new AppearanceSettings { SidebarCollapsed = true } }); var restarted = new SettingsService(new TestLogService(), path); Assert.IsTrue((await restarted.LoadAsync()).Appearance.SidebarCollapsed); }
    [TestMethod] public async Task ThemeChange_PreservesSidebarState() { using var temp = new TempDirectory(); var service = new SettingsService(new TestLogService(), temp.Combine("settings.json")); await service.SaveAsync(new AppSettings { Appearance = new AppearanceSettings { Theme = ThemeMode.Dark, SidebarCollapsed = true } }); var settings = await service.LoadAsync(); Assert.AreEqual(ThemeMode.Dark, settings.Appearance.Theme); Assert.IsTrue(settings.Appearance.SidebarCollapsed); }
    [TestMethod] public void Dpi125_UsesDeviceIndependentUnclippedButton() => AssertDpiSafe(1.25);
    [TestMethod] public void Dpi150_UsesDeviceIndependentUnclippedButton() => AssertDpiSafe(1.5);
    [TestMethod] public void DarkTheme_UsesDynamicDialogAndSidebarBrushes() { Contains(Text("src/RAWSelectionAssistant/Views/FeedbackDialog.xaml"), "DynamicResource WindowBackgroundBrush", "DynamicResource TextSecondaryBrush"); Contains(MainXaml(), "DynamicResource SidebarBackgroundBrush"); }
    [TestMethod] public void LightTheme_UsesSemanticBrushes() { Contains(Text("src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Light.xaml"), "SidebarBackgroundBrush"); Contains(Text("src/RAWSelectionAssistant/Views/FeedbackDialog.xaml"), "DynamicResource AccentBrush"); }
    [TestMethod] public void ReducedMotion_DoesNotRunWidthAnimation() => DoesNotContain(MainXaml(), "DoubleAnimation", "ScaleTransform", "OpacityAnimation");
    [TestMethod] public void WinExe_RemainsEnabled() => Contains(Text("src/RAWSelectionAssistant/RAWSelectionAssistant.csproj"), "<OutputType>WinExe</OutputType>");
    [TestMethod] public void ReleaseProvider_RemainsNone() => Contains(Text("src/RAWSelectionAssistant/appsettings.license.json"), "\"Provider\": \"None\"");
    [TestMethod] public void Release_DoesNotEnableMockProvider() => Contains(Text("src/RAWSelectionAssistant/App.xaml.cs"), "allowMockProvider: false");
    [TestMethod] public void Version_Is230() => Contains(Text("src/RAWSelectionAssistant.Core/Models/Branding.cs"), "ProductVersion = \"2.3.0\"");

    private static FeedbackRequest Request() => new FeedbackRequestBuilder().Build("Windows 11 测试版");
    private static string MainXaml() => Text("src/RAWSelectionAssistant/MainWindow.xaml");
    private static void AssertDpiSafe(double scale) { Contains(MainXaml(), "UseLayoutRounding=\"True\"", "SnapsToDevicePixels=\"True\""); Contains(Text("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Navigation.xaml"), "SidebarButtonHeight", "Stretch=\"Uniform\""); Assert.IsGreaterThanOrEqualTo(40 * scale, 40 * scale); }
    private static void Contains(string text, params string[] values) { foreach (var value in values) StringAssert.Contains(text, value); }
    private static void DoesNotContain(string text, params string[] values) { foreach (var value in values) Assert.IsFalse(text.Contains(value, StringComparison.Ordinal), $"不应包含：{value}"); }
    private static string Text(string relativePath) => File.ReadAllText(Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent; return directory?.FullName ?? throw new DirectoryNotFoundException(); }

    private sealed class FakeClipboard(bool throwOnSet = false) : IFeedbackClipboard
    {
        public string Text { get; private set; } = string.Empty;
        public void SetText(string text) { if (throwOnSet) throw new InvalidOperationException("clipboard unavailable"); Text = text; }
    }

    private sealed class FakeLauncher(bool throwOnOpen = false) : IFeedbackMailLauncher
    {
        public string MailtoUri { get; private set; } = string.Empty;
        public void Open(string mailtoUri) { if (throwOnOpen) throw new InvalidOperationException("mail unavailable"); MailtoUri = mailtoUri; }
    }
}
