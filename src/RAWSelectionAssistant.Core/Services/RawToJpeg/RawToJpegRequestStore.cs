using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Services.RawToJpeg;

[SupportedOSPlatform("windows")]
public sealed class RawToJpegRequestStore : IRawToJpegRequestStore
{
    private static readonly byte[] Entropy = SHA256.HashData(Encoding.UTF8.GetBytes("PixelTart-RawToJpeg-Recovery-v1"));
    private readonly ConcurrentDictionary<Guid, RawToJpegRecoveryCheckpoint> _checkpoints = new();
    private readonly string _directory;

    public RawToJpegRequestStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(AppDataPaths.DataDirectory, "RawToJpegRecovery");
    }

    public void Register(Guid taskId, RawToJpegBatchRequest request)
    {
        request.Validate();
        var checkpoint = new RawToJpegRecoveryCheckpoint(request, request.SourceFiles.ToArray(), []);
        if (!_checkpoints.TryAdd(taskId, checkpoint))
            throw new InvalidOperationException("A RAW conversion request already exists for this task.");
        try { Persist(taskId, checkpoint); }
        catch
        {
            _checkpoints.TryRemove(taskId, out _);
            throw;
        }
    }

    public bool TryGet(Guid taskId, out RawToJpegRecoveryCheckpoint checkpoint)
    {
        if (_checkpoints.TryGetValue(taskId, out checkpoint!)) return true;
        var path = PathFor(taskId);
        if (!File.Exists(path)) return false;
        try
        {
            var protectedBytes = File.ReadAllBytes(path);
            var clearBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            checkpoint = JsonSerializer.Deserialize<RawToJpegRecoveryCheckpoint>(clearBytes)
                ?? throw new InvalidDataException("RAW recovery checkpoint is empty.");
            checkpoint.OriginalRequest.Validate();
            _checkpoints[taskId] = checkpoint;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or JsonException or InvalidDataException)
        {
            checkpoint = default!;
            return false;
        }
    }

    public void Update(Guid taskId, RawToJpegRecoveryCheckpoint checkpoint)
    {
        checkpoint.OriginalRequest.Validate();
        Persist(taskId, checkpoint);
        _checkpoints[taskId] = checkpoint;
    }

    public void Remove(Guid taskId)
    {
        _checkpoints.TryRemove(taskId, out _);
        try
        {
            var path = PathFor(taskId);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    private void Persist(Guid taskId, RawToJpegRecoveryCheckpoint checkpoint)
    {
        Directory.CreateDirectory(_directory);
        var clearBytes = JsonSerializer.SerializeToUtf8Bytes(checkpoint);
        var protectedBytes = ProtectedData.Protect(clearBytes, Entropy, DataProtectionScope.CurrentUser);
        var path = PathFor(taskId);
        var temporaryPath = path + ".tmp";
        using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
                   16384, FileOptions.WriteThrough))
        {
            stream.Write(protectedBytes);
            stream.Flush(true);
        }
        File.Move(temporaryPath, path, true);
    }

    private string PathFor(Guid taskId) => Path.Combine(_directory, taskId.ToString("N") + ".dat");
}
