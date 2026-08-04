using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public enum CalendarViewMode { Month, Week, Day }

public sealed record CalendarStatusOption(string Label, ShootBookingStatus? Value);
public sealed record CalendarTypeOption(string Label, string? Value);
public sealed record CalendarSearchScopeOption(string Label, BookingSearchScope Value);

public sealed class WorkCalendarViewModel : ObservableObject, IDisposable
{
    private readonly IShootBookingService _bookingService;
    private readonly RAWSelectionAssistant.Core.Services.Database.IProjectRepository _projectRepository;
    private CancellationTokenSource? _queryCancellation;
    private bool _initialized;
    private bool _isBusy;
    private string _statusText = "准备就绪";
    private CalendarViewMode _viewMode = CalendarViewMode.Month;
    private DateTime _selectedDate = DateTime.Today;
    private CalendarStatusOption _selectedStatus;
    private CalendarTypeOption _selectedType;
    private CalendarSearchScopeOption _selectedSearchScope;
    private string _searchText = string.Empty;
    private ShootBookingPageCursor? _nextCursor;
    private bool _isDetailsOpen;
    private bool _isArchivedPaneOpen;

    public WorkCalendarViewModel(IShootBookingService bookingService, RAWSelectionAssistant.Core.Services.Database.IProjectRepository projectRepository,
        IBookingDocumentWorkflowService? documentWorkflow = null, IDialogService? dialogs = null)
    {
        _bookingService = bookingService;
        _projectRepository = projectRepository;
        StatusOptions =
        [
            new("全部状态", null), new("待确定", ShootBookingStatus.Tentative), new("已确认", ShootBookingStatus.Confirmed),
            new("准备中", ShootBookingStatus.Preparing), new("拍摄中", ShootBookingStatus.Shooting), new("已完成", ShootBookingStatus.Completed),
            new("已取消", ShootBookingStatus.Cancelled), new("已延期", ShootBookingStatus.Postponed)
        ];
        TypeOptions =
        [
            new("全部类型", null), new("人像", "Portrait"), new("婚礼", "Wedding"), new("商业", "Commercial"),
            new("活动", "Event"), new("产品", "Product"), new("其他", "Other")
        ];
        SearchScopeOptions = [new("当前视图", BookingSearchScope.CurrentView), new("全部未归档排期", BookingSearchScope.AllUnarchived)];
        _selectedStatus = StatusOptions[0];
        _selectedType = TypeOptions[0];
        _selectedSearchScope = SearchScopeOptions[0];

        Month = new MonthCalendarViewModel(SelectDate, OpenBookingAsync, CreateForDate);
        Week = new WeekCalendarViewModel(SelectDate, OpenBookingAsync, CreateAt);
        Day = new DayCalendarViewModel(OpenBookingAsync, CreateAt);
        DaySchedule = new DaySchedulePanelViewModel(OpenBookingAsync);
        Details = new ShootBookingDetailsViewModel(bookingService, documentWorkflow, dialogs);
        Details.CloseRequested += (_, _) => IsDetailsOpen = false;
        Details.EditRequested += (_, bookingId) => _ = RequestEditorAsync(bookingId, null);
        Details.Archived += (_, _) => _ = RefreshAsync();
        Archived = new ArchivedBookingsViewModel(bookingService);
        Archived.OpenDetailsRequested += (_, id) => _ = OpenBookingAsync(id, includeArchived: true);
        Archived.Restored += (_, _) => _ = RefreshAsync();

        TodayCommand = new AsyncRelayCommand(_ => GoTodayAsync());
        PreviousCommand = new AsyncRelayCommand(_ => MovePeriodAsync(-1));
        NextCommand = new AsyncRelayCommand(_ => MovePeriodAsync(1));
        SetMonthViewCommand = new AsyncRelayCommand(_ => SetModeAsync(CalendarViewMode.Month));
        SetWeekViewCommand = new AsyncRelayCommand(_ => SetModeAsync(CalendarViewMode.Week));
        SetDayViewCommand = new AsyncRelayCommand(_ => SetModeAsync(CalendarViewMode.Day));
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        NewBookingCommand = new AsyncRelayCommand(_ => RequestEditorAsync(null, DefaultStartForSelectedDate()));
        LoadMoreCommand = new AsyncRelayCommand(_ => LoadMoreAsync(), _ => HasMoreGlobalResults && !IsBusy);
        OpenBookingCommand = new AsyncRelayCommand(parameter => parameter is ShootBookingSummary item ? OpenBookingAsync(item.Id) : Task.CompletedTask);
        ToggleArchivedCommand = new AsyncRelayCommand(_ => ToggleArchivedAsync());
        CloseDetailsCommand = new RelayCommand(_ => IsDetailsOpen = false);
        FocusSearchCommand = new RelayCommand(_ => FocusSearchRequested());
    }

