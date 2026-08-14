using System.Security.Cryptography;
using System.IO;

namespace PixelTart.SelectionApi.Server;

public sealed record SelectionObjectWriteResult(string ObjectKey, long Bytes, string Sha256);

public enum SelectionObjectVariant
{
    Thumb,
    Preview,
    Proxy
}

public interface ISelectionObjectStorage
{
    string RootDirectory { get; }

    Task<SelectionObjectWriteResult> PutAsync(
        string objectKey,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<SelectionObjectWriteResult> PutImageAsync(
        Guid projectId,
        Guid selectionAssetId,
        SelectionObjectVariant variant,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default);
}
/// <summary>
/// Local development storage only. It keeps proxy bytes under a caller-owned
/// temporary root and rejects traversal; it is not a production object store.
/// </summary>
public sealed class LocalSelectionObjectStorage : ISelectionObjectStorage
{
    private static readonly HashSet<string> RawExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".arw", ".cr2", ".cr3", ".dng", ".nef", ".nrw", ".orf", ".pef", ".raf", ".rw2", ".srw"
    };

    public LocalSelectionObjectStorage(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("需要明确本地开发存储目录。", nameof(rootDirectory));
        RootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(RootDirectory);
    }

    public string RootDirectory { get; }

    public async Task<SelectionObjectWriteResult> PutAsync(string objectKey, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentNullException.ThrowIfNull(content);
        if (RawExtensions.Contains(Path.GetExtension(objectKey)))
            throw new InvalidOperationException("RAW files are not accepted by online-selection storage.");
        var destination = Resolve(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var staging = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var output = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(staging, destination, overwrite: true);
            await using var read = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
            return new(objectKey.Replace('\\', '/'), read.Length, Convert.ToHexString(await SHA256.HashDataAsync(read, cancellationToken)).ToLowerInvariant());
        }
        finally
        {
            if (File.Exists(staging))
            {
                try { File.Delete(staging); } catch { }
            }
        }
    }

    public Task<SelectionObjectWriteResult> PutImageAsync(
        Guid projectId,
        Guid selectionAssetId,
        SelectionObjectVariant variant,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || selectionAssetId == Guid.Empty) throw new ArgumentException("Project and asset identifiers are required.");
        if (RawExtensions.Contains(Path.GetExtension(originalFileName)))
            throw new InvalidOperationException("RAW files are never stored by the online-selection service.");
        var folder = variant.ToString().ToLowerInvariant();
        var key = $"{projectId:N}/{selectionAssetId:N}/{folder}.jpg";
        return PutAsync(key, content, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(objectKey);
        if (!File.Exists(path)) return Task.FromResult<Stream?>(null);
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(objectKey);
        if (!File.Exists(path)) return Task.FromResult(false);
        File.Delete(path);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(Resolve(objectKey)));
    }

    private string Resolve(string objectKey)
    {
        var normalized = objectKey.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized)) throw new ArgumentException("对象键不能是绝对路径。", nameof(objectKey));
        var root = Path.GetFullPath(RootDirectory + Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(root, normalized));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("对象键不能越过本地开发存储根目录。", nameof(objectKey));
        return path;
    }
}
