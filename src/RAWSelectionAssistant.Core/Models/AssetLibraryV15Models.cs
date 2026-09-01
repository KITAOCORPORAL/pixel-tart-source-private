namespace RAWSelectionAssistant.Core.Models;

public sealed record AssetFolderCount(Guid FolderId, int DirectAssetCount, int DescendantAssetCount);

public sealed record AssetFolderTreeItem(
    AssetFolder Folder,
    string Path,
    int Depth,
    int DirectAssetCount,
    int DescendantAssetCount,
    IReadOnlyList<AssetFolderTreeItem> Children);

public sealed record AssetFolderMoveRequest(Guid FolderId, Guid? NewParentFolderId, int SortOrder);

public sealed record AssetFolderBatchCreateResult(
    IReadOnlyList<AssetFolder> Created,
    IReadOnlyList<string> ExistingPaths,
    IReadOnlyList<string> InvalidPaths);

public sealed record AssetTagUsageSummary(AssetTag Tag, int SelectedCount, int MembershipCount)
{
    public bool IsCommon => SelectedCount > 0 && MembershipCount == SelectedCount;
    public bool IsPartial => MembershipCount > 0 && MembershipCount < SelectedCount;
}
public sealed record AssetUndoJournalEntry(
    AssetLibraryUndoToken Token,
    string OperationKind,
    bool IsUndone,
    DateTimeOffset? UndoneAt = null);

public enum AssetRelinkMatchMode
{
    FileName,
    RelativePath,
    ContentHash
}

public sealed record AssetRelinkRequest(
    string NewRoot,
    AssetRelinkMatchMode MatchMode = AssetRelinkMatchMode.FileName,
    string? PreviousRoot = null);

public sealed record AssetRelinkResult(int RelinkedCount, int StillMissingCount, IReadOnlyList<string> Warnings);

public enum AssetLibrarySystemCollection
{
    AllAssets,
    RecentlyAdded,
    Uncategorized,
    Untagged,
    MissingFiles,
    HighRating,
    Archived,
    RecycleBin
}

public sealed record SmartFolderDefinition(
    SmartFolder Folder,
    IReadOnlyList<SmartFolderRule> Rules);

public static class AssetLibrarySystemCollections
{
    public static AssetLibraryQuery CreateQuery(AssetLibrarySystemCollection collection) => collection switch
    {
        AssetLibrarySystemCollection.Uncategorized => new(UncategorizedOnly: true, SystemCollection: collection),
        AssetLibrarySystemCollection.Untagged => new(UntaggedOnly: true, SystemCollection: collection),
        AssetLibrarySystemCollection.MissingFiles => new(MissingOnly: true, SystemCollection: collection),
        AssetLibrarySystemCollection.HighRating => new(MinimumRating: 4, SystemCollection: collection),
        AssetLibrarySystemCollection.RecentlyAdded => new(PageSize: 100, SortField: AssetLibrarySortField.AddedAt, SortDirection: AssetLibrarySortDirection.Descending, SystemCollection: collection),
        AssetLibrarySystemCollection.Archived => new(ArchiveScope: AssetLibraryArchiveScope.ArchivedOnly, SystemCollection: collection),
        _ => new(SystemCollection: collection)
    };
}
