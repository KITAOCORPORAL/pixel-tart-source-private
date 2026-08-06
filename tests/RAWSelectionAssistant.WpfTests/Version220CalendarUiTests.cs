using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version220CalendarUiTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 8, 4, 9, 0, 0, TimeSpan.Zero);

    [TestMethod] public void Calendar_DefaultsToMonthAndCurrentViewSearch()
    {
        var vm = Calendar();
        Assert.AreEqual(CalendarViewMode.Month, vm.ViewMode);
        Assert.IsTrue(vm.IsMonthView);
        Assert.AreEqual(BookingSearchScope.CurrentView, vm.SelectedSearchScope.Value);
    }

    [TestMethod] public void MonthView_AlwaysBuildsFortyTwoDays()
    {
        var month = Month();
        month.Configure(new DateTime(2026, 8, 1), [], new DateTime(2026, 8, 4));
        Assert.HasCount(42, month.Days);
        Assert.AreEqual(DayOfWeek.Monday, month.Days[0].Date.DayOfWeek);
    }

    [TestMethod] public void MonthView_ShowsThreeBookingsAndOverflowCount()
    {
        var month = Month();
        month.Configure(new DateTime(2026, 8, 1), Enumerable.Range(0, 5).Select(index => Summary(Guid.NewGuid(), BaseTime.AddMinutes(index * 5), BaseTime.AddHours(1), $"项目{index}")).ToArray(), new DateTime(2026, 8, 4));
        var day = month.Days.Single(item => item.Date == new DateTime(2026, 8, 4));
        Assert.HasCount(3, day.VisibleBookings);
        Assert.AreEqual(2, day.OverflowCount);
        Assert.AreEqual("另有 2 项", day.OverflowText);
    }

    [TestMethod] public void WeekView_BuildsSevenColumns()
    {
        var week = new WeekCalendarViewModel(_ => { }, _ => Task.CompletedTask, _ => { });
        week.Configure(new DateTime(2026, 8, 3), [], new DateTime(2026, 8, 4));
        Assert.HasCount(7, week.Days);
    }

    [TestMethod] public void DayView_BuildsTwentyFourClickableSlots()
    {
        var day = new DayCalendarViewModel(_ => Task.CompletedTask, _ => { });
        day.Configure(new DateTime(2026, 8, 4), []);
        Assert.HasCount(24, day.TimeSlots);
        Assert.AreEqual("00:00", day.TimeSlots[0].Label);
        Assert.AreEqual("23:00", day.TimeSlots[^1].Label);
    }

    [TestMethod] public void CrossDayBooking_AppearsOnEachCoveredDate()
    {
        var booking = Summary(Guid.NewGuid(), BaseTime.AddHours(13), BaseTime.AddDays(1).AddHours(2), "跨天");
        Assert.IsTrue(CalendarBookingItemViewModel.SpansDate(booking, new DateTime(2026, 8, 4)));
        Assert.IsTrue(CalendarBookingItemViewModel.SpansDate(booking, new DateTime(2026, 8, 5)));
        Assert.IsFalse(CalendarBookingItemViewModel.SpansDate(booking, new DateTime(2026, 8, 6)));
    }

    [TestMethod] public async Task CurrentView_SearchAndFiltersGoThroughService()
    {
        var service = new StubBookingService();
        var vm = Calendar(service);
        await vm.InitializeAsync();
        vm.SearchText = "本地关键词";
        vm.SelectedStatus = vm.StatusOptions.Single(option => option.Value == ShootBookingStatus.Confirmed);
        vm.SelectedType = vm.TypeOptions.Single(option => option.Value == "Commercial");
        await vm.RefreshAsync();
        Assert.AreEqual("本地关键词", service.LastCurrentQuery!.Keyword);
        Assert.AreEqual(ShootBookingStatus.Confirmed, service.LastCurrentQuery.Status);
        Assert.AreEqual("Commercial", service.LastCurrentQuery.ShootingType);
    }

    [TestMethod] public async Task GlobalSearch_UsesFiftyItemCursorPage()
    {
        var service = new StubBookingService { GlobalPage = new([], new(BaseTime, Guid.NewGuid())) };
        var vm = Calendar(service);
        vm.SelectedSearchScope = vm.SearchScopeOptions.Single(option => option.Value == BookingSearchScope.AllUnarchived);
        await vm.InitializeAsync();
        Assert.AreEqual(50, service.LastSearchRequest!.PageSize);
        Assert.IsTrue(vm.HasMoreGlobalResults);
    }

    [TestMethod] public async Task SearchChange_CancelsOlderCurrentViewQuery()
    {
        var service = new StubBookingService
        {
            CurrentViewHandler = async (query, token) =>
            {
                if (query.Keyword == "旧查询") await Task.Delay(500, token);
                return [Summary(Guid.NewGuid(), BaseTime, BaseTime.AddHours(1), query.Keyword ?? "初始")];
            }
        };
        var vm = Calendar(service);
        await vm.InitializeAsync();
        vm.SearchText = "旧查询";
        await Task.Delay(30);
        vm.SearchText = "新查询";
        await WaitUntilAsync(() => vm.Month.AllItems.Count == 1 && vm.Month.AllItems[0].Title == "新查询");
        Assert.AreEqual("新查询", vm.Month.AllItems.Single().Title);
    }

    [TestMethod] public async Task Details_MasksAmountsUntilExplicitReveal()
    {
        var service = new StubBookingService();
        var booking = Booking(total: 10_000, paid: 4_000);
        service.Bookings[booking.Id] = booking;
        var vm = new ShootBookingDetailsViewModel(service);
        await vm.LoadAsync(booking.Id);
        Assert.AreEqual("••••••", vm.TotalAmountText);
        vm.ToggleAmountsCommand.Execute(null);
        Assert.AreEqual("CNY 100.00", vm.TotalAmountText);
        Assert.AreEqual("待收金额", vm.BalanceLabel);
    }

    [TestMethod] public async Task Details_OverpaymentUsesWarningAndOverpaidLabel()
    {
        var service = new StubBookingService();
        var booking = Booking(total: 10_000, paid: 12_500);
        service.Bookings[booking.Id] = booking;
        var vm = new ShootBookingDetailsViewModel(service);
        await vm.LoadAsync(booking.Id);
        vm.ToggleAmountsCommand.Execute(null);
        Assert.IsTrue(vm.HasMoneyWarning);
        Assert.AreEqual("多收金额", vm.BalanceLabel);
        Assert.AreEqual("CNY 25.00", vm.BalanceText);
    }

    [TestMethod] public async Task Details_LoadRefreshesEditAndArchiveCommandState()
    {
        var service = new StubBookingService();
        var booking = Booking();
        service.Bookings[booking.Id] = booking;
        var vm = new ShootBookingDetailsViewModel(service);
        Assert.IsFalse(vm.EditCommand.CanExecute(null));
        Assert.IsFalse(vm.ArchiveCommand.CanExecute(null));
        var editChanged = 0;
        var archiveChanged = 0;
        vm.EditCommand.CanExecuteChanged += (_, _) => editChanged++;
        vm.ArchiveCommand.CanExecuteChanged += (_, _) => archiveChanged++;

        await vm.LoadAsync(booking.Id);

        Assert.IsTrue(vm.EditCommand.CanExecute(null));
        Assert.IsTrue(vm.ArchiveCommand.CanExecute(null));
        Assert.IsGreaterThan(0, editChanged);
        Assert.IsGreaterThan(0, archiveChanged);
    }

    [TestMethod] public void Requirements_SupportAddRemoveAndReorder()
    {
        var vm = new ShootRequirementsViewModel();
        vm.AddCommand.Execute(null); vm.AddCommand.Execute(null);
        vm.Items[0].ItemText = "电池"; vm.Items[1].ItemText = "灯架";
        vm.MoveUpCommand.Execute(vm.Items[1]);
        Assert.AreEqual("灯架", vm.Items[0].ItemText);
        vm.RemoveCommand.Execute(vm.Items[1]);
        Assert.HasCount(1, vm.Items);
    }

    [TestMethod] public void Requirements_ReportsCompletionRate()
    {
        var vm = new ShootRequirementsViewModel();
        vm.Load([new() { ItemText = "电池", IsCompleted = true }, new() { ItemText = "灯架" }]);
        StringAssert.Contains(vm.CompletionText, "50%");
    }

    [TestMethod] public async Task Editor_DefaultsToTentativeAndDisallowsOverlap()
    {
        var vm = new ShootBookingEditorViewModel(new StubBookingService(), new StubProjectRepository(), suggestedStart: new DateTime(2026, 8, 4, 13, 0, 0));
        await vm.InitializeAsync();
        Assert.AreEqual(ShootBookingStatus.Tentative, vm.SelectedStatus!.Value);
        Assert.IsFalse(vm.AllowOverlap);
        Assert.AreEqual("13:00", vm.StartTimeText);
        Assert.AreEqual("14:00", vm.EndTimeText);
    }

    [TestMethod] public async Task Editor_AllowsOverpaymentAndShowsWarningAfterSave()
    {
        var service = new StubBookingService
        {
            SaveHandler = draft => Task.FromResult(new BookingSaveResult(BookingSaveStatus.Saved, Booking(total: draft.TotalAmountMinor, paid: draft.PaidAmountMinor), BookingMoneyCalculator.Calculate(draft.TotalAmountMinor, draft.DepositAmountMinor, draft.PaidAmountMinor), [], []))
        };
        var vm = new ShootBookingEditorViewModel(service, new StubProjectRepository(), suggestedStart: new DateTime(2026, 8, 4, 9, 0, 0));
        await vm.InitializeAsync();
        vm.Title = "商业拍摄"; vm.TotalAmountText = "100"; vm.PaidAmountText = "125";
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.Saved += (_, _) => saved.SetResult();
        vm.SaveCommand.Execute(null);
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.IsTrue(vm.HasMoneyWarning);
        Assert.AreEqual("多收金额", vm.BalanceLabel);
    }

    [TestMethod] public async Task ConflictInteraction_ExposesStatusAndThreeDecisions()
    {
        var conflict = new BookingConflict(Guid.NewGuid(), "重叠排期", "客户", BaseTime, BaseTime.AddHours(2), "影棚", ShootBookingStatus.Confirmed, TimeSpan.FromHours(1), false, true);
        var service = new StubBookingService { SaveHandler = draft => Task.FromResult(new BookingSaveResult(BookingSaveStatus.NeedsAttention, null, BookingMoneyCalculator.Calculate(null, null, null), [conflict], [])) };
        var vm = new ShootBookingEditorViewModel(service, new StubProjectRepository(), suggestedStart: new DateTime(2026, 8, 4, 9, 0, 0));
        await vm.InitializeAsync(); vm.Title = "新排期";
        vm.SaveCommand.Execute(null);
        await WaitUntilAsync(() => vm.IsConflictVisible);
        Assert.AreEqual("已确认", vm.Conflicts.Single().StatusText);
        Assert.IsNotNull(vm.ReturnToEditCommand); Assert.IsNotNull(vm.SaveAnywayCommand); Assert.IsNotNull(vm.MarkOverlapAndSaveCommand);
    }

    [TestMethod] public async Task Details_ArchiveUsesArchiveOnlyService()
    {
        var service = new StubBookingService();
        var booking = Booking(); service.Bookings[booking.Id] = booking;
        var vm = new ShootBookingDetailsViewModel(service); await vm.LoadAsync(booking.Id);
        var archived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.Archived += (_, _) => archived.SetResult();
        vm.ArchiveCommand.Execute(null);
        await archived.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual(1, service.ArchiveCalls);
        Assert.IsTrue(vm.IsArchived);
    }

    [TestMethod] public async Task ArchivedPage_RestoresAndReloads()
    {
        var service = new StubBookingService { ArchivedPage = new([Summary(Guid.NewGuid(), BaseTime, BaseTime.AddHours(1), "归档")], null) };
        var vm = new ArchivedBookingsViewModel(service); await vm.RefreshAsync();
        var restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.Restored += (_, _) => restored.SetResult();
        vm.RestoreCommand.Execute(vm.Items[0]);
        await restored.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual(1, service.RestoreCalls);
        Assert.AreEqual(50, service.LastArchivedRequest!.PageSize);
    }

    [TestMethod] public void CalendarSource_HasNoDirectSqliteOrFileOperations()
    {
        var source = Text("src/RAWSelectionAssistant/ViewModels/CalendarViewModels.cs") + Text("src/RAWSelectionAssistant/ViewModels/BookingEditorViewModels.cs");
        Assert.IsFalse(source.Contains("Microsoft.Data.Sqlite", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("FileOperationPlan", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("File.Copy", StringComparison.Ordinal));
    }

    [TestMethod] public void MainViewModel_OnlyHostsCalendarAndDoesNotOwnBookingCollection()
    {
        var source = Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        StringAssert.Contains(source, "WorkCalendarViewModel WorkCalendarPage");
        Assert.IsFalse(source.Contains("ObservableCollection<ShootBooking", StringComparison.Ordinal));
    }

    [TestMethod] public void StageB_HasNoPermanentDeleteEntry()
    {
        var text = string.Join("\n", Directory.GetFiles(Path.Combine(Root(), "src", "RAWSelectionAssistant", "Views"), "*Calendar*.xaml").Select(File.ReadAllText)) + Text("src/RAWSelectionAssistant/Views/ShootBookingDetailsView.xaml") + Text("src/RAWSelectionAssistant/Views/ArchivedBookingsView.xaml");
        Assert.IsFalse(text.Contains("Content=\"永久删除\"", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("DeleteAsync", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("PurgeAsync", StringComparison.Ordinal));
    }

    [TestMethod] public void CalendarXaml_DeclaresKeyboardAndAccessibilityContracts()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/WorkCalendarView.xaml");
        foreach (var value in new[] { "Key=\"N\" Modifiers=\"Control\"", "Key=\"F\" Modifiers=\"Control\"", "Key=\"T\" Modifiers=\"Control\"", "Key=\"D1\" Modifiers=\"Alt\"", "Key=\"PageUp\"", "Key=\"Escape\"", "AutomationProperties.Name", "AutomationProperties.HelpText" }) StringAssert.Contains(xaml, value);
    }

    [TestMethod] public void CalendarXaml_UsesDynamicThemeResourcesAndScrollableSurfaces()
    {
        var text = string.Join("\n", new[] { "WorkCalendarView.xaml", "MonthCalendarView.xaml", "WeekCalendarView.xaml", "DayCalendarView.xaml", "ShootBookingDetailsView.xaml", "ShootBookingEditorView.xaml" }.Select(file => Text($"src/RAWSelectionAssistant/Views/{file}")));
        StringAssert.Contains(text, "DynamicResource");
        StringAssert.Contains(text, "HorizontalScrollBarVisibility=\"Auto\"");
        StringAssert.Contains(text, "VerticalScrollBarVisibility=\"Auto\"");
        Assert.IsFalse(text.Contains("#FFFFFF", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod] public Task CalendarControls_MeasureInIsolatedLogicalDpiViewports() => RunSta(() =>
    {
        var app = new App();
        app.InitializeComponent();
        try
        {
            foreach (var viewport in new[] { new Size(1024, 640), new Size(854, 534), new Size(720, 480) })
            {
                var view = new WorkCalendarView { DataContext = Calendar(), Width = viewport.Width, Height = viewport.Height };
                view.Measure(viewport); view.Arrange(new Rect(viewport)); view.UpdateLayout();
                Assert.IsFalse(double.IsNaN(view.ActualWidth)); Assert.IsFalse(double.IsNaN(view.ActualHeight));
                Assert.IsLessThanOrEqualTo(viewport.Width, view.ActualWidth); Assert.IsLessThanOrEqualTo(viewport.Height, view.ActualHeight);

                var documentPanel = new BookingDocumentsPanel { Width = viewport.Width, Height = viewport.Height };
                documentPanel.Measure(viewport); documentPanel.Arrange(new Rect(viewport)); documentPanel.UpdateLayout();
                Assert.IsFalse(double.IsNaN(documentPanel.ActualWidth)); Assert.IsFalse(double.IsNaN(documentPanel.ActualHeight));
                Assert.IsLessThanOrEqualTo(viewport.Width, documentPanel.ActualWidth); Assert.IsLessThanOrEqualTo(viewport.Height, documentPanel.ActualHeight);

                foreach (var stageDControl in new FrameworkElement[] { new BookingRemindersPanel(), new BookingWeatherPanel(), new WorkbenchCalendarSummaryView(), new ReminderNotificationHost(), new TetherCaptureView() })
                {
                    stageDControl.Width = viewport.Width; stageDControl.Height = viewport.Height;
                    stageDControl.Measure(viewport); stageDControl.Arrange(new Rect(viewport)); stageDControl.UpdateLayout();
                    Assert.IsFalse(double.IsNaN(stageDControl.ActualWidth)); Assert.IsFalse(double.IsNaN(stageDControl.ActualHeight));
                    Assert.IsLessThanOrEqualTo(viewport.Width, stageDControl.ActualWidth); Assert.IsLessThanOrEqualTo(viewport.Height, stageDControl.ActualHeight);
                }
            }
        }
        finally { app.Shutdown(); }
        return Task.CompletedTask;
    });

    private static WorkCalendarViewModel Calendar(StubBookingService? service = null) => new(service ?? new(), new StubProjectRepository());
    private static MonthCalendarViewModel Month() => new(_ => { }, _ => Task.CompletedTask, _ => { });
    private static ShootBookingSummary Summary(Guid id, DateTimeOffset start, DateTimeOffset end, string title) => new(id, null, title, "客户", start, end, TimeZoneInfo.Utc.Id, false, ShootBookingStatus.Tentative, "影棚", "Portrait", false, false);
    private static ShootBooking Booking(long? total = null, long? paid = null) => new() { Id = Guid.NewGuid(), Title = "排期", ClientDisplayName = "客户", StartAtUtc = BaseTime, EndAtUtc = BaseTime.AddHours(1), TimeZoneId = TimeZoneInfo.Utc.Id, ShootingType = "Portrait", TotalAmountMinor = total, PaidAmountMinor = paid };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(20);
        Assert.IsTrue(condition());
    }

    private static Task RunSta(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(async () => { try { await action(); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }

    private static string Text(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }

    private sealed class StubProjectRepository : IProjectRepository
    {
        public Task UpsertAsync(PhotoProjectRecord project, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PhotoProjectRecord>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PhotoProjectRecord>>([]);
    }

    private sealed class StubBookingService : IShootBookingService
    {
        public Dictionary<Guid, ShootBooking> Bookings { get; } = [];
        public ShootBookingQuery? LastCurrentQuery { get; private set; }
        public ShootBookingSearchRequest? LastSearchRequest { get; private set; }
        public ShootBookingSearchRequest? LastArchivedRequest { get; private set; }
        public ShootBookingPage GlobalPage { get; set; } = new([], null);
        public ShootBookingPage ArchivedPage { get; set; } = new([], null);
        public Func<ShootBookingDraft, Task<BookingSaveResult>>? SaveHandler { get; set; }
        public Func<ShootBookingQuery, CancellationToken, Task<IReadOnlyList<ShootBookingSummary>>>? CurrentViewHandler { get; set; }
        public int ArchiveCalls { get; private set; }
        public int RestoreCalls { get; private set; }
        public Task<BookingSaveResult> SaveAsync(ShootBookingDraft draft, BookingConflictResolution conflictResolution = BookingConflictResolution.None, CancellationToken cancellationToken = default) => SaveHandler?.Invoke(draft) ?? Task.FromResult(new BookingSaveResult(BookingSaveStatus.Saved, Booking(total: draft.TotalAmountMinor, paid: draft.PaidAmountMinor), BookingMoneyCalculator.Calculate(draft.TotalAmountMinor, draft.DepositAmountMinor, draft.PaidAmountMinor), [], []));
        public Task<ShootBooking?> GetAsync(Guid id, bool includeArchived = false, CancellationToken cancellationToken = default) => Task.FromResult(Bookings.GetValueOrDefault(id));
        public Task<IReadOnlyList<ShootRequirementItem>> GetRequirementsAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShootRequirementItem>>([]);
        public Task<IReadOnlyList<ShootBookingSummary>> QueryCurrentViewAsync(ShootBookingQuery query, CancellationToken cancellationToken = default) { LastCurrentQuery = query; return CurrentViewHandler?.Invoke(query, cancellationToken) ?? Task.FromResult<IReadOnlyList<ShootBookingSummary>>([]); }
        public Task<ShootBookingPage> SearchAllUnarchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) { LastSearchRequest = request; return Task.FromResult(GlobalPage); }
        public Task<ShootBookingPage> SearchArchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) { LastArchivedRequest = request; return Task.FromResult(ArchivedPage); }
        public Task<bool> CompleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken = default) { ArchiveCalls++; return Task.FromResult(true); }
        public Task<bool> RestoreAsync(Guid id, CancellationToken cancellationToken = default) { RestoreCalls++; return Task.FromResult(true); }
    }
}
