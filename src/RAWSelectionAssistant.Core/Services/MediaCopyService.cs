using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.FileOperations;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.Core.Services;

public sealed class MediaCopyService
{
    private const int BufferSize = 1024 * 1024;
    private readonly ILogService _logService;
    private readonly IFileOperationExecutor? _fileOperationExecutor;
    private readonly IFileConflictResolver? _fileConflictResolver;

    public MediaCopyService(ILogService logService, IFileOperationExecutor? fileOperationExecutor = null, IFileConflictResolver? fileConflictResolver = null)
    {
        _logService = logService;
        _fileOperationExecutor = fileOperationExecutor;
        _fileConflictResolver = fileConflictResolver;
    }

    public Task<MediaCopySummary> CopyAsync(
        IEnumerable<MediaSelectionItem> selectionItems,
        string outputDirectory,
        OutputMode outputMode,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken) => Task.Run(async () =>
    {
        var copyEntries = selectionItems
            .Where(item => item.IsSelected)
            .SelectMany(item => item.FormatResults
                .Where(result => result.SelectedFile is not null && result.Status is MatchStatus.Matched or MatchStatus.ManuallyConfirmed)
                .Select(result => new CopyEntry(item, result, result.SelectedFile!)))
            .GroupBy(entry => entry.File.FullPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        if (copyEntries.Count == 0) throw new InvalidOperationException("没有可复制的已匹配文件。仍有冲突的格式不会被复制。");

        if (_fileOperationExecutor is not null && _fileConflictResolver is not null)
            return await CopyWithPlanAsync(copyEntries, outputDirectory, outputMode, progress, cancellationToken).ConfigureAwait(false);

        Directory.CreateDirectory(outputDirectory);
        EnsureWritable(outputDirectory);
        EnsureFreeSpace(outputDirectory, copyEntries.Sum(x => x.File.Size));
        var summary = new MediaCopySummary();
        for (var index = 0; index < copyEntries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = copyEntries[index];
            var destination = BuildDestinationPath(outputDirectory, entry.File, outputMode);
            var destinationCreated = false;
            var operationTime = DateTime.Now;
            try
            {
                if (!File.Exists(entry.File.FullPath)) throw new FileNotFoundException("源文件不存在，可能已被移动或存储设备已断开。", entry.File.FullPath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (File.Exists(destination)) destination = GetAvailablePath(destination);
                progress?.Report(new OperationProgress("复制已匹配文件", entry.File.FileName, index, copyEntries.Count, index * 100d / copyEntries.Count));
                await using var input = new FileStream(entry.File.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
                await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
                destinationCreated = true;
                await input.CopyToAsync(output, BufferSize, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                File.SetLastWriteTimeUtc(destination, entry.File.LastWriteTimeUtc);
                summary.Outcomes.Add(new MediaCopyOutcome(entry.Item.Id, entry.Result.Key, entry.File.FullPath, destination, MatchStatus.Copied, string.Empty, operationTime));
            }
            catch (OperationCanceledException)
            {
                if (destinationCreated) TryDelete(destination);
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or FileNotFoundException or PathTooLongException)
            {
                _logService.Error($"复制照片文件失败：{entry.File.FullPath}", ex);
                summary.Outcomes.Add(new MediaCopyOutcome(entry.Item.Id, entry.Result.Key, entry.File.FullPath, destination, MatchStatus.CopyFailed, Friendly(ex), operationTime));
                if (destinationCreated) TryDelete(destination);
            }
        }
        progress?.Report(new OperationProgress("复制完成", $"成功 {summary.CopiedCount}，失败 {summary.FailedCount}", copyEntries.Count, copyEntries.Count, 100));
        return summary;
    }, cancellationToken);

    private async Task<MediaCopySummary> CopyWithPlanAsync(IReadOnlyList<CopyEntry> copyEntries, string outputDirectory, OutputMode outputMode, IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<FileOperationItem>(copyEntries.Count);
        for (var index = 0; index < copyEntries.Count; index++)
        {
            var entry = copyEntries[index];
            var desired = BuildDestinationPath(outputDirectory, entry.File, outputMode);
            var destination = _fileConflictResolver!.ResolveDestination(desired, FileConflictPolicy.AutoNumber, reserved);
            reserved.Add(destination);
            items.Add(new FileOperationItem(Guid.NewGuid(), index, entry.File.FullPath, destination, FileOperationType.Copy, FileConflictPolicy.AutoNumber, entry.File.Size, entry.File.LastWriteTimeUtc));
        }
        var taskId = TaskExecutionAmbient.CurrentTaskId.Value ?? Guid.NewGuid();
        var commonRoot = copyEntries.Select(x => Path.GetFullPath(x.File.SourceRoot)).OrderBy(x => x.Length).FirstOrDefault() ?? Path.GetDirectoryName(copyEntries[0].File.FullPath)!;
        var plan = new FileOperationPlan(1, Guid.NewGuid(), taskId, null, FileOperationType.Copy, commonRoot, Path.GetFullPath(outputDirectory), FileConflictPolicy.AutoNumber, items, copyEntries.Sum(x => x.File.Size), FileOperationRiskLevel.Low, DateTimeOffset.UtcNow);
        var ambientContext = TaskExecutionAmbient.CurrentContext.Value;
        var bridgeProgress = new Progress<(double Progress, string CurrentFile, TaskResultSummary Summary)>(value =>
        {
            progress?.Report(new OperationProgress("复制已匹配文件", value.CurrentFile, value.Summary.Succeeded + value.Summary.Failed, value.Summary.Total, value.Progress));
            if (ambientContext is not null) _ = ambientContext.ReportProgressAsync(value.Progress, "复制已匹配文件", value.CurrentFile, value.Summary, cancellationToken);
        });
        var execution = await _fileOperationExecutor!.ExecuteAsync(plan,
            safeBoundary: ambientContext is null ? null : ambientContext.SafeBoundaryAsync,
            progress: bridgeProgress,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var byId = execution.Items.ToDictionary(x => x.ItemId);
        var summary = new MediaCopySummary();
        for (var index = 0; index < copyEntries.Count; index++)
        {
            var entry = copyEntries[index];
            var item = items[index];
            if (byId.TryGetValue(item.Id, out var result) && result.State == FileOperationItemState.Completed)
                summary.Outcomes.Add(new MediaCopyOutcome(entry.Item.Id, entry.Result.Key, entry.File.FullPath, result.DestinationPath ?? item.DestinationPath, MatchStatus.Copied, string.Empty, DateTime.Now));
            else
                summary.Outcomes.Add(new MediaCopyOutcome(entry.Item.Id, entry.Result.Key, entry.File.FullPath, item.DestinationPath, MatchStatus.CopyFailed, byId.TryGetValue(item.Id, out var failed) ? failed.ErrorMessage ?? ErrorCodeCatalog.Describe(failed.ErrorCode) : "文件未完成复制。", DateTime.Now));
        }
        return summary;
    }

    private static string BuildDestinationPath(string root, MediaFileRecord file, OutputMode mode)
    {
        if (mode == OutputMode.Flat) return Path.Combine(root, file.FileName);
        if (mode == OutputMode.ByFileCategory)
        {
            var directory = file.Category switch { FileCategory.Jpeg => "JPG", FileCategory.Raw => "RAW", _ => "OTHER" };
            return Path.Combine(root, directory, file.FileName);
        }
        var relative = Path.GetRelativePath(file.SourceRoot, file.FullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)) relative = file.FileName;
        var destination = Path.GetFullPath(Path.Combine(root, relative));
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return destination.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) ? destination : Path.Combine(root, file.FileName);
    }

    private static string GetAvailablePath(string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidate = Path.Combine(directory, $"{stem}_{suffix}{extension}");
            if (!File.Exists(candidate)) return candidate;
        }
        throw new IOException("无法生成可用的安全文件名。");
    }

    private static void EnsureWritable(string outputDirectory)
    {
        var probe = Path.Combine(outputDirectory, $".write-test-{Guid.NewGuid():N}.tmp");
        try { using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { } }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new UnauthorizedAccessException("输出目录不可写。", ex); }
    }

    private static void EnsureFreeSpace(string outputDirectory, long requiredBytes)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(outputDirectory));
            if (!string.IsNullOrWhiteSpace(root))
            {
                var drive = new DriveInfo(root);
                if (drive.IsReady && drive.AvailableFreeSpace < requiredBytes) throw new IOException("磁盘剩余空间不足。");
            }
        }
        catch (IOException) { throw; }
        catch { }
    }

    private static string Friendly(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "没有访问权限，请检查源文件和输出目录权限。",
        FileNotFoundException => "源文件不存在或存储设备已断开。",
        DirectoryNotFoundException => "目录不存在或暂时无法访问。",
        PathTooLongException => "文件路径过长。",
        _ => "文件正在使用、磁盘空间不足或存储设备不可用。"
    };

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    private sealed record CopyEntry(MediaSelectionItem Item, MediaFormatMatchResult Result, MediaFileRecord File);
}
