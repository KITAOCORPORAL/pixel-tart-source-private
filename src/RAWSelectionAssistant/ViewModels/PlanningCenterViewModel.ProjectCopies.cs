using System.IO;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class PlanningCenterViewModel
{
    private bool? ChooseProjectCopy()
    {
        if (_dialogs.Confirm("导入项目副本？\n是：复制到项目管理目录，记录来源和SHA-256，源文件不变。\n否：下一步可选择仅关联原位置。单文件上限64MB。", "资源归属")) return true;
        return _dialogs.Confirm("仅关联原位置（外链）？\n不复制文件，搬迁后原位置不可用时不会自动恢复。\n否：取消本次添加。", "保留外链或取消") ? false : null;
    }
    public async Task<bool> ImportProjectCopiesAsync(IReadOnlyList<PlanningDocumentReference> references, IReadOnlyList<PlanningAttachment> attachments, CancellationToken token = default)
    {
        if (ProjectId is not Guid project || !await FlushAsync(token)) return false;
        // The store commits one transaction before a reference is exposed to the editor/autosave maps.
        await _saveGate.WaitAsync(token);
        try
        {
            await _planning.ImportProjectCopiesAsync(project, references, attachments, token);
            if (ProjectId == project)
            {
                _state = await _planning.LoadAsync(project, CancellationToken.None);
                var saved=_state.Document!;
                if(_pendingDocuments.ContainsKey(project))
                {
                    // Edits made while copying are not discarded or allowed to overwrite the new receipts.
                    var mergedReferences=_document.References.ToList();
                    foreach(var imported in saved.References.Where(r=>r.ProjectCopy is not null)){var index=mergedReferences.FindIndex(r=>r.Source.ReferenceId==imported.Source.ReferenceId);if(index<0)mergedReferences.Add(imported);else mergedReferences[index]=mergedReferences[index] with{ProjectCopy=imported.ProjectCopy};}
                    var mergedAttachments=_document.Attachments.ToList();
                    foreach(var imported in saved.Attachments.Where(a=>a.ProjectCopy is not null)){var index=mergedAttachments.FindIndex(a=>a.Id==imported.Id);if(index<0)mergedAttachments.Add(imported);else mergedAttachments[index]=mergedAttachments[index] with{ProjectCopy=imported.ProjectCopy};}
                    _document=_document with{References=mergedReferences,Attachments=mergedAttachments};QueueDocumentSave();
                }
                else _document = saved;
                RefreshAttachments(); await RefreshDocumentReferencesAsync(); OnPropertyChanged(string.Empty); PresentationChanged();
            }
            SaveStatus = "项目副本已导入并保存；原来源保持不变"; return true;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OperationCanceledException)
        { SaveStatus = "项目副本导入未完成，未新增有效引用：" + error.Message; _dialogs.ShowError(SaveStatus); return false; }
        finally { _saveGate.Release(); }
    }
    public string? ResolveAttachmentPath(PlanningAttachment item)
    {
        if (item.ProjectCopy is not {} copy) return File.Exists(item.Path) ? item.Path : null;
        try { PlanningProjectCopyStorage.ReadVerified(copy); return PlanningProjectCopyStorage.Resolve(copy); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { SaveStatus = "项目副本不可用：" + error.Message; return null; }
    }
    public string AttachmentStatus(PlanningAttachment item) => item.ProjectCopy is {} copy ? PlanningProjectCopyStorage.Status(copy) : "外链 · " + PlanningReadableSummaryBuilder.ReferenceState(item.Path);
    public void RevealAttachment(PlanningAttachment item) { if (ResolveAttachmentPath(item) is string path) _dialogs.RevealFile(path); else _dialogs.ShowInfo("文件不可用，未回退原来源。"); }
}
