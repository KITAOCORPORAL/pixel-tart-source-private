using System.Text;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Business;

public sealed class BookingPeopleService(IBookingPeopleRepository repository) : IBookingPeopleService
{
    public Task<IReadOnlyList<BookingContact>> ListContactsAsync(Guid bookingId, CancellationToken cancellationToken = default) => repository.ListContactsAsync(bookingId, cancellationToken);
    public Task<IReadOnlyList<BookingStaffMember>> ListStaffAsync(Guid bookingId, CancellationToken cancellationToken = default) => repository.ListStaffAsync(bookingId, cancellationToken);

    public Task SaveAsync(Guid bookingId, IReadOnlyList<BookingContact> contacts, IReadOnlyList<BookingStaffMember> staff, CancellationToken cancellationToken = default)
    {
        if (bookingId == Guid.Empty) throw new ArgumentException("排期标识无效。", nameof(bookingId));
        if (contacts.Any(item => string.IsNullOrWhiteSpace(item.DisplayName))) throw new ArgumentException("联系人姓名或代号不能为空。", nameof(contacts));
        if (staff.Any(item => string.IsNullOrWhiteSpace(item.DisplayName))) throw new ArgumentException("工作人员姓名或代号不能为空。", nameof(staff));
        if (contacts.Count(item => item.IsPrimary) > 1) throw new ArgumentException("同一排期只能设置一个主要联系人。", nameof(contacts));
        var now = DateTimeOffset.UtcNow;
        var normalizedContacts = contacts.Select(item => item with { BookingId=bookingId,DisplayName=item.DisplayName.Trim(),Phone=Clean(item.Phone),WeChat=Clean(item.WeChat),Email=Clean(item.Email),OtherContact=Clean(item.OtherContact),Note=Clean(item.Note),UpdatedAtUtc=now }).ToArray();
        var normalizedStaff = staff.Select((item,index) => item with { BookingId=bookingId,DisplayName=item.DisplayName.Trim(),Phone=Clean(item.Phone),WeChat=Clean(item.WeChat),Email=Clean(item.Email),Note=Clean(item.Note),SortOrder=index,UpdatedAtUtc=now }).ToArray();
        return repository.ReplaceAsync(bookingId, normalizedContacts, normalizedStaff, cancellationToken);
    }

    private static string? Clean(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
}

public sealed class FinanceService(IFinanceRepository repository) : IFinanceService
{
    public Task<IReadOnlyList<FinanceCategory>> ListCategoriesAsync(bool includeDisabled = false, CancellationToken cancellationToken = default) =>
        repository.ListCategoriesAsync(includeDisabled, cancellationToken);

    public Task<FinanceTransaction?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.GetAsync(id, cancellationToken);

    public Task<IReadOnlyList<FinanceTransaction>> QueryAsync(FinanceQuery query, CancellationToken cancellationToken = default) =>
        repository.QueryAsync(query, cancellationToken);

    public async Task<FinanceTransaction> SaveAsync(FinanceTransaction value, CancellationToken cancellationToken = default)
    {
        if (value.AmountMinor <= 0) throw new ArgumentOutOfRangeException(nameof(value), "金额必须大于零。");
        if (value.CurrencyScale is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(value), "币种精度无效。");
        if (string.IsNullOrWhiteSpace(value.CurrencyCode)) throw new ArgumentException("请选择币种。", nameof(value));
        if (value.AttachmentPaths.Any(Directory.Exists)) throw new ArgumentException("附件只能关联文件，不能关联文件夹。", nameof(value));

        var categories = await repository.ListCategoriesAsync(true, cancellationToken).ConfigureAwait(false);
        var category = categories.FirstOrDefault(item => item.Id == value.CategoryId && !item.IsDisabled)
            ?? throw new ArgumentException("收支分类不可用。", nameof(value));
        if (category.Kind != value.Kind) throw new ArgumentException("分类与收入支出类型不一致。", nameof(value));

        var attachments = value.AttachmentPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (attachments.Any(path => !File.Exists(path))) throw new FileNotFoundException("有附件已移动或暂时不可访问。");

        var now = DateTimeOffset.UtcNow;
        var normalized = value with
        {
            CurrencyCode = value.CurrencyCode.Trim().ToUpperInvariant(),
            Counterparty = Clean(value.Counterparty),
            PaymentMethod = Clean(value.PaymentMethod),
            Note = Clean(value.Note),
            AttachmentCount = attachments.Length,
            AttachmentPaths = attachments,
            UpdatedAtUtc = now
        };
        await repository.SaveAsync(normalized, cancellationToken).ConfigureAwait(false);
        return normalized;
    }

