using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Utilities;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
    private readonly StudioVersionRepository? _versionRepository;
    public RelayCommand RenameEditVersionCommand { get; private set; }=null!;
    public AsyncRelayCommand ArchiveEditVersionCommand { get; private set; }=null!;
    public AsyncRelayCommand SaveEditVersionsCommand { get; private set; }=null!;
    public string EditVersionName {get;set;}="";
    public StudioEditVersion? SelectedCatalogVersion {get;set;}
    public AsyncRelayCommand SelectEditVersionCommand {get;private set;}=null!;
    private async Task LoadPersistentVersionsAsync(ReferenceTargetItem target,CancellationToken token)
    {
        if(_versionRepository is null||target.SourceHash is null||target.VersionCatalog is not null)return;
        var key=StudioVersionRepository.Identity(target.LibraryId,target.AssetId,target.SourceHash);
        var catalog=await _versionRepository.LoadAsync(key,token);
        if(target.LoadedCatalogRevision is long sessionRevision && catalog.Revision!=sessionRevision && target.EditVersions.Count>0 && !StudioVersionRepository.SameContent(catalog,target.EditVersions.ToArray(),target.ActiveEditVersionId))throw new IOException("会话版本与目录修订不同，保留当前会话；请另存后处理冲突。");
        if(target.RestoredSession && target.LoadedCatalogRevision is null && target.EditVersions.Any(v=>catalog.Versions.Any(c=>c.Id==v.Id&&System.Text.Json.JsonSerializer.Serialize(c)!=System.Text.Json.JsonSerializer.Serialize(v))))throw new IOException("旧会话与目录同名版本内容冲突，未覆盖任何参数。");
        target.VersionCatalog=catalog;
        if(catalog.Versions.Count==0){if(target.EditVersions.Count==0)target.ActiveEditVersionId=StudioVersionRepository.DefaultVersionId(key);CaptureEditVersion(target);return;}
        if(target.RestoredSession && target.EditVersions.Count==0)CaptureEditVersion(target);
        var requestedVersion=target.RestoredSession?target.ActiveEditVersionId:catalog.SelectedId;
        // Embedded session is a recoverable snapshot; merge unique old IDs, never overwrite repository revisions.
        var embedded=target.EditVersions.ToArray();target.EditVersions.Clear();foreach(var v in catalog.Versions)target.EditVersions.Add(v);
        foreach(var v in embedded.Where(v=>!catalog.Versions.Any(c=>c.Id==v.Id)))target.EditVersions.Add(v);
        var active=target.EditVersions.FirstOrDefault(v=>v.Id==requestedVersion&&!v.Archived)??target.EditVersions.FirstOrDefault(v=>!v.Archived);
        if(active is not null){target.ActiveEditVersionId=active.Id;target.AppliedLookSnapshot=active.Look;target.ColorAdjustmentStackSnapshot=active.Stack?.DeepClone();target.FilmSettingsSnapshot=active.Film;target.EngineSnapshot=active.Engine;target.ExecutionModeSnapshot=active.Mode;}
    }
    public async Task SaveEditVersionsAsync(CancellationToken token=default)
    {
        if(_versionRepository is null)return;
        if(ActiveTarget is {} active)Editor.CopyCurrentLookTo([active]);
        foreach(var target in Targets.Where(t=>t.SourceHash is not null))
        {
            await LoadPersistentVersionsAsync(target,token);CaptureEditVersion(target);
            target.VersionCatalog=await _versionRepository.SaveAsync(target.VersionCatalog!,target.EditVersions.ToArray(),target.ActiveEditVersionId,token);target.LoadedCatalogRevision=target.VersionCatalog.Revision;
        }
        StatusText="编辑版本目录已保存";
    }
    public AsyncRelayCommand RelocateTargetCommand { get; private set; } = null!;
    public RelayCommand CreateEditVersionCommand { get; private set; } = null!;
    public AsyncRelayCommand NextEditVersionCommand { get; private set; } = null!;
    private void CaptureEditVersion(ReferenceTargetItem target)
    {
        for(var i=0;i<target.EditVersions.Count;i++) if(target.EditVersions[i].SourceHash is null && target.SourceHash is not null) target.EditVersions[i]=target.EditVersions[i] with{SourceHash=target.SourceHash};
        target.ActiveEditVersionId ??= target.Id; // deterministic default migration for old v1 sessions
        var existing=target.EditVersions.FirstOrDefault(v=>v.Id==target.ActiveEditVersionId);
        var snapshot=new StudioEditVersion(target.ActiveEditVersionId.Value,existing?.Name??"默认版本",existing?.CreatedAt??DateTimeOffset.UtcNow,
            target.LibraryId,target.AssetId,target.SourceHash,target.AppliedLookSnapshot,target.ColorAdjustmentStackSnapshot?.DeepClone(),target.FilmSettingsSnapshot,target.EngineSnapshot,target.ExecutionModeSnapshot){Archived=existing?.Archived??false};
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
        SelectEditVersionCommand=new AsyncRelayCommand(async _=>{if(SelectedCatalogVersion is {} v && !v.Archived)await SwitchEditVersionAsync(v.Id);});
        SaveEditVersionsCommand=new AsyncRelayCommand(async _=>{try{await SaveEditVersionsAsync();}catch(Exception e)when(e is IOException or InvalidOperationException){StatusText="版本保存未完成："+e.Message;}});
        RenameEditVersionCommand=new RelayCommand(_=>{if(ActiveTarget is not {} target||string.IsNullOrWhiteSpace(EditVersionName))return;CaptureEditVersion(target);var index=target.EditVersions.ToList().FindIndex(v=>v.Id==target.ActiveEditVersionId);target.EditVersions[index]=target.EditVersions[index] with{Name=EditVersionName.Trim()};});
        ArchiveEditVersionCommand=new AsyncRelayCommand(async _=>{if(ActiveTarget is not {} target)return;CaptureEditVersion(target);var next=target.EditVersions.FirstOrDefault(v=>v.Id!=target.ActiveEditVersionId&&!v.Archived);if(next is null){StatusText="至少保留一个版本";return;}var index=target.EditVersions.ToList().FindIndex(v=>v.Id==target.ActiveEditVersionId);var old=target.EditVersions[index];await SwitchEditVersionAsync(next.Id);target.EditVersions[index]=old with{Archived=true};});
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
            var available=target.EditVersions.Where(v=>!v.Archived).ToArray();var index=Array.FindIndex(available,v=>v.Id==target.ActiveEditVersionId); await SwitchEditVersionAsync(available[(index+1)%available.Length].Id);
        });
    }
}
