using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record PlanningImportTarget(string Area,string RelativePath,long Bytes,string Sha256);
public sealed record PlanningImportJournal(int Version,Guid TransactionId,Guid ProjectId,Guid DocumentId,string Name,string PlanningRoot,string ShotRoot,string Phase,int ProcessId,long ProcessStartTicks,DateTimeOffset StartedAt,IReadOnlyList<PlanningImportTarget> Files);
public sealed record PlanningImportRecovery(string Stage,string Status,string Detail);
public sealed record PlanningCurrentImportResult(Guid ProjectId,string Name,IReadOnlyList<string> ExcludedDependencies);
public static class PlanningCurrentRootImport
{
    private const string Marker="current-root-owner.json";
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    public static string RecoveryRoot(PlanningProjectStore store)=>Path.Combine(store.StorageRoot,".package-imports");
    private static string Hash(byte[] data)=>Convert.ToHexString(SHA256.HashData(data));
    private static void Receipt(string stage,PlanningImportJournal journal)
    {
        var path=Path.Combine(stage,Marker);var temporary=path+".writing";
        try{using(var f=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(f,journal,Json);f.Flush(true);}File.Move(temporary,path,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    private static string Target(PlanningImportJournal j,PlanningImportTarget f)
    {
        var root=f.Area=="planning"?j.PlanningRoot:f.Area=="shots"?j.ShotRoot:throw new InvalidDataException("未知目标存储");var rel=f.RelativePath.Replace('\\','/');
        if(Path.IsPathRooted(rel)||rel.Contains(':')||rel.Split('/').Any(p=>p is "" or "." or ".."))throw new InvalidDataException("目标路径不安全");
        if(f.Area=="shots"&&rel!=j.ProjectId.ToString("N")+".shots.json")throw new InvalidDataException("镜头归属不符");
        if(f.Area=="planning"&&rel!=j.ProjectId.ToString("N")+".planning.json"&&!rel.StartsWith($"project-copies/{j.ProjectId:N}/{j.DocumentId:N}/",StringComparison.Ordinal)&&!rel.StartsWith($"images/{j.DocumentId:N}/",StringComparison.Ordinal))throw new InvalidDataException("策划文件归属不符");
        if(f.Bytes<1||f.Bytes>64L*1024*1024||f.Sha256.Length!=64||!f.Sha256.All(Uri.IsHexDigit))throw new InvalidDataException("目标清单无效");var target=Path.Combine(root,rel);PlanningImageStorage.RejectLinks(root,target);return target;
    }
    private static bool Active(PlanningImportJournal j)
    {
        try{using var p=Process.GetProcessById(j.ProcessId);return !p.HasExited&&p.StartTime.ToUniversalTime().Ticks==j.ProcessStartTicks;}catch(ArgumentException){return false;}catch(InvalidOperationException){return false;}
    }
    private static bool Match(string path,PlanningImportTarget f)
    {using var s=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);return s.Length==f.Bytes&&Convert.ToHexString(SHA256.HashData(s)).Equals(f.Sha256,StringComparison.OrdinalIgnoreCase);}
    private static async Task<PlanningImportRecovery> Settle(string stage,PlanningImportJournal j,IPlanningImportRepository db,bool force,CancellationToken token)
    {
        if(!force&&Active(j))return new(stage,"保留","原导入进程仍活动");
        var receipt=await db.GetPlanningImportReceiptAsync(j.ProjectId,token);var exists=await db.ContainsProjectIdentityAsync(j.ProjectId,token);
        if(receipt==j.TransactionId)
        {
            foreach(var f in j.Files){var target=Target(j,f);if(!File.Exists(target)||!Match(target,f))return new(stage,"保留已提交异常","数据库已有本事务收据，但文件缺失/变化，未删除数据库或文件");}
            Receipt(stage,j with{Phase="Committed"});return new(stage,"已提交","数据库收据与全部文件校验一致，保留有效项目");
        }
        if(exists)return new(stage,"保留冲突","数据库存在其他项目身份/事务，未处理任何文件");
        if(j.Phase=="Prepared"){Receipt(stage,j with{Phase="RolledBack"});return new(stage,"已撤回准备阶段","从未安装最终文件；不检查或删除任何最终目标，仅保留stage审计");}
        // Only our expected final paths, and only after all of them pass ownership/hash checks.
        foreach(var f in j.Files){var target=Target(j,f);if(!File.Exists(Path.Combine(stage,f.Area,f.RelativePath))&&File.Exists(target)&&!Match(target,f))return new(stage,"保留变化文件","未提交文件已变化，不撤回未知内容");}
        foreach(var f in j.Files){token.ThrowIfCancellationRequested();var target=Target(j,f);if(!File.Exists(Path.Combine(stage,f.Area,f.RelativePath))&&File.Exists(target)){if(!Match(target,f))throw new IOException("撤回前文件变化");File.Delete(target);}}
        Receipt(stage,j with{Phase="RolledBack"});return new(stage,"已撤回未提交文件","数据库无本项目，清理仅本事务清单且哈希一致的文件；stage与审计保留，可安全重试");
    }
    public static async Task<IReadOnlyList<PlanningImportRecovery>> RecoverAsync(PlanningProjectStore planning,ProjectShotStore shots,IPlanningImportRepository db,CancellationToken token=default)
    {
        var root=RecoveryRoot(planning);PlanningImageStorage.RejectLinks(planning.StorageRoot,root);if(!Directory.Exists(root))return [];var results=new List<PlanningImportRecovery>();
        foreach(var stage in Directory.EnumerateDirectories(root,".import-*",SearchOption.TopDirectoryOnly).Take(1000))
        {
            try
            {
                PlanningImageStorage.RejectLinks(root,stage);var marker=Path.Combine(stage,Marker);PlanningImageStorage.RejectLinks(root,marker);if(!File.Exists(marker)){results.Add(new(stage,"保留未知暂存","没有本功能归属凭据"));continue;}
                PlanningImportJournal j;using(var f=new FileStream(marker,FileMode.Open,FileAccess.Read,FileShare.None)){if(f.Length>1024*1024)throw new InvalidDataException("归属清单过大");j=JsonSerializer.Deserialize<PlanningImportJournal>(f,Json)??throw new InvalidDataException();}
                if(j.Version!=1||j.TransactionId==Guid.Empty||j.ProjectId==Guid.Empty||j.DocumentId==Guid.Empty||j.ProcessId<=0||Path.GetFileName(stage)!=".import-"+j.TransactionId.ToString("N")||j.PlanningRoot!=planning.StorageRoot||j.ShotRoot!=shots.StorageRoot||j.Files is null||j.Files.Count>2000||j.Phase is not ("Prepared" or "Installing" or "FilesInstalled" or "Committed" or "RolledBack"))throw new InvalidDataException("项目/根/阶段归属不符");
                if(j.Files.Select(x=>x.Area+"/"+x.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=j.Files.Count)throw new InvalidDataException("归属清单目标重复");foreach(var item in j.Files)Target(j,item);
                if(j.Phase is "Committed" or "RolledBack"){results.Add(new(stage,"保留审计",j.Phase));continue;}
                var identity=Path.Combine(root,j.ProjectId.ToString("N")+".lock");PlanningImageStorage.RejectLinks(root,identity);using var projectLease=new FileStream(identity,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
                PlanningImageStorage.RejectLinks(root,Path.Combine(stage,"active.lock"));using var lease=new FileStream(Path.Combine(stage,"active.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None);results.Add(await Settle(stage,j,db,false,token));
            }
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or JsonException or NotSupportedException or System.ComponentModel.Win32Exception){results.Add(new(stage,"保留",e.Message));}
        }
        if(results.Count>0){var audit=Path.Combine(root,Guid.NewGuid().ToString("N")+".recovery.json");using var stream=new FileStream(audit,FileMode.CreateNew,FileAccess.Write,FileShare.None);JsonSerializer.Serialize(stream,results,Json);stream.Flush(true);}return results;
    }
    public static async Task<PlanningCurrentImportResult> ImportAsync(string package,PlanningProjectStore planning,ProjectShotStore shots,IPlanningImportRepository db,CancellationToken token=default,IProgress<string>? progress=null)
    {
        var validated=PlanningProjectPackage.Validate(package,token);var m=validated.Manifest;
        await RecoverAsync(planning,shots,db,token);
        var recoveryRoot=RecoveryRoot(planning);Directory.CreateDirectory(recoveryRoot);PlanningImageStorage.RejectLinks(planning.StorageRoot,recoveryRoot);
        // Exclusive same-ID lease for this importer across processes; no source project ever overwritten.
        var identityLock=Path.Combine(recoveryRoot,m.ProjectId.ToString("N")+".lock");PlanningImageStorage.RejectLinks(recoveryRoot,identityLock);using var lease=new FileStream(identityLock,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        if(await db.ContainsProjectIdentityAsync(m.ProjectId,token))throw new IOException("项目ID冲突：当前应用已有该项目（含归档），原项目保持不变");
        var payload=new Dictionary<(string Area,string Path),byte[]>();
        // Keep excluded relations as metadata only. Never attach them to unrelated current business IDs.
        var state=validated.State with{BookingId=null,VisualLinks=[],CapturedAssets=[]};state=state with{Document=state.Document! with{Package=state.Document.Package! with{Assets=[]}}};
        var extensions=state.Document!.Extensions is null?new Dictionary<string,JsonElement>():new(state.Document.Extensions);extensions["PackageImportExcludedDependencies"]=JsonSerializer.SerializeToElement(m.ExcludedDependencies);state=state with{Document=state.Document with{Extensions=extensions}};
        payload.Add(("planning",m.ProjectId.ToString("N")+".planning.json"),JsonSerializer.SerializeToUtf8Bytes(state,Json));payload.Add(("shots",m.ProjectId.ToString("N")+".shots.json"),JsonSerializer.SerializeToUtf8Bytes(validated.Shots with{Shots=validated.Shots.Shots.Select(s=>s with{ReferenceLookId=null}).ToArray()},Json));
        foreach(var item in validated.Data.Where(x=>x.Key.StartsWith("planning/",StringComparison.Ordinal)))payload.Add(("planning",item.Key[9..]),item.Value);
        var process=Process.GetCurrentProcess();var tx=Guid.NewGuid();var j=new PlanningImportJournal(1,tx,m.ProjectId,m.DocumentId,m.Name,planning.StorageRoot,shots.StorageRoot,"Prepared",Environment.ProcessId,process.StartTime.ToUniversalTime().Ticks,DateTimeOffset.UtcNow,payload.Select(x=>new PlanningImportTarget(x.Key.Area,x.Key.Path,x.Value.LongLength,Hash(x.Value))).ToArray());
        foreach(var f in j.Files)if(File.Exists(Target(j,f))||Directory.Exists(Target(j,f)))throw new IOException("项目文件ID冲突：当前存储已有文件，未覆盖");
        var stage=Path.Combine(recoveryRoot,".import-"+tx.ToString("N"));PlanningImageStorage.RejectLinks(recoveryRoot,stage);Directory.CreateDirectory(stage);PlanningImageStorage.RejectLinks(recoveryRoot,stage);using var active=new FileStream(Path.Combine(stage,"active.lock"),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);Receipt(stage,j);
        try
        {
            foreach(var item in payload){token.ThrowIfCancellationRequested();var staged=Path.Combine(stage,item.Key.Area,item.Key.Path);PlanningImageStorage.RejectLinks(stage,staged);Directory.CreateDirectory(Path.GetDirectoryName(staged)!);PlanningImageStorage.RejectLinks(stage,staged);using var output=new FileStream(staged,FileMode.CreateNew,FileAccess.Write,FileShare.None);output.Write(item.Value);output.Flush(true);}
            await File.WriteAllTextAsync(Path.Combine(stage,"manifest.json"),JsonSerializer.Serialize(m,Json),token);progress?.Report("Prepared");token.ThrowIfCancellationRequested();j=j with{Phase="Installing"};Receipt(stage,j);
            foreach(var f in j.Files){token.ThrowIfCancellationRequested();var target=Target(j,f);Directory.CreateDirectory(Path.GetDirectoryName(target)!);PlanningImageStorage.RejectLinks(f.Area=="planning"?planning.StorageRoot:shots.StorageRoot,target);File.Move(Path.Combine(stage,f.Area,f.RelativePath),target,false);progress?.Report("FileInstalled");}
            foreach(var f in j.Files)if(!Match(Target(j,f),f))throw new IOException("提交前目标文件校验失败");j=j with{Phase="FilesInstalled"};Receipt(stage,j);progress?.Report("FilesInstalled");token.ThrowIfCancellationRequested();
            await db.InsertPlanningImportAsync(new PhotoProjectRecord{Id=m.ProjectId,Name=m.Name},tx,token);j=j with{Phase="Committed"};Receipt(stage,j);progress?.Report("Committed");return new(m.ProjectId,m.Name,m.ExcludedDependencies);
        }
        catch{await Settle(stage,j,db,true,CancellationToken.None);throw;}
    }
}