    public event EventHandler<BookingEditorRequestEventArgs>? EditorRequested;

    public IReadOnlyList<CalendarStatusOption> StatusOptions { get; }
    public IReadOnlyList<CalendarTypeOption> TypeOptions { get; }
    public IReadOnlyList<CalendarSearchScopeOption> SearchScopeOptions { get; }
    public ObservableCollection<ShootBookingSummary> GlobalSearchResults { get; } = [];
    public MonthCalendarViewModel Month { get; }
    public WeekCalendarViewModel Week { get; }
    public DayCalendarViewModel Day { get; }
    public DaySchedulePanelViewModel DaySchedule { get; }
    public ShootBookingDetailsViewModel Details { get; }
    public ArchivedBookingsViewModel Archived { get; }

    public ICommand TodayCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand SetMonthViewCommand { get; }
    public ICommand SetWeekViewCommand { get; }
    public ICommand SetDayViewCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand NewBookingCommand { get; }
    public ICommand LoadMoreCommand { get; }
    public ICommand OpenBookingCommand { get; }
    public ICommand ToggleArchivedCommand { get; }
    public ICommand CloseDetailsCommand { get; }
    public ICommand FocusSearchCommand { get; }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public CalendarViewMode ViewMode { get => _viewMode; private set { if (!SetProperty(ref _viewMode, value)) return; NotifyMode(); } }
    public bool IsMonthView => ViewMode == CalendarViewMode.Month;
    public bool IsWeekView => ViewMode == CalendarViewMode.Week;
    public bool IsDayView => ViewMode == CalendarViewMode.Day;
    public bool IsCurrentViewSearch => SelectedSearchScope.Value == BookingSearchScope.CurrentView;
    public bool IsGlobalSearch => !IsCurrentViewSearch;
    public bool HasMoreGlobalResults => _nextCursor is not null;
    public bool IsDetailsOpen { get => _isDetailsOpen; private set => SetProperty(ref _isDetailsOpen, value); }
    public bool IsArchivedPaneOpen { get => _isArchivedPaneOpen; private set => SetProperty(ref _isArchivedPaneOpen, value); }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            var date = value.Date;
            if (!SetProperty(ref _selectedDate, date)) return;
            OnPropertyChanged(nameof(DisplayPeriod));
            QueueRefresh();
        }
    }

    public string DisplayPeriod => ViewMode switch
    {
        CalendarViewMode.Month => SelectedDate.ToString("yyyy年M月", CultureInfo.GetCultureInfo("zh-CN")),
        CalendarViewMode.Week => $"{StartOfWeek(SelectedDate):yyyy年M月d日} — {StartOfWeek(SelectedDate).AddDays(6):M月d日}",
        _ => SelectedDate.ToString("yyyy年M月d日 dddd", CultureInfo.GetCultureInfo("zh-CN"))
    };

    public CalendarStatusOption SelectedStatus
    {
        get => _selectedStatus;
        set { if (value is not null && SetProperty(ref _selectedStatus, value)) QueueRefresh(); }
    }

    public CalendarTypeOption SelectedType
    {
        get => _selectedType;
        set { if (value is not null && SetProperty(ref _selectedType, value)) QueueRefresh(); }
    }

    public CalendarSearchScopeOption SelectedSearchScope
    {
        get => _selectedSearchScope;
        set
        {
            if (value is null || !SetProperty(ref _selectedSearchScope, value)) return;
            OnPropertyChanged(nameof(IsCurrentViewSearch));
            OnPropertyChanged(nameof(IsGlobalSearch));
            QueueRefresh();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value ?? string.Empty)) QueueRefresh(); }
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await RefreshAsync().ConfigureAwait(true);
    }

    public async Task RefreshAsync()
    {
        _queryCancellation?.Cancel();
        _queryCancellation?.Dispose();
        _queryCancellation = new CancellationTokenSource();
        var cancellationToken = _queryCancellation.Token;
        IsBusy = true;
        try
        {
            if (IsGlobalSearch)
            {
                var page = await _bookingService.SearchAllUnarchivedAsync(BuildSearchRequest(null), cancellationToken).ConfigureAwait(true);
                Replace(GlobalSearchResults, page.Items);
                _nextCursor = page.NextCursor;
                OnPropertyChanged(nameof(HasMoreGlobalResults));
                StatusText = $"已加载 {GlobalSearchResults.Count} 条未归档排期";
                return;
            }

            var (startDate, endDateExclusive) = CurrentDateRange();
            var range = ShootBookingTimeRules.CreateAllDayRange(DateOnly.FromDateTime(startDate), DateOnly.FromDateTime(endDateExclusive), TimeZoneInfo.Local.Id);
            var items = await _bookingService.QueryCurrentViewAsync(new ShootBookingQuery(
                range.StartAtUtc, range.EndAtUtc, SelectedStatus.Value, SelectedType.Value, SearchText), cancellationToken).ConfigureAwait(true);
            ApplyCurrentView(items);
            _nextCursor = null;
            OnPropertyChanged(nameof(HasMoreGlobalResults));
            StatusText = $"当前视图 {items.Count} 条排期";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusText = $"加载排期失败：{ex.Message}";
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested) IsBusy = false;
        }
    }

    public void FocusSearchRequested() => SearchFocusRequested?.Invoke(this, EventArgs.Empty);
    public event EventHandler? SearchFocusRequested;

    private ShootBookingSearchRequest BuildSearchRequest(ShootBookingPageCursor? cursor) =>
        new(SearchText, SelectedStatus.Value, SelectedType.Value, cursor, 50);

    private async Task LoadMoreAsync()
    {
        if (_nextCursor is null) return;
        var cancellationToken = _queryCancellation?.Token ?? CancellationToken.None;
        IsBusy = true;
        try
        {
            var page = await _bookingService.SearchAllUnarchivedAsync(BuildSearchRequest(_nextCursor), cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var item in page.Items) GlobalSearchResults.Add(item);
            _nextCursor = page.NextCursor;
            OnPropertyChanged(nameof(HasMoreGlobalResults));
            StatusText = $"已加载 {GlobalSearchResults.Count} 条未归档排期";
        }
        catch (OperationCanceledException) { }
        finally { if (!cancellationToken.IsCancellationRequested) IsBusy = false; }
    }

    private void ApplyCurrentView(IReadOnlyList<ShootBookingSummary> items)
    {
        Month.Configure(SelectedDate, items, SelectedDate);
        Week.Configure(StartOfWeek(SelectedDate), items, SelectedDate);
        Day.Configure(SelectedDate, items);
        DaySchedule.Configure(SelectedDate, ItemsOnDate(items, SelectedDate));
    }

    private static IReadOnlyList<ShootBookingSummary> ItemsOnDate(IEnumerable<ShootBookingSummary> items, DateTime date) =>
        items.Where(item => CalendarBookingItemViewModel.SpansDate(item, date)).OrderBy(item => item.StartAtUtc).ToArray();

    private void SelectDate(DateTime date)
    {
        var previousMonth = (_selectedDate.Year, _selectedDate.Month);
        _selectedDate = date.Date;
        OnPropertyChanged(nameof(SelectedDate));
        OnPropertyChanged(nameof(DisplayPeriod));
        if (ViewMode == CalendarViewMode.Month && previousMonth != (_selectedDate.Year, _selectedDate.Month))
        {
            _ = RefreshAsync();
            return;
        }
        Month.Configure(_selectedDate, Month.AllItems, _selectedDate);
        DaySchedule.Configure(SelectedDate, Month.AllItems.Where(item => CalendarBookingItemViewModel.SpansDate(item, SelectedDate)).ToArray());
    }

    private void CreateForDate(DateTime date) => _ = RequestEditorAsync(null, DefaultStart(date));
    private void CreateAt(DateTime dateTime) => _ = RequestEditorAsync(null, dateTime);

    private async Task OpenBookingAsync(Guid bookingId, bool includeArchived = false)
    {
        await Details.LoadAsync(bookingId, includeArchived).ConfigureAwait(true);
        IsDetailsOpen = Details.Booking is not null;
    }

    private Task OpenBookingAsync(ShootBookingSummary item) => OpenBookingAsync(item.Id);

    private async Task RequestEditorAsync(Guid? bookingId, DateTime? suggestedStart)
    {
        var editor = new ShootBookingEditorViewModel(_bookingService, _projectRepository, bookingId, suggestedStart);
        await editor.InitializeAsync().ConfigureAwait(true);
        editor.Saved += async (_, saved) =>
        {
            await RefreshAsync().ConfigureAwait(true);
            await OpenBookingAsync(saved.Id).ConfigureAwait(true);
        };
        EditorRequested?.Invoke(this, new BookingEditorRequestEventArgs(editor));
    }

    private async Task ToggleArchivedAsync()
    {
        IsArchivedPaneOpen = !IsArchivedPaneOpen;
        if (IsArchivedPaneOpen) await Archived.RefreshAsync().ConfigureAwait(true);
    }

    private async Task SetModeAsync(CalendarViewMode mode)
    {
        ViewMode = mode;
        OnPropertyChanged(nameof(DisplayPeriod));
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task GoTodayAsync()
    {
        _selectedDate = DateTime.Today;
        OnPropertyChanged(nameof(SelectedDate));
        OnPropertyChanged(nameof(DisplayPeriod));
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task MovePeriodAsync(int direction)
    {
        _selectedDate = ViewMode switch
        {
            CalendarViewMode.Month => SelectedDate.AddMonths(direction),
            CalendarViewMode.Week => SelectedDate.AddDays(7 * direction),
            _ => SelectedDate.AddDays(direction)
        };
        OnPropertyChanged(nameof(SelectedDate));
        OnPropertyChanged(nameof(DisplayPeriod));
        await RefreshAsync().ConfigureAwait(true);
    }

    private (DateTime Start, DateTime EndExclusive) CurrentDateRange() => ViewMode switch
    {
        CalendarViewMode.Month => (MonthGridStart(SelectedDate), MonthGridStart(SelectedDate).AddDays(42)),
        CalendarViewMode.Week => (StartOfWeek(SelectedDate), StartOfWeek(SelectedDate).AddDays(7)),
        _ => (SelectedDate.Date, SelectedDate.Date.AddDays(1))
    };

    private DateTime DefaultStartForSelectedDate() => SelectedDate.Date == DateTime.Today ? NextFullHour(DateTime.Now) : DefaultStart(SelectedDate);
    private static DateTime DefaultStart(DateTime date) => date.Date.AddHours(9);
    private static DateTime NextFullHour(DateTime value) => new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0).AddHours(1);
    internal static DateTime StartOfWeek(DateTime date) => date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    internal static DateTime MonthGridStart(DateTime date) => StartOfWeek(new DateTime(date.Year, date.Month, 1));

    private void NotifyMode()
    {
        OnPropertyChanged(nameof(IsMonthView));
        OnPropertyChanged(nameof(IsWeekView));
        OnPropertyChanged(nameof(IsDayView));
        OnPropertyChanged(nameof(DisplayPeriod));
    }

    private void QueueRefresh()
    {
        if (_initialized) _ = RefreshAsync();
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    public void Dispose()
    {
        _queryCancellation?.Cancel();
        _queryCancellation?.Dispose();
    }
}

public sealed class BookingEditorRequestEventArgs(ShootBookingEditorViewModel editor) : EventArgs
{
    public ShootBookingEditorViewModel Editor { get; } = editor;
}

public sealed class CalendarBookingItemViewModel
{
    public CalendarBookingItemViewModel(ShootBookingSummary booking)
    {
        Booking = booking;
        var zone = ResolveZone(booking.TimeZoneId);
        LocalStart = TimeZoneInfo.ConvertTime(booking.StartAtUtc, zone).DateTime;
        LocalEnd = TimeZoneInfo.ConvertTime(booking.EndAtUtc, zone).DateTime;
    }

    public ShootBookingSummary Booking { get; }
    public Guid Id => Booking.Id;
    public string Title => Booking.Title;
    public string ClientDisplayName => Booking.ClientDisplayName;
    public DateTime LocalStart { get; }
    public DateTime LocalEnd { get; }
    public bool IsAllDay => Booking.IsAllDay;
    public string TimeText => IsAllDay ? "全天" : $"{LocalStart:HH:mm}–{LocalEnd:HH:mm}";
    public string StatusText => CalendarText.Status(Booking.Status);
    public string StatusGlyph => Booking.Status switch { ShootBookingStatus.Completed => "✓", ShootBookingStatus.Cancelled => "×", ShootBookingStatus.Confirmed => "●", ShootBookingStatus.Preparing => "◆", ShootBookingStatus.Shooting => "▶", ShootBookingStatus.Postponed => "↷", _ => "○" };
    public string AccessibilityName => $"{Title}，{ClientDisplayName}，{TimeText}，{StatusText}";

    public static bool SpansDate(ShootBookingSummary booking, DateTime date)
    {
        var item = new CalendarBookingItemViewModel(booking);
        var last = item.LocalEnd.TimeOfDay == TimeSpan.Zero ? item.LocalEnd.Date.AddDays(-1) : item.LocalEnd.Date;
        return date.Date >= item.LocalStart.Date && date.Date <= last;
    }

    private static TimeZoneInfo ResolveZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return TimeZoneInfo.Local; }
    }
}

