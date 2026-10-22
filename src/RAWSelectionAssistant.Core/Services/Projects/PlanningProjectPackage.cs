using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record PlanningPackageFile(string Path,long Bytes,string Sha256);
public sealed record PlanningPackageManifest(int Version,Guid ProjectId,Guid DocumentId,string Name,DateTimeOffset CreatedAt,IReadOnlyList<PlanningPackageFile> Files,IReadOnlyList<string> ExcludedDependencies);
public sealed record PlanningPackageResult(string DataRoot,Guid ProjectId,string Name,IReadOnlyList<string> ExcludedDependencies);
public static class PlanningProjectPackage
{
    public const int Version=1;
    public const long MaximumTotalBytes=256L*1024*1024;
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    public static IReadOnlyList<string> Dependencies(PlanningProjectState state,ProjectShotCatalog shots)
    {
        var d=state.Document??throw new InvalidDataException("项目无策划数据");var rows=new List<string>();
        foreach(var r in d.References.Where(r=>r.ProjectCopy is null))rows.Add($"外链/库引用 {r.Source.ReferenceId:D}：{r.Source.ExternalReference??$"Library {r.Source.LibraryId}/{r.Source.AssetId}"}；原件未包含");
        foreach(var a in d.Attachments.Where(a=>a.ProjectCopy is null))rows.Add($"外链附件 {a.Id:D}：{a.Name} / {a.Path}；未包含");
        var owned=d.References.Where(r=>r.ProjectCopy is not null).Select(r=>r.Source.ReferenceId).ToHashSet();
        foreach(var r in shots.Shots.SelectMany(s=>s.References).Where(r=>!owned.Contains(r.ReferenceId)))rows.Add($"镜头外部/库引用 {r.ReferenceId:D}：{r.ExternalReference??$"Library {r.LibraryId}/{r.AssetId}"}；未包含");
        if(state.BookingId is not null)rows.Add($"档期/客户数据库关系 {state.BookingId:D}：未包含关联业务记录");
        foreach(var link in state.VisualLinks??[])rows.Add($"灵感板/自由画布依赖 {link.TargetId:D}：{link.Title}；未包含");
        foreach(var image in d.Package?.RichLayout?.Images??[])if(image.Source is {} source)rows.Add($"文档图片原素材库 {source.LibraryId:D}/{source.AssetId:D}：只包含自有PNG，不包含库原件");
        foreach(var item in d.Resources.Items.OfType<PlanningLight>().Where(l=>!string.IsNullOrWhiteSpace(l.ReferencePath)))rows.Add($"灯位外部位置记录 {item.Id:D}：{item.ReferencePath}；不额外读取路径");
        foreach(var c in state.CapturedAssets??[])rows.Add($"已拍素材关系 {c.AssetId:D}：只保留身份，素材文件/库未包含");
        foreach(var id in shots.Shots.Where(s=>s.ReferenceLookId is not null).Select(s=>s.ReferenceLookId).Distinct())rows.Add($"镜头色彩方案 {id}：方案工作区未包含");
        rows.Add("账本、付款、财务附件、在线服务、素材库原件、色彩方案/画布工作区未包含；不宣称完整跨机便携。");return rows.Distinct().ToArray();
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    private static string Safe(string path)
    {
        if(string.IsNullOrWhiteSpace(path)||path.Length>500||path.Contains('\\')||path.Contains(':')||path.StartsWith('/')||path.Split('/').Any(p=>p is "" or "." or ".."))throw new InvalidDataException("包路径不安全");return path;
    }
    public static async Task ExportAsync(string path,PhotoProjectRecord project,PlanningProjectState state,ProjectShotCatalog shots,string planningRoot,CancellationToken token=default,IProgress<long>? progress=null)
    {
        if(project.Id!=state.ProjectId||shots.ProjectId!=project.Id)throw new InvalidDataException("导出项目身份不一致");state=state.Normalize();var d=PlanningProjectCopyStorage.Bind(state.Document??throw new InvalidDataException("无策划文档"),planningRoot);state=state with{Document=d};
        var data=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
        void Add(string name,byte[] bytes){Safe(name);if(bytes.LongLength>64L*1024*1024||!data.TryAdd(name,bytes))throw new InvalidDataException("重复文件或单文件过大");if(data.Count>2000||data.Values.Sum(b=>(long)b.Length)>MaximumTotalBytes)throw new InvalidDataException("项目包超出256MiB/2000文件限制");}
        Add("planning.json",JsonSerializer.SerializeToUtf8Bytes(state,Json));Add("shots.json",JsonSerializer.SerializeToUtf8Bytes(shots,Json));
        foreach(var c in d.References.Select(r=>r.ProjectCopy).Concat(d.Attachments.Select(a=>a.ProjectCopy)).OfType<PlanningProjectCopy>().DistinctBy(c=>c.RelativePath)){token.ThrowIfCancellationRequested();Add("planning/"+c.RelativePath,PlanningProjectCopyStorage.ReadVerified(c));progress?.Report(data.Values.Sum(b=>(long)b.Length));}
        foreach(var image in d.Package?.RichLayout?.Images??[])
        {
            var bound=PlanningImageStorage.Bind(image,planningRoot);var actual=PlanningImageStorage.Resolve(bound);var expected=Path.GetFullPath(Path.Combine(planningRoot,PlanningImageStorage.RelativePath(image.DocumentId,image.DocumentImageId)));
            if(!actual.Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("旧图片未位于当前项目规范目录，先保存迁移再导出");
            PlanningImageStorage.RejectLinks(planningRoot,actual);using var f=new FileStream(actual,FileMode.Open,FileAccess.Read,FileShare.Read);if(f.Length>64L*1024*1024)throw new InvalidDataException("图片过大");var b=new byte[checked((int)f.Length)];f.ReadExactly(b);if(!Hash(b).Equals(image.CopyHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("图片hash变化");var key="planning/"+PlanningImageStorage.RelativePath(image.DocumentId,image.DocumentImageId);if(!data.ContainsKey(key))Add(key,b);
        }
        var manifest=new PlanningPackageManifest(Version,project.Id,d.Package!.DocumentId,project.Name,DateTimeOffset.UtcNow,data.Select(p=>new PlanningPackageFile(p.Key,p.Value.LongLength,Hash(p.Value))).ToArray(),Dependencies(state,shots));
        var full=Path.GetFullPath(path);if(!Path.GetExtension(full).Equals(".ptplan",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("策划项目包须使用.ptplan扩展名");PlanningImageStorage.RejectLinks(Path.GetPathRoot(full)!,full);if(File.Exists(full))throw new IOException("导出不覆盖已有项目包，请选新文件名");Directory.CreateDirectory(Path.GetDirectoryName(full)!);var temp=full+"."+Guid.NewGuid().ToString("N")+".package-stage";
        try{using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true)){foreach(var item in data){token.ThrowIfCancellationRequested();using var entry=zip.CreateEntry(item.Key).Open();entry.Write(item.Value);}using var m=zip.CreateEntry("manifest.json").Open();JsonSerializer.Serialize(m,manifest,Json);}stream.Flush(true);}token.ThrowIfCancellationRequested();File.Move(temp,full,false);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public static (PlanningPackageManifest Manifest,Dictionary<string,byte[]> Data,PlanningProjectState State,ProjectShotCatalog Shots) Validate(string path,CancellationToken token=default,IProgress<long>? progress=null)
    {
        path=Path.GetFullPath(path);PlanningImageStorage.RejectLinks(Path.GetPathRoot(path)!,path);using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(file.Length>MaximumTotalBytes)throw new InvalidDataException("压缩包过大");using var zip=new ZipArchive(file,ZipArchiveMode.Read);if(zip.Entries.Count is <3 or >2001)throw new InvalidDataException("项目包文件数无效");var entries=new Dictionary<string,ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);long total=0;
        foreach(var e in zip.Entries){token.ThrowIfCancellationRequested();Safe(e.FullName);if(e.Length<1||e.Length>64L*1024*1024||(total+=e.Length)>MaximumTotalBytes||!entries.TryAdd(e.FullName,e)||((e.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("重复/超限/符号链接包条目");}
        if(!entries.TryGetValue("manifest.json",out var me)||me.Length>1024*1024)throw new InvalidDataException("缺少或过大manifest");var manifest=JsonSerializer.Deserialize<PlanningPackageManifest>(Read(me,1024*1024),Json)??throw new InvalidDataException("空manifest");
        if(manifest.Version!=Version)throw new NotSupportedException($"项目包版本{manifest.Version}不支持；仅支持版本{Version}，未写入任何项目");if(manifest.ProjectId==Guid.Empty||manifest.DocumentId==Guid.Empty||string.IsNullOrWhiteSpace(manifest.Name)||manifest.Name.Length>300||manifest.Files is null||manifest.ExcludedDependencies is null||manifest.Files.Count+1!=entries.Count)throw new InvalidDataException("manifest身份或数量无效");
        var data=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);long done=0;
        foreach(var row in manifest.Files){token.ThrowIfCancellationRequested();Safe(row.Path);if(row.Path=="manifest.json"||!entries.TryGetValue(row.Path,out var entry)||row.Bytes!=entry.Length||row.Sha256 is null||row.Sha256.Length!=64||!row.Sha256.All(Uri.IsHexDigit))throw new InvalidDataException("文件清单不匹配/缺文件");var bytes=Read(entry,64*1024*1024);if(!Hash(bytes).Equals(row.Sha256,StringComparison.OrdinalIgnoreCase)||!data.TryAdd(row.Path,bytes))throw new InvalidDataException("项目包文件SHA-256不符/重复");progress?.Report(done+=bytes.Length);}
        if(!data.TryGetValue("planning.json",out var stateBytes)||!data.TryGetValue("shots.json",out var shotBytes)||stateBytes.Length>16*1024*1024||shotBytes.Length>16*1024*1024)throw new InvalidDataException("策划元数据缺失或超限");var state=(JsonSerializer.Deserialize<PlanningProjectState>(stateBytes,Json)??throw new InvalidDataException()).Normalize();var shots=JsonSerializer.Deserialize<ProjectShotCatalog>(shotBytes,Json)??throw new InvalidDataException();
        if(state.ProjectId!=manifest.ProjectId||state.Document?.Package?.DocumentId!=manifest.DocumentId||shots.ProjectId!=manifest.ProjectId||shots.Version!=1||shots.Revision<0||shots.Shots is null||shots.Shots.Any(s=>s.ProjectId!=manifest.ProjectId)||shots.Shots.Select(s=>s.ShotId).Distinct().Count()!=shots.Shots.Count)throw new InvalidDataException("包内项目/文档/镜头身份不匹配");foreach(var s in shots.Shots)s.Normalize();
        var d=PlanningProjectCopyStorage.Bind(state.Document!,Path.GetTempPath());var wanted=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"planning.json","shots.json"};
        foreach(var c in d.References.Select(r=>r.ProjectCopy).Concat(d.Attachments.Select(a=>a.ProjectCopy)).OfType<PlanningProjectCopy>()){var name="planning/"+c.RelativePath;if(!data.TryGetValue(name,out var b)||b.Length!=c.Bytes||!Hash(b).Equals(c.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("自有副本缺失或hash不符");wanted.Add(name);}
        foreach(var image in d.Package?.RichLayout?.Images??[]){image.Validate();if(image.DocumentId!=manifest.DocumentId)throw new InvalidDataException("图片跨文档");var rel=PlanningImageStorage.RelativePath(image.DocumentId,image.DocumentImageId);if(image.CopyPath.Replace('\\','/')!=rel)throw new InvalidDataException("包内图片须规范相对路径");var name="planning/"+rel;if(!data.TryGetValue(name,out var b)||!Hash(b).Equals(image.CopyHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("文档图片缺失/变化");wanted.Add(name);}
        if(!Dependencies(state,shots).OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(manifest.ExcludedDependencies.OrderBy(x=>x,StringComparer.Ordinal)))throw new InvalidDataException("未包含依赖清单与项目内容不符");
        if(!wanted.SetEquals(data.Keys))throw new InvalidDataException("存在未声明归属的额外包文件");return(manifest,data,state,shots);
    }
    private static void DeleteStage(string stage)
    {
        foreach(var entry in Directory.EnumerateFileSystemEntries(stage)){PlanningImageStorage.RejectLinks(stage,entry);if(Directory.Exists(entry))DeleteStage(entry);else File.Delete(entry);}Directory.Delete(stage);
    }
    private static byte[] Read(ZipArchiveEntry entry,int maximum){if(entry.Length>maximum)throw new InvalidDataException("条目过大");using var s=entry.Open();var b=new byte[checked((int)entry.Length)];s.ReadExactly(b);if(s.ReadByte()!=-1)throw new InvalidDataException("条目长度不符");return b;}
    public static async Task<PlanningPackageResult> ImportAsync(string package,string destination,CancellationToken token=default,IProgress<long>? progress=null)
    {
        // No writes or database creation before full hash/schema/identity validation.
        var checkedPackage=Validate(package,token,progress);token.ThrowIfCancellationRequested();var m=checkedPackage.Manifest;var root=Path.GetFullPath(destination);PlanningImageStorage.RejectLinks(Path.GetPathRoot(root)!,root);Directory.CreateDirectory(root);var target=Path.Combine(root,m.ProjectId.ToString("N"));if(Directory.Exists(target)||File.Exists(target))throw new IOException("项目ID冲突：目标已有该项目，未覆盖；请选择另一个全新数据根");
        var stage=Path.Combine(root,".import-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);PlanningImageStorage.RejectLinks(root,stage);
        try
        {
            foreach(var item in checkedPackage.Data.Where(p=>p.Key.StartsWith("planning/",StringComparison.Ordinal))){token.ThrowIfCancellationRequested();var path=Path.Combine(stage,item.Key);PlanningImageStorage.RejectLinks(stage,path);Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllBytesAsync(path,item.Value,token).ConfigureAwait(false);progress?.Report(item.Value.Length);}
            var planning=new PlanningProjectStore(Path.Combine(stage,"planning"));await planning.SaveAsync(checkedPackage.State,token).ConfigureAwait(false);var shots=new ProjectShotStore(Path.Combine(stage,"shots"));await shots.SaveCatalogAsync(checkedPackage.Shots,token).ConfigureAwait(false);
            var loaded=await planning.LoadAsync(m.ProjectId,token).ConfigureAwait(false);foreach(var copy in loaded.Document!.References.Select(r=>r.ProjectCopy).Concat(loaded.Document.Attachments.Select(a=>a.ProjectCopy)).OfType<PlanningProjectCopy>())PlanningProjectCopyStorage.ReadVerified(copy);
            foreach(var image in loaded.Document.Package?.RichLayout?.Images??[])PlanningImageStorage.Resolve(image);
            await File.WriteAllTextAsync(Path.Combine(stage,"imported-manifest.json"),JsonSerializer.Serialize(m,Json),token).ConfigureAwait(false);
            var db=new PixelTartDatabase(Path.Combine(stage,"project.db"));var migration=await new DatabaseMigrator(db,new DatabaseBackupService(db,Path.Combine(stage,"backup"))).MigrateAsync(token).ConfigureAwait(false);if(!migration.Success)throw new IOException("隔离项目数据库建立失败");
            await new SqliteProjectRepository(db).UpsertAsync(new PhotoProjectRecord{Id=m.ProjectId,Name=m.Name},token).ConfigureAwait(false);Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            var dependencies=Dependencies(checkedPackage.State,checkedPackage.Shots);await File.WriteAllTextAsync(Path.Combine(stage,"import-result.json"),JsonSerializer.Serialize(new{version=Version,project=m.ProjectId,name=m.Name,dependencies,scope="独立项目数据根；未迁移外部业务依赖",sameName="同名项目保留不同ID目录，不覆盖"},Json),token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();PlanningImageStorage.RejectLinks(root,target);Directory.Move(stage,target);return new(target,m.ProjectId,m.Name,dependencies);
        }
        finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(stage)){PlanningImageStorage.RejectLinks(root,stage);DeleteStage(stage);}}
    }
}
