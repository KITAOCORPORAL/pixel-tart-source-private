using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Services;

public sealed class MediaIndexService
{
    private readonly FileNameNormalizer _normalizer;
    private readonly ILogService _logService;
    private readonly IRawFileSystem _fileSystem;
    private readonly IJpegMetadataService _jpegMetadataService;
    private readonly IFeatureGateService? _featureGateService;
    private readonly string _cacheFilePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public MediaIndexService(
        FileNameNormalizer normalizer,
        ILogService logService,
        IRawFileSystem? fileSystem = null,
        string? cacheFilePath = null,
        IJpegMetadataService? jpegMetadataService = null,
        IFeatureGateService? featureGateService = null)
    {
        _normalizer = normalizer;
        _logService = logService;
        _fileSystem = fileSystem ?? new SystemRawFileSystem();
        _jpegMetadataService = jpegMetadataService ?? new JpegMetadataService(logService);
        _featureGateService = featureGateService;
        _cacheFilePath = cacheFilePath ?? Path.Combine(AppDataPaths.IndexDirectory, "media-index.json");
        AppDataPaths.EnsureCreated();
    }

    public Task<MediaIndexSnapshot> ScanAsync(
        IEnumerable<string> sourceRoots,
        IEnumerable<string> enabledExtensions,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken) => ScanAsync(
            sourceRoots.Select((path, priority) => new SourceDirectoryEntry
            {
                Path = path,
                DirectoryType = SourceDirectoryType.Mixed,
                Priority = priority
            }),
            enabledExtensions,
            progress,
            cancellationToken);

    public Task<MediaIndexSnapshot> ScanAsync(
        IEnumerable<SourceDirectoryEntry> sourceDirectories,
        IEnumerable<string> enabledExtensions,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken) => Task.Run(async () =>
    {
        var sources = sourceDirectories
            .Where(x => !string.IsNullOrWhiteSpace(x.Path))
            .GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(x => x.Priority)
            .ToList();
        if (_featureGateService is not null && sources.Count > ProjectEntitlementService.FreeSourceDirectoryLimit &&
            !_featureGateService.HasAccess(LicensedFeature.MultipleSourceDirectories))
        {
            throw new InvalidOperationException("免费版最多添加 1 个照片来源目录；现有目录不会被删除。 ");
        }

        var extensions = new HashSet<string>(
            enabledExtensions.Select(MediaExtensionPolicy.NormalizeExtension),
            StringComparer.OrdinalIgnoreCase);
        var standardExtensions = MediaExtensionPolicy.DefaultJpegExtensions
            .Concat(MediaExtensionPolicy.DefaultRawExtensions)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (_featureGateService is not null && extensions.Any(extension => !standardExtensions.Contains(extension)) &&
            !_featureGateService.HasAccess(LicensedFeature.CustomFileFormats))
        {
            throw new InvalidOperationException("自定义文件格式是专业版功能；免费版仍可扫描 JPG 和 RAW。 ");
        }
        var snapshotFiles = new List<MediaFileRecord>();
        var indexedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long scanned = 0;
        var skippedDirectories = 0;

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var root = source.Path;
            if (!_fileSystem.DirectoryExists(root))
            {
                skippedDirectories++;
                _logService.Error($"照片来源目录不存在或无法访问：{root}");
                continue;
            }

            var fullRoot = Path.GetFullPath(root);
            var stack = new Stack<string>();
            stack.Push(fullRoot);
            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = stack.Pop();
                progress?.Report(new OperationProgress("扫描照片文件", directory, scanned));
                foreach (var child in TryEnumerate(() => _fileSystem.EnumerateDirectories(directory), directory, ref skippedDirectories))
                {
                    stack.Push(child);
                }

                foreach (var filePath in TryEnumerate(() => _fileSystem.EnumerateFiles(directory), directory, ref skippedDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    scanned++;
                    var extension = MediaExtensionPolicy.NormalizeExtension(Path.GetExtension(filePath));
                    if (!extensions.Contains(extension) || !IsEnabledForSourceType(extension, source.DirectoryType) || !indexedPaths.Add(filePath)) continue;
                    try
                    {
                        var record = MediaFileRecord.FromFile(filePath, fullRoot, _normalizer, extensions);
                        record.SourceDirectoryType = source.DirectoryType;
                        record.SourcePriority = source.Priority;
                        if (record.Category == FileCategory.Jpeg)
                        {
                            record.JpegQuality = _jpegMetadataService.Read(filePath);
                            new JpegQualityAssessmentService().Assess(record.JpegQuality, record.FileName);
                        }
                        snapshotFiles.Add(record);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException or PathTooLongException)
                    {
                        _logService.Error($"无法读取照片文件信息：{filePath}", ex);
                    }
                }
            }
        }

        var snapshot = MediaIndexSnapshot.Create(snapshotFiles);
        snapshot.CreatedAtUtc = DateTime.UtcNow;
        snapshot.SkippedDirectoryCount = skippedDirectories;
        progress?.Report(new OperationProgress("扫描完成", $"已索引 {snapshot.Files.Count:N0} 个照片文件", scanned, scanned, 100));
        if (CanUsePersistentIndex)
        {
            await SaveCacheAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
        return snapshot;
    }, cancellationToken);

    private static bool IsEnabledForSourceType(string extension, SourceDirectoryType sourceType)
    {
        var category = MediaExtensionPolicy.Classify(extension, []);
        return sourceType switch
        {
            SourceDirectoryType.Jpeg => category == FileCategory.Jpeg,
            SourceDirectoryType.Raw => category == FileCategory.Raw,
            SourceDirectoryType.Other => category is not FileCategory.Jpeg and not FileCategory.Raw,
            _ => true
        };
    }

    public async Task<MediaIndexSnapshot?> LoadCacheAsync(CancellationToken cancellationToken = default)
    {
        if (!CanUsePersistentIndex) return null;
        try
        {
            if (!File.Exists(_cacheFilePath)) return null;
            await using var stream = File.OpenRead(_cacheFilePath);
            var files = await JsonSerializer.DeserializeAsync<List<MediaFileRecord>>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
            return files is null ? null : MediaIndexSnapshot.Create(files);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logService.Error("照片文件索引缓存损坏或无法读取，将忽略缓存。", ex);
            return null;
        }
    }

    private bool CanUsePersistentIndex => _featureGateService is null ||
        _featureGateService.HasAccess(LicensedFeature.PersistentFileIndex);

    private async Task SaveCacheAsync(MediaIndexSnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cacheFilePath)!);
            var temporary = _cacheFilePath + ".tmp";
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot.Files, _jsonOptions, cancellationToken).ConfigureAwait(false);
            }
            File.Move(temporary, _cacheFilePath, true);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logService.Error("无法保存照片文件索引缓存。", ex);
        }
    }

    private IReadOnlyList<string> TryEnumerate(Func<IEnumerable<string>> action, string directory, ref int skippedDirectories)
    {
        try { return action().ToArray(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException)
        {
            skippedDirectories++;
            _logService.Error($"跳过无法访问的照片目录：{directory}", ex);
            return [];
        }
    }
}
