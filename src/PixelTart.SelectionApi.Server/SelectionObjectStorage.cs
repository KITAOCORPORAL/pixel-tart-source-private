using System.Security.Cryptography;

namespace PixelTart.SelectionApi.Server;

public sealed record SelectionObjectWriteResult(string ObjectKey, long Bytes, string Sha256);

public interface ISelectionObjectStorage
{
    string RootDirectory { get; }

    Task<SelectionObjectWriteResult> PutAsync(
        string objectKey,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}
/// <summary>
/// Local development storage only. It keeps proxy bytes under a caller-owned
/// temporary root and rejects traversal; it is not a production object store.
/// </summary>
public sealed class LocalSelectionObjectStorage : ISelectionObjectStorage
{
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
