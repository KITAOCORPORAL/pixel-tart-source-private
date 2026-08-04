using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;

namespace RAWSelectionAssistant.Core.Services.FileOperations;

public sealed class SqliteUndoJournalRepository(IPixelTartDatabase database) : IUndoJournalRepository
{
    public async Task AppendAsync(UndoJournalEntry entry, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO UndoJournals(Id,TaskId,Sequence,ReverseOperation,SourcePath,DestinationPath,ExpectedCurrentSize,ExpectedCurrentHash,Preconditions,State,CreatedAt,AppliedAt)
            VALUES($id,$task,$sequence,$operation,$source,$destination,$size,$hash,$preconditions,$state,$created,$applied);
            """;
        command.Parameters.AddWithValue("$id", entry.Id.ToString("D"));
        command.Parameters.AddWithValue("$task", entry.TaskId.ToString("D"));
        command.Parameters.AddWithValue("$sequence", entry.Sequence);
        command.Parameters.AddWithValue("$operation", entry.ReverseOperation.ToString());
        command.Parameters.AddWithValue("$source", entry.SourcePath);
        command.Parameters.AddWithValue("$destination", entry.DestinationPath);
        command.Parameters.AddWithValue("$size", (object?)entry.ExpectedCurrentSize ?? DBNull.Value);
        command.Parameters.AddWithValue("$hash", (object?)entry.ExpectedCurrentHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$preconditions", entry.Preconditions);
        command.Parameters.AddWithValue("$state", entry.State.ToString());
        command.Parameters.AddWithValue("$created", entry.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$applied", (object?)entry.AppliedAt?.ToString("O") ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UndoJournalEntry>> ListAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,TaskId,Sequence,ReverseOperation,SourcePath,DestinationPath,ExpectedCurrentSize,ExpectedCurrentHash,Preconditions,State,CreatedAt,AppliedAt FROM UndoJournals WHERE TaskId=$task ORDER BY Sequence DESC;";
        command.Parameters.AddWithValue("$task", taskId.ToString("D"));
        var result = new List<UndoJournalEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            result.Add(new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), reader.GetInt32(2), Enum.Parse<FileOperationType>(reader.GetString(3)), reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetInt64(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetString(8), Enum.Parse<UndoJournalState>(reader.GetString(9)), DateTimeOffset.Parse(reader.GetString(10)), reader.IsDBNull(11) ? null : DateTimeOffset.Parse(reader.GetString(11))));
        return result;
    }

    public async Task UpdateStateAsync(Guid id, UndoJournalState state, DateTimeOffset? appliedAt, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE UndoJournals SET State=$state,AppliedAt=$applied WHERE Id=$id;";
        command.Parameters.AddWithValue("$state", state.ToString());
        command.Parameters.AddWithValue("$applied", (object?)appliedAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

