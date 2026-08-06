using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Bookings;

public sealed class BookingDocumentService(IBookingDocumentRepository repository) : IBookingDocumentService
{
    private static readonly HashSet<string> BlockedExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".jse", ".msi", ".msp", ".dll", ".scr", ".lnk"
    };

    public async Task<BookingDocumentRecord> AddReferenceAsync(Guid bookingId, Guid? projectId, BookingDocumentType documentType, string filePath, string? displayName = null, string? notes = null, CancellationToken cancellationToken = default)
    {
        var fullPath = ValidateExistingFile(filePath);
        var info = new FileInfo(fullPath);
        var now = DateTimeOffset.UtcNow;
        var document = new BookingDocumentRecord
        {
            BookingId = bookingId,
            ProjectId = projectId,
            DocumentType = documentType,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? info.Name : displayName.Trim(),
            FilePath = fullPath,
            NormalizedPath = Normalize(fullPath),
            FileExtension = info.Extension.ToLowerInvariant(),
            FileSize = info.Length,
            LastKnownModifiedAtUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
            LinkMode = BookingDocumentLinkMode.Reference,
            AddedAtUtc = now,
            UpdatedAtUtc = now,
            LastVerifiedAtUtc = now,
            IsMissing = false,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
        await repository.AddAsync(document, cancellationToken).ConfigureAwait(false);
        return document;
    }

    public async Task<BookingDocumentRecord?> VerifyAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await repository.GetAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (document is null) return null;
        var now = DateTimeOffset.UtcNow;
        if (!File.Exists(document.FilePath))
        {
            await repository.SetMissingAsync(document.Id, true, now, cancellationToken).ConfigureAwait(false);
            return document with { IsMissing = true, MissingSinceAtUtc = document.MissingSinceAtUtc ?? now, LastVerifiedAtUtc = now, UpdatedAtUtc = now };
        }
        var info = new FileInfo(document.FilePath);
        await repository.UpdateLocationAsync(document.Id, info.FullName, Normalize(info.FullName), info.Extension.ToLowerInvariant(), info.Length,
            new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), false, now, cancellationToken).ConfigureAwait(false);
        return document with { FilePath = info.FullName, NormalizedPath = Normalize(info.FullName), FileExtension = info.Extension.ToLowerInvariant(), FileSize = info.Length,
            LastKnownModifiedAtUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), IsMissing = false, MissingSinceAtUtc = null, LastVerifiedAtUtc = now, UpdatedAtUtc = now };
    }

    public async Task<BookingDocumentRecord> RelocateAsync(Guid documentId, string newFilePath, CancellationToken cancellationToken = default)
    {
        var document = await repository.GetAsync(documentId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("Document association was not found.");
        var fullPath = ValidateExistingFile(newFilePath);
        var info = new FileInfo(fullPath);
        var now = DateTimeOffset.UtcNow;
        await repository.UpdateLocationAsync(document.Id, fullPath, Normalize(fullPath), info.Extension.ToLowerInvariant(), info.Length,
            new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), false, now, cancellationToken).ConfigureAwait(false);
        return document with { FilePath = fullPath, NormalizedPath = Normalize(fullPath), FileExtension = info.Extension.ToLowerInvariant(), FileSize = info.Length,
            LastKnownModifiedAtUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), IsMissing = false, MissingSinceAtUtc = null, LastVerifiedAtUtc = now, UpdatedAtUtc = now };
    }

    public Task<bool> RemoveAssociationAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        repository.RemoveAssociationAsync(documentId, cancellationToken);

    private static string ValidateExistingFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("Document path is required.", nameof(filePath));
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Document file was not found.", fullPath);
        var extension = Path.GetExtension(fullPath);
        if (BlockedExecutableExtensions.Contains(extension)) throw new NotSupportedException("为避免误执行，程序或脚本文件不能作为拍摄资料关联。");
        return fullPath;
    }

    private static string Normalize(string filePath) => Path.GetFullPath(filePath).ToUpperInvariant();
}
