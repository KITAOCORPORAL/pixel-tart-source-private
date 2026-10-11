using RAWSelectionAssistant.Core.Models;
using Microsoft.Data.Sqlite;
namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

public sealed record PhotographerAnnotation(Guid AssetId, int Rating, string Color, string Note, long Revision = 0);
public sealed record AnnotationWriteResult(bool Applied, PhotographerAnnotation Current,AssetLibraryUndoToken? UndoToken=null);
/// <summary>Value-based CAS includes every photographer field. Customer fields are never read or written.</summary>
public sealed class PhotographerAnnotationStore(AssetLibraryDatabase database)
{
    public async Task<PhotographerAnnotation> ReadAsync(Guid id, CancellationToken token = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, token);
        await EnsureAsync(connection,token);
        return await ReadAsync(connection, null, id, token);
    }
    public async Task<AnnotationWriteResult> CompareExchangeAsync(PhotographerAnnotation expected, PhotographerAnnotation desired, CancellationToken token = default,AssetLibraryUndoToken? operation=null)
    {
        if (expected.AssetId != desired.AssetId || desired.Rating is < 0 or > 5 || desired.Color is not ("" or "红" or "橙" or "黄" or "绿" or "蓝" or "紫")) throw new ArgumentException("标注身份或星级无效。");
        await using var connection = await database.OpenConnectionAsync(write: true, token);
        await EnsureAsync(connection,token);
        using var transaction = connection.BeginTransaction(deferred: false);
        var current = await ReadAsync(connection, transaction, expected.AssetId, token);
        if (current != expected) return new(false,current);
        if(current.Rating==desired.Rating&&current.Color==desired.Color&&current.Note==desired.Note)return new(true,current);
        await using var command = connection.CreateCommand(); command.Transaction=transaction;
        command.CommandText="UPDATE AssetItems SET Rating=$rating,Comment=$note WHERE AssetId=$id; INSERT INTO AssetPresentationMetadata(AssetId,Color) VALUES($id,$color) ON CONFLICT(AssetId) DO UPDATE SET Color=excluded.Color;";
        command.Parameters.AddWithValue("$id",desired.AssetId.ToString("D")); command.Parameters.AddWithValue("$rating",desired.Rating);
        command.Parameters.AddWithValue("$note",desired.Note); command.Parameters.AddWithValue("$color",desired.Color);
        await command.ExecuteNonQueryAsync(token); var committed=await ReadAsync(connection,transaction,desired.AssetId,token); operation??=new(Guid.NewGuid(),"修改摄影师标注",DateTimeOffset.UtcNow);
        await AnnotationCasJournal.AppendAsync(connection,transaction,operation,new(current,committed),token);transaction.Commit(); return new(true,committed,operation);
    }
    public async Task<IReadOnlyList<AnnotationBatchItemResult>> ApplyBatchAsync(IEnumerable<PhotographerAnnotation> expected,int? rating=null,string? color=null,string? note=null,CancellationToken token=default)
    {
        var results=new List<AnnotationBatchItemResult>();var operation=new AssetLibraryUndoToken(Guid.NewGuid(),"批量修改摄影师标注",DateTimeOffset.UtcNow);
        foreach(var item in expected)
        {
            token.ThrowIfCancellationRequested();
            try{var result=await CompareExchangeAsync(item,item with {Rating=rating??item.Rating,Color=color??item.Color,Note=note??item.Note},token,operation);results.Add(new(item.AssetId,result.Applied?"Success":"Conflict",result.Current,UndoToken:result.UndoToken));}
            catch(Exception e)when(e is IOException or SqliteException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException){results.Add(new(item.AssetId,"Failed",null,e.GetType().Name));}
        }
        return results;
    }
    internal static async Task EnsureAsync(SqliteConnection connection,CancellationToken token)
    {
        await using var command=connection.CreateCommand();command.CommandText="""
        CREATE TABLE IF NOT EXISTS PhotographerAnnotationVersions(AssetId TEXT PRIMARY KEY,Revision INTEGER NOT NULL DEFAULT 0);
        CREATE TRIGGER IF NOT EXISTS PhotographerRatingRevision AFTER UPDATE OF Rating,Comment ON AssetItems BEGIN
        INSERT INTO PhotographerAnnotationVersions VALUES(NEW.AssetId,1) ON CONFLICT(AssetId) DO UPDATE SET Revision=Revision+1; END;
        CREATE TRIGGER IF NOT EXISTS PhotographerColorRevision AFTER UPDATE OF Color ON AssetPresentationMetadata BEGIN
        INSERT INTO PhotographerAnnotationVersions VALUES(NEW.AssetId,1) ON CONFLICT(AssetId) DO UPDATE SET Revision=Revision+1; END;
        CREATE TRIGGER IF NOT EXISTS PhotographerColorInsertRevision AFTER INSERT ON AssetPresentationMetadata BEGIN
        INSERT INTO PhotographerAnnotationVersions VALUES(NEW.AssetId,1) ON CONFLICT(AssetId) DO UPDATE SET Revision=Revision+1; END;
        """;await command.ExecuteNonQueryAsync(token);
    }
    private static async Task<PhotographerAnnotation> ReadAsync(SqliteConnection connection, SqliteTransaction? transaction, Guid id, CancellationToken token)
    {
        await using var command=connection.CreateCommand(); command.Transaction=transaction;
        command.CommandText="SELECT a.Rating,COALESCE(p.Color,''),a.Comment,COALESCE(v.Revision,0) FROM AssetItems a LEFT JOIN AssetPresentationMetadata p ON p.AssetId=a.AssetId LEFT JOIN PhotographerAnnotationVersions v ON v.AssetId=a.AssetId WHERE a.AssetId=$id;";
        command.Parameters.AddWithValue("$id",id.ToString("D")); await using var reader=await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw new KeyNotFoundException("素材不存在。");
        return new(id,reader.GetInt32(0),reader.GetString(1),reader.GetString(2),reader.GetInt64(3));
    }
}
public sealed record AnnotationBatchItemResult(Guid AssetId,string Status,PhotographerAnnotation? Current,string? Error=null,AssetLibraryUndoToken? UndoToken=null);
