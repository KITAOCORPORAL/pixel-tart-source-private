using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Business;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed record FinanceKindOption(FinanceTransactionKind Value, string Label);
public sealed record FinanceKindFilterOption(FinanceTransactionKind? Value, string Label);
public sealed record FinancePaymentOption(FinancePaymentStatus Value, string Label);
public sealed record FinancePaymentFilterOption(FinancePaymentStatus? Value, string Label);
public sealed record FinanceCategoryFilterOption(Guid? Id, string Label);
public sealed record FinanceLinkOption(Guid? Id, string Label);

public sealed class FinanceAttachmentItem(string fullPath)
{
    public string FullPath { get; } = Path.GetFullPath(fullPath);
    public string DisplayName => Path.GetFileName(FullPath);
    public string StateText => File.Exists(FullPath) ? "仅关联原位置" : "当前不可访问";
}

public sealed class FinanceTransactionItemViewModel(
    FinanceTransaction transaction,
    string categoryName,
    string bookingName,
    string projectName)
{
    public FinanceTransaction Transaction { get; } = transaction;
    public Guid Id => Transaction.Id;
    public string KindText => Transaction.Kind == FinanceTransactionKind.Income ? "收入" : "支出";
    public string CategoryName { get; } = categoryName;
    public string AmountText => $"{(Transaction.Kind == FinanceTransactionKind.Income ? "+" : "-")} {Transaction.AmountMinor / (decimal)Math.Pow(10, Transaction.CurrencyScale):N2} {Transaction.CurrencyCode}";
    public string DateText => Transaction.OccurredOn.ToString("yyyy-MM-dd");
    public string PaymentStatusText => FinanceViewModel.PaymentStatusText(Transaction.PaymentStatus);
    public string CounterpartyText => string.IsNullOrWhiteSpace(Transaction.Counterparty) ? "—" : Transaction.Counterparty;
    public string BookingText { get; } = bookingName;
    public string ProjectText { get; } = projectName;
    public string AttachmentText => Transaction.AttachmentPaths.Count == 0 ? "无附件" : $"{Transaction.AttachmentPaths.Count} 个附件引用";
}

public sealed class FinanceViewModel : ObservableObject
{
    private readonly IFinanceService _service;
    private readonly IDialogService _dialogs;
    private readonly IProjectRepository? _projectRepository;
    private readonly IShootBookingService? _bookingService;
    private IReadOnlyList<FinanceCategory> _allCategories = [];
    private bool _isBusy;
    private string _statusText = "本地摄影业务记账，不替代会计或税务软件。";
    private string _keyword = string.Empty;
    private FinanceTransactionItemViewModel? _selectedTransaction;
    private Guid? _editingId;
    private FinanceKindOption _selectedKind;
    private FinanceCategory? _selectedCategory;
    private FinancePaymentOption _selectedPaymentStatus;
    private FinanceKindFilterOption _selectedKindFilter;
    private FinancePaymentFilterOption _selectedPaymentFilter;
    private FinanceCategoryFilterOption _selectedCategoryFilter;
    private FinanceLinkOption _selectedBookingFilter;
    private FinanceLinkOption _selectedProjectFilter;
    private FinanceLinkOption _selectedBookingLink;
    private FinanceLinkOption _selectedProjectLink;
    private string _amountText = string.Empty;
    private DateTime _occurredOn = DateTime.Today;
    private DateTime _selectedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private string _currencyCode = "CNY";
    private string _counterparty = string.Empty;
    private string _paymentMethod = string.Empty;
    private string _note = string.Empty;
    private FinanceSummary _summary = new(0, 0, 0, 0, 0, 0);

