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
public sealed record ShootingTypeEditorOption(string Value, string Label) { public override string ToString() => Label; }
public sealed record BookingStaffRoleOption(BookingStaffRole Value, string Label) { public override string ToString() => Label; }

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
    public BookingStaffMember ToModel(Guid bookingId, int sortOrder)
    {
        DateTimeOffset? arrival = DateTimeOffset.TryParse(ArrivalTimeText, out var parsed) ? parsed : null;
        return new() { Id = Id, BookingId = bookingId, DisplayName = DisplayName, Role = SelectedRole?.Value ?? BookingStaffRole.Other, ArrivalTime = arrival, Phone = Phone, WeChat = WeChat, Email = Email, Note = Note, SortOrder = sortOrder };
    }
    public static BookingStaffEditorViewModel From(BookingStaffMember value, IReadOnlyList<BookingStaffRoleOption> options) => new() { Id = value.Id, DisplayName = value.DisplayName, SelectedRole = options.First(x => x.Value == value.Role), ArrivalTimeText = value.ArrivalTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? string.Empty, Phone = value.Phone ?? string.Empty, WeChat = value.WeChat ?? string.Empty, Email = value.Email ?? string.Empty, Note = value.Note ?? string.Empty };
}

public sealed class BookingContactDetailsViewModel(BookingContact value)
{
    public string DisplayName => value.DisplayName + (value.IsPrimary ? "（主要联系人）" : string.Empty);
    public string ContactText => string.Join(" · ", new[] { value.Phone, value.WeChat, value.Email, value.OtherContact }.Where(item => !string.IsNullOrWhiteSpace(item))!);
}

public sealed class BookingStaffDetailsViewModel(BookingStaffMember value)
{
    public string DisplayName => value.DisplayName;
    public string RoleText => value.Role switch { BookingStaffRole.Photographer => "摄影师", BookingStaffRole.PhotographyAssistant => "摄影助理", BookingStaffRole.LightingTechnician => "灯光师", BookingStaffRole.MakeupArtist => "化妆师", BookingStaffRole.Stylist => "造型师", BookingStaffRole.ModelOrActor => "模特或演员", BookingStaffRole.ClientRepresentative => "客户代表", BookingStaffRole.FloorAssistant => "场务", _ => "其他" };
    public string ArrivalText => value.ArrivalTime?.ToLocalTime().ToString("M月d日 HH:mm 到场") ?? "未设置到场时间";
}

public sealed class ShootBookingDetailsViewModel : ObservableObject
{
    private readonly IShootBookingService _service;
    private readonly IBookingReminderScheduler? _reminderScheduler;
    private ShootBooking? _booking;
    private bool _amountsVisible;
    private bool _isBusy;
    private string _statusText = string.Empty;
    private readonly IBookingPeopleService? _peopleService;
    private readonly IFinanceService? _financeService;
    private FinanceSummary _financeSummary = new(0, 0, 0, 0, 0, 0);

