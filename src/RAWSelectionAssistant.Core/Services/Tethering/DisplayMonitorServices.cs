using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Tethering;

public interface IDisplayTopologyService
{
    event EventHandler? TopologyChanged;
    IReadOnlyList<MonitorDisplayInfo> GetDisplays();
    MonitorDisplayInfo? FindByStableKey(string stableKey);
}

public interface IDisplayProfileService
{
    Task<DisplayColorProfile> GetProfileAsync(MonitorDisplayInfo display, CancellationToken cancellationToken = default);
}

public interface IColorProfileCache
{
    bool TryGet(string stableDisplayKey, out DisplayColorProfile? profile);
    void Set(DisplayColorProfile profile);
    void Invalidate(string stableDisplayKey);
}

public interface IDisplayColorCoordinator
{
    Task<DisplayColorProfile> ResolveAsync(MonitorDisplayInfo display, CancellationToken cancellationToken = default);
}

public interface IMonitorPreferenceStore
{
    Task<MonitorDisplayPreference?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(MonitorDisplayPreference preference, CancellationToken cancellationToken = default);
}

public static class DisplayStableKey
{
    public static string Create(string deviceName, string? deviceId)
    {
        var identity = $"{deviceName.Trim().ToUpperInvariant()}|{deviceId?.Trim().ToUpperInvariant()}";
        return "display-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant()[..24];
    }
}

public sealed class MemoryColorProfileCache : IColorProfileCache
{
    private readonly Dictionary<string, DisplayColorProfile> _profiles = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    public bool TryGet(string stableDisplayKey, out DisplayColorProfile? profile) { lock (_sync) return _profiles.TryGetValue(stableDisplayKey, out profile); }
    public void Set(DisplayColorProfile profile) { lock (_sync) _profiles[profile.StableDisplayKey] = profile; }
    public void Invalidate(string stableDisplayKey) { lock (_sync) _profiles.Remove(stableDisplayKey); }
}

public sealed class DisplayColorCoordinator(IDisplayProfileService profiles, IColorProfileCache cache) : IDisplayColorCoordinator
{
    public async Task<DisplayColorProfile> ResolveAsync(MonitorDisplayInfo display, CancellationToken cancellationToken = default)
    {
        if (cache.TryGet(display.StableKey, out var cached) && cached is not null) return cached;
        var resolved = await profiles.GetProfileAsync(display, cancellationToken).ConfigureAwait(false);
        cache.Set(resolved);
        return resolved;
    }
}

public sealed class JsonMonitorPreferenceStore(string root) : IMonitorPreferenceStore
{
    private readonly string _path = Path.Combine(root, "client-monitor.json");
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task<MonitorDisplayPreference?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { if (!File.Exists(_path)) return null; await using var stream = File.OpenRead(_path); return await JsonSerializer.DeserializeAsync<MonitorDisplayPreference>(stream, cancellationToken: cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
        finally { _gate.Release(); }
    }
    public async Task SaveAsync(MonitorDisplayPreference preference, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { Directory.CreateDirectory(Path.GetDirectoryName(_path)!); var temporary = _path + ".tmp-" + Guid.NewGuid().ToString("N"); await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { await JsonSerializer.SerializeAsync(stream, preference, cancellationToken: cancellationToken).ConfigureAwait(false); await stream.FlushAsync(cancellationToken).ConfigureAwait(false); stream.Flush(true); } File.Move(temporary, _path, true); }
        finally { _gate.Release(); }
    }
}

public sealed class ClientMonitorCoordinator
{
    private readonly object _sync = new();
    private Guid? _latestAssetId;
    private Guid? _mainAssetId;
    private Guid? _displayedAssetId;
    private int _newAssetCount;
    public ClientMonitorFollowMode FollowMode { get; private set; } = ClientMonitorFollowMode.FollowMainSelection;
    public bool IsConnected { get; private set; }
    public bool IsOpen { get; private set; }

    public ClientMonitorState Open(Guid? mainAssetId, bool displayConnected)
    {
        lock (_sync) { IsConnected = displayConnected; IsOpen = displayConnected; _mainAssetId = mainAssetId; if (FollowMode != ClientMonitorFollowMode.Locked) _displayedAssetId = ResolveFollowTarget(); return Snapshot(); }
    }
    public ClientMonitorState Close() { lock (_sync) { IsOpen = false; return Snapshot(); } }
    public ClientMonitorState SetFollowMode(ClientMonitorFollowMode mode) { lock (_sync) { FollowMode = mode; if (mode != ClientMonitorFollowMode.Locked) { _displayedAssetId = ResolveFollowTarget(); _newAssetCount = 0; } return Snapshot(); } }
    public ClientMonitorState OnMainSelection(Guid? assetId) { lock (_sync) { _mainAssetId = assetId; if (FollowMode == ClientMonitorFollowMode.FollowMainSelection) { _displayedAssetId = assetId; _newAssetCount = 0; } return Snapshot(); } }
    public ClientMonitorState OnReady(Guid assetId) { lock (_sync) { _latestAssetId = assetId; if (FollowMode == ClientMonitorFollowMode.FollowLatest) { _displayedAssetId = assetId; _newAssetCount = 0; } else if (FollowMode == ClientMonitorFollowMode.Locked) _newAssetCount++; return Snapshot(); } }
    public ClientMonitorState Disconnect() { lock (_sync) { IsConnected = false; IsOpen = false; return Snapshot("客户显示器未连接；联机会话和主监看继续。"); } }
    public ClientMonitorState Reconnect() { lock (_sync) { IsConnected = true; return Snapshot("客户显示器已重新连接，可恢复监看。"); } }
    public ClientMonitorState Snapshot(string? status = null) { lock (_sync) return new(FollowMode, _displayedAssetId, IsConnected, IsOpen, _newAssetCount, status ?? (IsOpen ? "客户监看已开启" : "客户监看未开启")); }
    private Guid? ResolveFollowTarget() => FollowMode == ClientMonitorFollowMode.FollowLatest ? _latestAssetId : _mainAssetId;
}
