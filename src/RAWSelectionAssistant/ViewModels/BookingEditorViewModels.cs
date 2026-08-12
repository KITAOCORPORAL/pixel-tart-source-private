using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Business;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed record ProjectOption(Guid? Id, string Name) { public override string ToString() => Name; }
public sealed record TimeZoneOption(string Id, string Label) { public override string ToString() => Label; }
public sealed record BookingStatusEditorOption(ShootBookingStatus Value, string Label) { public override string ToString() => Label; }
public sealed record CalendarWorkflowStatusOption(CalendarWorkflowStatus Value, string Label) { public override string ToString() => Label; }
public sealed record ShootingTypeEditorOption(string Value, string Label) { public override string ToString() => Label; }
public sealed record BookingStaffRoleOption(BookingStaffRole Value, string Label) { public override string ToString() => Label; }

public enum BookingEditorPresentation
{
    QuickCreate,
    QuickEdit,
    FullPlanning
}

public sealed class BookingContactEditorViewModel : ObservableObject
{
    private string _displayName = string.Empty; private string _phone = string.Empty; private string _weChat = string.Empty; private string _email = string.Empty; private string _otherContact = string.Empty; private bool _isPrimary; private string _note = string.Empty;
    public Guid Id { get; init; } = Guid.NewGuid();
    public string DisplayName { get => _displayName; set => SetProperty(ref _displayName, value ?? string.Empty); }
    public string Phone { get => _phone; set => SetProperty(ref _phone, value ?? string.Empty); }
    public string WeChat { get => _weChat; set => SetProperty(ref _weChat, value ?? string.Empty); }
    public string Email { get => _email; set => SetProperty(ref _email, value ?? string.Empty); }
    public string OtherContact { get => _otherContact; set => SetProperty(ref _otherContact, value ?? string.Empty); }
    public bool IsPrimary { get => _isPrimary; set => SetProperty(ref _isPrimary, value); }
    public string Note { get => _note; set => SetProperty(ref _note, value ?? string.Empty); }
    public BookingContact ToModel(Guid bookingId) => new() { Id = Id, BookingId = bookingId, DisplayName = DisplayName, Phone = Phone, WeChat = WeChat, Email = Email, OtherContact = OtherContact, IsPrimary = IsPrimary, Note = Note };
    public static BookingContactEditorViewModel From(BookingContact value) => new() { Id = value.Id, DisplayName = value.DisplayName, Phone = value.Phone ?? string.Empty, WeChat = value.WeChat ?? string.Empty, Email = value.Email ?? string.Empty, OtherContact = value.OtherContact ?? string.Empty, IsPrimary = value.IsPrimary, Note = value.Note ?? string.Empty };
}

public sealed class BookingStaffEditorViewModel : ObservableObject
{
    private string _displayName = string.Empty; private BookingStaffRoleOption? _selectedRole; private string _arrivalTimeText = string.Empty; private string _phone = string.Empty; private string _weChat = string.Empty; private string _email = string.Empty; private string _note = string.Empty;
    public Guid Id { get; init; } = Guid.NewGuid();
    public string DisplayName { get => _displayName; set => SetProperty(ref _displayName, value ?? string.Empty); }
    public BookingStaffRoleOption? SelectedRole { get => _selectedRole; set => SetProperty(ref _selectedRole, value); }
    public string ArrivalTimeText { get => _arrivalTimeText; set => SetProperty(ref _arrivalTimeText, value ?? string.Empty); }
    public string Phone { get => _phone; set => SetProperty(ref _phone, value ?? string.Empty); }
    public string WeChat { get => _weChat; set => SetProperty(ref _weChat, value ?? string.Empty); }
    public string Email { get => _email; set => SetProperty(ref _email, value ?? string.Empty); }
    public string Note { get => _note; set => SetProperty(ref _note, value ?? string.Empty); }
    public BookingStaffMember ToModel(Guid bookingId, int sortOrder, string? timeZoneId = null)
    {
        DateTimeOffset? arrival = null;
        if (DateTime.TryParse(ArrivalTimeText, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            var local = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
            var zone = BookingTimeDisplayService.Default.ResolveTimeZone(timeZoneId);
            if (!zone.IsInvalidTime(local))
            {
                var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
                arrival = new DateTimeOffset(local, offset).ToUniversalTime();
            }
        }
        return new() { Id = Id, BookingId = bookingId, DisplayName = DisplayName, Role = SelectedRole?.Value ?? BookingStaffRole.Other, ArrivalTime = arrival, Phone = Phone, WeChat = WeChat, Email = Email, Note = Note, SortOrder = sortOrder };
    }
    public static BookingStaffEditorViewModel From(BookingStaffMember value, IReadOnlyList<BookingStaffRoleOption> options, string? timeZoneId = null, IBookingTimeDisplayService? timeDisplay = null) => new() { Id = value.Id, DisplayName = value.DisplayName, SelectedRole = options.First(x => x.Value == value.Role), ArrivalTimeText = value.ArrivalTime is { } arrival ? (timeDisplay ?? BookingTimeDisplayService.Default).ToBookingTime(arrival, timeZoneId).ToString("yyyy-MM-dd HH:mm") : string.Empty, Phone = value.Phone ?? string.Empty, WeChat = value.WeChat ?? string.Empty, Email = value.Email ?? string.Empty, Note = value.Note ?? string.Empty };
}

public sealed class BookingContactDetailsViewModel(BookingContact value)
{
    public string DisplayName => value.DisplayName + (value.IsPrimary ? "（主要联系人）" : string.Empty);
    public string ContactText => string.Join(" · ", new[] { value.Phone, value.WeChat, value.Email, value.OtherContact }.Where(item => !string.IsNullOrWhiteSpace(item))!);
}

public sealed class BookingStaffDetailsViewModel(BookingStaffMember value, string? timeZoneId = null, IBookingTimeDisplayService? timeDisplay = null)
{
    public string DisplayName => value.DisplayName;
    public string RoleText => value.Role switch { BookingStaffRole.Photographer => "摄影师", BookingStaffRole.PhotographyAssistant => "摄影助理", BookingStaffRole.LightingTechnician => "灯光师", BookingStaffRole.MakeupArtist => "化妆师", BookingStaffRole.Stylist => "造型师", BookingStaffRole.ModelOrActor => "模特或演员", BookingStaffRole.ClientRepresentative => "客户代表", BookingStaffRole.FloorAssistant => "场务", _ => "其他" };
    public string ArrivalText => value.ArrivalTime is { } arrival ? (timeDisplay ?? BookingTimeDisplayService.Default).ToBookingTime(arrival, timeZoneId).ToString("M月d日 HH:mm 到场") : "未设置到场时间";
}

public sealed class ShootBookingDetailsViewModel : ObservableObject
{
    private readonly IShootBookingService _service;
    private readonly IBookingReminderScheduler? _reminderScheduler;
    private readonly IDialogService? _dialogs;
    private readonly IBookingTimeDisplayService _timeDisplay;
    private ShootBooking? _booking;
    private bool _amountsVisible;
    private bool _isBusy;
    private string _statusText = string.Empty;
    private int _selectedTabIndex;
    private readonly IBookingPeopleService? _peopleService;
    private readonly IFinanceService? _financeService;
    private FinanceSummary _financeSummary = new(0, 0, 0, 0, 0, 0);
    private CalendarWorkflowStatusOption? _selectedWorkflowStatus;
    private bool _suppressWorkflowStatusChange;
    private readonly IBookingWorkflowService? _workflowService;

