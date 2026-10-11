using Microsoft.Data.Sqlite;
namespace RAWSelectionAssistant.Core.Services.Database;

public sealed record BridgeOperationClaim(Guid OperationId, int Attempt);

public sealed partial class IdentityBridgeRepository
{
    public FileStream? TryAcquireExecutionLock(string key)
    {
        var root = database.DatabasePath + ".bridge-locks";
        Directory.CreateDirectory(root);
        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key.Trim())));
        try { return new FileStream(Path.Combine(root, digest + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { return null; }
    }
    // AttemptCount is a persisted fencing generation, not a process-local lock.
    public async Task<BridgeOperationClaim?> TryClaimOperationAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE CrossModuleOperations SET State='Running',AttemptCount=AttemptCount+1,UpdatedAtUtc=$now WHERE OperationId=$id AND State='Pending' RETURNING AttemptCount;";
        command.Parameters.AddWithValue("$id", operationId.ToString("D"));
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null ? null : new(operationId, Convert.ToInt32(result));
    }

    public async Task<CrossModuleOperation?> GetOperationAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken);
        await using var transaction = connection.BeginTransaction();
        return await ReadOperationByIdAsync(connection, transaction, operationId, cancellationToken);
    }

    public async Task<IReadOnlyList<CrossModuleOperation>> ListIncompleteOperationsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT OperationId,IdempotencyKey,OperationType,SourceKind,SourceId,LibraryId,AssetId,State,AttemptCount,PayloadHash,LastErrorCode,CreatedAtUtc,UpdatedAtUtc FROM CrossModuleOperations WHERE State!='Ready' ORDER BY UpdatedAtUtc;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<CrossModuleOperation>();
        while (await reader.ReadAsync(cancellationToken)) result.Add(ReadOperation(reader));
        return result;
    }

    public Task<bool> CompleteClaimAsync(BridgeOperationClaim claim, Guid assetId, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(assetId, nameof(assetId));
        return FinishClaimAsync(claim, "Ready", assetId, null, cancellationToken);
    }

    public Task<bool> FailClaimAsync(BridgeOperationClaim claim, string errorCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        return FinishClaimAsync(claim, "Failed", null, errorCode.Trim(), cancellationToken);
    }

    private async Task<bool> FinishClaimAsync(BridgeOperationClaim claim, string state, Guid? assetId, string? error, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE CrossModuleOperations SET State=$state,AssetId=COALESCE($asset,AssetId),LastErrorCode=$error,UpdatedAtUtc=$now WHERE OperationId=$id AND State='Running' AND AttemptCount=$attempt AND (AssetId IS NULL OR $asset IS NULL OR AssetId=$asset);";
        command.Parameters.AddWithValue("$state", state);
        command.Parameters.AddWithValue("$asset", (object?)assetId?.ToString("D") ?? DBNull.Value);
        command.Parameters.AddWithValue("$error", (object?)error ?? DBNull.Value);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", claim.OperationId.ToString("D"));
        command.Parameters.AddWithValue("$attempt", claim.Attempt);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    // Explicit recovery requires the generation the operator observed. Never recover Conflict.
    // Caller must stop the old worker before recovering Running; fencing rejects its late completion.
    public async Task<bool> RetryOperationAsync(Guid operationId, int expectedAttempt, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE CrossModuleOperations SET State='Pending',AttemptCount=AttemptCount+1,UpdatedAtUtc=$now WHERE OperationId=$id AND AttemptCount=$attempt AND State IN ('Failed','Running','Pending');";
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", operationId.ToString("D"));
        command.Parameters.AddWithValue("$attempt", expectedAttempt);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