    public FinanceViewModel(
        IFinanceService service,
        IDialogService dialogs,
        IProjectRepository? projectRepository = null,
        IShootBookingService? bookingService = null)
    {
        _service = service;
        _dialogs = dialogs;
        _projectRepository = projectRepository;
        _bookingService = bookingService;

        KindOptions = [new(FinanceTransactionKind.Income, "收入"), new(FinanceTransactionKind.Expense, "支出")];
        KindFilterOptions = [new(null, "全部类型"), new(FinanceTransactionKind.Income, "收入"), new(FinanceTransactionKind.Expense, "支出")];
        PaymentOptions = Enum.GetValues<FinancePaymentStatus>().Select(value => new FinancePaymentOption(value, PaymentStatusText(value))).ToArray();
        PaymentFilterOptions = [new(null, "全部支付状态"), .. Enum.GetValues<FinancePaymentStatus>().Select(value => new FinancePaymentFilterOption(value, PaymentStatusText(value)))];
        CurrencyOptions = ["CNY", "USD", "EUR", "JPY", "HKD"];

        _selectedKind = KindOptions[0];
        _selectedPaymentStatus = PaymentOptions.First(item => item.Value == FinancePaymentStatus.Receivable);
        _selectedKindFilter = KindFilterOptions[0];
        _selectedPaymentFilter = PaymentFilterOptions[0];
        _selectedCategoryFilter = new(null, "全部分类");
        _selectedBookingFilter = new(null, "全部拍摄任务");
        _selectedProjectFilter = new(null, "全部项目");
        _selectedBookingLink = new(null, "不关联拍摄任务");
        _selectedProjectLink = new(null, "不关联项目");

        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync(), _ => !IsBusy);
        PreviousMonthCommand = new RelayCommand(_ => SelectedMonth = SelectedMonth.AddMonths(-1));
        NextMonthCommand = new RelayCommand(_ => SelectedMonth = SelectedMonth.AddMonths(1));
        CurrentMonthCommand = new RelayCommand(_ => SelectedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1));
        NewIncomeCommand = new RelayCommand(_ => BeginNew(FinanceTransactionKind.Income));
        NewExpenseCommand = new RelayCommand(_ => BeginNew(FinanceTransactionKind.Expense));
        EditCommand = new RelayCommand(_ => BeginEdit(SelectedTransaction), _ => SelectedTransaction is not null);
        CopyCommand = new RelayCommand(_ => BeginCopy(SelectedTransaction), _ => SelectedTransaction is not null);
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(), _ => !IsBusy);
        CancelCommand = new RelayCommand(_ => ResetEditor());
        DeleteCommand = new AsyncRelayCommand(_ => DeleteAsync(), _ => SelectedTransaction is not null && !IsBusy);
        ExportCommand = new AsyncRelayCommand(_ => ExportAsync(), _ => !IsBusy);
        AddAttachmentCommand = new RelayCommand(_ => AddAttachments());
        RemoveAttachmentCommand = new RelayCommand(parameter => RemoveAttachment(parameter as FinanceAttachmentItem));
    }

    public ObservableCollection<FinanceTransactionItemViewModel> Transactions { get; } = [];
    public ObservableCollection<FinanceCategory> AvailableCategories { get; } = [];
    public ObservableCollection<FinanceCategoryFilterOption> CategoryFilterOptions { get; } = [];
    public ObservableCollection<FinanceLinkOption> BookingFilterOptions { get; } = [];
    public ObservableCollection<FinanceLinkOption> ProjectFilterOptions { get; } = [];
    public ObservableCollection<FinanceLinkOption> BookingLinkOptions { get; } = [];
    public ObservableCollection<FinanceLinkOption> ProjectLinkOptions { get; } = [];
    public ObservableCollection<FinanceAttachmentItem> Attachments { get; } = [];
    public IReadOnlyList<FinanceKindOption> KindOptions { get; }
    public IReadOnlyList<FinanceKindFilterOption> KindFilterOptions { get; }
    public IReadOnlyList<FinancePaymentOption> PaymentOptions { get; }
    public IReadOnlyList<FinancePaymentFilterOption> PaymentFilterOptions { get; }
    public IReadOnlyList<string> CurrencyOptions { get; }

    public ICommand RefreshCommand { get; }
    public ICommand PreviousMonthCommand { get; }
    public ICommand NextMonthCommand { get; }
    public ICommand CurrentMonthCommand { get; }
    public ICommand NewIncomeCommand { get; }
    public ICommand NewExpenseCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand AddAttachmentCommand { get; }
    public ICommand RemoveAttachmentCommand { get; }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public bool IsEditing => _editingId.HasValue || !string.IsNullOrWhiteSpace(AmountText);
    public string EditorTitle => _editingId.HasValue ? "编辑收支记录" : "新建收支记录";
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string Keyword { get => _keyword; set { if (SetProperty(ref _keyword, value ?? string.Empty)) _ = RefreshAsync(); } }
    public FinanceTransactionItemViewModel? SelectedTransaction { get => _selectedTransaction; set { if (SetProperty(ref _selectedTransaction, value)) RefreshCommandStates(); } }
    public FinanceKindOption SelectedKind { get => _selectedKind; set { if (value is not null && SetProperty(ref _selectedKind, value)) { RefreshCategoryChoices(); SelectedPaymentStatus = PaymentOptions.First(item => item.Value == (value.Value == FinanceTransactionKind.Income ? FinancePaymentStatus.Receivable : FinancePaymentStatus.Payable)); } } }
    public FinanceCategory? SelectedCategory { get => _selectedCategory; set => SetProperty(ref _selectedCategory, value); }
    public FinancePaymentOption SelectedPaymentStatus { get => _selectedPaymentStatus; set => SetProperty(ref _selectedPaymentStatus, value); }
    public FinanceKindFilterOption SelectedKindFilter { get => _selectedKindFilter; set { if (value is not null && SetProperty(ref _selectedKindFilter, value)) _ = RefreshAsync(); } }
    public FinancePaymentFilterOption SelectedPaymentFilter { get => _selectedPaymentFilter; set { if (value is not null && SetProperty(ref _selectedPaymentFilter, value)) _ = RefreshAsync(); } }
    public FinanceCategoryFilterOption SelectedCategoryFilter { get => _selectedCategoryFilter; set { if (value is not null && SetProperty(ref _selectedCategoryFilter, value)) _ = RefreshAsync(); } }
    public FinanceLinkOption SelectedBookingFilter { get => _selectedBookingFilter; set { if (value is not null && SetProperty(ref _selectedBookingFilter, value)) _ = RefreshAsync(); } }
    public FinanceLinkOption SelectedProjectFilter { get => _selectedProjectFilter; set { if (value is not null && SetProperty(ref _selectedProjectFilter, value)) _ = RefreshAsync(); } }
    public FinanceLinkOption SelectedBookingLink { get => _selectedBookingLink; set => SetProperty(ref _selectedBookingLink, value); }
    public FinanceLinkOption SelectedProjectLink { get => _selectedProjectLink; set => SetProperty(ref _selectedProjectLink, value); }
    public string AmountText { get => _amountText; set { if (SetProperty(ref _amountText, value ?? string.Empty)) OnPropertyChanged(nameof(IsEditing)); } }
    public DateTime OccurredOn { get => _occurredOn; set => SetProperty(ref _occurredOn, value.Date); }
    public DateTime SelectedMonth { get => _selectedMonth; set { var month = new DateTime(value.Year, value.Month, 1); if (SetProperty(ref _selectedMonth, month)) { OnPropertyChanged(nameof(SelectedMonthText)); _ = RefreshAsync(); } } }
    public string SelectedMonthText => SelectedMonth.ToString("yyyy年M月");
    public string CurrencyCode { get => _currencyCode; set => SetProperty(ref _currencyCode, value ?? string.Empty); }
    public string Counterparty { get => _counterparty; set => SetProperty(ref _counterparty, value ?? string.Empty); }
    public string PaymentMethod { get => _paymentMethod; set => SetProperty(ref _paymentMethod, value ?? string.Empty); }
    public string Note { get => _note; set => SetProperty(ref _note, value ?? string.Empty); }
    public string MonthIncomeText => Money(_summary.IncomeMinor);
    public string MonthExpenseText => Money(_summary.ExpenseMinor);
    public string NetCashFlowText => Money(_summary.NetCashFlowMinor);
    public string ReceivableText => Money(_summary.ReceivableMinor);
    public string PayableText => Money(_summary.PayableMinor);
    public string ExpectedProfitText => Money(_summary.ExpectedProfitMinor);

    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await LoadChoicesAsync().ConfigureAwait(true);
            var query = BuildQuery();
            var items = await _service.QueryAsync(query).ConfigureAwait(true);
            Transactions.Clear();
            foreach (var item in items)
            {
                Transactions.Add(new(
                    item,
                    _allCategories.FirstOrDefault(category => category.Id == item.CategoryId)?.Name ?? "未分类",
                    BookingLinkOptions.FirstOrDefault(option => option.Id == item.BookingId)?.Label ?? "未关联拍摄",
                    ProjectLinkOptions.FirstOrDefault(option => option.Id == item.ProjectId)?.Label ?? "未关联项目"));
            }
            _summary = await _service.SummarizeAsync(query).ConfigureAwait(true);
            NotifySummary();
            StatusText = $"{SelectedMonthText}共 {Transactions.Count} 条本地收支记录。CSV 仅在你主动导出时生成。";
        }
        catch
        {
            StatusText = "收支数据暂时无法读取；未对任何项目或文件执行修改。";
        }
        finally
        {
            IsBusy = false;
            RefreshCommandStates();
        }
    }

    public async Task OpenForBookingAsync(Guid bookingId, FinanceTransactionKind? newKind = null)
    {
        await LoadChoicesAsync().ConfigureAwait(true);
        _selectedBookingFilter = BookingFilterOptions.FirstOrDefault(option => option.Id == bookingId) ?? BookingFilterOptions[0];
        OnPropertyChanged(nameof(SelectedBookingFilter));
        await RefreshAsync().ConfigureAwait(true);
        if (newKind is null) return;
        BeginNew(newKind.Value);
        SelectedBookingLink = BookingLinkOptions.FirstOrDefault(option => option.Id == bookingId) ?? BookingLinkOptions[0];
    }

