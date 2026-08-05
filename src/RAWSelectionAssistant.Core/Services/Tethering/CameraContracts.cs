using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Tethering;

public sealed record CameraDescriptor(string Id, string DisplayName, CameraProviderType ProviderType, bool IsAvailable);
public sealed record CameraCapabilities(bool FileTransfer, bool LiveView, bool RemoteShutter, bool CameraSettings);

public interface ICameraDiscoveryService
{
    Task<IReadOnlyList<CameraDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default);
}

public interface ICameraCapabilityService
{
    CameraCapabilities GetCapabilities(CameraProviderType providerType);
}

public interface ICameraSession : IAsyncDisposable
{
    TetherSessionRecord Session { get; }
    event EventHandler<TetherSessionSnapshot>? SnapshotChanged;
    Task StopAsync(CancellationToken cancellationToken = default);
    Task ReconcileAsync(CancellationToken cancellationToken = default);
}

public interface ICameraTetherProvider
{
    CameraProviderType ProviderType { get; }
    string DisplayName { get; }
    Task<ICameraSession> StartAsync(WatchFolderStartRequest request, CancellationToken cancellationToken = default);
}

public interface ICameraTransferService
{
    Task<TetherCopyResult> CopyToProjectAsync(TetherAssetRecord asset, string destinationRoot, bool verifySha256, CancellationToken cancellationToken = default);
    Task<TetherCopyResult> CopyToBackupAsync(TetherAssetRecord asset, string destinationRoot, bool verifySha256, CancellationToken cancellationToken = default);
}

public interface ICameraConnectionMonitor
{
    CameraProviderType ActiveProvider { get; }
    bool IsConnected { get; }
}

public sealed class DefaultCameraDiscoveryService : ICameraDiscoveryService
{
    public Task<IReadOnlyList<CameraDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CameraDescriptor>>([new("none", "未连接相机", CameraProviderType.None, true), new("watch-folder", "看守文件夹", CameraProviderType.WatchFolder, true)]);
}

public sealed class DefaultCameraCapabilityService : ICameraCapabilityService
{
    public CameraCapabilities GetCapabilities(CameraProviderType providerType) => providerType == CameraProviderType.WatchFolder
        ? new(FileTransfer: true, LiveView: false, RemoteShutter: false, CameraSettings: false)
        : new(false, false, false, false);
}

public interface IRawPreviewDecoder
{
    string Name { get; }
    Task<RawPreviewResult> DecodeAsync(string sourcePath, string destinationPath, int longestEdge, CancellationToken cancellationToken = default);
}

public sealed record RawPreviewResult(bool Success, string? PreviewPath, string? ErrorCode = null);

public sealed class NoneRawPreviewDecoder : IRawPreviewDecoder
{
    public string Name => "None";
    public Task<RawPreviewResult> DecodeAsync(string sourcePath, string destinationPath, int longestEdge, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RawPreviewResult(false, null, null));
}

public interface ITetherProxyCache
{
    Task<string?> GetOrCreateAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default);
    string? ResolvePath(string? cacheKey);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
