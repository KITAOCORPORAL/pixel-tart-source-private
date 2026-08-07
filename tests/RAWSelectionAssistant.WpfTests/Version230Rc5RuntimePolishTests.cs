using System.IO;
using System.Xml.Linq;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version230Rc5RuntimePolishTests
{
    [TestMethod]
    public void TaskCenter_UsesChineseLabelsForEveryLifecycleState()
    {
        foreach (var state in Enum.GetValues<TaskLifecycleState>())
        {
            var snapshot = new TaskProgressSnapshot(Guid.NewGuid(), null, "测试任务", state, 0, string.Empty, string.Empty, TaskResultSummary.Empty, null, null, null, null, DateTimeOffset.UtcNow);
            var label = new TaskSnapshotViewModel(snapshot).StateLabel;

            Assert.AreNotEqual(state.ToString(), label);
            Assert.IsFalse(label.Contains("Completed", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void LocalSplit_BlockerAndInteractionFillRegressionAreRemoved()
    {
        var source = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        Assert.IsFalse(source.Contains("x:Name=\"LocalSplitHelpToolTip\" StaysOpen=\"False\"", StringComparison.Ordinal));
        Assert.AreEqual(4, Count(source, "Background\" Value=\"{DynamicResource WorkbenchHeroBrush}"));
        StringAssert.Contains(source, "LocalSplitHeroButton");
        StringAssert.Contains(Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs"), "CurrentPersistedTaskProjectId");
    }

    [TestMethod]
    public void BookingVisibleTime_UsesStoredBookingZoneInsteadOfMachineZone()
    {
        var booking = Summary(ShootBookingStatus.Confirmed);
        var item = new CalendarBookingItemViewModel(booking);
        Assert.AreEqual("2026-08-15 09:00", item.StartText);
        Assert.AreEqual("09:00–11:00", item.TimeText);
        Assert.IsFalse(Read("src/RAWSelectionAssistant/Views/ArchivedBookingsView.xaml").Contains("StartAtUtc", StringComparison.Ordinal));
        Assert.IsFalse(Read("src/RAWSelectionAssistant/ViewModels/WeatherViewModels.cs").Contains("ToLocalTime()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void StaffArrival_RoundTripsThroughBookingTimeZone()
    {
        var editor = new BookingStaffEditorViewModel
        {
            DisplayName = "摄影师",
            SelectedRole = new BookingStaffRoleOption(BookingStaffRole.Photographer, "摄影师"),
            ArrivalTimeText = "2026-08-15 09:00"
        };
        var model = editor.ToModel(Guid.NewGuid(), 0, "China Standard Time");
        Assert.AreEqual(new DateTimeOffset(2026, 8, 15, 1, 0, 0, TimeSpan.Zero), model.ArrivalTime);
        var roundTrip = BookingStaffEditorViewModel.From(model, [editor.SelectedRole!], "China Standard Time");
        Assert.AreEqual("2026-08-15 09:00", roundTrip.ArrivalTimeText);
    }

    [TestMethod]
    public void CalendarMixedDay_UsesAtMostThreeSegmentsAndCountText()
    {
        var day = new MonthDayViewModel { Date = new DateTime(2026, 8, 15), IsCurrentMonth = true };
        day.VisibleBookings.Add(new(Summary(ShootBookingStatus.Confirmed)));
        day.VisibleBookings.Add(new(Summary(ShootBookingStatus.Completed)));
        day.VisibleBookings.Add(new(Summary(ShootBookingStatus.AwaitingDelivery)));
        day.VisibleBookings.Add(new(Summary(ShootBookingStatus.Delivered)));

        Assert.AreEqual("4场", day.BookingCountText);
        Assert.HasCount(3, day.WorkflowSegments);
        Assert.IsTrue(day.HasMixedWorkflowStatuses);
    }

    [TestMethod]
    [DataRow("Theme.Dark.xaml")]
    [DataRow("Theme.Light.xaml")]
    [DataRow("Theme.HighContrast.xaml")]
    public void CalendarThemes_KeepFiveFixedSemanticColorResources(string theme)
    {
        var source = Read($"src/RAWSelectionAssistant/Resources/DesignSystem/{theme}");
        foreach (var key in new[] { "CalendarStatusFreeBrush", "CalendarStatusScheduledBrush", "CalendarStatusShotBrush", "CalendarStatusPendingDeliveryBrush", "CalendarStatusDeliveredBrush" })
            StringAssert.Contains(source, key);
    }

    [TestMethod]
    [DataRow("空闲")]
    [DataRow("有拍摄")]
    [DataRow("已拍摄")]
    [DataRow("待返图")]
    [DataRow("已返图")]
    [DataRow("DetailedStatusOptions")]
    [DataRow("时间冲突")]
    [DataRow("天气风险")]
    public void FullCalendar_ExposesPrimaryAndDetailedStatusFilters(string token) =>
        ContainsAny(token, "src/RAWSelectionAssistant/Views/WorkCalendarView.xaml", "src/RAWSelectionAssistant/ViewModels/CalendarViewModels.cs");

    [TestMethod]
    public void CalendarDetails_UsesDarkTabsAndImmediateWorkflowSelector()
    {
        Contains("src/RAWSelectionAssistant/Views/ShootBookingDetailsView.xaml",
            "BookingDetailsTabItemStyle", "SurfacePrimaryBrush", "拍摄流程状态", "SelectedWorkflowStatus", "选择后立即保存");
        Contains("src/RAWSelectionAssistant/ViewModels/BookingEditorViewModels.cs", "ChangeWorkflowStatusAsync", "确认回退拍摄状态", "SetStatusAsync");
    }

    [TestMethod]
    public void Sidebar_PrimaryEntriesUseDistinctVectorKeysAndAccessibleNames()
    {
        var main = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        var keys = new[] { "IconDashboard", "IconPhotoStack", "IconCalendar", "IconCameraTether", "IconWallet", "IconArchiveHistory", "IconShieldKey", "IconGear", "IconQuestionCircle" };
        foreach (var key in keys) StringAssert.Contains(main, key);
        Assert.AreEqual(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        foreach (var name in new[] { "工作台", "归片工作区", "工作日历", "联机拍摄", "摄影收支", "项目历史", "授权与版本", "设置", "帮助" })
            StringAssert.Contains(main, $"AutomationProperties.Name=\"{name}\"");
        Contains("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Navigation.xaml", "ToolTipService.InitialShowDelay", "250");
    }

    [TestMethod]
    public void Sidebar_CollapsedModeUsesDedicatedPanelIcons()
    {
        Contains("src/RAWSelectionAssistant/MainWindow.xaml", "IconPanelCollapse", "IconPanelExpand", "收起侧栏", "展开侧栏");
        Contains("src/RAWSelectionAssistant/Resources/DesignSystem/Icons.Navigation.xaml", "IconToolbox", "IconBookPlay", "IconMessageBug");
    }

    [TestMethod]
    public void Weather_DefaultsToEnabledAutoLocationWithClearFallback()
    {
        var settings = new WeatherSettings();
        Assert.IsTrue(settings.Enabled);
        Assert.IsTrue(settings.AutoRefreshEnabled);
        Contains("src/RAWSelectionAssistant/ViewModels/WeatherViewModels.cs", "ResolveCurrentLocationAsync(force: false", "无法获取当前位置，请选择城市。", "其他城市");
        Contains("src/RAWSelectionAssistant/Views/BookingWeatherPanel.xaml", "重试当前位置", "IsManualCityMode");
    }

    [TestMethod]
    public void FinanceAndDocuments_UseCompactRc5Controls()
    {
        Contains("src/RAWSelectionAssistant/Views/FinanceView.xaml", "搜索交易、客户、项目或备注", "更多筛选", "SelectedCurrencyFilter");
        Contains("src/RAWSelectionAssistant/ViewModels/FinanceViewModel.cs", "全部分类");
        Contains("src/RAWSelectionAssistant/Views/BookingDocumentsPanel.xaml", "检查资料状态", "预览", "打开", "更多资料操作", "移除关联");
        Assert.IsFalse(Read("src/RAWSelectionAssistant/Views/BookingDocumentsPanel.xaml").Contains("检查全部关联文件", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BookingEditor_UsesDarkStepperAndBoundedContentWidth()
    {
        Contains("src/RAWSelectionAssistant/Views/ShootBookingEditorView.xaml", "EditorStepCircle", "Step1Glyph", "Step4Glyph", "MaxWidth=\"980\"", "HorizontalAlignment=\"Center\"");
        Assert.IsFalse(Read("src/RAWSelectionAssistant/Views/ShootBookingEditorView.xaml").Contains("Background=\"White\"", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void StatusBarAndIsolatedRuntimeHaveExplicitSeparationContracts()
    {
        Contains("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs", "CurrentPageStatus", "BackgroundTaskStatus", "NotificationStatus", "本月暂无收支记录");
        Contains("src/RAWSelectionAssistant.Core/Utilities/AppDataPaths.cs", "PIXEL_TART_ISOLATED_RUNTIME", "PIXEL_TART_ISOLATED_RUNTIME_ROOT", "Path.IsPathFullyQualified");
    }

    [TestMethod]
    public void Rc5ModifiedViewsRemainValidXaml()
    {
        foreach (var relative in new[]
        {
            "MainWindow.xaml", "Views/WorkCalendarView.xaml", "Views/MonthCalendarView.xaml", "Views/WorkbenchCalendarPanel.xaml",
            "Views/ShootBookingDetailsView.xaml", "Views/ShootBookingEditorView.xaml", "Views/BookingWeatherPanel.xaml",
            "Views/FinanceView.xaml", "Views/BookingDocumentsPanel.xaml", "Views/ArchivedBookingsView.xaml"
        })
            XDocument.Parse(Read("src/RAWSelectionAssistant/" + relative));
    }

    private static ShootBookingSummary Summary(ShootBookingStatus status) => new(
        Guid.NewGuid(), null, "排期", "客户代号",
        new DateTimeOffset(2026, 8, 15, 1, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 8, 15, 3, 0, 0, TimeSpan.Zero),
        "China Standard Time", false, status, "影棚", "Portrait", false, false);

    private static int Count(string source, string token)
    {
        var count = 0;
        for (var index = 0; (index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0; index += token.Length) count++;
        return count;
    }

    private static void Contains(string relative, params string[] tokens)
    {
        var source = Read(relative);
        foreach (var token in tokens) StringAssert.Contains(source, token);
    }

    private static void ContainsAny(string token, params string[] relatives)
    {
        Assert.IsTrue(relatives.Any(relative => Read(relative).Contains(token, StringComparison.Ordinal)), $"未找到：{token}");
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
