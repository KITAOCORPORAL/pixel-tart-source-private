using System.Text.Json;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.OnlineSelection;

public sealed class InMemorySelectionWorkspaceStore(SelectionWorkspaceSnapshot? initial = null) : ISelectionWorkspaceStore
{
    private readonly object _sync = new();
    private SelectionWorkspaceSnapshot _snapshot = initial ?? SelectionWorkspaceSnapshot.Empty;

    public Task<SelectionWorkspaceSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync) return Task.FromResult(_snapshot);
    }

    public Task SaveAsync(SelectionWorkspaceSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_sync) _snapshot = snapshot;
        return Task.CompletedTask;
    }
}

public sealed class JsonSelectionWorkspaceStore : ISelectionWorkspaceStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonSelectionWorkspaceStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("需要指定本地工作区文件。", nameof(filePath));
        FilePath = Path.GetFullPath(filePath);
    }

    public string FilePath { get; }

    public async Task<SelectionWorkspaceSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(FilePath)) return SelectionWorkspaceSnapshot.Empty;
            await using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 32 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var snapshot = await JsonSerializer.DeserializeAsync<SelectionWorkspaceSnapshot>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
            if (snapshot is null || snapshot.Projects is null || snapshot.Assets is null || snapshot.Rules is null || snapshot.FinalResults is null)
                throw new InvalidDataException("在线选片工作区数据损坏，原文件已保留且未写入。");
            return snapshot;
        }
        catch (JsonException)
        {
            throw new InvalidDataException("在线选片工作区数据损坏，原文件已保留且未写入。");
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(SelectionWorkspaceSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var directory = Path.GetDirectoryName(FilePath) ?? throw new InvalidOperationException("工作区文件目录无效。");
        Directory.CreateDirectory(directory);
        var temporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 32 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try { File.Delete(temporaryPath); } catch { /* 保留正式快照，不让清理异常覆盖主结果。 */ }
            }
            _gate.Release();
        }
    }
}
