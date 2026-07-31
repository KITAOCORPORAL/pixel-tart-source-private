using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class RawCopyService(ILogService logService)
{
    private const int BufferSize = 1024 * 1024;

    public Task<CopySummary> CopyAsync(
        IEnumerable<SelectionItem> selectionItems,
        string outputDirectory,
        OutputMode outputMode,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken) => Task.Run(async () =>
    {
        var allItems = selectionItems.ToList();
        if (allItems.Any(x => x.IsSelected && x.Status == MatchStatus.Conflict))
        {
            throw new InvalidOperationException("仍有未解决的 RAW 冲突，请先选择正确的候选文件。");
        }

        var copyItems = allItems
            .Where(x => x.IsSelected && x.SelectedRaw is not null && x.Status is MatchStatus.Matched or MatchStatus.ManuallyConfirmed)
            .GroupBy(x => x.SelectedRaw!.FullPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (copyItems.Count == 0)
        {
            throw new InvalidOperationException("没有可复制的已匹配 RAW 文件。");
        }

        Directory.CreateDirectory(outputDirectory);
        EnsureWritable(outputDirectory);
        EnsureFreeSpace(outputDirectory, copyItems.Sum(x => x.SelectedRaw!.Size));

        var summary = new CopySummary();
        for (var index = 0; index < copyItems.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = copyItems[index];
            var source = item.SelectedRaw!;
            var destination = BuildDestinationPath(outputDirectory, source, outputMode);
            var operationTime = DateTime.Now;
            var destinationCreated = false;

            try
            {
                if (!File.Exists(source.FullPath))
                {
                    throw new FileNotFoundException("源 RAW 文件不存在，可能已被移动或磁盘已断开。", source.FullPath);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (File.Exists(destination))
                {
                    if (string.Equals(Path.GetFullPath(source.FullPath), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase) &&
                        new FileInfo(destination).Length == source.Size)
                    {
                        summary.Outcomes.Add(new CopyOutcome(item.Id, source.FullPath, destination, MatchStatus.Skipped, "目标就是同一文件，已跳过。", operationTime));
                        continue;
                    }

                    destination = GetAvailablePath(destination);
                }

                progress?.Report(new OperationProgress(
                    "复制 RAW",
                    source.FileName,
                    index,
                    copyItems.Count,
                    index * 100d / copyItems.Count));

                await CopyFileAsync(source.FullPath, destination, () => destinationCreated = true, cancellationToken).ConfigureAwait(false);
                File.SetLastWriteTimeUtc(destination, source.LastWriteTimeUtc);
                summary.Outcomes.Add(new CopyOutcome(item.Id, source.FullPath, destination, MatchStatus.Copied, string.Empty, operationTime));
            }
            catch (OperationCanceledException)
            {
                if (destinationCreated) TryDeletePartial(destination);
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or FileNotFoundException or PathTooLongException)
            {
                var friendly = ToFriendlyMessage(ex);
                logService.Error($"复制 RAW 失败：{source.FullPath}", ex);
                summary.Outcomes.Add(new CopyOutcome(item.Id, source.FullPath, destination, MatchStatus.CopyFailed, friendly, operationTime));
                if (destinationCreated) TryDeletePartial(destination);
            }
        }

        progress?.Report(new OperationProgress("复制完成", $"成功 {summary.CopiedCount}，跳过 {summary.SkippedCount}，失败 {summary.FailedCount}", copyItems.Count, copyItems.Count, 100));
        return summary;
    }, cancellationToken);

    private static async Task CopyFileAsync(string source, string destination, Action onDestinationCreated, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        onDestinationCreated();
        await input.CopyToAsync(output, BufferSize, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string BuildDestinationPath(string outputDirectory, RawFileEntry source, OutputMode outputMode)
    {
        if (outputMode == OutputMode.Flat)
        {
            return Path.Combine(outputDirectory, source.FileName);
        }

        var relative = Path.GetRelativePath(source.SourceRoot, source.FullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            relative = source.FileName;
        }

        var candidate = Path.GetFullPath(Path.Combine(outputDirectory, relative));
        var root = Path.GetFullPath(outputDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? candidate
            : Path.Combine(outputDirectory, source.FileName);
    }

    private static string GetAvailablePath(string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidate = Path.Combine(directory, $"{stem}_{suffix}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("无法生成可用的安全文件名。");
    }

    private static void EnsureWritable(string outputDirectory)
    {
        var probe = Path.Combine(outputDirectory, $".write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException("输出目录不可写，请选择其他目录。", ex);
        }
    }

    private static void EnsureFreeSpace(string outputDirectory, long requiredBytes)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(outputDirectory));
            if (!string.IsNullOrWhiteSpace(root))
            {
                var drive = new DriveInfo(root);
                if (drive.IsReady && drive.AvailableFreeSpace < requiredBytes)
                {
                    throw new IOException("磁盘剩余空间不足，无法复制全部 RAW 文件。");
                }
            }
        }
        catch (IOException)
        {
            throw;
        }
        catch
        {
            // Some network paths cannot report free space; per-file errors remain protected.
        }
    }

    private static string ToFriendlyMessage(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "没有访问权限，请检查源文件和输出目录权限。",
        FileNotFoundException => "源 RAW 文件不存在或磁盘已断开。",
        DirectoryNotFoundException => "目录不存在或暂时无法访问。",
        PathTooLongException => "文件路径过长，无法完成复制。",
        IOException => "文件正在使用、磁盘空间不足或存储设备不可用。",
        _ => "复制失败。"
    };

    private static void TryDeletePartial(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // A partial output is logged by the caller; source files are never touched.
        }
    }
}
