using System.Security.Cryptography;
namespace RAWSelectionAssistant.Core.Services.AssetLibrary;
public sealed record AssetRelocationResult(AssetResolutionStatus Status, string? Path, string Message);
public sealed class AssetRelocationService(AssetLibraryContainerDescriptor library)
{
    public async Task<AssetRelocationResult> RelocateAsync(AssetResolutionRequest identity, string candidate, CancellationToken token=default)
    {
        if(identity.LibraryId!=library.LibraryId) return new(AssetResolutionStatus.Ambiguous,null,"请选择身份对应的素材库；跨库引用不能直接改写。");
        if(!File.Exists(library.DatabasePath)) return new(AssetResolutionStatus.Offline,null,"素材库离线。");
        await using var repo=new SqliteAssetLibraryRepository(library.DatabasePath);
        var asset=await repo.GetAssetAsync(identity.AssetId,token);
        if(asset is null) return new(AssetResolutionStatus.Missing,null,"素材记录不存在。");
        var expected=identity.NormalizedExpectedContentHash??asset.ContentHash?.ToLowerInvariant();
        if(expected is null) return new(AssetResolutionStatus.Ambiguous,null,"缺少历史内容 hash，不能仅凭文件名确认。");
        if(!File.Exists(candidate)) return new(AssetResolutionStatus.Offline,null,"候选文件离线或不存在。");
        candidate=Path.GetFullPath(candidate);
        await using var source=new FileStream(candidate,FileMode.Open,FileAccess.Read,FileShare.Read);
        var hash=Convert.ToHexString(await SHA256.HashDataAsync(source,token)).ToLowerInvariant();
        if(hash!=expected) return new(AssetResolutionStatus.Changed,null,"候选内容与历史 hash 不一致。");
        await using var connection=await new AssetLibraryDatabase(library.DatabasePath).OpenConnectionAsync(write:true,token);
        using var transaction=connection.BeginTransaction(deferred:false);
        await using var command=connection.CreateCommand(); command.Transaction=transaction;
        command.CommandText="CREATE TABLE IF NOT EXISTS AssetRelocations(Id TEXT PRIMARY KEY,AssetId TEXT,OldPath TEXT,NewPath TEXT,ContentHash TEXT,AtUtc TEXT); UPDATE AssetItems SET SourcePath=$path,NormalizedSourcePath=$normalized,IsMissing=0 WHERE AssetId=$id AND SourcePath=$old AND (ContentHash IS NULL OR lower(ContentHash)=$expected);";
        command.Parameters.AddWithValue("$path",candidate); command.Parameters.AddWithValue("$normalized",OperatingSystem.IsWindows()?candidate.ToUpperInvariant():candidate);
        command.Parameters.AddWithValue("$expected",expected);
        command.Parameters.AddWithValue("$id",asset.AssetId.ToString("D")); command.Parameters.AddWithValue("$old",asset.SourcePath);
        if(await command.ExecuteNonQueryAsync(token)!=1) return new(AssetResolutionStatus.Ambiguous,null,"路径已被另一操作改变，请重新读取。");
        command.CommandText="INSERT INTO AssetRelocations VALUES($operation,$id,$old,$path,$hash,$at);";
        command.Parameters.AddWithValue("$operation",Guid.NewGuid().ToString("D"));command.Parameters.AddWithValue("$hash",hash);command.Parameters.AddWithValue("$at",DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(token);transaction.Commit();return new(AssetResolutionStatus.Available,candidate,"已按内容确认重新定位，素材身份与引用保留。");
    }
}
