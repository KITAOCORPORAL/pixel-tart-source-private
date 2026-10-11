using Microsoft.Data.Sqlite;
namespace RAWSelectionAssistant.Core.Services.AssetLibrary;
public sealed partial class SqliteAssetLibraryRepository
{
    private static async Task CaptureUndoVersionsAsync(SqliteConnection connection,SqliteTransaction transaction,Guid operation,IEnumerable<Guid> assets,CancellationToken token)
    {
        await using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="CREATE TABLE IF NOT EXISTS AnnotationUndoVersions(OperationId TEXT,AssetId TEXT,Revision INTEGER,PRIMARY KEY(OperationId,AssetId));";await command.ExecuteNonQueryAsync(token);
        foreach(var asset in assets.Distinct())
        {
            command.Parameters.Clear();command.CommandText="INSERT INTO AnnotationUndoVersions SELECT $op,$asset,COALESCE((SELECT Revision FROM PhotographerAnnotationVersions WHERE AssetId=$asset),0) ON CONFLICT(OperationId,AssetId) DO UPDATE SET Revision=excluded.Revision;";
            command.Parameters.AddWithValue("$op",operation.ToString("D"));command.Parameters.AddWithValue("$asset",asset.ToString("D"));await command.ExecuteNonQueryAsync(token);
        }
    }
    private static async Task<bool> UndoVersionsMatchAsync(SqliteConnection connection,SqliteTransaction transaction,Guid operation,string kind,CancellationToken token)
    {
        if(kind is not ("asset-metadata-v2" or "asset-batch-metadata-v2" or "annotation-cas-v3"))return true;
        await using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='AnnotationUndoVersions';";if(Convert.ToInt32(await command.ExecuteScalarAsync(token))==0)return false;
        command.CommandText="SELECT COUNT(*),SUM(CASE WHEN u.Revision!=COALESCE(v.Revision,0) THEN 1 ELSE 0 END) FROM AnnotationUndoVersions u LEFT JOIN PhotographerAnnotationVersions v ON v.AssetId=u.AssetId WHERE u.OperationId=$id;";command.Parameters.AddWithValue("$id",operation.ToString("D"));
        await using var reader=await command.ExecuteReaderAsync(token);await reader.ReadAsync(token);return reader.GetInt32(0)>0&&(reader.IsDBNull(1)||reader.GetInt32(1)==0);
    }
    private static async Task RefreshUndoVersionsAsync(SqliteConnection connection,SqliteTransaction transaction,Guid operation,string kind,CancellationToken token)
    {
        if(kind is not ("asset-metadata-v2" or "asset-batch-metadata-v2" or "annotation-cas-v3"))return;
        if(kind==AnnotationCasJournal.Kind)
        {
            await using var read=connection.CreateCommand();read.Transaction=transaction;read.CommandText="SELECT PayloadJson FROM AssetLibraryUndoJournal WHERE OperationId=$id;";read.Parameters.AddWithValue("$id",operation.ToString("D"));
            var changes=System.Text.Json.JsonSerializer.Deserialize<List<AnnotationCasChange>>((string)(await read.ExecuteScalarAsync(token))!)!;
            foreach(var change in changes)
            {
                await using var fence=connection.CreateCommand();fence.Transaction=transaction;fence.CommandText="SELECT Revision FROM AnnotationUndoVersions WHERE OperationId=$op AND AssetId=$asset;";fence.Parameters.AddWithValue("$op",operation.ToString("D"));fence.Parameters.AddWithValue("$asset",change.After.AssetId.ToString("D"));var priorFence=Convert.ToInt64(await fence.ExecuteScalarAsync(token));
                await using var update=connection.CreateCommand();update.Transaction=transaction;
                update.CommandText="UPDATE AnnotationUndoVersions SET Revision=COALESCE((SELECT Revision FROM PhotographerAnnotationVersions WHERE AssetId=$asset),0) WHERE AssetId=$asset AND (OperationId=$operation OR Revision=$before OR Revision=$after OR Revision=$prior);";
                update.Parameters.AddWithValue("$asset",change.After.AssetId.ToString("D"));update.Parameters.AddWithValue("$operation",operation.ToString("D"));update.Parameters.AddWithValue("$prior",priorFence);update.Parameters.AddWithValue("$before",change.Before.Revision);update.Parameters.AddWithValue("$after",change.After.Revision);await update.ExecuteNonQueryAsync(token);
            }
        }        await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="UPDATE AnnotationUndoVersions SET Revision=COALESCE((SELECT Revision FROM PhotographerAnnotationVersions v WHERE v.AssetId=AnnotationUndoVersions.AssetId),0) WHERE EXISTS(SELECT 1 FROM AnnotationUndoVersions active WHERE active.OperationId=$id AND active.AssetId=AnnotationUndoVersions.AssetId AND active.Revision=AnnotationUndoVersions.Revision);";command.Parameters.AddWithValue("$id",operation.ToString("D"));await command.ExecuteNonQueryAsync(token);
    }
}