    public ShootBookingDetailsViewModel(IShootBookingService service, IBookingDocumentWorkflowService? documentWorkflow = null, IDialogService? dialogs = null,
        IBookingReminderService? reminderService = null, IBookingReminderScheduler? reminderScheduler = null,
        IWeatherForecastService? weatherService = null, WeatherFeatureState? weatherState = null,
        IBookingPeopleService? peopleService = null, IFinanceService? financeService = null,
        ICurrentLocationService? currentLocationService = null,
        IBookingTimeDisplayService? timeDisplay = null,
        IBookingWorkflowService? workflowService = null)
    {
        _service = service;
        _reminderScheduler = reminderScheduler;
        _dialogs = dialogs;
        _timeDisplay = timeDisplay ?? BookingTimeDisplayService.Default;
        _peopleService = peopleService;
        _financeService = financeService;
        _workflowService = workflowService;
        if (documentWorkflow is not null && dialogs is not null) Documents = new BookingDocumentsViewModel(documentWorkflow, dialogs);
        if (reminderService is not null) Reminders = new BookingRemindersViewModel(reminderService, reminderScheduler);
        if (weatherService is not null && weatherState is not null) Weather = new BookingWeatherViewModel(weatherService, weatherState, currentLocationService, _timeDisplay);
        ToggleAmountsCommand = new RelayCommand(_ => AmountsVisible = !AmountsVisible);
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(this, EventArgs.Empty));
        EditCommand = new RelayCommand(_ => { if (Booking is not null) EditRequested?.Invoke(this, Booking.Id); }, _ => Booking is { IsArchived: false });
        FullPlanningCommand = new RelayCommand(_ => { if (Booking is not null) FullPlanningRequested?.Invoke(this, Booking.Id); }, _ => Booking is { IsArchived: false });
        CompleteCommand = new AsyncRelayCommand(_ => CompleteAsync(), _ => CanComplete);
        ArchiveCommand = new AsyncRelayCommand(_ => ArchiveAsync(), _ => Booking is { IsArchived: false });
        ViewFinanceCommand = new RelayCommand(_ => RequestFinance(null), _ => Booking is not null);
        AddIncomeCommand = new RelayCommand(_ => RequestFinance(FinanceTransactionKind.Income), _ => Booking is { IsArchived: false });
        AddExpenseCommand = new RelayCommand(_ => RequestFinance(FinanceTransactionKind.Expense), _ => Booking is { IsArchived: false });
        WorkflowStatusOptions = Enum.GetValues<CalendarWorkflowStatus>()
            .Select(value => new CalendarWorkflowStatusOption(value, CalendarWorkflowStatusMapper.DisplayName(value)))
            .ToArray();
    }

    public event EventHandler? CloseRequested;
    public event EventHandler<Guid>? EditRequested;
    public event EventHandler<Guid>? FullPlanningRequested;
    public event EventHandler<Guid>? Completed;
    public event EventHandler<Guid>? WorkflowStatusChanged;
    public event EventHandler<Guid>? Archived;
    public event EventHandler<BookingFinanceRequestEventArgs>? FinanceRequested;
    public ObservableCollection<ShootRequirementItem> Requirements { get; } = [];
    public ObservableCollection<BookingContactDetailsViewModel> Contacts { get; } = [];
    public ObservableCollection<BookingStaffDetailsViewModel> Staff { get; } = [];
    public IReadOnlyList<CalendarWorkflowStatusOption> WorkflowStatusOptions { get; }
    public BookingDocumentsViewModel? Documents { get; }
    public BookingRemindersViewModel? Reminders { get; }
    public BookingWeatherViewModel? Weather { get; }
    public bool HasDocumentsPanel => Documents is not null;
    public bool HasRemindersPanel => Reminders is not null;
    public bool HasWeatherPanel => Weather is not null;
    public ICommand ToggleAmountsCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand FullPlanningCommand { get; }
    public ICommand CompleteCommand { get; }
    public ICommand ArchiveCommand { get; }
    public ICommand ViewFinanceCommand { get; }
    public ICommand AddIncomeCommand { get; }
    public ICommand AddExpenseCommand { get; }
    public ShootBooking? Booking { get => _booking; private set { if (SetProperty(ref _booking, value)) NotifyBooking(); } }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool AmountsVisible { get => _amountsVisible; set { if (SetProperty(ref _amountsVisible, value)) NotifyMoney(); } }
    public int SelectedTabIndex { get => _selectedTabIndex; set => SetProperty(ref _selectedTabIndex, Math.Clamp(value, 0, 4)); }
    public bool IsArchived => Booking?.IsArchived == true;
    public bool CanEdit => Booking is { IsArchived: false };
    public bool CanComplete => Booking is { IsArchived: false, Status: not ShootBookingStatus.Completed and not ShootBookingStatus.Cancelled };
    public string ShootCompletionText => Booking?.ShotCompletedAtUtc is { } completed ? $"拍摄 ✓ 已完成 · {completed.ToLocalTime():yyyy-MM-dd HH:mm}" : "拍摄 · 待拍摄";
    public string WorkflowStageText => Booking is null ? "—" : CalendarWorkflowStateMapper.DisplayName(CalendarWorkflowStateMapper.FromBookingStatus(Booking.Status));
    public string Title => Booking?.Title ?? "排期详情";
    public string ClientDisplayName => Booking?.ClientDisplayName ?? "—";
    public string TimeText => Booking is null ? "—" : FormatTime(Booking);
    public string TimeZoneText => Booking is null ? "—" : _timeDisplay.FriendlyTimeZoneName(Booking.TimeZoneId);
    public string LocationText => Booking?.Location ?? "未填写";
    public string ShootingTypeText => Booking is null ? "—" : CalendarText.Type(Booking.ShootingType);
    public string BookingStatusText => Booking is null ? "—" : CalendarText.Status(Booking.Status);
    public CalendarWorkflowStatusOption? SelectedWorkflowStatus
    {
        get => _selectedWorkflowStatus;
        set
        {
            if (value is null || !SetProperty(ref _selectedWorkflowStatus, value) || _suppressWorkflowStatusChange) return;
            _ = ChangeWorkflowStatusAsync(value);
        }
    }
    public string ShootingRequirementsText => Booking?.ShootingRequirements ?? "未填写";
    public string PreparationNotesText => Booking?.PreparationNotes ?? "未填写";
    public string NotesText => Booking?.Notes ?? "未填写";
    public string AmountVisibilityText => AmountsVisible ? "隐藏金额" : "显示金额";
    public string TotalAmountText => MoneyText(Booking?.TotalAmountMinor);
    public string DepositAmountText => MoneyText(Booking?.DepositAmountMinor);
    public string PaidAmountText => MoneyText(Booking?.PaidAmountMinor);
    public string BalanceLabel => Money.DisplayKind == BookingMoneyDisplayKind.Overpaid ? "多收金额" : "待收金额";
    public string BalanceText => MoneyText(Money.DisplayAmountMinor);
    public bool HasMoneyWarning => Money.Warnings.Count > 0;
    public string MoneyWarningText => Money.Warnings.FirstOrDefault()?.Message ?? string.Empty;
    public string ExpectedIncomeText => FinanceMoney(_financeSummary.IncomeMinor + _financeSummary.ReceivableMinor);
    public string FinanceReceivedText => FinanceMoney(_financeSummary.IncomeMinor);
    public string FinanceReceivableText => FinanceMoney(_financeSummary.ReceivableMinor);
    public string FinanceExpenseText => FinanceMoney(_financeSummary.ExpenseMinor);
    public string FinancePayableText => FinanceMoney(_financeSummary.PayableMinor);
    public string FinanceNetText => FinanceMoney(_financeSummary.NetCashFlowMinor);
    private BookingMoneySummary Money => BookingMoneyCalculator.Calculate(Booking?.TotalAmountMinor, Booking?.DepositAmountMinor, Booking?.PaidAmountMinor);

