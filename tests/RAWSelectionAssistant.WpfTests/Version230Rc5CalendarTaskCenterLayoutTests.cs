using System.IO;
using System.Xml.Linq;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version230Rc5CalendarTaskCenterLayoutTests
{
    [TestMethod]
    public void MiniCalendar_DayNumberBadgeOwnsWorkflowColorWithoutBottomStatusBars()
    {
        var xaml = Read("src/RAWSelectionAssistant/Views/WorkbenchCalendarPanel.xaml");
        Contains(xaml, "MinWidth=\"28\"", "Height=\"24\"", "Padding=\"6,2\"", "CornerRadius=\"5\"", "TooltipText");
        Contains(xaml, "CalendarStatusFreeBrush", "CalendarStatusScheduledBrush", "CalendarStatusShotBrush", "CalendarStatusPendingDeliveryBrush", "CalendarStatusDeliveredBrush");
        Assert.IsFalse(xaml.Contains("ItemsSource=\"{Binding WorkflowSegments}\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("Height=\"2\" VerticalAlignment=\"Bottom\" Background=\"{DynamicResource CalendarStatusFreeBrush}\"", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("Theme.Dark.xaml")]
    [DataRow("Theme.Light.xaml")]
    [DataRow("Theme.HighContrast.xaml")]
    public void MiniCalendar_ThemesProvideHighContrastBadgeForegrounds(string theme)
    {
        var source = Read($"src/RAWSelectionAssistant/Resources/DesignSystem/{theme}");
        foreach (var key in new[]
        {
            "CalendarStatusFreeForegroundBrush", "CalendarStatusScheduledForegroundBrush", "CalendarStatusShotForegroundBrush",
            "CalendarStatusPendingDeliveryForegroundBrush", "CalendarStatusDeliveredForegroundBrush", "CalendarTodayOutlineBrush"
        }) StringAssert.Contains(source, key);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(7)]
    [DataRow(8)]
    [DataRow(10)]
    [DataRow(11)]
    [DataRow(18)]
    [DataRow(28)]
    [DataRow(31)]
    public void MiniCalendar_DayNumberRemainsExactForSingleAndDoubleDigits(int dayNumber)
    {
        var day = new MonthDayViewModel { Date = new DateTime(2026, 8, dayNumber), IsCurrentMonth = true };
        Assert.AreEqual(dayNumber, day.DayNumber);
        StringAssert.Contains(Read("src/RAWSelectionAssistant/Views/WorkbenchCalendarPanel.xaml"), "MinWidth=\"28\"");
    }

    [TestMethod]
    public void MiniCalendar_PrimaryStateUsesAttentionPriorityNotInsertionOrder()
    {
        var day = new MonthDayViewModel { Date = new DateTime(2026, 8, 7), IsCurrentMonth = true };
        day.VisibleBookings.Add(Item(ShootBookingStatus.Delivered, "已交付"));
        day.VisibleBookings.Add(Item(ShootBookingStatus.AwaitingDelivery, "待交付"));
        day.VisibleBookings.Add(Item(ShootBookingStatus.Completed, "已拍摄"));
        day.VisibleBookings.Add(Item(ShootBookingStatus.Confirmed, "未拍摄"));

        Assert.AreEqual(CalendarWorkflowStatus.Scheduled, day.PrimaryWorkflowStatus);
        Assert.IsTrue(day.HasMultipleBookings);
        Assert.AreEqual("4", day.BookingCountBadgeText);
        StringAssert.Contains(day.TooltipText, "4场拍摄");
        StringAssert.Contains(day.TooltipText, "• 未拍摄：有拍摄");
    }

    [TestMethod]
    [DataRow(ShootBookingStatus.Confirmed, CalendarWorkflowStatus.Scheduled)]
    [DataRow(ShootBookingStatus.Completed, CalendarWorkflowStatus.Shot)]
    [DataRow(ShootBookingStatus.AwaitingDelivery, CalendarWorkflowStatus.PendingDelivery)]
    [DataRow(ShootBookingStatus.Delivered, CalendarWorkflowStatus.Delivered)]
    public void MiniCalendar_SingleBookingKeepsExistingWorkflowSemantics(ShootBookingStatus status, CalendarWorkflowStatus expected)
    {
        var day = new MonthDayViewModel { Date = new DateTime(2026, 8, 7), IsCurrentMonth = true };
        day.VisibleBookings.Add(Item(status, "拍摄"));
        Assert.AreEqual(expected, day.PrimaryWorkflowStatus);
    }

    [TestMethod]
    public void MiniCalendar_TodayAndSelectionUseIndependentOutlineChannels()
    {
        var xaml = Read("src/RAWSelectionAssistant/Views/WorkbenchCalendarPanel.xaml");
        Contains(xaml, "Binding=\"{Binding IsToday}\"", "CalendarTodayOutlineBrush", "Binding=\"{Binding IsSelected}\"", "BorderBrush\" Value=\"{DynamicResource AccentBrush}");
        Assert.IsFalse(xaml.Contains("DataTrigger Binding=\"{Binding IsSelected}\" Value=\"True\"><Setter Property=\"Background\"", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(1920)]
    [DataRow(1600)]
    [DataRow(1440)]
    [DataRow(1280)]
    public void FullCalendar_HeaderHasPredictableResponsiveContracts(int width)
    {
        var xaml = Read("src/RAWSelectionAssistant/Views/WorkCalendarView.xaml");
        var code = Read("src/RAWSelectionAssistant/Views/WorkCalendarView.xaml.cs");
        Contains(xaml, "CalendarViewGroup", "CalendarDateNavigationGroup", "CalendarFilterSearchGroup", "Width=\"24\"", "DisplayYear", "DisplayMonth", "Margin=\"16,0,0,0\"");
        Contains(code, "width < 1180", "Grid.SetRow(CalendarFilterSearchGroup", "Grid.SetColumnSpan(CalendarFilterSearchGroup", "< 1050 => 150", "< 1400 => 170", "_ => 210");
        CollectionAssert.Contains(new[] { 1280, 1440, 1600, 1920 }, width);
    }

    [TestMethod]
    public void FullCalendar_MonthGridKeepsToolbarLegendWeekdayAndCellsApart()
    {
        Contains(Read("src/RAWSelectionAssistant/Views/WorkCalendarView.xaml"), "Margin=\"0,14,0,18\"", "Margin=\"0,0,0,12\"");
        Contains(Read("src/RAWSelectionAssistant/Views/MonthCalendarView.xaml"), "Height=\"36\"", "Margin=\"0,10,0,0\"");
    }

    [TestMethod]
    public void TaskCenter_UsesBalancedHeightAndFixedHeaderSummaryListFooter()
    {
        var xaml = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        Contains(xaml, "Height=\"52*\"", "Height=\"48*\" MinHeight=\"320\"", "Margin=\"0,16,0,0\" MinHeight=\"320\"",
            "TaskCenterRuntimeContent", "VerticalScrollBarVisibility=\"Auto\"", "TaskCenterEmptyState", "暂无处理任务", "需要处理", "更多任务操作");
        Assert.AreEqual(1, Count(xaml, "x:Name=\"TaskCenterRuntimeContent\""));
    }

    [TestMethod]
    public void TaskCenter_ListCarriesSourceProgressStateAndUpdatedTime()
    {
        var xaml = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        Contains(xaml, "SourceModuleText", "UpdatedAtText", "StateLabel", "Progress", "CurrentStep", "查看任务历史");
        var item = new TaskSnapshotViewModel(new(Guid.NewGuid(), null, "联机复制", TaskLifecycleState.Running, 50, "复制中", string.Empty,
            TaskResultSummary.Empty, null, null, null, null, new DateTimeOffset(2026, 8, 7, 12, 30, 0, TimeSpan.Zero)));
        Assert.AreEqual("来源：联机拍摄", item.SourceModuleText);
        StringAssert.StartsWith(item.UpdatedAtText, "更新 ");
    }

    [TestMethod]
    public void TaskCenter_ResponsiveWidthStaysWithinRequestedBands()
    {
        var code = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        Contains(code, "ActualWidth >= 1920 ? 360d : 320d", "TaskCenterPanel.Width = compact ? 300", "shortWorkbench", "veryShortWorkbench", "WorkbenchOverviewRow.Height");
    }

    [TestMethod]
    public void LayoutPatch_ModifiedViewsRemainValidXaml()
    {
        foreach (var relative in new[]
        {
            "src/RAWSelectionAssistant/MainWindow.xaml", "src/RAWSelectionAssistant/Views/WorkbenchCalendarPanel.xaml",
            "src/RAWSelectionAssistant/Views/WorkCalendarView.xaml", "src/RAWSelectionAssistant/Views/MonthCalendarView.xaml"
        }) XDocument.Parse(Read(relative));
    }

    private static CalendarBookingItemViewModel Item(ShootBookingStatus status, string title)
    {
        var start = new DateTimeOffset(2026, 8, 7, 1, 0, 0, TimeSpan.Zero);
        return new(new ShootBookingSummary(Guid.NewGuid(), null, title, "客户代号", start, start.AddHours(1), "China Standard Time", false,
            status, "影棚", "Portrait", false, false));
    }

    private static int Count(string source, string token)
    {
        var count = 0;
        for (var index = 0; (index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0; index += token.Length) count++;
        return count;
    }

    private static void Contains(string source, params string[] tokens)
    {
        foreach (var token in tokens) StringAssert.Contains(source, token);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
