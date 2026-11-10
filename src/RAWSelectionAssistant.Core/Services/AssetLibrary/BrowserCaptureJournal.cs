using Microsoft.Data.Sqlite;
using System.Text.Json;
namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

public sealed record BrowserCaptureRecord(Guid CaptureId, Guid LibraryId, Guid? ProjectId, Guid? BoardId,
    string PageUrl, string ImageUrl, string PageTitle, DateTimeOffset CapturedAtUtc, string ContentHash,
    Guid? AssetId = null, string Outcome = "Pending", DateTimeOffset? ImportedAtUtc = null);
/// <summary>Application-owned operation and provenance journal keyed by stable library/asset identity.</summary>
public sealed class BrowserCaptureJournal(string databasePath)
{
    public string DatabasePath => databasePath;
    private async Task<SqliteConnection> Open(CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=databasePath, Pooling=false }.ToString());
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand(); command.CommandText = """
            PRAGMA busy_timeout=5000;
            CREATE TABLE IF NOT EXISTS BrowserCaptures(CaptureId TEXT PRIMARY KEY, Fingerprint TEXT NOT NULL,
                LibraryId TEXT NOT NULL, AssetId TEXT NULL, CapturedAtUtc TEXT NOT NULL, State TEXT NOT NULL, RecordJson TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS IX_BrowserCaptures_Asset ON BrowserCaptures(LibraryId,AssetId,CapturedAtUtc);
            """;
        await command.ExecuteNonQueryAsync(token); return connection;
    }
    public async Task<BrowserCaptureRecord> ReserveAsync(BrowserCaptureRecord record, string fingerprint, CancellationToken token=default)
    {
        await using var c=await Open(token); using var tx=c.BeginTransaction(deferred:false);
        await using(var insert=c.CreateCommand()) {
            insert.Transaction=tx; insert.CommandText="INSERT OR IGNORE INTO BrowserCaptures VALUES($id,$finger,$library,NULL,$time,'Pending',$json);";
            insert.Parameters.AddWithValue("$id",record.CaptureId.ToString("D"));insert.Parameters.AddWithValue("$finger",fingerprint);insert.Parameters.AddWithValue("$library",record.LibraryId.ToString("D"));insert.Parameters.AddWithValue("$time",record.CapturedAtUtc.ToString("O"));insert.Parameters.AddWithValue("$json",JsonSerializer.Serialize(record));await insert.ExecuteNonQueryAsync(token);
        }
        BrowserCaptureRecord saved;
        await using(var query=c.CreateCommand()) {
            query.Transaction=tx;query.CommandText="SELECT Fingerprint,RecordJson FROM BrowserCaptures WHERE CaptureId=$id;";query.Parameters.AddWithValue("$id",record.CaptureId.ToString("D"));
            await using var reader=await query.ExecuteReaderAsync(token);await reader.ReadAsync(token);
            if(reader.GetString(0)!=fingerprint)throw new InvalidDataException("同一次采集的图片或目的地已变化，请明确重新选择并发起新采集。");
            saved=JsonSerializer.Deserialize<BrowserCaptureRecord>(reader.GetString(1))!;
        }
        tx.Commit();return saved;
    }
    public async Task CompleteAsync(BrowserCaptureRecord record, CancellationToken token=default)
    {
        if(record.AssetId is null||record.AssetId==Guid.Empty||record.ImportedAtUtc is null||record.Outcome=="Pending")throw new ArgumentException("Only a committed asset can have a receipt.");
        await using var c=await Open(token);await using var command=c.CreateCommand();command.CommandText="UPDATE BrowserCaptures SET AssetId=$asset,State='Completed',RecordJson=$json WHERE CaptureId=$id;";
        command.Parameters.AddWithValue("$asset",record.AssetId.Value.ToString("D"));command.Parameters.AddWithValue("$json",JsonSerializer.Serialize(record));command.Parameters.AddWithValue("$id",record.CaptureId.ToString("D"));if(await command.ExecuteNonQueryAsync(token)!=1)throw new InvalidDataException("Capture reservation is missing.");
    }
    public async Task<BrowserCaptureRecord?> LatestSourceAsync(Guid libraryId, Guid assetId, CancellationToken token=default)
    {
        await using var c=await Open(token);await using var command=c.CreateCommand();command.CommandText="SELECT RecordJson FROM BrowserCaptures WHERE LibraryId=$library AND AssetId=$asset AND State='Completed' ORDER BY CapturedAtUtc DESC,CaptureId LIMIT 1;";
        command.Parameters.AddWithValue("$library",libraryId.ToString("D"));command.Parameters.AddWithValue("$asset",assetId.ToString("D"));var value=await command.ExecuteScalarAsync(token);return value is string text?JsonSerializer.Deserialize<BrowserCaptureRecord>(text):null;
    }
}