    public async Task LoadAsync(Guid bookingId, bool includeArchived = false)
    {
        IsBusy = true;
        AmountsVisible = false;
        SelectedTabIndex = 0;
        Documents?.Reset();
        try
        {
            Booking = await _service.GetAsync(bookingId, includeArchived).ConfigureAwait(true);
            Requirements.Clear();
            if (Booking is not null)
            {
                foreach (var item in await _service.GetRequirementsAsync(bookingId).ConfigureAwait(true)) Requirements.Add(item);
                Contacts.Clear(); Staff.Clear();
                if (_peopleService is not null)
                {
                    foreach (var item in await _peopleService.ListContactsAsync(bookingId).ConfigureAwait(true)) Contacts.Add(new(item));
                    foreach (var item in await _peopleService.ListStaffAsync(bookingId).ConfigureAwait(true)) Staff.Add(new(item, Booking.TimeZoneId, _timeDisplay));
                }
                if (Documents is not null) await Documents.LoadAsync(Booking.Id, Booking.ProjectId, Booking.IsArchived).ConfigureAwait(true);
                if (Reminders is not null) await Reminders.LoadAsync(Booking.Id, Booking.ProjectId, Booking.IsArchived, Booking.TimeZoneId).ConfigureAwait(true);
                if (Weather is not null) await Weather.LoadAsync(Booking).ConfigureAwait(true);
                if (_financeService is not null)
                {
                    _financeSummary = await _financeService.SummarizeAsync(new FinanceQuery(BookingId: Booking.Id)).ConfigureAwait(true);
                    NotifyFinance();
                }
            }
            StatusText = Booking is null ? "排期不存在或已归档" : string.Empty;
        }
        catch (Exception ex) { StatusText = $"加载失败：{ex.Message}"; }
        finally { IsBusy = false; }
    }

    public void Clear()
    {
        Booking = null;
        Requirements.Clear();
        Contacts.Clear();
        Staff.Clear();
        _financeSummary = new(0, 0, 0, 0, 0, 0);
        StatusText = string.Empty;
        AmountsVisible = false;
        SelectedTabIndex = 0;
        NotifyFinance();
    }

    private async Task ArchiveAsync()
    {
        if (Booking is null || !await _service.ArchiveAsync(Booking.Id).ConfigureAwait(true)) return;
        Booking = Booking with { IsArchived = true, ArchivedAtUtc = DateTimeOffset.UtcNow };
        if (Documents is not null) await Documents.LoadAsync(Booking.Id, Booking.ProjectId, isArchived: true).ConfigureAwait(true);
        if (Reminders is not null) await Reminders.LoadAsync(Booking.Id, Booking.ProjectId, isReadOnly: true, Booking.TimeZoneId).ConfigureAwait(true);
        if (_reminderScheduler is not null) await _reminderScheduler.RefreshAsync().ConfigureAwait(true);
        StatusText = "排期已归档；提醒已关闭，关联数据与电脑文件均已保留。";
        Archived?.Invoke(this, Booking.Id);
    }

    private async Task CompleteAsync()
    {
        if (Booking is null) return;
        var completed = _workflowService is not null
            ? await _workflowService.MarkShootCompletedAsync(Booking.Id).ConfigureAwait(true)
            : new BookingWorkflowResult(Booking.Id, (await _service.CompleteAsync(Booking.Id).ConfigureAwait(true)) ? BookingWorkflowOperationStatus.Succeeded : BookingWorkflowOperationStatus.Failed,
                CalendarWorkflowStateMapper.FromBookingStatus(Booking.Status), CalendarWorkflowState.PostProduction, DateTimeOffset.UtcNow);
        if (!completed.IsSuccess) { StatusText = completed.ErrorMessage ?? "拍摄完成状态未能保存，请重试。"; return; }
        Booking = Booking with { Status = ShootBookingStatus.Completed, ShotCompletedAtUtc = completed.ShotCompletedAtUtc ?? DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow };
        if (Reminders is not null) await Reminders.LoadAsync(Booking.Id, Booking.ProjectId, isReadOnly: false, Booking.TimeZoneId).ConfigureAwait(true);
        if (_reminderScheduler is not null) await _reminderScheduler.RefreshAsync().ConfigureAwait(true);
        StatusText = "拍摄已完成，流程已进入后期；未来未触发提醒已关闭，排期和历史记录均已保留。";
        Completed?.Invoke(this, Booking.Id);
    }

    private async Task ChangeWorkflowStatusAsync(CalendarWorkflowStatusOption requested)
    {
        if (Booking is null || Booking.IsArchived) { SyncWorkflowStatus(); return; }
        var current = CalendarWorkflowStatusMapper.FromBookingStatus(Booking.Status);
        if (requested.Value == current) return;
        if (requested.Value < current && _dialogs is not null && !_dialogs.Confirm(
                $"将拍摄流程从“{CalendarWorkflowStatusMapper.DisplayName(current)}”退回到“{requested.Label}”会立即保存，是否继续？",
                "确认回退拍摄状态"))
        {
            SyncWorkflowStatus();
            return;
        }

        IsBusy = true;
        try
        {
            var status = CalendarWorkflowStatusMapper.ToBookingStatus(requested.Value);
            var changed = _workflowService is not null
                ? requested.Value switch
                {
                    CalendarWorkflowStatus.Scheduled => await _workflowService.UndoShootCompletedAsync(Booking.Id).ConfigureAwait(true),
                    CalendarWorkflowStatus.PendingDelivery => await _workflowService.SetPostProductionStageAsync(Booking.Id, CalendarPostProductionStage.PendingDelivery).ConfigureAwait(true),
                    CalendarWorkflowStatus.Delivered => await _workflowService.MarkDeliveredAsync(Booking.Id).ConfigureAwait(true),
                    _ => new BookingWorkflowResult(Booking.Id, BookingWorkflowOperationStatus.Rejected, current is CalendarWorkflowStatus.Scheduled ? CalendarWorkflowState.Scheduled : CalendarWorkflowState.PostProduction, CalendarWorkflowState.PostProduction, ErrorMessage: "请使用“标记拍摄完成”进入后期流程。")
                }
                : new BookingWorkflowResult(Booking.Id, (await _service.SetStatusAsync(Booking.Id, status).ConfigureAwait(true)) ? BookingWorkflowOperationStatus.Succeeded : BookingWorkflowOperationStatus.Failed, CalendarWorkflowStateMapper.FromBookingStatus(Booking.Status), CalendarWorkflowStateMapper.FromBookingStatus(status));
            if (!changed.IsSuccess)
            {
                StatusText = changed.ErrorMessage ?? "拍摄流程状态未能保存，请重试。";
                SyncWorkflowStatus();
                return;
            }
            Booking = Booking with { Status = status, ShotCompletedAtUtc = changed.ShotCompletedAtUtc ?? Booking.ShotCompletedAtUtc, UpdatedAtUtc = DateTimeOffset.UtcNow };
            if (Reminders is not null) await Reminders.LoadAsync(Booking.Id, Booking.ProjectId, isReadOnly: false, Booking.TimeZoneId).ConfigureAwait(true);
            if (_reminderScheduler is not null) await _reminderScheduler.RefreshAsync().ConfigureAwait(true);
            StatusText = $"拍摄流程已更新为“{requested.Label}”。";
            WorkflowStatusChanged?.Invoke(this, Booking.Id);
        }
        catch (Exception ex)
        {
            StatusText = $"拍摄流程状态保存失败：{ex.Message}";
            SyncWorkflowStatus();
        }
        finally { IsBusy = false; }
    }

    private void RequestFinance(FinanceTransactionKind? kind)
    {
        if (Booking is not null) FinanceRequested?.Invoke(this, new(Booking.Id, kind));
    }

