using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed record ProjectOption(Guid? Id, string Name);
public sealed record TimeZoneOption(string Id, string Label);
public sealed record BookingStatusEditorOption(ShootBookingStatus Value, string Label);
public sealed record ShootingTypeEditorOption(string Value, string Label);

public sealed class ShootBookingDetailsViewModel : ObservableObject
{
    private readonly IShootBookingService _service;
    private ShootBooking? _booking;
    private bool _amountsVisible;
    private bool _isBusy;
    private string _statusText = string.Empty;

    public ShootBookingDetailsViewModel(IShootBookingService service, IBookingDocumentWorkflowService? documentWorkflow = null, IDialogService? dialogs = null)
    {
        _service = service;
        if (documentWorkflow is not null && dialogs is not null) Documents = new BookingDocumentsViewModel(documentWorkflow, dialogs);
        ToggleAmountsCommand = new RelayCommand(_ => AmountsVisible = !AmountsVisible);
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(this, EventArgs.Empty));
        EditCommand = new RelayCommand(_ => { if (Booking is not null) EditRequested?.Invoke(this, Booking.Id); }, _ => Booking is { IsArchived: false });
        ArchiveCommand = new AsyncRelayCommand(_ => ArchiveAsync(), _ => Booking is { IsArchived: false });
    }

    public event EventHandler? CloseRequested;
    public event EventHandler<Guid>? EditRequested;
    public event EventHandler<Guid>? Archived;
    public ObservableCollection<ShootRequirementItem> Requirements { get; } = [];
    public BookingDocumentsViewModel? Documents { get; }
    public bool HasDocumentsPanel => Documents is not null;
    public ICommand ToggleAmountsCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand ArchiveCommand { get; }
    public ShootBooking? Booking { get => _booking; private set { if (SetProperty(ref _booking, value)) NotifyBooking(); } }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool AmountsVisible { get => _amountsVisible; set { if (SetProperty(ref _amountsVisible, value)) NotifyMoney(); } }
    public bool IsArchived => Booking?.IsArchived == true;
    public bool CanEdit => Booking is { IsArchived: false };
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
    private BookingMoneySummary Money => BookingMoneyCalculator.Calculate(Booking?.TotalAmountMinor, Booking?.DepositAmountMinor, Booking?.PaidAmountMinor);

    public async Task LoadAsync(Guid bookingId, bool includeArchived = false)
    {
        IsBusy = true;
        AmountsVisible = false;
        try
        {
            Booking = await _service.GetAsync(bookingId, includeArchived).ConfigureAwait(true);
            Requirements.Clear();
            if (Booking is not null)
            {
                foreach (var item in await _service.GetRequirementsAsync(bookingId).ConfigureAwait(true)) Requirements.Add(item);
                if (Documents is not null) await Documents.LoadAsync(Booking.Id, Booking.ProjectId, Booking.IsArchived).ConfigureAwait(true);
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
        StatusText = "排期已归档；提醒已关闭，关联数据与电脑文件均已保留。";
        Archived?.Invoke(this, Booking.Id);
    }

    private void NotifyBooking()
    {
        foreach (var name in new[] { nameof(IsArchived), nameof(CanEdit), nameof(Title), nameof(ClientDisplayName), nameof(TimeText), nameof(TimeZoneText), nameof(LocationText), nameof(ShootingTypeText), nameof(BookingStatusText), nameof(ShootingRequirementsText), nameof(PreparationNotesText), nameof(NotesText) }) OnPropertyChanged(name);
        NotifyMoney();
    }

    private void NotifyMoney()
    {
        foreach (var name in new[] { nameof(AmountVisibilityText), nameof(TotalAmountText), nameof(DepositAmountText), nameof(PaidAmountText), nameof(BalanceLabel), nameof(BalanceText), nameof(HasMoneyWarning), nameof(MoneyWarningText) }) OnPropertyChanged(name);
    }

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

public sealed class ShootBookingEditorViewModel : ObservableObject
{
    private readonly IShootBookingService _service;
    private readonly IProjectRepository _projectRepository;
    private readonly Guid? _bookingId;
    private readonly DateTime? _suggestedStart;
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

    public ShootBookingEditorViewModel(IShootBookingService service, IProjectRepository projectRepository, Guid? bookingId = null, DateTime? suggestedStart = null)
    {
        _service = service;
        _projectRepository = projectRepository;
        _bookingId = bookingId;
        _suggestedStart = suggestedStart;
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
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(BookingConflictResolution.None), _ => !IsBusy);
        SaveAnywayCommand = new AsyncRelayCommand(_ => SaveAsync(BookingConflictResolution.SaveAnyway), _ => !IsBusy && IsConflictVisible);
        MarkOverlapAndSaveCommand = new AsyncRelayCommand(_ => SaveAsync(BookingConflictResolution.MarkAllowOverlap), _ => !IsBusy && IsConflictVisible);
        ReturnToEditCommand = new RelayCommand(_ => { IsConflictVisible = false; Conflicts.Clear(); });
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler<ShootBooking>? Saved;
    public event EventHandler? CloseRequested;
    public IReadOnlyList<BookingStatusEditorOption> StatusOptions { get; }
    public IReadOnlyList<ShootingTypeEditorOption> ShootingTypeOptions { get; }
    public IReadOnlyList<TimeZoneOption> TimeZoneOptions { get; }
    public ObservableCollection<ProjectOption> ProjectOptions { get; } = [];
    public ObservableCollection<BookingConflictViewModel> Conflicts { get; } = [];
    public ShootRequirementsViewModel Requirements { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAnywayCommand { get; }
    public ICommand MarkOverlapAndSaveCommand { get; }
    public ICommand ReturnToEditCommand { get; }
    public ICommand CancelCommand { get; }
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
            IsConflictVisible = false;
            Saved?.Invoke(this, result.Booking);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { ValidationText = $"保存失败：{ex.Message}"; }
        finally { IsBusy = false; }
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
