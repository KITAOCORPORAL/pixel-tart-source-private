namespace RAWSelectionAssistant.Core.Models;

public enum BookingStaffRole
{
    Photographer,
    PhotographyAssistant,
    LightingTechnician,
    MakeupArtist,
    Stylist,
    ModelOrActor,
    ClientRepresentative,
    FloorAssistant,
    Other
}

public sealed record BookingContact
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid BookingId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? WeChat { get; init; }
    public string? Email { get; init; }
    public string? OtherContact { get; init; }
    public bool IsPrimary { get; init; }
    public string? Note { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record BookingStaffMember
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid BookingId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public BookingStaffRole Role { get; init; } = BookingStaffRole.Other;
    public DateTimeOffset? ArrivalTime { get; init; }
    public string? Phone { get; init; }
    public string? WeChat { get; init; }
    public string? Email { get; init; }
    public string? Note { get; init; }
    public int SortOrder { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public enum FinanceTransactionKind { Income, Expense }
public enum FinancePaymentStatus { Expected, Receivable, Received, Payable, Paid, Cancelled }

public sealed record FinanceCategory
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public FinanceTransactionKind Kind { get; init; }
    public string Name { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool IsSystemDefault { get; init; }
    public bool IsDisabled { get; init; }
}

public sealed record FinanceTransaction
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public FinanceTransactionKind Kind { get; init; }
    public Guid CategoryId { get; init; }
    public long AmountMinor { get; init; }
    public string CurrencyCode { get; init; } = "CNY";
    public int CurrencyScale { get; init; } = 2;
    public DateOnly OccurredOn { get; init; } = DateOnly.FromDateTime(DateTime.Today);
    public FinancePaymentStatus PaymentStatus { get; init; } = FinancePaymentStatus.Expected;
    public Guid? BookingId { get; init; }
    public Guid? ProjectId { get; init; }
    public string? Counterparty { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Note { get; init; }
    public int AttachmentCount { get; init; }
    public IReadOnlyList<string> AttachmentPaths { get; init; } = [];
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record FinanceQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    FinanceTransactionKind? Kind = null,
    FinancePaymentStatus? PaymentStatus = null,
    Guid? BookingId = null,
    Guid? ProjectId = null,
    Guid? CategoryId = null,
    string? Keyword = null,
    string? CurrencyCode = null);

public sealed record FinanceSummary(
    long IncomeMinor,
    long ExpenseMinor,
    long NetCashFlowMinor,
    long ReceivableMinor,
    long PayableMinor,
    long ExpectedProfitMinor,
    string CurrencyCode = "CNY",
    int CurrencyScale = 2);