public sealed class MonthDayViewModel
{
    public DateTime Date { get; init; }
    public int DayNumber => Date.Day;
    public bool IsCurrentMonth { get; init; }
    public bool IsToday => Date == DateTime.Today;
    public bool IsSelected { get; init; }
    public ObservableCollection<CalendarBookingItemViewModel> VisibleBookings { get; } = [];
    public int OverflowCount { get; init; }
    public string OverflowText => OverflowCount > 0 ? $"另有 {OverflowCount} 项" : string.Empty;
    public string AccessibilityName => $"{Date:yyyy年M月d日}，{VisibleBookings.Count + OverflowCount}项排期";
}

public sealed class MonthCalendarViewModel
{
    private readonly Action<DateTime> _selectDate;
    public MonthCalendarViewModel(Action<DateTime> selectDate, Func<ShootBookingSummary, Task> openBooking, Action<DateTime> create)
    {
        _selectDate = selectDate;
        OpenBookingCommand = new AsyncRelayCommand(parameter => parameter is CalendarBookingItemViewModel item ? openBooking(item.Booking) : Task.CompletedTask);
        SelectDateCommand = new RelayCommand(parameter => { if (parameter is MonthDayViewModel day) _selectDate(day.Date); });
        CreateBookingCommand = new RelayCommand(parameter => { if (parameter is MonthDayViewModel day) create(day.Date); });
    }

