using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Services.Database;

public sealed record JsonMigrationItemResult(string SourceFile, string EntityType, int ReadCount, int ImportedCount, bool Success, string? Error);
public sealed record JsonMigrationReport(bool AlreadyCompleted, bool Success, string? BackupDirectory, IReadOnlyList<JsonMigrationItemResult> Items, DateTimeOffset CompletedAt);

public interface IJsonDataMigrationService
{
    Task<JsonMigrationReport> MigrateAsync(CancellationToken cancellationToken = default);
}

public sealed class JsonDataMigrationService(IPixelTartDatabase database, string? root = null, string? backupRoot = null) : IJsonDataMigrationService
{
    private readonly string _root = root ?? AppDataPaths.Root;
    private readonly string _backupRoot = backupRoot ?? AppDataPaths.MigrationBackupDirectory;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public async Task<JsonMigrationReport> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var marker = Path.Combine(Path.GetDirectoryName(database.DatabasePath)!, "json-migration-v1.completed.json");
        if (File.Exists(marker))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<JsonMigrationReport>(await File.ReadAllTextAsync(marker, cancellationToken).ConfigureAwait(false), _json);
                if (existing is not null) return existing with { AlreadyCompleted = true };
            }
            catch (JsonException) { }
        }

        var candidates = DiscoverFiles().Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var backupDirectory = Path.Combine(_backupRoot, DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        if (candidates.Length > 0) Directory.CreateDirectory(backupDirectory);
        foreach (var file in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(_root, file).Replace(':', '_');
            var destination = Path.Combine(backupDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, false);
        }

        var results = new List<JsonMigrationItemResult>();
        foreach (var file in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var name = Path.GetFileName(file);
                JsonMigrationItemResult result;
                if (name.Equals("projects.json", StringComparison.OrdinalIgnoreCase)) result = await ImportProjectsAsync(file, cancellationToken).ConfigureAwait(false);
                else if (name.Equals("settings.json", StringComparison.OrdinalIgnoreCase)) result = await ImportQuickToolsAsync(file, cancellationToken).ConfigureAwait(false);
                else if (name.Equals("media-index.json", StringComparison.OrdinalIgnoreCase)) result = await ImportMediaAsync(file, cancellationToken).ConfigureAwait(false);
                else if (name.Equals("raw-index.json", StringComparison.OrdinalIgnoreCase)) result = await ImportRawIndexAsync(file, cancellationToken).ConfigureAwait(false);
                else continue;
                results.Add(result);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or SqliteException)
            {
                results.Add(new(file, Classify(file), 0, 0, false, ex.Message));
            }
        }

        var report = new JsonMigrationReport(false, results.All(x => x.Success), candidates.Length == 0 ? null : backupDirectory, results, DateTimeOffset.UtcNow);
        Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
        await File.WriteAllTextAsync(marker, JsonSerializer.Serialize(report, _json), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(marker)!, "json-migration-v1-report.json"), JsonSerializer.Serialize(report, _json), cancellationToken).ConfigureAwait(false);
        return report;
    }

    private IEnumerable<string> DiscoverFiles()
    {
        yield return Path.Combine(_root, "Projects", "projects.json");
        yield return Path.Combine(_root, "settings.json");
        yield return Path.Combine(_root, "Indexes", "media-index.json");
        yield return Path.Combine(_root, "Indexes", "raw-index.json");
        var legacy = _root.Replace("KitaoPhotoSelector", "RAWSelectionAssistant", StringComparison.OrdinalIgnoreCase);
        if (!string.Equals(legacy, _root, StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(legacy, "Projects", "projects.json");
            yield return Path.Combine(legacy, "settings.json");
            yield return Path.Combine(legacy, "Indexes", "media-index.json");
            yield return Path.Combine(legacy, "Indexes", "raw-index.json");
        }
    }

    private async Task<JsonMigrationItemResult> ImportProjectsAsync(string file, CancellationToken cancellationToken)
    {
        await using var input = File.OpenRead(file);
        var projects = await JsonSerializer.DeserializeAsync<List<PhotoProjectRecord>>(input, _json, cancellationToken).ConfigureAwait(false) ?? [];
        var imported = 0;
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var project in projects)
        {
            await ExecuteAsync(connection, transaction, """
                INSERT OR IGNORE INTO Projects(Id,Name,ProjectType,WorkflowState,RootPath,CreatedAt,UpdatedAt,LastOpenedAt,IsArchived)
                VALUES($id,$name,$type,$state,$root,$created,$updated,$opened,0);
                """, cancellationToken,
                ("$id", project.Id.ToString("D")), ("$name", project.Name), ("$type", project.Category.ToString()), ("$state", project.Status.ToString()),
                ("$root", Db(project.OutputDirectory)), ("$created", project.CreatedAt.ToString("O")), ("$updated", project.UpdatedAt.ToString("O")), ("$opened", project.UpdatedAt.ToString("O"))).ConfigureAwait(false);
            imported++;
            for (var index = 0; index < project.SourceDirectories.Count; index++)
            {
                var path = project.SourceDirectories[index];
                await ExecuteAsync(connection, transaction, "INSERT OR IGNORE INTO ProjectSources(Id,ProjectId,Path,Purpose,Priority,IsAvailable,LastCheckedAt) VALUES($id,$project,$path,'Mixed',$priority,$available,$checked);", cancellationToken,
                    ("$id", StableGuid($"source|{project.Id}|{path}").ToString("D")), ("$project", project.Id.ToString("D")), ("$path", path), ("$priority", index), ("$available", Directory.Exists(path) ? 1 : 0), ("$checked", DateTimeOffset.UtcNow.ToString("O"))).ConfigureAwait(false);
            }
            foreach (var selection in project.SelectionInputs)
            {
                var digits = new string(selection.Where(char.IsDigit).ToArray());
                await ExecuteAsync(connection, transaction, "INSERT OR IGNORE INTO SelectionInputs(Id,ProjectId,OriginalInput,NormalizedName,NumericId,SourceType,CreatedAt) VALUES($id,$project,$input,$normalized,$numeric,'LegacyJson',$created);", cancellationToken,
                    ("$id", StableGuid($"selection|{project.Id}|{selection}").ToString("D")), ("$project", project.Id.ToString("D")), ("$input", selection), ("$normalized", Path.GetFileNameWithoutExtension(selection).ToUpperInvariant()), ("$numeric", long.TryParse(digits, out var number) ? number : DBNull.Value), ("$created", project.CreatedAt.ToString("O"))).ConfigureAwait(false);
            }
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(file, "Projects", projects.Count, imported, projects.Count == imported, null);
    }

    private async Task<JsonMigrationItemResult> ImportQuickToolsAsync(string file, CancellationToken cancellationToken)
    {
        await using var input = File.OpenRead(file);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(input, _json, cancellationToken).ConfigureAwait(false) ?? new AppSettings();
        var tools = QuickToolsService.Normalize(settings.QuickToolLayout.OrderedToolIds.Count > 0 ? settings.QuickToolLayout.OrderedToolIds : settings.PinnedQuickTools);
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        for (var index = 0; index < tools.Count; index++)
            await ExecuteAsync(connection, transaction, "INSERT INTO QuickTools(ToolId,SortOrder,IsPinned,UpdatedAt) VALUES($id,$sort,1,$updated) ON CONFLICT(ToolId) DO UPDATE SET SortOrder=excluded.SortOrder,IsPinned=1,UpdatedAt=excluded.UpdatedAt;", cancellationToken,
                ("$id", tools[index]), ("$sort", index), ("$updated", DateTimeOffset.UtcNow.ToString("O"))).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(file, "QuickTools", tools.Count, tools.Count, true, null);
    }

    private async Task<JsonMigrationItemResult> ImportMediaAsync(string file, CancellationToken cancellationToken)
    {
        await using var input = File.OpenRead(file);
        var media = await JsonSerializer.DeserializeAsync<List<MediaFileRecord>>(input, _json, cancellationToken).ConfigureAwait(false) ?? [];
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var imported = 0;
        foreach (var item in media)
        {
            await InsertMediaAsync(connection, transaction, item.FullPath, item.FileName, item.Extension, item.Size, item.LastWriteTimeUtc, item.Category.ToString(), cancellationToken).ConfigureAwait(false);
            imported++;
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(file, "MediaFiles", media.Count, imported, media.Count == imported, null);
    }

    private async Task<JsonMigrationItemResult> ImportRawIndexAsync(string file, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false));
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("RAW index root is not an array.");
        var count = 0;
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (!element.TryGetProperty("FullPath", out var pathProperty)) continue;
            var path = pathProperty.GetString();
            if (string.IsNullOrWhiteSpace(path)) continue;
            var size = element.TryGetProperty("Size", out var sizeProperty) && sizeProperty.TryGetInt64(out var parsedSize) ? parsedSize : 0;
            var modified = element.TryGetProperty("LastWriteTimeUtc", out var modifiedProperty) && modifiedProperty.TryGetDateTime(out var parsedModified) ? parsedModified : DateTime.UtcNow;
            await InsertMediaAsync(connection, transaction, path, Path.GetFileName(path), Path.GetExtension(path), size, modified, "Raw", cancellationToken).ConfigureAwait(false);
            count++;
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(file, "MediaFiles", count, count, true, null);
    }

    private static Task<int> InsertMediaAsync(SqliteConnection connection, SqliteTransaction transaction, string path, string fileName, string extension, long size, DateTime modified, string format, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction, """
            INSERT OR IGNORE INTO MediaFiles(Id,ProjectId,FullPath,NormalizedPath,FileName,Extension,FileSize,ModifiedAt,Format,MetadataState,OptionalHash,IsAvailable)
            VALUES($id,NULL,$path,$normalized,$file,$extension,$size,$modified,$format,'LegacyJson',NULL,$available);
            """, cancellationToken, ("$id", StableGuid("media|" + Path.GetFullPath(path).ToUpperInvariant()).ToString("D")), ("$path", path), ("$normalized", Path.GetFullPath(path).ToUpperInvariant()), ("$file", fileName), ("$extension", extension), ("$size", size), ("$modified", new DateTimeOffset(modified.ToUniversalTime()).ToString("O")), ("$format", format), ("$available", File.Exists(path) ? 1 : 0));

    private static async Task<int> ExecuteAsync(SqliteConnection connection, SqliteTransaction transaction, string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] values)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var value in values) command.Parameters.AddWithValue(value.Name, value.Value ?? DBNull.Value);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Guid StableGuid(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes);
    }
    private static object Db(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    private static string Classify(string file) => Path.GetFileName(file).ToLowerInvariant() switch { "projects.json" => "Projects", "settings.json" => "QuickTools", _ => "MediaFiles" };
}
