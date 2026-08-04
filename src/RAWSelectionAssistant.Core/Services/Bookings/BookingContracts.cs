using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Bookings;

public interface IShootBookingRepository
{
    Task<ShootBooking?> GetAsync(Guid id, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShootRequirementItem>> GetRequirementsAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task SaveAsync(ShootBooking booking, IReadOnlyList<ShootRequirementItem> requirements, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShootBookingSummary>> QueryCurrentViewAsync(ShootBookingQuery query, CancellationToken cancellationToken = default);
    Task<ShootBookingPage> SearchAllUnarchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default);
    Task<ShootBookingPage> SearchArchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShootBookingSummary>> FindOverlappingAsync(DateTimeOffset startAtUtc, DateTimeOffset endAtUtc, Guid? excludeBookingId = null, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(Guid id, DateTimeOffset archivedAtUtc, CancellationToken cancellationToken = default);
    Task<bool> RestoreAsync(Guid id, DateTimeOffset restoredAtUtc, CancellationToken cancellationToken = default);
}

public interface IShootBookingService
{
    Task<BookingSaveResult> SaveAsync(ShootBookingDraft draft, BookingConflictResolution conflictResolution = BookingConflictResolution.None, CancellationToken cancellationToken = default);
    Task<ShootBooking?> GetAsync(Guid id, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShootRequirementItem>> GetRequirementsAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShootBookingSummary>> QueryCurrentViewAsync(ShootBookingQuery query, CancellationToken cancellationToken = default);
    Task<ShootBookingPage> SearchAllUnarchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default);
    Task<ShootBookingPage> SearchArchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> RestoreAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IBookingConflictDetector
{
    Task<IReadOnlyList<BookingConflict>> DetectAsync(Guid? bookingId, DateTimeOffset startAtUtc, DateTimeOffset endAtUtc, bool allowOverlap, CancellationToken cancellationToken = default);
}

public interface IBookingDocumentRepository
{
    Task AddAsync(BookingDocumentRecord document, CancellationToken cancellationToken = default);
    Task<BookingDocumentRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookingDocumentRecord>> ListByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task UpdateLocationAsync(Guid id, string filePath, string normalizedPath, string fileExtension, long? fileSize, DateTimeOffset? modifiedAtUtc, bool isMissing, DateTimeOffset verifiedAtUtc, CancellationToken cancellationToken = default);
    Task SetMissingAsync(Guid id, bool isMissing, DateTimeOffset verifiedAtUtc, CancellationToken cancellationToken = default);
    Task<bool> RemoveAssociationAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IBookingDocumentService
{
    Task<BookingDocumentRecord> AddReferenceAsync(Guid bookingId, Guid? projectId, BookingDocumentType documentType, string filePath, string? displayName = null, string? notes = null, CancellationToken cancellationToken = default);
    Task<BookingDocumentRecord?> VerifyAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<BookingDocumentRecord> RelocateAsync(Guid documentId, string newFilePath, CancellationToken cancellationToken = default);
    Task<bool> RemoveAssociationAsync(Guid documentId, CancellationToken cancellationToken = default);
}
