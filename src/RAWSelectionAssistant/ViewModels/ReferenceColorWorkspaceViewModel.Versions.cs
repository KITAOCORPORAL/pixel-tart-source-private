using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Utilities;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
    public AsyncRelayCommand CopyVersionToLibraryCommand {get;private set;}=null!;
    public Func<StudioVersionConflict,Task<string>>? ResolveVersionConflict {get;set;}
    private async Task<bool> ResolveConflictAsync(ReferenceTargetItem target,StudioVersionCatalog repository,string reason,CancellationToken token)
    {
        var local=System.Text.Json.JsonSerializer.Deserialize<StudioEditVersion[]>(System.Text.Json.JsonSerializer.Serialize(target.EditVersions.ToArray()))!;
        var conflict=new StudioVersionConflict(repository,local,target.ActiveEditVersionId,reason);
        await _versionRepository!.SaveRecoveryAsync(conflict,token);
        var choice=ResolveVersionConflict is null?"Cancel":await ResolveVersionConflict(conflict);
        if(choice=="Cancel")return false;
        var latest=await _versionRepository!.LoadAsync(repository.Identity,token);
        if(latest.Revision!=repository.Revision)throw new IOException("处理界面打开后仓储又发生改变，请重新处理。");
        StudioVersionCatalog resolved;
        if(choice=="KeepLocal")
        {
            var versions=StudioVersionConflictResolution.PreserveLocalAsNew(conflict);var selected=versions.LastOrDefault(v=>!v.Archived)?.Id;
            resolved=await _versionRepository.SaveAsync(repository,versions,selected,token);
        }
        else if(choice=="Reload")resolved=await _versionRepository.SaveAsync(repository,repository.Versions,repository.SelectedId,token);
        else return false;
        target.EditVersions.Clear();foreach(var version in resolved.Versions)target.EditVersions.Add(version);
        target.VersionCatalog=resolved;target.LoadedCatalogRevision=resolved.Revision;target.ActiveEditVersionId=resolved.SelectedId;
        if(resolved.Versions.FirstOrDefault(v=>v.Id==resolved.SelectedId) is {} active){target.AppliedLookSnapshot=active.Look;target.ColorAdjustmentStackSnapshot=active.Stack?.DeepClone();target.RawDefinition=active.RawDefinition;target.RawInput=active.RawInput;target.FilmSettingsSnapshot=active.Film;target.EngineSnapshot=active.Engine;target.ExecutionModeSnapshot=active.Mode;}
        return true;
    }
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
        if(target.RestoredSession&&target.EmbeddedVersionsAuthoritative&&target.EditVersions.Count>0)
        {
            // Portable project is authoritative for its own edit snapshot. Shared repository
            // remains independent; a later explicit save still compares the frozen revision.
            target.VersionCatalog=new(key,target.LoadedCatalogRevision??catalog.Revision,target.EditVersions.ToArray(),target.ActiveEditVersionId);
            return;
        }
        if(target.LoadedCatalogRevision is long sessionRevision && catalog.Revision!=sessionRevision && target.EditVersions.Count>0 && !StudioVersionRepository.SameContent(catalog,target.EditVersions.ToArray(),target.ActiveEditVersionId)){if(!await ResolveConflictAsync(target,catalog,"会话与仓储修订不同",token))throw new IOException("已取消，恢复快照保留。");return;}
        if(target.RestoredSession && target.LoadedCatalogRevision is null && target.EditVersions.Any(v=>catalog.Versions.Any(c=>c.Id==v.Id&&System.Text.Json.JsonSerializer.Serialize(c)!=System.Text.Json.JsonSerializer.Serialize(v)))){if(!await ResolveConflictAsync(target,catalog,"旧会话与同 ID 仓储版本不同",token))throw new IOException("已取消，旧快照保留。");return;}
        target.VersionCatalog=catalog;
        if(catalog.Versions.Count==0){if(target.EditVersions.Count==0)target.ActiveEditVersionId=StudioVersionRepository.DefaultVersionId(key);CaptureEditVersion(target);return;}
        if(target.RestoredSession && target.EditVersions.Count==0)CaptureEditVersion(target);
        var requestedVersion=target.RestoredSession?target.ActiveEditVersionId:catalog.SelectedId;
        // Embedded session is a recoverable snapshot; merge unique old IDs, never overwrite repository revisions.
        var embedded=target.EditVersions.ToArray();target.EditVersions.Clear();foreach(var v in catalog.Versions)target.EditVersions.Add(v);
        foreach(var v in embedded.Where(v=>!catalog.Versions.Any(c=>c.Id==v.Id)))target.EditVersions.Add(v);
        var active=target.EditVersions.FirstOrDefault(v=>v.Id==requestedVersion&&!v.Archived)??target.EditVersions.FirstOrDefault(v=>!v.Archived);
        if(active is not null){target.ActiveEditVersionId=active.Id;target.AppliedLookSnapshot=active.Look;target.ColorAdjustmentStackSnapshot=active.Stack?.DeepClone();target.RawDefinition=active.RawDefinition;target.RawInput=active.RawInput;target.FilmSettingsSnapshot=active.Film;target.EngineSnapshot=active.Engine;target.ExecutionModeSnapshot=active.Mode;}
    }
    public async Task SaveEditVersionsAsync(CancellationToken token=default)
    {
        if(_versionRepository is null)return;
        if(ActiveTarget is {} active)Editor.CopyCurrentLookTo([active]);
        foreach(var target in Targets.Where(t=>t.SourceHash is not null))
        {
            await LoadPersistentVersionsAsync(target,token);CaptureEditVersion(target);
            try{target.VersionCatalog=await _versionRepository.SaveAsync(target.VersionCatalog!,target.EditVersions.ToArray(),target.ActiveEditVersionId,token);target.LoadedCatalogRevision=target.VersionCatalog.Revision;}catch(InvalidOperationException){var remote=await _versionRepository.LoadAsync(target.VersionCatalog!.Identity,token);if(!await ResolveConflictAsync(target,remote,"保存期间其他工作区已修改",token))throw new IOException("已取消，当前参数保留。");if(ReferenceEquals(target,ActiveTarget))await ActivateTargetAsync(target);}
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
        if(existing?.IsOriginal==true)return;
        var snapshot=new StudioEditVersion(target.ActiveEditVersionId.Value,existing?.Name??"默认版本",existing?.CreatedAt??DateTimeOffset.UtcNow,
            target.LibraryId,target.AssetId,target.SourceHash,target.AppliedLookSnapshot,target.ColorAdjustmentStackSnapshot?.DeepClone(),target.FilmSettingsSnapshot,target.EngineSnapshot,target.ExecutionModeSnapshot){IsOriginal=existing?.IsOriginal??false,IsMain=existing?.IsMain??false,MainExplicit=existing?.MainExplicit??false,Rating=existing?.Rating??0,ColorLabel=existing?.ColorLabel,Archived=existing?.Archived??false,OriginVersionId=existing?.OriginVersionId,OriginIdentity=existing?.OriginIdentity,RawDefinition=target.RawDefinition,RawInput=target.RawInput};
        if(existing is null)target.EditVersions.Add(snapshot);else target.EditVersions[target.EditVersions.IndexOf(existing)]=snapshot;
    }
    public void CreateEditVersion()
    {
        if(ActiveTarget is not {} target || IsLoading || IsExporting || IsInstantGenerating)return;
        Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);EnsureR53Variants(target);
        target.ActiveEditVersionId=Guid.NewGuid();CaptureEditVersion(target);
        var version=target.EditVersions.Last();target.EditVersions[^1]=version with{Name="版本 "+target.EditVersions.Count};
        StatusText="已创建编辑版本；保存工作文件以持久化。";
    }
    public async Task SwitchEditVersionAsync(Guid id)
    {
        CloseR53VariantComparison();
        if(ActiveTarget is not {} target || IsLoading || IsExporting || IsInstantGenerating)return;
        await StopComparisonAsync();if(_disposed||!ReferenceEquals(ActiveTarget,target))return;
        if(target.ActiveEditVersionId==id)return;
        var version=target.EditVersions.SingleOrDefault(v=>v.Id==id&&!v.Archived);if(version is null){StatusText="引用变体已删除或不可用，未替换为其他变体。";return;}
        Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);
        Editor.StopProcessing();_activationCancellation?.Cancel();Interlocked.Increment(ref _activationRevision);
        target.ActiveEditVersionId=id;target.AppliedLookSnapshot=version.Look;target.ColorAdjustmentStackSnapshot=version.Stack?.DeepClone();
        target.RawDefinition=version.RawDefinition;target.RawInput=version.RawInput;target.FilmSettingsSnapshot=version.Film;target.EngineSnapshot=version.Engine;target.ExecutionModeSnapshot=version.Mode;
        await ActivateTargetAsync(target);NotifyR53Variants();StatusText="当前编辑版本："+version.Name;
    }
    private void InitializeVersionCommands()
    {
        CopyVersionToLibraryCommand=new AsyncRelayCommand(async _=>
        {
            if(_versionRepository is null||ActiveTarget is not {} target||target.SourceHash is null)return;if(!await ValidateTargetIdentityAsync(target,CancellationToken.None)){StatusText=target.Error??"源身份验证失败";return;}CaptureEditVersion(target);
            var root=_dialogs.ChooseFolder("明确选择目标 .ptlibrary",null);if(root is null)return;
            try
            {
                var library=await new RAWSelectionAssistant.Core.Services.AssetLibrary.AssetLibraryContainerService().OpenAsync(root);
                await using var repository=new RAWSelectionAssistant.Core.Services.AssetLibrary.SqliteAssetLibraryRepository(library.DatabasePath);
                var candidates=new List<RAWSelectionAssistant.Core.Models.AssetItem>();string? cursor=null;
                do{var page=await repository.QueryAsync(new(PageSize:500,Cursor:cursor));candidates.AddRange(page.Items.Where(a=>string.Equals(a.ContentHash,target.SourceHash,StringComparison.OrdinalIgnoreCase)));cursor=page.NextCursor;}while(cursor is not null);
                if(candidates.Count!=1){StatusText="目标库内容身份缺失或歧义，请先确认目标素材";return;}
                var asset=candidates[0];var resolved=await new RAWSelectionAssistant.Core.Services.AssetLibrary.AssetLibraryIdentityResolver(library,repository).ResolveAsync(new(library.LibraryId,asset.AssetId,target.SourceHash));
                if(resolved.Status!=RAWSelectionAssistant.Core.Services.AssetLibrary.AssetResolutionStatus.Available){StatusText=resolved.Message??"目标不可用";return;}
                if(!_dialogs.Confirm($"复制当前版本到库 {library.DisplayName}，素材 {asset.AssetId:D}？原目录和历史保留。","跨库版本复制"))return;
                if(!await ValidateTargetIdentityAsync(target,CancellationToken.None))throw new IOException("源内容在确认期间改变。");
                var confirmed=await new RAWSelectionAssistant.Core.Services.AssetLibrary.AssetLibraryIdentityResolver(library,repository).ResolveAsync(new(library.LibraryId,asset.AssetId,target.SourceHash));if(confirmed.Status!=RAWSelectionAssistant.Core.Services.AssetLibrary.AssetResolutionStatus.Available)throw new IOException("目标内容在确认期间改变。");
                await SaveEditVersionsAsync();var key=StudioVersionRepository.Identity(library.LibraryId,asset.AssetId,target.SourceHash);var destination=await _versionRepository.LoadAsync(key);
                await _versionRepository.CopySelectedAsync(target.VersionCatalog!.Identity,target.ActiveEditVersionId!.Value,destination,library.LibraryId,asset.AssetId,target.SourceHash);StatusText="已复制选中版本，来源记录保留";
            }
            catch(Exception e)when(e is IOException or InvalidOperationException or ArgumentException){StatusText="版本复制未完成："+e.Message;}
        });
        InitializeProfessionalRawCommands();
        SelectEditVersionCommand=new AsyncRelayCommand(async _=>{if(SelectedCatalogVersion is {} v && !v.Archived)await SwitchEditVersionAsync(v.Id);});
        SaveEditVersionsCommand=new AsyncRelayCommand(async _=>{try{await SaveEditVersionsAsync();}catch(Exception e)when(e is IOException or InvalidOperationException){StatusText="版本保存未完成："+e.Message;}});
        RenameEditVersionCommand=new RelayCommand(_=>{if(ActiveTarget is not {} target||string.IsNullOrWhiteSpace(EditVersionName))return;CaptureEditVersion(target);var index=target.EditVersions.ToList().FindIndex(v=>v.Id==target.ActiveEditVersionId);target.EditVersions[index]=target.EditVersions[index] with{Name=EditVersionName.Trim()};NotifyR53Variants();ScheduleR53Autosave();});
        ArchiveEditVersionCommand=new AsyncRelayCommand(async _=>await DeleteR53VariantAsync());
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
