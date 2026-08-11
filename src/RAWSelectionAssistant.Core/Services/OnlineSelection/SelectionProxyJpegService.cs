using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Core.Services.OnlineSelection;

public sealed class SelectionProxyJpegService(ISelectionProxyRenderer renderer)
{
    private static readonly HashSet<string> SupportedSourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".tif", ".tiff",
        ".arw", ".cr2", ".cr3", ".dng", ".nef", ".nrw", ".orf", ".pef", ".raf", ".rw2", ".srw"
    };

    public IReadOnlyCollection<string> SupportedExtensions => SupportedSourceExtensions;

    public async Task<SelectionProxyResult> GenerateAsync(
        string sourcePath,
        string outputDirectory,
        SelectionProxyOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(outputDirectory))
            return new(SelectionProxyState.Failed, null, 0, "代理图输入无效。", OnlineSelectionErrorCodes.ProxyGenerationFailed);
        var source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source)) return new(SelectionProxyState.Failed, null, 0, "源文件当前不可访问。", ErrorCodeCatalog.SourceNotFound);
        if (!SupportedSourceExtensions.Contains(Path.GetExtension(source)))
            return new(SelectionProxyState.Unsupported, null, 0, "当前文件类型不能生成在线选片代理图。", ErrorCodeCatalog.UnsupportedFormat);

        var beforeLength = new FileInfo(source).Length;
        var beforeWriteUtc = File.GetLastWriteTimeUtc(source);
        var directory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(directory);
        string? ownedStagingPath = null;
        try
        {
            var staging = CreateOwnedStagingFile(directory);
            ownedStagingPath = staging.Path;
            await using (staging.Stream)
            {
                await renderer.RenderJpegAsync(source, staging.Stream, options ?? SelectionProxyOptions.OnlineDefault, cancellationToken).ConfigureAwait(false);
                await staging.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                staging.Stream.Flush(flushToDisk: true);
            }
            var outputLength = new FileInfo(ownedStagingPath).Length;
            if (outputLength <= 0) throw new InvalidDataException("代理图为空。");
            var sourceInfo = new FileInfo(source);
            if (sourceInfo.Length != beforeLength || sourceInfo.LastWriteTimeUtc != beforeWriteUtc)
                throw new IOException("源文件在生成代理图期间发生变化。");
            cancellationToken.ThrowIfCancellationRequested();
            var destination = MoveToNumberedPath(
                ownedStagingPath,
                directory,
                Path.GetFileNameWithoutExtension(source) + "_proxy",
                ".jpg");
            ownedStagingPath = null;
            return new(SelectionProxyState.Ready, destination, outputLength, $"已生成在线选片代理 JPG（{renderer.Name}）。");
        }
        catch (OperationCanceledException)
        {
            SafeDeleteOwned(ownedStagingPath);
            throw;
        }
        catch
        {
            SafeDeleteOwned(ownedStagingPath);
            return new(SelectionProxyState.Failed, null, 0, "代理图生成未完成，源文件保持不变。", OnlineSelectionErrorCodes.ProxyGenerationFailed);
        }
    }

    private static (string Path, FileStream Stream) CreateOwnedStagingFile(string directory)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var path = Path.Combine(directory, $".selection-proxy-{Guid.NewGuid():N}.tmp");
            try
            {
                return (path, new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan));
            }
            catch (IOException) when (File.Exists(path))
            {
            }
        }
        throw new IOException("无法创建代理图临时文件。");
    }

    private static string MoveToNumberedPath(string ownedStagingPath, string directory, string stem, string extension)
    {
        for (var index = 1; index < 100_000; index++)
        {
            var suffix = index == 1 ? string.Empty : $"_{index}";
            var candidate = Path.Combine(directory, stem + suffix + extension);
            try
            {
                File.Move(ownedStagingPath, candidate, overwrite: false);
                return candidate;
            }
            catch (IOException) when (File.Exists(ownedStagingPath) && File.Exists(candidate))
            {
            }
        }
        throw new IOException("无法创建唯一的代理图文件名。");
    }

    private static void SafeDeleteOwned(string? ownedStagingPath)
    {
        if (string.IsNullOrWhiteSpace(ownedStagingPath)) return;
        try { if (File.Exists(ownedStagingPath)) File.Delete(ownedStagingPath); } catch { }
    }
}

public sealed class PassThroughJpegProxyRenderer : ISelectionProxyRenderer
{
    public string Name => "JPEG原图代理";

    public async Task RenderJpegAsync(string sourcePath, Stream destination, SelectionProxyOptions options, CancellationToken cancellationToken = default)
    {
        if (!new[] { ".jpg", ".jpeg" }.Contains(Path.GetExtension(sourcePath), StringComparer.OrdinalIgnoreCase))
            throw new NotSupportedException("该渲染器仅用于已是 JPEG 的选片输入。");
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(destination, 64 * 1024, cancellationToken).ConfigureAwait(false);
    }
}
