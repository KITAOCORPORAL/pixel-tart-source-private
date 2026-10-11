using Microsoft.Data.Sqlite;
namespace RAWSelectionAssistant.Core.Services.AssetLibrary;
public static class CrossLibraryRelocation
{
    public static async Task<AssetResolutionResult> LinkAsync(AssetLibraryContainerDescriptor source,AssetResolutionRequest identity,
        AssetLibraryContainerDescriptor target,Guid targetAssetId,CancellationToken token=default)
    {
        if(source.LibraryId!=identity.LibraryId || source.LibraryId==target.LibraryId)throw new ArgumentException("跨库定位必须明确两个不同素材库。");
        await using var sourceRepo=new SqliteAssetLibraryRepository(source.DatabasePath);
        var original=await sourceRepo.GetAssetAsync(identity.AssetId,token)??throw new KeyNotFoundException("源身份不存在。");
        var expected=identity.NormalizedExpectedContentHash??original.ContentHash;
        if(string.IsNullOrWhiteSpace(expected))return new(AssetResolutionStatus.Ambiguous,identity.LibraryId,identity.AssetId,Message:"缺少源 hash，不能确认跨库映射。");
        await using var targetRepo=new SqliteAssetLibraryRepository(target.DatabasePath);
        var verified=await new AssetLibraryIdentityResolver(target,targetRepo).ResolveAsync(new(target.LibraryId,targetAssetId,expected),token);
        if(verified.Status!=AssetResolutionStatus.Available)return verified;
        await using var connection=await new AssetLibraryDatabase(source.DatabasePath).OpenConnectionAsync(write:true,token);
        using var transaction=connection.BeginTransaction(deferred:false);await using var cmd=connection.CreateCommand();cmd.Transaction=transaction;
        cmd.CommandText="CREATE TABLE IF NOT EXISTS CrossLibraryRelocations(SourceAssetId TEXT PRIMARY KEY,TargetLibraryId TEXT,TargetAssetId TEXT,TargetContainer TEXT,ContentHash TEXT,CreatedAt TEXT); INSERT INTO CrossLibraryRelocations VALUES($source,$library,$asset,$container,$hash,$at) ON CONFLICT(SourceAssetId) DO NOTHING;";
        cmd.Parameters.AddWithValue("$source",identity.AssetId.ToString("D"));cmd.Parameters.AddWithValue("$library",target.LibraryId.ToString("D"));cmd.Parameters.AddWithValue("$asset",targetAssetId.ToString("D"));cmd.Parameters.AddWithValue("$container",target.ContainerPath);cmd.Parameters.AddWithValue("$hash",expected.ToLowerInvariant());cmd.Parameters.AddWithValue("$at",DateTimeOffset.UtcNow.ToString("O"));
        if(await cmd.ExecuteNonQueryAsync(token)!=1)return new(AssetResolutionStatus.Ambiguous,identity.LibraryId,identity.AssetId,Message:"已有跨库映射，需人工复核，不覆盖。");
        transaction.Commit();return verified with{LibraryId=identity.LibraryId,AssetId=identity.AssetId};
    }
    public static async Task<AssetResolutionResult?> ResolveAsync(AssetLibraryContainerDescriptor source,AssetResolutionRequest identity,CancellationToken token)
    {
        await using var connection=await new AssetLibraryDatabase(source.DatabasePath).OpenConnectionAsync(cancellationToken:token);
        await using var cmd=connection.CreateCommand();cmd.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='CrossLibraryRelocations';";
        if(Convert.ToInt32(await cmd.ExecuteScalarAsync(token))==0)return null;
        cmd.CommandText="SELECT TargetLibraryId,TargetAssetId,TargetContainer,ContentHash FROM CrossLibraryRelocations WHERE SourceAssetId=$id;";cmd.Parameters.AddWithValue("$id",identity.AssetId.ToString("D"));
        await using var reader=await cmd.ExecuteReaderAsync(token);if(!await reader.ReadAsync(token))return null;
        var libraryId=Guid.Parse(reader.GetString(0));var assetId=Guid.Parse(reader.GetString(1));var root=reader.GetString(2);var hash=reader.GetString(3);
        if(identity.NormalizedExpectedContentHash is {} required && required!=hash)return new(AssetResolutionStatus.Changed,identity.LibraryId,identity.AssetId,Message:"跨库映射 hash 与引用不一致。");
        try
        {
            var target=await new AssetLibraryContainerService().OpenAsync(root,token);
            if(target.LibraryId!=libraryId)return new(AssetResolutionStatus.Ambiguous,identity.LibraryId,identity.AssetId,Message:"目标库身份改变。");
            await using var repo=new SqliteAssetLibraryRepository(target.DatabasePath);
            var result=await new AssetLibraryIdentityResolver(target,repo).ResolveLocalAsync(new(libraryId,assetId,hash),token);
            return result with{LibraryId=identity.LibraryId,AssetId=identity.AssetId};
        }
        catch(IOException){return new(AssetResolutionStatus.Offline,identity.LibraryId,identity.AssetId,Message:"映射目标库离线。");}
    }
}
