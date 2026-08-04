using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Core.Models;

public enum BookingDocumentFileState
{
    Normal,
    Missing,
    Modified,
    WaitingForConfirmation,
    Copying,
    PartiallyCompleted,
    Failed
}

public enum BookingDocumentBatchStatus
{
    Completed,
    PartiallyCompleted,
    NeedsAttention,
    Failed,
    Cancelled
}

public enum BookingDocumentRelocationStatus
{
    Relocated,
    HashMismatch,
    NotFound,
    Failed
}

public sealed record BookingDocumentAddRequest(
    Guid BookingId,
    Guid? ProjectId,
    BookingDocumentType DocumentType,
    IReadOnlyList<string> FilePaths);

public sealed record BookingDocumentCopyRequest(
    Guid BookingId,
    Guid? ProjectId,
    BookingDocumentType DocumentType,
    IReadOnlyList<string> FilePaths,
    string DestinationRoot,
    bool VerifySha256 = true);

public sealed record PendingDocumentAssociation(
    Guid TaskId,
    Guid BookingId,
    Guid? ProjectId,
    BookingDocumentType DocumentType,
    string DestinationPath,
    string? OutputHash,
    long? OutputSize);

public sealed record BookingDocumentItemOutcome(
    string SourcePath,
    string? DestinationPath,
    BookingDocumentFileState State,
    BookingDocumentRecord? Document,
    PendingDocumentAssociation? PendingAssociation,
    string? ErrorCode,
    string Message);

public sealed record BookingDocumentBatchResult(
    Guid? TaskId,
    BookingDocumentBatchStatus Status,
    TaskResultSummary Summary,
    IReadOnlyList<BookingDocumentItemOutcome> Items)
{
    public int Successful => Items.Count(item => item.Document is not null && item.ErrorCode != ErrorCodeCatalog.DuplicateConflict);
    public int Failed => Items.Count(item => item.State == BookingDocumentFileState.Failed);
    public int Skipped => Items.Count(item => item.ErrorCode == ErrorCodeCatalog.DuplicateConflict);
    public int WaitingForAttention => Items.Count(item => item.PendingAssociation is not null || item.State == BookingDocumentFileState.WaitingForConfirmation);
}

public sealed record BookingDocumentCheckResult(
    BookingDocumentRecord Document,
    BookingDocumentFileState State,
    string Message);

public sealed record BookingDocumentRelocationResult(
    BookingDocumentRelocationStatus Status,
    BookingDocumentRecord? Document,
    bool RequiresConfirmation,
    string Message);

public sealed record BookingDocumentRetryResult(
    bool Succeeded,
    BookingDocumentRecord? Document,
    PendingDocumentAssociation? PendingAssociation,
    string? ErrorCode,
    string Message);
