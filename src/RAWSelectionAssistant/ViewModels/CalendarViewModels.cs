using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Business;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public enum CalendarViewMode { Month, Week, Day }
public enum CalendarPresentationMode { Calendar, List, Overview }
public enum CalendarSortMode { ShootTime, CreatedTime, Status, ClientOrProject }

public sealed record CalendarStatusOption(
    string Label,
    ShootBookingStatus? Value = null,
    IReadOnlyList<ShootBookingStatus>? Values = null,
    string? BusinessState = null,
    CalendarWorkflowStatus? WorkflowStatus = null,
    bool FreeOnly = false,
    bool ConflictOnly = false,
    bool WeatherRiskOnly = false,
    bool ArchivedOnly = false)
{
    public override string ToString() => Label;
}
public sealed record CalendarTypeOption(string Label, string? Value) { public override string ToString() => Label; }
public sealed record CalendarSearchScopeOption(string Label, BookingSearchScope Value) { public override string ToString() => Label; }
public sealed record CalendarPresentationOption(string Label, CalendarPresentationMode Value) { public override string ToString() => Label; }
public sealed record CalendarSortOption(string Label, CalendarSortMode Value) { public override string ToString() => Label; }

public sealed class WorkCalendarViewModel : ObservableObject, IDisposable
{
    private readonly IShootBookingService _bookingService;
    private readonly RAWSelectionAssistant.Core.Services.Database.IProjectRepository _projectRepository;
    private readonly IBookingReminderScheduler? _reminderScheduler;
    private readonly IWeatherForecastService? _weatherService;
    private readonly WeatherFeatureState? _weatherState;
    private readonly ICalendarAvailabilityStore _availabilityStore;
    private readonly IDialogService? _dialogs;
    private readonly IBookingPeopleService? _bookingPeopleService;
    private readonly IBookingDocumentWorkflowService? _documentWorkflow;
    private readonly IBookingTimeDisplayService _timeDisplay;
    private readonly ICurrentLocationService? _currentLocationService;
    private IReadOnlyDictionary<Guid, BookingWeatherSummary?> _currentWeather = new Dictionary<Guid, BookingWeatherSummary?>();
    private CancellationTokenSource? _queryCancellation;
    private bool _initialized;
    private bool _isBusy;
    private string _statusText = "准备就绪";
    private CalendarViewMode _viewMode = CalendarViewMode.Month;
    private DateTime _selectedDate = DateTime.Today;
    private CalendarStatusOption _selectedStatus;
    private CalendarStatusOption _selectedDetailedStatus;
    private CalendarTypeOption _selectedType;
    private CalendarSearchScopeOption _selectedSearchScope;
    private string _searchText = string.Empty;
    private ShootBookingPageCursor? _nextCursor;
    private bool _isDetailsOpen;
    private bool _isArchivedPaneOpen;
    private Guid? _selectedBookingId;
    private CalendarPresentationOption _selectedPresentation;
    private CalendarSortOption _selectedSort;
    private bool _selectFirstBookingAfterRefresh;

