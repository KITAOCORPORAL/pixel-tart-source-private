using System.Diagnostics;
using System.Text.Json;
namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record PlanningCopyJournal(int Version, Guid ProjectId, Guid DocumentId, string TemporaryName, int ProcessId, long ProcessStartTicks, DateTimeOffset StartedAt, PlanningProjectCopy? Copy=null);
public sealed record PlanningRecoveryItem(string Path,string Action,string Reason);
public static class PlanningCopyRecovery
{
    public static readonly TimeSpan MinimumAge=TimeSpan.FromHours(1);
    internal static PlanningCopyJournal New(Guid project,Guid document,string temporary)=>new(1,project,document,Path.GetFileName(temporary),Environment.ProcessId,Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks,DateTimeOffset.UtcNow);
    internal static void Save(string journal,PlanningCopyJournal value)
    {
        var temp=journal+".writing";try{using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(f,value);f.Flush(true);}File.Move(temp,journal,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public static IReadOnlyList<PlanningRecoveryItem> Recover(string root,Guid project,PlanningDocument document)
    {
        var items=new List<PlanningRecoveryItem>();var doc=document.Package?.DocumentId??Guid.Empty;if(doc==Guid.Empty)return items;
        var staging=Path.Combine(root,"project-copies",project.ToString("N"),doc.ToString("N"),".staging");
        PlanningImageStorage.RejectLinks(root,staging);if(!Directory.Exists(staging))return items;
        // Draft uncertainty means no deletion. A valid recovery draft can still own a completed copy.
        var references=document.References.Select(r=>r.ProjectCopy).Concat(document.Attachments.Select(a=>a.ProjectCopy)).Where(c=>c is not null).Select(c=>c!.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var draftPath=Path.Combine(root,project.ToString("N")+".planning.json.draft");
        if(File.Exists(draftPath))
        {
            try{PlanningImageStorage.RejectLinks(root,draftPath);using var f=File.OpenRead(draftPath);var draft=JsonSerializer.Deserialize<PlanningDocumentDraft>(f);if(draft?.ProjectId!=project)throw new InvalidDataException();foreach(var c in draft.Document.References.Select(r=>r.ProjectCopy).Concat(draft.Document.Attachments.Select(a=>a.ProjectCopy)).OfType<PlanningProjectCopy>())references.Add(c.RelativePath);}
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException){return [new(draftPath,"保留","草稿归属/引用无法核对，未清理任何文件")];}
        }
        foreach(var journal in Directory.EnumerateFiles(staging,"*.owner.json",SearchOption.TopDirectoryOnly).Take(1000))
        {
            try
            {
                PlanningImageStorage.RejectLinks(root,journal);using var held=new FileStream(journal,FileMode.Open,FileAccess.Read,FileShare.None);
                if(held.Length>16384)throw new InvalidDataException("归属凭据超限");var j=JsonSerializer.Deserialize<PlanningCopyJournal>(held)??throw new InvalidDataException();
                if(j.ProcessId<=0||j.Version!=1||j.ProjectId!=project||j.DocumentId!=doc||j.StartedAt>DateTimeOffset.UtcNow||!Guid.TryParseExact(Path.GetFileNameWithoutExtension(j.TemporaryName),"N",out _)||Path.GetExtension(j.TemporaryName)!=".part"||Path.GetFileName(journal)!=j.TemporaryName+".owner.json")throw new InvalidDataException("归属凭据不匹配");
                if(j.Copy is {} receipt){receipt.Validate();if(receipt.ProjectId!=project||receipt.DocumentId!=doc)throw new InvalidDataException("副本归属不匹配，暂存保留");}
                if(DateTimeOffset.UtcNow-j.StartedAt<MinimumAge){items.Add(new(journal,"保留","未过期"));continue;}
                try{using var process=Process.GetProcessById(j.ProcessId);if(process.StartTime.ToUniversalTime().Ticks==j.ProcessStartTicks&&!process.HasExited){items.Add(new(journal,"保留","原复制进程仍活动"));continue;}}catch(ArgumentException){}catch(InvalidOperationException){}
                var temporary=Path.Combine(staging,j.TemporaryName);PlanningImageStorage.RejectLinks(root,temporary);
                if(File.Exists(temporary))
                {
                    // Hold exclusive access before deleting only this exact receipt-owned .part.
                    using(var part=new FileStream(temporary,FileMode.Open,FileAccess.ReadWrite,FileShare.None,4096,FileOptions.DeleteOnClose)){if((File.GetAttributes(temporary)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("暂存为重解析点");}
                    items.Add(new(temporary,"清理过期暂存","项目/文档/名称/进程归属已核对"));
                }
                if(j.Copy is {} copy)
                {
                    copy.Validate();if(copy.ProjectId!=project||copy.DocumentId!=doc)throw new InvalidDataException("副本身份不匹配");copy=copy with{StorageRoot=Path.GetFullPath(root)};
                    if(references.Contains(copy.RelativePath))items.Add(new(copy.RelativePath,"保留有效副本","当前策划或恢复草稿引用，绝不删除"));
                    else if(File.Exists(PlanningProjectCopyStorage.Resolve(copy))){PlanningProjectCopyStorage.ReadVerified(copy);items.Add(new(copy.RelativePath,"保留孤立副本","SHA-256已核对；未提交引用，不自动认领、不删除，可再次明确导入复用"));}
                }
            }
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or NotSupportedException or System.ComponentModel.Win32Exception){items.Add(new(journal,"保留",e.Message));}
        }
        foreach(var part in Directory.EnumerateFiles(staging,"*.part",SearchOption.TopDirectoryOnly).Take(1000))if(!File.Exists(part+".owner.json"))items.Add(new(part,"保留未知暂存","旧版本无归属凭据，不因扩展名删除"));
        if(items.Count>0)
        {
            try{var log=Path.Combine(staging,Guid.NewGuid().ToString("N")+".recovery-audit.json");PlanningImageStorage.RejectLinks(root,log);using var output=new FileStream(log,FileMode.CreateNew,FileAccess.Write,FileShare.None);JsonSerializer.Serialize(output,new{utc=DateTimeOffset.UtcNow,project,document=doc,items});output.Flush(true);}
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or InvalidDataException){items.Add(new(staging,"审计未写入",e.Message));}
        }
        return items;
    }
}
