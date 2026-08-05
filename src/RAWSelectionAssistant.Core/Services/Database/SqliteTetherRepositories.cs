using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Database;

public interface ITetherSessionRepository
{
    Task AddAsync(TetherSessionRecord session, CancellationToken cancellationToken = default);
    Task UpdateAsync(TetherSessionRecord session, CancellationToken cancellationToken = default);
    Task<TetherSessionRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TetherSessionRecord>> ListActiveAsync(CancellationToken cancellationToken = default);
}

public interface ITetherAssetRepository
{
    Task<TetherAssetRecord> UpsertDiscoveredAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default);
    Task UpdateAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default);
    Task<TetherAssetRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TetherAssetRecord?> GetByPathAsync(Guid sessionId, string normalizedPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TetherAssetRecord>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<bool> PairAsync(Guid sessionId, Guid leftAssetId, Guid rightAssetId, string pairingKey, CancellationToken cancellationToken = default);
}

public interface ITetherAnnotationRepository
{
    Task UpsertAsync(TetherAnnotationRecord annotation, CancellationToken cancellationToken = default);
    Task<TetherAnnotationRecord?> GetByAssetAsync(Guid assetId, CancellationToken cancellationToken = default);
}

public sealed class SqliteTetherSessionRepository(IPixelTartDatabase database) : ITetherSessionRepository
{
    public async Task AddAsync(TetherSessionRecord session, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO TetherSessions(Id,ProjectId,ProviderType,WatchDirectory,NormalizedWatchDirectory,State,StartedAtUtc,DiscoveryCutoffUtc,ImportExisting,CopyToProject,ProjectDestination,CopyToBackup,BackupDestination,LastReconciledAtUtc,CreatedAtUtc,UpdatedAtUtc,StoppedAtUtc,LastErrorCode)
            VALUES($id,$project,$provider,$directory,$normalized,$state,$started,$cutoff,$existing,$projectCopy,$projectDestination,$backupCopy,$backupDestination,$reconciled,$created,$updated,$stopped,$error);
            """;
        Bind(command, session);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(TetherSessionRecord session, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE TetherSessions SET ProjectId=$project,ProviderType=$provider,WatchDirectory=$directory,NormalizedWatchDirectory=$normalized,State=$state,
            DiscoveryCutoffUtc=$cutoff,ImportExisting=$existing,CopyToProject=$projectCopy,ProjectDestination=$projectDestination,CopyToBackup=$backupCopy,
            BackupDestination=$backupDestination,LastReconciledAtUtc=$reconciled,CreatedAtUtc=$created,UpdatedAtUtc=$updated,StoppedAtUtc=$stopped,LastErrorCode=$error WHERE Id=$id;
            """;
        Bind(command, session);
        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
            throw new InvalidOperationException("Tether session was not found.");
    }

