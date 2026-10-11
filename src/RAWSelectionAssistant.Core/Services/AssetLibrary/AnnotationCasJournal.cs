using Microsoft.Data.Sqlite;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
namespace RAWSelectionAssistant.Core.Services.AssetLibrary;
public sealed record AnnotationCasChange(PhotographerAnnotation Before,PhotographerAnnotation After);
internal static class AnnotationCasJournal
{
    internal const string Kind="annotation-cas-v3";
    internal static async Task AppendAsync(SqliteConnection connection,SqliteTransaction transaction,AssetLibraryUndoToken operation,AnnotationCasChange change,CancellationToken token)
    {
        await using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="CREATE TABLE IF NOT EXISTS AnnotationUndoVersions(OperationId TEXT,AssetId TEXT,Revision INTEGER,PRIMARY KEY(OperationId,AssetId)); SELECT PayloadJson FROM AssetLibraryUndoJournal WHERE OperationId=$op;";command.Parameters.AddWithValue("$op",operation.OperationId.ToString("D"));
        var json=await command.ExecuteScalarAsync(token) as string;var changes=json is null?new List<AnnotationCasChange>():JsonSerializer.Deserialize<List<AnnotationCasChange>>(json)!;changes.Add(change);
        command.CommandText="INSERT INTO AssetLibraryUndoJournal(OperationId,Description,OperationKind,PayloadJson,CreatedAt,UndoneAt,JournalVersion) VALUES($op,$description,$kind,$payload,$created,NULL,3) ON CONFLICT(OperationId) DO UPDATE SET PayloadJson=excluded.PayloadJson; INSERT INTO AnnotationUndoVersions VALUES($op,$asset,$revision) ON CONFLICT(OperationId,AssetId) DO UPDATE SET Revision=excluded.Revision;";
        command.Parameters.AddWithValue("$description",operation.Description);command.Parameters.AddWithValue("$kind",Kind);command.Parameters.AddWithValue("$payload",JsonSerializer.Serialize(changes));command.Parameters.AddWithValue("$created",operation.CreatedAt.ToString("O"));command.Parameters.AddWithValue("$asset",change.After.AssetId.ToString("D"));command.Parameters.AddWithValue("$revision",change.After.Revision);await command.ExecuteNonQueryAsync(token);
        command.CommandText="DELETE FROM AssetLibraryUndoJournal WHERE OperationId NOT IN(SELECT OperationId FROM AssetLibraryUndoJournal ORDER BY CreatedAt DESC,rowid DESC LIMIT 100); DELETE FROM AnnotationUndoVersions WHERE OperationId NOT IN(SELECT OperationId FROM AssetLibraryUndoJournal);";await command.ExecuteNonQueryAsync(token);
    }
    internal static async Task<bool> RestoreAsync(SqliteConnection connection,SqliteTransaction transaction,string json,bool redo,CancellationToken token)
    {
        var changes=JsonSerializer.Deserialize<List<AnnotationCasChange>>(json)??throw new InvalidDataException("标注日志无效。");
        foreach(var change in changes)
        {
            var expected=redo?change.Before:change.After;var desired=redo?change.After:change.Before;
            await using var command=connection.CreateCommand();command.Transaction=transaction;
            command.CommandText="SELECT COUNT(*) FROM AssetItems a WHERE a.AssetId=$id AND a.Rating=$oldRating AND a.Comment=$oldNote AND COALESCE((SELECT Color FROM AssetPresentationMetadata WHERE AssetId=$id),'')=$oldColor;";
            command.Parameters.AddWithValue("$id",desired.AssetId.ToString("D"));command.Parameters.AddWithValue("$oldRating",expected.Rating);command.Parameters.AddWithValue("$oldNote",expected.Note);command.Parameters.AddWithValue("$oldColor",expected.Color);
            if(Convert.ToInt32(await command.ExecuteScalarAsync(token))!=1)return false;
            command.CommandText="UPDATE AssetItems SET Rating=$rating,Comment=$note WHERE AssetId=$id; INSERT INTO AssetPresentationMetadata(AssetId,Color) VALUES($id,$color) ON CONFLICT(AssetId) DO UPDATE SET Color=excluded.Color;";
            command.Parameters.AddWithValue("$rating",desired.Rating);command.Parameters.AddWithValue("$note",desired.Note);command.Parameters.AddWithValue("$color",desired.Color);await command.ExecuteNonQueryAsync(token);
        }
        return true;
    }
}
