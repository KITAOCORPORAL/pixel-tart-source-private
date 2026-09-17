using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Publishing;

public interface IPublishingRenderer
{
    Task RenderAsync(string sourcePath, string destinationPath, PublishingOptions options, CancellationToken cancellationToken = default);
    Task VerifyAsync(string imagePath, CancellationToken cancellationToken = default);
}

public interface IPublishingExportService
{
    Task<PublishingExportResult> ExportAsync(Guid taskId, PublishingExportRequest request, IProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class PublishingExportService(IPublishingRenderer renderer) : IPublishingExportService
{
    public async Task<PublishingExportResult> ExportAsync(Guid taskId, PublishingExportRequest request, IProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>? progress = null, CancellationToken cancellationToken = default)
    {
        request.Validate();
        Directory.CreateDirectory(request.DestinationDirectory);
        var sourceFiles = request.SourceFiles.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var results = new List<PublishingItemResult>(sourceFiles.Length);
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < sourceFiles.Length; index++)
        {
            var source = sourceFiles[index];
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(source)) throw new FileNotFoundException("照片不可用。", source);
                if (!PublishingDefaults.SupportedExtensions.Contains(Path.GetExtension(source))) throw new InvalidDataException("暂不支持此图片格式；当前支持 JPG、JPEG、PNG。");
                var before = await ComputeHashAsync(source, cancellationToken).ConfigureAwait(false);
                var destination = ResolveDestination(source, request.DestinationDirectory, request.Options, reserved);
                reserved.Add(destination);
                var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".publishing";
                try
                {
                    await renderer.RenderAsync(source, temporary, request.Options, cancellationToken).ConfigureAwait(false);
                    await renderer.VerifyAsync(temporary, cancellationToken).ConfigureAwait(false);
                    var after = await ComputeHashAsync(source, cancellationToken).ConfigureAwait(false);
                    if (!CryptographicOperations.FixedTimeEquals(before, after)) throw new IOException("源照片在发布处理期间发生变化，已停止输出。");
                    File.Move(temporary, destination, false);
                    results.Add(new(index, PublishingItemState.Completed, source, destination, new FileInfo(destination).Length));
                }
                finally { TryDelete(temporary); }
            }
            catch (OperationCanceledException)
            {
                for (var pending = index; pending < sourceFiles.Length; pending++) results.Add(new(pending, PublishingItemState.Cancelled, sourceFiles[pending], null, 0));
                break;
            }
            catch (Exception error)
            {
                results.Add(new(index, PublishingItemState.Failed, source, null, 0, UserMessage(error)));
            }
            var summary = Summarize(sourceFiles.Length, results);
            progress?.Report((results.Count * 100d / sourceFiles.Length, Path.GetFileName(source), summary));
        }
        var finalSummary = Summarize(sourceFiles.Length, results);
        var state = finalSummary.Succeeded == sourceFiles.Length ? TaskLifecycleState.Completed
            : finalSummary.Succeeded > 0 ? TaskLifecycleState.PartiallyCompleted
            : finalSummary.Cancelled > 0 && finalSummary.Failed == 0 ? TaskLifecycleState.Cancelled : TaskLifecycleState.Failed;
        return new(taskId, state, finalSummary, results.OrderBy(item => item.Sequence).ToArray());
    }

    public static string ResolveDestination(string sourcePath, string destinationDirectory, PublishingOptions options, ISet<string>? reserved = null)
    {
        var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourcePath))!;
        var destinationRoot = Path.GetFullPath(destinationDirectory);
        var suffix = options.Suffix ?? string.Empty;
        if (string.Equals(sourceDirectory.TrimEnd(Path.DirectorySeparatorChar), destinationRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(suffix)) suffix = PublishingDefaults.DefaultSuffix;
        var extension = options.OutputFormat == PublishingOutputFormat.Png ? ".png" : ".jpg";
        var stem = Path.GetFileNameWithoutExtension(sourcePath) + suffix;
        var desired = Path.Combine(destinationRoot, stem + extension);
        var candidate = desired; var number = 2;
        while (File.Exists(candidate) || reserved?.Contains(candidate) == true) candidate = Path.Combine(destinationRoot, $"{stem}_{number++}{extension}");
        return candidate;
    }

    private static async Task<byte[]> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
    }
    private static TaskResultSummary Summarize(int total, IReadOnlyCollection<PublishingItemResult> results) => new(total, results.Count(item => item.State == PublishingItemState.Completed), results.Count(item => item.State == PublishingItemState.Failed), 0, results.Count(item => item.State == PublishingItemState.Cancelled), 0, 0, results.Sum(item => item.BytesWritten));
    private static string UserMessage(Exception error) => error switch { FileNotFoundException => "源照片不可用。", UnauthorizedAccessException => "无法写入输出目录。", InvalidDataException invalid => invalid.Message, _ => "发布输出失败，请检查照片和输出目录。" };
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}

public static class PublishingFolderInput
{
    public static IReadOnlyList<string> Scan(string directory)
    {
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => PublishingDefaults.SupportedExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }
}
