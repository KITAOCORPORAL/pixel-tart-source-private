using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class PlanningCenterViewModel
{
    public bool IsAdvancedPlanning => _document.Presentation.Mode == "Advanced";
    public IReadOnlyList<PlanningStructureBlock> StructureBlocks => _document.Presentation.Blocks;
    public async Task<bool> SwitchPlanningModeAsync(bool advanced)
    {
        if(!await FlushAsync())return false;
        if(advanced==IsAdvancedPlanning)return true;
        CapturePlanningUndo();
        var old=_document.Presentation;
        var next=advanced?old.Initialize(Shots.Select(s=>s.ShotId),ResourceCatalog.Items.Where(i=>!i.Archived).Select(i=>i.Id)):old;
        UpdateDocument(_document with { Presentation=next with {Mode=advanced?"Advanced":"Simple"} });
        if(!await FlushAsync())
        {
            // Keep the proposal draft, but never display the new mode until persistence succeeds.
            UpdateDocument(_document with{Presentation=_document.Presentation with{Mode=old.Mode}});
            _saveDelay?.Cancel();
            if(ProjectId is Guid project) try { await _planning.WriteDraftAsync(project,_document,_state?.Summary??new(),shots:_pendingShots.Values.Where(s=>s.ProjectId==project).ToArray()); } catch(Exception e) when(e is IOException or UnauthorizedAccessException) { ResourceStatus="恢复草稿无法落盘；当前模式与内存草稿保留："+e.Message; }
            return false;
        }
        OnPropertyChanged(nameof(IsAdvancedPlanning));PresentationChanged();return true;
    }
    public async Task<bool> RemoveStructureBlockAsync(Guid blockId)
    {
        if(!await FlushAsync())return false;
        CapturePlanningUndo();UpdateDocument(_document with{Presentation=_document.Presentation with{Blocks=StructureBlocks.Where(b=>b.Id!=blockId).ToArray()}});
        var saved=await FlushAsync();if(saved)PresentationChanged();return saved;
    }
    public async Task<bool> RefreshStructureSourcesAsync()
    {
        if(!await FlushAsync())return false;
        CapturePlanningUndo();
        UpdateDocument(_document with{Presentation=_document.Presentation with { Blocks=StructureBlocks.Select(b=>b with { SourceIds=b.Kind=="Shots"?Shots.Select(s=>s.ShotId).ToArray():ResourceCatalog.Items.Where(i=>!i.Archived).Select(i=>i.Id).ToArray() }).ToArray() }});
        var saved=await FlushAsync();if(saved)PresentationChanged();return saved;
    }
    public void StageStructureDraft(ProjectShot shot)
    {
        if(!StructureBlocks.Any(b=>b.Kind=="Shots"&&b.SourceIds.Contains(shot.ShotId)))throw new InvalidOperationException("镜头不属于当前结构块。");
        CapturePlanningUndo();StageShot(shot);
    }
    public async Task<bool> ApplyStructureShotAsync(ProjectShot shot)
    {
        if(!StructureBlocks.Any(b=>b.Kind=="Shots"&&b.SourceIds.Contains(shot.ShotId)) || !await FlushAsync())return false;
        CapturePlanningUndo(); StageShot(shot);return await FlushAsync();
    }
}
