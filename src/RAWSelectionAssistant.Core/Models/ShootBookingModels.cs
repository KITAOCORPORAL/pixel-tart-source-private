namespace RAWSelectionAssistant.Core.Models;

public enum ShootBookingStatus
{
    Tentative,
    Confirmed,
    Preparing,
    Shooting,
    Completed,
    Cancelled,
    Postponed
}

public enum ShootRequirementPriority { Low, Normal, High, Critical }
public enum BookingDocumentType { PhotographyPlan, ShootAgreement, ModelRelease, Quotation, VenueMaterial, WardrobeReference, LightingDiagram, Other }
public enum BookingDocumentLinkMode { Reference, ManagedCopy }
public enum BookingSearchScope { CurrentView, AllUnarchived }
public enum BookingConflictResolution { None, SaveAnyway, MarkAllowOverlap }
public enum BookingSaveStatus { Saved, NeedsAttention, ValidationFailed, NotFound }
public enum BookingMoneyDisplayKind { Unknown, Receivable, Settled, Overpaid }

public sealed record ShootBooking
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? ProjectId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ClientDisplayName { get; init; } = string.Empty;
    public DateTimeOffset StartAtUtc { get; init; }
    public DateTimeOffset EndAtUtc { get; init; }
    public string TimeZoneId { get; init; } = TimeZoneInfo.Local.Id;
    public bool IsAllDay { get; init; }
    public ShootBookingStatus Status { get; init; } = ShootBookingStatus.Tentative;
    public string? Location { get; init; }
    public string ShootingType { get; init; } = "Other";
    public string? ShootingRequirements { get; init; }
    public string? PreparationNotes { get; init; }
    public long? TotalAmountMinor { get; init; }
    public long? DepositAmountMinor { get; init; }
    public long? PaidAmountMinor { get; init; }
    public string CurrencyCode { get; init; } = "CNY";
    public int CurrencyScale { get; init; } = 2;
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public bool AllowOverlap { get; init; }
    public bool ConflictOverride { get; init; }
    public string? Notes { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public bool IsArchived { get; init; }
    public DateTimeOffset? ArchivedAtUtc { get; init; }
}

public sealed record ShootBookingDraft
{
    public Guid? Id { get; init; }
    public Guid? ProjectId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ClientDisplayName { get; init; } = string.Empty;
    public DateTimeOffset StartAt { get; init; }
    public DateTimeOffset EndAt { get; init; }
    public string TimeZoneId { get; init; } = TimeZoneInfo.Local.Id;
    public bool IsAllDay { get; init; }
    public ShootBookingStatus Status { get; init; } = ShootBookingStatus.Tentative;
    public string? Location { get; init; }
    public string ShootingType { get; init; } = "Other";
    public string? ShootingRequirements { get; init; }
    public string? PreparationNotes { get; init; }
    public long? TotalAmountMinor { get; init; }
    public long? DepositAmountMinor { get; init; }
    public long? PaidAmountMinor { get; init; }
    public string CurrencyCode { get; init; } = "CNY";
    public int CurrencyScale { get; init; } = 2;
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public bool AllowOverlap { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<ShootRequirementItem> Requirements { get; init; } = [];
}

public sealed record ShootRequirementItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid BookingId { get; init; }
    public string ItemText { get; init; } = string.Empty;
    public bool IsCompleted { get; init; }
    public ShootRequirementPriority Priority { get; init; } = ShootRequirementPriority.Normal;
    public int SortOrder { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ShootBookingSummary(
    Guid Id,
    Guid? ProjectId,
    string Title,
    string ClientDisplayName,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    string TimeZoneId,
    bool IsAllDay,
    ShootBookingStatus Status,
    string? Location,
    string ShootingType,
    bool AllowOverlap,
    bool IsArchived);

public sealed record BookingDocumentRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid BookingId { get; init; }
    public Guid? ProjectId { get; init; }
    public BookingDocumentType DocumentType { get; init; } = BookingDocumentType.Other;
    public string DisplayName { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public string NormalizedPath { get; init; } = string.Empty;
    public string FileExtension { get; init; } = string.Empty;
    public long? FileSize { get; init; }
    public DateTimeOffset? LastKnownModifiedAtUtc { get; init; }
    public string? OptionalHash { get; init; }
    public BookingDocumentLinkMode LinkMode { get; init; } = BookingDocumentLinkMode.Reference;
    public Guid? ImportTaskId { get; init; }
    public DateTimeOffset AddedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastVerifiedAtUtc { get; init; }
    public bool IsMissing { get; init; }
    public DateTimeOffset? MissingSinceAtUtc { get; init; }
    public string? Notes { get; init; }
}

public sealed record ShootBookingQuery(
    DateTimeOffset RangeStartUtc,
    DateTimeOffset RangeEndUtc,
    ShootBookingStatus? Status = null,
    string? ShootingType = null,
    string? Keyword = null,
    bool IncludeArchived = false);

public sealed record ShootBookingPageCursor(DateTimeOffset StartAtUtc, Guid Id);

public sealed record ShootBookingSearchRequest(
    string? Keyword = null,
    ShootBookingStatus? Status = null,
    string? ShootingType = null,
    ShootBookingPageCursor? Cursor = null,
    int PageSize = 50);

public sealed record ShootBookingPage(IReadOnlyList<ShootBookingSummary> Items, ShootBookingPageCursor? NextCursor);

public sealed record BookingMoneyWarning(string Code, string Message);

public sealed record BookingMoneySummary(
    long? TotalAmountMinor,
    long? DepositAmountMinor,
    long? PaidAmountMinor,
    long? SignedBalanceMinor,
    long? DisplayAmountMinor,
    BookingMoneyDisplayKind DisplayKind,
    IReadOnlyList<BookingMoneyWarning> Warnings);

public sealed record BookingConflict(
    Guid BookingId,
    string Title,
    string ClientDisplayName,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    string? Location,
    ShootBookingStatus Status,
    TimeSpan Overlap,
    bool ExistingAllowsOverlap,
    bool IsBlocking);

public sealed record BookingSaveResult(
    BookingSaveStatus Status,
    ShootBooking? Booking,
    BookingMoneySummary Money,
    IReadOnlyList<BookingConflict> Conflicts,
    IReadOnlyList<string> ValidationErrors)
{
    public static BookingSaveResult ValidationFailed(BookingMoneySummary money, IReadOnlyList<string> errors) =>
        new(BookingSaveStatus.ValidationFailed, null, money, [], errors);
}
