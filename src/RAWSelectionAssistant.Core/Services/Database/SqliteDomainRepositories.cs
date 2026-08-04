using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Database;

public interface IProjectRepository
{
    Task UpsertAsync(PhotoProjectRecord project, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PhotoProjectRecord>> ListAsync(CancellationToken cancellationToken = default);
}

public sealed class SqliteProjectRepository(IPixelTartDatabase database) : IProjectRepository
{
    public async Task UpsertAsync(PhotoProjectRecord project, CancellationToken cancellationToken = default)
    {
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Projects(Id,Name,ProjectType,WorkflowState,RootPath,CreatedAt,UpdatedAt,LastOpenedAt,IsArchived)
                VALUES($id,$name,$type,$state,$root,$created,$updated,$opened,0)
                ON CONFLICT(Id) DO UPDATE SET Name=excluded.Name,ProjectType=excluded.ProjectType,WorkflowState=excluded.WorkflowState,RootPath=excluded.RootPath,UpdatedAt=excluded.UpdatedAt,LastOpenedAt=excluded.LastOpenedAt;
                """;
            command.Parameters.AddWithValue("$id", project.Id.ToString("D")); command.Parameters.AddWithValue("$name", project.Name); command.Parameters.AddWithValue("$type", project.Category.ToString()); command.Parameters.AddWithValue("$state", project.Status.ToString());
            command.Parameters.AddWithValue("$root", (object?)project.OutputDirectory ?? DBNull.Value); command.Parameters.AddWithValue("$created", project.CreatedAt.ToString("O")); command.Parameters.AddWithValue("$updated", project.UpdatedAt.ToString("O")); command.Parameters.AddWithValue("$opened", project.UpdatedAt.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await DeleteChildrenAsync(connection, transaction, project.Id, cancellationToken).ConfigureAwait(false);
        for (var index = 0; index < project.SourceDirectories.Count; index++)
        {
            await using var source = connection.CreateCommand(); source.Transaction = transaction;
            source.CommandText = "INSERT INTO ProjectSources(Id,ProjectId,Path,Purpose,Priority,IsAvailable,LastCheckedAt) VALUES($id,$project,$path,'Mixed',$priority,$available,$checked);";
            source.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D")); source.Parameters.AddWithValue("$project", project.Id.ToString("D")); source.Parameters.AddWithValue("$path", project.SourceDirectories[index]); source.Parameters.AddWithValue("$priority", index); source.Parameters.AddWithValue("$available", Directory.Exists(project.SourceDirectories[index]) ? 1 : 0); source.Parameters.AddWithValue("$checked", DateTimeOffset.UtcNow.ToString("O"));
            await source.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        foreach (var value in project.SelectionInputs)
        {
            await using var input = connection.CreateCommand(); input.Transaction = transaction;
            input.CommandText = "INSERT INTO SelectionInputs(Id,ProjectId,OriginalInput,NormalizedName,NumericId,SourceType,CreatedAt) VALUES($id,$project,$value,$normalized,$number,'CurrentProject',$created);";
            input.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D")); input.Parameters.AddWithValue("$project", project.Id.ToString("D")); input.Parameters.AddWithValue("$value", value); input.Parameters.AddWithValue("$normalized", Path.GetFileNameWithoutExtension(value).ToUpperInvariant());
            var digits = new string(value.Where(char.IsDigit).ToArray()); input.Parameters.AddWithValue("$number", long.TryParse(digits, out var number) ? number : DBNull.Value); input.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
            await input.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PhotoProjectRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var projects = new List<PhotoProjectRecord>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,Name,ProjectType,WorkflowState,RootPath,CreatedAt,UpdatedAt FROM Projects WHERE IsArchived=0 ORDER BY UpdatedAt DESC;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            projects.Add(new PhotoProjectRecord { Id = Guid.Parse(reader.GetString(0)), Name = reader.GetString(1), Category = Enum.TryParse<CollectionCategory>(reader.GetString(2), out var category) ? category : CollectionCategory.JpegAndRaw, Status = Enum.TryParse<PhotoProjectStatus>(reader.GetString(3), out var status) ? status : PhotoProjectStatus.Draft, OutputDirectory = reader.IsDBNull(4) ? string.Empty : reader.GetString(4), CreatedAt = DateTimeOffset.Parse(reader.GetString(5)), UpdatedAt = DateTimeOffset.Parse(reader.GetString(6)) });
        }
        foreach (var project in projects)
        {
            await using var source = connection.CreateCommand(); source.CommandText = "SELECT Path FROM ProjectSources WHERE ProjectId=$project ORDER BY Priority;"; source.Parameters.AddWithValue("$project", project.Id.ToString("D"));
            await using var sourceReader = await source.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); while (await sourceReader.ReadAsync(cancellationToken).ConfigureAwait(false)) project.SourceDirectories.Add(sourceReader.GetString(0));
            await using var input = connection.CreateCommand(); input.CommandText = "SELECT OriginalInput FROM SelectionInputs WHERE ProjectId=$project ORDER BY CreatedAt;"; input.Parameters.AddWithValue("$project", project.Id.ToString("D"));
            await using var inputReader = await input.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); while (await inputReader.ReadAsync(cancellationToken).ConfigureAwait(false)) project.SelectionInputs.Add(inputReader.GetString(0));
        }
        return projects;
    }

    private static async Task DeleteChildrenAsync(SqliteConnection connection, SqliteTransaction transaction, Guid projectId, CancellationToken cancellationToken)
    {
        foreach (var table in new[] { "ProjectSources", "SelectionInputs" })
        {
            await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = $"DELETE FROM {table} WHERE ProjectId=$project;"; command.Parameters.AddWithValue("$project", projectId.ToString("D")); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

public interface IMediaIndexRepository
{
    Task ReplaceAsync(IReadOnlyList<MediaFileRecord> media, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaFileRecord>> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed class SqliteMediaIndexRepository(IPixelTartDatabase database) : IMediaIndexRepository
{
    public async Task ReplaceAsync(IReadOnlyList<MediaFileRecord> media, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (var clear = connection.CreateCommand()) { clear.Transaction = transaction; clear.CommandText = "DELETE FROM MediaFiles WHERE ProjectId IS NULL;"; await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        foreach (var item in media)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "INSERT INTO MediaFiles(Id,ProjectId,FullPath,NormalizedPath,FileName,Extension,FileSize,ModifiedAt,Format,MetadataState,OptionalHash,IsAvailable) VALUES($id,NULL,$path,$normalized,$file,$extension,$size,$modified,$format,$metadata,NULL,1);";
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D")); command.Parameters.AddWithValue("$path", item.FullPath); command.Parameters.AddWithValue("$normalized", Path.GetFullPath(item.FullPath).ToUpperInvariant()); command.Parameters.AddWithValue("$file", item.FileName); command.Parameters.AddWithValue("$extension", item.Extension); command.Parameters.AddWithValue("$size", item.Size); command.Parameters.AddWithValue("$modified", new DateTimeOffset(item.LastWriteTimeUtc.ToUniversalTime()).ToString("O")); command.Parameters.AddWithValue("$format", item.Category.ToString()); command.Parameters.AddWithValue("$metadata", item.JpegQuality is null ? "Unknown" : "Read");
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MediaFileRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<MediaFileRecord>();
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT FullPath,FileName,Extension,FileSize,ModifiedAt,Format,IsAvailable FROM MediaFiles WHERE ProjectId IS NULL ORDER BY FileName;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new MediaFileRecord { FullPath = reader.GetString(0), FileName = reader.GetString(1), BaseName = Path.GetFileNameWithoutExtension(reader.GetString(1)), NormalizedName = Path.GetFileNameWithoutExtension(reader.GetString(1)).ToUpperInvariant(), Extension = reader.GetString(2), Size = reader.GetInt64(3), LastWriteTimeUtc = DateTimeOffset.Parse(reader.GetString(4)).UtcDateTime, Category = Enum.TryParse<FileCategory>(reader.GetString(5), out var category) ? category : FileCategory.Custom, SourceRoot = Path.GetDirectoryName(reader.GetString(0)) ?? string.Empty });
        }
        return result;
    }
}

public interface IQuickToolsRepository
{
    Task<IReadOnlyList<string>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyList<string> toolIds, CancellationToken cancellationToken = default);
}

public sealed class SqliteQuickToolsRepository(IPixelTartDatabase database) : IQuickToolsRepository
{
    public async Task<IReadOnlyList<string>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<string>(); await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false); await using var command = connection.CreateCommand(); command.CommandText = "SELECT ToolId FROM QuickTools WHERE IsPinned=1 ORDER BY SortOrder;"; await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(reader.GetString(0)); return result;
    }
    public async Task SaveAsync(IReadOnlyList<string> toolIds, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false); await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false); await using (var clear = connection.CreateCommand()) { clear.Transaction = transaction; clear.CommandText = "DELETE FROM QuickTools;"; await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        for (var index = 0; index < toolIds.Count; index++) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "INSERT INTO QuickTools(ToolId,SortOrder,IsPinned,UpdatedAt) VALUES($id,$sort,1,$updated);"; command.Parameters.AddWithValue("$id", toolIds[index]); command.Parameters.AddWithValue("$sort", index); command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O")); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}

public interface IMatchDecisionRepository
{
    Task SaveAsync(Guid projectId, IReadOnlyList<MediaSelectionItem> selections, CancellationToken cancellationToken = default);
}

public sealed class SqliteMatchDecisionRepository(IPixelTartDatabase database) : IMatchDecisionRepository
{
    public async Task SaveAsync(Guid projectId, IReadOnlyList<MediaSelectionItem> selections, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (var clear = connection.CreateCommand()) { clear.Transaction = transaction; clear.CommandText = "DELETE FROM MatchDecisions WHERE ProjectId=$project;"; clear.Parameters.AddWithValue("$project", projectId.ToString("D")); await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        foreach (var selection in selections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selectionId = await FindSelectionIdAsync(connection, transaction, projectId, selection.OriginalInput, cancellationToken).ConfigureAwait(false);
            foreach (var result in selection.FormatResults)
            {
                string? mediaId = null;
                if (result.SelectedFile is not null) mediaId = await EnsureMediaAsync(connection, transaction, projectId, result.SelectedFile, cancellationToken).ConfigureAwait(false);
                await using var command = connection.CreateCommand(); command.Transaction = transaction;
                command.CommandText = "INSERT INTO MatchDecisions(Id,ProjectId,SelectionInputId,Format,Status,SelectedMediaFileId,Reason,IsManual,UpdatedAt) VALUES($id,$project,$selection,$format,$status,$media,$reason,$manual,$updated);";
                command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D")); command.Parameters.AddWithValue("$project", projectId.ToString("D")); command.Parameters.AddWithValue("$selection", selectionId); command.Parameters.AddWithValue("$format", result.Key); command.Parameters.AddWithValue("$status", result.Status.ToString()); command.Parameters.AddWithValue("$media", (object?)mediaId ?? DBNull.Value); command.Parameters.AddWithValue("$reason", result.RecommendedCandidateReason); command.Parameters.AddWithValue("$manual", result.Status == MatchStatus.ManuallyConfirmed ? 1 : 0); command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> FindSelectionIdAsync(SqliteConnection connection, SqliteTransaction transaction, Guid projectId, string input, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "SELECT Id FROM SelectionInputs WHERE ProjectId=$project AND OriginalInput=$input ORDER BY CreatedAt LIMIT 1;"; command.Parameters.AddWithValue("$project", projectId.ToString("D")); command.Parameters.AddWithValue("$input", input); var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false); if (value is string id) return id;
        var created = Guid.NewGuid().ToString("D"); await using var insert = connection.CreateCommand(); insert.Transaction = transaction; insert.CommandText = "INSERT INTO SelectionInputs(Id,ProjectId,OriginalInput,NormalizedName,NumericId,SourceType,CreatedAt) VALUES($id,$project,$input,$normalized,NULL,'Match',$created);"; insert.Parameters.AddWithValue("$id", created); insert.Parameters.AddWithValue("$project", projectId.ToString("D")); insert.Parameters.AddWithValue("$input", input); insert.Parameters.AddWithValue("$normalized", Path.GetFileNameWithoutExtension(input).ToUpperInvariant()); insert.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O")); await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); return created;
    }

    private static async Task<string> EnsureMediaAsync(SqliteConnection connection, SqliteTransaction transaction, Guid projectId, MediaFileRecord media, CancellationToken cancellationToken)
    {
        var normalized = Path.GetFullPath(media.FullPath).ToUpperInvariant(); await using var find = connection.CreateCommand(); find.Transaction = transaction; find.CommandText = "SELECT Id FROM MediaFiles WHERE NormalizedPath=$path LIMIT 1;"; find.Parameters.AddWithValue("$path", normalized); if (await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is string existing) return existing;
        var id = Guid.NewGuid().ToString("D"); await using var insert = connection.CreateCommand(); insert.Transaction = transaction; insert.CommandText = "INSERT INTO MediaFiles(Id,ProjectId,FullPath,NormalizedPath,FileName,Extension,FileSize,ModifiedAt,Format,MetadataState,OptionalHash,IsAvailable) VALUES($id,$project,$path,$normalized,$file,$extension,$size,$modified,$format,$metadata,NULL,$available);"; insert.Parameters.AddWithValue("$id", id); insert.Parameters.AddWithValue("$project", projectId.ToString("D")); insert.Parameters.AddWithValue("$path", media.FullPath); insert.Parameters.AddWithValue("$normalized", normalized); insert.Parameters.AddWithValue("$file", media.FileName); insert.Parameters.AddWithValue("$extension", media.Extension); insert.Parameters.AddWithValue("$size", media.Size); insert.Parameters.AddWithValue("$modified", new DateTimeOffset(media.LastWriteTimeUtc.ToUniversalTime()).ToString("O")); insert.Parameters.AddWithValue("$format", media.Category.ToString()); insert.Parameters.AddWithValue("$metadata", media.JpegQuality is null ? "Unknown" : "Read"); insert.Parameters.AddWithValue("$available", File.Exists(media.FullPath) ? 1 : 0); await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); return id;
    }
}
