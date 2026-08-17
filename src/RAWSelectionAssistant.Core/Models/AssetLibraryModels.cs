namespace RAWSelectionAssistant.Core.Models;

/// <summary>
/// Stable metadata identity for an item in the local Asset Library.  The library
/// stores references and metadata only; it never owns the source file unless the
/// caller explicitly requests a managed copy import.
/// </summary>
public sealed record AssetItem(
    Guid AssetId,
    string SourcePath,
    string DisplayName,
    string Extension,
    string MediaType,
    long FileSize,
    string? ContentHash,
    int? Width,
    int? Height,
    string? Orientation,
    DateTimeOffset? CaptureTime,
    DateTimeOffset AddedAt,
    DateTimeOffset ModifiedAt,
    int Rating = 0,
    string Comment = "",
    bool IsMissing = false,
    bool IsArchived = false,
    AssetImportMode ImportMode = AssetImportMode.Reference,
    string? ManagedCopyPath = null)
{
    public string FileName => Path.GetFileName(SourcePath);
    public string OriginalStem => Path.GetFileNameWithoutExtension(DisplayName);
}

public enum AssetImportMode
{
    Reference,
    ManagedCopy
}

public sealed record AssetImportRequest(
    string SourcePath,
    AssetImportMode Mode = AssetImportMode.Reference,
    string? ManagedLibraryRoot = null,
    bool ComputeContentHash = false,
    AssetDuplicateBehavior DuplicateBehavior = AssetDuplicateBehavior.Skip);

public enum AssetDuplicateBehavior
{
    Skip,
    ImportIndependentRecord
}

public sealed record AssetFolder(
    Guid FolderId,
    Guid? ParentFolderId,
    string Name,
    string Description = "",
    string? Icon = null,
    string? Color = null,
    int SortOrder = 0,
    DateTimeOffset? CreatedAt = null,
    DateTimeOffset? UpdatedAt = null,
    bool IsArchived = false,
    bool IsSystem = false,
    IReadOnlyList<Guid>? AutoTagIds = null)
{
    public DateTimeOffset EffectiveCreatedAt => CreatedAt ?? DateTimeOffset.UtcNow;
    public DateTimeOffset EffectiveUpdatedAt => UpdatedAt ?? EffectiveCreatedAt;
}

public sealed record AssetFolderMembership(Guid AssetId, Guid FolderId, DateTimeOffset AddedAt);

public sealed record TagGroup(
    Guid TagGroupId,
    string Name,
    int SortOrder = 0,
    DateTimeOffset? CreatedAt = null,
    bool IsArchived = false);

public sealed record AssetTag(
    Guid TagId,
    string Name,
    Guid? TagGroupId = null,
    int SortOrder = 0,
    int UsageCount = 0,
    DateTimeOffset? CreatedAt = null,
    bool IsArchived = false)
{
    public DateTimeOffset EffectiveCreatedAt => CreatedAt ?? DateTimeOffset.UtcNow;
}

public sealed record AssetTagMembership(Guid AssetId, Guid TagId, DateTimeOffset AddedAt);

public enum SmartFolderLogic
{
    And,
    Or
}

public enum SmartFolderField
{
    FileName,
    Extension,
    MediaType,
    Folder,
    Tag,
    Rating,
    Comment,
    AddedAt,
    CaptureTime,
    Width,
    Height,
    AspectRatio,
    Orientation,
    FileSize,
    IsUncategorized,
    IsUntagged,
    IsMissing,
    VisualAnalysisStatus,
    VisualHarmony,
    VisualToneKey,
    VisualContrast,
    VisualSaturation,
    VisualWarmCool,
    VisualDominantHue,
    VisualDominantColor,
    VisualAverageLuma,
    VisualAverageSaturation,
    VisualLumaSpread,
    VisualShadowRatio,
    VisualHighlightRatio,
    VisualBlackClipRatio,
    VisualWhiteClipRatio
}

public enum SmartFolderOperator
{
    Contains,
    Equals,
    NotEquals,
    StartsWith,
    EndsWith,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Regex,
    IsTrue,
    IsFalse,
    InRange
}

public sealed record SmartFolderRule(
    Guid RuleId,
    Guid SmartFolderId,
    SmartFolderField Field,
    SmartFolderOperator Operator,
    string Value = "",
    bool Negated = false,
    int SortOrder = 0,
    Guid? GroupId = null,
    SmartFolderLogic GroupLogic = SmartFolderLogic.And);

public sealed record SmartFolder(
    Guid SmartFolderId,
    string Name,
    SmartFolderLogic Logic = SmartFolderLogic.And,
    string Description = "",
    DateTimeOffset? CreatedAt = null,
    DateTimeOffset? UpdatedAt = null,
    bool IsArchived = false)
{
    public DateTimeOffset EffectiveCreatedAt => CreatedAt ?? DateTimeOffset.UtcNow;
    public DateTimeOffset EffectiveUpdatedAt => UpdatedAt ?? EffectiveCreatedAt;
}

public sealed record AssetLibraryQuery(
    string SearchText = "",
    Guid? FolderId = null,
    Guid? TagId = null,
    int? MinimumRating = null,
    int? MaximumRating = null,
    string? MediaType = null,
    string? Extension = null,
    bool UncategorizedOnly = false,
    bool UntaggedOnly = false,
    bool MissingOnly = false,
    string? FileNameRegex = null,
    Guid? SmartFolderId = null,
    int PageSize = 100,
    string? Cursor = null,
    bool IncludeArchived = false,
    IReadOnlyList<Guid>? FolderIds = null,
    IReadOnlyList<Guid>? TagIds = null,
    DateTimeOffset? AddedFrom = null,
    DateTimeOffset? AddedTo = null,
    DateTimeOffset? CaptureFrom = null,
    DateTimeOffset? CaptureTo = null)
{
    public int EffectivePageSize => Math.Clamp(PageSize <= 0 ? 100 : PageSize, 1, 500);
}

public sealed record AssetLibraryPage(
    IReadOnlyList<AssetItem> Items,
    string? NextCursor,
    int TotalCount,
    string? RegexError = null);

public sealed record AssetLibraryUndoToken(Guid OperationId, string Description, DateTimeOffset CreatedAt);

public sealed record AssetLibraryBatchResult(
    int ChangedCount,
    AssetLibraryUndoToken? UndoToken,
    IReadOnlyList<string> Warnings);
public sealed record AssetLibraryFolderNode(AssetFolder Folder, IReadOnlyList<AssetLibraryFolderNode> Children);

public sealed record AssetLibraryMetadataIndexResult(
    int ImportedCount,
    int SkippedCount,
    int MissingCount,
    bool Cancelled,
    TimeSpan Elapsed,
    IReadOnlyList<string> Warnings);
