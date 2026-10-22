using System.IO;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.ViewModels;
public sealed record PlanningExternalDependency(Guid Id, string Kind, string Name, string Section, string OriginalPath, string Reason, bool CanReconnect);
public sealed partial class PlanningCenterViewModel
{
    public IReadOnlyList<PlanningExternalDependency> ExternalDependencies => _document.References.Where(x=>x.ProjectCopy is null).Select(x=>
        new PlanningExternalDependency(x.Source.ReferenceId,"reference",x.Source.Title??"未命名参考",x.Category+" / "+x.Group+(x.IsMoodboard?" / 情绪板":""),x.Source.ExternalReference??$"素材库 {x.Source.LibraryId}/{x.Source.AssetId}",
            x.Source.LibraryId is not null?"素材库原件未打包；本功能不定位或扫描素材库":ExternalReason(x.ReconnectedFile,x.Source.ReferenceId,"reference",x.Source.ExternalReference),x.Source.LibraryId is null && x.Source.ExternalReference is not null))
        .Concat(_document.Attachments.Where(x=>x.ProjectCopy is null).Select(x=>new PlanningExternalDependency(x.Id,"attachment",x.Name,"文件",x.Path,ExternalReason(x.ReconnectedFile,x.Id,"attachment",x.Path),true))).ToArray();
    public IReadOnlyList<string> UnsupportedPackageDependencies => _document.Extensions?.TryGetValue("PackageImportExcludedDependencies",out var value)==true && value.ValueKind==System.Text.Json.JsonValueKind.Array
        ? value.EnumerateArray().Select(x=>x.GetString()??"").Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray() : ["素材库原件、档期、账本、画布及色彩方案不在本功能重连范围；不会自动扫描个人目录。"];
    private string ExternalReason(PlanningExternalFile? file, Guid id, string kind, string? original)
    {
        if(file is not null && original is not null)
        {
            try { PlanningExternalFile.ResolveVerified(file,id,kind,original);return "已手动重连 · 类型/大小/SHA-256已核对"; }
            catch(Exception e)when(PackageError(e)){return "手动重连文件不可用："+e.Message;}
        }
        if(IsPackageImported || ImportedPackageRoot is not null)return "项目包未包含原文件；为避免误读同名路径，须手动定位";
        return string.IsNullOrWhiteSpace(original)?"缺少原路径":File.Exists(original)?"外链仍在原位置；未复制":"原路径文件缺失或不可访问";
    }
    public async Task LocateExternalAsync(PlanningExternalDependency item,bool importCopy)
    {
        if(!item.CanReconnect)return;
        var path=_dialogs.ChooseFiles("定位新文件："+item.Name,item.Kind=="reference"?"参考图|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.tif;*.tiff":"普通附件|*.*",false).FirstOrDefault();
        if(path is null){SaveStatus="已取消定位，策划及已有副本不变";return;}
        try
        {
            var selected=await PlanningExternalFile.InspectAsync(item.Id,item.Kind,item.OriginalPath,path);
            if(!_dialogs.Confirm($"原引用ID：{item.Id}\n分区：{item.Section}\n原路径：{item.OriginalPath}\n新文件：{selected.SelectedPath}\n类型：{selected.FileType} · 大小：{selected.Bytes} bytes\nSHA-256：{selected.Sha256}\n原文件未打包，不能证明与原内容相同，请确认这就是要使用的替代文件。\n"+(importCopy?"导入项目自有副本，原文件不变。":"继续外链，不复制，重启后仍核对本次hash。"),"确认逐项重连")){SaveStatus="已取消确认，策划及已有副本不变";return;}
            await ReconnectExternalAsync(selected,importCopy);
        }
        catch(Exception e)when(PackageError(e)){SaveStatus="重连未完成，策划及已有副本不变："+e.Message;_dialogs.ShowError(SaveStatus);}
    }
    public async Task<bool> ReconnectExternalAsync(PlanningExternalFile selected,bool importCopy,CancellationToken token=default)
    {
        if(ProjectId is not Guid project || !await FlushAsync(token))return false;
        await _saveGate.WaitAsync(token);
        try
        {
            await _planning.ReconnectExternalAsync(project,selected,importCopy,token);
            if(ProjectId==project)
            {
                var saved=(await _planning.LoadAsync(project,CancellationToken.None)).Document!;
                var reference=saved.References.SingleOrDefault(x=>x.Source.ReferenceId==selected.ReferenceId);
                var attachment=saved.Attachments.SingleOrDefault(x=>x.Id==selected.ReferenceId);
                _document=_document with
                {
                    References=_document.References.Select(x=>reference is not null && x.Source.ReferenceId==selected.ReferenceId?x with{ProjectCopy=reference.ProjectCopy,ReconnectedFile=reference.ReconnectedFile}:x).ToArray(),
                    Attachments=_document.Attachments.Select(x=>attachment is not null && x.Id==selected.ReferenceId?x with{ProjectCopy=attachment.ProjectCopy,ReconnectedFile=attachment.ReconnectedFile}:x).ToArray()
                };
                // Preserve edits made while copying; their next save must carry the new receipt.
                if(_pendingDocuments.ContainsKey(project))QueueDocumentSave();
                RefreshAttachments();await RefreshDocumentReferencesAsync();PresentationChanged();
            }
            SaveStatus=importCopy?"已核对并导入项目副本；引用ID、原路径和分区不变":"已核对并保存外链；引用ID、原路径和分区不变";return true;
        }
        finally{_saveGate.Release();}
    }
}
