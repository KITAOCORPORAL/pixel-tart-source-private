using Microsoft.Data.Sqlite;
namespace RAWSelectionAssistant.Core.Services.AssetLibrary;
/// <summary>Read-only opt-in SQL timing observation; no cache, SQL, or threshold changes.</summary>
public static class AssetLibraryQueryDiagnostics
{
 public sealed record Query(string CountSql,string PageSql,Dictionary<string,object?> CountParameters,Dictionary<string,object?> PageParameters,double CountMs,double PageMs,int Total);
 private static readonly AsyncLocal<Action<Query>?> Sink=new();
 public static IDisposable Capture(Action<Query> callback){var old=Sink.Value;Sink.Value=callback;return new Scope(()=>Sink.Value=old);}
 internal static void Emit(SqliteCommand count,SqliteCommand page,double countMs,double pageMs,int total)
 {if(Sink.Value is {} sink)sink(new(count.CommandText,page.CommandText,count.Parameters.Cast<SqliteParameter>().ToDictionary(p=>p.ParameterName,p=>p.Value is DBNull?null:p.Value),page.Parameters.Cast<SqliteParameter>().ToDictionary(p=>p.ParameterName,p=>p.Value is DBNull?null:p.Value),countMs,pageMs,total));}
 private sealed class Scope(Action close):IDisposable{public void Dispose()=>close();}
}