    private void NotifyBooking()
    {
        foreach (var name in new[] { nameof(IsArchived), nameof(CanEdit), nameof(CanComplete), nameof(Title), nameof(ClientDisplayName), nameof(TimeText), nameof(TimeZoneText), nameof(LocationText), nameof(ShootingTypeText), nameof(BookingStatusText), nameof(ShootCompletionText), nameof(WorkflowStageText), nameof(ShootingRequirementsText), nameof(PreparationNotesText), nameof(NotesText) }) OnPropertyChanged(name);
        (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (FullPlanningCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CompleteCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ArchiveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ViewFinanceCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddIncomeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddExpenseCommand as RelayCommand)?.RaiseCanExecuteChanged();
        NotifyMoney();
        SyncWorkflowStatus();
    }

    private void NotifyMoney()
    {
        foreach (var name in new[] { nameof(AmountVisibilityText), nameof(TotalAmountText), nameof(DepositAmountText), nameof(PaidAmountText), nameof(BalanceLabel), nameof(BalanceText), nameof(HasMoneyWarning), nameof(MoneyWarningText) }) OnPropertyChanged(name);
    }

    private void NotifyFinance()
    {
        foreach (var name in new[] { nameof(ExpectedIncomeText), nameof(FinanceReceivedText), nameof(FinanceReceivableText), nameof(FinanceExpenseText), nameof(FinancePayableText), nameof(FinanceNetText) }) OnPropertyChanged(name);
    }

    private static string FinanceMoney(long value) => $"CNY {value / 100m:N2}";

    private string MoneyText(long? value)
    {
        if (!AmountsVisible) return "••••••";
        if (!value.HasValue) return "未记录";
        var scale = Booking?.CurrencyScale ?? 2;
        var divisor = (decimal)Math.Pow(10, scale);
        return $"{Booking?.CurrencyCode ?? "CNY"} {value.Value / divisor:N2}";
    }

    private string FormatTime(ShootBooking booking) => _timeDisplay.FormatRange(booking.StartAtUtc, booking.EndAtUtc, booking.TimeZoneId, booking.IsAllDay);

    private void SyncWorkflowStatus()
    {
        _suppressWorkflowStatusChange = true;
        try
        {
            _selectedWorkflowStatus = Booking is null
                ? null
                : WorkflowStatusOptions.First(option => option.Value == CalendarWorkflowStatusMapper.FromBookingStatus(Booking.Status));
            OnPropertyChanged(nameof(SelectedWorkflowStatus));
        }
        finally { _suppressWorkflowStatusChange = false; }
    }
}

public sealed class BookingFinanceRequestEventArgs(Guid bookingId, FinanceTransactionKind? kind) : EventArgs
{
    public Guid BookingId { get; } = bookingId;
    public FinanceTransactionKind? Kind { get; } = kind;
}

public sealed class ShootBookingEditorViewModel : ObservableObject
{
    private readonly IShootBookingService _service;
    private readonly IProjectRepository _projectRepository;
    private readonly Guid? _bookingId;
    private readonly Guid _stableBookingId;
    private readonly Guid _editorSessionId = Guid.NewGuid();
    private readonly DateTime? _suggestedStart;
    private readonly IBookingPeopleService? _peopleService;
    private readonly IDialogService? _dialogs;
    private readonly BookingWeatherViewModel? _weather;
    private string _title = string.Empty;
    private string _clientDisplayName = string.Empty;
    private DateTime _startDate = DateTime.Today;
    private string _startTimeText = "09:00";
    private DateTime _endDate = DateTime.Today;
    private string _endTimeText = "10:00";
    private TimeZoneOption? _selectedTimeZone;
    private bool _isAllDay;
    private string _location = string.Empty;
    private ShootingTypeEditorOption? _selectedShootingType;
    private BookingStatusEditorOption? _selectedStatus;
    private string _shootingRequirements = string.Empty;
    private string _preparationNotes = string.Empty;
    private string _totalAmountText = string.Empty;
    private string _depositAmountText = string.Empty;
    private string _paidAmountText = string.Empty;
    private ProjectOption? _selectedProject;
    private bool _allowOverlap;
    private string _notes = string.Empty;
    private string _validationText = string.Empty;
    private bool _isBusy;
    private bool _isConflictVisible;
    private string _moneyWarningText = string.Empty;
    private string _balanceLabel = "待收金额";
    private string _balanceText = "未记录";
    private int _currentStep = 1;
    private string? _initialSignature;
    private bool _wasSaved;
    private BookingEditorSaveStatus _saveStatus = BookingEditorSaveStatus.ValidationFailed;

