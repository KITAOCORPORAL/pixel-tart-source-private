using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class UiDesignSystem140Tests
{
    [TestMethod] public void Appearance_DefaultThemeUsesProfessionalDarkMode() => Assert.AreEqual(ThemeMode.Dark, new AppearanceSettings().Theme);
    [TestMethod] public void Appearance_DefaultAccentUsesKitaoBlue() => Assert.AreEqual(AccentPreset.KitaoBlue, new AppearanceSettings().Accent);
    [TestMethod] public void Appearance_DefaultDensityIsComfortable() => Assert.AreEqual(InterfaceDensity.Comfortable, new AppearanceSettings().Density);
    [TestMethod] public void Appearance_DefaultSidebarRemembersState() => Assert.AreEqual(SidebarMode.Remember, new AppearanceSettings().Sidebar);
    [TestMethod] public void Appearance_DefaultMotionIsNormal() => Assert.AreEqual(MotionPreference.Normal, new AppearanceSettings().Motion);
    [TestMethod] public void Appearance_DefaultFontScaleIsStandard() => Assert.AreEqual(FontScale.Standard, new AppearanceSettings().FontScale);
    [TestMethod] public void Appearance_DefaultCustomAccentIsValidHex() => StringAssert.Matches(new AppearanceSettings().CustomAccentColor, new System.Text.RegularExpressions.Regex("^#[0-9A-F]{6}$"));

    [TestMethod]
    public async Task Settings_MissingAppearanceMigratesToDefaults()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("settings.json");
        await File.WriteAllTextAsync(path, "{}");
        var settings = await new SettingsService(new TestLogService(), path).LoadAsync();
        Assert.IsNotNull(settings.Appearance);
        Assert.AreEqual(ThemeMode.Dark, settings.Appearance.Theme);
    }

    [TestMethod]
    public async Task Settings_AppearanceRoundTrips()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("settings.json");
        var service = new SettingsService(new TestLogService(), path);
        var settings = new AppSettings { Appearance = new AppearanceSettings { Theme = ThemeMode.Dark, Accent = AccentPreset.WineRed, Density = InterfaceDensity.Compact, SidebarCollapsed = true } };
        await service.SaveAsync(settings);
        var loaded = await service.LoadAsync();
        Assert.AreEqual(ThemeMode.Dark, loaded.Appearance.Theme);
        Assert.AreEqual(AccentPreset.WineRed, loaded.Appearance.Accent);
        Assert.AreEqual(InterfaceDensity.Compact, loaded.Appearance.Density);
        Assert.IsTrue(loaded.Appearance.SidebarCollapsed);
    }

    [TestMethod]
    public async Task Settings_InvalidCustomAccentReturnsSafeDefault()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("settings.json");
        await File.WriteAllTextAsync(path, "{\"Appearance\":{\"CustomAccentColor\":\"invalid\"}}");
        var settings = await new SettingsService(new TestLogService(), path).LoadAsync();
        Assert.AreEqual("#C98220", settings.Appearance.CustomAccentColor);
    }

    [TestMethod]
    public async Task Settings_UnknownAppearanceEnumsReturnDefaults()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("settings.json");
        await File.WriteAllTextAsync(path, "{\"Appearance\":{\"Theme\":999,\"Accent\":999,\"Density\":999,\"Sidebar\":999,\"Motion\":999,\"FontScale\":999}}");
        var settings = await new SettingsService(new TestLogService(), path).LoadAsync();
        Assert.AreEqual(ThemeMode.System, settings.Appearance.Theme);
        Assert.AreEqual(AccentPreset.KitaoBlue, settings.Appearance.Accent);
        Assert.AreEqual(InterfaceDensity.Comfortable, settings.Appearance.Density);
        Assert.AreEqual(SidebarMode.Remember, settings.Appearance.Sidebar);
    }

    [TestMethod] public void App_MergesDesignSystemResources() => Contains("src/RAWSelectionAssistant/App.xaml", "Resources/DesignSystem/DesignTokens.xaml", "Theme.Dark.xaml", "Controls.Status.xaml");
    [TestMethod] public void LightTheme_DefinesSemanticSurfaces() => Contains(Ds("Theme.Light.xaml"), "WindowBackgroundBrush", "SurfacePrimaryBrush", "TextPrimaryBrush", "SuccessSoftBrush");
    [TestMethod] public void DarkTheme_DefinesSemanticSurfaces() => Contains(Ds("Theme.Dark.xaml"), "WindowBackgroundBrush", "SurfacePrimaryBrush", "TextPrimaryBrush", "WarningSoftBrush");
    [TestMethod] public void HighContrastTheme_UsesSystemColors() => Contains(Ds("Theme.HighContrast.xaml"), "SystemColors.WindowColor", "SystemColors.WindowTextColor", "SystemColors.HighlightColor");
    [TestMethod] public void AccentPalette_DefinesInteractionStates() => Contains(Ds("AccentColors.xaml"), "AccentBrush", "AccentHoverBrush", "AccentPressedBrush", "AccentForegroundBrush");
    [TestMethod] public void Tokens_DefineDensityAndScale() => Contains(Ds("DesignTokens.xaml"), "ControlHeight", "RowHeight", "SidebarWidth", "BodyFontSize", "ControlPadding");
    [TestMethod] public void Typography_UsesDynamicThemeBrushes() => Contains(Ds("Typography.xaml"), "DynamicResource WindowBackgroundBrush", "DynamicResource TextPrimaryBrush", "PageTitleText");
    [TestMethod] public void Buttons_HaveFocusHoverPressedDisabledStates() => Contains(Ds("Controls.Buttons.xaml"), "IsKeyboardFocused", "IsMouseOver", "IsPressed", "IsEnabled");
    [TestMethod] public void Inputs_HaveKeyboardFocusState() => Contains(Ds("Controls.Inputs.xaml"), "IsKeyboardFocused", "AccentBrush", "TextBox", "ComboBox");
    [TestMethod] public void Cards_ExposeSemanticVariants() => Contains(Ds("Controls.Cards.xaml"), "InfoCard", "WarningCard", "DangerCard", "SuccessCard");
    [TestMethod] public void Tables_UseThemeAwareRows() => Contains(Ds("Controls.Tables.xaml"), "DynamicResource SurfacePrimaryBrush", "DynamicResource AccentSoftBrush", "DynamicResource RowHeight");
    [TestMethod] public void Navigation_UsesLinearLayoutStyles() => Contains(Ds("Controls.Navigation.xaml"), "NavButton", "WorkflowStepButton", "SidebarSectionLabel");
    [TestMethod] public void Menus_UseThemeAwareResources() => Contains(Ds("Controls.Menu.xaml"), "DynamicResource SurfacePrimaryBrush", "DynamicResource TextPrimaryBrush", "MenuItem");
    [TestMethod] public void DialogResources_DefineTitleAndFooter() => Contains(Ds("Controls.Dialogs.xaml"), "DialogFooter", "DialogTitle");
    [TestMethod] public void StatusResources_DefineBadgeToastProgress() => Contains(Ds("Controls.Status.xaml"), "StatusBadge", "ToastCard", "ProgressBar");

    [TestMethod]
    public void MainWindow_TopMenusFollowRequiredOrder()
    {
        var text = Text("src/RAWSelectionAssistant/MainWindow.xaml");
        var headers = new[] { "文件(_F)", "项目(_P)", "编辑(_E)", "视图(_V)", "工具(_T)", "帮助(_H)" };
        var previous = -1;
        foreach (var header in headers)
        {
            var current = text.IndexOf(header, StringComparison.Ordinal);
            Assert.IsGreaterThan(previous, current, $"菜单 {header} 顺序不正确");
            previous = current;
        }
    }

    [TestMethod] public void MainWindow_GlobalAppBarIsRemovedAndEditionLivesInSidebar() { DoesNotContain("src/RAWSelectionAssistant/MainWindow.xaml", "TopBarBadge", "TopBarTaskButton"); Contains("src/RAWSelectionAssistant/MainWindow.xaml", "EditionStatusArea", "SidebarEditionCard", "IndexedMediaCount"); }
    [TestMethod] public void MainWindow_SidebarIsResizableAndCollapsible() => Contains("src/RAWSelectionAssistant/MainWindow.xaml", "SidebarWidth", "ToggleSidebarCommand", "IsSidebarExpanded");
    [TestMethod] public void MainWindow_HasFourStepWorkflow() => Contains("src/RAWSelectionAssistant/MainWindow.xaml", "1  来源与索引", "2  导入选片", "3  匹配检查", "4  输出交付");
    [TestMethod] public void MainWindow_HasSearchAndAttentionFilter() => Contains("src/RAWSelectionAssistant/MainWindow.xaml", "SearchQuery", "OnlyShowAttentionItems", "Ctrl+F");

    [TestMethod]
    public void MainWindow_DetailsActionAppearsBeforeWideMetadataColumns()
    {
        var text = Text("src/RAWSelectionAssistant/MainWindow.xaml");
        Assert.IsLessThan(text.IndexOf("Header=\"标准化名称\"", StringComparison.Ordinal), text.IndexOf("x:Name=\"DetailsColumn\"", StringComparison.Ordinal));
    }

    [TestMethod] public void MainWindow_AppearancePageExposesAllOptions() => Contains("src/RAWSelectionAssistant/MainWindow.xaml", "ThemeOptions", "AccentOptions", "DensityOptions", "SidebarOptions", "MotionOptions", "FontScaleOptions");
    [TestMethod] public void MainWindow_AppearanceHasRealtimePreviewAndReset() => Contains("src/RAWSelectionAssistant/MainWindow.xaml", "实时预览", "AccentPreviewHex", "ResetAppearanceCommand");
    [TestMethod] public void MainWindow_ToastIsNonModalAndDismissible() => Contains("src/RAWSelectionAssistant/MainWindow.xaml", "IsToastVisible", "ToastMessage", "DismissToastCommand");
    [TestMethod] public void MainWindow_TutorialTargetsRemainAvailable() => Contains("src/RAWSelectionAssistant/MainWindow.xaml", "AddSourceButton", "CustomerDropArea", "MatchButton", "ResultsGrid", "BrowseOutputButton", "TutorialPrimaryButton");
    [TestMethod] public void MainWindow_UsesDynamicSemanticColors() => Contains("src/RAWSelectionAssistant/MainWindow.xaml", "DynamicResource WindowBackgroundBrush", "DynamicResource SurfacePrimaryBrush", "DynamicResource AccentBrush", "DynamicResource TextSecondaryBrush");
    [TestMethod] public void MainWindow_DoesNotUseLegacyDarkGreenSidebar() => DoesNotContain("src/RAWSelectionAssistant/MainWindow.xaml", "#173D33");
    [TestMethod] public void DetailsDialog_UsesDynamicTheme() => Contains("src/RAWSelectionAssistant/Views/MediaDetailsWindow.xaml", "DynamicResource WindowBackgroundBrush", "DynamicResource TextSecondaryBrush", "IsCancel=\"True\"");
    [TestMethod] public void CandidateDialog_UsesDynamicThemeAndDefaultAction() => Contains("src/RAWSelectionAssistant/Views/CandidateSelectionWindow.xaml", "DynamicResource WindowBackgroundBrush", "IsDefault=\"True\"", "IsCancel=\"True\"");
    [TestMethod] public void HelpDialog_ShowsVersion2031() => Contains("src/RAWSelectionAssistant/Views/HelpWindow.xaml", "版本 2.0.3.1", "DynamicResource WindowBackgroundBrush");
    [TestMethod] public void TutorialOfferDialog_UsesDynamicTheme() => Contains("src/RAWSelectionAssistant/Views/UpgradeTutorialWindow.xaml", "DynamicResource WindowBackgroundBrush", "DynamicResource TextSecondaryBrush");
    [TestMethod] public void AppearanceService_RespondsToWindowsAndHighContrast() => Contains("src/RAWSelectionAssistant/Services/AppearanceService.cs", "AppsUseLightTheme", "SystemParameters.HighContrast", "UserPreferenceChanged");
    [TestMethod] public void AppearanceService_DoesNotResetMainViewModel() => DoesNotContain("src/RAWSelectionAssistant/Services/AppearanceService.cs", "MainViewModel");
    [TestMethod] public void AccentService_UsesContrastNotFixedForeground() => Contains("src/RAWSelectionAssistant/Services/AppearanceService.cs", "GetReadableForeground", "RelativeLuminance", "ContrastRatio");
    [TestMethod] public void Version_BrandingSourceIs2031() => Contains("src/RAWSelectionAssistant.Core/Models/Branding.cs", "ProductVersion = \"2.0.3.1\"");
    [TestMethod] public void Version_InstallerOutputs2031Package() => Contains("installer/RAWSelectionAssistant.iss", "MyAppVersion \"2.0.3.1\"", "像素蛋挞_Setup_2.0.3.1_x64");
    [TestMethod] public void Packaging_RemainsWinExeSelfContainedX64() => Contains("src/RAWSelectionAssistant/RAWSelectionAssistant.csproj", "<OutputType>WinExe</OutputType>", "<SelfContained>true</SelfContained>", "<RuntimeIdentifier>win-x64</RuntimeIdentifier>");
    [TestMethod] public void LicenseConfig_RemainsProviderNone() => Contains("src/RAWSelectionAssistant/appsettings.license.json", "\"Provider\": \"None\"");
    [TestMethod] public void ReleaseStartup_ForbidsMockProvider() => Contains("src/RAWSelectionAssistant/App.xaml.cs", "allowMockProvider: false");
    [TestMethod] public void LicenseFactoryStillExistsAndWasNotReplacedByUiWork() => Contains("src/RAWSelectionAssistant/App.xaml.cs", "LicenseProviderFactory.Create", "FeatureGateService", "ProjectEntitlementService");
    [TestMethod] public void ExistingJpegSafetyCopyAndReportServicesRemainInjected() => Contains("src/RAWSelectionAssistant/App.xaml.cs", "JpegQualityAssessmentService", "MediaCopyService", "MediaReportService");
    [TestMethod] public void MainWindow_NoDebugUiPreviewEntryShipsInXaml() => DoesNotContain("src/RAWSelectionAssistant/MainWindow.xaml", "UI Preview", "界面预览入口");

    private static string Ds(string file) => $"src/RAWSelectionAssistant/Resources/DesignSystem/{file}";

    private static void Contains(string relativePath, params string[] values)
    {
        var text = Text(relativePath);
        foreach (var value in values) StringAssert.Contains(text, value, relativePath);
    }

    private static void DoesNotContain(string relativePath, params string[] values)
    {
        var text = Text(relativePath);
        foreach (var value in values) Assert.IsFalse(text.Contains(value, StringComparison.Ordinal), $"{relativePath} 不应包含 {value}");
    }

    private static string Text(string relativePath) => File.ReadAllText(Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("未找到解决方案目录。");
    }
}
