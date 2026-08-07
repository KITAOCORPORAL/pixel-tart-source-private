using System.Xml.Linq;
using System.IO;
using System.Net.Http;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version230Rc4RuntimeWorkflowUiTests
{
    [TestMethod]
    public async Task BookingEditor_DatabaseFailureRetainsInputsStepAndStableBookingId()
    {
        var service = new FailingBookingService();
        var editor = new ShootBookingEditorViewModel(service, new ProjectRepositoryStub(), suggestedStart: new DateTime(2026, 9, 8, 9, 0, 0));
        await editor.InitializeAsync();
        editor.Title = "保留的排期";
        editor.ClientDisplayName = "客户代号";
        editor.Location = "影棚A";
        editor.Notes = "保留备注";
        editor.CurrentStep = 4;
        editor.Contacts.Add(new() { DisplayName = "联系人", Phone = "13800000000", IsPrimary = true });
        editor.Staff.Add(new() { DisplayName = "摄影师", SelectedRole = editor.StaffRoleOptions[0], Phone = "13900000000" });
        var stableId = editor.StableBookingId;
        var closed = false;
        editor.CloseRequested += (_, _) => closed = true;

        await ExecuteAndWaitForFailureAsync(editor);

        Assert.AreEqual(BookingEditorSaveStatus.DatabaseFailed, editor.SaveStatus);
        Assert.AreEqual(4, editor.CurrentStep);
        Assert.AreEqual("保留的排期", editor.Title);
        Assert.AreEqual("联系人", editor.Contacts.Single().DisplayName);
        Assert.AreEqual("摄影师", editor.Staff.Single().DisplayName);
        Assert.AreEqual(stableId, service.Drafts.Single().Id);
        Assert.IsTrue(service.Drafts.Single().CreateIfMissing);
        Assert.IsTrue(service.Drafts.Single().ReplacePeople);
        Assert.IsFalse(closed);
    }

    [TestMethod]
    public async Task BookingEditor_RetryAfterFailureUsesSameBookingId()
    {
        var service = new FailingBookingService();
        var editor = new ShootBookingEditorViewModel(service, new ProjectRepositoryStub(), suggestedStart: new DateTime(2026, 9, 8, 9, 0, 0)) { Title = "重试排期" };
        await editor.InitializeAsync();

        await ExecuteAndWaitForFailureAsync(editor);
        await ExecuteAndWaitForFailureAsync(editor);

        Assert.HasCount(2, service.Drafts);
        Assert.AreEqual(service.Drafts[0].Id, service.Drafts[1].Id);
        Assert.AreEqual(editor.EditorSessionId, service.Drafts[0].EditorSessionId);
        Assert.AreEqual(editor.EditorSessionId, service.Drafts[1].EditorSessionId);
    }

    [TestMethod]
    public async Task BookingEditor_PartialDraftPersistsWithSafeDefaultsAndSkipsBlankPeople()
    {
        var service = new SuccessfulBookingService();
        var editor = new ShootBookingEditorViewModel(service, new ProjectRepositoryStub(), suggestedStart: new DateTime(2026, 9, 8, 9, 0, 0));
        await editor.InitializeAsync();
        editor.Title = string.Empty;
        editor.StartTimeText = "尚未确定";
        editor.EndTimeText = "尚未确定";
        editor.Contacts.Add(new() { DisplayName = string.Empty, IsPrimary = true });
        editor.Staff.Add(new() { DisplayName = string.Empty, SelectedRole = editor.StaffRoleOptions[0] });

        await ExecuteAndWaitAsync(editor, editor.SaveDraftCommand);

        var draft = service.Drafts.Single();
        Assert.AreEqual("未命名草稿", draft.Title);
        Assert.AreEqual(ShootBookingStatus.Draft, draft.Status);
        Assert.IsTrue(draft.EndAt > draft.StartAt);
        Assert.HasCount(0, draft.Contacts);
        Assert.HasCount(0, draft.Staff);
        Assert.AreEqual(BookingEditorSaveStatus.DraftSaved, editor.SaveStatus);
    }

    [TestMethod]
    public void MultidayCalendarItem_UsesOneBookingIdAndClearContinuationLabels()
    {
        var start = new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero);
        var booking = new ShootBookingSummary(Guid.NewGuid(), null, "跨日拍摄", "客户", start, start.AddDays(2).AddHours(3), "UTC", false, ShootBookingStatus.Confirmed, null, "Portrait", false, false);
        var first = new CalendarBookingItemViewModel(booking, displayDate: new DateTime(2026, 9, 8));
        var middle = new CalendarBookingItemViewModel(booking, displayDate: new DateTime(2026, 9, 9));
        var last = new CalendarBookingItemViewModel(booking, displayDate: new DateTime(2026, 9, 10));

        Assert.AreEqual(first.Id, middle.Id);
        Assert.AreEqual(middle.Id, last.Id);
        StringAssert.Contains(first.MonthTitle, "共3天");
        StringAssert.Contains(middle.MonthTitle, "延续");
        StringAssert.Contains(last.MonthTitle, "结束");
        Assert.AreEqual("跨日排期，第2天 / 共3天", middle.CrossDayText);
    }

    [TestMethod]
    public async Task Calendar_SelectingTaskDayThenEmptyDayKeepsDateDetailsAndBookingIdSynchronized()
    {
        var start = new DateTimeOffset(DateTime.Today.AddHours(9), TimeZoneInfo.Local.GetUtcOffset(DateTime.Today.AddHours(9)));
        var booking = new ShootBooking
        {
            Id = Guid.NewGuid(), Title = "同步排期", ClientDisplayName = "客户", StartAtUtc = start.ToUniversalTime(),
            EndAtUtc = start.AddHours(2).ToUniversalTime(), TimeZoneId = TimeZoneInfo.Local.Id, ShootingType = "Portrait"
        };
        var service = new CalendarBookingService(booking);
        using var calendar = new WorkCalendarViewModel(service, new ProjectRepositoryStub());
        await calendar.InitializeAsync();
        var taskDay = calendar.Month.Days.Single(day => day.Date == DateTime.Today);
        var selected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        calendar.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(WorkCalendarViewModel.SelectedBookingId) && calendar.SelectedBookingId == booking.Id)
                selected.TrySetResult();
        };

        calendar.Month.SelectDateCommand.Execute(taskDay);
        await selected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(DateTime.Today, calendar.SelectedDate);
        Assert.AreEqual(booking.Id, calendar.SelectedBookingId);
        Assert.AreEqual(booking.Id, calendar.Details.Booking?.Id);

        var emptyDate = DateTime.Today.AddDays(1);
        var emptyDay = calendar.Month.Days.Single(day => day.Date == emptyDate);
        calendar.Month.SelectDateCommand.Execute(emptyDay);

        Assert.AreEqual(emptyDate, calendar.SelectedDate);
        Assert.IsNull(calendar.SelectedBookingId);
        Assert.IsNull(calendar.Details.Booking);
        Assert.IsFalse(calendar.IsDetailsOpen);
    }

    [TestMethod]
    public async Task Weather_DefaultCurrentLocationRequestsOnceAndLoadsForecast()
    {
        var state = new WeatherFeatureState();
        state.Apply(new WeatherSettings { Enabled = true });
        var location = new CurrentLocationStub(new(CurrentLocationPermission.Allowed, 31.2, 121.4, "一次性定位完成"));
        var weather = new WeatherServiceStub();
        var viewModel = new BookingWeatherViewModel(weather, state, location);

        await viewModel.LoadAsync(Booking());

        Assert.AreEqual(WeatherLocationMode.CurrentLocation, viewModel.SelectedLocationMode.Value);
        Assert.AreEqual(1, location.RequestCount);
        Assert.AreEqual(1, weather.ForecastCount);
        Assert.IsNotNull(viewModel.Summary);
    }

    [TestMethod]
    public async Task Weather_DeniedLocationFallsBackWithoutBlockingBooking()
    {
        var state = new WeatherFeatureState();
        state.Apply(new WeatherSettings { Enabled = true });
        var location = new CurrentLocationStub(new(CurrentLocationPermission.Denied, Message: "denied"));
        var weather = new WeatherServiceStub();
        var viewModel = new BookingWeatherViewModel(weather, state, location);

        await viewModel.LoadAsync(Booking());

        Assert.AreEqual("无法获取当前位置，请选择城市。", viewModel.StatusText);
        Assert.AreEqual(0, weather.ForecastCount);
        Assert.IsTrue(viewModel.IsEnabled);
    }

    [TestMethod]
    public async Task Weather_ForecastFailureDoesNotMisreportSuccessfulLocationAsDenied()
    {
        var state = new WeatherFeatureState();
        state.Apply(new WeatherSettings { Enabled = true });
        var location = new CurrentLocationStub(new(CurrentLocationPermission.Allowed, 31.2, 121.4, "一次性定位完成"));
        var viewModel = new BookingWeatherViewModel(new FailingWeatherService(), state, location);

        await viewModel.LoadAsync(Booking());

        StringAssert.Contains(viewModel.StatusText, "当前位置已获取");
        Assert.IsFalse(viewModel.StatusText.Contains("请选择城市", StringComparison.Ordinal));
        StringAssert.Contains(viewModel.LocationPermissionText, "已允许");
    }

    [TestMethod]
    [DataRow("ShowPreSessionPage")]
    [DataRow("ShowMonitorWorkspace")]
    [DataRow("联机拍摄启动页")]
    [DataRow("等待照片")]
    [DataRow("HasReadyAsset")]
    public void Tether_DeclaresTrueSessionGatedWorkspace(string token) => Contains("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml", "src/RAWSelectionAssistant/ViewModels/TetherCaptureViewModel.cs", token);

    [TestMethod]
    [DataRow("LocalSplitHeroButton")]
    [DataRow("IsMouseOver")]
    [DataRow("IsKeyboardFocused")]
    [DataRow("IsPressed")]
    [DataRow("WorkbenchHeroBrush")]
    public void LocalSplitHero_KeepsSameFillAcrossInteractionStates(string token) => Contains("src/RAWSelectionAssistant/MainWindow.xaml", token);

    [TestMethod]
    [DataRow("仅关联原位置")]
    [DataRow("安全复制到项目目录")]
    [DataRow("AddDocumentsCommand")]
    [DataRow("SelectedLinkMode")]
    [DataRow("尚未添加拍摄资料")]
    [DataRow("等待排期创建后处理")]
    public void Documents_UseOnePersistentModeAndRetainPendingSelection(string token) => Contains("src/RAWSelectionAssistant/Views/BookingDocumentsPanel.xaml", "src/RAWSelectionAssistant/ViewModels/BookingDocumentsViewModel.cs", token);

    [TestMethod]
    [DataRow("当前位置")]
    [DataRow("跟随拍摄地点")]
    [DataRow("其他城市")]
    [DataRow("无法获取当前位置，请选择城市。")]
    [DataRow("OpenLocationSettingsCommand")]
    public void WeatherUi_ExposesSafeLocationModesAndFallback(string token) => Contains("src/RAWSelectionAssistant/Views/BookingWeatherPanel.xaml", "src/RAWSelectionAssistant/ViewModels/WeatherViewModels.cs", token);

    [TestMethod]
    [DataRow("CurrentPageStatus")]
    [DataRow("BackgroundTaskStatus")]
    [DataRow("NotificationStatus")]
    public void MainStatusBar_SeparatesStatusSources(string token) => Contains("src/RAWSelectionAssistant/MainWindow.xaml", "src/RAWSelectionAssistant/ViewModels/MainViewModel.cs", token);

    [TestMethod]
    [DataRow("天气预览")]
    [DataRow("天气风险")]
    [DataRow("提醒设置")]
    [DataRow("当前提醒列表")]
    [DataRow("allowPartial: asDraft")]
    [DataRow("未命名草稿")]
    public void BookingEditor_ExposesRc4StepTwoAndPartialDraftContract(string token) => Contains("src/RAWSelectionAssistant/Views/ShootBookingEditorView.xaml", "src/RAWSelectionAssistant/ViewModels/BookingEditorViewModels.cs", token);

    [TestMethod]
    public void Rc4ModifiedViewsRemainValidXaml()
    {
        foreach (var relative in new[] { "MainWindow.xaml", "Views/WorkCalendarView.xaml", "Views/MonthCalendarView.xaml", "Views/BookingDocumentsPanel.xaml", "Views/BookingWeatherPanel.xaml", "Views/ShootBookingDetailsView.xaml", "Views/ShootBookingEditorView.xaml", "Views/FinanceView.xaml", "Views/TetherCaptureView.xaml" })
            XDocument.Parse(File.ReadAllText(Path.Combine(Root(), "src", "RAWSelectionAssistant", relative.Replace('/', Path.DirectorySeparatorChar))));
    }

    private static async Task ExecuteAndWaitForFailureAsync(ShootBookingEditorViewModel editor)
    {
        await ExecuteAndWaitAsync(editor, editor.SaveCommand);
    }

    private static async Task ExecuteAndWaitAsync(ShootBookingEditorViewModel editor, System.Windows.Input.ICommand command)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sawBusy = false;
        System.ComponentModel.PropertyChangedEventHandler? handler = null;
        handler = (_, args) =>
        {
            if (args.PropertyName != nameof(ShootBookingEditorViewModel.IsBusy)) return;
            if (editor.IsBusy) sawBusy = true;
            else if (sawBusy)
                completion.TrySetResult();
        };
        editor.PropertyChanged += handler;
        try
        {
            command.Execute(null);
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { editor.PropertyChanged -= handler; }
    }

    private static ShootBooking Booking()
    {
        var start = new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.FromHours(8));
        return new() { Id = Guid.NewGuid(), Title = "天气排期", ClientDisplayName = "客户", StartAtUtc = start.ToUniversalTime(), EndAtUtc = start.AddHours(2).ToUniversalTime(), TimeZoneId = "China Standard Time", ShootingType = "Portrait" };
    }

    private static void Contains(params string[] filesAndToken)
    {
        var token = filesAndToken[^1];
        var text = string.Join('\n', filesAndToken[..^1].Select(relative => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)))));
        StringAssert.Contains(text, token);
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }

    private sealed class ProjectRepositoryStub : IProjectRepository
    {
        public Task UpsertAsync(PhotoProjectRecord project, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PhotoProjectRecord>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PhotoProjectRecord>>([]);
    }

    private sealed class FailingBookingService : IShootBookingService
    {
        public List<ShootBookingDraft> Drafts { get; } = [];
        public Task<BookingSaveResult> SaveAsync(ShootBookingDraft draft, BookingConflictResolution conflictResolution = BookingConflictResolution.None, CancellationToken cancellationToken = default) { Drafts.Add(draft); throw new IOException("database unavailable"); }
        public Task<ShootBooking?> GetAsync(Guid id, bool includeArchived = false, CancellationToken cancellationToken = default) => Task.FromResult<ShootBooking?>(null);
        public Task<IReadOnlyList<ShootRequirementItem>> GetRequirementsAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShootRequirementItem>>([]);
        public Task<IReadOnlyList<ShootBookingSummary>> QueryCurrentViewAsync(ShootBookingQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShootBookingSummary>>([]);
        public Task<ShootBookingPage> SearchAllUnarchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ShootBookingPage([], null));
        public Task<ShootBookingPage> SearchArchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ShootBookingPage([], null));
        public Task<bool> CompleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> RestoreAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class SuccessfulBookingService : IShootBookingService
    {
        public List<ShootBookingDraft> Drafts { get; } = [];
        public Task<BookingSaveResult> SaveAsync(ShootBookingDraft draft, BookingConflictResolution conflictResolution = BookingConflictResolution.None, CancellationToken cancellationToken = default)
        {
            Drafts.Add(draft);
            var booking = new ShootBooking
            {
                Id = draft.Id!.Value,
                ProjectId = draft.ProjectId,
                Title = draft.Title,
                ClientDisplayName = draft.ClientDisplayName,
                StartAtUtc = draft.StartAt.ToUniversalTime(),
                EndAtUtc = draft.EndAt.ToUniversalTime(),
                TimeZoneId = draft.TimeZoneId,
                Status = draft.Status,
                ShootingType = draft.ShootingType
            };
            return Task.FromResult(new BookingSaveResult(BookingSaveStatus.Saved, booking, BookingMoneyCalculator.Calculate(null, null, null), [], []));
        }
        public Task<ShootBooking?> GetAsync(Guid id, bool includeArchived = false, CancellationToken cancellationToken = default) => Task.FromResult<ShootBooking?>(null);
        public Task<IReadOnlyList<ShootRequirementItem>> GetRequirementsAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShootRequirementItem>>([]);
        public Task<IReadOnlyList<ShootBookingSummary>> QueryCurrentViewAsync(ShootBookingQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShootBookingSummary>>([]);
        public Task<ShootBookingPage> SearchAllUnarchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ShootBookingPage([], null));
        public Task<ShootBookingPage> SearchArchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ShootBookingPage([], null));
        public Task<bool> CompleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> RestoreAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class CalendarBookingService(ShootBooking booking) : IShootBookingService
    {
        private ShootBookingSummary Summary => new(booking.Id, booking.ProjectId, booking.Title, booking.ClientDisplayName, booking.StartAtUtc, booking.EndAtUtc, booking.TimeZoneId, booking.IsAllDay, booking.Status, booking.Location, booking.ShootingType, booking.AllowOverlap, booking.IsArchived, booking.CreatedAtUtc);
        public Task<BookingSaveResult> SaveAsync(ShootBookingDraft draft, BookingConflictResolution conflictResolution = BookingConflictResolution.None, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ShootBooking?> GetAsync(Guid id, bool includeArchived = false, CancellationToken cancellationToken = default) => Task.FromResult<ShootBooking?>(id == booking.Id ? booking : null);
        public Task<IReadOnlyList<ShootRequirementItem>> GetRequirementsAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShootRequirementItem>>([]);
        public Task<IReadOnlyList<ShootBookingSummary>> QueryCurrentViewAsync(ShootBookingQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShootBookingSummary>>([Summary]);
        public Task<ShootBookingPage> SearchAllUnarchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ShootBookingPage([Summary], null));
        public Task<ShootBookingPage> SearchArchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ShootBookingPage([], null));
        public Task<bool> CompleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> RestoreAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class CurrentLocationStub(CurrentLocationResult result) : ICurrentLocationService
    {
        public int RequestCount { get; private set; }
        public Task<CurrentLocationResult> GetCurrentLocationAsync(CancellationToken cancellationToken = default) { RequestCount++; return Task.FromResult(result); }
        public Task OpenLocationPrivacySettingsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class WeatherServiceStub : IWeatherForecastService
    {
        public int ForecastCount { get; private set; }
        public Task<IReadOnlyList<WeatherLocationCandidate>> SearchLocationsAsync(string query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WeatherLocationCandidate>>([]);
        public void ConfirmLocation(Guid bookingId, WeatherLocationCandidate candidate) { }
        public Task<BookingWeatherSummary> GetBookingWeatherAsync(Guid bookingId, DateTimeOffset startAtUtc, DateTimeOffset endAtUtc, bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            ForecastCount++;
            var location = new WeatherLocation("上海", "上海", "中国", 31.2, 121.4, "Asia/Shanghai", "Test");
            var hour = new HourlyWeatherForecast(startAtUtc, "1", 23, 24, 10, 0, 8, 12, 60, 20, 10000);
            return Task.FromResult(new BookingWeatherSummary(bookingId, WeatherAvailability.Available, location, hour, null, [hour], [], [], DateTimeOffset.UtcNow, "Test", false, false, false, "天气已更新。"));
        }
        public Task<BookingWeatherSummary?> TryGetCachedBookingWeatherAsync(Guid bookingId, DateTimeOffset startAtUtc, DateTimeOffset endAtUtc, CancellationToken cancellationToken = default) => Task.FromResult<BookingWeatherSummary?>(null);
        public Task ClearCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FailingWeatherService : IWeatherForecastService
    {
        public Task<IReadOnlyList<WeatherLocationCandidate>> SearchLocationsAsync(string query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WeatherLocationCandidate>>([]);
        public void ConfirmLocation(Guid bookingId, WeatherLocationCandidate candidate) { }
        public Task<BookingWeatherSummary> GetBookingWeatherAsync(Guid bookingId, DateTimeOffset startAtUtc, DateTimeOffset endAtUtc, bool forceRefresh = false, CancellationToken cancellationToken = default) => throw new HttpRequestException("offline");
        public Task<BookingWeatherSummary?> TryGetCachedBookingWeatherAsync(Guid bookingId, DateTimeOffset startAtUtc, DateTimeOffset endAtUtc, CancellationToken cancellationToken = default) => Task.FromResult<BookingWeatherSummary?>(null);
        public Task ClearCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