    public async Task<TetherSessionRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectColumns + " WHERE Id=$id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    public async Task<IReadOnlyList<TetherSessionRecord>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<TetherSessionRecord>();
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectColumns + " WHERE State IN ('Running','NeedsAttention') ORDER BY UpdatedAtUtc DESC;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(Read(reader));
        return result;
    }

    private const string SelectColumns = "SELECT Id,ProjectId,ProviderType,WatchDirectory,NormalizedWatchDirectory,State,StartedAtUtc,DiscoveryCutoffUtc,ImportExisting,CopyToProject,ProjectDestination,CopyToBackup,BackupDestination,UpdatedAtUtc,StoppedAtUtc,LastErrorCode,LastReconciledAtUtc,CreatedAtUtc FROM TetherSessions";

    private static void Bind(SqliteCommand command, TetherSessionRecord value)
    {
        command.Parameters.AddWithValue("$id", value.Id.ToString("D"));
        command.Parameters.AddWithValue("$project", Db(value.ProjectId?.ToString("D")));
        command.Parameters.AddWithValue("$provider", value.ProviderType.ToString());
        command.Parameters.AddWithValue("$directory", value.WatchDirectory);
        command.Parameters.AddWithValue("$normalized", value.NormalizedWatchDirectory);
        command.Parameters.AddWithValue("$state", value.State.ToString());
        command.Parameters.AddWithValue("$started", value.StartedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$cutoff", value.DiscoveryCutoffUtc.ToString("O"));
        command.Parameters.AddWithValue("$existing", value.ImportExisting ? 1 : 0);
        command.Parameters.AddWithValue("$projectCopy", value.CopyToProject ? 1 : 0);
        command.Parameters.AddWithValue("$projectDestination", Db(value.ProjectDestination));
        command.Parameters.AddWithValue("$backupCopy", value.CopyToBackup ? 1 : 0);
        command.Parameters.AddWithValue("$backupDestination", Db(value.BackupDestination));
        command.Parameters.AddWithValue("$reconciled", Db(value.LastReconciledAtUtc?.ToString("O")));
        command.Parameters.AddWithValue("$created", (value.CreatedAtUtc ?? value.StartedAtUtc).ToString("O"));
        command.Parameters.AddWithValue("$updated", value.UpdatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$stopped", Db(value.StoppedAtUtc?.ToString("O")));
        command.Parameters.AddWithValue("$error", Db(value.LastErrorCode));
    }

    private static TetherSessionRecord Read(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)), reader.IsDBNull(1) ? null : Guid.Parse(reader.GetString(1)), Parse(reader.GetString(2), CameraProviderType.None),
        reader.GetString(3), reader.GetString(4), Parse(reader.GetString(5), TetherSessionState.NeedsAttention), DateTimeOffset.Parse(reader.GetString(6)),
        DateTimeOffset.Parse(reader.GetString(7)), reader.GetInt32(8) != 0, reader.GetInt32(9) != 0, reader.IsDBNull(10) ? null : reader.GetString(10),
        reader.GetInt32(11) != 0, reader.IsDBNull(12) ? null : reader.GetString(12), DateTimeOffset.Parse(reader.GetString(13)),
        reader.IsDBNull(14) ? null : DateTimeOffset.Parse(reader.GetString(14)), reader.IsDBNull(15) ? null : reader.GetString(15),
        reader.IsDBNull(16) ? null : DateTimeOffset.Parse(reader.GetString(16)), reader.IsDBNull(17) ? null : DateTimeOffset.Parse(reader.GetString(17)));

    private static object Db(object? value) => value ?? DBNull.Value;
    private static T Parse<T>(string value, T fallback) where T : struct => Enum.TryParse<T>(value, out var parsed) ? parsed : fallback;
}

