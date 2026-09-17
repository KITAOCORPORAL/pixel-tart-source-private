namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

public sealed record AssetPresentationMetadata(Guid AssetId, string Url = "", string Color = "");

/// <summary>Library-only annotation data. Never opens an asset source for writing.</summary>
public sealed class AssetPresentationMetadataStore(AssetLibraryDatabase database)
{
    public async Task<AssetPresentationMetadata> GetAsync(Guid assetId, CancellationToken token = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, token).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS AssetPresentationMetadata(AssetId TEXT PRIMARY KEY,Url TEXT NOT NULL DEFAULT '',Color TEXT NOT NULL DEFAULT '');";
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        command.CommandText = "SELECT Url,Color FROM AssetPresentationMetadata WHERE AssetId=$id;";
        command.Parameters.AddWithValue("$id", assetId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        return await reader.ReadAsync(token).ConfigureAwait(false) ? new(assetId, reader.GetString(0), reader.GetString(1)) : new(assetId);
    }

    public async Task SaveAsync(IEnumerable<Guid> assetIds, string? url = null, string? color = null, CancellationToken token = default)
    {
        var ids = assetIds.Distinct().ToArray();
        if (ids.Length == 0) return;
        if (url is not null && url.Length > 4096) throw new ArgumentException("链接过长。");
        if (color is not null && color is not ("" or "红" or "橙" or "黄" or "绿" or "蓝" or "紫")) throw new ArgumentException("未知颜色标记。");
        await GetAsync(ids[0], token).ConfigureAwait(false);
        await using var connection = await database.OpenConnectionAsync(write: true, token).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction();
        foreach (var id in ids)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO AssetPresentationMetadata(AssetId,Url,Color) VALUES($id,COALESCE($url,''),COALESCE($color,'')) ON CONFLICT(AssetId) DO UPDATE SET Url=COALESCE($url,Url),Color=COALESCE($color,Color);";
            command.Parameters.AddWithValue("$id", id.ToString("D"));
            command.Parameters.AddWithValue("$url", (object?)url ?? DBNull.Value);
            command.Parameters.AddWithValue("$color", (object?)color ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }
        await transaction.CommitAsync(token).ConfigureAwait(false);
    }
}
