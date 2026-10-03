using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

public sealed partial class SqliteAssetLibraryRepository
{
    // Library metadata only. Source files and external project documents are never deleted here.
    public async Task<int> DeleteTrashedAssetRecordsAsync(IEnumerable<Guid> assetIds, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var changed = 0;
        foreach (var id in assetIds.Where(id => id != Guid.Empty).Distinct())
        {
            await using var remove = connection.CreateCommand(); remove.Transaction = transaction;
            remove.CommandText = "DELETE FROM AssetItems WHERE AssetId=$id AND EXISTS(SELECT 1 FROM AssetTrashEntries WHERE AssetId=$id);";
            remove.Parameters.AddWithValue("$id", id.ToString("D"));
            if (await remove.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 0) continue;
            changed++;
            await ExecuteAsync(connection, transaction, "DELETE FROM AssetPresentationMetadata WHERE AssetId=$id; DELETE FROM AssetLibraryUndoJournal WHERE PayloadJson LIKE $pattern;", cancellationToken, ("$id", id.ToString("D")), ("$pattern", "%" + id.ToString("D") + "%")).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        if (changed > 0) _undo.Clear();
        return changed;
    }

    public async Task<int> DeleteFolderDefinitionAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        // Children retain their own membership and order and move to the deleted folder's parent.
        await ExecuteAsync(connection, transaction, "UPDATE AssetFolders SET ParentFolderId=(SELECT ParentFolderId FROM AssetFolders WHERE FolderId=$id) WHERE ParentFolderId=$id AND EXISTS(SELECT 1 FROM AssetFolders WHERE FolderId=$id AND IsSystem=0);", cancellationToken, ("$id", folderId.ToString("D"))).ConfigureAwait(false);
        await using var remove = connection.CreateCommand(); remove.Transaction=transaction;
        remove.CommandText="DELETE FROM AssetFolders WHERE FolderId=$id AND IsSystem=0;";remove.Parameters.AddWithValue("$id",folderId.ToString("D"));
        var changed=await remove.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if(changed>0) await ExecuteAsync(connection,transaction,"DELETE FROM AssetLibraryUndoJournal WHERE PayloadJson LIKE $pattern;",cancellationToken,("$pattern","%"+folderId.ToString("D")+"%")).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        if(changed>0)_undo.Clear();
        return changed;
    }
}