#if RC3_REVIEW_ONLY
    public void ApplyReviewState(string state)
    {
        var incomeCategory = new FinanceCategory { Id = Guid.Parse("23030000-0000-0000-0000-000000000001"), Kind = FinanceTransactionKind.Income, Name = "拍摄定金" };
        var expenseCategory = new FinanceCategory { Id = Guid.Parse("23030000-0000-0000-0000-000000000002"), Kind = FinanceTransactionKind.Expense, Name = "场地费" };
        _allCategories = [incomeCategory, expenseCategory];
        AvailableCategories.Clear();
        AvailableCategories.Add(incomeCategory);
        AvailableCategories.Add(expenseCategory);
        Transactions.Clear();
        var bookingId = Guid.Parse("23030000-0000-0000-0000-000000000010");
        var projectId = Guid.Parse("23030000-0000-0000-0000-000000000011");
        var rows = new[]
        {
            new FinanceTransaction { Id=Guid.Parse("23030000-0000-0000-0000-000000000101"), Kind=FinanceTransactionKind.Income, CategoryId=incomeCategory.Id, AmountMinor=280000, PaymentStatus=FinancePaymentStatus.Received, OccurredOn=DateOnly.FromDateTime(DateTime.Today.AddDays(-6)), BookingId=bookingId, ProjectId=projectId, Counterparty="合成演示客户", PaymentMethod="转账" },
            new FinanceTransaction { Id=Guid.Parse("23030000-0000-0000-0000-000000000102"), Kind=FinanceTransactionKind.Income, CategoryId=incomeCategory.Id, AmountMinor=520000, PaymentStatus=FinancePaymentStatus.Receivable, OccurredOn=DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), BookingId=bookingId, ProjectId=projectId, Counterparty="合成演示客户" },
            new FinanceTransaction { Id=Guid.Parse("23030000-0000-0000-0000-000000000103"), Kind=FinanceTransactionKind.Expense, CategoryId=expenseCategory.Id, AmountMinor=120000, PaymentStatus=FinancePaymentStatus.Paid, OccurredOn=DateOnly.FromDateTime(DateTime.Today.AddDays(-4)), BookingId=bookingId, ProjectId=projectId, Counterparty="合成演示影棚", AttachmentCount=1, AttachmentPaths=["[隔离附件引用]"] },
            new FinanceTransaction { Id=Guid.Parse("23030000-0000-0000-0000-000000000104"), Kind=FinanceTransactionKind.Expense, CategoryId=expenseCategory.Id, AmountMinor=60000, PaymentStatus=FinancePaymentStatus.Payable, OccurredOn=DateOnly.FromDateTime(DateTime.Today), BookingId=bookingId, ProjectId=projectId, Counterparty="合成演示工作人员" }
        };
        foreach (var row in rows)
            Transactions.Add(new(row, row.Kind == FinanceTransactionKind.Income ? incomeCategory.Name : expenseCategory.Name, "今日品牌人像拍摄", "RC3 合成摄影项目"));
        _summary = new(280000, 120000, 160000, 520000, 60000, 620000);
        NotifySummary();
        StatusText = state switch
        {
            "FinanceProjectSummary" => "RC3 项目收支摘要：预计收入、已收、支出和当前净收益均来自本机合成记录。",
            "FinanceFilters" => "RC3 收支筛选：按月份、类型、项目、拍摄任务和分类组合查询。",
            "FinanceIncome" => "RC3 新建收入：金额信息仅保存在本机。",
            "FinanceExpense" => "RC3 新建支出：附件只保存本地引用，不保存文件正文。",
            _ => "RC3 隔离验收数据：金额、客户和附件均为合成内容，仅保存在验收配置中。"
        };
        SelectedTransaction = Transactions.FirstOrDefault();
        if (state == "FinanceIncome") BeginNew(FinanceTransactionKind.Income);
        else if (state == "FinanceExpense") BeginNew(FinanceTransactionKind.Expense);
        else if (state == "FinanceFilters") Keyword = "合成演示";
    }
