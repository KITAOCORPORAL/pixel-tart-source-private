using System.Text.Json;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;

namespace RAWSelectionAssistant.Core.Services.Tasks;

public sealed class SqliteTaskRepository(IPixelTartDatabase database) : ITaskRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    public async Task SaveAsync(TaskRuntimeState state, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Tasks(Id,ProjectId,Type,DisplayName,State,Progress,CurrentStep,CreatedAt,StartedAt,CompletedAt,LastUpdatedAt,LastErrorCode,LastErrorMessage,RetryCount,InputSnapshot,ResultSummary,Priority,MaximumRetryCount,OperationPlanId)
            VALUES($id,$project,$type,$name,$state,$progress,$step,$created,$started,$completed,$updated,$code,$message,$retry,$input,$summary,$priority,$maxRetry,$plan)
            ON CONFLICT(Id) DO UPDATE SET State=excluded.State,Progress=excluded.Progress,CurrentStep=excluded.CurrentStep,StartedAt=excluded.StartedAt,
              CompletedAt=excluded.CompletedAt,LastUpdatedAt=excluded.LastUpdatedAt,LastErrorCode=excluded.LastErrorCode,LastErrorMessage=excluded.LastErrorMessage,
              RetryCount=excluded.RetryCount,ResultSummary=excluded.ResultSummary;
            """;
        Bind(command, state);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TaskRuntimeState?> GetAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectSql + " WHERE Id=$id LIMIT 1;";
        command.Parameters.AddWithValue("$id", taskId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    public async Task<IReadOnlyList<TaskRuntimeState>> ListAsync(int limit = 200, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectSql + " ORDER BY LastUpdatedAt DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 5000));
        return await ReadManyAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TaskRuntimeState>> ListUnfinishedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectSql + " WHERE State NOT IN ('Completed','Cancelled','PartiallyCompleted','Failed') ORDER BY LastUpdatedAt;";
        return await ReadManyAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveCheckpointAsync(Guid taskId, TaskCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO TaskSteps(Id,TaskId,Sequence,Name,State,Progress,Checkpoint,StartedAt,CompletedAt,LastErrorCode)
            VALUES($id,$task,$sequence,$name,'Completed',100,$checkpoint,$at,$at,NULL)
            ON CONFLICT(TaskId,Sequence) DO UPDATE SET Name=excluded.Name,State='Completed',Progress=100,Checkpoint=excluded.Checkpoint,CompletedAt=excluded.CompletedAt;
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$task", taskId.ToString("D"));
        command.Parameters.AddWithValue("$sequence", checkpoint.CompletedItems);
        command.Parameters.AddWithValue("$name", checkpoint.StepName);
        command.Parameters.AddWithValue("$checkpoint", (object?)checkpoint.Payload ?? DBNull.Value);
        command.Parameters.AddWithValue("$at", checkpoint.CreatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string SelectSql => "SELECT Id,ProjectId,Type,DisplayName,State,Progress,CurrentStep,CreatedAt,StartedAt,CompletedAt,LastUpdatedAt,LastErrorCode,LastErrorMessage,RetryCount,InputSnapshot,ResultSummary,Priority,MaximumRetryCount,OperationPlanId FROM Tasks";

    private static void Bind(SqliteCommand command, TaskRuntimeState state)
    {
        var definition = state.Definition;
        command.Parameters.AddWithValue("$id", definition.Id.ToString("D"));
        command.Parameters.AddWithValue("$project", (object?)definition.ProjectId?.ToString("D") ?? DBNull.Value);
        command.Parameters.AddWithValue("$type", definition.Type);
        command.Parameters.AddWithValue("$name", definition.DisplayName);
        command.Parameters.AddWithValue("$state", state.State.ToString());
        command.Parameters.AddWithValue("$progress", state.Progress);
        command.Parameters.AddWithValue("$step", state.CurrentStep);
        command.Parameters.AddWithValue("$created", definition.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$started", (object?)state.StartedAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$completed", (object?)state.CompletedAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$updated", state.LastUpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$code", (object?)state.LastErrorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$message", (object?)state.LastErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$retry", state.RetryCount);
        command.Parameters.AddWithValue("$input", definition.InputSnapshot);
        command.Parameters.AddWithValue("$summary", JsonSerializer.Serialize(state.ResultSummary, JsonOptions));
        command.Parameters.AddWithValue("$priority", (int)definition.Priority);
        command.Parameters.AddWithValue("$maxRetry", definition.MaximumRetryCount);
        command.Parameters.AddWithValue("$plan", (object?)definition.OperationPlanId?.ToString("D") ?? DBNull.Value);
    }

    private static async Task<IReadOnlyList<TaskRuntimeState>> ReadManyAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var result = new List<TaskRuntimeState>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(Read(reader));
        return result;
    }

    private static TaskRuntimeState Read(SqliteDataReader reader)
    {
        var definition = new TaskDefinition(Guid.Parse(reader.GetString(0)), reader.IsDBNull(1) ? null : Guid.Parse(reader.GetString(1)), reader.GetString(2), reader.GetString(3), reader.GetString(14), reader.IsDBNull(18) ? null : Guid.Parse(reader.GetString(18)), DateTimeOffset.Parse(reader.GetString(7)), (TaskPriority)reader.GetInt32(16), reader.GetInt32(17));
        return new TaskRuntimeState
        {
            Definition = definition,
            State = Enum.Parse<TaskLifecycleState>(reader.GetString(4)),
            Progress = reader.GetDouble(5),
            CurrentStep = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            StartedAt = reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8)),
            CompletedAt = reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9)),
            LastUpdatedAt = DateTimeOffset.Parse(reader.GetString(10)),
            LastErrorCode = reader.IsDBNull(11) ? null : reader.GetString(11),
            LastErrorMessage = reader.IsDBNull(12) ? null : reader.GetString(12),
            RetryCount = reader.GetInt32(13),
            ResultSummary = reader.IsDBNull(15) ? TaskResultSummary.Empty : JsonSerializer.Deserialize<TaskResultSummary>(reader.GetString(15), JsonOptions) ?? TaskResultSummary.Empty
        };
    }
}