public sealed class SqliteTetherAssetRepository(IPixelTartDatabase database) : ITetherAssetRepository
{
    public async Task<TetherAssetRecord> UpsertDiscoveredAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO TetherAssets(Id,SessionId,ProjectId,SourcePath,NormalizedSourcePath,FileName,Extension,MediaKind,FileSize,ModifiedAtUtc,FirstSeenAtUtc,ReadyAtUtc,StabilityState,ProcessingState,PreviewState,ProxyCacheKey,PairingKey,PairedAssetId,ProjectCopyTaskId,ProjectCopyPath,BackupCopyTaskId,BackupCopyPath,LastErrorCode,UpdatedAtUtc)
            VALUES($id,$session,$project,$source,$normalized,$file,$extension,$kind,$size,$modified,$seen,$stable,$stability,$processing,$preview,$proxy,$pairing,$paired,$projectTask,$projectPath,$backupTask,$backupPath,$error,$updated)
            ON CONFLICT(SessionId,NormalizedSourcePath) DO UPDATE SET SourcePath=excluded.SourcePath,FileName=excluded.FileName,Extension=excluded.Extension,MediaKind=excluded.MediaKind,UpdatedAtUtc=excluded.UpdatedAtUtc;
            """;
        Bind(command, asset);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return (await GetByPathAsync(asset.SessionId, asset.NormalizedSourcePath, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task UpdateAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE TetherAssets SET ProjectId=$project,SourcePath=$source,NormalizedSourcePath=$normalized,FileName=$file,Extension=$extension,MediaKind=$kind,
            FileSize=$size,ModifiedAtUtc=$modified,ReadyAtUtc=$stable,StabilityState=$stability,ProcessingState=$processing,PreviewState=$preview,
            ProxyCacheKey=$proxy,PairingKey=$pairing,PairedAssetId=$paired,ProjectCopyTaskId=$projectTask,ProjectCopyPath=$projectPath,
            BackupCopyTaskId=$backupTask,BackupCopyPath=$backupPath,LastErrorCode=$error,UpdatedAtUtc=$updated WHERE Id=$id;
            """;
        Bind(command, asset);
        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
            throw new InvalidOperationException("Tether asset was not found.");
    }

    public async Task<TetherAssetRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectColumns + " WHERE Id=$id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    public async Task<TetherAssetRecord?> GetByPathAsync(Guid sessionId, string normalizedPath, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectColumns + " WHERE SessionId=$session AND NormalizedSourcePath=$path LIMIT 1;";
        command.Parameters.AddWithValue("$session", sessionId.ToString("D"));
        command.Parameters.AddWithValue("$path", normalizedPath);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    public async Task<IReadOnlyList<TetherAssetRecord>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var result = new List<TetherAssetRecord>();
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectColumns + " WHERE SessionId=$session ORDER BY FirstSeenAtUtc DESC, Id DESC;";
        command.Parameters.AddWithValue("$session", sessionId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(Read(reader));
        return result;
    }

    public async Task<bool> PairAsync(Guid sessionId, Guid leftAssetId, Guid rightAssetId, string pairingKey, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE TetherAssets
            SET PairingKey=$key,
                PairedAssetId=CASE WHEN Id=$left THEN $right ELSE $left END,
                UpdatedAtUtc=$updated
            WHERE SessionId=$session AND Id IN ($left,$right) AND PairedAssetId IS NULL;
            """;
        command.Parameters.AddWithValue("$key", pairingKey);
        command.Parameters.AddWithValue("$left", leftAssetId.ToString("D"));
        command.Parameters.AddWithValue("$right", rightAssetId.ToString("D"));
        command.Parameters.AddWithValue("$session", sessionId.ToString("D"));
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        var updated = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (updated != 2) { await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false); return false; }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private const string SelectColumns = "SELECT Id,SessionId,ProjectId,SourcePath,NormalizedSourcePath,FileName,Extension,MediaKind,FileSize,ModifiedAtUtc,FirstSeenAtUtc,StabilityState,ProcessingState,PreviewState,UpdatedAtUtc,ReadyAtUtc,ProxyCacheKey,PairingKey,PairedAssetId,ProjectCopyTaskId,ProjectCopyPath,BackupCopyTaskId,BackupCopyPath,LastErrorCode FROM TetherAssets";

    private static void Bind(SqliteCommand command, TetherAssetRecord value)
    {
        command.Parameters.AddWithValue("$id", value.Id.ToString("D")); command.Parameters.AddWithValue("$session", value.SessionId.ToString("D"));
        command.Parameters.AddWithValue("$project", Db(value.ProjectId?.ToString("D"))); command.Parameters.AddWithValue("$source", value.SourcePath);
        command.Parameters.AddWithValue("$normalized", value.NormalizedSourcePath); command.Parameters.AddWithValue("$file", value.FileName);
        command.Parameters.AddWithValue("$extension", value.Extension); command.Parameters.AddWithValue("$kind", value.MediaKind.ToString());
        command.Parameters.AddWithValue("$size", Db(value.FileSize)); command.Parameters.AddWithValue("$modified", Db(value.ModifiedAtUtc?.ToString("O")));
        command.Parameters.AddWithValue("$seen", value.FirstSeenAtUtc.ToString("O")); command.Parameters.AddWithValue("$stable", Db(value.ReadyAtUtc?.ToString("O")));
        command.Parameters.AddWithValue("$stability", value.StabilityState.ToString()); command.Parameters.AddWithValue("$processing", value.ProcessingState.ToString());
        command.Parameters.AddWithValue("$preview", value.PreviewState.ToString()); command.Parameters.AddWithValue("$proxy", Db(value.ProxyCacheKey));
        command.Parameters.AddWithValue("$pairing", Db(value.PairingKey)); command.Parameters.AddWithValue("$paired", Db(value.PairedAssetId?.ToString("D")));
        command.Parameters.AddWithValue("$projectTask", Db(value.ProjectCopyTaskId?.ToString("D"))); command.Parameters.AddWithValue("$projectPath", Db(value.ProjectCopyPath));
        command.Parameters.AddWithValue("$backupTask", Db(value.BackupCopyTaskId?.ToString("D"))); command.Parameters.AddWithValue("$backupPath", Db(value.BackupCopyPath));
        command.Parameters.AddWithValue("$error", Db(value.LastErrorCode)); command.Parameters.AddWithValue("$updated", value.UpdatedAtUtc.ToString("O"));
    }

    private static TetherAssetRecord Read(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)), reader.GetString(3), reader.GetString(4),
        reader.GetString(5), reader.GetString(6), Parse(reader.GetString(7), TetherMediaKind.Unsupported), reader.IsDBNull(8) ? null : reader.GetInt64(8),
        reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9)), DateTimeOffset.Parse(reader.GetString(10)), Parse(reader.GetString(11), TetherStabilityState.Pending),
        Parse(reader.GetString(12), TetherProcessingState.Pending), Parse(reader.GetString(13), TetherPreviewState.None), DateTimeOffset.Parse(reader.GetString(14)),
        reader.IsDBNull(15) ? null : DateTimeOffset.Parse(reader.GetString(15)), reader.IsDBNull(16) ? null : reader.GetString(16), reader.IsDBNull(17) ? null : reader.GetString(17),
        reader.IsDBNull(18) ? null : Guid.Parse(reader.GetString(18)), reader.IsDBNull(19) ? null : Guid.Parse(reader.GetString(19)), reader.IsDBNull(20) ? null : reader.GetString(20),
        reader.IsDBNull(21) ? null : Guid.Parse(reader.GetString(21)), reader.IsDBNull(22) ? null : reader.GetString(22), reader.IsDBNull(23) ? null : reader.GetString(23));

    private static object Db(object? value) => value ?? DBNull.Value;
    private static T Parse<T>(string value, T fallback) where T : struct => Enum.TryParse<T>(value, out var parsed) ? parsed : fallback;
}

public sealed class SqliteTetherAnnotationRepository(IPixelTartDatabase database) : ITetherAnnotationRepository
{
    public async Task UpsertAsync(TetherAnnotationRecord annotation, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO TetherAnnotations(Id,AssetId,Rating,ColorLabel,PhotographerNote,ClientFavorite,ClientNote,IsRejected,CreatedAtUtc,UpdatedAtUtc)
            VALUES($id,$asset,$rating,$color,$photographerNote,$favorite,$clientNote,$rejected,$created,$updated)
            ON CONFLICT(AssetId) DO UPDATE SET Rating=excluded.Rating,ColorLabel=excluded.ColorLabel,PhotographerNote=excluded.PhotographerNote,ClientFavorite=excluded.ClientFavorite,ClientNote=excluded.ClientNote,IsRejected=excluded.IsRejected,UpdatedAtUtc=excluded.UpdatedAtUtc;
            """;
        command.Parameters.AddWithValue("$id", annotation.Id.ToString("D")); command.Parameters.AddWithValue("$asset", annotation.AssetId.ToString("D"));
        command.Parameters.AddWithValue("$rating", Math.Clamp(annotation.Rating, 0, 5)); command.Parameters.AddWithValue("$color", (object?)annotation.ColorLabel ?? DBNull.Value);
        command.Parameters.AddWithValue("$photographerNote", (object?)annotation.PhotographerNote ?? DBNull.Value);
        command.Parameters.AddWithValue("$favorite", annotation.ClientFavorite ? 1 : 0); command.Parameters.AddWithValue("$clientNote", (object?)annotation.ClientNote ?? DBNull.Value);
        command.Parameters.AddWithValue("$rejected", annotation.IsRejected ? 1 : 0); command.Parameters.AddWithValue("$created", annotation.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$updated", annotation.UpdatedAtUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TetherAnnotationRecord?> GetByAssetAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,AssetId,Rating,ColorLabel,PhotographerNote,CreatedAtUtc,UpdatedAtUtc,ClientFavorite,ClientNote,IsRejected FROM TetherAnnotations WHERE AssetId=$asset LIMIT 1;";
        command.Parameters.AddWithValue("$asset", assetId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        return new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), reader.GetInt32(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), DateTimeOffset.Parse(reader.GetString(5)), DateTimeOffset.Parse(reader.GetString(6)), reader.GetInt32(7) != 0, reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetInt32(9) != 0);
    }
}
