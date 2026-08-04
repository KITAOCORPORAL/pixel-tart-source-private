using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Database;

namespace RAWSelectionAssistant.Tests;

internal sealed class TestLogService : ILogService
{
    public List<string> Messages { get; } = [];
    public void Info(string message) => Messages.Add(message);
    public void Error(string message, Exception? exception = null) => Messages.Add($"{message}|{exception?.GetType().Name}");
}

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory(string? name = null)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RAWSelectionAssistant.Tests", name ?? Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }
    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public string CreateFile(string relativePath, byte[]? content = null)
    {
        var path = Combine(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content ?? []);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, true); } catch { }
    }
}

internal static class SqliteTestIsolation
{
    public static void ClearPool(PixelTartDatabase database)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = database.DatabasePath,
            Mode = database.IsReadOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            DefaultTimeout = 5
        };
        using var connection = new SqliteConnection(builder.ToString());
        SqliteConnection.ClearPool(connection);
    }
}

internal static class IndexFactory
{
    public static RawFileEntry Entry(string path, string sourceRoot, FileNameNormalizer normalizer)
    {
        var info = new FileInfo(path);
        var normalized = normalizer.Normalize(info.Name);
        return new RawFileEntry
        {
            FullPath = info.FullName,
            FileName = info.Name,
            Extension = info.Extension,
            Size = info.Exists ? info.Length : 0,
            LastWriteTimeUtc = info.Exists ? info.LastWriteTimeUtc : DateTime.UtcNow,
            SourceRoot = sourceRoot,
            NormalizedName = normalized.ComparisonName,
            NumericId = normalized.NumericId
        };
    }

    public static RawIndexSnapshot Snapshot(params RawFileEntry[] entries)
    {
        var result = new RawIndexSnapshot();
        foreach (var entry in entries)
        {
            result.Files.Add(entry);
            Add(result.ByFullName, entry.NormalizedName, entry);
            if (entry.NumericId.Length > 0) Add(result.ByNumericId, entry.NumericId, entry);
        }
        return result;
    }

    private static void Add(Dictionary<string, List<RawFileEntry>> map, string key, RawFileEntry entry)
    {
        if (!map.TryGetValue(key, out var list)) map[key] = list = [];
        list.Add(entry);
    }
}