#endif

    private async Task LoadChoicesAsync()
    {
        _allCategories = await _service.ListCategoriesAsync().ConfigureAwait(true);
        RefreshCategoryChoices();
        var selectedCategoryId = SelectedCategoryFilter.Id;
        Replace(CategoryFilterOptions, [new(null, "全部分类"), .. _allCategories.OrderBy(item => item.Kind).ThenBy(item => item.SortOrder).Select(item => new FinanceCategoryFilterOption(item.Id, item.Name))]);
        SelectedCategoryFilter = CategoryFilterOptions.FirstOrDefault(item => item.Id == selectedCategoryId) ?? CategoryFilterOptions[0];

        if (_projectRepository is not null)
        {
            var selectedFilter = SelectedProjectFilter.Id;
            var selectedLink = SelectedProjectLink.Id;
            var projects = await _projectRepository.ListAsync().ConfigureAwait(true);
            Replace(ProjectFilterOptions, [new(null, "全部项目"), .. projects.Select(item => new FinanceLinkOption(item.Id, item.Name))]);
            Replace(ProjectLinkOptions, [new(null, "不关联项目"), .. projects.Select(item => new FinanceLinkOption(item.Id, item.Name))]);
            _selectedProjectFilter = ProjectFilterOptions.FirstOrDefault(item => item.Id == selectedFilter) ?? ProjectFilterOptions[0];
            _selectedProjectLink = ProjectLinkOptions.FirstOrDefault(item => item.Id == selectedLink) ?? ProjectLinkOptions[0];
            OnPropertyChanged(nameof(SelectedProjectFilter));
            OnPropertyChanged(nameof(SelectedProjectLink));
        }
        else if (ProjectFilterOptions.Count == 0)
        {
            ProjectFilterOptions.Add(_selectedProjectFilter);
            ProjectLinkOptions.Add(_selectedProjectLink);
        }

        if (_bookingService is not null)
        {
            var selectedFilter = SelectedBookingFilter.Id;
            var selectedLink = SelectedBookingLink.Id;
            var bookings = (await _bookingService.SearchAllUnarchivedAsync(new(PageSize: 100)).ConfigureAwait(true)).Items;
            Replace(BookingFilterOptions, [new(null, "全部拍摄任务"), .. bookings.Select(item => new FinanceLinkOption(item.Id, $"{item.StartAtUtc.ToLocalTime():MM-dd} · {item.Title}"))]);
            Replace(BookingLinkOptions, [new(null, "不关联拍摄任务"), .. bookings.Select(item => new FinanceLinkOption(item.Id, $"{item.StartAtUtc.ToLocalTime():MM-dd} · {item.Title}"))]);
            _selectedBookingFilter = BookingFilterOptions.FirstOrDefault(item => item.Id == selectedFilter) ?? BookingFilterOptions[0];
            _selectedBookingLink = BookingLinkOptions.FirstOrDefault(item => item.Id == selectedLink) ?? BookingLinkOptions[0];
            OnPropertyChanged(nameof(SelectedBookingFilter));
            OnPropertyChanged(nameof(SelectedBookingLink));
        }
        else if (BookingFilterOptions.Count == 0)
        {
            BookingFilterOptions.Add(_selectedBookingFilter);
            BookingLinkOptions.Add(_selectedBookingLink);
        }
    }

    private FinanceQuery BuildQuery()
    {
        var from = DateOnly.FromDateTime(SelectedMonth);
        var to = from.AddMonths(1).AddDays(-1);
        return new(
            from,
            to,
            SelectedKindFilter.Value,
            SelectedPaymentFilter.Value,
            SelectedBookingFilter.Id,
            SelectedProjectFilter.Id,
            SelectedCategoryFilter.Id,
            string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim());
    }

    private void RefreshCategoryChoices()
    {
        var selectedId = SelectedCategory?.Id;
        AvailableCategories.Clear();
        foreach (var item in _allCategories.Where(item => item.Kind == SelectedKind.Value && !item.IsDisabled).OrderBy(item => item.SortOrder)) AvailableCategories.Add(item);
        SelectedCategory = AvailableCategories.FirstOrDefault(item => item.Id == selectedId) ?? AvailableCategories.FirstOrDefault();
    }

    private void BeginNew(FinanceTransactionKind kind)
    {
        ResetEditor();
        SelectedKind = KindOptions.First(item => item.Value == kind);
        AmountText = "0.00";
    }

    private void BeginEdit(FinanceTransactionItemViewModel? item)
    {
        if (item is not null) LoadEditor(item.Transaction, item.Transaction.Id);
    }

    private void BeginCopy(FinanceTransactionItemViewModel? item)
    {
        if (item is not null) LoadEditor(item.Transaction with { Id = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UtcNow }, null);
    }

    private void LoadEditor(FinanceTransaction item, Guid? editingId)
    {
        _editingId = editingId;
        SelectedKind = KindOptions.First(option => option.Value == item.Kind);
        SelectedCategory = AvailableCategories.FirstOrDefault(category => category.Id == item.CategoryId);
        AmountText = (item.AmountMinor / (decimal)Math.Pow(10, item.CurrencyScale)).ToString($"F{item.CurrencyScale}", CultureInfo.CurrentCulture);
        CurrencyCode = item.CurrencyCode;
        OccurredOn = item.OccurredOn.ToDateTime(TimeOnly.MinValue);
        SelectedPaymentStatus = PaymentOptions.First(option => option.Value == item.PaymentStatus);
        SelectedBookingLink = BookingLinkOptions.FirstOrDefault(option => option.Id == item.BookingId) ?? BookingLinkOptions.FirstOrDefault() ?? new(null, "不关联拍摄任务");
        SelectedProjectLink = ProjectLinkOptions.FirstOrDefault(option => option.Id == item.ProjectId) ?? ProjectLinkOptions.FirstOrDefault() ?? new(null, "不关联项目");
        Counterparty = item.Counterparty ?? string.Empty;
        PaymentMethod = item.PaymentMethod ?? string.Empty;
        Note = item.Note ?? string.Empty;
        Replace(Attachments, item.AttachmentPaths.Select(path => new FinanceAttachmentItem(path)));
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(IsEditing));
    }

    private void AddAttachments()
    {
        var paths = _dialogs.ChooseFiles("添加收支附件（仅关联原位置）", "所有文件|*.*", true);
        foreach (var path in paths.Where(File.Exists).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
            if (!Attachments.Any(item => string.Equals(item.FullPath, path, StringComparison.OrdinalIgnoreCase))) Attachments.Add(new(path));
        StatusText = paths.Any(Directory.Exists)
            ? "附件只支持文件，不会扫描文件夹；已忽略文件夹。"
            : "附件默认仅关联原位置，不复制、不移动、不删除。";
    }

    private void RemoveAttachment(FinanceAttachmentItem? item)
    {
        if (item is null) return;
        Attachments.Remove(item);
        StatusText = "已从当前记录移除附件引用，不会删除电脑中的原文件。";
    }

    private async Task SaveAsync()
    {
        if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0 || amount > long.MaxValue / 100m)
        {
            StatusText = "请输入大于零的有效金额。";
            return;
        }
        if (SelectedCategory is null)
        {
            StatusText = "请选择收入或支出分类。";
            return;
        }

        IsBusy = true;
        var saved = false;
        try
        {
            await _service.SaveAsync(new FinanceTransaction
            {
                Id = _editingId ?? Guid.NewGuid(),
                Kind = SelectedKind.Value,
                CategoryId = SelectedCategory.Id,
                AmountMinor = checked((long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero)),
                CurrencyCode = CurrencyCode,
                OccurredOn = DateOnly.FromDateTime(OccurredOn),
                PaymentStatus = SelectedPaymentStatus.Value,
                BookingId = SelectedBookingLink.Id,
                ProjectId = SelectedProjectLink.Id,
                Counterparty = Counterparty,
                PaymentMethod = PaymentMethod,
                Note = Note,
                AttachmentPaths = Attachments.Select(item => item.FullPath).ToArray(),
                AttachmentCount = Attachments.Count
            }).ConfigureAwait(true);
            ResetEditor();
            saved = true;
        }
        catch (FileNotFoundException)
        {
            StatusText = "有附件已移动或暂时不可访问，请移除或重新选择；原文件未被修改。";
        }
        catch
        {
            StatusText = "收支记录保存失败；数据库未确认成功前不会显示为已保存。";
        }
        finally
        {
            IsBusy = false;
        }
        if (saved) await RefreshAsync().ConfigureAwait(true);
    }

    private async Task DeleteAsync()
    {
        if (SelectedTransaction is null || !_dialogs.Confirm("只删除这条本地收支记录，不会删除关联排期、项目或附件源文件。", "删除收支记录")) return;
        await _service.DeleteAsync(SelectedTransaction.Id, true).ConfigureAwait(true);
        SelectedTransaction = null;
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task ExportAsync()
    {
        var path = _dialogs.ChooseSaveFile("导出本地收支 CSV", "CSV 文件 (*.csv)|*.csv", ".csv", $"像素蛋挞_收支_{SelectedMonth:yyyyMM}.csv");
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            await _service.ExportCsvAsync(path, BuildQuery()).ConfigureAwait(true);
            StatusText = "CSV 已按你的选择导出；现有文件不会被覆盖。";
        }
        catch (IOException)
        {
            StatusText = "导出位置已有同名文件。为防止覆盖，请选择新文件名。";
        }
        catch
        {
            StatusText = "CSV 导出失败；未修改现有文件。";
        }
    }

    private void ResetEditor()
    {
        _editingId = null;
        AmountText = string.Empty;
        CurrencyCode = "CNY";
        OccurredOn = DateTime.Today;
        Counterparty = string.Empty;
        PaymentMethod = string.Empty;
        Note = string.Empty;
        Attachments.Clear();
        SelectedBookingLink = BookingLinkOptions.FirstOrDefault() ?? new(null, "不关联拍摄任务");
        SelectedProjectLink = ProjectLinkOptions.FirstOrDefault() ?? new(null, "不关联项目");
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(IsEditing));
    }

    private void RefreshCommandStates()
    {
        (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CopyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RefreshCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private void NotifySummary()
    {
        OnPropertyChanged(nameof(MonthIncomeText));
        OnPropertyChanged(nameof(MonthExpenseText));
        OnPropertyChanged(nameof(NetCashFlowText));
        OnPropertyChanged(nameof(ReceivableText));
        OnPropertyChanged(nameof(PayableText));
        OnPropertyChanged(nameof(ExpectedProfitText));
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private static string Money(long value) => $"¥ {value / 100m:N2}";

    internal static string PaymentStatusText(FinancePaymentStatus value) => value switch
    {
        FinancePaymentStatus.Expected => "预计",
        FinancePaymentStatus.Receivable => "待收",
        FinancePaymentStatus.Received => "已收",
        FinancePaymentStatus.Payable => "待付",
        FinancePaymentStatus.Paid => "已付",
        FinancePaymentStatus.Cancelled => "已取消",
        _ => "未知"
    };
}