    public ShootBookingEditorViewModel(IShootBookingService service, IProjectRepository projectRepository, Guid? bookingId = null, DateTime? suggestedStart = null,
        IBookingPeopleService? peopleService = null, IBookingDocumentWorkflowService? documentWorkflow = null, IDialogService? dialogs = null,
        IWeatherForecastService? weatherService = null, WeatherFeatureState? weatherState = null, ICurrentLocationService? currentLocationService = null)
    {
        _service = service;
        _projectRepository = projectRepository;
        _bookingId = bookingId;
        _stableBookingId = bookingId ?? Guid.NewGuid();
        _suggestedStart = suggestedStart;
        _peopleService = peopleService;
        _dialogs = dialogs;
        StatusOptions = Enum.GetValues<ShootBookingStatus>().Select(value => new BookingStatusEditorOption(value, CalendarText.Status(value))).ToArray();
        ShootingTypeOptions =
        [
            new("Portrait", "人像"), new("Wedding", "婚礼"), new("Commercial", "商业"), new("Event", "活动"),
            new("Product", "产品"), new("Other", "其他")
        ];
        TimeZoneOptions = TimeZoneInfo.GetSystemTimeZones().Select(zone => new TimeZoneOption(zone.Id, zone.DisplayName)).ToArray();
        _selectedStatus = StatusOptions.First(option => option.Value == ShootBookingStatus.Tentative);
        _selectedShootingType = ShootingTypeOptions.First(option => option.Value == "Other");
        _selectedTimeZone = TimeZoneOptions.FirstOrDefault(option => option.Id == TimeZoneInfo.Local.Id) ?? TimeZoneOptions.First();
        Requirements = new ShootRequirementsViewModel();
        StaffRoleOptions =
        [
            new(BookingStaffRole.Photographer, "摄影师"), new(BookingStaffRole.PhotographyAssistant, "摄影助理"), new(BookingStaffRole.LightingTechnician, "灯光师"),
            new(BookingStaffRole.MakeupArtist, "化妆师"), new(BookingStaffRole.Stylist, "造型师"), new(BookingStaffRole.ModelOrActor, "模特或演员"),
            new(BookingStaffRole.ClientRepresentative, "客户代表"), new(BookingStaffRole.FloorAssistant, "场务"), new(BookingStaffRole.Other, "其他")
        ];
        if (documentWorkflow is not null && dialogs is not null) Documents = new BookingDocumentsViewModel(documentWorkflow, dialogs);
        if (weatherService is not null && weatherState is not null) _weather = new BookingWeatherViewModel(weatherService, weatherState, currentLocationService, BookingTimeDisplayService.Default);
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(BookingConflictResolution.None, asDraft: false), _ => !IsBusy);
        ContinuePlanningCommand = new AsyncRelayCommand(_ => SaveAsync(BookingConflictResolution.None, asDraft: false, continuePlanning: true), _ => !IsBusy);
        SaveDraftCommand = new AsyncRelayCommand(_ => SaveDraftAsync(), _ => !IsBusy);
        SaveAnywayCommand = new AsyncRelayCommand(_ => SaveAsync(BookingConflictResolution.SaveAnyway, asDraft: false), _ => !IsBusy && IsConflictVisible);
        MarkOverlapAndSaveCommand = new AsyncRelayCommand(_ => SaveAsync(BookingConflictResolution.MarkAllowOverlap, asDraft: false), _ => !IsBusy && IsConflictVisible);
        ReturnToEditCommand = new RelayCommand(_ => { IsConflictVisible = false; Conflicts.Clear(); });
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(this, EventArgs.Empty));
        NextStepCommand = new RelayCommand(_ => CurrentStep = Math.Min(4, CurrentStep + 1), _ => CurrentStep < 4);
        PreviousStepCommand = new RelayCommand(_ => CurrentStep = Math.Max(1, CurrentStep - 1), _ => CurrentStep > 1);
        AddContactCommand = new RelayCommand(_ => Contacts.Add(new() { IsPrimary = Contacts.Count == 0 }));
        RemoveContactCommand = new RelayCommand(parameter => { if (parameter is BookingContactEditorViewModel item) Contacts.Remove(item); });
        AddStaffCommand = new RelayCommand(_ => Staff.Add(new() { SelectedRole = StaffRoleOptions.Last() }));
        RemoveStaffCommand = new RelayCommand(parameter => { if (parameter is BookingStaffEditorViewModel item) Staff.Remove(item); });
        MoveStaffUpCommand = new RelayCommand(parameter => MoveStaff(parameter as BookingStaffEditorViewModel, -1));
        MoveStaffDownCommand = new RelayCommand(parameter => MoveStaff(parameter as BookingStaffEditorViewModel, 1));
        OpenConflictCommand = new RelayCommand(parameter => { if (parameter is BookingConflictViewModel item) OpenConflictingBookingRequested?.Invoke(this, item.BookingId); });
    }

    public event EventHandler<ShootBooking>? Saved;
    public event Func<ShootBooking, Task>? SavedAsync;
    public event Func<ShootBooking, Task>? ContinuePlanningRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler<string>? FocusFieldRequested;
    public event EventHandler<Guid>? OpenConflictingBookingRequested;
    public IReadOnlyList<BookingStatusEditorOption> StatusOptions { get; }
    public IReadOnlyList<ShootingTypeEditorOption> ShootingTypeOptions { get; }
    public IReadOnlyList<TimeZoneOption> TimeZoneOptions { get; }
    public ObservableCollection<ProjectOption> ProjectOptions { get; } = [];
    public ObservableCollection<BookingConflictViewModel> Conflicts { get; } = [];
    public ShootRequirementsViewModel Requirements { get; }
    public BookingDocumentsViewModel? Documents { get; }
    public BookingWeatherViewModel? Weather => _weather;
    public bool HasWeatherPanel => Weather is not null;
    public ObservableCollection<BookingContactEditorViewModel> Contacts { get; } = [];
    public ObservableCollection<BookingStaffEditorViewModel> Staff { get; } = [];
    public IReadOnlyList<BookingStaffRoleOption> StaffRoleOptions { get; }
    public ICommand SaveCommand { get; }
    public ICommand ContinuePlanningCommand { get; }
    public ICommand SaveDraftCommand { get; }
    public ICommand SaveAnywayCommand { get; }
    public ICommand MarkOverlapAndSaveCommand { get; }
    public ICommand ReturnToEditCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand NextStepCommand { get; }
    public ICommand PreviousStepCommand { get; }
    public ICommand AddContactCommand { get; }
    public ICommand RemoveContactCommand { get; }
    public ICommand AddStaffCommand { get; }
    public ICommand RemoveStaffCommand { get; }
    public ICommand MoveStaffUpCommand { get; }
    public ICommand MoveStaffDownCommand { get; }
    public ICommand OpenConflictCommand { get; }
    public string DialogTitle => _bookingId.HasValue ? "编辑拍摄排期" : "新建拍摄排期";
    public bool IsEditMode => _bookingId.HasValue;
    public bool IsCreateMode => !IsEditMode;
    public string EditorMode => IsEditMode ? "EditMode" : "CreateMode";
    public string QuickDialogTitle => IsEditMode ? "快速编辑拍摄" : "新建拍摄";
    public string QuickDescription => IsEditMode ? "只修改高频信息；完整策划、资料、人员和收支保持不变。" : "先记录必要信息，创建后可继续完善完整拍摄策划。";
    public string QuickPrimarySaveText => IsEditMode ? "保存修改" : "创建拍摄";
    public string ContinuePlanningText => IsEditMode ? "保存并打开完整策划" : "继续完善策划";
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public Guid EditorSessionId => _editorSessionId;
    public Guid StableBookingId => _stableBookingId;
    public BookingEditorSaveStatus SaveStatus { get => _saveStatus; private set => SetProperty(ref _saveStatus, value); }
    public bool IsConflictVisible { get => _isConflictVisible; private set => SetProperty(ref _isConflictVisible, value); }
    public string ValidationText { get => _validationText; private set { if (SetProperty(ref _validationText, value)) OnPropertyChanged(nameof(HasValidationError)); } }
    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationText);
    public string MoneyWarningText { get => _moneyWarningText; private set { if (SetProperty(ref _moneyWarningText, value)) OnPropertyChanged(nameof(HasMoneyWarning)); } }
    public bool HasMoneyWarning => !string.IsNullOrWhiteSpace(MoneyWarningText);
    public string BalanceLabel { get => _balanceLabel; private set => SetProperty(ref _balanceLabel, value); }
    public string BalanceText { get => _balanceText; private set => SetProperty(ref _balanceText, value); }
    public string Title { get => _title; set => SetProperty(ref _title, value ?? string.Empty); }
    public string ClientDisplayName { get => _clientDisplayName; set => SetProperty(ref _clientDisplayName, value ?? string.Empty); }
    public DateTime StartDate { get => _startDate; set => SetProperty(ref _startDate, value.Date); }
    public string StartTimeText { get => _startTimeText; set => SetProperty(ref _startTimeText, value ?? string.Empty); }
    public DateTime EndDate { get => _endDate; set => SetProperty(ref _endDate, value.Date); }
    public string EndTimeText { get => _endTimeText; set => SetProperty(ref _endTimeText, value ?? string.Empty); }
    public TimeZoneOption? SelectedTimeZone { get => _selectedTimeZone; set => SetProperty(ref _selectedTimeZone, value); }
    public bool IsAllDay
    {
        get => _isAllDay;
        set
        {
            if (!SetProperty(ref _isAllDay, value)) return;
            if (value) { StartTimeText = "00:00"; EndTimeText = "00:00"; if (EndDate <= StartDate) EndDate = StartDate.AddDays(1); }
        }
    }
    public string Location
    {
        get => _location;
        set
        {
            if (!SetProperty(ref _location, value ?? string.Empty)) return;
            OnPropertyChanged(nameof(WeatherPreviewText));
        }
    }
    public ShootingTypeEditorOption? SelectedShootingType { get => _selectedShootingType; set => SetProperty(ref _selectedShootingType, value); }
    public BookingStatusEditorOption? SelectedStatus { get => _selectedStatus; set => SetProperty(ref _selectedStatus, value); }
    public string ShootingRequirements { get => _shootingRequirements; set => SetProperty(ref _shootingRequirements, value ?? string.Empty); }
    public string PreparationNotes { get => _preparationNotes; set => SetProperty(ref _preparationNotes, value ?? string.Empty); }
    public string TotalAmountText { get => _totalAmountText; set { if (SetProperty(ref _totalAmountText, value ?? string.Empty)) UpdateMoneyPreview(); } }
    public string DepositAmountText { get => _depositAmountText; set { if (SetProperty(ref _depositAmountText, value ?? string.Empty)) UpdateMoneyPreview(); } }
    public string PaidAmountText { get => _paidAmountText; set { if (SetProperty(ref _paidAmountText, value ?? string.Empty)) UpdateMoneyPreview(); } }
    public ProjectOption? SelectedProject { get => _selectedProject; set => SetProperty(ref _selectedProject, value); }
    public bool AllowOverlap { get => _allowOverlap; set => SetProperty(ref _allowOverlap, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value ?? string.Empty); }
    public int CurrentStep { get => _currentStep; set { var next = Math.Clamp(value, 1, 4); if (!SetProperty(ref _currentStep, next)) return; OnPropertyChanged(nameof(CurrentStepIndex)); OnPropertyChanged(nameof(StepTitle)); OnPropertyChanged(nameof(CanGoPrevious)); OnPropertyChanged(nameof(CanGoNext)); OnPropertyChanged(nameof(IsFinalStep)); foreach (var name in new[] { nameof(Step1State), nameof(Step2State), nameof(Step3State), nameof(Step4State), nameof(Step1Glyph), nameof(Step2Glyph), nameof(Step3Glyph), nameof(Step4Glyph) }) OnPropertyChanged(name); (NextStepCommand as RelayCommand)?.RaiseCanExecuteChanged(); (PreviousStepCommand as RelayCommand)?.RaiseCanExecuteChanged(); if (next == 2) _ = LoadWeatherPreviewAsync(); } }
    public int CurrentStepIndex { get => CurrentStep - 1; set => CurrentStep = value + 1; }
    public string StepTitle => CurrentStep switch { 1 => "1 基础信息", 2 => "2 时间、天气与提醒", 3 => "3 策划资料", _ => "4 人员与收支" };
    public string Step1State => StepState(1); public string Step2State => StepState(2); public string Step3State => StepState(3); public string Step4State => StepState(4);
    public string Step1Glyph => StepGlyph(1); public string Step2Glyph => StepGlyph(2); public string Step3Glyph => StepGlyph(3); public string Step4Glyph => StepGlyph(4);
    public string WeatherPreviewText => string.IsNullOrWhiteSpace(Location) ? "天气预览：拍摄地点待确认" : $"天气预览：将按“{Location.Trim()}”显示候选地点和预报";
    public string WeatherRiskText => "天气风险：尚未取得可靠预报时不会臆测风险；天气服务不可用也不会阻止排期保存。";
    public string ReminderSummaryText => "当前提醒列表：0 条；提醒默认关闭，可在排期详情中新增或调整。";
    public bool CanGoPrevious => CurrentStep > 1;
    public bool CanGoNext => CurrentStep < 4;
    public bool IsFinalStep => CurrentStep == 4;
    public string PrimarySaveText => _bookingId.HasValue ? "保存排期" : "创建排期";

    public async Task InitializeAsync()
    {
        ProjectOptions.Clear();
        ProjectOptions.Add(new(null, "不关联项目"));
        foreach (var project in await _projectRepository.ListAsync().ConfigureAwait(true)) ProjectOptions.Add(new(project.Id, project.Name));
        SelectedProject = ProjectOptions[0];

        if (_bookingId is null)
        {
            var start = _suggestedStart ?? DateTime.Today.AddHours(9);
            StartDate = start.Date;
            StartTimeText = start.ToString("HH:mm");
            EndDate = start.Date;
            EndTimeText = start.AddHours(1).ToString("HH:mm");
            Documents?.BeginDraft(_stableBookingId, SelectedProject?.Id);
            _initialSignature = BuildEditSignature();
            return;
        }

        var booking = await _service.GetAsync(_bookingId.Value, includeArchived: true).ConfigureAwait(true);
        if (booking is null) { ValidationText = "排期不存在。"; return; }
        var zone = TimeZoneInfo.FindSystemTimeZoneById(booking.TimeZoneId);
        var startLocal = TimeZoneInfo.ConvertTime(booking.StartAtUtc, zone);
        var endLocal = TimeZoneInfo.ConvertTime(booking.EndAtUtc, zone);
        Title = booking.Title;
        ClientDisplayName = booking.ClientDisplayName;
        StartDate = startLocal.Date;
        StartTimeText = startLocal.ToString("HH:mm");
        EndDate = endLocal.Date;
        EndTimeText = endLocal.ToString("HH:mm");
        SelectedTimeZone = TimeZoneOptions.FirstOrDefault(option => option.Id == booking.TimeZoneId) ?? _selectedTimeZone;
        IsAllDay = booking.IsAllDay;
        Location = booking.Location ?? string.Empty;
        SelectedShootingType = ShootingTypeOptions.FirstOrDefault(option => option.Value == booking.ShootingType) ?? new(booking.ShootingType, booking.ShootingType);
        SelectedStatus = StatusOptions.First(option => option.Value == booking.Status);
        ShootingRequirements = booking.ShootingRequirements ?? string.Empty;
        PreparationNotes = booking.PreparationNotes ?? string.Empty;
        TotalAmountText = MinorToText(booking.TotalAmountMinor, booking.CurrencyScale);
        DepositAmountText = MinorToText(booking.DepositAmountMinor, booking.CurrencyScale);
        PaidAmountText = MinorToText(booking.PaidAmountMinor, booking.CurrencyScale);
        SelectedProject = ProjectOptions.FirstOrDefault(option => option.Id == booking.ProjectId) ?? ProjectOptions[0];
        AllowOverlap = booking.AllowOverlap;
        Notes = booking.Notes ?? string.Empty;
        Requirements.Load(await _service.GetRequirementsAsync(booking.Id).ConfigureAwait(true));
        if (_peopleService is not null)
        {
            foreach (var item in await _peopleService.ListContactsAsync(booking.Id).ConfigureAwait(true)) Contacts.Add(BookingContactEditorViewModel.From(item));
            foreach (var item in await _peopleService.ListStaffAsync(booking.Id).ConfigureAwait(true)) Staff.Add(BookingStaffEditorViewModel.From(item, StaffRoleOptions, booking.TimeZoneId));
        }
        if (Documents is not null) await Documents.LoadAsync(booking.Id, booking.ProjectId, booking.IsArchived).ConfigureAwait(true);
        _initialSignature = BuildEditSignature();
    }

    private async Task LoadWeatherPreviewAsync()
    {
        if (Weather is null || SelectedTimeZone is null || !TryBuildRange(SelectedTimeZone.Id, out var start, out var end, out _)) return;
        await Weather.LoadAsync(new ShootBooking
        {
            Id = _stableBookingId,
            ProjectId = SelectedProject?.Id,
            Title = string.IsNullOrWhiteSpace(Title) ? "新建拍摄排期" : Title.Trim(),
            ClientDisplayName = ClientDisplayName,
            StartAtUtc = start,
            EndAtUtc = end,
            TimeZoneId = SelectedTimeZone.Id,
            IsAllDay = IsAllDay,
            Status = SelectedStatus?.Value ?? ShootBookingStatus.Tentative,
            Location = string.IsNullOrWhiteSpace(Location) ? null : Location.Trim(),
            ShootingType = SelectedShootingType?.Value ?? "Other"
        }).ConfigureAwait(true);
    }

    private string StepState(int step) => CurrentStep == step ? "Current" : CurrentStep > step ? "Complete" : "Pending";
    private string StepGlyph(int step) => CurrentStep > step ? "✓" : step.ToString(CultureInfo.InvariantCulture);

    private async Task SaveAsync(BookingConflictResolution resolution, bool asDraft, bool continuePlanning = false)
    {
        ValidationText = string.Empty;
        if (!TryBuildDraft(out var draft, out var validation, allowPartial: asDraft))
        {
            ValidationText = validation;
            SaveStatus = BookingEditorSaveStatus.ValidationFailed;
            FocusFieldRequested?.Invoke(this, validation.Contains("项目名称", StringComparison.Ordinal) ? "Title" : "StartDate");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _service.SaveAsync(draft!, resolution).ConfigureAwait(true);
            UpdateMoney(result.Money);
            if (result.Status == BookingSaveStatus.NeedsAttention)
            {
                Conflicts.Clear();
                foreach (var conflict in result.Conflicts) Conflicts.Add(new(conflict));
                IsConflictVisible = true;
                ValidationText = "检测到时间冲突。请选择返回修改、仍然保存，或标记当前排期允许重叠后保存。";
                SaveStatus = BookingEditorSaveStatus.ValidationFailed;
                return;
            }
            if (result.Status != BookingSaveStatus.Saved || result.Booking is null)
            {
                ValidationText = string.Join(Environment.NewLine, result.ValidationErrors.DefaultIfEmpty("排期未保存。"));
                SaveStatus = BookingEditorSaveStatus.DatabaseFailed;
                return;
            }
            if (Documents?.HasDraftOperations == true)
            {
                try
                {
                    await Documents.CommitDraftAsync().ConfigureAwait(true);
                }
                catch
                {
                    SaveStatus = BookingEditorSaveStatus.NeedsDocumentAttention;
                    ValidationText = "排期已保存，但部分策划资料尚未完成关联；请在资料面板中重试。";
                    _wasSaved = false;
                    return;
                }
            }
            IsConflictVisible = false;
            _wasSaved = true;
            SaveStatus = asDraft ? BookingEditorSaveStatus.DraftSaved : BookingEditorSaveStatus.Created;
            ValidationText = string.Empty;
            Saved?.Invoke(this, result.Booking);
            try
            {
                await NotifySavedAsync(result.Booking).ConfigureAwait(true);
            }
            catch
            {
                ValidationText = "排期已保存，但日历刷新暂时未完成；重新打开工作日历即可刷新。";
            }
            if (continuePlanning)
            {
                try
                {
                    if (!await ContinuePlanningAsync(result.Booking).ConfigureAwait(true))
                        ValidationText = "排期已保存，但未能打开完整策划；请从工作日历重新打开。";
                }
                catch
                {
                    ValidationText = "排期已保存，但完整策划暂时无法打开；请从工作日历重新打开。";
                }
            }
            else
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (IOException) { SaveStatus = BookingEditorSaveStatus.DatabaseFailed; ValidationText = "保存失败：本地数据库暂时不可用。请保持当前输入并重试。"; }
        catch { SaveStatus = BookingEditorSaveStatus.DatabaseFailed; ValidationText = "保存失败：本地数据库未确认完成。当前输入已保留，请重试。"; }
        finally { IsBusy = false; }
    }

    private async Task SaveDraftAsync()
    {
        SelectedStatus = StatusOptions.First(option => option.Value == ShootBookingStatus.Tentative);
        await SaveAsync(BookingConflictResolution.None, asDraft: true).ConfigureAwait(true);
    }

    private async Task<bool> ContinuePlanningAsync(ShootBooking booking)
    {
        var handlers = ContinuePlanningRequested;
        if (handlers is null) return false;
        foreach (Func<ShootBooking, Task> handler in handlers.GetInvocationList())
            await handler(booking).ConfigureAwait(true);
        return true;
    }

    private async Task NotifySavedAsync(ShootBooking booking)
    {
        var handlers = SavedAsync;
        if (handlers is null) return;
        foreach (Func<ShootBooking, Task> handler in handlers.GetInvocationList())
            await handler(booking).ConfigureAwait(true);
    }

    private void MoveStaff(BookingStaffEditorViewModel? item, int offset)
    {
        if (item is null) return;
        var current = Staff.IndexOf(item);
        var target = current + offset;
        if (current < 0 || target < 0 || target >= Staff.Count) return;
        Staff.Move(current, target);
    }

    public bool HasUnsavedChanges => Documents?.HasDraftOperations == true ||
        (!_wasSaved && _initialSignature is not null && !string.Equals(_initialSignature, BuildEditSignature(), StringComparison.Ordinal));
    public bool WasSaved => _wasSaved;

    public bool ConfirmDiscardChanges()
    {
        if (!HasUnsavedChanges) return true;
        return _dialogs?.Confirm("当前拍摄任务有尚未保存的修改。是否放弃这些修改？", "放弃未保存内容") ?? true;
    }

    private string BuildEditSignature()
    {
        var requirements = string.Join("|", Requirements.Items.Select(item => $"{item.Id:D}:{item.ItemText}:{item.IsCompleted}:{item.Priority}"));
        var contacts = string.Join("|", Contacts.Select(item => $"{item.Id:D}:{item.DisplayName}:{item.Phone}:{item.WeChat}:{item.Email}:{item.OtherContact}:{item.IsPrimary}:{item.Note}"));
        var staff = string.Join("|", Staff.Select(item => $"{item.Id:D}:{item.DisplayName}:{item.SelectedRole?.Value}:{item.ArrivalTimeText}:{item.Phone}:{item.WeChat}:{item.Email}:{item.Note}"));
        return string.Join("\u001f", new[]
        {
            Title, ClientDisplayName, StartDate.ToString("O"), StartTimeText, EndDate.ToString("O"), EndTimeText,
            SelectedTimeZone?.Id ?? string.Empty, IsAllDay.ToString(), Location, SelectedShootingType?.Value ?? string.Empty,
            SelectedStatus?.Value.ToString() ?? string.Empty, ShootingRequirements, PreparationNotes, TotalAmountText,
            DepositAmountText, PaidAmountText, SelectedProject?.Id?.ToString("D") ?? string.Empty, AllowOverlap.ToString(), Notes,
            requirements, contacts, staff
        });
    }

    private bool TryBuildDraft(out ShootBookingDraft? draft, out string validation, bool allowPartial = false)
    {
        draft = null;
        var errors = new List<string>();
        if (!allowPartial && string.IsNullOrWhiteSpace(Title)) errors.Add("项目名称不能为空。");
        if (!allowPartial && SelectedTimeZone is null) errors.Add("请选择有效时区。");
        if (!allowPartial && SelectedShootingType is null) errors.Add("请选择拍摄类型。");
        if (!allowPartial && SelectedStatus is null) errors.Add("请选择拍摄状态。");
        if (!allowPartial && SelectedStatus?.Value == ShootBookingStatus.Draft) errors.Add("正式创建前请选择非草稿状态。");
        if (!TryMinor(TotalAmountText, 2, "总金额", errors, out var total) || !TryMinor(DepositAmountText, 2, "定金", errors, out var deposit) || !TryMinor(PaidAmountText, 2, "已收金额", errors, out var paid))
        {
            validation = string.Join(Environment.NewLine, errors);
            return false;
        }
        if (total < 0 || deposit < 0 || paid < 0) errors.Add("金额不得为负数。");
        if (total.HasValue && deposit.HasValue && deposit > total) errors.Add("定金不能高于拍摄总金额。");
        var timeZone = SelectedTimeZone ?? TimeZoneOptions.FirstOrDefault(option => option.Id == TimeZoneInfo.Local.Id) ?? TimeZoneOptions.First();
        DateTimeOffset start = default;
        DateTimeOffset end = default;
        if (!TryBuildRange(timeZone.Id, out start, out end, out var timeError))
        {
            if (!allowPartial) errors.Add(timeError);
            else
            {
                // A draft is allowed to be incomplete, but it must still be a valid
                // persisted aggregate so that it can be resumed without losing input.
                var fallbackStart = DateTime.SpecifyKind(StartDate.Date.AddHours(9), DateTimeKind.Unspecified);
                var fallbackEnd = fallbackStart.AddHours(1);
                var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone.Id);
                start = new DateTimeOffset(fallbackStart, zone.GetUtcOffset(fallbackStart));
                end = new DateTimeOffset(fallbackEnd, zone.GetUtcOffset(fallbackEnd));
            }
        }
        if (errors.Count > 0) { validation = string.Join(Environment.NewLine, errors); return false; }

        var contacts = Contacts
            .Where(item => !allowPartial || !string.IsNullOrWhiteSpace(item.DisplayName))
            .Select(item => item.ToModel(_stableBookingId))
            .ToArray();
        var staff = Staff
            .Where(item => !allowPartial || !string.IsNullOrWhiteSpace(item.DisplayName))
            .Select((item, index) => item.ToModel(_stableBookingId, index, timeZone.Id))
            .ToArray();
        var requirements = Requirements.ToModels(_stableBookingId)
            .Where(item => !allowPartial || !string.IsNullOrWhiteSpace(item.ItemText))
            .ToArray();
        var title = string.IsNullOrWhiteSpace(Title) ? "未命名草稿" : Title;
        var shootingType = SelectedShootingType?.Value ?? "Other";
        var status = allowPartial ? ShootBookingStatus.Draft : SelectedStatus?.Value ?? ShootBookingStatus.Tentative;

        draft = new ShootBookingDraft
        {
            Id = _stableBookingId,
            ProjectId = SelectedProject?.Id,
            Title = title,
            ClientDisplayName = ClientDisplayName,
            ContactName = Contacts.FirstOrDefault(item => item.IsPrimary)?.DisplayName ?? Contacts.FirstOrDefault()?.DisplayName,
            ContactPhone = Contacts.FirstOrDefault(item => item.IsPrimary)?.Phone ?? Contacts.FirstOrDefault()?.Phone,
            StartAt = start,
            EndAt = end,
            TimeZoneId = timeZone.Id,
            IsAllDay = IsAllDay,
            Location = Location,
            ShootingType = shootingType,
            Status = status,
            ShootingRequirements = ShootingRequirements,
            PreparationNotes = PreparationNotes,
            TotalAmountMinor = total,
            DepositAmountMinor = deposit,
            PaidAmountMinor = paid,
            CurrencyCode = "CNY",
            CurrencyScale = 2,
            AllowOverlap = AllowOverlap,
            Notes = Notes,
            Requirements = requirements,
            EditorSessionId = _editorSessionId,
            CreateIfMissing = !_bookingId.HasValue,
            ReplacePeople = true,
            Contacts = contacts,
            Staff = staff
        };
        validation = string.Empty;
        return true;
    }

    private bool TryBuildRange(string timeZoneId, out DateTimeOffset start, out DateTimeOffset end, out string error)
    {
        try
        {
            if (IsAllDay)
            {
                var range = ShootBookingTimeRules.CreateAllDayRange(DateOnly.FromDateTime(StartDate), DateOnly.FromDateTime(EndDate), timeZoneId);
                start = range.StartAtUtc;
                end = range.EndAtUtc;
            }
            else
            {
                if (!TimeOnly.TryParse(StartTimeText, CultureInfo.CurrentCulture, DateTimeStyles.None, out var startTime) || !TimeOnly.TryParse(EndTimeText, CultureInfo.CurrentCulture, DateTimeStyles.None, out var endTime))
                    throw new FormatException("时间格式应为 HH:mm。");
                var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                start = ResolveLocal(StartDate, startTime, zone);
                end = ResolveLocal(EndDate, endTime, zone);
            }
            if (end <= start) throw new InvalidOperationException("结束时间必须晚于开始时间。");
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            start = end = default;
            error = ex.Message;
            return false;
        }
    }

    private static DateTimeOffset ResolveLocal(DateTime date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(date.Date.Add(time.ToTimeSpan()), DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local)) throw new InvalidOperationException("所选本地时间因夏令时切换而不存在。");
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }

    private void UpdateMoneyPreview()
    {
        var errors = new List<string>();
        if (!TryMinor(TotalAmountText, 2, "总金额", errors, out var total) || !TryMinor(DepositAmountText, 2, "定金", errors, out var deposit) || !TryMinor(PaidAmountText, 2, "已收金额", errors, out var paid))
        {
            MoneyWarningText = errors.FirstOrDefault() ?? string.Empty;
            return;
        }
        var validation = BookingMoneyCalculator.Validate(total, deposit, paid, 2);
        if (validation.Count > 0)
        {
            MoneyWarningText = validation[0];
            return;
        }
        UpdateMoney(BookingMoneyCalculator.Calculate(total, deposit, paid));
    }

    private void UpdateMoney(BookingMoneySummary money)
    {
        MoneyWarningText = money.Warnings.FirstOrDefault()?.Message ?? string.Empty;
        BalanceLabel = money.DisplayKind == BookingMoneyDisplayKind.Overpaid ? "多收金额" : "待收金额";
        BalanceText = money.DisplayAmountMinor.HasValue ? $"CNY {money.DisplayAmountMinor.Value / 100m:N2}" : "未记录";
    }

    private static bool TryMinor(string text, int scale, string field, List<string> errors, out long? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount))
        {
            errors.Add($"{field}格式无效。");
            return false;
        }
        try { value = checked((long)decimal.Round(amount * (decimal)Math.Pow(10, scale), 0, MidpointRounding.AwayFromZero)); }
        catch (OverflowException) { errors.Add($"{field}超出可记录范围。"); return false; }
        return true;
    }

    private static string MinorToText(long? value, int scale) => value.HasValue ? (value.Value / (decimal)Math.Pow(10, scale)).ToString($"F{scale}", CultureInfo.CurrentCulture) : string.Empty;
}

