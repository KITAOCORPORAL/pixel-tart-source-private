using System.Text.Json;
namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record PlanningShotLinkIssue(Guid ShotId,Guid ReferenceId,string OriginalPath,string Reason,string? CopyPath);
public static class PlanningShotLinkLifecycle
{
    public static bool Matches(ProjectShot? shot,PlanningShotExternalLink link)=>shot?.References.Any(r=>r.ReferenceId==link.ReferenceId&&r.LibraryId is null&&r.AssetId is null&&r.ExternalReference==link.OriginalPath)==true;
    public static IReadOnlyList<PlanningShotLinkIssue> Inspect(PlanningDocument document,ProjectShotCatalog catalog)
    {
        var rows=new List<PlanningShotLinkIssue>();
        foreach(var link in document.ShotExternalLinks)
        {
            foreach(var id in link.ShotIds)if(!Matches(catalog.Shots.FirstOrDefault(s=>s.ShotId==id),link))rows.Add(new(id,link.ReferenceId,link.OriginalPath,"镜头已删除、引用移除或来源路径/库身份已变化",link.ProjectCopy?.RelativePath));
            if(document.References.Any(r=>r.Source.ReferenceId==link.ReferenceId)||document.Attachments.Any(a=>a.Id==link.ReferenceId))rows.Add(new(Guid.Empty,link.ReferenceId,link.OriginalPath,"镜头独有批准与文档/附件身份冲突",link.ProjectCopy?.RelativePath));
        }
        return rows;
    }
    // Only prune approvals proven valid before this edit. Pre-existing mismatches require explicit repair.
    public static PlanningDocument Reconcile(PlanningDocument document,ProjectShotCatalog before,ProjectShotCatalog after)
    {
        var links=document.ShotExternalLinks.Select(link=>
        {
            var ids=link.ShotIds.Where(id=>!Matches(before.Shots.FirstOrDefault(s=>s.ShotId==id),link)||Matches(after.Shots.FirstOrDefault(s=>s.ShotId==id),link)).ToArray();
            return link with{ShotIds=ids};
        }).Where(link=>link.ShotIds.Count>0).ToArray();return document with{ShotExternalLinks=links};
    }
}
public sealed partial class PlanningProjectStore
{
    internal async Task CommitShotEditAsync(ProjectShotCatalog before,ProjectShotCatalog after,Func<Task> commit,CancellationToken token)
    {
        // Name/timing/status edits do not touch source identity and must retain existing cross-file draft recovery order.
        string Sources(ProjectShotCatalog c)=>JsonSerializer.Serialize(c.Shots.OrderBy(x=>x.ShotId).Select(x=>new{x.ShotId,Sources=x.References.Select(r=>new{r.ReferenceId,r.LibraryId,r.AssetId,r.ExternalReference}).OrderBy(r=>r.ReferenceId)}));
        if(Sources(before)==Sources(after)){await commit();return;}
        var path=FilePath(before.ProjectId);var gate=Gates.GetOrAdd(path,_=>new(1,1));await gate.WaitAsync(token).ConfigureAwait(false);
        var temporary=path+"."+Guid.NewGuid().ToString("N")+".shot-link-stage";byte[]? original=null;var replaced=false;
        try
        {
            if(!File.Exists(path)){await commit();return;}
            original=await File.ReadAllBytesAsync(path,token);var current=await ReadAsync(before.ProjectId,path,token);var doc=current.Document;
            if(doc is null||doc.ShotExternalLinks.Count==0){await commit();return;}
            var next=PlanningShotLinkLifecycle.Reconcile(doc,before,after);
            if(JsonSerializer.Serialize(next.ShotExternalLinks)==JsonSerializer.Serialize(doc.ShotExternalLinks)){await commit();return;}
            var state=current with{Document=next,Revision=current.Revision+1,UpdatedAt=DateTimeOffset.UtcNow};
            // Reversible metadata only. Never remove a project-owned file when its approval is pruned.
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){await JsonSerializer.SerializeAsync(stream,state,cancellationToken:token);await stream.FlushAsync(token);stream.Flush(true);}
            token.ThrowIfCancellationRequested();File.Move(temporary,path,true);replaced=true;await commit();
        }
        catch
        {
            if(replaced&&original is not null){using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(original);stream.Flush(true);}File.Move(temporary,path,true);}throw;
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);gate.Release();}
    }
    public Task RepairShotLinkAsync(Guid project,ProjectShotStore shots,PlanningShotExternalLink expected,Guid shotId,PlanningExternalFile? replacement=null,bool importCopy=false,CancellationToken token=default)=>
        shots.WithCatalogLockAsync(project,async catalog=>
        {
            var created=new List<string>();
            await UpdateAsync(project,async state=>
            {
                var d=state.Document??throw new InvalidDataException("无策划文档");var link=d.ShotExternalLinks.SingleOrDefault(x=>x.ReferenceId==expected.ReferenceId)??throw new InvalidDataException("批准已变化，请重新检查");
                if(JsonSerializer.Serialize(link)!=JsonSerializer.Serialize(expected))throw new InvalidDataException("批准已变化，请重新检查");
                if(!PlanningShotLinkLifecycle.Inspect(d,catalog).Any(i=>i.ReferenceId==link.ReferenceId&&i.ShotId==shotId))throw new InvalidDataException("该项不再失配，未修改");
                var links=d.ShotExternalLinks.Where(x=>x.ReferenceId!=link.ReferenceId).ToList();var retained=link.ShotIds.Where(id=>id!=shotId&&PlanningShotLinkLifecycle.Matches(catalog.Shots.FirstOrDefault(s=>s.ShotId==id),link)).ToArray();
                if(replacement is null)
                {
                    // Remove only the chosen invalid recipient; other pre-existing issues remain visible.
                    var remaining=shotId==Guid.Empty?Array.Empty<Guid>():link.ShotIds.Where(id=>id!=shotId).ToArray();if(remaining.Length>0)links.Add(link with{ShotIds=remaining});
                }
                else
                {
                    if(retained.Length>0)throw new InvalidDataException("共享ID仍有有效旧来源；不能用新来源覆盖其他镜头，请先为改源项建立独立引用ID");
                    var recipients=catalog.Shots.Where(s=>s.References.Any(r=>r.ReferenceId==link.ReferenceId)).ToArray();var sources=recipients.SelectMany(s=>s.References.Where(r=>r.ReferenceId==link.ReferenceId)).ToArray();
                    if(sources.Length==0||sources.Any(r=>r.LibraryId is not null||r.AssetId is not null||r.ExternalReference!=replacement.OriginalPath)||PlanningReferenceLinks.HasConflict(d,catalog.Shots,link.ReferenceId))throw new InvalidDataException("当前来源不唯一或为素材库；不猜测、不接受冲突来源");
                    replacement.Validate(link.ReferenceId,"reference",sources[0].ExternalReference!);var verified=await PlanningExternalFile.InspectAsync(link.ReferenceId,"reference",replacement.OriginalPath,replacement.SelectedPath,token);if(!replacement.SameContent(verified))throw new InvalidDataException("确认后来源类型/大小/SHA-256变化");
                    PlanningProjectCopy? copy=null;if(importCopy){copy=await PlanningProjectCopyStorage.CopyAsync(StorageRoot,project,d.Package!.DocumentId,link.ReferenceId,"reference",replacement.SelectedPath,created,token,recordedOriginal:replacement.OriginalPath);if(copy.Bytes!=verified.Bytes||!copy.Sha256.Equals(verified.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("复制后hash不符");}
                    links.Add(new(link.ReferenceId,replacement.OriginalPath,recipients.Select(s=>s.ShotId).ToArray(),verified,copy));
                }
                // Preserve exact original bytes before a user-confirmed metadata repair. No source/copy deletion.
                var original=FilePath(project);var backup=original+"."+Guid.NewGuid().ToString("N")+".before-shot-link-repair";File.Copy(original,backup,false);
                return state with{Document=d with{ShotExternalLinks=links}};
            },token,()=>{foreach(var f in created){PlanningImageStorage.RejectLinks(StorageRoot,f);if(File.Exists(f))File.Delete(f);}});
        },token);
}