    public ShootBookingDetailsViewModel(IShootBookingService service, IBookingDocumentWorkflowService? documentWorkflow = null, IDialogService? dialogs = null,
        IBookingReminderService? reminderService = null, IBookingReminderScheduler? reminderScheduler = null,
        IWeatherForecastService? weatherService = null, WeatherFeatureState? weatherState = null,
        IBookingPeopleService? peopleService = null, IFinanceService? financeService = null)
    {
        _service = service;
        _reminderScheduler = reminderScheduler;
        _peopleService = peopleService;
        _financeService = financeService;
        if (documentWorkflow is not null && dialogs is not null) Documents = new BookingDocumentsViewModel(documentWorkflow, dialogs);
        if (reminderService is not null) Reminders = new BookingRemindersViewModel(reminderService, reminderScheduler);
        if (weatherService is not null && weatherState is not null) Weather = new BookingWeatherViewModel(weatherService, weatherState);
        ToggleAmountsCommand = new RelayCommand(_ => AmountsVisible = !AmountsVisible);
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(this, EventArgs.Empty));
        EditCommand = new RelayCommand(_ => { if (Booking is not null) EditRequested?.Invoke(this, Booking.Id); }, _ => Booking is { IsArchived: false });
        CompleteCommand = new AsyncRelayCommand(_ => CompleteAsync(), _ => CanComplete);
        ArchiveCommand = new AsyncRelayCommand(_ => ArchiveAsync(), _ => Booking is { IsArchived: false });
        ViewFinanceCommand = new RelayCommand(_ => RequestFinance(null), _ => Booking is not null);
        AddIncomeCommand = new RelayCommand(_ => RequestFinance(FinanceTransactionKind.Income), _ => Booking is { IsArchived: false });
        AddExpenseCommand = new RelayCommand(_ => RequestFinance(FinanceTransactionKind.Expense), _ => Booking is { IsArchived: false });
    }

    public event EventHandler? CloseRequested;
    public event EventHandler<Guid>? EditRequested;
    public event EventHandler<Guid>? Completed;
    public event EventHandler<Guid>? Archived;
    public event EventHandler<BookingFinanceRequestEventArgs>? FinanceRequested;
    public ObservableCollection<ShootRequirementItem> Requirements { get; } = [];
    public ObservableCollection<BookingContactDetailsViewModel> Contacts { get; } = [];
    public ObservableCollection<BookingStaffDetailsViewModel> Staff { get; } = [];
    public BookingDocumentsViewModel? Documents { get; }
    public BookingRemindersViewModel? Reminders { get; }
    public BookingWeatherViewModel? Weather { get; }
    public bool HasDocumentsPanel => Documents is not null;
    public bool HasRemindersPanel => Reminders is not null;
    public bool HasWeatherPanel => Weather is not null;
    public ICommand ToggleAmountsCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand CompleteCommand { get; }
    public ICommand ArchiveCommand { get; }
    public ICommand ViewFinanceCommand { get; }
    public ICommand AddIncomeCommand { get; }
    public ICommand AddExpenseCommand { get; }
    public ShootBooking? Booking { get => _booking; private set { if (SetProperty(ref _booking, value)) NotifyBooking(); } }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool AmountsVisible { get => _amountsVisible; set { if (SetProperty(ref _amountsVisible, value)) NotifyMoney(); } }
    public bool IsArchived => Booking?.IsArchived == true;
    public bool CanEdit => Booking is { IsArchived: false };
    public bool CanComplete => Booking is { IsArchived: false, Status: not ShootBookingStatus.Completed and not ShootBookingStatus.Cancelled };
    public string Title => Booking?.Title ?? "排期详情";
    public string ClientDisplayName => Booking?.ClientDisplayName ?? "—";
    public string TimeText => Booking is null ? "—" : FormatTime(Booking);
    public string TimeZoneText => Booking?.TimeZoneId ?? "—";
    public string LocationText => Booking?.Location ?? "未填写";
    public string ShootingTypeText => Booking is null ? "—" : CalendarText.Type(Booking.ShootingType);
    public string BookingStatusText => Booking is null ? "—" : CalendarText.Status(Booking.Status);
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
                    foreach (var item in await _peopleService.ListStaffAsync(bookingId).ConfigureAwait(true)) Staff.Add(new(item));
                }
                if (Documents is not null) await Documents.LoadAsync(Booking.Id, Booking.ProjectId, Booking.IsArchived).ConfigureAwait(true);
                if (Reminders is not null) await Reminders.LoadAsync(Booking.Id, Booking.ProjectId, Booking.IsArchived).ConfigureAwait(true);
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

    private async Task ArchiveAsync()
    {
        if (Booking is null || !await _service.ArchiveAsync(Booking.Id).ConfigureAwait(true)) return;
        Booking = Booking with { IsArchived = true, ArchivedAtUtc = DateTimeOffset.UtcNow };
        if (Documents is not null) await Documents.LoadAsync(Booking.Id, Booking.ProjectId, isArchived: true).ConfigureAwait(true);
        if (Reminders is not null) await Reminders.LoadAsync(Booking.Id, Booking.ProjectId, isReadOnly: true).ConfigureAwait(true);
        if (_reminderScheduler is not null) await _reminderScheduler.RefreshAsync().ConfigureAwait(true);
        StatusText = "排期已归档；提醒已关闭，关联数据与电脑文件均已保留。";
        Archived?.Invoke(this, Booking.Id);
    }

    private async Task CompleteAsync()
    {
        if (Booking is null || !await _service.CompleteAsync(Booking.Id).ConfigureAwait(true)) return;
        Booking = Booking with { Status = ShootBookingStatus.Completed, UpdatedAtUtc = DateTimeOffset.UtcNow };
        if (Reminders is not null) await Reminders.LoadAsync(Booking.Id, Booking.ProjectId, isReadOnly: false).ConfigureAwait(true);
        if (_reminderScheduler is not null) await _reminderScheduler.RefreshAsync().ConfigureAwait(true);
        StatusText = "拍摄已完成；未来未触发提醒已关闭，排期和历史记录均已保留。";
        Completed?.Invoke(this, Booking.Id);
    }

    private void RequestFinance(FinanceTransactionKind? kind)
    {
        if (Booking is not null) FinanceRequested?.Invoke(this, new(Booking.Id, kind));
    }

    private void NotifyBooking()
    {
        foreach (var name in new[] { nameof(IsArchived), nameof(CanEdit), nameof(CanComplete), nameof(Title), nameof(ClientDisplayName), nameof(TimeText), nameof(TimeZoneText), nameof(LocationText), nameof(ShootingTypeText), nameof(BookingStatusText), nameof(ShootingRequirementsText), nameof(PreparationNotesText), nameof(NotesText) }) OnPropertyChanged(name);
        (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CompleteCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ArchiveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ViewFinanceCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddIncomeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddExpenseCommand as RelayCommand)?.RaiseCanExecuteChanged();
        NotifyMoney();
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

    private static string FormatTime(ShootBooking booking)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(booking.TimeZoneId); } catch { zone = TimeZoneInfo.Local; }
        var start = TimeZoneInfo.ConvertTime(booking.StartAtUtc, zone);
        var end = TimeZoneInfo.ConvertTime(booking.EndAtUtc, zone);
        return booking.IsAllDay ? $"{start:yyyy-MM-dd} 至 {end.AddDays(-1):yyyy-MM-dd}（全天）" : $"{start:yyyy-MM-dd HH:mm} — {end:yyyy-MM-dd HH:mm}";
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
    private readonly DateTime? _suggestedStart;
    private readonly IBookingPeopleService? _peopleService;
    private readonly IDialogService? _dialogs;
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

    public ShootBookingEditorViewModel(IShootBookingService service, IProjectRepository projectRepository, Guid? bookingId = null, DateTime? suggestedStart = null,
        IBookingPeopleService? peopleService = null, IBookingDocumentWorkflowService? documentWorkflow = null, IDialogService? dialogs = null)
    {
        _service = service;
        _projectRepository = projectRepository;
        _bookingId = bookingId;
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
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(BookingConflictResolution.None), _ => !IsBusy);
        SaveDraftCommand = new AsyncRelayCommand(_ => SaveDraftAsync(), _ => !IsBusy);
        SaveAnywayCommand = new AsyncRelayCommand(_ => SaveAsync(BookingConflictResolution.SaveAnyway), _ => !IsBusy && IsConflictVisible);
        MarkOverlapAndSaveCommand = new AsyncRelayCommand(_ => SaveAsync(BookingConflictResolution.MarkAllowOverlap), _ => !IsBusy && IsConflictVisible);
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
    }

    public event EventHandler<ShootBooking>? Saved;
    public event EventHandler? CloseRequested;
    public IReadOnlyList<BookingStatusEditorOption> StatusOptions { get; }
    public IReadOnlyList<ShootingTypeEditorOption> ShootingTypeOptions { get; }
    public IReadOnlyList<TimeZoneOption> TimeZoneOptions { get; }
    public ObservableCollection<ProjectOption> ProjectOptions { get; } = [];
    public ObservableCollection<BookingConflictViewModel> Conflicts { get; } = [];
    public ShootRequirementsViewModel Requirements { get; }
    public BookingDocumentsViewModel? Documents { get; }
    public ObservableCollection<BookingContactEditorViewModel> Contacts { get; } = [];
    public ObservableCollection<BookingStaffEditorViewModel> Staff { get; } = [];
    public IReadOnlyList<BookingStaffRoleOption> StaffRoleOptions { get; }
    public ICommand SaveCommand { get; }
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
    public string DialogTitle => _bookingId.HasValue ? "编辑拍摄排期" : "新建拍摄排期";
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
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
    public string Location { get => _location; set => SetProperty(ref _location, value ?? string.Empty); }
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
    public int CurrentStep { get => _currentStep; set { var next = Math.Clamp(value, 1, 4); if (!SetProperty(ref _currentStep, next)) return; OnPropertyChanged(nameof(CurrentStepIndex)); OnPropertyChanged(nameof(StepTitle)); (NextStepCommand as RelayCommand)?.RaiseCanExecuteChanged(); (PreviousStepCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }
    public int CurrentStepIndex { get => CurrentStep - 1; set => CurrentStep = value + 1; }
    public string StepTitle => CurrentStep switch { 1 => "1 基础信息", 2 => "2 时间、天气与准备", 3 => "3 策划资料", _ => "4 联系人、工作人员与收支" };

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
            foreach (var item in await _peopleService.ListStaffAsync(booking.Id).ConfigureAwait(true)) Staff.Add(BookingStaffEditorViewModel.From(item, StaffRoleOptions));
        }
        if (Documents is not null) await Documents.LoadAsync(booking.Id, booking.ProjectId, booking.IsArchived).ConfigureAwait(true);
        _initialSignature = BuildEditSignature();
    }

    private async Task SaveAsync(BookingConflictResolution resolution)
    {
        ValidationText = string.Empty;
        if (!TryBuildDraft(out var draft, out var validation))
        {
            ValidationText = validation;
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
                return;
            }
            if (result.Status != BookingSaveStatus.Saved || result.Booking is null)
            {
                ValidationText = string.Join(Environment.NewLine, result.ValidationErrors.DefaultIfEmpty("排期未保存。"));
                return;
            }
            if (_peopleService is not null)
            {
                try
                {
                    await _peopleService.SaveAsync(result.Booking.Id, Contacts.Select(item => item.ToModel(result.Booking.Id)).ToArray(), Staff.Select((item, index) => item.ToModel(result.Booking.Id, index)).ToArray()).ConfigureAwait(true);
                }
                catch
                {
                    ValidationText = "排期基础信息已保存，但联系人或工作人员未完成保存。请检查必填姓名与主要联系人设置后重试；不会删除任何文件。";
                    Saved?.Invoke(this, result.Booking);
                    return;
                }
            }
            IsConflictVisible = false;
            _wasSaved = true;
            Saved?.Invoke(this, result.Booking);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { ValidationText = $"保存失败：{ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task SaveDraftAsync()
    {
        SelectedStatus = StatusOptions.First(option => option.Value == ShootBookingStatus.Tentative);
        await SaveAsync(BookingConflictResolution.None).ConfigureAwait(true);
    }

    private void MoveStaff(BookingStaffEditorViewModel? item, int offset)
    {
        if (item is null) return;
        var current = Staff.IndexOf(item);
        var target = current + offset;
        if (current < 0 || target < 0 || target >= Staff.Count) return;
        Staff.Move(current, target);
    }

    public bool HasUnsavedChanges => !_wasSaved && _initialSignature is not null && !string.Equals(_initialSignature, BuildEditSignature(), StringComparison.Ordinal);
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

    private bool TryBuildDraft(out ShootBookingDraft? draft, out string validation)
    {
        draft = null;
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Title)) errors.Add("项目名称不能为空。");
        if (SelectedTimeZone is null) errors.Add("请选择有效时区。");
        if (SelectedShootingType is null) errors.Add("请选择拍摄类型。");
        if (SelectedStatus is null) errors.Add("请选择拍摄状态。");
        if (!TryMinor(TotalAmountText, 2, "总金额", errors, out var total) || !TryMinor(DepositAmountText, 2, "定金", errors, out var deposit) || !TryMinor(PaidAmountText, 2, "已收金额", errors, out var paid))
        {
            validation = string.Join(Environment.NewLine, errors);
            return false;
        }
        if (total < 0 || deposit < 0 || paid < 0) errors.Add("金额不得为负数。");
        DateTimeOffset start = default;
        DateTimeOffset end = default;
        if (SelectedTimeZone is not null && !TryBuildRange(SelectedTimeZone.Id, out start, out end, out var timeError)) errors.Add(timeError);
        if (errors.Count > 0) { validation = string.Join(Environment.NewLine, errors); return false; }

        draft = new ShootBookingDraft
        {
            Id = _bookingId,
            ProjectId = SelectedProject?.Id,
            Title = Title,
            ClientDisplayName = ClientDisplayName,
            ContactName = Contacts.FirstOrDefault(item => item.IsPrimary)?.DisplayName ?? Contacts.FirstOrDefault()?.DisplayName,
            ContactPhone = Contacts.FirstOrDefault(item => item.IsPrimary)?.Phone ?? Contacts.FirstOrDefault()?.Phone,
            StartAt = start,
            EndAt = end,
            TimeZoneId = SelectedTimeZone!.Id,
            IsAllDay = IsAllDay,
            Location = Location,
            ShootingType = SelectedShootingType!.Value,
            Status = SelectedStatus!.Value,
            ShootingRequirements = ShootingRequirements,
            PreparationNotes = PreparationNotes,
            TotalAmountMinor = total,
            DepositAmountMinor = deposit,
            PaidAmountMinor = paid,
            CurrencyCode = "CNY",
            CurrencyScale = 2,
            AllowOverlap = AllowOverlap,
            Notes = Notes,
            Requirements = Requirements.ToModels(_bookingId ?? Guid.Empty)
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
    public string Title { get; } = conflict.Title;
    public string ClientDisplayName { get; } = conflict.ClientDisplayName;
    public string TimeText { get; } = $"{conflict.StartAtUtc.ToLocalTime():yyyy-MM-dd HH:mm} — {conflict.EndAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}";
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
