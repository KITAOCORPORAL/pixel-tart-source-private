using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version230Rc5CoreHotfix2InteractionTests
{
    [TestMethod]
    public async Task ViewDayDetailsCommand_ConsumesRequestedDateAfterActivationAndSelectsFirstBooking()
    {
        var target = new DateTime(2026, 8, 15);
        var booking = Booking(target);
        var service = new CalendarBookingServiceStub([booking]);
        using var calendar = new WorkCalendarViewModel(service, new ProjectRepositoryStub(), availabilityStore: new AvailabilityStoreStub());
        var pageRequests = 0;
        var detailRequests = 0;
        calendar.CalendarPageRequested += (_, _) => pageRequests++;
        calendar.DayDetailsNavigationRequested += (_, _) => detailRequests++;

        calendar.ViewDayDetailsCommand.Execute(target);

        Assert.AreEqual(1, pageRequests);
        Assert.AreNotEqual(target, calendar.SelectedDate);
        await calendar.ActivateAsync();
        Assert.AreEqual(target, calendar.SelectedDate);
        Assert.AreEqual(booking.Id, calendar.SelectedBookingId);
        Assert.IsTrue(calendar.IsDetailsOpen);
        Assert.AreEqual(1, detailRequests);
        Assert.AreEqual(2, service.QueryCount);

        await calendar.ActivateAsync();
        Assert.AreEqual(2, service.QueryCount, "The one-shot requested date must be consumed exactly once.");
    }

    [TestMethod]
    public async Task ViewDayDetailsCommand_EmptyDateOpensSelectedDayEmptyState()
    {
        var target = new DateTime(2026, 8, 16);
        var service = new CalendarBookingServiceStub([]);
        using var calendar = new WorkCalendarViewModel(service, new ProjectRepositoryStub(), availabilityStore: new AvailabilityStoreStub());
        var detailRequests = 0;
        calendar.DayDetailsNavigationRequested += (_, _) => detailRequests++;

        calendar.ViewDayDetailsCommand.Execute(target);
        await calendar.ActivateAsync();

        Assert.AreEqual(target, calendar.SelectedDate);
        Assert.IsNull(calendar.SelectedBookingId);
        Assert.IsFalse(calendar.IsDetailsOpen);
        Assert.AreEqual(1, detailRequests);
        Assert.HasCount(0, calendar.DaySchedule.Bookings);
    }

    [TestMethod]
    public async Task ClosedDay_PersistsAcrossNewStoreAndOpenPersistsImmediately()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "calendar-availability.json");
        var target = new DateTime(2026, 8, 20);
        var first = new JsonCalendarAvailabilityStore(path);
        await first.SetClosedAsync(target, true);

        var afterRestart = new JsonCalendarAvailabilityStore(path);
        await afterRestart.LoadAsync();
        Assert.IsTrue(afterRestart.IsClosed(target));

        await afterRestart.SetClosedAsync(target, false);
        var afterSecondRestart = new JsonCalendarAvailabilityStore(path);
        await afterSecondRestart.LoadAsync();
        Assert.IsFalse(afterSecondRestart.IsClosed(target));
    }

    [TestMethod]
    public async Task ClosedDay_CommandsRefreshVisualStateWithoutMonthSwitchOrRestart()
    {
        var target = new DateTime(2026, 8, 20);
        var availability = new AvailabilityStoreStub();
        using var calendar = new WorkCalendarViewModel(new CalendarBookingServiceStub([]), new ProjectRepositoryStub(), availabilityStore: availability);
        await calendar.OpenDayDetailsForDateAsync(target);
        var openDay = calendar.Month.Days.Single(day => day.Date == target);

        await ExecuteAsync(calendar.Month.CloseDayCommand, openDay);
        var closedDay = calendar.Month.Days.Single(day => day.Date == target);
        Assert.IsTrue(closedDay.IsClosed);
        Assert.IsFalse(calendar.Month.CloseDayCommand.CanExecute(closedDay));
        Assert.IsTrue(calendar.Month.OpenDayCommand.CanExecute(closedDay));

        await ExecuteAsync(calendar.Month.OpenDayCommand, closedDay);
        var reopenedDay = calendar.Month.Days.Single(day => day.Date == target);
        Assert.IsFalse(reopenedDay.IsClosed);
        Assert.IsTrue(calendar.Month.CloseDayCommand.CanExecute(reopenedDay));
    }

    [TestMethod]
    public void Toolbox_PinViewModelRaisesAllVisibleStateImmediately()
    {
        var item = new ToolboxItemViewModel(new ToolDefinition(
            ToolId.PhotoOrganize, "Organize", "Organize photos", "ToolIconOrganize", "PhotoGrouping",
            true, true, FeatureAvailability.Production, ToolMenuGroup.Organize, 1));
        var changes = new List<string>();
        item.PropertyChanged += (_, args) => changes.Add(args.PropertyName ?? string.Empty);

        item.SetPinned(true);

        Assert.IsTrue(item.IsPinned);
        Assert.AreEqual("ToolIconPinFilled", item.PinIconResourceKey);
        Assert.IsFalse(string.IsNullOrWhiteSpace(item.PinStateLabel));
        CollectionAssert.IsSubsetOf(new[]
        {
            nameof(ToolboxItemViewModel.IsPinned), nameof(ToolboxItemViewModel.PinIconResourceKey),
            nameof(ToolboxItemViewModel.PinStateLabel), nameof(ToolboxItemViewModel.PinToolTip)
        }, changes);
        item.SetPinned(false);
        Assert.AreEqual("ToolIconPinOutline", item.PinIconResourceKey);
        Assert.AreEqual(string.Empty, item.PinStateLabel);
    }

    [TestMethod]
    [DataRow(150)]
    [DataRow(200)]
    public Task BookingEditor_ActualBoundsHaveNoUnexpectedOverlapAtHighDpiProfiles(int dpiPercent) => RunSta(() =>
    {
        var application = EnsureApplication(out var ownsApplication);
        try
        {
            var editor = new ShootBookingEditorView { Width = 1080, Height = 800 };
            editor.Measure(new Size(1080, 800));
            editor.Arrange(new Rect(0, 0, 1080, 800));
            editor.UpdateLayout();
            var stepper = (Grid)editor.FindName("BookingEditorStepper");
            var primary = (Grid)editor.FindName("BookingEditorPrimaryFields");
            Assert.IsNotNull(stepper);
            Assert.IsNotNull(primary);
            AssertNoUnexpectedOverlap(stepper);
            AssertNoUnexpectedOverlap(primary);
            Assert.IsGreaterThanOrEqualTo(528d, stepper.ActualWidth);
            Assert.IsTrue(new[] { 150, 200 }.Contains(dpiPercent));
        }
        finally
        {
            if (ownsApplication) application.Shutdown();
        }
        return Task.CompletedTask;
    });

    [TestMethod]
    [DataRow("ViewDayDetailsCommand")]
    [DataRow("RequestDayDetailsNavigation")]
    [DataRow("_pendingNavigationDate")]
    [DataRow("CalendarPageRequested")]
    [DataRow("ActivateAsync")]
    [DataRow("OpenDayDetailsForDateAsync(requestedDate)")]
    [DataRow("_pendingNavigationDate = null")]
    public void Navigation_UsesDeferredRequestedDateContract(string token) => Contains(CalendarViewModel(), token);

    [TestMethod]
    [DataRow("ViewDayDetailsCommand")]
    [DataRow("CommandParameter=\"{Binding PlacementTarget.DataContext")]
    [DataRow("PlacementTarget.Tag.ViewDayDetailsCommand")]
    [DataRow("AncestorType=ContextMenu")]
    public void MiniCalendar_ContextMenuBindsRealDateCommand(string token) => Contains(MiniCalendar(), token);

    [TestMethod]
    public void MiniCalendar_DoesNotUseCodeBehindForContextCommands()
    {
        var code = Text("src/RAWSelectionAssistant/Views/WorkbenchCalendarPanel.xaml.cs");
        DoesNotContain(code, "ViewDayDetails_Click");
        DoesNotContain(code, "CreateBooking_Click");
    }

    [TestMethod]
    [DataRow("EditBookingCommand")]
    [DataRow("ChangeWorkflowStatusCommand")]
    [DataRow("ArchiveBookingCommand")]
    [DataRow("ScheduledStatusRequest")]
    [DataRow("ShotStatusRequest")]
    [DataRow("PendingDeliveryStatusRequest")]
    [DataRow("DeliveredStatusRequest")]
    public void FullCalendar_BookingMenuBindsUnifiedCommands(string token) => Contains(FullCalendar(), token);

    [TestMethod]
    [DataRow("Header=\"创建拍摄任务\"")]
    [DataRow("Header=\"查看当天详情\"")]
    [DataRow("Header=\"关闭本日档期\"")]
    [DataRow("Header=\"开放本日档期\"")]
    [DataRow("Header=\"本日设置\"")]
    public void FullCalendar_DateMenuContainsRequiredCommand(string token) => Contains(FullCalendar(), token);

    [TestMethod]
    [DataRow("Header=\"查看详情\"")]
    [DataRow("Header=\"编辑排期\"")]
    [DataRow("Header=\"修改状态\"")]
    [DataRow("Header=\"有拍摄\"")]
    [DataRow("Header=\"标记拍摄完成\"")]
    [DataRow("Header=\"待返图\"")]
    [DataRow("Header=\"已返图\"")]
    [DataRow("Header=\"归档\"")]
    public void FullCalendar_BookingMenuContainsRequiredCommand(string token) => Contains(FullCalendar(), token);

    [TestMethod]
    public void FullCalendar_ContextMenusDoNotUseCodeBehindClickHandlers()
    {
        var xaml = FullCalendar();
        foreach (var handler in new[] { "CreateBooking_Click", "CloseDay_Click", "OpenDay_Click", "ViewDay_Click", "DaySettings_Click" })
            DoesNotContain(xaml, handler);
    }

    [TestMethod]
    [DataRow("FullCalendarDayNumberBadge")]
    [DataRow("MinWidth=\"32\"")]
    [DataRow("Height=\"26\"")]
    [DataRow("Padding=\"6,0\"")]
    [DataRow("CalendarStatusScheduledBrush")]
    [DataRow("CalendarStatusPendingDeliveryBrush")]
    [DataRow("CalendarStatusPendingDeliveryForegroundBrush")]
    [DataRow("CalendarStatusDeliveredBrush")]
    public void FullCalendar_UsesCompleteFiveStateDayBadge(string token) => Contains(FullCalendar(), token);

    [TestMethod]
    public void FullCalendar_HasNoBottomLineAsPrimaryState()
    {
        var xaml = FullCalendar();
        DoesNotContain(xaml, "Height=\"2\" VerticalAlignment=\"Bottom\"");
        DoesNotContain(xaml, "BottomStatusLine");
    }

    [TestMethod]
    public void FullCalendar_TaskCardKeepsOnlyTitleAndOneWayTimeSummary()
    {
        var xaml = FullCalendar();
        Contains(xaml, "{Binding MonthTitle}");
        Contains(xaml, "{Binding TimeText, Mode=OneWay}");
        DoesNotContain(xaml, "{Binding WorkflowStatusText, Mode=OneWay}");
        DoesNotContain(xaml, "Content=\"编辑\"");
        Contains(xaml, "Header=\"编辑排期\"");
    }

    [TestMethod]
    [DataRow("CalendarDayVisualState")]
    [DataRow("CalendarDayVisualStateResolver")]
    [DataRow("DateTime Date")]
    [DataRow("CalendarWorkflowStatus PrimaryWorkflowStatus")]
    [DataRow("int BookingCount")]
    [DataRow("bool IsClosed")]
    [DataRow("bool IsToday")]
    [DataRow("bool IsSelected")]
    public void Calendar_UsesSingleVisualStateResolver(string token) => Contains(CalendarViewModel(), token);

    [TestMethod]
    [DataRow(ShootBookingStatus.Confirmed, CalendarWorkflowStatus.Scheduled)]
    [DataRow(ShootBookingStatus.Completed, CalendarWorkflowStatus.PendingDelivery)]
    [DataRow(ShootBookingStatus.AwaitingDelivery, CalendarWorkflowStatus.PendingDelivery)]
    [DataRow(ShootBookingStatus.Delivered, CalendarWorkflowStatus.Delivered)]
    public void CalendarVisualState_ResolvesAllWorkflowColors(ShootBookingStatus status, CalendarWorkflowStatus expected)
    {
        var date = new DateTime(2026, 8, 15);
        var start = new DateTimeOffset(date.AddHours(9), TimeSpan.FromHours(8)).ToUniversalTime();
        var summary = new ShootBookingSummary(Guid.NewGuid(), null, "Visual", "Client", start, start.AddHours(2), "China Standard Time", false, status, "Studio", "Portrait", false, false);
        var state = CalendarDayVisualStateResolver.Resolve(date, [new CalendarBookingItemViewModel(summary)], false, false, true);
        Assert.AreEqual(expected, state.PrimaryWorkflowStatus);
        Assert.IsTrue(state.HasBookings);
        Assert.IsTrue(state.IsSelected);
    }

    [TestMethod]
    public void CalendarVisualState_ClosedDayIsIndependentFromBookingWorkflow()
    {
        var date = new DateTime(2026, 8, 20);
        var start = new DateTimeOffset(date.AddHours(9), TimeSpan.FromHours(8)).ToUniversalTime();
        var summary = new ShootBookingSummary(Guid.NewGuid(), null, "Visual", "Client", start, start.AddHours(2), "China Standard Time", false, ShootBookingStatus.Confirmed, "Studio", "Portrait", false, false);
        var state = CalendarDayVisualStateResolver.Resolve(date, [new CalendarBookingItemViewModel(summary)], true, false, false);
        Assert.IsTrue(state.IsClosed);
        Assert.AreEqual(CalendarWorkflowStatus.Scheduled, state.PrimaryWorkflowStatus);
        Assert.AreEqual(1, state.BookingCount);
        Assert.IsFalse(CalendarDayVisualStateResolver.Resolve(date, [], false, false, false).IsClosed);
    }

    [TestMethod]
    [DataRow("M5,10 L5,7 C5,3 11,3 11,7 L11,10")]
    [DataRow("当天已关闭接单")]
    [DataRow("Binding IsClosed")]
    [DataRow("SurfaceSecondaryBrush")]
    public void ClosedDay_HasLockLabelAndDarkenedCell(string token) => Contains(MiniCalendar() + FullCalendar(), token);

    [TestMethod]
    [DataRow("parameter is MonthDayViewModel { IsClosed: false }")]
    [DataRow("parameter is MonthDayViewModel { IsClosed: true }")]
    [DataRow("await _availabilityStore.SetClosedAsync")]
    [DataRow("Month.Configure(SelectedDate")]
    public void ClosedDay_CommandsHaveCanExecutePersistenceAndImmediateRefresh(string token) => Contains(CalendarViewModel(), token);

    [TestMethod]
    [DataRow("Details.EditRequested += (_, bookingId) => EditBookingCommand.Execute(bookingId)")]
    [DataRow("Header=\"编辑排期\" Command=")]
    [DataRow("EditBookingCommand = new AsyncRelayCommand")]
    [DataRow("CreateIfMissing = !_bookingId.HasValue")]
    [DataRow("Id = _stableBookingId")]
    public void EditBooking_AllEntriesUseOneStableIdImplementation(string token) =>
        Contains(CalendarViewModel() + FullCalendar() + EditorViewModel(), token);

    [TestMethod]
    [DataRow("savedDate")]
    [DataRow("savedLastDate")]
    [DataRow("_selectedDate = savedDate")]
    [DataRow("await RefreshAsync()")]
    [DataRow("await OpenBookingAsync(saved.Id)")]
    public void EditBooking_SaveMaintainsCorrectCalendarPosition(string token) => Contains(CalendarViewModel(), token);

    [TestMethod]
    [DataRow("BookingEditorStepper")]
    [DataRow("MinWidth=\"132\"")]
    [DataRow("MaxWidth=\"980\"")]
    [DataRow("Margin=\"24,12,24,16\"")]
    [DataRow("BookingEditorPrimaryFields")]
    [DataRow("Margin=\"20,0,8,0\"")]
    public void BookingEditor_UsesNonOverlappingResponsiveGeometry(string token) => Contains(Editor(), token);

    [TestMethod]
    public void BookingEditor_HasNoNegativeMarginsOrTransforms()
    {
        var xaml = Editor();
        DoesNotContain(xaml, "Margin=\"-");
        DoesNotContain(xaml, "ScaleTransform");
        DoesNotContain(xaml, "TranslateTransform");
    }

    [TestMethod]
    [DataRow("ToolIconPinOutline")]
    [DataRow("ToolIconPinFilled")]
    [DataRow("11.5 13,11.5 13,20 12,22 11,20")]
    [DataRow("Width=\"32\" Height=\"32\"")]
    [DataRow("Width=\"20\" Height=\"20\"")]
    [DataRow("#FFFFC44D")]
    [DataRow("Text=\"已固定\"")]
    public void Toolbox_PinHasRecognizableVectorAndDistinctPinnedState(string token) => Contains(Toolbox(), token);

    [TestMethod]
    public void Toolbox_PinDoesNotUseUnicodeGlyph()
    {
        var source = Toolbox();
        DoesNotContain(source, "Segoe UI Symbol");
        DoesNotContain(source, "📌");
    }

    private static string MiniCalendar() => Text("src/RAWSelectionAssistant/Views/WorkbenchCalendarPanel.xaml");
    private static string FullCalendar() => Text("src/RAWSelectionAssistant/Views/MonthCalendarView.xaml");
    private static string CalendarViewModel() => Text("src/RAWSelectionAssistant/ViewModels/CalendarViewModels.cs");
    private static string Editor() => Text("src/RAWSelectionAssistant/Views/ShootBookingEditorView.xaml");
    private static string EditorViewModel() => Text("src/RAWSelectionAssistant/ViewModels/BookingEditorViewModels.cs");
    private static string Toolbox() => Text("src/RAWSelectionAssistant/MainWindow.xaml") + Text("src/RAWSelectionAssistant/Resources/DesignSystem/Icons.Tools.xaml");

    private static ShootBooking Booking(DateTime date)
    {
        var start = new DateTimeOffset(date.AddHours(9), TimeSpan.FromHours(8)).ToUniversalTime();
        return new ShootBooking
        {
            Id = Guid.NewGuid(), Title = "CoreHotfix2 navigation", ClientDisplayName = "Isolated",
            StartAtUtc = start, EndAtUtc = start.AddHours(2), TimeZoneId = "China Standard Time",
            Status = ShootBookingStatus.Confirmed, ShootingType = "Portrait"
        };
    }

    private static async Task ExecuteAsync(ICommand command, object parameter)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = false;
        void Changed(object? _, EventArgs __)
        {
            if (!started) { started = true; return; }
            if (command.CanExecute(parameter)) completion.TrySetResult();
        }
        command.CanExecuteChanged += Changed;
        try
        {
            command.Execute(parameter);
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally { command.CanExecuteChanged -= Changed; }
    }

    private static void AssertNoUnexpectedOverlap(Grid grid)
    {
        var elements = grid.Children.OfType<FrameworkElement>()
            .Where(element => element.Visibility == Visibility.Visible && element.ActualWidth > 0 && element.ActualHeight > 0)
            .Select(element => (Element: element, Bounds: new Rect(element.TransformToAncestor(grid).Transform(new Point()), element.RenderSize)))
            .ToArray();
        for (var left = 0; left < elements.Length; left++)
        for (var right = left + 1; right < elements.Length; right++)
        {
            var intersection = Rect.Intersect(elements[left].Bounds, elements[right].Bounds);
            Assert.IsTrue(intersection.IsEmpty || intersection.Width <= .1 || intersection.Height <= .1,
                $"Unexpected overlap at {grid.Name}: {elements[left].Element.GetType().Name} and {elements[right].Element.GetType().Name}, {intersection}.");
        }
    }

    private static App EnsureApplication(out bool ownsApplication)
    {
        ownsApplication = Application.Current is null;
        if (Application.Current is App current) return current;
        var application = new App();
        application.InitializeComponent();
        return application;
    }

    private static Task RunSta(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(async () =>
        {
            try { await action(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private sealed class AvailabilityStoreStub : ICalendarAvailabilityStore
    {
        private readonly HashSet<DateTime> _closed = [];
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public bool IsClosed(DateTime date) => _closed.Contains(date.Date);
        public Task SetClosedAsync(DateTime date, bool isClosed, CancellationToken cancellationToken = default)
        {
            if (isClosed) _closed.Add(date.Date); else _closed.Remove(date.Date);
            return Task.CompletedTask;
        }
    }

    private sealed class CalendarBookingServiceStub(IReadOnlyList<ShootBooking> bookings) : IShootBookingService
    {
        public int QueryCount { get; private set; }
        public Task<BookingSaveResult> SaveAsync(ShootBookingDraft draft, BookingConflictResolution conflictResolution = BookingConflictResolution.None, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ShootBooking?> GetAsync(Guid id, bool includeArchived = false, CancellationToken cancellationToken = default) => Task.FromResult(bookings.FirstOrDefault(item => item.Id == id));
        public Task<IReadOnlyList<ShootRequirementItem>> GetRequirementsAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShootRequirementItem>>([]);
        public Task<IReadOnlyList<ShootBookingSummary>> QueryCurrentViewAsync(ShootBookingQuery query, CancellationToken cancellationToken = default)
        {
            QueryCount++;
            return Task.FromResult<IReadOnlyList<ShootBookingSummary>>(bookings.Select(item => new ShootBookingSummary(
                item.Id, item.ProjectId, item.Title, item.ClientDisplayName, item.StartAtUtc, item.EndAtUtc, item.TimeZoneId,
                item.IsAllDay, item.Status, item.Location, item.ShootingType, item.AllowOverlap, item.IsArchived, item.CreatedAtUtc)).ToArray());
        }
        public Task<ShootBookingPage> SearchAllUnarchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ShootBookingPage([], null));
        public Task<ShootBookingPage> SearchArchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ShootBookingPage([], null));
        public Task<bool> CompleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> SetStatusAsync(Guid id, ShootBookingStatus status, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> RestoreAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class ProjectRepositoryStub : IProjectRepository
    {
        public Task UpsertAsync(PhotoProjectRecord project, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PhotoProjectRecord>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PhotoProjectRecord>>([]);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PixelTart.CoreHotfix2", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }
        public string Path { get; }
        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }

    private static void Contains(string source, string expected) => StringAssert.Contains(source, expected);
    private static void DoesNotContain(string source, string forbidden) => Assert.IsFalse(source.Contains(forbidden, StringComparison.Ordinal), $"Found forbidden token: {forbidden}");
    private static string Text(string relativePath) => File.ReadAllText(Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
