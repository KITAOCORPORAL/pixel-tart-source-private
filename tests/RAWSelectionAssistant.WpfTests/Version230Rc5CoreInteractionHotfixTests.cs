using System.IO;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version230Rc5CoreInteractionHotfixTests
{
    [TestMethod]
    [DataRow("CalendarStatusFreeBrush", "#59616B")]
    [DataRow("CalendarStatusScheduledBrush", "#E05252")]
    [DataRow("CalendarStatusShotBrush", "#3DB879")]
    [DataRow("CalendarStatusPendingDeliveryBrush", "#DDAF32")]
    [DataRow("CalendarStatusDeliveredBrush", "#3E8ED0")]
    public void Calendar_DarkThemeUsesVisibleFiveStatePalette(string key, string color)
    {
        var source = Text("src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Dark.xaml");
        StringAssert.Contains(source, $"x:Key=\"{key}\" Color=\"{color}\"");
    }

    [TestMethod]
    [DataRow(ShootBookingStatus.Tentative, CalendarWorkflowStatus.Scheduled)]
    [DataRow(ShootBookingStatus.Confirmed, CalendarWorkflowStatus.Scheduled)]
    [DataRow(ShootBookingStatus.Preparing, CalendarWorkflowStatus.Scheduled)]
    [DataRow(ShootBookingStatus.Shooting, CalendarWorkflowStatus.Shot)]
    [DataRow(ShootBookingStatus.Completed, CalendarWorkflowStatus.Shot)]
    [DataRow(ShootBookingStatus.AwaitingSelectionDelivery, CalendarWorkflowStatus.PendingDelivery)]
    [DataRow(ShootBookingStatus.AwaitingSelection, CalendarWorkflowStatus.PendingDelivery)]
    [DataRow(ShootBookingStatus.Selected, CalendarWorkflowStatus.PendingDelivery)]
    [DataRow(ShootBookingStatus.AwaitingRetouch, CalendarWorkflowStatus.PendingDelivery)]
    [DataRow(ShootBookingStatus.Retouched, CalendarWorkflowStatus.PendingDelivery)]
    [DataRow(ShootBookingStatus.AwaitingDelivery, CalendarWorkflowStatus.PendingDelivery)]
    [DataRow(ShootBookingStatus.Delivered, CalendarWorkflowStatus.Delivered)]
    public void Calendar_WorkflowMapperIsSingleStateSource(ShootBookingStatus status, CalendarWorkflowStatus expected) =>
        Assert.AreEqual(expected, CalendarWorkflowStatusMapper.FromBookingStatus(status));

    [TestMethod]
    [DataRow("MonthCalendarView.xaml")]
    [DataRow("WorkbenchCalendarPanel.xaml")]
    public void Calendar_DayNumberBadgeUsesRealWorkflowState(string view)
    {
        var source = Text($"src/RAWSelectionAssistant/Views/{view}");
        StringAssert.Contains(source, "PrimaryWorkflowStatus");
        StringAssert.Contains(source, "DayNumber");
        StringAssert.Contains(source, "CalendarStatusScheduledBrush");
    }

    [TestMethod]
    public void Calendar_FullViewUsesDirectRuntimeDayStateTriggers()
    {
        var source = Text("src/RAWSelectionAssistant/Views/MonthCalendarView.xaml");
        StringAssert.Contains(source, "Binding PrimaryWorkflowStatus");
        StringAssert.Contains(source, "CalendarStatusDeliveredBrush");
        Assert.DoesNotContain("WorkflowSegments", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Height=\"2\" VerticalAlignment=\"Bottom\"", source, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Calendar_StatusSaveRefreshesAllSharedConsumers()
    {
        var calendar = Text("src/RAWSelectionAssistant/ViewModels/CalendarViewModels.cs");
        var workbench = Text("src/RAWSelectionAssistant/ViewModels/StageDViewModels.cs");
        StringAssert.Contains(calendar, "WorkflowStatusChanged += (_, _) => _ = RefreshAfterBookingChangeAsync()");
        StringAssert.Contains(workbench, "BookingChanged += BookingChanges_BookingChanged");
    }

    [TestMethod]
    [DataRow("ToolIconPinOutline")]
    [DataRow("ToolIconPinFilled")]
    [DataRow("PinIconResourceKey")]
    [DataRow("Width=\"32\" Height=\"32\"")]
    [DataRow("固定到工作台")]
    [DataRow("从工作台取消固定")]
    [DataRow("已固定")]
    public void Toolbox_PinUsesAccessibleVectorState(string token)
    {
        var source = Text("src/RAWSelectionAssistant/MainWindow.xaml") + Text("src/RAWSelectionAssistant/ViewModels/ToolboxItemViewModel.cs") + Text("src/RAWSelectionAssistant/Resources/DesignSystem/Icons.Tools.xaml");
        StringAssert.Contains(source, token);
    }

    [TestMethod]
    [DataRow(ToolId.LocalSplit)]
    [DataRow(ToolId.Workflow)]
    [DataRow(ToolId.PhotoOrganize)]
    [DataRow(ToolId.Collage)]
    [DataRow(ToolId.BatchCompress)]
    [DataRow(ToolId.Watermark)]
    public void Toolbox_ReleaseCatalogContainsOnlyCoreEntries(ToolId expected) =>
        Assert.IsTrue(ToolRegistry.ReleaseCatalog.Any(item => item.Id == expected));

    [TestMethod]
    [DataRow(ToolId.DeleteRejects)]
    [DataRow(ToolId.FtpTool)]
    [DataRow(ToolId.BatchRename)]
    [DataRow(ToolId.BatchConvert)]
    public void Toolbox_ReleaseCatalogHidesLowValueEntries(ToolId hidden) =>
        Assert.IsFalse(ToolRegistry.ReleaseCatalog.Any(item => item.Id == hidden));

    [TestMethod]
    public void Toolbox_WatermarkIsPreviewAndNeverDefaultPinned()
    {
        Assert.AreEqual(FeatureAvailability.Preview, ToolRegistry.Get(ToolId.Watermark).Availability);
        Assert.DoesNotContain(ToolId.Watermark.ToString(), QuickToolsService.DefaultPinnedTools);
    }

    [TestMethod]
    public void Toolbox_LegacyHiddenPinsAreNormalizedAway()
    {
        var values = QuickToolsService.Normalize(["DeleteRejects", "FtpTool", "BatchRename", "BatchConvert", "Collage"]);
        CollectionAssert.AreEqual(new[] { "Collage" }, values);
    }

    [TestMethod]
    public void Toolbox_CapacityMessageIsUserFacing()
    {
        StringAssert.Contains(Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs"), "工作台快捷区已满，请先取消一个已固定工具。");
    }

    [TestMethod]
    [DataRow("OpenDayDetailsForDateAsync")]
    [DataRow("NavigateToCalendarDetailsAsync")]
    [DataRow("day.Date")]
    [DataRow("CurrentPage = \"WorkCalendar\"")]
    [DataRow("DaySchedule.Bookings.FirstOrDefault")]
    public void Calendar_ViewDayDetailsUsesRealContextDate(string token)
    {
        var source = Text("src/RAWSelectionAssistant/Views/WorkbenchCalendarPanel.xaml.cs") + Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs") + Text("src/RAWSelectionAssistant/ViewModels/CalendarViewModels.cs");
        StringAssert.Contains(source, token);
    }

    [TestMethod]
    [DataRow("Title")]
    [DataRow("TimeText")]
    [DataRow("StatusText")]
    [DataRow("ClientDisplayName")]
    [DataRow("Booking.Location")]
    [DataRow("VisibleBookings")]
    [DataRow("还有 {OverflowCount} 项")]
    public void Calendar_DayDetailsContainRequiredSummary(string token)
    {
        var source = Text("src/RAWSelectionAssistant/Views/DaySchedulePanel.xaml") + Text("src/RAWSelectionAssistant/ViewModels/CalendarViewModels.cs");
        StringAssert.Contains(source, token);
    }

    [TestMethod]
    public void Workbench_UpcomingCardsMakeProjectNameProminent()
    {
        var source = Text("src/RAWSelectionAssistant/Views/WorkbenchScheduleView.xaml") + Text("src/RAWSelectionAssistant/ViewModels/StageDViewModels.cs");
        StringAssert.Contains(source, "Text=\"{Binding Title}\" FontSize=\"14\" FontWeight=\"SemiBold\"");
        StringAssert.Contains(source, "项目名称：{Title}");
    }

    [TestMethod]
    [DataRow("SelectedTabIndex = 0")]
    [DataRow("OverviewScroll.ScrollToTop")]
    [DataRow("PrepareForNavigation")]
    [DataRow("DayDetailsNavigationRequested")]
    [DataRow("DayDetailsHost")]
    [DataRow("DayDetailsPanel")]
    public void Calendar_DetailsNavigationResetsOverviewScrollAndFocus(string token)
    {
        var source = Text("src/RAWSelectionAssistant/Views/ShootBookingDetailsView.xaml.cs") + Text("src/RAWSelectionAssistant/Views/WorkCalendarView.xaml.cs") + Text("src/RAWSelectionAssistant/Views/WorkCalendarView.xaml");
        StringAssert.Contains(source, token);
    }

    [TestMethod]
    [DataRow(".ppt")]
    [DataRow(".pptx")]
    [DataRow(".pdf")]
    [DataRow(".docx")]
    [DataRow(".txt")]
    public void Documents_SupportedDraftFileIsNotMissing(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"rc5-hotfix-{Guid.NewGuid():N}{extension}");
        try
        {
            File.WriteAllText(path, "isolated test");
            var record = new BookingDocumentRecord { FilePath = path, DisplayName = Path.GetFileName(path), FileExtension = extension, IsMissing = false };
            var item = new BookingDocumentItemViewModel(record, BookingDocumentFileState.WaitingForConfirmation);
            Assert.IsFalse(item.IsMissing);
            Assert.AreEqual("等待确认", item.StateText);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [TestMethod]
    [DataRow(0L, 0L, true)]
    [DataRow(23_200L, 23_200L, true)]
    [DataRow(23_200L, 44_400L, false)]
    public void Finance_DepositCannotExceedTotal(long total, long deposit, bool valid) =>
        Assert.AreEqual(valid, BookingMoneyCalculator.Validate(total, deposit, 0, 2).Count == 0);

    [TestMethod]
    public void Finance_OverpaymentWarnsAndNeverDisplaysNegativeReceivable()
    {
        var result = BookingMoneyCalculator.Calculate(23_200, 0, 44_400);
        Assert.AreEqual(BookingMoneyDisplayKind.Overpaid, result.DisplayKind);
        Assert.AreEqual(21_200, result.DisplayAmountMinor);
        StringAssert.Contains(result.Warnings.Single().Message, "追加费用");
    }

    [TestMethod]
    [DataRow("全部类型")]
    [DataRow("全部支付状态")]
    [DataRow("全部分类")]
    public void Finance_DefaultFiltersAreExplicit(string label) =>
        StringAssert.Contains(Text("src/RAWSelectionAssistant/ViewModels/FinanceViewModel.cs"), label);

    [TestMethod]
    public void TaskCenter_ShowsActiveAndOnlyTwoRecentCompleted()
    {
        var source = Text("src/RAWSelectionAssistant/ViewModels/TaskCenterViewModels.cs") + Text("src/RAWSelectionAssistant/MainWindow.xaml");
        StringAssert.Contains(source, "Tasks.Where(item => !item.IsTerminal)");
        StringAssert.Contains(source, "Tasks.Where(item => item.IsTerminal).Take(2)");
        StringAssert.Contains(source, "TaskCenter.VisibleTasks");
    }

    private static string Text(string relativePath) => File.ReadAllText(Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