    public Task<bool> DeleteAsync(Guid id, bool userConfirmed, CancellationToken cancellationToken = default)
    {
        if (!userConfirmed) throw new InvalidOperationException("删除收支记录需要明确确认。");
        return repository.DeleteAsync(id, cancellationToken);
    }

    public async Task<FinanceSummary> SummarizeAsync(FinanceQuery query, CancellationToken cancellationToken = default)
    {
        var items = await repository.QueryAsync(query, cancellationToken).ConfigureAwait(false);
        var active = items.Where(item => item.PaymentStatus != FinancePaymentStatus.Cancelled).ToArray();
        var income = active.Where(item => item.Kind == FinanceTransactionKind.Income && item.PaymentStatus == FinancePaymentStatus.Received).Sum(item => item.AmountMinor);
        var expense = active.Where(item => item.Kind == FinanceTransactionKind.Expense && item.PaymentStatus == FinancePaymentStatus.Paid).Sum(item => item.AmountMinor);
        var receivable = active.Where(item => item.Kind == FinanceTransactionKind.Income && item.PaymentStatus is FinancePaymentStatus.Expected or FinancePaymentStatus.Receivable).Sum(item => item.AmountMinor);
        var payable = active.Where(item => item.Kind == FinanceTransactionKind.Expense && item.PaymentStatus is FinancePaymentStatus.Expected or FinancePaymentStatus.Payable).Sum(item => item.AmountMinor);
        var expectedIncome = active.Where(item => item.Kind == FinanceTransactionKind.Income).Sum(item => item.AmountMinor);
        var expectedExpense = active.Where(item => item.Kind == FinanceTransactionKind.Expense).Sum(item => item.AmountMinor);
        return new(income, expense, income - expense, receivable, payable, expectedIncome - expectedExpense);
    }

    public async Task ExportCsvAsync(string outputPath, FinanceQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("请选择导出位置。", nameof(outputPath));
        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("导出路径无效。", nameof(outputPath));
        Directory.CreateDirectory(directory);

        var items = await repository.QueryAsync(query, cancellationToken).ConfigureAwait(false);
        var categories = await repository.ListCategoriesAsync(true, cancellationToken).ConfigureAwait(false);
        await using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(true));
        await writer.WriteLineAsync("类型,分类,金额,币种,日期,支付状态,排期ID,项目ID,付款方或收款方,支付方式,备注,附件数").ConfigureAwait(false);
        foreach (var item in items)
        {
            var fields = new[]
            {
                item.Kind == FinanceTransactionKind.Income ? "收入" : "支出",
                categories.FirstOrDefault(category => category.Id == item.CategoryId)?.Name ?? "未分类",
                (item.AmountMinor / (decimal)Math.Pow(10, item.CurrencyScale)).ToString($"F{item.CurrencyScale}"),
                item.CurrencyCode,
                item.OccurredOn.ToString("yyyy-MM-dd"),
                PaymentStatusLabel(item.PaymentStatus),
                item.BookingId?.ToString("D") ?? string.Empty,
                item.ProjectId?.ToString("D") ?? string.Empty,
                item.Counterparty ?? string.Empty,
                item.PaymentMethod ?? string.Empty,
                item.Note ?? string.Empty,
                item.AttachmentPaths.Count.ToString()
            };
            await writer.WriteLineAsync(string.Join(',', fields.Select(Csv))).ConfigureAwait(false);
        }
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string PaymentStatusLabel(FinancePaymentStatus value) => value switch
    {
        FinancePaymentStatus.Expected => "预计",
        FinancePaymentStatus.Receivable => "待收",
        FinancePaymentStatus.Received => "已收",
        FinancePaymentStatus.Payable => "待付",
        FinancePaymentStatus.Paid => "已付",
        FinancePaymentStatus.Cancelled => "已取消",
        _ => "未知"
    };

    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
