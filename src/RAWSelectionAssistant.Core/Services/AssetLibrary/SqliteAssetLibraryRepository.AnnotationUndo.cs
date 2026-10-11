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
        if(kind is not ("asset-metadata-v2" or "asset-batch-metadata-v2"))return true;
        await using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='AnnotationUndoVersions';";if(Convert.ToInt32(await command.ExecuteScalarAsync(token))==0)return false;
        command.CommandText="SELECT COUNT(*),SUM(CASE WHEN u.Revision!=COALESCE(v.Revision,0) THEN 1 ELSE 0 END) FROM AnnotationUndoVersions u LEFT JOIN PhotographerAnnotationVersions v ON v.AssetId=u.AssetId WHERE u.OperationId=$id;";command.Parameters.AddWithValue("$id",operation.ToString("D"));
        await using var reader=await command.ExecuteReaderAsync(token);await reader.ReadAsync(token);return reader.GetInt32(0)>0&&(reader.IsDBNull(1)||reader.GetInt32(1)==0);
    }
    private static async Task RefreshUndoVersionsAsync(SqliteConnection connection,SqliteTransaction transaction,Guid operation,string kind,CancellationToken token)
    {
        if(kind is not ("asset-metadata-v2" or "asset-batch-metadata-v2"))return;
        await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="UPDATE AnnotationUndoVersions SET Revision=COALESCE((SELECT Revision FROM PhotographerAnnotationVersions v WHERE v.AssetId=AnnotationUndoVersions.AssetId),0) WHERE OperationId=$id;";command.Parameters.AddWithValue("$id",operation.ToString("D"));await command.ExecuteNonQueryAsync(token);
    }
}
