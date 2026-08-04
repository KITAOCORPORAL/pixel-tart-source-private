using System.Text;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;

namespace RAWSelectionAssistant.Core.Services.Database;

public sealed class SqliteShootBookingRepository(IPixelTartDatabase database) : IShootBookingRepository
{
    public async Task<ShootBooking?> GetAsync(Guid id, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = BookingSelect + " WHERE Id=$id" + (includeArchived ? string.Empty : " AND IsArchived=0") + ";";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadBooking(reader) : null;
    }

    public async Task<IReadOnlyList<ShootRequirementItem>> GetRequirementsAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var result = new List<ShootRequirementItem>();
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,BookingId,ItemText,IsCompleted,Priority,SortOrder,CompletedAtUtc,CreatedAtUtc,UpdatedAtUtc FROM ShootRequirementItems WHERE BookingId=$booking ORDER BY SortOrder,Id;";
        command.Parameters.AddWithValue("$booking", bookingId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(ReadRequirement(reader));
        return result;
    }

    public async Task SaveAsync(ShootBooking booking, IReadOnlyList<ShootRequirementItem> requirements, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO ShootBookings(
                    Id,ProjectId,Title,ClientDisplayName,StartAtUtc,EndAtUtc,TimeZoneId,IsAllDay,Status,Location,ShootingType,
                    ShootingRequirements,PreparationNotes,TotalAmountMinor,DepositAmountMinor,PaidAmountMinor,CurrencyCode,CurrencyScale,
                    ContactName,ContactPhone,AllowOverlap,ConflictOverride,Notes,CreatedAtUtc,UpdatedAtUtc,IsArchived,ArchivedAtUtc)
                VALUES(
                    $id,$project,$title,$client,$start,$end,$zone,$allDay,$status,$location,$type,
                    $requirements,$preparation,$total,$deposit,$paid,$currency,$scale,
                    $contact,$phone,$overlap,$override,$notes,$created,$updated,$archived,$archivedAt)
                ON CONFLICT(Id) DO UPDATE SET
                    ProjectId=excluded.ProjectId,Title=excluded.Title,ClientDisplayName=excluded.ClientDisplayName,
                    StartAtUtc=excluded.StartAtUtc,EndAtUtc=excluded.EndAtUtc,TimeZoneId=excluded.TimeZoneId,
                    IsAllDay=excluded.IsAllDay,Status=excluded.Status,Location=excluded.Location,ShootingType=excluded.ShootingType,
                    ShootingRequirements=excluded.ShootingRequirements,PreparationNotes=excluded.PreparationNotes,
                    TotalAmountMinor=excluded.TotalAmountMinor,DepositAmountMinor=excluded.DepositAmountMinor,PaidAmountMinor=excluded.PaidAmountMinor,
                    CurrencyCode=excluded.CurrencyCode,CurrencyScale=excluded.CurrencyScale,ContactName=excluded.ContactName,ContactPhone=excluded.ContactPhone,
                    AllowOverlap=excluded.AllowOverlap,ConflictOverride=excluded.ConflictOverride,Notes=excluded.Notes,
                    UpdatedAtUtc=excluded.UpdatedAtUtc,IsArchived=excluded.IsArchived,ArchivedAtUtc=excluded.ArchivedAtUtc;
                """;
            AddBookingParameters(command, booking);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM ShootRequirementItems WHERE BookingId=$booking;";
            clear.Parameters.AddWithValue("$booking", booking.Id.ToString("D"));
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var item in requirements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO ShootRequirementItems(Id,BookingId,ItemText,IsCompleted,Priority,SortOrder,CompletedAtUtc,CreatedAtUtc,UpdatedAtUtc) VALUES($id,$booking,$text,$completed,$priority,$sort,$completedAt,$created,$updated);";
            command.Parameters.AddWithValue("$id", item.Id.ToString("D"));
            command.Parameters.AddWithValue("$booking", booking.Id.ToString("D"));
            command.Parameters.AddWithValue("$text", item.ItemText);
            command.Parameters.AddWithValue("$completed", item.IsCompleted ? 1 : 0);
            command.Parameters.AddWithValue("$priority", item.Priority.ToString());
            command.Parameters.AddWithValue("$sort", item.SortOrder);
            command.Parameters.AddWithValue("$completedAt", Db(item.CompletedAtUtc));
            command.Parameters.AddWithValue("$created", Utc(item.CreatedAtUtc));
            command.Parameters.AddWithValue("$updated", Utc(item.UpdatedAtUtc));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ShootBookingSummary>> QueryCurrentViewAsync(ShootBookingQuery query, CancellationToken cancellationToken = default)
    {
        if (query.RangeEndUtc <= query.RangeStartUtc) throw new ArgumentOutOfRangeException(nameof(query), "Query range end must be after start.");
        var result = new List<ShootBookingSummary>();
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        var sql = new StringBuilder(SummarySelect).Append(" WHERE StartAtUtc < $end AND EndAtUtc > $start");
        command.Parameters.AddWithValue("$start", Utc(query.RangeStartUtc));
        command.Parameters.AddWithValue("$end", Utc(query.RangeEndUtc));
        if (!query.IncludeArchived) sql.Append(" AND IsArchived=0");
        AddFilters(sql, command, query.Status, query.ShootingType, query.Keyword);
        sql.Append(" ORDER BY StartAtUtc,Id;");
        command.CommandText = sql.ToString();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(ReadSummary(reader));
        return result;
    }

    public async Task<ShootBookingPage> SearchAllUnarchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default)
        => await SearchAsync(request, isArchived: false, cancellationToken).ConfigureAwait(false);

    public async Task<ShootBookingPage> SearchArchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default)
        => await SearchAsync(request, isArchived: true, cancellationToken).ConfigureAwait(false);

    private async Task<ShootBookingPage> SearchAsync(ShootBookingSearchRequest request, bool isArchived, CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var result = new List<ShootBookingSummary>(pageSize + 1);
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        var sql = new StringBuilder(SummarySelect).Append(isArchived ? " WHERE IsArchived=1" : " WHERE IsArchived=0");
        AddFilters(sql, command, request.Status, request.ShootingType, request.Keyword);
        if (request.Cursor is not null)
        {
            sql.Append(" AND (StartAtUtc < $cursorStart OR (StartAtUtc=$cursorStart AND Id < $cursorId))");
            command.Parameters.AddWithValue("$cursorStart", Utc(request.Cursor.StartAtUtc));
            command.Parameters.AddWithValue("$cursorId", request.Cursor.Id.ToString("D"));
        }
        sql.Append(" ORDER BY StartAtUtc DESC,Id DESC LIMIT $limit;");
        command.Parameters.AddWithValue("$limit", pageSize + 1);
        command.CommandText = sql.ToString();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(ReadSummary(reader));
        var hasMore = result.Count > pageSize;
        if (hasMore) result.RemoveAt(result.Count - 1);
        var last = result.LastOrDefault();
        return new(result, hasMore && last is not null ? new(last.StartAtUtc, last.Id) : null);
    }

    public async Task<IReadOnlyList<ShootBookingSummary>> FindOverlappingAsync(DateTimeOffset startAtUtc, DateTimeOffset endAtUtc, Guid? excludeBookingId = null, CancellationToken cancellationToken = default)
    {
        var result = new List<ShootBookingSummary>();
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SummarySelect + " WHERE IsArchived=0 AND Status<>'Cancelled' AND StartAtUtc<$end AND EndAtUtc>$start" + (excludeBookingId.HasValue ? " AND Id<>$exclude" : string.Empty) + " ORDER BY StartAtUtc,Id;";
        command.Parameters.AddWithValue("$start", Utc(startAtUtc));
        command.Parameters.AddWithValue("$end", Utc(endAtUtc));
        if (excludeBookingId.HasValue) command.Parameters.AddWithValue("$exclude", excludeBookingId.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(ReadSummary(reader));
        return result;
    }

    public async Task<bool> ArchiveAsync(Guid id, DateTimeOffset archivedAtUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var booking = connection.CreateCommand();
        booking.Transaction = transaction;
        booking.CommandText = "UPDATE ShootBookings SET IsArchived=1,ArchivedAtUtc=$at,UpdatedAtUtc=$at WHERE Id=$id AND IsArchived=0;";
        booking.Parameters.AddWithValue("$id", id.ToString("D"));
        booking.Parameters.AddWithValue("$at", Utc(archivedAtUtc));
        var changed = await booking.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (changed > 0)
        {
            await using var reminders = connection.CreateCommand();
            reminders.Transaction = transaction;
            reminders.CommandText = "UPDATE BookingReminders SET IsEnabled=0,Status=CASE WHEN Status='Scheduled' THEN 'Cancelled' ELSE Status END,UpdatedAtUtc=$at WHERE BookingId=$id;";
            reminders.Parameters.AddWithValue("$id", id.ToString("D"));
            reminders.Parameters.AddWithValue("$at", Utc(archivedAtUtc));
            await reminders.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return changed > 0;
    }

    public async Task<bool> RestoreAsync(Guid id, DateTimeOffset restoredAtUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ShootBookings SET IsArchived=0,ArchivedAtUtc=NULL,UpdatedAtUtc=$at WHERE Id=$id AND IsArchived=1;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.Parameters.AddWithValue("$at", Utc(restoredAtUtc));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    private static void AddFilters(StringBuilder sql, SqliteCommand command, ShootBookingStatus? status, string? shootingType, string? keyword)
    {
        if (status.HasValue)
        {
            sql.Append(" AND Status=$status");
            command.Parameters.AddWithValue("$status", status.Value.ToString());
        }
        if (!string.IsNullOrWhiteSpace(shootingType))
        {
            sql.Append(" AND ShootingType=$type COLLATE NOCASE");
            command.Parameters.AddWithValue("$type", shootingType.Trim());
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            sql.Append(" AND (Title LIKE $keyword ESCAPE '\\' OR ClientDisplayName LIKE $keyword ESCAPE '\\' OR IFNULL(Location,'') LIKE $keyword ESCAPE '\\' OR ShootingType LIKE $keyword ESCAPE '\\' OR IFNULL(ShootingRequirements,'') LIKE $keyword ESCAPE '\\' OR IFNULL(Notes,'') LIKE $keyword ESCAPE '\\')");
            command.Parameters.AddWithValue("$keyword", "%" + EscapeLike(keyword.Trim()) + "%");
        }
    }

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private static void AddBookingParameters(SqliteCommand command, ShootBooking booking)
    {
        command.Parameters.AddWithValue("$id", booking.Id.ToString("D"));
        command.Parameters.AddWithValue("$project", Db(booking.ProjectId));
        command.Parameters.AddWithValue("$title", booking.Title);
        command.Parameters.AddWithValue("$client", booking.ClientDisplayName);
        command.Parameters.AddWithValue("$start", Utc(booking.StartAtUtc));
        command.Parameters.AddWithValue("$end", Utc(booking.EndAtUtc));
        command.Parameters.AddWithValue("$zone", booking.TimeZoneId);
        command.Parameters.AddWithValue("$allDay", booking.IsAllDay ? 1 : 0);
        command.Parameters.AddWithValue("$status", booking.Status.ToString());
        command.Parameters.AddWithValue("$location", Db(booking.Location));
        command.Parameters.AddWithValue("$type", booking.ShootingType);
        command.Parameters.AddWithValue("$requirements", Db(booking.ShootingRequirements));
        command.Parameters.AddWithValue("$preparation", Db(booking.PreparationNotes));
        command.Parameters.AddWithValue("$total", Db(booking.TotalAmountMinor));
        command.Parameters.AddWithValue("$deposit", Db(booking.DepositAmountMinor));
        command.Parameters.AddWithValue("$paid", Db(booking.PaidAmountMinor));
        command.Parameters.AddWithValue("$currency", booking.CurrencyCode);
        command.Parameters.AddWithValue("$scale", booking.CurrencyScale);
        command.Parameters.AddWithValue("$contact", Db(booking.ContactName));
        command.Parameters.AddWithValue("$phone", Db(booking.ContactPhone));
        command.Parameters.AddWithValue("$overlap", booking.AllowOverlap ? 1 : 0);
        command.Parameters.AddWithValue("$override", booking.ConflictOverride ? 1 : 0);
        command.Parameters.AddWithValue("$notes", Db(booking.Notes));
        command.Parameters.AddWithValue("$created", Utc(booking.CreatedAtUtc));
        command.Parameters.AddWithValue("$updated", Utc(booking.UpdatedAtUtc));
        command.Parameters.AddWithValue("$archived", booking.IsArchived ? 1 : 0);
        command.Parameters.AddWithValue("$archivedAt", Db(booking.ArchivedAtUtc));
    }

    private static ShootBooking ReadBooking(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)), ProjectId = GuidOrNull(reader, 1), Title = reader.GetString(2), ClientDisplayName = reader.GetString(3),
        StartAtUtc = ParseUtc(reader.GetString(4)), EndAtUtc = ParseUtc(reader.GetString(5)), TimeZoneId = reader.GetString(6), IsAllDay = reader.GetInt32(7) != 0,
        Status = EnumValue(reader.GetString(8), ShootBookingStatus.Tentative), Location = TextOrNull(reader, 9), ShootingType = reader.GetString(10),
        ShootingRequirements = TextOrNull(reader, 11), PreparationNotes = TextOrNull(reader, 12), TotalAmountMinor = LongOrNull(reader, 13), DepositAmountMinor = LongOrNull(reader, 14),
        PaidAmountMinor = LongOrNull(reader, 15), CurrencyCode = reader.GetString(16), CurrencyScale = reader.GetInt32(17), ContactName = TextOrNull(reader, 18), ContactPhone = TextOrNull(reader, 19),
        AllowOverlap = reader.GetInt32(20) != 0, ConflictOverride = reader.GetInt32(21) != 0, Notes = TextOrNull(reader, 22), CreatedAtUtc = ParseUtc(reader.GetString(23)),
        UpdatedAtUtc = ParseUtc(reader.GetString(24)), IsArchived = reader.GetInt32(25) != 0, ArchivedAtUtc = DateOrNull(reader, 26)
    };

    private static ShootBookingSummary ReadSummary(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)), GuidOrNull(reader, 1), reader.GetString(2), reader.GetString(3), ParseUtc(reader.GetString(4)), ParseUtc(reader.GetString(5)),
        reader.GetString(6), reader.GetInt32(7) != 0, EnumValue(reader.GetString(8), ShootBookingStatus.Tentative), TextOrNull(reader, 9), reader.GetString(10), reader.GetInt32(11) != 0, reader.GetInt32(12) != 0);

    private static ShootRequirementItem ReadRequirement(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)), BookingId = Guid.Parse(reader.GetString(1)), ItemText = reader.GetString(2), IsCompleted = reader.GetInt32(3) != 0,
        Priority = EnumValue(reader.GetString(4), ShootRequirementPriority.Normal), SortOrder = reader.GetInt32(5), CompletedAtUtc = DateOrNull(reader, 6),
        CreatedAtUtc = ParseUtc(reader.GetString(7)), UpdatedAtUtc = ParseUtc(reader.GetString(8))
    };

    private const string BookingSelect = "SELECT Id,ProjectId,Title,ClientDisplayName,StartAtUtc,EndAtUtc,TimeZoneId,IsAllDay,Status,Location,ShootingType,ShootingRequirements,PreparationNotes,TotalAmountMinor,DepositAmountMinor,PaidAmountMinor,CurrencyCode,CurrencyScale,ContactName,ContactPhone,AllowOverlap,ConflictOverride,Notes,CreatedAtUtc,UpdatedAtUtc,IsArchived,ArchivedAtUtc FROM ShootBookings";
    private const string SummarySelect = "SELECT Id,ProjectId,Title,ClientDisplayName,StartAtUtc,EndAtUtc,TimeZoneId,IsAllDay,Status,Location,ShootingType,AllowOverlap,IsArchived FROM ShootBookings";
    private static string Utc(DateTimeOffset value) => value.ToUniversalTime().ToString("O");
    private static DateTimeOffset ParseUtc(string value) => DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime();
    private static object Db(object? value) => value switch { null => DBNull.Value, Guid guid => guid.ToString("D"), DateTimeOffset date => Utc(date), _ => value };
    private static string? TextOrNull(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static Guid? GuidOrNull(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Guid.Parse(reader.GetString(ordinal));
    private static long? LongOrNull(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    private static DateTimeOffset? DateOrNull(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : ParseUtc(reader.GetString(ordinal));
    private static T EnumValue<T>(string value, T fallback) where T : struct, Enum => Enum.TryParse<T>(value, out var parsed) ? parsed : fallback;
}
