using RAWSelectionAssistant.Core.Services.AssetSelection;

namespace RAWSelectionAssistant.Tests;

internal sealed class FakeAssetSelectionSource(IReadOnlyList<AssetSelectionSnapshot> assets, IReadOnlyDictionary<Guid, byte[]> proxySources)
    : IAssetSelectionSource
{
    public string ContractVersion => AssetSelectionContract.ContractVersion;

    public Task<AssetSelectionPage> QueryAssetsAsync(AssetSelectionQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pageSize = query.EffectivePageSize;
        var offset = int.TryParse(query.Cursor, out var parsed) ? Math.Max(0, parsed) : 0;
        var filtered = assets.Where(asset =>
            (string.IsNullOrWhiteSpace(query.SearchText) || asset.DisplayName.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase)) &&
            (!query.MinimumRating.HasValue || asset.Rating >= query.MinimumRating) &&
            (!query.MaximumRating.HasValue || asset.Rating <= query.MaximumRating) &&
            (string.IsNullOrWhiteSpace(query.MediaType) || string.Equals(asset.MediaType, query.MediaType, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(query.Extension) || string.Equals(asset.Extension, query.Extension, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(asset => asset.AssetId)
            .ToArray();
        var items = filtered.Skip(offset).Take(pageSize).ToArray();
        var next = offset + items.Length < filtered.Length ? (offset + items.Length).ToString() : null;
        return Task.FromResult(new AssetSelectionPage(items, next, filtered.Length));
    }

    public Task<AssetSelectionSnapshot?> GetAssetSnapshotAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<AssetSelectionSnapshot?>(assets.SingleOrDefault(asset => asset.AssetId == assetId));
    }

    public Task<AssetProxySource?> GetProxySourceAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!proxySources.TryGetValue(assetId, out var bytes)) return Task.FromResult<AssetProxySource?>(null);
        AssetProxySource source = new(assetId, AssetProxySourceKind.ExistingProxy, "proxy.jpg", "image/jpeg", bytes.Length,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),
            _ => Task.FromResult<Stream>(new MemoryStream(bytes, writable: false)), isUploadReady: false);
        return Task.FromResult<AssetProxySource?>(source);
    }
}
