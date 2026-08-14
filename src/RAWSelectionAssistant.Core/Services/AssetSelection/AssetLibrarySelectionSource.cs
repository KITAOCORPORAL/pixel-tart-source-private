using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Core.Services.AssetSelection;

public sealed class AssetLibrarySelectionSource(IAssetLibraryRepository repository) : IAssetSelectionSource
{
    public string ContractVersion => AssetSelectionContract.ContractVersion;

    public async Task<AssetSelectionPage> QueryAssetsAsync(AssetSelectionQuery query, CancellationToken cancellationToken = default)
    {
        var assetQuery = new AssetLibraryQuery(
            SearchText: query.SearchText,
            FolderId: query.FolderIds?.Count == 1 ? query.FolderIds[0] : null,
            TagId: query.TagIds?.Count == 1 ? query.TagIds[0] : null,
            MinimumRating: query.MinimumRating,
            MaximumRating: query.MaximumRating,
            MediaType: query.MediaType,
            Extension: query.Extension,
            SmartFolderId: query.SmartFolderId,
            PageSize: query.EffectivePageSize,
            Cursor: query.Cursor,
            FolderIds: query.FolderIds,
            TagIds: query.TagIds);
        var page = await repository.QueryAsync(assetQuery, cancellationToken).ConfigureAwait(false);
        var snapshots = page.Items.Select(ToSnapshot).ToArray();
        return new(snapshots, page.NextCursor, page.TotalCount);
    }

    public async Task<AssetSelectionSnapshot?> GetAssetSnapshotAsync(Guid assetId, CancellationToken cancellationToken = default)
        => await repository.GetAssetAsync(assetId, cancellationToken).ConfigureAwait(false) is { } item ? ToSnapshot(item) : null;

    public async Task<AssetProxySource?> GetProxySourceAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        var item = await repository.GetAssetAsync(assetId, cancellationToken).ConfigureAwait(false);
        if (item is null || item.IsMissing || !File.Exists(item.SourcePath)) return null;
        if (item.MediaType == "Raw") return null;
        var fingerprint = item.ContentHash ?? await FingerprintAsync(item.SourcePath, cancellationToken).ConfigureAwait(false);
        return new(assetId, AssetProxySourceKind.RasterOriginal, item.DisplayName, item.MediaType, item.FileSize, fingerprint,
            _ => Task.FromResult<Stream>(new FileStream(item.SourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan)),
            isUploadReady: false);
    }

    private static AssetSelectionSnapshot ToSnapshot(AssetItem item) => new(
        item.AssetId,
        item.DisplayName,
        item.FileName,
        item.OriginalStem,
        item.Extension,
        item.MediaType,
        item.FileSize,
        item.ContentHash,
        item.Width,
        item.Height,
        item.Orientation,
        item.CaptureTime,
        item.Rating,
        item.IsMissing);

    private static async Task<string> FingerprintAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }
}
