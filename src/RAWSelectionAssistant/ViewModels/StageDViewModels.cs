using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed record ReminderPresetOption(string Label, int? OffsetMinutes, bool IsCustom = false);

public sealed class BookingReminderItemViewModel(ReminderDefinition reminder, TimeZoneInfo localTimeZone, TimeProvider timeProvider)
{
    public ReminderDefinition Reminder { get; } = reminder;
    public Guid Id => Reminder.Id;
    public string TypeText => Reminder.Trigger.Kind == ReminderTriggerKind.AbsoluteTime ? "自定义时间" : OffsetText(Reminder.Trigger.Offset);
    public string PlannedTimeText => Reminder.Trigger.At is { } at ? TimeZoneInfo.ConvertTime(at, localTimeZone).ToString("yyyy-MM-dd HH:mm") : "未设置";
    public string RelativeTimeText => Reminder.Trigger.At is { } at ? Relative(at, timeProvider.GetUtcNow()) : string.Empty;
    public string StatusText => Reminder.Status switch
    {
        ReminderStatus.Disabled => "已关闭", ReminderStatus.Scheduled => "已启用", ReminderStatus.Triggered => "已触发",
        ReminderStatus.Dismissed => "已确认", ReminderStatus.Cancelled => "已取消", _ => Reminder.Status.ToString()
    };
    public bool IsEnabled => Reminder.IsEnabled && Reminder.Status == ReminderStatus.Scheduled;
    public bool CanToggle => Reminder.Status is ReminderStatus.Disabled or ReminderStatus.Scheduled;
    public string LastTriggeredText => Reminder.LastTriggeredAt is { } at ? TimeZoneInfo.ConvertTime(at, localTimeZone).ToString("yyyy-MM-dd HH:mm") : "尚未触发";

    private static string OffsetText(TimeSpan? offset) => offset?.TotalMinutes switch
    {
        60 => "提前1小时", 180 => "提前3小时", 1440 => "提前1天",
        { } minutes => $"提前{minutes:0}分钟", _ => "相对时间"
    };

    private static string Relative(DateTimeOffset at, DateTimeOffset now)
    {
        var span = at - now;
        if (span < TimeSpan.Zero) return $"已过去 {Format(-span)}";
        return $"还有 {Format(span)}";
    }

    private static string Format(TimeSpan span) => span.TotalDays >= 1 ? $"{span.TotalDays:0.#}天" : span.TotalHours >= 1 ? $"{span.TotalHours:0.#}小时" : $"{Math.Max(1, span.TotalMinutes):0}分钟";
}

public sealed class BookingRemindersViewModel : ObservableObject
{
    private readonly IBookingReminderService _service;
    private readonly IBookingReminderScheduler? _scheduler;
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _localTimeZone;
    private Guid _bookingId;
    private Guid? _projectId;
    private bool _isReadOnly;
    private ReminderPresetOption _selectedPreset;
    private DateTime _customDate = DateTime.Today;
    private string _customTimeText = "09:00";
    private bool _newReminderEnabled;
    private string _statusText = string.Empty;
    private bool _isBusy;
    private Guid? _editingReminderId;
    private DateTimeOffset? _editingCreatedAt;

