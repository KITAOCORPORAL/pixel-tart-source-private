namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

/// <summary>Origin of a creative-planning reference. The reference never owns source bytes.</summary>
public enum CreativeAssetSourceType
{
    AssetLibrary,
    ExternalImage,
    ClientUpload,
    AiGeneratedReference,
    InspirationReference
}

/// <summary>
/// Portable source identity used by inspiration, moodboard and planning features.
/// Library assets are identified only through <see cref="AssetLibraryStableReference"/>;
/// external sources retain their own opaque reference and are not represented as AssetItem.
/// </summary>
public sealed record CreativeAssetReference
{
    public CreativeAssetReference(
        CreativeAssetSourceType sourceType,
        AssetLibraryStableReference? stableReference = null,
        string? externalReference = null)
    {
        var hasStableReference = stableReference is not null;
        var hasExternalReference = !string.IsNullOrWhiteSpace(externalReference);
        if (sourceType == CreativeAssetSourceType.AssetLibrary && (!hasStableReference || hasExternalReference))
            throw new ArgumentException("素材库来源必须且只能提供稳定引用。", nameof(stableReference));
        if (sourceType is CreativeAssetSourceType.ExternalImage or CreativeAssetSourceType.ClientUpload or CreativeAssetSourceType.AiGeneratedReference
            && (!hasExternalReference || hasStableReference))
            throw new ArgumentException("外部、客户上传或 AI 来源必须且只能提供外部引用。", nameof(externalReference));
        if (sourceType == CreativeAssetSourceType.InspirationReference && hasStableReference == hasExternalReference)
            throw new ArgumentException("灵感来源必须提供稳定引用或外部引用中的一种。", nameof(externalReference));

        SourceType = sourceType;
        StableReference = stableReference;
        ExternalReference = string.IsNullOrWhiteSpace(externalReference) ? null : externalReference.Trim();
    }

    public CreativeAssetSourceType SourceType { get; }
    public AssetLibraryStableReference? StableReference { get; }
    public string? ExternalReference { get; }
}

public enum CreativeAssetResolutionState
{
    Resolved,
    LibraryOffline,
    AssetMissing,
    HashMismatch,
    ExternalUnavailable,
    Unsupported
}

/// <summary>Result of resolving a non-owning creative reference to a readable source.</summary>
public sealed record CreativeAssetResolution(
    CreativeAssetReference Source,
    CreativeAssetResolutionState State,
    string? ResolvedSourcePath = null,
    string? Reason = null)
{
    public bool IsResolved => State == CreativeAssetResolutionState.Resolved
        && !string.IsNullOrWhiteSpace(ResolvedSourcePath);
}

/// <summary>
/// Shared resolution seam for future trays, moodboards and planning surfaces. Implementations
/// must verify LibraryId, AssetId and ContentHash before returning a library source path.
/// </summary>
public interface ICreativeAssetReferenceResolver
{
    Task<CreativeAssetResolution> ResolveAsync(
        CreativeAssetReference source,
        CancellationToken cancellationToken = default);
}

/// <summary>Provider-neutral thumbnail request shared by future creative surfaces.</summary>
public sealed record AssetThumbnailReference(
    CreativeAssetReference Source,
    int PreferredPixelWidth = 256,
    string? Variant = null)
{
    public int EffectivePixelWidth => Math.Clamp(PreferredPixelWidth, 96, 512);
}

