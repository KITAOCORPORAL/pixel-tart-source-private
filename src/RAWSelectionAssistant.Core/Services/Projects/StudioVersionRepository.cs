using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record StudioVersionCatalog(string Identity,long Revision,IReadOnlyList<StudioEditVersion> Versions,Guid? SelectedId,int Version=1);
public sealed class StudioVersionRepository(string directory)
{
    public static string Identity(Guid? library,Guid? asset,string hash)
    {
        if(hash.Length!=64||!hash.All(Uri.IsHexDigit))throw new ArgumentException("版本目录需要内容 SHA256。");
        return library.HasValue&&asset.HasValue?$"library:{library:D}:{asset:D}:{hash.ToLowerInvariant()}":"external-sha256:"+hash.ToLowerInvariant();
    }
    public static Guid DefaultVersionId(string identity)=>new(SHA256.HashData(Encoding.UTF8.GetBytes("default:"+identity)).AsSpan(0,16));
    public static bool SameContent(StudioVersionCatalog catalog,IReadOnlyList<StudioEditVersion> versions,Guid? selected)=>catalog.SelectedId==selected&&JsonSerializer.Serialize(catalog.Versions)==JsonSerializer.Serialize(versions);
    public async Task<StudioVersionCatalog> CopySelectedAsync(string sourceIdentity,Guid versionId,StudioVersionCatalog target,Guid targetLibrary,Guid targetAsset,string verifiedHash,CancellationToken token=default)
    {
        var source=await LoadAsync(sourceIdentity,token);var version=source.Versions.Single(v=>v.Id==versionId);
        if(!string.Equals(version.SourceHash,verifiedHash,StringComparison.OrdinalIgnoreCase)||Identity(targetLibrary,targetAsset,verifiedHash)!=target.Identity)throw new InvalidDataException("跨库版本源内容不一致。");
        var existing=target.Versions.FirstOrDefault(v=>v.OriginIdentity==sourceIdentity&&v.OriginVersionId==versionId);
        if(existing is not null)return target;
        var copied=version with{Id=Guid.NewGuid(),LibraryId=targetLibrary,AssetId=targetAsset,SourceHash=verifiedHash,OriginIdentity=sourceIdentity,OriginVersionId=versionId,CreatedAt=DateTimeOffset.UtcNow,Name=version.Name+" · 跨库副本"};
        return await SaveAsync(target,target.Versions.Append(copied).ToArray(),copied.Archived?target.SelectedId:copied.Id,token);
    }
    public async Task<string> SaveRecoveryAsync(StudioVersionConflict conflict,CancellationToken token=default)
    {
        Directory.CreateDirectory(directory);var path=PathFor(conflict.Repository.Identity)+".recovery-"+Guid.NewGuid().ToString("N")+".json";
        await using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);await JsonSerializer.SerializeAsync(stream,conflict,cancellationToken:token);await stream.FlushAsync(token);stream.Flush(true);return path;
    }
    private string PathFor(string identity)=>Path.Combine(directory,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))+".json");
    public async Task<StudioVersionCatalog> LoadAsync(string identity,CancellationToken token=default)
    {
        var path=PathFor(identity);if(!File.Exists(path))return new(identity,0,[],null);
        await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        var catalog=await JsonSerializer.DeserializeAsync<StudioVersionCatalog>(stream,cancellationToken:token)??throw new InvalidDataException("版本目录为空。");
        if(catalog.Identity!=identity||catalog.Version!=1)throw new InvalidDataException("版本目录身份无效。");return catalog;
    }
    public async Task<StudioVersionCatalog> SaveAsync(StudioVersionCatalog expected,IReadOnlyList<StudioEditVersion> versions,Guid? selected,CancellationToken token=default)
    {
        Directory.CreateDirectory(directory);var path=PathFor(expected.Identity);await using var gate=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var current=await LoadAsync(expected.Identity,token);if(current.Revision!=expected.Revision && SameContent(current,versions,selected))return current;if(current.Revision!=expected.Revision)throw new InvalidOperationException("版本目录已由其他会话修改，请重新打开后合并。");
        if(versions.Select(x=>x.Id).Distinct().Count()!=versions.Count||selected is Guid id&&!versions.Any(v=>v.Id==id&&!v.Archived))throw new InvalidDataException("版本选择无效。");
        foreach(var version in versions)
        {
            if(Identity(version.LibraryId,version.AssetId,version.SourceHash??"")!=expected.Identity)throw new InvalidDataException("编辑版本源身份与目录不一致。");
            version.Stack?.Normalize();version.Film?.Validate();
        }
        var next=new StudioVersionCatalog(expected.Identity,current.Revision+1,versions,selected);var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{await using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){await JsonSerializer.SerializeAsync(stream,next,cancellationToken:token);await stream.FlushAsync(token);stream.Flush(true);}File.Move(temporary,path,true);return next;}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