    public BookingRemindersViewModel(IBookingReminderService service, IBookingReminderScheduler? scheduler = null, TimeProvider? timeProvider = null, TimeZoneInfo? localTimeZone = null)
    {
        _service = service;
        _scheduler = scheduler;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _localTimeZone = localTimeZone ?? TimeZoneInfo.Local;
        Presets =
        [
            new("提前1天", 1440), new("提前3小时", 180), new("提前1小时", 60), new("自定义时间", null, true)
        ];
        _selectedPreset = Presets[2];
        AddCommand = new AsyncRelayCommand(_ => AddAsync(), _ => CanEdit && !IsBusy);
        EditCommand = new RelayCommand(Edit, parameter => CanEdit && parameter is BookingReminderItemViewModel);
        CancelEditCommand = new RelayCommand(_ => ResetEditor(), _ => EditingReminderId.HasValue);
        ToggleCommand = new AsyncRelayCommand(ToggleAsync, parameter => CanEdit && parameter is BookingReminderItemViewModel);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, parameter => CanEdit && parameter is BookingReminderItemViewModel);
        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync(_bookingId, _projectId, _isReadOnly), _ => _bookingId != Guid.Empty && !IsBusy);
    }

    public IReadOnlyList<ReminderPresetOption> Presets { get; }
    public ObservableCollection<BookingReminderItemViewModel> Items { get; } = [];
    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand ToggleCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RefreshCommand { get; }
    public ReminderPresetOption SelectedPreset { get => _selectedPreset; set { if (value is not null && SetProperty(ref _selectedPreset, value)) OnPropertyChanged(nameof(IsCustomPreset)); } }
    public bool IsCustomPreset => SelectedPreset.IsCustom;
    public DateTime CustomDate { get => _customDate; set => SetProperty(ref _customDate, value.Date); }
    public string CustomTimeText { get => _customTimeText; set => SetProperty(ref _customTimeText, value ?? string.Empty); }
    public bool NewReminderEnabled { get => _newReminderEnabled; set => SetProperty(ref _newReminderEnabled, value); }
    public bool IsReadOnly { get => _isReadOnly; private set { if (SetProperty(ref _isReadOnly, value)) OnPropertyChanged(nameof(CanEdit)); } }
    public bool CanEdit => !IsReadOnly;
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string EmptyText => IsReadOnly ? "此归档排期没有提醒记录。" : "尚未添加提醒；新提醒默认关闭。";
    public Guid? EditingReminderId { get => _editingReminderId; private set { if (!SetProperty(ref _editingReminderId, value)) return; OnPropertyChanged(nameof(IsEditing)); OnPropertyChanged(nameof(SaveButtonText)); } }
    public bool IsEditing => EditingReminderId.HasValue;
    public string SaveButtonText => IsEditing ? "保存修改" : "添加提醒";

    public async Task LoadAsync(Guid bookingId, Guid? projectId, bool isReadOnly)
    {
        _bookingId = bookingId;
        _projectId = projectId;
        IsReadOnly = isReadOnly;
        IsBusy = true;
        try
        {
            Items.Clear();
            foreach (var reminder in await _service.ListAsync(bookingId).ConfigureAwait(true)) Items.Add(new(reminder, _localTimeZone, _timeProvider));
            StatusText = string.Empty;
            OnPropertyChanged(nameof(EmptyText));
        }
        catch (Exception ex) { StatusText = $"加载提醒失败：{ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task AddAsync()
    {
        StatusText = string.Empty;
        ReminderTrigger trigger;
        if (SelectedPreset.IsCustom)
        {
            if (!TimeOnly.TryParse(CustomTimeText, CultureInfo.CurrentCulture, DateTimeStyles.None, out var time)) { StatusText = "自定义时间格式应为 HH:mm。"; return; }
            var local = DateTime.SpecifyKind(CustomDate.Add(time.ToTimeSpan()), DateTimeKind.Unspecified);
            if (_localTimeZone.IsInvalidTime(local)) { StatusText = "该本地时间因夏令时切换而不存在。"; return; }
            var offset = _localTimeZone.IsAmbiguousTime(local) ? _localTimeZone.GetAmbiguousTimeOffsets(local).Max() : _localTimeZone.GetUtcOffset(local);
            trigger = new(ReminderTriggerKind.AbsoluteTime, new DateTimeOffset(local, offset).ToUniversalTime(), null);
        }
        else
        {
            trigger = new(ReminderTriggerKind.RelativeToBookingStart, null, TimeSpan.FromMinutes(SelectedPreset.OffsetMinutes ?? 0));
        }
        IsBusy = true;
        try
        {
            await _service.SaveAsync(new(EditingReminderId ?? Guid.NewGuid(), _projectId, string.Empty, string.Empty, trigger,
                NewReminderEnabled ? ReminderStatus.Scheduled : ReminderStatus.Disabled, _bookingId, NewReminderEnabled, CreatedAt: _editingCreatedAt), default).ConfigureAwait(true);
            ResetEditor();
            await LoadAsync(_bookingId, _projectId, _isReadOnly).ConfigureAwait(true);
            if (_scheduler is not null) await _scheduler.RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex) { StatusText = $"保存提醒失败：{ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void Edit(object? parameter)
    {
        if (parameter is not BookingReminderItemViewModel item) return;
        EditingReminderId = item.Id;
        _editingCreatedAt = item.Reminder.CreatedAt;
        NewReminderEnabled = item.IsEnabled;
        if (item.Reminder.Trigger.Kind == ReminderTriggerKind.AbsoluteTime && item.Reminder.Trigger.At is { } at)
        {
            SelectedPreset = Presets.Single(x => x.IsCustom);
            var local = TimeZoneInfo.ConvertTime(at, _localTimeZone);
            CustomDate = local.Date;
            CustomTimeText = local.ToString("HH:mm");
        }
        else
        {
            var minutes = (int)Math.Round(item.Reminder.Trigger.Offset?.TotalMinutes ?? 0);
            SelectedPreset = Presets.FirstOrDefault(x => x.OffsetMinutes == minutes && !x.IsCustom) ?? Presets[0];
        }
        StatusText = "正在编辑提醒；保存不会自动改变拍摄排期。";
    }

    private void ResetEditor()
    {
        EditingReminderId = null;
        _editingCreatedAt = null;
        NewReminderEnabled = false;
        SelectedPreset = Presets[2];
        StatusText = string.Empty;
    }

    private async Task ToggleAsync(object? parameter)
    {
        if (parameter is not BookingReminderItemViewModel item) return;
        IsBusy = true;
        try
        {
            if (!await _service.SetEnabledAsync(item.Id, !item.IsEnabled).ConfigureAwait(true)) StatusText = "提醒状态未更新；请确认排期尚未归档或取消。";
            await LoadAsync(_bookingId, _projectId, _isReadOnly).ConfigureAwait(true);
            if (_scheduler is not null) await _scheduler.RefreshAsync().ConfigureAwait(true);
        }
        finally { IsBusy = false; }
    }

    private async Task DeleteAsync(object? parameter)
    {
        if (parameter is not BookingReminderItemViewModel item) return;
        IsBusy = true;
        try
        {
            await _service.DeleteAsync(item.Id).ConfigureAwait(true);
            await LoadAsync(_bookingId, _projectId, _isReadOnly).ConfigureAwait(true);
        }
        finally { IsBusy = false; }
    }
}

public sealed class WorkbenchScheduleItemViewModel(WorkbenchScheduleItem item, TimeZoneInfo localTimeZone, TimeProvider timeProvider,
    BookingWeatherSummary? weather = null, bool preferDailyWeather = false)
{
    public Guid BookingId => item.BookingId;
    public string Title => string.IsNullOrWhiteSpace(item.ProjectName) ? item.Title : item.ProjectName;
    public string TimeText
    {
        get
        {
            var start = TimeZoneInfo.ConvertTime(item.StartAtUtc, localTimeZone);
            var end = TimeZoneInfo.ConvertTime(item.EndAtUtc, localTimeZone);
            return item.IsAllDay ? "全天" : $"{start:HH:mm}–{end:HH:mm}";
        }
    }
    public string StatusText => item.IsOngoing ? "进行中" : CalendarText.Status(item.Status);
    public string ProjectName => item.ProjectName;
    public string LocationText => item.LocationDisplay;
    public string PreparationText => item.RequirementTotal == 0 ? "准备清单未设置" : $"准备 {item.RequirementCompleted}/{item.RequirementTotal}";
    public string ReminderText => item.HasEnabledReminder ? "提醒已开" : "提醒关闭";
    public string DocumentText => $"文档 {item.DocumentCount}";
    public string WeatherIcon => CalendarBookingItemViewModel.WeatherIconFor(weather?.RepresentativeHour?.WeatherCode ?? weather?.Day?.WeatherCode);
    public string WeatherText => preferDailyWeather && weather?.Day is { } futureDay
        ? $"{WeatherIcon} {futureDay.MinimumTemperatureC:0.#}–{futureDay.MaximumTemperatureC:0.#}°C · 降雨 {futureDay.PrecipitationProbability}%"
        : weather?.RepresentativeHour is { } hour
            ? $"{WeatherIcon} {hour.TemperatureC:0.#}°C · 降雨 {hour.PrecipitationProbability}% · 风 {hour.WindSpeedKph:0.#} km/h{(weather.Risks.Any(risk => risk.Code == "StrongWind") ? " · 强风风险" : string.Empty)}"
            : weather?.Day is { } day ? $"{WeatherIcon} {day.MinimumTemperatureC:0.#}–{day.MaximumTemperatureC:0.#}°C · 降雨 {day.PrecipitationProbability}%" : string.Empty;
    public string DistanceText
    {
        get
        {
            if (item.IsOngoing) return "正在进行";
            var span = item.StartAtUtc - timeProvider.GetUtcNow();
            if (span <= TimeSpan.Zero) return "已结束";
            return span.TotalDays >= 1 ? $"还有 {span.TotalDays:0.#} 天" : span.TotalHours >= 1 ? $"还有 {span.TotalHours:0.#} 小时" : $"还有 {Math.Max(1, span.TotalMinutes):0} 分钟";
        }
    }
    public string MetaText => string.Join(" · ", new[] { LocationText, ReminderText, DocumentText, PreparationText, WeatherText }.Where(value => !string.IsNullOrWhiteSpace(value)));
}

public sealed class WorkbenchScheduleDayViewModel(WorkbenchScheduleDay day, TimeZoneInfo localTimeZone, TimeProvider timeProvider, IReadOnlyDictionary<Guid, BookingWeatherSummary?>? weather = null)
{
    public string DateText => day.Date.ToDateTime(TimeOnly.MinValue).ToString("M月d日 dddd", CultureInfo.GetCultureInfo("zh-CN"));
    public IReadOnlyList<WorkbenchScheduleItemViewModel> Items { get; } = day.Items.Select(x => new WorkbenchScheduleItemViewModel(x, localTimeZone, timeProvider, weather is not null && weather.TryGetValue(x.BookingId, out var summary) ? summary : null, true)).ToArray();
}

public class WorkbenchScheduleViewModel : ObservableObject, IDisposable
{
    private readonly IWorkbenchScheduleService _service;
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _localTimeZone;
    private readonly IBookingChangeNotifier? _bookingChanges;
    private readonly IWeatherForecastService? _weatherService;
    private ITimer? _timer;
    private bool _showFuture;
    private bool _isBusy;
    private string _statusText = string.Empty;
    private DateOnly _loadedDate;
    private int _futureTotalCount;

    public WorkbenchScheduleViewModel(IWorkbenchScheduleService service, IBookingChangeNotifier? bookingChanges = null, TimeProvider? timeProvider = null, TimeZoneInfo? localTimeZone = null, IWeatherForecastService? weatherService = null)
    {
        _service = service;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _localTimeZone = localTimeZone ?? TimeZoneInfo.Local;
        _bookingChanges = bookingChanges;
        _weatherService = weatherService;
        if (_bookingChanges is not null) _bookingChanges.BookingChanged += BookingChanges_BookingChanged;
        ShowTodayCommand = new RelayCommand(_ => ShowFuture = false);
        ShowFutureCommand = new RelayCommand(_ => ShowFuture = true);
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync(), _ => !IsBusy);
        OpenBookingCommand = new AsyncRelayCommand(OpenBookingAsync, parameter => parameter is WorkbenchScheduleItemViewModel);
        OpenCalendarCommand = new RelayCommand(_ => OpenCalendarRequested?.Invoke(this, EventArgs.Empty));
    }

    public ObservableCollection<WorkbenchScheduleItemViewModel> Today { get; } = [];
    public ObservableCollection<WorkbenchScheduleDayViewModel> Future { get; } = [];
    public event EventHandler<Guid>? OpenBookingRequested;
    public event EventHandler? OpenCalendarRequested;
    public ICommand ShowTodayCommand { get; }
    public ICommand ShowFutureCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenBookingCommand { get; }
    public ICommand OpenCalendarCommand { get; }
    public bool ShowFuture { get => _showFuture; set { if (!SetProperty(ref _showFuture, value)) return; OnPropertyChanged(nameof(ShowToday)); OnPropertyChanged(nameof(ShowTodayEmpty)); OnPropertyChanged(nameof(ShowFutureEmpty)); } }
    public bool ShowToday => !ShowFuture;
    public bool ShowTodayEmpty => ShowToday && Today.Count == 0;
    public bool ShowFutureEmpty => ShowFuture && Future.Count == 0;
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string TodayCountText => $"今日 {Today.Count}";
    public string FutureCountText => $"未来7天 {_futureTotalCount}";
    public string NextTodayText => Today.FirstOrDefault(item => !item.StatusText.Equals("已完成", StringComparison.Ordinal)) is { } next ? $"下一场：{next.TimeText} · {next.ProjectName}" : "今天没有待开始的拍摄";

    public async Task InitializeAsync()
    {
        await RefreshAsync().ConfigureAwait(true);
        _timer ??= _timeProvider.CreateTimer(_ => _ = RefreshIfDateChangedAsync(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var snapshot = await _service.LoadAsync().ConfigureAwait(true);
            var all = snapshot.Today.Concat(snapshot.FutureSevenDays.SelectMany(day => day.Items)).DistinctBy(item => item.BookingId).ToArray();
            var weatherPairs = await Task.WhenAll(all.Select(async item => new KeyValuePair<Guid, BookingWeatherSummary?>(item.BookingId,
                await TryGetCachedWeatherAsync(item).ConfigureAwait(true)))).ConfigureAwait(true);
            var weather = weatherPairs.ToDictionary(pair => pair.Key, pair => pair.Value);
            Today.Clear();
            foreach (var item in snapshot.Today) Today.Add(new(item, _localTimeZone, _timeProvider, weather.GetValueOrDefault(item.BookingId)));
            Future.Clear();
            foreach (var day in snapshot.FutureSevenDays) Future.Add(new(day, _localTimeZone, _timeProvider, weather));
            _futureTotalCount = snapshot.FutureTotalCount;
            _loadedDate = snapshot.LocalDate;
            StatusText = string.Empty;
            OnPropertyChanged(nameof(TodayCountText));
            OnPropertyChanged(nameof(FutureCountText));
            OnPropertyChanged(nameof(NextTodayText));
            OnPropertyChanged(nameof(ShowTodayEmpty));
            OnPropertyChanged(nameof(ShowFutureEmpty));
        }
        catch (Exception ex) { StatusText = $"加载近期拍摄失败：{ex.Message}"; }
        finally { IsBusy = false; }
    }

    public async Task RefreshIfDateChangedAsync()
    {
        var now = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _localTimeZone);
        if (DateOnly.FromDateTime(now.DateTime) == _loadedDate) return;
        await RunOnUiAsync(RefreshAsync).ConfigureAwait(false);
    }

    private async Task OpenBookingAsync(object? parameter)
    {
        if (parameter is not WorkbenchScheduleItemViewModel item) return;
        OpenBookingRequested?.Invoke(this, item.BookingId);
        await Task.CompletedTask;
    }

    private async Task<BookingWeatherSummary?> TryGetCachedWeatherAsync(WorkbenchScheduleItem item)
    {
        if (_weatherService is null) return null;
        try { return await _weatherService.TryGetCachedBookingWeatherAsync(item.BookingId, item.StartAtUtc, item.EndAtUtc).ConfigureAwait(true); }
        catch { return null; }
    }

    private static Task RunOnUiAsync(Func<Task> action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        return dispatcher is null || dispatcher.CheckAccess() ? action() : dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    private void BookingChanges_BookingChanged(object? sender, Guid bookingId) => _ = RunOnUiAsync(RefreshAsync);

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        if (_bookingChanges is not null) _bookingChanges.BookingChanged -= BookingChanges_BookingChanged;
    }
}

public sealed class WorkbenchCalendarSummaryViewModel : WorkbenchScheduleViewModel
{
    public WorkbenchCalendarSummaryViewModel(IWorkbenchScheduleService service, IBookingChangeNotifier? bookingChanges = null, TimeProvider? timeProvider = null, TimeZoneInfo? localTimeZone = null, IWeatherForecastService? weatherService = null)
        : base(service, bookingChanges, timeProvider, localTimeZone, weatherService) { }
}

public sealed class ReminderNotificationItemViewModel
{
    public ReminderNotificationItemViewModel(ReminderPublishedEvent published)
        : this(published.Notification, published.Dispatch.Booking.Id, published.Dispatch.Reminder.Id) { }

    public ReminderNotificationItemViewModel(NotificationMessage notification, Guid bookingId, Guid reminderId)
    {
        Notification = notification;
        BookingId = bookingId;
        ReminderId = reminderId;
    }

    public NotificationMessage Notification { get; }
    public Guid NotificationId => Notification.Id;
    public Guid BookingId { get; }
    public Guid ReminderId { get; }
    public string Title => Notification.Title;
    public string Message => Notification.Message;
}

public sealed class ReminderNotificationCenterViewModel : ObservableObject, IDisposable
{
    private readonly IBookingReminderNotificationService _publisher;
    private readonly INotificationCenter _notificationCenter;
    private readonly IBookingReminderService _reminderService;

    public ReminderNotificationCenterViewModel(IBookingReminderNotificationService publisher, INotificationCenter notificationCenter, IBookingReminderService reminderService)
    {
        _publisher = publisher;
        _notificationCenter = notificationCenter;
        _reminderService = reminderService;
        _publisher.ReminderPublished += Publisher_ReminderPublished;
        OpenCommand = new AsyncRelayCommand(OpenAsync, parameter => parameter is ReminderNotificationItemViewModel);
        LaterCommand = new RelayCommand(Later, parameter => parameter is ReminderNotificationItemViewModel);
        AcknowledgeCommand = new AsyncRelayCommand(AcknowledgeAsync, parameter => parameter is ReminderNotificationItemViewModel);
    }

    public event EventHandler<Guid>? OpenBookingRequested;
    public ObservableCollection<ReminderNotificationItemViewModel> Items { get; } = [];
    public ICommand OpenCommand { get; }
    public ICommand LaterCommand { get; }
    public ICommand AcknowledgeCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Items.Clear();
        foreach (var notification in await _notificationCenter.GetHistoryAsync(100, cancellationToken).ConfigureAwait(true))
        {
            if (notification.IsRead || notification.TaskId is not { } bookingId || !TryReminderId(notification.DeduplicationKey, out var reminderId)) continue;
            Items.Add(new(notification, bookingId, reminderId));
        }
    }

    private void Publisher_ReminderPublished(object? sender, ReminderPublishedEvent e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) Items.Insert(0, new(e));
        else _ = dispatcher.InvokeAsync(() => Items.Insert(0, new(e)));
    }

    private async Task OpenAsync(object? parameter)
    {
        if (parameter is not ReminderNotificationItemViewModel item) return;
        await _notificationCenter.MarkReadAsync(item.NotificationId).ConfigureAwait(true);
        Items.Remove(item);
        OpenBookingRequested?.Invoke(this, item.BookingId);
    }

    private void Later(object? parameter)
    {
        if (parameter is ReminderNotificationItemViewModel item) Items.Remove(item);
    }

    private async Task AcknowledgeAsync(object? parameter)
    {
        if (parameter is not ReminderNotificationItemViewModel item) return;
        await _notificationCenter.MarkReadAsync(item.NotificationId).ConfigureAwait(true);
        await _reminderService.DismissAsync(item.ReminderId).ConfigureAwait(true);
        Items.Remove(item);
    }

    private static bool TryReminderId(string? key, out Guid reminderId)
    {
        reminderId = Guid.Empty;
        const string prefix = "booking-reminder:";
        return key?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true && Guid.TryParse(key[prefix.Length..], out reminderId);
    }

    public void Dispose() => _publisher.ReminderPublished -= Publisher_ReminderPublished;
}
