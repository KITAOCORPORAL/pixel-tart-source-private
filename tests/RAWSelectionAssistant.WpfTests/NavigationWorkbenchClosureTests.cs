using System.IO;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class NavigationWorkbenchClosureTests
{
    [TestMethod]
    public void Sidebar_HasTheExactSevenPrimaryPagesInProductOrder()
    {
        var source = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        var document = XDocument.Parse(source);
        var sidebar = document.Descendants().Single(element => Attribute(element, "Name") == "SidebarContainer");
        var primaryGroup = sidebar.Descendants().Single(element => Attribute(element, "Name") == "PrimaryNavigationGroup");
        var primaryKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "Workbench", "AssetLibrary", "Workflow", "WorkCalendar", "Tether", "Finance", "History"
        };
        var primaryButtons = primaryGroup.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Where(element => primaryKeys.Contains(Attribute(element, "CommandParameter") ?? string.Empty))
            .ToArray();

        CollectionAssert.AreEqual(
            new[] { "Workbench", "AssetLibrary", "Workflow", "WorkCalendar", "Tether", "Finance", "History" },
            primaryButtons.Select(element => Attribute(element, "CommandParameter")).ToArray());
        CollectionAssert.AreEqual(
            new[] { "工作台", "素材库", "归片工作区", "工作日历", "联机拍摄", "摄影收支", "项目历史" },
            primaryButtons.Select(element => Attribute(element, "Content")).ToArray());
        Assert.AreEqual("AssetLibraryNavigationButton", Attribute(primaryButtons[1], "AutomationProperties.AutomationId"));
        Assert.AreEqual(1, sidebar.Descendants().Count(element => Attribute(element, "CommandParameter") == "AssetLibrary"));
        Assert.IsFalse(primaryGroup.Descendants().Any(element => Attribute(element, "CommandParameter") == "OnlineSelection"));
        Assert.IsFalse(sidebar.Descendants().Any(element =>
            Attribute(element, "AutomationProperties.AutomationId") is "ToolboxAssetLibraryEntry" or "ToolboxPageAssetLibraryEntry"));

        var sidebarSource = source[source.IndexOf("x:Name=\"SidebarContainer\"", StringComparison.Ordinal)..source.IndexOf("x:Name=\"WorkbenchShell\"", StringComparison.Ordinal)];
        foreach (var entry in new[] { "授权与版本", "设置", "帮助" })
            StringAssert.Contains(sidebarSource, $"AutomationProperties.Name=\"{entry}\"");
        ContainsAll(sidebarSource, "Text=\"工作\"", "Text=\"工具\"", "Text=\"系统\"");
        Assert.IsFalse(sidebarSource.Contains("Content=\"使用教程\"", StringComparison.Ordinal));
        Assert.IsFalse(sidebarSource.Contains("Content=\"问题反馈\"", StringComparison.Ordinal));
        StringAssert.Contains(source, "Content=\"打开帮助与教程\"");
        StringAssert.Contains(source, "Content=\"建议与问题反馈\"");
    }

    [TestMethod]
    public void AssetPrimaryNavigationHasIconSelectionFocusHoverAndDirectionalKeyboardContracts()
    {
        var source = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        var start = source.IndexOf("AutomationProperties.AutomationId=\"AssetLibraryNavigationButton\"", StringComparison.Ordinal);
        var buttonStart = source.LastIndexOf("<Button", start, StringComparison.Ordinal);
        var buttonEnd = source.IndexOf("</Button>", start, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, buttonStart);
        Assert.IsGreaterThan(buttonStart, buttonEnd);
        var button = source[buttonStart..buttonEnd];
        ContainsAll(button,
            "Content=\"素材库\"",
            "Tag=\"{StaticResource IconAssetLibrary}\"",
            "CommandParameter=\"AssetLibrary\"",
            "IsAssetLibraryPage");
        ContainsAll(source,
            "x:Name=\"PrimaryNavigationGroup\"",
            "KeyboardNavigation.DirectionalNavigation=\"Contained\"",
            "KeyboardNavigation.TabNavigation=\"Continue\"");

        var navigationStyle = Read("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Navigation.xaml");
        ContainsAll(navigationStyle,
            "IsKeyboardFocused",
            "KeyboardFocusRing",
            "SidebarSelectedBorderThickness");
        var buttonStyle = Read("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Buttons.xaml");
        ContainsAll(buttonStyle, "x:Key=\"GhostButton\"", "IsMouseOver", "SurfaceHoverBrush");
    }

    [TestMethod]
    public void Workbench_HasFourQuickActionSlotsWithIndependentToolbox()
    {
        var source = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        var start = source.IndexOf("x:Name=\"WorkbenchQuickActions\"", StringComparison.Ordinal);
        var end = source.IndexOf("x:Name=\"ProjectOverviewCard\"", start, StringComparison.Ordinal);
        var actions = source[start..end];

        StringAssert.Contains(actions, "ItemsSource=\"{Binding DisplayedPinnedToolboxItems}\"");
        StringAssert.Contains(actions, "WrapPanel ItemWidth=\"116\"");
        StringAssert.Contains(actions, "x:Name=\"ToolboxQuickButton\"");
        StringAssert.Contains(actions, "x:Name=\"WorkbenchToolboxPopup\"");
    }

    [TestMethod]
    public void OnlineSelection_KeepsRouteAndViewHostWithoutAFirstLevelSidebarEntry()
    {
        var source = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        ContainsAll(source, "<views:OnlineSelectionView");
        ContainsAll(Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs"), "IsOnlineSelectionPage", "\"OnlineSelection\"");
        var document = XDocument.Parse(source);
        var primaryGroup = document.Descendants().Single(element => Attribute(element, "Name") == "PrimaryNavigationGroup");
        Assert.IsFalse(primaryGroup.Descendants().Any(element => Attribute(element, "CommandParameter") == "OnlineSelection"));
    }

    [TestMethod]
    public void AssetLibraryCtrlFUsesTheVisibleEmbeddedSearchInsteadOfTheRawWorkspaceSearch()
    {
        var source = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        var start = source.IndexOf("private void FocusSearchForActivePage()", StringComparison.Ordinal);
        var end = source.IndexOf("private bool TryCloseActiveInputPopup()", start, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start);
        Assert.IsGreaterThan(start, end);
        var focusRouter = source[start..end];
        ContainsAll(focusRouter,
            "IsAssetLibraryPage",
            "AssetLibraryWorkspace.Content",
            "FocusSearch()",
            "AssetLibraryWorkspace.RequestInitialFocus()",
            "SearchBox.Focus()");
        Assert.IsLessThan(
            upperBound: focusRouter.IndexOf("SearchBox.Focus()", StringComparison.Ordinal),
            value: focusRouter.IndexOf("return;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void WorkCalendar_UsesSixtyFortyCalendarAndDetailsColumns()
    {
        var source = Read("src/RAWSelectionAssistant/Views/WorkCalendarView.xaml");
        ContainsAll(source, "<ColumnDefinition Width=\"60*\"", "<ColumnDefinition Width=\"40*\"", "x:Name=\"DayDetailsHost\"");
    }

    [TestMethod]
    public void NavigationAndCalendarMarkupRemainWellFormed()
    {
        XDocument.Parse(Read("src/RAWSelectionAssistant/MainWindow.xaml"));
        XDocument.Parse(Read("src/RAWSelectionAssistant/Views/WorkCalendarView.xaml"));
    }

    [TestMethod]
    public void CalendarTaskCardsAndQuickDrawerUseFocusedResponsiveLayout()
    {
        var month = Read("src/RAWSelectionAssistant/Views/MonthCalendarView.xaml");
        var quick = Read("src/RAWSelectionAssistant/Views/QuickBookingEditorView.xaml");
        ContainsAll(month, "Header=\"编辑排期\"", "Text=\"{Binding MonthTitle}\"", "Text=\"{Binding TimeText, Mode=OneWay}\"");
        Assert.IsFalse(month.Contains("Content=\"编辑\"", StringComparison.Ordinal));
        Assert.IsFalse(month.Contains("Text=\"{Binding MonthWeatherIcon}\"", StringComparison.Ordinal));
        Assert.IsFalse(month.Contains("{Binding WorkflowStatusText, Mode=OneWay}", StringComparison.Ordinal));
        Assert.IsFalse(quick.Contains("MinWidth=\"620\"", StringComparison.Ordinal));
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));

    private static void ContainsAll(string source, params string[] values)
    {
        foreach (var value in values) StringAssert.Contains(source, value);
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static string? Attribute(XElement element, string localName) =>
        element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;

        throw new DirectoryNotFoundException("RAWSelectionAssistant.sln was not found.");
    }
}