    public ObservableCollection<MonthDayViewModel> Days { get; } = [];
    public IReadOnlyList<ShootBookingSummary> AllItems { get; private set; } = [];
    public ICommand OpenBookingCommand { get; }
    public ICommand SelectDateCommand { get; }
    public ICommand CreateBookingCommand { get; }

    public void Configure(DateTime month, IReadOnlyList<ShootBookingSummary> items, DateTime selectedDate)
    {
        AllItems = items;
        Days.Clear();
        var start = WorkCalendarViewModel.MonthGridStart(month);
        for (var offset = 0; offset < 42; offset++)
        {
            var date = start.AddDays(offset);
            var matches = items.Where(item => CalendarBookingItemViewModel.SpansDate(item, date)).Select(item => new CalendarBookingItemViewModel(item)).OrderBy(item => item.LocalStart).ToArray();
            var day = new MonthDayViewModel { Date = date, IsCurrentMonth = date.Month == month.Month && date.Year == month.Year, IsSelected = date == selectedDate.Date, OverflowCount = Math.Max(0, matches.Length - 3) };
            foreach (var match in matches.Take(3)) day.VisibleBookings.Add(match);
            Days.Add(day);
        }
    }
}

public sealed class CalendarDayColumnViewModel
{
    public DateTime Date { get; init; }
    public string Header => Date.ToString("ddd M/d", CultureInfo.GetCultureInfo("zh-CN"));
    public bool IsToday => Date == DateTime.Today;
    public ObservableCollection<CalendarBookingItemViewModel> AllDayBookings { get; } = [];
    public ObservableCollection<CalendarBookingItemViewModel> TimedBookings { get; } = [];
    public ObservableCollection<DayTimeSlotViewModel> TimeSlots { get; } = [];
}