public sealed class BookingConflictViewModel(BookingConflict conflict)
{
    private DateTimeOffset LocalStart => BookingTimeDisplayService.Default.ToBookingTime(conflict.StartAtUtc, conflict.TimeZoneId);
    private DateTimeOffset LocalEnd => BookingTimeDisplayService.Default.ToBookingTime(conflict.EndAtUtc, conflict.TimeZoneId);
    public Guid BookingId { get; } = conflict.BookingId;
    public string Title { get; } = conflict.Title;
    public string ClientDisplayName { get; } = conflict.ClientDisplayName;
    public string TimeText => $"{LocalStart:yyyy-MM-dd HH:mm} — {LocalEnd:yyyy-MM-dd HH:mm}";
    public string DateText => LocalStart.Date == LocalEnd.Date ? LocalStart.ToString("yyyy-MM-dd") : $"{LocalStart:yyyy-MM-dd} 至 {LocalEnd:yyyy-MM-dd}";
    public string StartText => LocalStart.ToString("HH:mm");
    public string EndText => LocalEnd.ToString("HH:mm");
    public string DurationText { get; } = conflict.EndAtUtc - conflict.StartAtUtc >= TimeSpan.FromHours(1) ? $"{(conflict.EndAtUtc - conflict.StartAtUtc).TotalHours:0.#} 小时" : $"{Math.Max(1, (conflict.EndAtUtc - conflict.StartAtUtc).TotalMinutes):0} 分钟";
    public string CrossDayText => LocalStart.Date == LocalEnd.Date ? "当日排期" : "跨日排期";
    public string Location { get; } = conflict.Location ?? "未填写";
    public string OverlapText { get; } = conflict.Overlap.TotalHours >= 1 ? $"{conflict.Overlap.TotalHours:0.#} 小时" : $"{Math.Max(1, conflict.Overlap.TotalMinutes):0} 分钟";
    public string StatusText { get; } = CalendarText.Status(conflict.Status);
}

