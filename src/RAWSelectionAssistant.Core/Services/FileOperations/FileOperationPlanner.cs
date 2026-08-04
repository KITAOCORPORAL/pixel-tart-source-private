using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.FileOperations;

public sealed class FileConflictResolver : IFileConflictResolver
{
    public string ResolveDestination(string desiredPath, FileConflictPolicy policy, ISet<string>? reservedPaths = null)
    {
        reservedPaths ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(desiredPath) && !Directory.Exists(desiredPath) && !reservedPaths.Contains(desiredPath)) return desiredPath;
        if (policy is FileConflictPolicy.Skip or FileConflictPolicy.Ask or FileConflictPolicy.Fail) return desiredPath;
        var directory = Path.GetDirectoryName(desiredPath)!;
        var name = Path.GetFileNameWithoutExtension(desiredPath);
        var extension = Path.GetExtension(desiredPath);
        for (var index = 1; index <= 99999; index++)
        {
            var candidate = Path.Combine(directory, $"{name} ({index}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate) && !reservedPaths.Contains(candidate)) return candidate;
        }
        throw new IOException("Unable to allocate a unique destination name.");
    }
}

public sealed class FileOperationPlanner(IFileConflictResolver conflictResolver) : IFileOperationPlanner
{
    public Task<FileOperationPlan> CreateAsync(Guid taskId, Guid? projectId, FileOperationType operationType, string sourceRoot, string destinationRoot, IEnumerable<string> sourceFiles, FileConflictPolicy conflictPolicy = FileConflictPolicy.AutoNumber, CancellationToken cancellationToken = default)
    {
        var normalizedSourceRoot = Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedDestinationRoot = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<FileOperationItem>();
        long bytes = 0;
        foreach (var source in sourceFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullSource = Path.GetFullPath(source);
            var relative = Path.GetRelativePath(normalizedSourceRoot, fullSource);
            if (relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || relative == "..") relative = Path.GetFileName(fullSource);
            var desired = Path.Combine(normalizedDestinationRoot, relative);
            var destination = conflictResolver.ResolveDestination(desired, conflictPolicy, reserved);
            reserved.Add(destination);
            var info = new FileInfo(fullSource);
            items.Add(new FileOperationItem(Guid.NewGuid(), items.Count, fullSource, destination, operationType, conflictPolicy,
                info.Exists ? info.Length : null, info.Exists ? info.LastWriteTimeUtc : null));
            if (info.Exists) bytes += info.Length;
        }
        var risk = operationType == FileOperationType.Move ? FileOperationRiskLevel.Medium : operationType == FileOperationType.DeleteCreatedOutput ? FileOperationRiskLevel.High : FileOperationRiskLevel.Low;
        return Task.FromResult(new FileOperationPlan(1, Guid.NewGuid(), taskId, projectId, operationType, normalizedSourceRoot, normalizedDestinationRoot, conflictPolicy, items, bytes, risk, DateTimeOffset.UtcNow));
    }
}

