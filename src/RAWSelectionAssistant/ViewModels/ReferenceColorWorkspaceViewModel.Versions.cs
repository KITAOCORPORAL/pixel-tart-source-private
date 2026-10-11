using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Utilities;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
    public AsyncRelayCommand RelocateTargetCommand { get; private set; } = null!;
    public RelayCommand CreateEditVersionCommand { get; private set; } = null!;
    public AsyncRelayCommand NextEditVersionCommand { get; private set; } = null!;
    private void CaptureEditVersion(ReferenceTargetItem target)
    {
        for(var i=0;i<target.EditVersions.Count;i++) if(target.EditVersions[i].SourceHash is null && target.SourceHash is not null) target.EditVersions[i]=target.EditVersions[i] with{SourceHash=target.SourceHash};
        target.ActiveEditVersionId ??= target.Id; // deterministic default migration for old v1 sessions
        var existing=target.EditVersions.FirstOrDefault(v=>v.Id==target.ActiveEditVersionId);
        var snapshot=new StudioEditVersion(target.ActiveEditVersionId.Value,existing?.Name??"默认版本",existing?.CreatedAt??DateTimeOffset.UtcNow,
            target.LibraryId,target.AssetId,target.SourceHash,target.AppliedLookSnapshot,target.ColorAdjustmentStackSnapshot?.DeepClone(),target.FilmSettingsSnapshot,target.EngineSnapshot,target.ExecutionModeSnapshot);
        if(existing is null)target.EditVersions.Add(snapshot);else target.EditVersions[target.EditVersions.IndexOf(existing)]=snapshot;
    }
    public void CreateEditVersion()
    {
        if(ActiveTarget is not {} target || IsLoading || IsExporting)return;
        Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);
        target.ActiveEditVersionId=Guid.NewGuid();CaptureEditVersion(target);
        var version=target.EditVersions.Last();target.EditVersions[^1]=version with{Name="版本 "+target.EditVersions.Count};
        StatusText="已创建编辑版本；保存工作文件以持久化。";
    }
    public async Task SwitchEditVersionAsync(Guid id)
    {
        if(ActiveTarget is not {} target || IsLoading || IsExporting)return;
        if(target.ActiveEditVersionId==id)return;
        var version=target.EditVersions.SingleOrDefault(v=>v.Id==id);if(version is null)return;
        Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);
        Editor.StopProcessing();_activationCancellation?.Cancel();Interlocked.Increment(ref _activationRevision);
        target.ActiveEditVersionId=id;target.AppliedLookSnapshot=version.Look;target.ColorAdjustmentStackSnapshot=version.Stack?.DeepClone();
        target.FilmSettingsSnapshot=version.Film;target.EngineSnapshot=version.Engine;target.ExecutionModeSnapshot=version.Mode;
        await ActivateTargetAsync(target);StatusText="当前编辑版本："+version.Name;
    }
    private void InitializeVersionCommands()
    {
        RelocateTargetCommand=new AsyncRelayCommand(async _=>
        {
            var target=FailedTarget??ActiveTarget;if(target is null)return;
            var paths=_dialogs.ChooseFiles("重新定位原片（按 hash 验证）","所有文件|*.*",false);if(paths.Count==0)return;
            if(target.LibraryId is not Guid libraryId || target.AssetId is not Guid assetId){StatusText="外部目标请先保留原片身份；当前入口需要素材库身份。";return;}
            await using var repo=OpenMetadataRepository(target);if(repo is null){StatusText="素材库离线，请先打开正确库。";return;}
            try
            {
                var root=Directory.GetParent(Path.GetDirectoryName(repo.DatabasePath)!)!.FullName;
                var descriptor=await new RAWSelectionAssistant.Core.Services.AssetLibrary.AssetLibraryContainerService().OpenAsync(root);
                var result=await new RAWSelectionAssistant.Core.Services.AssetLibrary.AssetRelocationService(descriptor).RelocateAsync(new(libraryId,assetId,target.SourceHash),paths[0]);
                StatusText=result.Message;if(result.Status==RAWSelectionAssistant.Core.Services.AssetLibrary.AssetResolutionStatus.Available)await ActivateTargetAsync(target);
            }
            catch(Exception e)when(e is IOException or InvalidOperationException){StatusText="重新定位未完成："+e.Message;}
        });
        CreateEditVersionCommand=new RelayCommand(_=>CreateEditVersion());
        NextEditVersionCommand=new AsyncRelayCommand(async _=>
        {
            if(ActiveTarget is not {} target)return;CaptureEditVersion(target);
            var index=target.EditVersions.ToList().FindIndex(v=>v.Id==target.ActiveEditVersionId);
            await SwitchEditVersionAsync(target.EditVersions[(index+1)%target.EditVersions.Count].Id);
        });
    }
}