public sealed class WeekCalendarViewModel
{
    public WeekCalendarViewModel(Action<DateTime> selectDate, Func<ShootBookingSummary, Task> openBooking, Action<DateTime> create)
    {
        SelectDateCommand = new RelayCommand(parameter => { if (parameter is CalendarDayColumnViewModel day) selectDate(day.Date); });
        OpenBookingCommand = new AsyncRelayCommand(parameter => parameter is CalendarBookingItemViewModel item ? openBooking(item.Booking) : Task.CompletedTask);
        CreateBookingCommand = new RelayCommand(parameter =>
        {
            if (parameter is DayTimeSlotViewModel slot) create(slot.Start);
            else if (parameter is CalendarDayColumnViewModel day) create(day.Date.AddHours(9));
        });
    }

    public ObservableCollection<CalendarDayColumnViewModel> Days { get; } = [];
    public ICommand SelectDateCommand { get; }
    public ICommand OpenBookingCommand { get; }
    public ICommand CreateBookingCommand { get; }

    public void Configure(DateTime weekStart, IReadOnlyList<ShootBookingSummary> items, DateTime selectedDate)
    {
        Days.Clear();
        for (var i = 0; i < 7; i++)
        {
            var date = weekStart.AddDays(i);
            var day = new CalendarDayColumnViewModel { Date = date };
            foreach (var source in items.Where(item => CalendarBookingItemViewModel.SpansDate(item, date)).Select(item => new CalendarBookingItemViewModel(item)).OrderBy(item => item.LocalStart))
            {
                if (source.IsAllDay || source.LocalStart.Date != source.LocalEnd.Date) day.AllDayBookings.Add(source);
                else day.TimedBookings.Add(source);
            }
            for (var hour = 0; hour < 24; hour++)
            {
                var slot = new DayTimeSlotViewModel { Start = date.AddHours(hour) };
                foreach (var booking in day.TimedBookings.Where(item => item.LocalStart.Hour == hour)) slot.Bookings.Add(booking);
                day.TimeSlots.Add(slot);
            }
            Days.Add(day);
        }
    }
}

