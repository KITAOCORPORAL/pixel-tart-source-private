using System.IO;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class PlanningCenterViewModel
{
    public IReadOnlyList<PlanningShotLinkIssue> ShotLinkIssues=>PlanningShotLinkLifecycle.Inspect(_document,new(ProjectId??Guid.Empty,Shots.ToArray()));
    public async Task RefreshShotLinkReceiptsAsync()
    {
        if(ProjectId is not Guid id)return;var saved=(await _planning.LoadAsync(id)).Document;if(saved is null||ProjectId!=id)return;
        _document=_document with{ShotExternalLinks=saved.ShotExternalLinks};if(_pendingDocuments.ContainsKey(id))QueueDocumentSave();
    }
    public async Task RepairShotLinkIssueAsync(PlanningShotLinkIssue issue,bool relocate,bool importCopy=false)
    {
        if(ProjectId is not Guid project)return;var expected=_document.ShotExternalLinks.SingleOrDefault(x=>x.ReferenceId==issue.ReferenceId);if(expected is null)return;
        PlanningExternalFile? selected=null;
        try
        {
            if(relocate)
            {
                var sources=Shots.Where(s=>s.References.Any(r=>r.ReferenceId==issue.ReferenceId)).SelectMany(s=>s.References.Where(r=>r.ReferenceId==issue.ReferenceId)).ToArray();
                if(sources.Length==0||sources.Any(r=>r.LibraryId is not null||r.AssetId is not null||r.ExternalReference!=sources[0].ExternalReference)||sources[0].ExternalReference is not string original){_dialogs.ShowError("当前引用不存在、库身份或共享来源冲突；不能猜测定位。请移除失效批准或给改源镜头独立引用ID。");return;}
                var file=_dialogs.ChooseFiles("重新定位失配镜头引用","图片|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.tif;*.tiff",false).FirstOrDefault();if(file is null)return;selected=await PlanningExternalFile.InspectAsync(issue.ReferenceId,"reference",original,file);
            }
            var details=$"镜头ID：{issue.ShotId}\n引用ID：{issue.ReferenceId}\n旧原路径：{issue.OriginalPath}\n原因：{issue.Reason}\n受影响副本（保留不删除）：{issue.CopyPath??"无"}\n";
            if(!_dialogs.Confirm(details+(selected is null?"仅移除此项失效批准，不删除源或项目副本。":$"明确重定位：{selected.SelectedPath}\n类型 {selected.FileType} / {selected.Bytes} bytes\nSHA-256 {selected.Sha256}\n不证明与旧内容相同，请确认选中的新来源。"),"逐项修复镜头外链批准"))return;
            if(ProjectId!=project)throw new InvalidOperationException("项目已切换");if(!await FlushAsync())return;await _saveGate.WaitAsync();
            try{await _planning.RepairShotLinkAsync(project,_shots,expected,issue.ShotId,selected,importCopy);await RefreshShotLinkReceiptsAsync();await RefreshDocumentReferencesAsync();SaveStatus="修复已保存，旧批准原文件备份已保留；副本未删除";PresentationChanged();}
            finally{_saveGate.Release();}
        }
        catch(Exception e)when(PackageError(e)){_dialogs.ShowError("修复未完成，原文件和旧批准保留："+e.Message);}
    }
}
