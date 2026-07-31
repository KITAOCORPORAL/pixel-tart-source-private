using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.Versioning;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Services;

[SupportedOSPlatform("windows")]
public sealed class DpapiLicenseStorageService : ILicenseStorageService
{
    private static readonly byte[] Entropy = SHA256.HashData(Encoding.UTF8.GetBytes("KitaoPhotoSelector-License-v1"));
    private readonly ILogService? _logService;
    private readonly string _path;

    public DpapiLicenseStorageService(ILogService? logService = null, string? path = null)
    {
        _logService = logService;
        _path = path ?? Path.Combine(AppDataPaths.LicenseDirectory, "license.dat");
    }

    public async Task<LicenseCredential?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return null;
        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
            var clearBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<LicenseCredential>(clearBytes);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
        {
            _logService?.Error("本地授权缓存损坏、被修改或无法读取。", ex);
            return null;
        }
    }

    public async Task SaveAsync(LicenseCredential credential, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var clearBytes = JsonSerializer.SerializeToUtf8Bytes(credential);
        var protectedBytes = ProtectedData.Protect(clearBytes, Entropy, DataProtectionScope.CurrentUser);
        var temporaryPath = _path + ".tmp";
        await File.WriteAllBytesAsync(temporaryPath, protectedBytes, cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, _path, true);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(_path)) File.Delete(_path);
        return Task.CompletedTask;
    }
}