public sealed class ShootRequirementItemEditorViewModel : ObservableObject
{
    private string _itemText = string.Empty;
    private bool _isCompleted;
    private ShootRequirementPriority _priority = ShootRequirementPriority.Normal;
    public Guid Id { get; init; } = Guid.NewGuid();
    public string ItemText { get => _itemText; set => SetProperty(ref _itemText, value ?? string.Empty); }
    public bool IsCompleted { get => _isCompleted; set => SetProperty(ref _isCompleted, value); }
    public ShootRequirementPriority Priority { get => _priority; set => SetProperty(ref _priority, value); }
}

public sealed class ShootRequirementsViewModel : ObservableObject
{
    public ShootRequirementsViewModel()
    {
        AddCommand = new RelayCommand(_ => Items.Add(new()));
        RemoveCommand = new RelayCommand(parameter => { if (parameter is ShootRequirementItemEditorViewModel item) Items.Remove(item); });
        MoveUpCommand = new RelayCommand(parameter => Move(parameter as ShootRequirementItemEditorViewModel, -1));
        MoveDownCommand = new RelayCommand(parameter => Move(parameter as ShootRequirementItemEditorViewModel, 1));
        Items.CollectionChanged += Items_CollectionChanged;
    }

    public ObservableCollection<ShootRequirementItemEditorViewModel> Items { get; } = [];
    public IReadOnlyList<ShootRequirementPriority> PriorityOptions { get; } = Enum.GetValues<ShootRequirementPriority>();
    public ICommand AddCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public string CompletionText => Items.Count == 0 ? "尚未添加准备项" : $"完成 {Items.Count(item => item.IsCompleted)} / {Items.Count}（{Items.Count(item => item.IsCompleted) * 100 / Items.Count}%）";

