using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Services;

public sealed class RawIndexService
{
    public static readonly string[] DefaultExtensions =
    [
        ".ARW", ".CR2", ".CR3", ".NEF", ".NRW", ".RAF", ".DNG", ".RW2",
        ".ORF", ".ORI", ".PEF", ".3FR", ".FFF", ".IIQ", ".SRW", ".RWL"
    ];

    private readonly FileNameNormalizer _normalizer;
    private readonly ILogService _logService;
    private readonly IRawFileSystem _fileSystem;
    private readonly string _cacheFilePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public RawIndexService(
        FileNameNormalizer normalizer,
        ILogService logService,
        IRawFileSystem? fileSystem = null,
        string? cacheFilePath = null)
    {
        _normalizer = normalizer;
        _logService = logService;
        _fileSystem = fileSystem ?? new SystemRawFileSystem();
        _cacheFilePath = cacheFilePath ?? AppDataPaths.IndexFile;
        AppDataPaths.EnsureCreated();
    }

    public Task<RawIndexSnapshot> ScanAsync(
        IEnumerable<string> sourceRoots,
        IEnumerable<string>? customExtensions,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken) => Task.Run(async () =>
    {
        var extensions = new HashSet<string>(DefaultExtensions, StringComparer.OrdinalIgnoreCase);
        foreach (var extension in customExtensions ?? [])
        {
            var value = extension.Trim();
            if (value.Length > 0)
            {
                extensions.Add(value.StartsWith('.') ? value : $".{value}");
            }
        }

        var snapshot = new RawIndexSnapshot();
        var indexedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long scanned = 0;

        foreach (var root in sourceRoots.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_fileSystem.DirectoryExists(root))
            {
                snapshot.SkippedDirectoryCount++;
                _logService.Error($"RAW 来源目录不存在或无法访问：{root}");
                continue;
            }

            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = stack.Pop();
                progress?.Report(new OperationProgress("扫描 RAW", directory, scanned));

                foreach (var child in TryEnumerate(() => _fileSystem.EnumerateDirectories(directory), directory, snapshot))
                {
                    stack.Push(child);
                }

                foreach (var filePath in TryEnumerate(() => _fileSystem.EnumerateFiles(directory), directory, snapshot))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    scanned++;
                    if (!extensions.Contains(Path.GetExtension(filePath)) || !indexedPaths.Add(filePath))
                    {
                        continue;
                    }

                    try
                    {
                        var info = _fileSystem.GetFileInfo(filePath);
                        var normalized = _normalizer.Normalize(info.Name);
                        var entry = new RawFileEntry
                        {
                            FullPath = info.FullName,
                            FileName = info.Name,
                            Extension = info.Extension,
                            Size = info.Length,
                            LastWriteTimeUtc = info.LastWriteTimeUtc,
                            SourceRoot = Path.GetFullPath(root),
                            NormalizedName = normalized.ComparisonName,
                            NumericId = normalized.NumericId
                        };
                        snapshot.Files.Add(entry);
                        AddLookup(snapshot.ByFullName, entry.NormalizedName, entry);
                        if (entry.NumericId.Length > 0)
                        {
                            AddLookup(snapshot.ByNumericId, entry.NumericId, entry);
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException or PathTooLongException)
                    {
                        _logService.Error($"无法读取 RAW 文件信息：{filePath}", ex);
                    }
                }
            }
        }

        snapshot.CreatedAtUtc = DateTime.UtcNow;
        progress?.Report(new OperationProgress("扫描完成", $"已索引 {snapshot.Files.Count:N0} 个 RAW 文件", scanned, scanned, 100));
        await SaveCacheAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return snapshot;
    }, cancellationToken);

    public async Task<RawIndexSnapshot?> LoadCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_cacheFilePath))
            {
                return null;
            }

            await using var stream = File.OpenRead(_cacheFilePath);
            var files = await JsonSerializer.DeserializeAsync<List<RawFileEntry>>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
            if (files is null)
            {
                return null;
            }

            var snapshot = new RawIndexSnapshot { CreatedAtUtc = File.GetLastWriteTimeUtc(_cacheFilePath) };
            foreach (var entry in files)
            {
                snapshot.Files.Add(entry);
                AddLookup(snapshot.ByFullName, entry.NormalizedName, entry);
                if (entry.NumericId.Length > 0)
                {
                    AddLookup(snapshot.ByNumericId, entry.NumericId, entry);
                }
            }

            return snapshot;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logService.Error("RAW 索引缓存损坏或无法读取，将忽略缓存。", ex);
            return null;
        }
    }

    private async Task SaveCacheAsync(RawIndexSnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cacheFilePath)!);
            var temporaryPath = _cacheFilePath + ".tmp";
            await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot.Files, _jsonOptions, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _cacheFilePath, true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logService.Error("无法保存 RAW 索引缓存。", ex);
        }
    }

    private IReadOnlyList<string> TryEnumerate(
        Func<IEnumerable<string>> action,
        string directory,
        RawIndexSnapshot snapshot)
    {
        try
        {
            return action().ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException)
        {
            snapshot.SkippedDirectoryCount++;
            _logService.Error($"跳过无法访问的 RAW 目录：{directory}", ex);
            return [];
        }
    }

    private static void AddLookup(Dictionary<string, List<RawFileEntry>> lookup, string key, RawFileEntry entry)
    {
        if (!lookup.TryGetValue(key, out var entries))
        {
            entries = [];
            lookup[key] = entries;
        }

        entries.Add(entry);
    }
}
