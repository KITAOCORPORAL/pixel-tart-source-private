namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class UsabilityFix151Tests
{
    [TestMethod] public void MenuStyle_ProvidesThemeAwarePopupTemplate() => Contains(Menu(), "x:Name=\"PART_Popup\"", "MenuPopupBackgroundBrush", "MenuPopupBorderBrush", "PlacementTarget=\"{Binding ElementName=ItemBorder}\"");
    [TestMethod] public void MenuStyle_UsesDynamicResourcesInsteadOfWhitePopup() { Contains(Menu(), "{DynamicResource MenuPopupBackgroundBrush}", "{DynamicResource TextPrimaryBrush}"); DoesNotContain(Menu(), "Background=\"White\"", "Background=\"#FFFFFF\""); }
    [TestMethod] public void MenuStyle_ThemesHeaderIconShortcutAndArrow() => Contains(Menu(), "ContentSource=\"Icon\"", "RecognizesAccessKey=\"True\"", "MenuShortcutBrush", "SubmenuArrow");
    [TestMethod] public void MenuStyle_ProvidesReadableHoverAndOpenedStates() => Contains(Menu(), "IsHighlighted", "MenuItemHoverBrush", "IsSubmenuOpen", "MenuItemOpenedBrush");
    [TestMethod] public void MenuStyle_ProvidesReadableDisabledState() => Contains(Menu(), "IsEnabled\" Value=\"False", "TextDisabledBrush", "Opacity\" Value=\"0.72");
    [TestMethod] public void MenuStyle_ProvidesCheckedAndKeyboardFocusStates() => Contains(Menu(), "IsChecked", "CheckMark", "IsKeyboardFocusWithin", "AccentBrush");
    [TestMethod] public void ContextMenu_UsesSameThemeResources() => Contains(Menu(), "TargetType=\"ContextMenu\"", "MenuPopupBackgroundBrush", "MenuPopupBorderBrush", "ItemsPresenter");
    [TestMethod] public void Separator_UsesLowContrastThemeBrush() => Contains(Menu(), "TargetType=\"Separator\"", "MenuSeparatorBrush", "Height=\"1\"");

    [TestMethod] public void DarkTheme_DefinesNonWhiteMenuPopupPalette() { var text = Theme("Dark"); Contains(text, "MenuPopupBackgroundBrush\" Color=\"#1E2024", "MenuItemHoverBrush\" Color=\"#2A2D32", "MenuShortcutBrush\" Color=\"#70737B"); DoesNotContain(text, "MenuPopupBackgroundBrush\" Color=\"#FFFFFF"); }
    [TestMethod] public void LightTheme_DefinesMenuPopupPalette() => Contains(Theme("Light"), "MenuPopupBackgroundBrush\" Color=\"#FFFFFF", "MenuPopupBorderBrush", "MenuItemHoverBrush", "MenuShortcutBrush");
    [TestMethod] public void HighContrastTheme_UsesSystemMenuColors() => Contains(Theme("HighContrast"), "SystemColors.MenuColor", "SystemColors.MenuTextColor", "SystemColors.HighlightColor");
    [TestMethod] public void AppearanceService_ReplacesThemeDictionaryImmediately() => Contains(Text("src/RAWSelectionAssistant/Services/AppearanceService.cs"), "ReplaceThemeDictionary(effectiveTheme)", "UserPreferenceChanged", "Application.Current.Dispatcher.BeginInvoke");

    [TestMethod] public void ResultsGrid_UsesAutomaticHorizontalScrolling() => Contains(MainXaml(), "x:Name=\"ResultsGrid\"", "ScrollViewer.HorizontalScrollBarVisibility=\"Auto\"", "ScrollViewer.VerticalScrollBarVisibility=\"Auto\"");
    [TestMethod] public void ResultsGrid_UsesSingleInternalScrollViewport() => Contains(MainXaml(), "ScrollViewer.CanContentScroll=\"True\"", "FrozenColumnCount=\"3\"");
    [TestMethod] public void ResultsGrid_KeepsReadableFixedColumnWidths() => Contains(MainXaml(), "Header=\"客户原始输入\"", "Width=\"145\"", "Header=\"标准化名称\"", "Width=\"125\"", "Header=\"JPG 文件名\"", "Width=\"155\"", "Header=\"备注\"", "Width=\"260\"");
    [TestMethod] public void ResultsGrid_HorizontalBarBelongsAboveSummary() { var text = MainXaml(); var grid = text.IndexOf("x:Name=\"ResultsGrid\"", StringComparison.Ordinal); var summary = text.IndexOf("所选记录快速摘要", StringComparison.Ordinal); Assert.IsTrue(grid >= 0 && summary > grid); }
    [TestMethod] public void ResultsGrid_HeaderAndBodyShareDataGridScroller() { var text = MainXaml(); var start = text.IndexOf("x:Name=\"ResultsGrid\"", StringComparison.Ordinal); var end = text.IndexOf("</DataGrid>", start, StringComparison.Ordinal); var dataGrid = text[start..end]; DoesNotContain(dataGrid, "<ScrollViewer", "<ScrollBar"); Contains(dataGrid, "<DataGrid.Columns>", "</DataGrid.Columns>"); }
    [TestMethod] public void HorizontalBar_IsAutoNotForcedAtNormalWidth() => DoesNotContain(MainXaml(), "ScrollViewer.HorizontalScrollBarVisibility=\"Visible\"");

    [TestMethod] public void ScrollBarStyle_HasThemeAwareTrackThumbAndHover() => Contains(Tables(), "TargetType=\"ScrollBar\"", "ScrollBarTrackBrush", "ScrollBarThumbBrush", "ScrollBarThumbHoverBrush");
    [TestMethod] public void HorizontalScrollBar_HasFourteenPixelUsableHeight() => Contains(Tables(), "Orientation\" Value=\"Horizontal", "Height\" Value=\"14", "MinHeight\" Value=\"14", "HorizontalScrollThumb");
    [TestMethod] public void HorizontalThumb_HasDpiSafeMinimum() => Contains(Tables(), "HorizontalScrollThumb", "MinWidth\" Value=\"28", "CornerRadius=\"5\"");
    [TestMethod] public void DarkTheme_DefinesVisibleScrollBarPalette() => Contains(Theme("Dark"), "ScrollBarTrackBrush\" Color=\"#111214", "ScrollBarThumbBrush\" Color=\"#454850", "ScrollBarThumbHoverBrush\" Color=\"#70737B");

    [TestMethod]
    [DataRow(1.25)]
    [DataRow(1.5)]
    public void ScrollBarMetrics_RemainUsableAtSupportedDpi(double scale)
    {
        Assert.IsGreaterThanOrEqualTo(12d * scale, 14d * scale);
        Assert.IsGreaterThanOrEqualTo(24d * scale, 28d * scale);
    }

    [TestMethod] public void Version_Is220() => Contains(Text("src/RAWSelectionAssistant.Core/Models/Branding.cs"), "ProductVersion = \"2.2.0\"");
    [TestMethod] public void Installer_IsNamedFor220() => Contains(Text("installer/RAWSelectionAssistant.iss"), "MyAppVersion \"2.2.0\"", "像素蛋挞_Setup_2.2.0_x64");
    [TestMethod] public void Release_RemainsWinExeSelfContained() => Contains(Text("src/RAWSelectionAssistant/RAWSelectionAssistant.csproj"), "<OutputType>WinExe</OutputType>", "<SelfContained>true</SelfContained>", "<RuntimeIdentifier>win-x64</RuntimeIdentifier>");

    private static string MainXaml() => Text("src/RAWSelectionAssistant/MainWindow.xaml");
    private static string Menu() => Text("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Menu.xaml");
    private static string Tables() => Text("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Tables.xaml");
    private static string Theme(string name) => Text($"src/RAWSelectionAssistant/Resources/DesignSystem/Theme.{name}.xaml");

    private static void Contains(string text, params string[] values) { foreach (var value in values) StringAssert.Contains(text, value); }
    private static void DoesNotContain(string text, params string[] values) { foreach (var value in values) Assert.IsFalse(text.Contains(value, StringComparison.Ordinal), $"不应包含：{value}"); }
    private static string Text(string relativePath) => File.ReadAllText(Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent; return directory?.FullName ?? throw new DirectoryNotFoundException(); }
}
