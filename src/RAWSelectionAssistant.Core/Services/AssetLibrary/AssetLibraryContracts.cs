using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

public interface IAssetLibraryRepository : IAsyncDisposable
{
    string DatabasePath { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<AssetLibraryMetadataIndexResult> ImportAsync(
        IEnumerable<AssetImportRequest> requests,
        CancellationToken cancellationToken = default,
        IProgress<int>? progress = null);

    Task<AssetItem?> GetAssetAsync(Guid assetId, CancellationToken cancellationToken = default);
    Task<AssetLibraryPage> QueryAsync(AssetLibraryQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetQueryValidationIssue>> ValidateQueryReferencesAsync(AssetQueryDocument document, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetQuerySuggestion>> GetQuerySuggestionsAsync(string text, int limit = 20, CancellationToken cancellationToken = default);
    Task UpdateAssetAsync(Guid assetId, int? rating = null, string? comment = null, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> UpdateAssetMetadataAsync(Guid assetId, int? rating = null, string? comment = null, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> UpdateAssetsMetadataAsync(IEnumerable<Guid> assetIds, int? rating = null, string? comment = null, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> SetAssetsArchivedAsync(IEnumerable<Guid> assetIds, bool isArchived, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> SetAssetsMissingAsync(IEnumerable<Guid> assetIds, bool isMissing, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssetFolder>> ListFoldersAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetFolderTreeItem>> GetFolderTreeAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<AssetFolder> SaveFolderAsync(AssetFolder folder, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> RenameFolderAsync(Guid folderId, string name, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> MoveFolderAsync(AssetFolderMoveRequest request, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> ReorderFoldersAsync(Guid? parentFolderId, IEnumerable<Guid> orderedFolderIds, CancellationToken cancellationToken = default);
    Task<AssetFolderBatchCreateResult> BatchCreateFoldersAsync(string paths, Guid? parentFolderId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetFolder>> CopyFolderStructureAsync(Guid sourceFolderId, Guid? targetParentFolderId, string? rootName = null, CancellationToken cancellationToken = default);
    Task ArchiveFolderAsync(Guid folderId, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> SetFolderArchivedAsync(Guid folderId, bool isArchived, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> RestoreFolderAsync(Guid folderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetFolderMembership>> ListFolderMembershipsAsync(Guid? folderId = null, Guid? assetId = null, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> AddToFolderAsync(IEnumerable<Guid> assetIds, Guid folderId, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> AddToFoldersAsync(IEnumerable<Guid> assetIds, IEnumerable<Guid> folderIds, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> RemoveFromFolderAsync(IEnumerable<Guid> assetIds, Guid folderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TagGroup>> ListTagGroupsAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<TagGroup> SaveTagGroupAsync(TagGroup group, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetTag>> ListTagsAsync(Guid? tagGroupId = null, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<AssetTag> SaveTagAsync(AssetTag tag, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetTag>> BatchCreateTagsAsync(string values, Guid? tagGroupId = null, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> RenameTagAsync(Guid tagId, string name, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> MoveTagsToGroupAsync(IEnumerable<Guid> tagIds, Guid? tagGroupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetTag>> SearchTagsAsync(string searchText, int limit = 30, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetTagUsageSummary>> GetTagUsageSummaryAsync(IEnumerable<Guid> assetIds, CancellationToken cancellationToken = default);
    Task ArchiveTagAsync(Guid tagId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetTagMembership>> ListTagMembershipsAsync(Guid? tagId = null, Guid? assetId = null, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> AddTagsAsync(IEnumerable<Guid> assetIds, IEnumerable<Guid> tagIds, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> RemoveTagsAsync(IEnumerable<Guid> assetIds, IEnumerable<Guid> tagIds, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> MergeTagsAsync(Guid sourceTagId, Guid targetTagId, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> MergeTagsAsync(IEnumerable<Guid> sourceTagIds, Guid targetTagId, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> SetTagArchivedAsync(Guid tagId, bool isArchived, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> SetTagGroupArchivedAsync(Guid tagGroupId, bool isArchived, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> ReorderTagGroupsAsync(IEnumerable<Guid> orderedTagGroupIds, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> ReorderTagsAsync(Guid? tagGroupId, IEnumerable<Guid> orderedTagIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SmartFolder>> ListSmartFoldersAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<SmartFolder> SaveSmartFolderAsync(SmartFolder folder, IEnumerable<SmartFolderRule> rules, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SmartFolderRule>> ListSmartFolderRulesAsync(Guid smartFolderId, CancellationToken cancellationToken = default);
    Task<SmartFolderQueryDocument?> GetSmartFolderQueryDocumentAsync(Guid smartFolderId, CancellationToken cancellationToken = default);
    Task<SmartFolder> SaveSmartFolderQueryDocumentAsync(SmartFolder folder, AssetQueryDocument document, CancellationToken cancellationToken = default);
    Task<SmartFolder> CopySmartFolderAsync(Guid smartFolderId, string? copyName = null, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> SetSmartFolderArchivedAsync(Guid smartFolderId, bool isArchived, CancellationToken cancellationToken = default);

    Task<AssetBatchMetadataPreview> PreviewBatchMetadataAsync(AssetBatchMetadataRequest request, CancellationToken cancellationToken = default);
    Task<AssetLibraryBatchResult> ApplyBatchMetadataAsync(AssetBatchMetadataRequest request, AssetBatchMetadataPreview previewContract, CancellationToken cancellationToken = default);

    Task<AssetRelinkResult> RelinkMissingAssetsAsync(AssetRelinkRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetUndoJournalEntry>> ListUndoJournalAsync(int limit = 100, CancellationToken cancellationToken = default);

    Task<bool> UndoAsync(AssetLibraryUndoToken token, CancellationToken cancellationToken = default);
    Task<bool> RedoAsync(AssetLibraryUndoToken token, CancellationToken cancellationToken = default);
}
public sealed class AssetLibraryDatabase
{
    public AssetLibraryDatabase(string databasePath)
    {
        DatabasePath = Path.GetFullPath(databasePath);
    }

    public string DatabasePath { get; }

    public async Task<Microsoft.Data.Sqlite.SqliteConnection> OpenConnectionAsync(bool write = false, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(DatabasePath);
        if (write && !string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = write ? Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate : Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
            Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Shared,
            Pooling = true,
            DefaultTimeout = 5
        };
        var connection = new Microsoft.Data.Sqlite.SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        connection.CreateFunction<string, string, bool>("regexp", (pattern, input) =>
        {
            if (string.IsNullOrEmpty(pattern) || input is null) return false;
            try
            {
                return System.Text.RegularExpressions.Regex.IsMatch(
                    input,
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException)
            {
                return false;
            }
        });
        connection.CreateFunction<string, string, bool>("regexp_cs", (pattern, input) =>
        {
            if (string.IsNullOrEmpty(pattern) || input is null) return false;
            try { return System.Text.RegularExpressions.Regex.IsMatch(input, pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)); }
            catch (ArgumentException) { return false; }
        });
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        await pragma.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
