using System.IO;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class NavigationWorkbenchClosureTests
{
    [TestMethod]
    public void Sidebar_HasSingleBusinessNavigationAndNoDuplicateSupportFooter()
    {
        var source = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        var start = source.IndexOf("x:Name=\"SidebarContainer\"", StringComparison.Ordinal);
        var end = source.IndexOf("x:Name=\"WorkbenchShell\"", start, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start);
        Assert.IsGreaterThan(start, end);
        var sidebar = source[start..end];

        foreach (var entry in new[] { "工作台", "归片工作区", "工作日历", "在线选片", "联机拍摄", "摄影收支", "项目历史", "授权与版本", "设置", "帮助" })
            StringAssert.Contains(sidebar, $"AutomationProperties.Name=\"{entry}\"");

        ContainsAll(sidebar, "Text=\"工作\"", "Text=\"工具\"", "Text=\"系统\"",
            "Content=\"摄影收支\"", "Tag=\"{StaticResource IconOnlineSelection}\"");
        Assert.AreEqual(1, Count(sidebar, "Tag=\"{StaticResource IconPhotoStack}\""));

        Assert.IsFalse(sidebar.Contains("Content=\"使用教程\"", StringComparison.Ordinal));
        Assert.IsFalse(sidebar.Contains("Content=\"问题反馈\"", StringComparison.Ordinal));
        StringAssert.Contains(source, "Content=\"打开帮助与教程\"");
        StringAssert.Contains(source, "Content=\"建议与问题反馈\"");
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
    public void OnlineSelection_HasSidebarRouteAndViewHost()
    {
        var source = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        ContainsAll(source, "CommandParameter=\"OnlineSelection\"", "<views:OnlineSelectionView");
        ContainsAll(Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs"), "IsOnlineSelectionPage", "\"OnlineSelection\"");
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

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;

        throw new DirectoryNotFoundException("RAWSelectionAssistant.sln was not found.");
    }
}
