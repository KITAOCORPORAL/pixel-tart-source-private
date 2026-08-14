using RAWSelectionAssistant.Core.Services.AssetSelection;

namespace PixelTart.Modules.AssetLibrary;

public sealed class AssetLibrarySelectionSourceAdapter : IAssetSelectionSource
{
    private readonly Func<AssetSelectionQuery, CancellationToken, Task<AssetSelectionPage>> _query;
    private readonly Func<Guid, CancellationToken, Task<AssetSelectionSnapshot?>> _snapshot;
    private readonly Func<Guid, CancellationToken, Task<AssetProxySource?>> _proxy;
    public AssetLibrarySelectionSourceAdapter(
        Func<AssetSelectionQuery, CancellationToken, Task<AssetSelectionPage>> query,
        Func<Guid, CancellationToken, Task<AssetSelectionSnapshot?>> snapshot,
        Func<Guid, CancellationToken, Task<AssetProxySource?>> proxy)
    {
        _query = query;
        _snapshot = snapshot;
        _proxy = proxy;
    }
    public string ContractVersion => AssetSelectionContract.ContractVersion;
    public Task<AssetSelectionPage> QueryAssetsAsync(AssetSelectionQuery query, CancellationToken cancellationToken = default) => _query(query, cancellationToken);
    public Task<AssetSelectionSnapshot?> GetAssetSnapshotAsync(Guid assetId, CancellationToken cancellationToken = default) => _snapshot(assetId, cancellationToken);
    public Task<AssetProxySource?> GetProxySourceAsync(Guid assetId, CancellationToken cancellationToken = default) => _proxy(assetId, cancellationToken);
}