public sealed class DayTimeSlotViewModel
{
    public DateTime Start { get; init; }
    public string Label => Start.ToString("HH:mm");
    public ObservableCollection<CalendarBookingItemViewModel> Bookings { get; } = [];
}

public sealed class DayCalendarViewModel
{
    private readonly Action<DateTime> _create;
    public DayCalendarViewModel(Func<ShootBookingSummary, Task> openBooking, Action<DateTime> create)
    {
        _create = create;
        OpenBookingCommand = new AsyncRelayCommand(parameter => parameter is CalendarBookingItemViewModel item ? openBooking(item.Booking) : Task.CompletedTask);
        CreateAtCommand = new RelayCommand(parameter => { if (parameter is DayTimeSlotViewModel slot) _create(slot.Start); });
    }

    public DateTime Date { get; private set; }
    public ObservableCollection<CalendarBookingItemViewModel> AllDayBookings { get; } = [];
    public ObservableCollection<DayTimeSlotViewModel> TimeSlots { get; } = [];
    public ICommand OpenBookingCommand { get; }
    public ICommand CreateAtCommand { get; }

    public void Configure(DateTime date, IReadOnlyList<ShootBookingSummary> items)
    {
        Date = date.Date;
        AllDayBookings.Clear();
        TimeSlots.Clear();
        var wrappers = items.Where(item => CalendarBookingItemViewModel.SpansDate(item, Date)).Select(item => new CalendarBookingItemViewModel(item)).ToArray();
        foreach (var item in wrappers.Where(item => item.IsAllDay || item.LocalStart.Date != item.LocalEnd.Date)) AllDayBookings.Add(item);
        for (var hour = 0; hour < 24; hour++)
        {
            var slot = new DayTimeSlotViewModel { Start = Date.AddHours(hour) };
            foreach (var item in wrappers.Where(item => !item.IsAllDay && item.LocalStart.Date == Date && item.LocalStart.Hour == hour)) slot.Bookings.Add(item);
            TimeSlots.Add(slot);
        }
    }
}

