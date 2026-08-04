using System.Security.Cryptography;

namespace RAWSelectionAssistant.Core.Services.FileOperations;

public sealed class FileVerificationService : IFileVerificationService
{
    public async Task<string?> ComputeSha256Async(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    public async Task<bool> VerifyAsync(string sourcePath, string destinationPath, bool verifyHash, CancellationToken cancellationToken = default)
    {
        var source = new FileInfo(sourcePath);
        var destination = new FileInfo(destinationPath);
        if (!source.Exists || !destination.Exists || source.Length != destination.Length) return false;
        if (!verifyHash) return true;
        return string.Equals(await ComputeSha256Async(sourcePath, cancellationToken).ConfigureAwait(false), await ComputeSha256Async(destinationPath, cancellationToken).ConfigureAwait(false), StringComparison.OrdinalIgnoreCase);
    }
}

