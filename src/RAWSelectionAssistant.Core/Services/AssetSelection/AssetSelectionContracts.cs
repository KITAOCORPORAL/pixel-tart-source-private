namespace RAWSelectionAssistant.Core.Services.AssetSelection;

public static class AssetSelectionContract
{
    public const string ContractVersion = "pixel-tart-asset-selection/v1";
}

public sealed record AssetSelectionQuery(
    string SearchText = "",
    IReadOnlyList<Guid>? FolderIds = null,
    Guid? SmartFolderId = null,
    IReadOnlyList<Guid>? TagIds = null,
    int? MinimumRating = null,
    int? MaximumRating = null,
    string? MediaType = null,
    string? Extension = null,
    string? Cursor = null,
    int PageSize = 100)
{
    public int EffectivePageSize => Math.Clamp(PageSize <= 0 ? 100 : PageSize, 1, 200);
}

public sealed record AssetSelectionSnapshot(
    Guid AssetId,
    string DisplayName,
    string OriginalFileName,
    string OriginalStem,
    string Extension,
    string MediaType,
    long FileSize,
    string? ContentFingerprint,
    int? Width,
    int? Height,
    string? Orientation,
    DateTimeOffset? CaptureTime,
    int Rating,
    bool IsMissing);

public sealed record AssetSelectionPage(
    IReadOnlyList<AssetSelectionSnapshot> Items,
    string? NextCursor,
    int TotalCount);

public enum AssetProxySourceKind
{
    RasterOriginal,
    ExistingProxy,
    EmbeddedRawPreview
}

public sealed class AssetProxySource : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<Stream>> _openReadAsync;
    private Stream? _openedStream;

    public AssetProxySource(Guid assetId, AssetProxySourceKind sourceKind, string suggestedFileName, string mediaType, long length, string contentFingerprint, Func<CancellationToken, Task<Stream>> openReadAsync, bool isUploadReady = false)
    {
        AssetId = assetId; SourceKind = sourceKind; SuggestedFileName = suggestedFileName; MediaType = mediaType; Length = length; ContentFingerprint = contentFingerprint; _openReadAsync = openReadAsync; IsUploadReady = isUploadReady;
    }

    public Guid AssetId { get; }
    public AssetProxySourceKind SourceKind { get; }
    public string SuggestedFileName { get; }
    public string MediaType { get; }
    public long Length { get; }
    public string ContentFingerprint { get; }
    public bool IsUploadReady { get; }

    public async Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        if (_openedStream is not null) throw new InvalidOperationException("Proxy source stream is already open.");
        _openedStream = await _openReadAsync(cancellationToken).ConfigureAwait(false);
        if (_openedStream.CanWrite) { await _openedStream.DisposeAsync().ConfigureAwait(false); _openedStream = null; throw new InvalidOperationException("Proxy source must be read-only."); }
        return _openedStream;
    }

    public async ValueTask DisposeAsync()
    {
        if (_openedStream is not null) await _openedStream.DisposeAsync().ConfigureAwait(false);
        _openedStream = null;
    }
}

public interface IAssetSelectionSource
{
    string ContractVersion { get; }
    Task<AssetSelectionPage> QueryAssetsAsync(AssetSelectionQuery query, CancellationToken cancellationToken = default);
    Task<AssetSelectionSnapshot?> GetAssetSnapshotAsync(Guid assetId, CancellationToken cancellationToken = default);
    Task<AssetProxySource?> GetProxySourceAsync(Guid assetId, CancellationToken cancellationToken = default);
}
