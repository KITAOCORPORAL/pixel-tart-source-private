using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Core.Services.Database;

public sealed class SqliteReminderRepository(IPixelTartDatabase database) : IReminderRepository
{
    public async Task SaveAsync(ReminderDefinition reminder, CancellationToken cancellationToken = default)
    {
        if (reminder.BookingId is null) throw new ArgumentException("A booking reminder requires BookingId.", nameof(reminder));
        var triggerAt = await ResolveTriggerAtAsync(reminder, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var created = reminder.CreatedAt ?? now;
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO BookingReminders(Id,BookingId,TriggerKind,OffsetMinutes,TriggerAtUtc,IsEnabled,Status,LastTriggeredAtUtc,CreatedAtUtc,UpdatedAtUtc)
            VALUES($id,$booking,$kind,$offset,$trigger,$enabled,$status,$last,$created,$updated)
            ON CONFLICT(Id) DO UPDATE SET BookingId=excluded.BookingId,TriggerKind=excluded.TriggerKind,OffsetMinutes=excluded.OffsetMinutes,
                TriggerAtUtc=excluded.TriggerAtUtc,IsEnabled=excluded.IsEnabled,Status=excluded.Status,LastTriggeredAtUtc=excluded.LastTriggeredAtUtc,UpdatedAtUtc=excluded.UpdatedAtUtc;
            """;
        command.Parameters.AddWithValue("$id", reminder.Id.ToString("D"));
        command.Parameters.AddWithValue("$booking", reminder.BookingId.Value.ToString("D"));
        command.Parameters.AddWithValue("$kind", reminder.Trigger.Kind.ToString());
        command.Parameters.AddWithValue("$offset", reminder.Trigger.Offset.HasValue ? Convert.ToInt64(Math.Round(Math.Abs(reminder.Trigger.Offset.Value.TotalMinutes))) : DBNull.Value);
        command.Parameters.AddWithValue("$trigger", Utc(triggerAt));
        command.Parameters.AddWithValue("$enabled", reminder.IsEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$status", reminder.IsEnabled ? reminder.Status.ToString() : ReminderStatus.Disabled.ToString());
        command.Parameters.AddWithValue("$last", Db(reminder.LastTriggeredAt));
        command.Parameters.AddWithValue("$created", Utc(created));
        command.Parameters.AddWithValue("$updated", Utc(reminder.UpdatedAt ?? now));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<ReminderDefinition?> GetAsync(Guid id, CancellationToken cancellationToken = default) => GetSingleAsync("r.Id=$value", id.ToString("D"), cancellationToken);

    public Task<IReadOnlyList<ReminderDefinition>> ListAsync(CancellationToken cancellationToken = default) =>
        ListWhereAsync("1=1", null, 1000, cancellationToken);

    public Task<IReadOnlyList<ReminderDefinition>> ListByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        ListWhereAsync("r.BookingId=$value", bookingId.ToString("D"), 1000, cancellationToken);

    public async Task<IReadOnlyList<ReminderDefinition>> ListDueAsync(DateTimeOffset fromUtc, DateTimeOffset untilUtc, int limit = 100, CancellationToken cancellationToken = default)
    {
        if (untilUtc <= fromUtc) throw new ArgumentOutOfRangeException(nameof(untilUtc));
        var result = new List<ReminderDefinition>();
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = Select + " WHERE r.IsEnabled=1 AND r.Status='Scheduled' AND r.LastTriggeredAtUtc IS NULL AND r.TriggerAtUtc>=$from AND r.TriggerAtUtc<$until AND b.IsArchived=0 AND b.Status<>'Cancelled' AND b.EndAtUtc>$until ORDER BY r.TriggerAtUtc,r.Id LIMIT $limit;";
        command.Parameters.AddWithValue("$from", Utc(fromUtc));
        command.Parameters.AddWithValue("$until", Utc(untilUtc));
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 1000));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(Read(reader));
        return result;
    }

    public async Task DisableForBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE BookingReminders SET IsEnabled=0,Status=CASE WHEN Status='Scheduled' THEN 'Cancelled' ELSE Status END,UpdatedAtUtc=$at WHERE BookingId=$booking;";
        command.Parameters.AddWithValue("$booking", bookingId.ToString("D"));
        command.Parameters.AddWithValue("$at", Utc(DateTimeOffset.UtcNow));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<DateTimeOffset> ResolveTriggerAtAsync(ReminderDefinition reminder, CancellationToken cancellationToken)
    {
        if (reminder.Trigger.Kind == ReminderTriggerKind.AbsoluteTime)
            return reminder.Trigger.At?.ToUniversalTime() ?? throw new ArgumentException("Absolute reminder requires trigger time.", nameof(reminder));
        if (reminder.Trigger.Offset is null || reminder.Trigger.Offset < TimeSpan.Zero)
            throw new ArgumentException("Relative reminder requires a non-negative lead time.", nameof(reminder));
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT StartAtUtc FROM ShootBookings WHERE Id=$booking;";
        command.Parameters.AddWithValue("$booking", reminder.BookingId!.Value.ToString("D"));
        var start = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        if (string.IsNullOrWhiteSpace(start)) throw new KeyNotFoundException("Booking was not found.");
        return ParseUtc(start) - reminder.Trigger.Offset.Value;
    }

    private async Task<ReminderDefinition?> GetSingleAsync(string predicate, string value, CancellationToken cancellationToken)
    {
        var rows = await ListWhereAsync(predicate, value, 1, cancellationToken).ConfigureAwait(false);
        return rows.FirstOrDefault();
    }

    private async Task<IReadOnlyList<ReminderDefinition>> ListWhereAsync(string predicate, string? value, int limit, CancellationToken cancellationToken)
    {
        var result = new List<ReminderDefinition>();
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = Select + $" WHERE {predicate} ORDER BY r.TriggerAtUtc,r.Id LIMIT $limit;";
        if (value is not null) command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(Read(reader));
        return result;
    }

    private static ReminderDefinition Read(SqliteDataReader reader)
    {
        var kind = EnumValue(reader.GetString(3), ReminderTriggerKind.AbsoluteTime);
        TimeSpan? offset = reader.IsDBNull(4) ? null : TimeSpan.FromMinutes(reader.GetInt64(4));
        var triggerAt = ParseUtc(reader.GetString(5));
        return new ReminderDefinition(
            Guid.Parse(reader.GetString(0)), reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)), reader.GetString(10), string.Empty,
            new(kind, kind == ReminderTriggerKind.AbsoluteTime ? triggerAt : null, offset), EnumValue(reader.GetString(7), ReminderStatus.Disabled),
            Guid.Parse(reader.GetString(1)), reader.GetInt32(6) != 0, reader.IsDBNull(8) ? null : ParseUtc(reader.GetString(8)), ParseUtc(reader.GetString(9)), ParseUtc(reader.GetString(11)));
    }

    private const string Select = "SELECT r.Id,r.BookingId,b.ProjectId,r.TriggerKind,r.OffsetMinutes,r.TriggerAtUtc,r.IsEnabled,r.Status,r.LastTriggeredAtUtc,r.CreatedAtUtc,b.Title,r.UpdatedAtUtc FROM BookingReminders r JOIN ShootBookings b ON b.Id=r.BookingId";
    private static string Utc(DateTimeOffset value) => value.ToUniversalTime().ToString("O");
    private static DateTimeOffset ParseUtc(string value) => DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime();
    private static object Db(object? value) => value switch { null => DBNull.Value, DateTimeOffset date => Utc(date), _ => value };
    private static T EnumValue<T>(string value, T fallback) where T : struct, Enum => Enum.TryParse<T>(value, out var parsed) ? parsed : fallback;
}
