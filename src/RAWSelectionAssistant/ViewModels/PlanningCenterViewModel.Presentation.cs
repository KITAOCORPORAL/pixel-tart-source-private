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
            _saveDelay?.Cancel();return false;
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
    public async Task<bool> ApplyStructureShotAsync(ProjectShot shot)
    {
        if(!StructureBlocks.Any(b=>b.Kind=="Shots"&&b.SourceIds.Contains(shot.ShotId)) || !await FlushAsync())return false;
        CapturePlanningUndo(); StageShot(shot);return await FlushAsync();
    }
}