    public WorkCalendarViewModel(IShootBookingService bookingService, RAWSelectionAssistant.Core.Services.Database.IProjectRepository projectRepository,
        IBookingDocumentWorkflowService? documentWorkflow = null, IDialogService? dialogs = null,
        IBookingReminderService? reminderService = null, IBookingReminderScheduler? reminderScheduler = null,
        IWeatherForecastService? weatherService = null, WeatherFeatureState? weatherState = null,
        ICalendarAvailabilityStore? availabilityStore = null,
        IBookingPeopleService? bookingPeopleService = null,
        IFinanceService? financeService = null,
        ICurrentLocationService? currentLocationService = null,
        IBookingTimeDisplayService? timeDisplay = null)
    {
        _bookingService = bookingService;
        _projectRepository = projectRepository;
        _reminderScheduler = reminderScheduler;
        _weatherService = weatherService;
        _weatherState = weatherState;
        _dialogs = dialogs;
        _bookingPeopleService = bookingPeopleService;
        _documentWorkflow = documentWorkflow;
        _timeDisplay = timeDisplay ?? BookingTimeDisplayService.Default;
        _currentLocationService = currentLocationService;
        _availabilityStore = availabilityStore ?? new JsonCalendarAvailabilityStore();
        StatusOptions =
        [
            new("全部状态"), new("空闲", FreeOnly: true),
            new("有拍摄", WorkflowStatus: CalendarWorkflowStatus.Scheduled),
            new("已拍摄", WorkflowStatus: CalendarWorkflowStatus.Shot),
            new("待返图", WorkflowStatus: CalendarWorkflowStatus.PendingDelivery),
            new("已返图", WorkflowStatus: CalendarWorkflowStatus.Delivered)
        ];
        DetailedStatusOptions =
        [
            new("全部详细状态"),
            new("未拍摄", Values: [ShootBookingStatus.Draft, ShootBookingStatus.Tentative, ShootBookingStatus.Confirmed, ShootBookingStatus.Preparing]),
            new("拍摄中", Value: ShootBookingStatus.Shooting),
            new("已拍摄", Value: ShootBookingStatus.Completed),
            new("待发送选片 / 待选片 / 已选片", Values: [ShootBookingStatus.AwaitingSelectionDelivery, ShootBookingStatus.AwaitingSelection, ShootBookingStatus.Selected]),
            new("待精修 / 已精修 / 待交付", Values: [ShootBookingStatus.AwaitingRetouch, ShootBookingStatus.Retouched, ShootBookingStatus.AwaitingDelivery]),
            new("已返图（已交付）", Value: ShootBookingStatus.Delivered),
            new("已取消", Value: ShootBookingStatus.Cancelled),
            new("已延期", Value: ShootBookingStatus.Postponed),
            new("时间冲突", ConflictOnly: true),
            new("天气风险", WeatherRiskOnly: true)
        ];
        TypeOptions =
        [
            new("全部类型", null), new("人像", "Portrait"), new("婚礼", "Wedding"), new("商业", "Commercial"),
            new("活动", "Event"), new("产品", "Product"), new("其他", "Other")
        ];
        SearchScopeOptions = [new("当前视图", BookingSearchScope.CurrentView), new("全部未归档排期", BookingSearchScope.AllUnarchived)];
        PresentationOptions = [new("日历视图", CalendarPresentationMode.Calendar), new("列表视图", CalendarPresentationMode.List), new("总览视图", CalendarPresentationMode.Overview)];
        SortOptions = [new("按拍摄时间", CalendarSortMode.ShootTime), new("按创建时间", CalendarSortMode.CreatedTime), new("按状态", CalendarSortMode.Status), new("按客户或项目", CalendarSortMode.ClientOrProject)];
        _selectedStatus = StatusOptions[0];
        _selectedDetailedStatus = DetailedStatusOptions[0];
        _selectedType = TypeOptions[0];
        _selectedSearchScope = SearchScopeOptions[0];
        _selectedPresentation = PresentationOptions[0];
        _selectedSort = SortOptions[0];

        Month = new MonthCalendarViewModel(SelectDate, OpenBookingAsync, CreateForDate, SetDayClosedAsync, _availabilityStore.IsClosed);
        Week = new WeekCalendarViewModel(SelectDate, OpenBookingAsync, CreateAt);
        Day = new DayCalendarViewModel(OpenBookingAsync, CreateAt);
        DaySchedule = new DaySchedulePanelViewModel(OpenBookingAsync, CreateForDate);
        Details = new ShootBookingDetailsViewModel(bookingService, documentWorkflow, dialogs, reminderService, reminderScheduler, weatherService, weatherState, bookingPeopleService, financeService, currentLocationService, _timeDisplay);
        Details.CloseRequested += (_, _) => IsDetailsOpen = false;
        Details.EditRequested += (_, bookingId) => _ = RequestEditorAsync(bookingId, null);
        Details.Archived += (_, _) => _ = RefreshAsync();
        Details.Completed += (_, _) => _ = RefreshAfterBookingChangeAsync();
        Details.WorkflowStatusChanged += (_, _) => _ = RefreshAfterBookingChangeAsync();
        Details.FinanceRequested += (_, request) => FinanceRequested?.Invoke(this, request);
        Archived = new ArchivedBookingsViewModel(bookingService, _timeDisplay);
        Archived.OpenDetailsRequested += (_, id) => _ = OpenBookingAsync(id, includeArchived: true);
        Archived.Restored += (_, _) => _ = RefreshAsync();

        TodayCommand = new AsyncRelayCommand(_ => GoTodayAsync());
        PreviousCommand = new AsyncRelayCommand(_ => MovePeriodAsync(-1));
        NextCommand = new AsyncRelayCommand(_ => MovePeriodAsync(1));
        SetMonthViewCommand = new AsyncRelayCommand(_ => SetModeAsync(CalendarViewMode.Month));
        SetWeekViewCommand = new AsyncRelayCommand(_ => SetModeAsync(CalendarViewMode.Week));
        SetDayViewCommand = new AsyncRelayCommand(_ => SetModeAsync(CalendarViewMode.Day));
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        NewBookingCommand = new AsyncRelayCommand(_ => RequestEditorAsync(null, DefaultStartForSelectedDate()), _ => !_availabilityStore.IsClosed(SelectedDate));
        LoadMoreCommand = new AsyncRelayCommand(_ => LoadMoreAsync(), _ => HasMoreGlobalResults && !IsBusy);
        OpenBookingCommand = new AsyncRelayCommand(parameter => parameter switch
        {
            ShootBookingSummary item => OpenBookingAsync(item.Id),
            CalendarBookingItemViewModel item => OpenBookingAsync(item.Booking),
            _ => Task.CompletedTask
        });
        ToggleArchivedCommand = new AsyncRelayCommand(_ => ToggleArchivedAsync());
        CloseDetailsCommand = new RelayCommand(_ => IsDetailsOpen = false);
        FocusSearchCommand = new RelayCommand(_ => FocusSearchRequested());
    }

    public event EventHandler<BookingEditorRequestEventArgs>? EditorRequested;
    public event EventHandler? DayDetailsNavigationRequested;
    public event EventHandler<BookingFinanceRequestEventArgs>? FinanceRequested;

