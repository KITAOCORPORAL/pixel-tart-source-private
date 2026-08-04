using System.Xml.Linq;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version220StageBUiArchitectureTests
{
    private static readonly string Root = FindRoot();

    [TestMethod]
    public void WorkCalendar_IsAFirstLevelNavigationEntry()
    {
        var xaml = Text("src/RAWSelectionAssistant/MainWindow.xaml");
        Contains(xaml, "Content=\"工作日历\"", "CommandParameter=\"WorkCalendar\"", "<views:WorkCalendarView", "DataContext=\"{Binding WorkCalendarPage}\"");
    }

    [TestMethod]
    public void MainWindow_OnlyHostsCalendarAndDoesNotContainCalendarGrid()
    {
        var xaml = Text("src/RAWSelectionAssistant/MainWindow.xaml");
        Assert.IsFalse(xaml.Contains("UniformGrid Columns=\"7\" Rows=\"6\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("DayTimeSlotViewModel", StringComparison.Ordinal));
    }

    [TestMethod]
    public void EveryRequiredStageBViewAndViewModelExists()
    {
        foreach (var (viewFile, viewModel) in new[]
        {
            ("WorkCalendarView.xaml", "WorkCalendarViewModel"), ("MonthCalendarView.xaml", "MonthCalendarViewModel"),
            ("WeekCalendarView.xaml", "WeekCalendarViewModel"), ("DayCalendarView.xaml", "DayCalendarViewModel"),
            ("DaySchedulePanel.xaml", "DaySchedulePanelViewModel"), ("ShootBookingDetailsView.xaml", "ShootBookingDetailsViewModel"),
            ("ShootBookingEditorView.xaml", "ShootBookingEditorViewModel"), ("ShootRequirementsPanel.xaml", "ShootRequirementsViewModel"),
            ("ArchivedBookingsView.xaml", "ArchivedBookingsViewModel")
        })
        {
            Assert.IsTrue(File.Exists(Path.Combine(Root, "src", "RAWSelectionAssistant", "Views", viewFile)), viewFile);
            Contains(CalendarSources(), $"class {viewModel}");
        }
    }

    [TestMethod]
    public void CalendarToolbar_ContainsAllRequiredNavigationSearchAndFilterActions()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/WorkCalendarView.xaml");
        foreach (var value in new[] { "今日", "上一个周期", "下一个周期", "日期跳转", "SetMonthViewCommand", "SetWeekViewCommand", "SetDayViewCommand", "拍摄状态筛选", "拍摄类型筛选", "搜索排期", "搜索范围", "新建拍摄排期" }) Contains(xaml, value);
    }

    [TestMethod]
    public void MonthWeekAndDayContractsAreExplicit()
    {
        Contains(Text("src/RAWSelectionAssistant/ViewModels/CalendarViewModels.cs"), "offset < 42", "matches.Take(3)", "matches.Length - 3", "for (var i = 0; i < 7; i++)", "for (var hour = 0; hour < 24; hour++)", "SpansDate");
        Contains(Text("src/RAWSelectionAssistant/Views/WeekCalendarView.xaml"), "HorizontalScrollBarVisibility=\"Auto\"", "VerticalScrollBarVisibility=\"Auto\"", "全天 / 跨天");
        Contains(Text("src/RAWSelectionAssistant/Views/DayCalendarView.xaml"), "CreateAtCommand", "全天 / 跨天");
    }

    [TestMethod]
    public void SearchSupportsCurrentViewCancellationAndFiftyItemCursorPaging()
    {
        var vm = Text("src/RAWSelectionAssistant/ViewModels/CalendarViewModels.cs");
        Contains(vm, "_queryCancellation?.Cancel()", "QueryCurrentViewAsync", "SearchAllUnarchivedAsync", "PageSize", "50", "LoadMoreAsync", "ShootBookingPageCursor");
        Assert.IsFalse(vm.Contains("ToListAsync()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void EditorContainsRequiredStageBFieldsAndConflictChoices()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/ShootBookingEditorView.xaml");
        foreach (var value in new[] { "项目名称", "客户显示名称", "开始日期", "结束日期", "拍摄时区", "全天排期", "拍摄地点", "拍摄类型", "拍摄状态", "具体拍摄要求", "准备说明", "ShootRequirementsPanel", "拍摄总金额", "定金", "已收金额", "关联单一项目", "允许排期重叠", "内部备注", "返回修改", "仍然保存", "标记允许重叠并保存" }) Contains(xaml, value);
    }

    [TestMethod]
    public void ArchivePageHasViewRestoreAndPaginationButNoDeleteCommand()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/ArchivedBookingsView.xaml");
        Contains(xaml, "已归档排期", "查看", "恢复", "LoadMoreCommand");
        DoesNotContain(xaml, "DeleteAsync", "PurgeAsync", "Content=\"永久删除\"");
        var contracts = Text("src/RAWSelectionAssistant.Core/Services/Bookings/BookingContracts.cs");
        DoesNotContain(contracts, "DeleteAsync", "PurgeAsync");
    }

    [TestMethod]
    public void MainViewModelDoesNotPerformCalendarCrud()
    {
        var main = Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        Contains(main, "WorkCalendarViewModel WorkCalendarPage", "IsWorkCalendarPage");
        DoesNotContain(main, "ShootBookingDraft", "SearchAllUnarchivedAsync", "ArchiveAsync(Guid", "RestoreAsync(Guid");
    }

    [TestMethod]
    public void CalendarViewModelsUseServicesNotSqliteOrFileApis()
    {
        var sources = CalendarSources() + Text("src/RAWSelectionAssistant.Core/Services/Bookings/ShootBookingDomainServices.cs");
        Contains(sources, "IShootBookingService", "IProjectRepository");
        DoesNotContain(sources, "Microsoft.Data.Sqlite", "SqliteConnection", "File.Copy", "File.Move", "FileOperationPlan");
    }

    [TestMethod]
    public void AmountsAreMaskedAndOverpaymentIsOnlyWarned()
    {
        var sources = CalendarSources() + Text("src/RAWSelectionAssistant.Core/Services/Bookings/ShootBookingDomainServices.cs");
        Contains(sources, "••••••", "PaidExceedsTotalCode", "多收金额", "MoneyWarningText");
        DoesNotContain(sources, "PaidAmountMinor = TotalAmountMinor", "Math.Min(draft.PaidAmountMinor");
    }

    [TestMethod]
    public void StageBDoesNotAddForbiddenFeatureSurfaces()
    {
        var newSurface = CalendarSources() + string.Join("\n", Directory.GetFiles(Path.Combine(Root, "src", "RAWSelectionAssistant", "Views"), "*Calendar*.xaml").Select(File.ReadAllText));
        foreach (var forbidden in new[] { "ProjectRelationships", "IProjectRelationshipService", "BookingDocumentsPanel", "ReminderScheduler", "今日拍摄", "未来7天", "项目模板", "状态机", "本地选片", "精修回匹配", "联系表", "交付包", "文件夹监听", "在线支付", "云同步" }) DoesNotContain(newSurface, forbidden);
    }

    [TestMethod]
    public void CalendarUsesWpfDipThemesScrollAndAccessibility()
    {
        var files = Directory.GetFiles(Path.Combine(Root, "src", "RAWSelectionAssistant", "Views"), "*.xaml").Where(path => Path.GetFileName(path).Contains("Calendar", StringComparison.Ordinal) || Path.GetFileName(path).Contains("Booking", StringComparison.Ordinal) || Path.GetFileName(path).Contains("Requirements", StringComparison.Ordinal)).ToArray();
        var text = string.Join("\n", files.Select(File.ReadAllText));
        Contains(text, "DynamicResource", "AutomationProperties.Name", "VerticalScrollBarVisibility=\"Auto\"", "HorizontalScrollBarVisibility=\"Auto\"");
        DoesNotContain(text, "LayoutTransform", "ScaleTransform");
        foreach (var file in files) XDocument.Parse(File.ReadAllText(file));
    }

    [TestMethod]
    public void CalendarDeclaresRequiredKeyboardShortcuts()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/WorkCalendarView.xaml");
        foreach (var shortcut in new[] { "Key=\"N\" Modifiers=\"Control\"", "Key=\"F\" Modifiers=\"Control\"", "Key=\"T\" Modifiers=\"Control\"", "Key=\"D1\" Modifiers=\"Alt\"", "Key=\"D2\" Modifiers=\"Alt\"", "Key=\"D3\" Modifiers=\"Alt\"", "Key=\"PageUp\"", "Key=\"PageDown\"", "Key=\"Escape\"" }) Contains(xaml, shortcut);
    }

    [TestMethod]
    public void ReleaseCalendarCodeContainsNoDemoBookingInjection()
    {
        var sources = CalendarSources();
        DoesNotContain(sources, "UI_REVIEW_BUILD", "SeedDemo", "DemoBooking", "演示排期");
    }

    [TestMethod]
    public void SchemaAndMigrationFilesRemainAtStageABoundary()
    {
        var migration = Text("src/RAWSelectionAssistant.Core/Services/Database/CalendarSchemaMigration.cs");
        Contains(migration, "Version => 2");
        Assert.AreEqual(4, Count(migration, "CREATE TABLE"));
        DoesNotContain(migration, "ProjectRelationships");
    }

    private static string CalendarSources() => Text("src/RAWSelectionAssistant/ViewModels/CalendarViewModels.cs") + Text("src/RAWSelectionAssistant/ViewModels/BookingEditorViewModels.cs");
    private static string Text(string relative) => File.ReadAllText(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));
    private static void Contains(string text, params string[] values) { foreach (var value in values) StringAssert.Contains(text, value); }
    private static void DoesNotContain(string text, params string[] values) { foreach (var value in values) Assert.IsFalse(text.Contains(value, StringComparison.Ordinal), value); }
    private static int Count(string text, string value) { var result = 0; for (var index = 0; (index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length) result++; return result; }
    private static string FindRoot() { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName; throw new DirectoryNotFoundException(); }
}