    public void Load(IEnumerable<ShootRequirementItem> items)
    {
        Items.Clear();
        foreach (var item in items.OrderBy(item => item.SortOrder))
        {
            var editor = new ShootRequirementItemEditorViewModel { Id = item.Id, ItemText = item.ItemText, IsCompleted = item.IsCompleted, Priority = item.Priority };
            Items.Add(editor);
        }
        NotifyCompletion();
    }

    public IReadOnlyList<ShootRequirementItem> ToModels(Guid bookingId) => Items.Select((item, index) => new ShootRequirementItem
    {
        Id = item.Id,
        BookingId = bookingId,
        ItemText = item.ItemText,
        IsCompleted = item.IsCompleted,
        Priority = item.Priority,
        SortOrder = index
    }).ToArray();

    private void Move(ShootRequirementItemEditorViewModel? item, int offset)
    {
        if (item is null) return;
        var index = Items.IndexOf(item);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= Items.Count) return;
        Items.Move(index, target);
    }

    private void NotifyCompletion() => OnPropertyChanged(nameof(CompletionText));

    private void Items_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (ShootRequirementItemEditorViewModel item in e.OldItems) item.PropertyChanged -= Item_PropertyChanged;
        if (e.NewItems is not null)
            foreach (ShootRequirementItemEditorViewModel item in e.NewItems) item.PropertyChanged += Item_PropertyChanged;
        NotifyCompletion();
    }

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => NotifyCompletion();
}