    public IReadOnlyList<CalendarStatusOption> StatusOptions { get; }
    public IReadOnlyList<CalendarStatusOption> DetailedStatusOptions { get; }
    public IReadOnlyList<CalendarTypeOption> TypeOptions { get; }
    public IReadOnlyList<CalendarSearchScopeOption> SearchScopeOptions { get; }
    public IReadOnlyList<CalendarPresentationOption> PresentationOptions { get; }
    public IReadOnlyList<CalendarSortOption> SortOptions { get; }
    public ObservableCollection<ShootBookingSummary> GlobalSearchResults { get; } = [];
    public ObservableCollection<ShootBookingSummary> CurrentItems { get; } = [];
    public ObservableCollection<CalendarBookingItemViewModel> GlobalSearchDisplayItems { get; } = [];
    public ObservableCollection<CalendarBookingItemViewModel> CurrentDisplayItems { get; } = [];
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
    public Guid? SelectedBookingId { get => _selectedBookingId; private set => SetProperty(ref _selectedBookingId, value); }
    public bool IsArchivedPaneOpen { get => _isArchivedPaneOpen; private set => SetProperty(ref _isArchivedPaneOpen, value); }
    public CalendarPresentationOption SelectedPresentation
    {
        get => _selectedPresentation;
        set { if (value is null || !SetProperty(ref _selectedPresentation, value)) return; OnPropertyChanged(nameof(IsCalendarPresentation)); OnPropertyChanged(nameof(IsListPresentation)); OnPropertyChanged(nameof(IsOverviewPresentation)); }
    }
    public CalendarSortOption SelectedSort
    {
        get => _selectedSort;
        set { if (value is not null && SetProperty(ref _selectedSort, value)) QueueRefresh(); }
    }
    public bool IsCalendarPresentation => SelectedPresentation.Value == CalendarPresentationMode.Calendar;
    public bool IsListPresentation => SelectedPresentation.Value == CalendarPresentationMode.List;
    public bool IsOverviewPresentation => SelectedPresentation.Value == CalendarPresentationMode.Overview;
    public int UnshotCount => CurrentItems.Count(item => CalendarWorkflowStatusMapper.FromBookingStatus(item.Status) == CalendarWorkflowStatus.Scheduled);
    public int ShotCount => CurrentItems.Count(item => CalendarWorkflowStatusMapper.FromBookingStatus(item.Status) == CalendarWorkflowStatus.Shot);
    public int AwaitingReturnCount => CurrentItems.Count(item => CalendarWorkflowStatusMapper.FromBookingStatus(item.Status) == CalendarWorkflowStatus.PendingDelivery);
    public int DeliveredCount => CurrentItems.Count(item => CalendarWorkflowStatusMapper.FromBookingStatus(item.Status) == CalendarWorkflowStatus.Delivered);

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            var date = value.Date;
            if (!SetProperty(ref _selectedDate, date)) return;
            ClearSelection();
            NotifyDisplayPeriod();
            QueueRefresh();
        }
    }

    public string DisplayPeriod => ViewMode switch
    {
        CalendarViewMode.Month => SelectedDate.ToString("yyyy年M月", CultureInfo.GetCultureInfo("zh-CN")),
        CalendarViewMode.Week => $"{StartOfWeek(SelectedDate):yyyy年M月d日} — {StartOfWeek(SelectedDate).AddDays(6):M月d日}",
        _ => SelectedDate.ToString("yyyy年M月d日 dddd", CultureInfo.GetCultureInfo("zh-CN"))
    };

    public string DisplayYear => $"{SelectedDate:yyyy}年";
    public string DisplayMonth => $"{SelectedDate.Month}月";

    public CalendarStatusOption SelectedStatus
    {
        get => _selectedStatus;
        set { if (value is not null && SetProperty(ref _selectedStatus, value)) QueueRefresh(); }
    }

    public CalendarStatusOption SelectedDetailedStatus
    {
        get => _selectedDetailedStatus;
        set { if (value is not null && SetProperty(ref _selectedDetailedStatus, value)) QueueRefresh(); }
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
        await _availabilityStore.LoadAsync().ConfigureAwait(true);
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
                var page = SelectedStatus.ArchivedOnly
                    ? await _bookingService.SearchArchivedAsync(BuildSearchRequest(null), cancellationToken).ConfigureAwait(true)
                    : await _bookingService.SearchAllUnarchivedAsync(BuildSearchRequest(null), cancellationToken).ConfigureAwait(true);
                var globalItems = ApplyNonWeatherStatusFilter(page.Items).ToArray();
                Replace(GlobalSearchResults, globalItems);
                Replace(GlobalSearchDisplayItems, globalItems.Select(item => new CalendarBookingItemViewModel(item, timeDisplay: _timeDisplay)));
                _nextCursor = page.NextCursor;
                OnPropertyChanged(nameof(HasMoreGlobalResults));
                StatusText = $"已加载 {GlobalSearchResults.Count} 条未归档排期";
                return;
            }

            var (startDate, endDateExclusive) = CurrentDateRange();
            var range = ShootBookingTimeRules.CreateAllDayRange(DateOnly.FromDateTime(startDate), DateOnly.FromDateTime(endDateExclusive), TimeZoneInfo.Local.Id);
            IReadOnlyList<ShootBookingSummary> items;
            if (SelectedStatus.ArchivedOnly)
            {
                var archived = await _bookingService.SearchArchivedAsync(new(SearchText, ShootingType: SelectedType.Value, PageSize: 100), cancellationToken).ConfigureAwait(true);
                items = archived.Items.Where(item => item.EndAtUtc > range.StartAtUtc && item.StartAtUtc < range.EndAtUtc).ToArray();
            }
            else
            {
                items = await _bookingService.QueryCurrentViewAsync(new ShootBookingQuery(
                    range.StartAtUtc, range.EndAtUtc, EffectiveDetailedStatus(), SelectedType.Value, SearchText), cancellationToken).ConfigureAwait(true);
            }
            _currentWeather = await LoadCachedWeatherAsync(items, cancellationToken).ConfigureAwait(true);
            ApplyCurrentView(items, _currentWeather);
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

    public void MoveSelectedDate(int days) => SelectDate(SelectedDate.AddDays(days));
    public void CreateBookingForSelectedDate() => CreateForDate(SelectedDate);

    private ShootBookingSearchRequest BuildSearchRequest(ShootBookingPageCursor? cursor) =>
        new(SearchText, EffectiveDetailedStatus(), SelectedType.Value, cursor, 50);

    private ShootBookingStatus? EffectiveDetailedStatus() => SelectedDetailedStatus.Value ?? SelectedStatus.Value;

    private async Task LoadMoreAsync()
    {
        if (_nextCursor is null) return;
        var cancellationToken = _queryCancellation?.Token ?? CancellationToken.None;
        IsBusy = true;
        try
        {
            var page = await _bookingService.SearchAllUnarchivedAsync(BuildSearchRequest(_nextCursor), cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var item in ApplyNonWeatherStatusFilter(page.Items))
            {
                GlobalSearchResults.Add(item);
                GlobalSearchDisplayItems.Add(new CalendarBookingItemViewModel(item, timeDisplay: _timeDisplay));
            }
            _nextCursor = page.NextCursor;
            OnPropertyChanged(nameof(HasMoreGlobalResults));
            StatusText = $"已加载 {GlobalSearchResults.Count} 条未归档排期";
        }
        catch (OperationCanceledException) { }
        finally { if (!cancellationToken.IsCancellationRequested) IsBusy = false; }
    }

    private void ApplyCurrentView(IReadOnlyList<ShootBookingSummary> items, IReadOnlyDictionary<Guid, BookingWeatherSummary?> weather)
    {
        var filtered = ApplyStatusFilter(items, weather).ToArray();
        var sorted = SortItems(filtered).ToArray();
        Replace(CurrentItems, sorted);
        Replace(CurrentDisplayItems, sorted.Select(item => new CalendarBookingItemViewModel(item, weather.GetValueOrDefault(item.Id), timeDisplay: _timeDisplay)));
        OnPropertyChanged(nameof(UnshotCount)); OnPropertyChanged(nameof(ShotCount)); OnPropertyChanged(nameof(AwaitingReturnCount)); OnPropertyChanged(nameof(DeliveredCount));
        Month.Configure(SelectedDate, sorted, SelectedDate, weather);
        Week.Configure(StartOfWeek(SelectedDate), sorted, SelectedDate, weather);
        Day.Configure(SelectedDate, sorted, weather);
        var selectedDayItems = ItemsOnDate(sorted, SelectedDate);
        DaySchedule.Configure(SelectedDate, selectedDayItems, weather);
        if (_selectFirstBookingAfterRefresh)
        {
            _selectFirstBookingAfterRefresh = false;
            if (selectedDayItems.FirstOrDefault() is { } first) _ = OpenBookingAsync(first);
        }
    }

    private IEnumerable<ShootBookingSummary> SortItems(IEnumerable<ShootBookingSummary> items) => SelectedSort.Value switch
    {
        CalendarSortMode.CreatedTime => items.OrderByDescending(item => item.CreatedAtUtc),
        CalendarSortMode.Status => items.OrderBy(item => item.Status).ThenBy(item => item.StartAtUtc),
        CalendarSortMode.ClientOrProject => items.OrderBy(item => item.ClientDisplayName).ThenBy(item => item.Title),
        _ => items.OrderBy(item => item.StartAtUtc)
    };

    private IEnumerable<ShootBookingSummary> ApplyNonWeatherStatusFilter(IEnumerable<ShootBookingSummary> items)
    {
        var result = items;
        if (SelectedStatus.FreeOnly) return [];
        if (SelectedStatus.WorkflowStatus is { } workflowStatus)
            result = result.Where(item => CalendarWorkflowStatusMapper.FromBookingStatus(item.Status) == workflowStatus);
        if (!string.IsNullOrWhiteSpace(SelectedStatus.BusinessState)) result = result.Where(item => CalendarText.BusinessState(item.Status) == SelectedStatus.BusinessState);
        if (SelectedStatus.ConflictOnly)
        {
            var all = result.ToArray();
            result = all.Where(item => HasCalendarConflict(item, all));
        }
        if (SelectedDetailedStatus.Values is { Count: > 0 } detailedValues)
            result = result.Where(item => detailedValues.Contains(item.Status));
        if (SelectedDetailedStatus.Value is { } detailedValue)
            result = result.Where(item => item.Status == detailedValue);
        if (!string.IsNullOrWhiteSpace(SelectedDetailedStatus.BusinessState))
            result = result.Where(item => CalendarText.BusinessState(item.Status) == SelectedDetailedStatus.BusinessState);
        if (SelectedDetailedStatus.ConflictOnly)
        {
            var all = result.ToArray();
            result = all.Where(item => HasCalendarConflict(item, all));
        }
        return result;
    }

    private IEnumerable<ShootBookingSummary> ApplyStatusFilter(IEnumerable<ShootBookingSummary> items, IReadOnlyDictionary<Guid, BookingWeatherSummary?> weather)
    {
        var result = ApplyNonWeatherStatusFilter(items);
        if (SelectedStatus.WeatherRiskOnly || SelectedDetailedStatus.WeatherRiskOnly)
            result = result.Where(item => weather.GetValueOrDefault(item.Id)?.Risks.Count > 0);
        return result;
    }

    private static bool HasCalendarConflict(ShootBookingSummary candidate, IReadOnlyList<ShootBookingSummary> all) =>
        !candidate.AllowOverlap && all.Any(other => other.Id != candidate.Id && !other.AllowOverlap && candidate.StartAtUtc < other.EndAtUtc && other.StartAtUtc < candidate.EndAtUtc);

    private async Task<IReadOnlyDictionary<Guid, BookingWeatherSummary?>> LoadCachedWeatherAsync(
        IReadOnlyList<ShootBookingSummary> items, CancellationToken cancellationToken)
    {
        if (_weatherService is null || _weatherState is null || !_weatherState.Enabled)
            return new Dictionary<Guid, BookingWeatherSummary?>();

        var results = new Dictionary<Guid, BookingWeatherSummary?>();
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                results[item.Id] = await _weatherService.TryGetCachedBookingWeatherAsync(
                    item.Id, item.StartAtUtc, item.EndAtUtc, cancellationToken).ConfigureAwait(true);
            }
            catch
            {
                results[item.Id] = null;
            }
        }
        return results;
    }

    private static IReadOnlyList<ShootBookingSummary> ItemsOnDate(IEnumerable<ShootBookingSummary> items, DateTime date) =>
        items.Where(item => CalendarBookingItemViewModel.SpansDate(item, date)).OrderBy(item => item.StartAtUtc).ToArray();

    private void SelectDate(DateTime date)
    {
        var previousMonth = (_selectedDate.Year, _selectedDate.Month);
        _selectedDate = date.Date;
        ClearSelection();
        OnPropertyChanged(nameof(SelectedDate));
        NotifyDisplayPeriod();
        if (ViewMode == CalendarViewMode.Month && previousMonth != (_selectedDate.Year, _selectedDate.Month))
        {
            _selectFirstBookingAfterRefresh = true;
            _ = RefreshAsync();
            return;
        }
        Month.Configure(_selectedDate, Month.AllItems, _selectedDate, _currentWeather);
        var selectedDayItems = Month.AllItems.Where(item => CalendarBookingItemViewModel.SpansDate(item, SelectedDate)).OrderBy(item => item.StartAtUtc).ToArray();
        DaySchedule.Configure(SelectedDate, selectedDayItems, _currentWeather);
        if (selectedDayItems.FirstOrDefault() is { } first) _ = OpenBookingAsync(first);
    }

    private void CreateForDate(DateTime date)
    {
        if (_availabilityStore.IsClosed(date)) { StatusText = $"{date:M月d日} 已关闭档期，请先重新开放。"; return; }
        _ = RequestEditorAsync(null, DefaultStart(date));
    }
    private void CreateAt(DateTime dateTime)
    {
        if (_availabilityStore.IsClosed(dateTime)) { StatusText = $"{dateTime:M月d日} 已关闭档期，请先重新开放。"; return; }
        _ = RequestEditorAsync(null, dateTime);
    }

    private async Task SetDayClosedAsync(DateTime date, bool closed)
    {
        var bookings = Month.AllItems.Count(item => CalendarBookingItemViewModel.SpansDate(item, date));
        if (closed && bookings > 0 && _dialogs is not null && !_dialogs.Confirm($"该日期已有 {bookings} 项拍摄任务。关闭档期不会删除或修改已有任务，是否继续？", "关闭本日档期")) return;
        await _availabilityStore.SetClosedAsync(date, closed).ConfigureAwait(true);
        Month.Configure(SelectedDate, Month.AllItems, SelectedDate, _currentWeather);
        StatusText = closed ? $"{date:M月d日} 已关闭新拍摄档期；已有任务保持不变。" : $"{date:M月d日} 已重新开放。";
    }

    private async Task OpenBookingAsync(Guid bookingId, bool includeArchived = false)
    {
        SelectedBookingId = bookingId;
        await Details.LoadAsync(bookingId, includeArchived).ConfigureAwait(true);
        IsDetailsOpen = Details.Booking is not null;
        if (Details.Booking is null) SelectedBookingId = null;
    }

    public Task OpenBookingDetailsAsync(Guid bookingId, bool includeArchived = false) => OpenBookingAsync(bookingId, includeArchived);

    public async Task OpenDayDetailsForDateAsync(DateTime date)
    {
        var target = date.Date;
        if (SelectedDate != target)
        {
            _selectedDate = target;
            ClearSelection();
            OnPropertyChanged(nameof(SelectedDate));
            NotifyDisplayPeriod();
        }
        if (ViewMode != CalendarViewMode.Month)
        {
            ViewMode = CalendarViewMode.Month;
            NotifyDisplayPeriod();
        }
        await RefreshAsync().ConfigureAwait(true);
        if (DaySchedule.Bookings.FirstOrDefault() is { } first)
            await OpenBookingAsync(first.Booking).ConfigureAwait(true);
        DayDetailsNavigationRequested?.Invoke(this, EventArgs.Empty);
    }

    private Task OpenBookingAsync(ShootBookingSummary item)
    {
        if (!CalendarBookingItemViewModel.SpansDate(item, SelectedDate))
            SelectedDate = _timeDisplay.ToBookingTime(item.StartAtUtc, item.TimeZoneId).Date;
        return OpenBookingAsync(item.Id);
    }

    private async Task RequestEditorAsync(Guid? bookingId, DateTime? suggestedStart)
    {
        var editor = new ShootBookingEditorViewModel(_bookingService, _projectRepository, bookingId, suggestedStart, _bookingPeopleService, _documentWorkflow, _dialogs, _weatherService, _weatherState, _currentLocationService);
        await editor.InitializeAsync().ConfigureAwait(true);
        editor.OpenConflictingBookingRequested += async (_, conflictId) => await OpenBookingAsync(conflictId).ConfigureAwait(true);
        editor.Saved += async (_, saved) =>
        {
            _weatherState?.MarkNeedsRefresh(saved.Id);
            if (saved.Status != ShootBookingStatus.Draft && _weatherService is not null && _weatherState?.AutoRefreshEnabled == true)
            {
                try
                {
                    await _weatherService.GetBookingWeatherAsync(saved.Id, saved.StartAtUtc, saved.EndAtUtc, true).ConfigureAwait(true);
                }
                catch
                {
                    // Optional weather must never block saving or opening a booking.
                }
            }
            await RefreshAsync().ConfigureAwait(true);
            if (_reminderScheduler is not null) await _reminderScheduler.RefreshAsync().ConfigureAwait(true);
            await OpenBookingAsync(saved.Id).ConfigureAwait(true);
        };
        EditorRequested?.Invoke(this, new BookingEditorRequestEventArgs(editor));
    }

    private async Task RefreshAfterBookingChangeAsync()
    {
        await RefreshAsync().ConfigureAwait(true);
        if (_reminderScheduler is not null) await _reminderScheduler.RefreshAsync().ConfigureAwait(true);
    }

    private async Task ToggleArchivedAsync()
    {
        IsArchivedPaneOpen = !IsArchivedPaneOpen;
        if (IsArchivedPaneOpen) await Archived.RefreshAsync().ConfigureAwait(true);
    }

    private async Task SetModeAsync(CalendarViewMode mode)
    {
        ViewMode = mode;
        NotifyDisplayPeriod();
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task GoTodayAsync()
    {
        _selectedDate = DateTime.Today;
        ClearSelection();
        OnPropertyChanged(nameof(SelectedDate));
        NotifyDisplayPeriod();
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
        ClearSelection();
        OnPropertyChanged(nameof(SelectedDate));
        NotifyDisplayPeriod();
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
        NotifyDisplayPeriod();
    }

    private void NotifyDisplayPeriod()
    {
        OnPropertyChanged(nameof(DisplayPeriod));
        OnPropertyChanged(nameof(DisplayYear));
        OnPropertyChanged(nameof(DisplayMonth));
    }

    private void ClearSelection()
    {
        SelectedBookingId = null;
        IsDetailsOpen = false;
        Details.Clear();
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
    public CalendarBookingItemViewModel(ShootBookingSummary booking, BookingWeatherSummary? weather = null, DateTime? displayDate = null, IBookingTimeDisplayService? timeDisplay = null)
    {
        Booking = booking;
        Weather = weather;
        var display = timeDisplay ?? BookingTimeDisplayService.Default;
        LocalStart = display.ToBookingTime(booking.StartAtUtc, booking.TimeZoneId).DateTime;
        LocalEnd = display.ToBookingTime(booking.EndAtUtc, booking.TimeZoneId).DateTime;
        DisplayDate = displayDate?.Date;
    }

    public ShootBookingSummary Booking { get; }
    public BookingWeatherSummary? Weather { get; }
    public Guid Id => Booking.Id;
    public string Title => Booking.Title;
    public string ClientDisplayName => Booking.ClientDisplayName;
    public string Location => Booking.Location ?? "未填写地点";
    public DateTime LocalStart { get; }
    public DateTime LocalEnd { get; }
    public DateTime? DisplayDate { get; }
    public bool IsAllDay => Booking.IsAllDay;
    public DateTime LastDisplayDate => LocalEnd.TimeOfDay == TimeSpan.Zero ? LocalEnd.Date.AddDays(-1) : LocalEnd.Date;
    public int TotalDays => Math.Max(1, (LastDisplayDate - LocalStart.Date).Days + 1);
    public int DisplayDayNumber => DisplayDate is null ? 1 : Math.Clamp((DisplayDate.Value - LocalStart.Date).Days + 1, 1, TotalDays);
    public bool IsCrossDay => TotalDays > 1;
    public bool IsCrossDayStart => IsCrossDay && DisplayDayNumber == 1;
    public bool IsCrossDayEnd => IsCrossDay && DisplayDayNumber == TotalDays;
    public string MonthTitle => !IsCrossDay ? Booking.Title : IsCrossDayStart ? $"{Booking.Title} · 共{TotalDays}天" : IsCrossDayEnd ? $"结束 · 第{DisplayDayNumber}天" : $"延续 · 第{DisplayDayNumber}天/{TotalDays}";
    public string CrossDayText => IsCrossDay ? $"跨日排期，第{DisplayDayNumber}天 / 共{TotalDays}天" : string.Empty;
    public string TimeText => IsAllDay ? "全天" : $"{LocalStart:HH:mm}–{LocalEnd:HH:mm}";
    public string StatusText => CalendarText.Status(Booking.Status);
    public string BusinessStateText => CalendarText.BusinessState(Booking.Status);
    public CalendarWorkflowStatus WorkflowStatus => CalendarWorkflowStatusMapper.FromBookingStatus(Booking.Status);
    public string WorkflowStatusText => CalendarWorkflowStatusMapper.DisplayName(WorkflowStatus);
    public string StartText => LocalStart.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    public string StatusGlyph => Booking.Status switch
    {
        ShootBookingStatus.Completed or ShootBookingStatus.Shooting => "●",
        ShootBookingStatus.AwaitingSelectionDelivery or ShootBookingStatus.AwaitingSelection or ShootBookingStatus.Selected or ShootBookingStatus.AwaitingRetouch or ShootBookingStatus.Retouched or ShootBookingStatus.AwaitingDelivery => "◆",
        ShootBookingStatus.Delivered => "✓",
        ShootBookingStatus.Cancelled => "×",
        ShootBookingStatus.Confirmed => "●",
        ShootBookingStatus.Preparing => "◇",
        ShootBookingStatus.Postponed => "↷",
        _ => "○"
    };
    public string WeatherIcon => WeatherIconFor(Weather?.RepresentativeHour?.WeatherCode ?? Weather?.Day?.WeatherCode);
    public bool HasWeatherRisk => Weather?.Risks.Count > 0;
    public string MonthWeatherIcon => HasWeatherRisk ? WeatherIcon : string.Empty;
    public string WeatherText
    {
        get
        {
            if (Weather?.RepresentativeHour is { } hour)
                return $"{WeatherIcon} {hour.TemperatureC:0}° · 降雨 {hour.PrecipitationProbability}% · 风 {hour.WindSpeedKph:0} km/h";
            if (Weather?.Day is { } day)
                return $"{WeatherIcon} {day.MinimumTemperatureC:0}–{day.MaximumTemperatureC:0}° · 降雨 {day.PrecipitationProbability}%";
            return string.Empty;
        }
    }
    public string AccessibilityName => string.Join("，", new[] { Title, ClientDisplayName, TimeText, StatusText, CrossDayText, WeatherText }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public static bool SpansDate(ShootBookingSummary booking, DateTime date)
    {
        var item = new CalendarBookingItemViewModel(booking);
        var last = item.LocalEnd.TimeOfDay == TimeSpan.Zero ? item.LocalEnd.Date.AddDays(-1) : item.LocalEnd.Date;
        return date.Date >= item.LocalStart.Date && date.Date <= last;
    }

    public static string WeatherIconFor(string? code)
    {
        if (!int.TryParse(code, out var value)) return "☁";
        return value switch
        {
            0 => "☀",
            1 or 2 => "🌤",
            3 => "☁",
            45 or 48 => "🌫",
            51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "🌧",
            71 or 73 or 75 or 77 or 85 or 86 => "🌨",
            95 or 96 or 99 => "⛈",
            _ => "☁"
        };
    }
}

public sealed class MonthDayViewModel
{
    private static readonly IReadOnlyDictionary<CalendarWorkflowStatus, int> PrimaryStatusPriority = new Dictionary<CalendarWorkflowStatus, int>
    {
        [CalendarWorkflowStatus.Scheduled] = 0,
        [CalendarWorkflowStatus.Shot] = 1,
        [CalendarWorkflowStatus.PendingDelivery] = 2,
        [CalendarWorkflowStatus.Delivered] = 3
    };

    public DateTime Date { get; init; }
    public int DayNumber => Date.Day;
    public bool IsCurrentMonth { get; init; }
    public bool IsToday => Date == DateTime.Today;
    public bool IsSelected { get; init; }
    public bool IsClosed { get; init; }
    public ObservableCollection<CalendarBookingItemViewModel> VisibleBookings { get; } = [];
    public int OverflowCount { get; init; }
    public int BookingCount => VisibleBookings.Count + OverflowCount;
    public string BookingCountText => BookingCount == 0 ? string.Empty : $"{BookingCount}场";
    public bool HasBookings => BookingCount > 0;
    public string PrimaryBusinessState => PrimaryBooking?.BusinessStateText ?? string.Empty;
    public CalendarWorkflowStatus PrimaryWorkflowStatus => PrimaryBooking?.WorkflowStatus ?? CalendarWorkflowStatus.Scheduled;
    public IReadOnlyList<CalendarWorkflowStatus> WorkflowSegments => VisibleBookings.Select(item => item.WorkflowStatus).Distinct().Take(3).ToArray();
    public bool HasMixedWorkflowStatuses => WorkflowSegments.Count > 1;
    public string PrimaryStatusGlyph => VisibleBookings.FirstOrDefault()?.StatusGlyph ?? string.Empty;
    public bool HasMultipleBookings => BookingCount > 1;
    public string BookingCountBadgeText => HasMultipleBookings ? BookingCount.ToString(CultureInfo.InvariantCulture) : string.Empty;
    public string WeatherGlyph => VisibleBookings.Select(item => item.MonthWeatherIcon).FirstOrDefault(value => !string.IsNullOrEmpty(value)) ?? string.Empty;
    public bool HasConflict { get; init; }
    public string ConflictGlyph => HasConflict ? "!" : string.Empty;
    public string OverflowText => OverflowCount > 0 ? $"另有 {OverflowCount} 项" : string.Empty;
    public string TooltipText
    {
        get
        {
            var lines = new List<string> { $"{Date:M月d日}", BookingCount == 0 ? "暂无拍摄" : $"{BookingCount}场拍摄" };
            lines.AddRange(VisibleBookings.Select(item => $"• {item.Title}：{item.WorkflowStatusText}"));
            if (OverflowCount > 0) lines.Add($"• 另有 {OverflowCount} 场拍摄");
            return string.Join(Environment.NewLine, lines);
        }
    }
    public string AccessibilityName => $"{Date:yyyy年M月d日}，{BookingCount}项排期" + (HasConflict ? "，存在时间冲突" : string.Empty);

    private CalendarBookingItemViewModel? PrimaryBooking => VisibleBookings
        .OrderBy(item => PrimaryStatusPriority.GetValueOrDefault(item.WorkflowStatus, int.MaxValue))
        .ThenBy(item => item.LocalStart)
        .FirstOrDefault();
}

public sealed class MonthCalendarViewModel
{
    private readonly Action<DateTime> _selectDate;
    private readonly Func<DateTime, bool, Task> _setClosed;
    private readonly Func<DateTime, bool> _isClosed;
    public MonthCalendarViewModel(Action<DateTime> selectDate, Func<ShootBookingSummary, Task> openBooking, Action<DateTime> create,
        Func<DateTime, bool, Task>? setClosed = null, Func<DateTime, bool>? isClosed = null)
    {
        _selectDate = selectDate;
        _setClosed = setClosed ?? ((_, _) => Task.CompletedTask);
        _isClosed = isClosed ?? (_ => false);
        OpenBookingCommand = new AsyncRelayCommand(async parameter =>
        {
            if (parameter is not CalendarBookingItemViewModel item) return;
            _selectDate(item.DisplayDate ?? item.LocalStart.Date);
            await openBooking(item.Booking).ConfigureAwait(true);
        });
        SelectDateCommand = new RelayCommand(parameter => { if (parameter is MonthDayViewModel day) _selectDate(day.Date); });
        CreateBookingCommand = new RelayCommand(parameter => { if (parameter is MonthDayViewModel day) create(day.Date); }, parameter => parameter is MonthDayViewModel day && !day.IsClosed);
        CloseDayCommand = new AsyncRelayCommand(parameter => parameter is MonthDayViewModel day ? _setClosed(day.Date, true) : Task.CompletedTask);
        OpenDayCommand = new AsyncRelayCommand(parameter => parameter is MonthDayViewModel day ? _setClosed(day.Date, false) : Task.CompletedTask);
    }

    public ObservableCollection<MonthDayViewModel> Days { get; } = [];
    public IReadOnlyList<ShootBookingSummary> AllItems { get; private set; } = [];
    public ICommand OpenBookingCommand { get; }
    public ICommand SelectDateCommand { get; }
    public ICommand CreateBookingCommand { get; }
    public ICommand CloseDayCommand { get; }
    public ICommand OpenDayCommand { get; }

    public void Configure(DateTime month, IReadOnlyList<ShootBookingSummary> items, DateTime selectedDate,
        IReadOnlyDictionary<Guid, BookingWeatherSummary?>? weather = null)
    {
        AllItems = items;
        Days.Clear();
        var start = WorkCalendarViewModel.MonthGridStart(month);
        for (var offset = 0; offset < 42; offset++)
        {
            var date = start.AddDays(offset);
            var matches = items.Where(item => CalendarBookingItemViewModel.SpansDate(item, date))
                .Select(item => new CalendarBookingItemViewModel(item, weather?.GetValueOrDefault(item.Id), date))
                .OrderBy(item => item.LocalStart).ToArray();
            var day = new MonthDayViewModel { Date = date, IsCurrentMonth = date.Month == month.Month && date.Year == month.Year, IsSelected = date == selectedDate.Date, IsClosed = _isClosed(date), OverflowCount = Math.Max(0, matches.Length - 3), HasConflict = HasConflict(matches) };
            foreach (var match in matches.Take(3)) day.VisibleBookings.Add(match);
            Days.Add(day);
        }
    }

    private static bool HasConflict(IReadOnlyList<CalendarBookingItemViewModel> items)
    {
        for (var left = 0; left < items.Count; left++)
            for (var right = left + 1; right < items.Count; right++)
                if (!items[left].Booking.AllowOverlap && !items[right].Booking.AllowOverlap &&
                    items[left].LocalStart < items[right].LocalEnd && items[right].LocalStart < items[left].LocalEnd)
                    return true;
        return false;
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

    public void Configure(DateTime weekStart, IReadOnlyList<ShootBookingSummary> items, DateTime selectedDate,
        IReadOnlyDictionary<Guid, BookingWeatherSummary?>? weather = null)
    {
        Days.Clear();
        for (var i = 0; i < 7; i++)
        {
            var date = weekStart.AddDays(i);
            var day = new CalendarDayColumnViewModel { Date = date };
            foreach (var source in items.Where(item => CalendarBookingItemViewModel.SpansDate(item, date))
                         .Select(item => new CalendarBookingItemViewModel(item, weather?.GetValueOrDefault(item.Id)))
                         .OrderBy(item => item.LocalStart))
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

    public void Configure(DateTime date, IReadOnlyList<ShootBookingSummary> items,
        IReadOnlyDictionary<Guid, BookingWeatherSummary?>? weather = null)
    {
        Date = date.Date;
        AllDayBookings.Clear();
        TimeSlots.Clear();
        var wrappers = items.Where(item => CalendarBookingItemViewModel.SpansDate(item, Date))
            .Select(item => new CalendarBookingItemViewModel(item, weather?.GetValueOrDefault(item.Id))).ToArray();
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
    private readonly Action<DateTime> _create;
    public DaySchedulePanelViewModel(Func<ShootBookingSummary, Task> openBooking, Action<DateTime> create)
    {
        _create = create;
        OpenBookingCommand = new AsyncRelayCommand(parameter => parameter is CalendarBookingItemViewModel item ? openBooking(item.Booking) : Task.CompletedTask);
        NewBookingCommand = new RelayCommand(_ => _create(Date));
    }
    public DateTime Date { get => _date; private set { if (!SetProperty(ref _date, value)) return; foreach (var name in new[] { nameof(Title), nameof(DateNumberText), nameof(MonthYearText), nameof(WeekdayText), nameof(EmptyText) }) OnPropertyChanged(name); } }
    public string Title => $"{Date:M月d日} 当天详情";
    public string DateNumberText => Date.Day.ToString(CultureInfo.InvariantCulture);
    public string MonthYearText => Date.ToString("yyyy年M月", CultureInfo.GetCultureInfo("zh-CN"));
    public string WeekdayText => Date.ToString("dddd", CultureInfo.GetCultureInfo("zh-CN"));
    public string EmptyText => $"{Date:M月d日}暂无拍摄任务";
    public ObservableCollection<CalendarBookingItemViewModel> Bookings { get; } = [];
    public IEnumerable<CalendarBookingItemViewModel> VisibleBookings => Bookings.Take(2);
    public int OverflowCount => Math.Max(0, Bookings.Count - 2);
    public string OverflowText => OverflowCount > 0 ? $"还有 {OverflowCount} 项 · 查看完整日历" : string.Empty;
    public ICommand OpenBookingCommand { get; }
    public ICommand NewBookingCommand { get; }
    public int BookingCount => Bookings.Count;
    public int UnshotCount => Bookings.Count(item => item.BusinessStateText == "未拍摄");
    public int ShotCount => Bookings.Count(item => item.BusinessStateText == "已拍摄");
    public int AwaitingReturnCount => Bookings.Count(item => item.BusinessStateText == "待返图");
    public int ConflictCount
    {
        get
        {
            var count = 0;
            for (var left = 0; left < Bookings.Count; left++)
                if (!Bookings[left].Booking.AllowOverlap && Bookings.Where((_, index) => index != left).Any(other => !other.Booking.AllowOverlap && Bookings[left].LocalStart < other.LocalEnd && other.LocalStart < Bookings[left].LocalEnd)) count++;
            return count;
        }
    }
    public void Configure(DateTime date, IReadOnlyList<ShootBookingSummary> items,
        IReadOnlyDictionary<Guid, BookingWeatherSummary?>? weather = null)
    {
        Date = date.Date;
        Bookings.Clear();
        foreach (var item in items.Select(item => new CalendarBookingItemViewModel(item, weather?.GetValueOrDefault(item.Id))).OrderBy(item => item.LocalStart)) Bookings.Add(item);
        foreach (var name in new[] { nameof(BookingCount), nameof(UnshotCount), nameof(ShotCount), nameof(AwaitingReturnCount), nameof(ConflictCount), nameof(VisibleBookings), nameof(OverflowCount), nameof(OverflowText) }) OnPropertyChanged(name);
    }
}

public sealed class ArchivedBookingItemViewModel(ShootBookingSummary booking, IBookingTimeDisplayService timeDisplay)
{
    public ShootBookingSummary Booking { get; } = booking;
    public Guid Id => Booking.Id;
    public string Title => Booking.Title;
    public string ClientDisplayName => Booking.ClientDisplayName;
    public string Location => Booking.Location ?? "未填写地点";
    public string StartText => timeDisplay.ToBookingTime(Booking.StartAtUtc, Booking.TimeZoneId).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    public string StatusText => CalendarText.Status(Booking.Status);
}

public sealed class ArchivedBookingsViewModel : ObservableObject
{
    private readonly IShootBookingService _service;
    private readonly IBookingTimeDisplayService _timeDisplay;
    private ShootBookingPageCursor? _nextCursor;
    private bool _isBusy;
    private string _searchText = string.Empty;
    private string _statusText = string.Empty;
    public ArchivedBookingsViewModel(IShootBookingService service, IBookingTimeDisplayService? timeDisplay = null)
    {
        _service = service;
        _timeDisplay = timeDisplay ?? BookingTimeDisplayService.Default;
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        LoadMoreCommand = new AsyncRelayCommand(_ => LoadMoreAsync(), _ => HasMore && !IsBusy);
        OpenCommand = new RelayCommand(parameter => { if (parameter is ArchivedBookingItemViewModel item) OpenDetailsRequested?.Invoke(this, item.Id); });
        RestoreCommand = new AsyncRelayCommand(parameter => parameter is ArchivedBookingItemViewModel item ? RestoreAsync(item.Id) : Task.CompletedTask);
    }

    public event EventHandler<Guid>? OpenDetailsRequested;
    public event EventHandler<Guid>? Restored;
    public ObservableCollection<ArchivedBookingItemViewModel> Items { get; } = [];
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
            foreach (var item in page.Items) Items.Add(new(item, _timeDisplay));
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
            foreach (var item in page.Items) Items.Add(new(item, _timeDisplay));
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
        ShootBookingStatus.Draft => "草稿", ShootBookingStatus.Tentative => "待确定", ShootBookingStatus.Confirmed => "已确认", ShootBookingStatus.Preparing => "准备中",
        ShootBookingStatus.Shooting => "拍摄中", ShootBookingStatus.Completed => "已拍摄", ShootBookingStatus.Cancelled => "已取消",
        ShootBookingStatus.Postponed => "已延期", ShootBookingStatus.AwaitingSelectionDelivery => "待发送选片", ShootBookingStatus.AwaitingSelection => "待选片",
        ShootBookingStatus.Selected => "已选片", ShootBookingStatus.AwaitingRetouch => "待精修", ShootBookingStatus.Retouched => "已精修",
        ShootBookingStatus.AwaitingDelivery => "待交付", ShootBookingStatus.Delivered => "已返图", _ => "未知状态"
    };

    public static string BusinessState(ShootBookingStatus status) => status switch
    {
        ShootBookingStatus.Shooting or ShootBookingStatus.Completed => "已拍摄",
        ShootBookingStatus.AwaitingSelectionDelivery or ShootBookingStatus.AwaitingSelection or ShootBookingStatus.Selected or ShootBookingStatus.AwaitingRetouch or ShootBookingStatus.Retouched or ShootBookingStatus.AwaitingDelivery => "待返图",
        ShootBookingStatus.Delivered => "已返图",
        ShootBookingStatus.Cancelled => "已取消",
        _ => "未拍摄"
    };

    public static string Type(string type) => type switch
    {
        "Portrait" => "人像", "Wedding" => "婚礼", "Commercial" => "商业", "Event" => "活动", "Product" => "产品", "Other" => "其他", _ => type
    };
}
