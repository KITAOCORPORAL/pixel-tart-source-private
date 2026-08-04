using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.FileOperations;

public sealed class FileOperationValidator : IFileOperationValidator
{
    public Task<FileOperationValidationResult> ValidateAsync(FileOperationPlan plan, CancellationToken cancellationToken = default)
    {
        var issues = new List<FileOperationValidationIssue>();
        var sourceRoot = Normalize(plan.SourceRoot);
        var destinationRoot = Normalize(plan.DestinationRoot);
        if (string.Equals(sourceRoot, destinationRoot, StringComparison.OrdinalIgnoreCase))
            issues.Add(new(ErrorCodeCatalog.SourceAndDestinationSame, "来源与目标不能是同一位置。"));
        if (IsInside(destinationRoot, sourceRoot))
            issues.Add(new(ErrorCodeCatalog.DestinationInsideSource, "目标目录不能位于来源目录内部。"));
        if (IsForbiddenDestination(destinationRoot))
            issues.Add(new(ErrorCodeCatalog.DestinationNotWritable, "不能将文件写入 Windows 系统保护目录。"));

        long bytes = 0;
        foreach (var item in plan.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(item.SourcePath))
            {
                issues.Add(new(ErrorCodeCatalog.SourceNotFound, "源文件不存在。", item.Id));
                continue;
            }
            var info = new FileInfo(item.SourcePath);
            bytes += info.Length;
            if (item.ExpectedSourceSize is long expected && expected != info.Length)
                issues.Add(new(ErrorCodeCatalog.SourceChanged, "源文件大小已改变。", item.Id, true));
            if (item.ExpectedSourceModifiedAt is DateTimeOffset modified && Math.Abs((info.LastWriteTimeUtc - modified.UtcDateTime).TotalSeconds) > 1)
                issues.Add(new(ErrorCodeCatalog.SourceChanged, "源文件修改时间已改变。", item.Id, true));
            if (string.Equals(Path.GetFullPath(item.SourcePath), Path.GetFullPath(item.DestinationPath), StringComparison.OrdinalIgnoreCase))
                issues.Add(new(ErrorCodeCatalog.SourceAndDestinationSame, "源文件与目标文件相同。", item.Id));
            if (File.Exists(item.DestinationPath) && item.ConflictPolicy != FileConflictPolicy.AutoNumber)
                issues.Add(new(ErrorCodeCatalog.DuplicateConflict, "目标位置存在同名文件，需要用户决定。", item.Id, item.ConflictPolicy == FileConflictPolicy.Ask));
            if (!CanReadExclusively(item.SourcePath))
                issues.Add(new(ErrorCodeCatalog.FileLocked, "源文件正被其他程序占用。", item.Id, true));
        }

        if (!issues.Any(x => x.ErrorCode == ErrorCodeCatalog.DestinationNotWritable))
        {
            try
            {
                Directory.CreateDirectory(destinationRoot);
                var probe = Path.Combine(destinationRoot, ".pixel-tart-write-test-" + Guid.NewGuid().ToString("N"));
                using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
                File.Delete(probe);
            }
            catch (UnauthorizedAccessException) { issues.Add(new(ErrorCodeCatalog.PermissionDenied, "目标目录没有写入权限。")); }
            catch (IOException) { issues.Add(new(ErrorCodeCatalog.DestinationNotWritable, "目标目录不可写或已断开。", RequiresAttention: true)); }
        }

        try
        {
            var root = Path.GetPathRoot(destinationRoot);
            if (!string.IsNullOrWhiteSpace(root))
            {
                var drive = new DriveInfo(root);
                if (drive.IsReady && drive.AvailableFreeSpace < bytes + Math.Max(64L * 1024 * 1024, bytes / 20))
                    issues.Add(new(ErrorCodeCatalog.DiskSpaceInsufficient, "目标磁盘可用空间不足。", RequiresAttention: true));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add(new(ErrorCodeCatalog.DestinationDisconnected, "无法读取目标磁盘状态。", RequiresAttention: true));
        }

        var risk = plan.OperationType == FileOperationType.Move ? FileOperationRiskLevel.Medium : plan.RiskLevel;
        return Task.FromResult(new FileOperationValidationResult(issues.Count == 0, issues, bytes, risk));
    }

    private static bool CanReadExclusively(string path)
    {
        try { using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); return stream.CanRead; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    private static bool IsInside(string path, string parent) => path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static bool IsForbiddenDestination(string path)
    {
        var windows = Normalize(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        var programFiles = Normalize(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        return string.Equals(path, windows, StringComparison.OrdinalIgnoreCase) || IsInside(path, windows) || string.Equals(path, programFiles, StringComparison.OrdinalIgnoreCase) || IsInside(path, programFiles);
    }
}

