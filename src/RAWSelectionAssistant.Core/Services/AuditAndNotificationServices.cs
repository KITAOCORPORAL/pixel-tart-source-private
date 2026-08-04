using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;

namespace RAWSelectionAssistant.Core.Services;

public interface IAuditLogService
{
    Task WriteAsync(string category, string eventType, string severity, string message, Guid? taskId = null, Guid? projectId = null, string? errorCode = null, string? correlationId = null, CancellationToken cancellationToken = default);
}

public sealed class AuditLogService(IPixelTartDatabase database) : IAuditLogService
{
    private static readonly Regex WindowsPath = new(@"(?<!\w)(?:[A-Za-z]:\\|\\\\)[^\r\n\t\""<>|]+", RegexOptions.Compiled);

    public async Task WriteAsync(string category, string eventType, string severity, string message, Guid? taskId = null, Guid? projectId = null, string? errorCode = null, string? correlationId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AuditLogs(Id,Timestamp,Category,EventType,Severity,TaskId,ProjectId,ErrorCode,SanitizedMessage,CorrelationId)
            VALUES($id,$at,$category,$event,$severity,$task,$project,$code,$message,$correlation);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$category", category);
        command.Parameters.AddWithValue("$event", eventType);
        command.Parameters.AddWithValue("$severity", severity);
        command.Parameters.AddWithValue("$task", (object?)taskId?.ToString("D") ?? DBNull.Value);
        command.Parameters.AddWithValue("$project", (object?)projectId?.ToString("D") ?? DBNull.Value);
        command.Parameters.AddWithValue("$code", (object?)errorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$message", Sanitize(message));
        command.Parameters.AddWithValue("$correlation", correlationId ?? Guid.NewGuid().ToString("N"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public static string Sanitize(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return string.Empty;
        var sanitized = WindowsPath.Replace(message, "[路径已隐藏]");
        sanitized = Regex.Replace(sanitized, @"(?i)(displayname|filename|documentname|optionalhash|documenthash)\s*[:=]\s*(?:""[^""]*""|\S+)", "$1=[已隐藏]");
        sanitized = Regex.Replace(sanitized, @"(?<![A-Fa-f0-9])[A-Fa-f0-9]{64}(?![A-Fa-f0-9])", "[哈希已隐藏]");
        sanitized = Regex.Replace(sanitized, @"(?i)(license|token|secret|key)\s*[:=]\s*\S+", "$1=[已隐藏]");
        return sanitized.Length <= 2000 ? sanitized : sanitized[..2000];
    }
}

public interface INotificationCenter
{
    event EventHandler<NotificationMessage>? Published;
    Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationMessage>> GetHistoryAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class NotificationCenter(IPixelTartDatabase database, TimeSpan? throttleWindow = null) : INotificationCenter
{
    private readonly TimeSpan _throttleWindow = throttleWindow ?? TimeSpan.FromSeconds(2);
    private readonly Dictionary<string, DateTimeOffset> _lastPublished = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    public event EventHandler<NotificationMessage>? Published;

    public async Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var key = message.DeduplicationKey;
        if (!string.IsNullOrWhiteSpace(key))
        {
            lock (_sync)
            {
                var now = DateTimeOffset.UtcNow;
                if (_lastPublished.TryGetValue(key, out var last) && now - last < _throttleWindow) return;
                _lastPublished[key] = now;
            }
        }
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO Notifications(Id,Type,Severity,Title,Message,TaskId,ProjectId,IsRead,CreatedAt,ExpiresAt,DeduplicationKey)
            VALUES($id,$type,$severity,$title,$message,$task,$project,$read,$created,$expires,$dedupe);
            """;
        command.Parameters.AddWithValue("$id", message.Id.ToString("D"));
        command.Parameters.AddWithValue("$type", message.Type.ToString());
        command.Parameters.AddWithValue("$severity", message.Severity.ToString());
        command.Parameters.AddWithValue("$title", message.Title);
        command.Parameters.AddWithValue("$message", message.Message);
        command.Parameters.AddWithValue("$task", (object?)message.TaskId?.ToString("D") ?? DBNull.Value);
        command.Parameters.AddWithValue("$project", (object?)message.ProjectId?.ToString("D") ?? DBNull.Value);
        command.Parameters.AddWithValue("$read", message.IsRead ? 1 : 0);
        command.Parameters.AddWithValue("$created", message.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$expires", (object?)message.ExpiresAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$dedupe", (object?)message.DeduplicationKey ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        Published?.Invoke(this, message);
    }

    public async Task<IReadOnlyList<NotificationMessage>> GetHistoryAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,Type,Severity,Title,Message,TaskId,ProjectId,IsRead,CreatedAt,ExpiresAt,DeduplicationKey FROM Notifications ORDER BY CreatedAt DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 1000));
        var result = new List<NotificationMessage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new NotificationMessage(
                Guid.Parse(reader.GetString(0)), Enum.Parse<NotificationType>(reader.GetString(1)), Enum.Parse<NotificationSeverity>(reader.GetString(2)),
                reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : Guid.Parse(reader.GetString(5)), reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6)),
                [], reader.GetInt32(7) != 0, DateTimeOffset.Parse(reader.GetString(8)), reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9)), reader.IsDBNull(10) ? null : reader.GetString(10)));
        }
        return result;
    }

    public async Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Notifications SET IsRead=1 WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

