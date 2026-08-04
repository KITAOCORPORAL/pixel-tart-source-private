using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;

namespace RAWSelectionAssistant.Core.Services.Database;

public sealed class SqliteBookingDocumentRepository(IPixelTartDatabase database) : IBookingDocumentRepository
{
    public async Task AddAsync(BookingDocumentRecord document, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO BookingDocuments(Id,BookingId,ProjectId,DocumentType,DisplayName,FilePath,NormalizedPath,FileExtension,FileSize,LastKnownModifiedAtUtc,OptionalHash,LinkMode,ImportTaskId,AddedAtUtc,UpdatedAtUtc,LastVerifiedAtUtc,IsMissing,MissingSinceAtUtc,Notes)
            VALUES($id,$booking,$project,$type,$name,$path,$normalized,$extension,$size,$modified,$hash,$mode,$task,$added,$updated,$verified,$missing,$missingSince,$notes);
            """;
        AddParameters(command, document);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<BookingDocumentRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = Select + " WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    public async Task<BookingDocumentRecord?> GetByNormalizedPathAsync(Guid bookingId, string normalizedPath, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = Select + " WHERE BookingId=$booking AND NormalizedPath=$path;";
        command.Parameters.AddWithValue("$booking", bookingId.ToString("D"));
        command.Parameters.AddWithValue("$path", normalizedPath);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    public async Task<BookingDocumentRecord?> GetByNormalizedPathAsync(string normalizedPath, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = Select + " WHERE NormalizedPath=$path LIMIT 1;";
        command.Parameters.AddWithValue("$path", normalizedPath);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    public async Task<IReadOnlyList<BookingDocumentRecord>> ListByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var result = new List<BookingDocumentRecord>();
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = Select + " WHERE BookingId=$booking ORDER BY DocumentType,AddedAtUtc,Id;";
        command.Parameters.AddWithValue("$booking", bookingId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(Read(reader));
        return result;
    }

    public async Task UpdateLocationAsync(Guid id, string filePath, string normalizedPath, string fileExtension, long? fileSize, DateTimeOffset? modifiedAtUtc, bool isMissing, DateTimeOffset verifiedAtUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE BookingDocuments SET FilePath=$path,NormalizedPath=$normalized,FileExtension=$extension,FileSize=$size,LastKnownModifiedAtUtc=$modified,IsMissing=$missing,MissingSinceAtUtc=CASE WHEN $missing=1 THEN COALESCE(MissingSinceAtUtc,$verified) ELSE NULL END,LastVerifiedAtUtc=$verified,UpdatedAtUtc=$verified WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.Parameters.AddWithValue("$path", filePath);
        command.Parameters.AddWithValue("$normalized", normalizedPath);
        command.Parameters.AddWithValue("$extension", fileExtension);
        command.Parameters.AddWithValue("$size", Db(fileSize));
        command.Parameters.AddWithValue("$modified", Db(modifiedAtUtc));
        command.Parameters.AddWithValue("$missing", isMissing ? 1 : 0);
        command.Parameters.AddWithValue("$verified", Utc(verifiedAtUtc));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateLocationAndHashAsync(Guid id, string filePath, string normalizedPath, string fileExtension, long? fileSize, DateTimeOffset? modifiedAtUtc, string? optionalHash, bool isMissing, DateTimeOffset verifiedAtUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE BookingDocuments SET FilePath=$path,NormalizedPath=$normalized,FileExtension=$extension,FileSize=$size,LastKnownModifiedAtUtc=$modified,OptionalHash=$hash,IsMissing=$missing,MissingSinceAtUtc=CASE WHEN $missing=1 THEN COALESCE(MissingSinceAtUtc,$verified) ELSE NULL END,LastVerifiedAtUtc=$verified,UpdatedAtUtc=$verified WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.Parameters.AddWithValue("$path", filePath);
        command.Parameters.AddWithValue("$normalized", normalizedPath);
        command.Parameters.AddWithValue("$extension", fileExtension);
        command.Parameters.AddWithValue("$size", Db(fileSize));
        command.Parameters.AddWithValue("$modified", Db(modifiedAtUtc));
        command.Parameters.AddWithValue("$hash", Db(optionalHash));
        command.Parameters.AddWithValue("$missing", isMissing ? 1 : 0);
        command.Parameters.AddWithValue("$verified", Utc(verifiedAtUtc));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetMissingAsync(Guid id, bool isMissing, DateTimeOffset verifiedAtUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE BookingDocuments SET IsMissing=$missing,MissingSinceAtUtc=CASE WHEN $missing=1 THEN COALESCE(MissingSinceAtUtc,$verified) ELSE NULL END,LastVerifiedAtUtc=$verified,UpdatedAtUtc=$verified WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.Parameters.AddWithValue("$missing", isMissing ? 1 : 0);
        command.Parameters.AddWithValue("$verified", Utc(verifiedAtUtc));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateHashAsync(Guid id, string? optionalHash, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE BookingDocuments SET OptionalHash=$hash,UpdatedAtUtc=$at WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.Parameters.AddWithValue("$hash", (object?)optionalHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$at", Utc(updatedAtUtc));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RemoveAssociationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM BookingDocuments WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    private static void AddParameters(SqliteCommand command, BookingDocumentRecord document)
    {
        command.Parameters.AddWithValue("$id", document.Id.ToString("D"));
        command.Parameters.AddWithValue("$booking", document.BookingId.ToString("D"));
        command.Parameters.AddWithValue("$project", Db(document.ProjectId));
        command.Parameters.AddWithValue("$type", document.DocumentType.ToString());
        command.Parameters.AddWithValue("$name", document.DisplayName);
        command.Parameters.AddWithValue("$path", document.FilePath);
        command.Parameters.AddWithValue("$normalized", document.NormalizedPath);
        command.Parameters.AddWithValue("$extension", document.FileExtension);
        command.Parameters.AddWithValue("$size", Db(document.FileSize));
        command.Parameters.AddWithValue("$modified", Db(document.LastKnownModifiedAtUtc));
        command.Parameters.AddWithValue("$hash", Db(document.OptionalHash));
        command.Parameters.AddWithValue("$mode", document.LinkMode.ToString());
        command.Parameters.AddWithValue("$task", Db(document.ImportTaskId));
        command.Parameters.AddWithValue("$added", Utc(document.AddedAtUtc));
        command.Parameters.AddWithValue("$updated", Utc(document.UpdatedAtUtc));
        command.Parameters.AddWithValue("$verified", Db(document.LastVerifiedAtUtc));
        command.Parameters.AddWithValue("$missing", document.IsMissing ? 1 : 0);
        command.Parameters.AddWithValue("$missingSince", Db(document.MissingSinceAtUtc));
        command.Parameters.AddWithValue("$notes", Db(document.Notes));
    }

    private static BookingDocumentRecord Read(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)), BookingId = Guid.Parse(reader.GetString(1)), ProjectId = GuidOrNull(reader, 2),
        DocumentType = EnumValue(reader.GetString(3), BookingDocumentType.Other), DisplayName = reader.GetString(4), FilePath = reader.GetString(5), NormalizedPath = reader.GetString(6),
        FileExtension = reader.GetString(7), FileSize = LongOrNull(reader, 8), LastKnownModifiedAtUtc = DateOrNull(reader, 9), OptionalHash = TextOrNull(reader, 10),
        LinkMode = EnumValue(reader.GetString(11), BookingDocumentLinkMode.Reference), ImportTaskId = GuidOrNull(reader, 12), AddedAtUtc = ParseUtc(reader.GetString(13)),
        UpdatedAtUtc = ParseUtc(reader.GetString(14)), LastVerifiedAtUtc = DateOrNull(reader, 15), IsMissing = reader.GetInt32(16) != 0, MissingSinceAtUtc = DateOrNull(reader, 17), Notes = TextOrNull(reader, 18)
    };

    private const string Select = "SELECT Id,BookingId,ProjectId,DocumentType,DisplayName,FilePath,NormalizedPath,FileExtension,FileSize,LastKnownModifiedAtUtc,OptionalHash,LinkMode,ImportTaskId,AddedAtUtc,UpdatedAtUtc,LastVerifiedAtUtc,IsMissing,MissingSinceAtUtc,Notes FROM BookingDocuments";
    private static string Utc(DateTimeOffset value) => value.ToUniversalTime().ToString("O");
    private static DateTimeOffset ParseUtc(string value) => DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime();
    private static object Db(object? value) => value switch { null => DBNull.Value, Guid guid => guid.ToString("D"), DateTimeOffset date => Utc(date), _ => value };
    private static string? TextOrNull(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static Guid? GuidOrNull(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Guid.Parse(reader.GetString(ordinal));
    private static long? LongOrNull(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    private static DateTimeOffset? DateOrNull(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : ParseUtc(reader.GetString(ordinal));
    private static T EnumValue<T>(string value, T fallback) where T : struct, Enum => Enum.TryParse<T>(value, out var parsed) ? parsed : fallback;
}
