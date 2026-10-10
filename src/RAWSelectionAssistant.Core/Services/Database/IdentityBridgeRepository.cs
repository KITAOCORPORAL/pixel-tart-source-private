using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.Core.Services.Database;

public enum IdentityBridgeState { Pending, Ready, Conflict, Failed }
public enum CrossModuleOperationState { Pending, Running, Ready, Conflict, Failed }

public sealed record TetherAssetLibraryLink(
    Guid TetherAssetId,
    Guid LibraryId,
    Guid AssetId,
    string? SourceContentHash,
    IdentityBridgeState State,
    string? LastErrorCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CrossModuleOperation(
    Guid OperationId,
    string IdempotencyKey,
    string OperationType,
    string SourceKind,
    Guid SourceId,
    Guid? LibraryId,
    Guid? AssetId,
    CrossModuleOperationState State,
    int AttemptCount,
    string? PayloadHash,
    string? LastErrorCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed class IdentityBridgeRepository(PixelTartDatabase database)
{
    public async Task<TetherAssetLibraryLink> SaveLinkAsync(
        Guid tetherAssetId,
        Guid libraryId,
        Guid assetId,
        string? sourceContentHash,
        IdentityBridgeState state,
        string? lastErrorCode = null,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tetherAssetId, nameof(tetherAssetId));
        ValidateIdentity(libraryId, nameof(libraryId));
        ValidateIdentity(assetId, nameof(assetId));
        sourceContentHash = NormalizeOptionalHash(sourceContentHash, nameof(sourceContentHash));
        var now = DateTimeOffset.UtcNow;
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var existing = await ReadLinkAsync(connection, transaction, tetherAssetId, libraryId, cancellationToken).ConfigureAwait(false);
        if (existing is not null && (existing.AssetId != assetId || HashConflicts(existing.SourceContentHash, sourceContentHash)))
        {
            await UpdateLinkStateAsync(connection, transaction, tetherAssetId, libraryId, IdentityBridgeState.Conflict,
                "IDENTITY_CONFLICT", now, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return existing with { State = IdentityBridgeState.Conflict, LastErrorCode = "IDENTITY_CONFLICT", UpdatedAtUtc = now };
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO TetherAssetLibraryLinks(TetherAssetId,LibraryId,AssetId,SourceContentHash,State,LastErrorCode,CreatedAtUtc,UpdatedAtUtc)
                VALUES($tether,$library,$asset,$hash,$state,$error,$created,$updated)
                ON CONFLICT(TetherAssetId,LibraryId) DO UPDATE SET
                    SourceContentHash=COALESCE(excluded.SourceContentHash,TetherAssetLibraryLinks.SourceContentHash),
                    State=excluded.State,LastErrorCode=excluded.LastErrorCode,UpdatedAtUtc=excluded.UpdatedAtUtc;
                """;
            command.Parameters.AddWithValue("$tether", tetherAssetId.ToString("D"));
            command.Parameters.AddWithValue("$library", libraryId.ToString("D"));
            command.Parameters.AddWithValue("$asset", assetId.ToString("D"));
            command.Parameters.AddWithValue("$hash", (object?)sourceContentHash ?? DBNull.Value);
            command.Parameters.AddWithValue("$state", state.ToString());
            command.Parameters.AddWithValue("$error", (object?)lastErrorCode ?? DBNull.Value);
            command.Parameters.AddWithValue("$created", existing?.CreatedAtUtc.ToString("O") ?? now.ToString("O"));
            command.Parameters.AddWithValue("$updated", now.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(tetherAssetId, libraryId, assetId, sourceContentHash ?? existing?.SourceContentHash,
            state, lastErrorCode, existing?.CreatedAtUtc ?? now, now);
    }

    public async Task<TetherAssetLibraryLink?> GetLinkAsync(
        Guid tetherAssetId,
        Guid libraryId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return await ReadLinkAsync(connection, null, tetherAssetId, libraryId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CrossModuleOperation> BeginOperationAsync(
        string idempotencyKey,
        string operationType,
        string sourceKind,
        Guid sourceId,
        Guid? libraryId = null,
        string? payloadHash = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("幂等键不能为空。", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(operationType)) throw new ArgumentException("操作类型不能为空。", nameof(operationType));
        if (string.IsNullOrWhiteSpace(sourceKind)) throw new ArgumentException("来源类型不能为空。", nameof(sourceKind));
        ValidateIdentity(sourceId, nameof(sourceId));
        if (libraryId == Guid.Empty) throw new ArgumentException("素材库身份不能是空 GUID。", nameof(libraryId));
        payloadHash = NormalizeOptionalHash(payloadHash, nameof(payloadHash));
        var now = DateTimeOffset.UtcNow;
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var existing = await ReadOperationAsync(connection, transaction, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (!string.Equals(existing.OperationType, operationType, StringComparison.Ordinal) ||
                !string.Equals(existing.SourceKind, sourceKind, StringComparison.Ordinal) ||
                existing.SourceId != sourceId || existing.LibraryId != libraryId || HashConflicts(existing.PayloadHash, payloadHash))
            {
                await UpdateOperationStateInternalAsync(connection, transaction, existing.OperationId,
                    CrossModuleOperationState.Conflict, existing.AssetId, "IDEMPOTENCY_CONFLICT", now, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return existing with { State = CrossModuleOperationState.Conflict, LastErrorCode = "IDEMPOTENCY_CONFLICT", UpdatedAtUtc = now };
            }
            if (existing.State == CrossModuleOperationState.Ready)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return existing;
            }
            await using var retry = connection.CreateCommand();
            retry.Transaction = transaction;
            retry.CommandText = "UPDATE CrossModuleOperations SET State='Pending',AttemptCount=AttemptCount+1,LastErrorCode=NULL,UpdatedAtUtc=$updated WHERE OperationId=$id;";
            retry.Parameters.AddWithValue("$updated", now.ToString("O"));
            retry.Parameters.AddWithValue("$id", existing.OperationId.ToString("D"));
            await retry.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return existing with { State = CrossModuleOperationState.Pending, AttemptCount = existing.AttemptCount + 1, LastErrorCode = null, UpdatedAtUtc = now };
        }

        var created = new CrossModuleOperation(Guid.NewGuid(), idempotencyKey.Trim(), operationType.Trim(), sourceKind.Trim(),
            sourceId, libraryId, null, CrossModuleOperationState.Pending, 1, payloadHash, null, now, now);
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO CrossModuleOperations(OperationId,IdempotencyKey,OperationType,SourceKind,SourceId,LibraryId,AssetId,State,AttemptCount,PayloadHash,LastErrorCode,CreatedAtUtc,UpdatedAtUtc)
                VALUES($id,$key,$type,$kind,$source,$library,NULL,'Pending',1,$payload,NULL,$created,$updated);
                """;
            insert.Parameters.AddWithValue("$id", created.OperationId.ToString("D"));
            insert.Parameters.AddWithValue("$key", created.IdempotencyKey);
            insert.Parameters.AddWithValue("$type", created.OperationType);
            insert.Parameters.AddWithValue("$kind", created.SourceKind);
            insert.Parameters.AddWithValue("$source", created.SourceId.ToString("D"));
            insert.Parameters.AddWithValue("$library", (object?)created.LibraryId?.ToString("D") ?? DBNull.Value);
            insert.Parameters.AddWithValue("$payload", (object?)created.PayloadHash ?? DBNull.Value);
            insert.Parameters.AddWithValue("$created", now.ToString("O"));
            insert.Parameters.AddWithValue("$updated", now.ToString("O"));
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return created;
    }

    public async Task<CrossModuleOperation> CompleteOperationAsync(
        Guid operationId,
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(operationId, nameof(operationId));
        ValidateIdentity(assetId, nameof(assetId));
        var now = DateTimeOffset.UtcNow;
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var operation = await ReadOperationByIdAsync(connection, transaction, operationId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("跨模块操作不存在。");
        if (operation.State == CrossModuleOperationState.Conflict)
            throw new InvalidOperationException("冲突操作不能直接标记为完成。");
        await UpdateOperationStateInternalAsync(connection, transaction, operationId, CrossModuleOperationState.Ready,
            assetId, null, now, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return operation with { State = CrossModuleOperationState.Ready, AssetId = assetId, LastErrorCode = null, UpdatedAtUtc = now };
    }

    private static async Task<TetherAssetLibraryLink?> ReadLinkAsync(SqliteConnection connection, SqliteTransaction? transaction,
        Guid tetherAssetId, Guid libraryId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT TetherAssetId,LibraryId,AssetId,SourceContentHash,State,LastErrorCode,CreatedAtUtc,UpdatedAtUtc FROM TetherAssetLibraryLinks WHERE TetherAssetId=$tether AND LibraryId=$library;";
        command.Parameters.AddWithValue("$tether", tetherAssetId.ToString("D")); command.Parameters.AddWithValue("$library", libraryId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadLink(reader) : null;
    }

    private static async Task<CrossModuleOperation?> ReadOperationAsync(SqliteConnection connection, SqliteTransaction transaction,
        string key, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT OperationId,IdempotencyKey,OperationType,SourceKind,SourceId,LibraryId,AssetId,State,AttemptCount,PayloadHash,LastErrorCode,CreatedAtUtc,UpdatedAtUtc FROM CrossModuleOperations WHERE IdempotencyKey=$key;";
        command.Parameters.AddWithValue("$key", key.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadOperation(reader) : null;
    }

    private static async Task<CrossModuleOperation?> ReadOperationByIdAsync(SqliteConnection connection, SqliteTransaction transaction,
        Guid operationId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT OperationId,IdempotencyKey,OperationType,SourceKind,SourceId,LibraryId,AssetId,State,AttemptCount,PayloadHash,LastErrorCode,CreatedAtUtc,UpdatedAtUtc FROM CrossModuleOperations WHERE OperationId=$id;";
        command.Parameters.AddWithValue("$id", operationId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadOperation(reader) : null;
    }

    private static async Task UpdateLinkStateAsync(SqliteConnection connection, SqliteTransaction transaction, Guid tetherAssetId,
        Guid libraryId, IdentityBridgeState state, string? error, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "UPDATE TetherAssetLibraryLinks SET State=$state,LastErrorCode=$error,UpdatedAtUtc=$updated WHERE TetherAssetId=$tether AND LibraryId=$library;";
        command.Parameters.AddWithValue("$state", state.ToString()); command.Parameters.AddWithValue("$error", (object?)error ?? DBNull.Value);
        command.Parameters.AddWithValue("$updated", now.ToString("O")); command.Parameters.AddWithValue("$tether", tetherAssetId.ToString("D")); command.Parameters.AddWithValue("$library", libraryId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpdateOperationStateInternalAsync(SqliteConnection connection, SqliteTransaction transaction, Guid operationId,
        CrossModuleOperationState state, Guid? assetId, string? error, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "UPDATE CrossModuleOperations SET State=$state,AssetId=COALESCE($asset,AssetId),LastErrorCode=$error,UpdatedAtUtc=$updated WHERE OperationId=$id;";
        command.Parameters.AddWithValue("$state", state.ToString()); command.Parameters.AddWithValue("$asset", (object?)assetId?.ToString("D") ?? DBNull.Value);
        command.Parameters.AddWithValue("$error", (object?)error ?? DBNull.Value); command.Parameters.AddWithValue("$updated", now.ToString("O")); command.Parameters.AddWithValue("$id", operationId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TetherAssetLibraryLink ReadLink(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), Guid.Parse(reader.GetString(2)),
        reader.IsDBNull(3) ? null : reader.GetString(3), Enum.Parse<IdentityBridgeState>(reader.GetString(4)),
        reader.IsDBNull(5) ? null : reader.GetString(5), DateTimeOffset.Parse(reader.GetString(6)), DateTimeOffset.Parse(reader.GetString(7)));

    private static CrossModuleOperation ReadOperation(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.GetString(3), Guid.Parse(reader.GetString(4)),
        reader.IsDBNull(5) ? null : Guid.Parse(reader.GetString(5)), reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6)),
        Enum.Parse<CrossModuleOperationState>(reader.GetString(7)), reader.GetInt32(8), reader.IsDBNull(9) ? null : reader.GetString(9),
        reader.IsDBNull(10) ? null : reader.GetString(10), DateTimeOffset.Parse(reader.GetString(11)), DateTimeOffset.Parse(reader.GetString(12)));

    private static void ValidateIdentity(Guid value, string name)
    { if (value == Guid.Empty) throw new ArgumentException("身份不能是空 GUID。", name); }

    private static string? NormalizeOptionalHash(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var hash = value.Trim().ToLowerInvariant();
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit)) throw new ArgumentException("内容 hash 必须是完整 SHA-256。", name);
        return hash;
    }

    private static bool HashConflicts(string? left, string? right) => left is not null && right is not null &&
        !string.Equals(left, right, StringComparison.Ordinal);
}