/// <summary>A named inspiration collection containing references, never copied asset records.</summary>
public sealed record InspirationCollection
{
    public InspirationCollection(
        Guid collectionId,
        string name,
        IReadOnlyList<AssetLibraryStableReference> assets,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        if (collectionId == Guid.Empty) throw new ArgumentException("灵感集身份不能为空。", nameof(collectionId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("灵感集名称不能为空。", nameof(name));
        ArgumentNullException.ThrowIfNull(assets);
        if (updatedAtUtc < createdAtUtc) throw new ArgumentException("灵感集更新时间不能早于创建时间。", nameof(updatedAtUtc));

        CollectionId = collectionId;
        Name = name.Trim();
        Assets = assets.Distinct().ToArray();
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid CollectionId { get; }
    public string Name { get; }
    public IReadOnlyList<AssetLibraryStableReference> Assets { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; }
}

public sealed record MoodboardPoint
{
    public MoodboardPoint(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
            throw new ArgumentOutOfRangeException(nameof(x), "情绪板位置必须是有限数值。");
        X = x;
        Y = y;
    }

    public double X { get; }
    public double Y { get; }
}

/// <summary>Future moodboard element contract. It intentionally has no AssetItem dependency.</summary>
public sealed record MoodboardItem
{
    public MoodboardItem(
        Guid itemId,
        CreativeAssetReference source,
        AssetThumbnailReference thumbnail,
        MoodboardPoint position,
        double scale,
        double rotation,
        string? note = null)
    {
        if (itemId == Guid.Empty) throw new ArgumentException("情绪板元素身份不能为空。", nameof(itemId));
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(thumbnail);
        ArgumentNullException.ThrowIfNull(position);
        if (thumbnail.Source != source) throw new ArgumentException("缩略图引用必须与元素来源一致。", nameof(thumbnail));
        if (!double.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale), "缩放必须是正有限数值。");
        if (!double.IsFinite(rotation)) throw new ArgumentOutOfRangeException(nameof(rotation), "旋转必须是有限数值。");

        ItemId = itemId;
        Source = source;
        Thumbnail = thumbnail;
        Position = position;
        Scale = scale;
        Rotation = rotation;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public Guid ItemId { get; }
    public CreativeAssetReference Source { get; }
    public AssetThumbnailReference Thumbnail { get; }
    public MoodboardPoint Position { get; }
    public double Scale { get; }
    public double Rotation { get; }
    public string? Note { get; }
}

/// <summary>Reference usable by a future project planning aggregate without owning source content.</summary>
public sealed record ProjectPlanningReference
{
    public ProjectPlanningReference(
        Guid referenceId,
        CreativeAssetReference source,
        AssetThumbnailReference thumbnail,
        string? role = null,
        string? note = null)
    {
        if (referenceId == Guid.Empty) throw new ArgumentException("策划引用身份不能为空。", nameof(referenceId));
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(thumbnail);
        if (thumbnail.Source != source) throw new ArgumentException("缩略图引用必须与策划来源一致。", nameof(thumbnail));

        ReferenceId = referenceId;
        Source = source;
        Thumbnail = thumbnail;
        Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public Guid ReferenceId { get; }
    public CreativeAssetReference Source { get; }
    public AssetThumbnailReference Thumbnail { get; }
    public string? Role { get; }
    public string? Note { get; }
}

/// <summary>
/// Stable calendar/project connection seam. A booking is optional because project assets may be
/// collected before a shoot is scheduled. Persistence is deliberately left to a later phase.
/// </summary>
public sealed record ProjectAssetReferenceLink
{
    public ProjectAssetReferenceLink(
        Guid projectId,
        Guid? bookingId,
        AssetLibraryStableReference stableReference,
        string? role,
        DateTimeOffset addedAtUtc,
        string? source = null)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("项目身份不能为空。", nameof(projectId));
        if (bookingId == Guid.Empty) throw new ArgumentException("预约身份不能为空 GUID。", nameof(bookingId));
        ArgumentNullException.ThrowIfNull(stableReference);

        ProjectId = projectId;
        BookingId = bookingId;
        StableReference = stableReference;
        Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim();
        AddedAtUtc = addedAtUtc;
        Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
    }

    public Guid ProjectId { get; }
    public Guid? BookingId { get; }
    public AssetLibraryStableReference StableReference { get; }
    public string? Role { get; }
    public DateTimeOffset AddedAtUtc { get; }
    public string? Source { get; }
}