public sealed class DaySchedulePanelViewModel : ObservableObject
{
    private DateTime _date;
    public DaySchedulePanelViewModel(Func<ShootBookingSummary, Task> openBooking) =>
        OpenBookingCommand = new AsyncRelayCommand(parameter => parameter is CalendarBookingItemViewModel item ? openBooking(item.Booking) : Task.CompletedTask);
    public DateTime Date { get => _date; private set { if (SetProperty(ref _date, value)) OnPropertyChanged(nameof(Title)); } }
    public string Title => $"{Date:M月d日} 当天排期";
    public ObservableCollection<CalendarBookingItemViewModel> Bookings { get; } = [];
    public ICommand OpenBookingCommand { get; }
    public void Configure(DateTime date, IReadOnlyList<ShootBookingSummary> items)
    {
        Date = date.Date;
        Bookings.Clear();
        foreach (var item in items.Select(item => new CalendarBookingItemViewModel(item)).OrderBy(item => item.LocalStart)) Bookings.Add(item);
    }
}

public sealed class ArchivedBookingsViewModel : ObservableObject
{
    private readonly IShootBookingService _service;
    private ShootBookingPageCursor? _nextCursor;
    private bool _isBusy;
    private string _searchText = string.Empty;
    private string _statusText = string.Empty;
    public ArchivedBookingsViewModel(IShootBookingService service)
    {
        _service = service;
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        LoadMoreCommand = new AsyncRelayCommand(_ => LoadMoreAsync(), _ => HasMore && !IsBusy);
        OpenCommand = new RelayCommand(parameter => { if (parameter is ShootBookingSummary item) OpenDetailsRequested?.Invoke(this, item.Id); });
        RestoreCommand = new AsyncRelayCommand(parameter => parameter is ShootBookingSummary item ? RestoreAsync(item.Id) : Task.CompletedTask);
    }

    public event EventHandler<Guid>? OpenDetailsRequested;
    public event EventHandler<Guid>? Restored;
    public ObservableCollection<ShootBookingSummary> Items { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand LoadMoreCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand RestoreCommand { get; }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public bool HasMore => _nextCursor is not null;
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value ?? string.Empty); }

    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var page = await _service.SearchArchivedAsync(new ShootBookingSearchRequest(SearchText, PageSize: 50)).ConfigureAwait(true);
            Items.Clear();
            foreach (var item in page.Items) Items.Add(item);
            _nextCursor = page.NextCursor;
            OnPropertyChanged(nameof(HasMore));
            StatusText = $"{Items.Count} 条已归档排期";
        }
        finally { IsBusy = false; }
    }

    private async Task LoadMoreAsync()
    {
        if (_nextCursor is null) return;
        IsBusy = true;
        try
        {
            var page = await _service.SearchArchivedAsync(new ShootBookingSearchRequest(SearchText, Cursor: _nextCursor, PageSize: 50)).ConfigureAwait(true);
            foreach (var item in page.Items) Items.Add(item);
            _nextCursor = page.NextCursor;
            OnPropertyChanged(nameof(HasMore));
            StatusText = $"{Items.Count} 条已归档排期";
        }
        finally { IsBusy = false; }
    }

    private async Task RestoreAsync(Guid id)
    {
        if (!await _service.RestoreAsync(id).ConfigureAwait(true)) return;
        await RefreshAsync().ConfigureAwait(true);
        Restored?.Invoke(this, id);
    }
}

internal static class CalendarText
{
    public static string Status(ShootBookingStatus status) => status switch
    {
        ShootBookingStatus.Tentative => "待确定", ShootBookingStatus.Confirmed => "已确认", ShootBookingStatus.Preparing => "准备中",
        ShootBookingStatus.Shooting => "拍摄中", ShootBookingStatus.Completed => "已完成", ShootBookingStatus.Cancelled => "已取消",
        ShootBookingStatus.Postponed => "已延期", _ => status.ToString()
    };

    public static string Type(string type) => type switch
    {
        "Portrait" => "人像", "Wedding" => "婚礼", "Commercial" => "商业", "Event" => "活动", "Product" => "产品", "Other" => "其他", _ => type
    };
}
