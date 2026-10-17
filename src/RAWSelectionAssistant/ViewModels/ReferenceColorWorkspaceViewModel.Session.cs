using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Utilities;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class ReferenceColorWorkspaceViewModel
{
    public AsyncRelayCommand SaveSessionCommand { get; private set; } = null!;
    public AsyncRelayCommand OpenSessionCommand { get; private set; } = null!;
    public string? SessionPath { get; private set; }
    private bool _sessionLoading;
    private CancellationTokenSource? _sessionPreparationCancellation;
    private readonly Dictionary<string,string?> _sessionFingerprints=new(StringComparer.OrdinalIgnoreCase);
    private ColorStudioLayout _layout = new();
    public ColorStudioLayout Layout { get => _layout; set => SetProperty(ref _layout, value.Normalize()); }
    private void InitializeSessionCommands()
    {
        InitializeVersionCommands();InitializeAnnotationUndo();
        UndoBatchAdjustmentCommand = new RelayCommand(_ => RestoreBatchAdjustment(false), _ => _batchUndo.Count > 0 && !IsLoading && !IsExporting);
        RedoBatchAdjustmentCommand = new RelayCommand(_ => RestoreBatchAdjustment(true), _ => _batchRedo.Count > 0 && !IsLoading && !IsExporting);
        SaveSessionCommand = new AsyncRelayCommand(async _ =>
        {
            var path = _dialogs.ChooseSaveFile(StudioLocalizationService.Current["SessionSaveTitle"], StudioLocalizationService.Current["SessionFilter"] + "|*.ptstudio.json", ".ptstudio.json", "ColorStudio.ptstudio.json");
            if (path is null) return;
            try
            {
                var full=Path.GetFullPath(path);
                if(File.Exists(full)&&!_sessionFingerprints.ContainsKey(full))
                {
                    if(!_dialogs.Confirm("该工作文件尚未由本工作区打开。是否以当前快照替换？其他实例之后的修改仍会被检测。","替换工作文件"))return;
                    _sessionFingerprints[full]=await ColorStudioSessionStore.FingerprintAsync(full);
                }
                await SaveSessionAsync(full);
            } catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException or InvalidOperationException) { StatusText = StudioLocalizationService.Current.Format("SessionSaveFailed", error.Message); }
        }, _ => Targets.Count > 0 && !IsLoading && !IsExporting);
        OpenSessionCommand = new AsyncRelayCommand(async _ =>
        {
            var path = _dialogs.ChooseFiles(StudioLocalizationService.Current["SessionOpenTitle"], StudioLocalizationService.Current["SessionFilter"] + "|*.ptstudio.json|JSON|*.json", false).FirstOrDefault();
            if (path is null) return;
            if (Targets.Count > 0 && !_dialogs.Confirm(StudioLocalizationService.Current["SessionReplace"], StudioLocalizationService.Current["SessionOpenTitle"])) return;
            try { await LoadSessionAsync(path); } catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException or InvalidOperationException) { StatusText = StudioLocalizationService.Current.Format("SessionOpenFailed", error.Message); }
        }, _ => !IsLoading && !IsExporting);
    }
    public async Task SaveSessionAsync(string path, CancellationToken token = default)
    {
        if (ActiveTarget is { } active) Editor.CopyCurrentLookTo([active]);
        await FlushMetadataAsync();
        foreach(var target in Targets) CaptureEditVersion(target);
        var document = new ColorStudioSessionDocument(Targets.Select(item => new ColorStudioSessionTarget(item.Id, item.Path, item.AssetId, item.IsSelected,
            item.AppliedLookSnapshot, item.ColorAdjustmentStackSnapshot, item.FilmSettingsSnapshot, item.EngineSnapshot, item.ExecutionModeSnapshot,
            item.AssetId is null ? item.Rating : null, item.AssetId is null ? item.ColorLabel : null, item.LibraryId, item.SourceHash, item.EditVersions.ToArray(), item.ActiveEditVersionId,item.VersionCatalog?.Revision,item.RawDefinition,item.RawInput)).ToArray(), ActiveTarget?.Id, FilterScope, MinimumRating, ColorLabelFilter,
            Layout: Layout with { AuxiliaryOpen = Editor.ContextRailOpen }){EmbeddedVersionsAuthoritative=true};
        path=Path.GetFullPath(path);
        if(Targets.Any(t=>string.Equals(Path.GetFullPath(t.Path),path,StringComparison.OrdinalIgnoreCase)))throw new IOException("不能将工作文件保存到源照片路径。");
        // Session embeds its versions as one portable snapshot. Repository persistence is an
        // explicit separate command, not a hidden second write between two project saves.
        if(!_sessionFingerprints.TryGetValue(path,out var expected)&&File.Exists(path))throw new InvalidOperationException("此工作区未读取该工作文件；请先打开或另存新文件，未覆盖。");
        _sessionFingerprints[path]=await ColorStudioSessionStore.SaveCheckedAsync(path,document,expected,token);
        SessionPath = Path.GetFullPath(path); OnPropertyChanged(nameof(SessionPath)); StatusText = "SessionSaved";
    }
    public async Task LoadSessionAsync(string path, CancellationToken token = default)
    {
        if(_skipSessionPreparation){await LoadSessionCoreAsync(path,token);return;}
        if(_disposed||IsLoading||IsInstantGenerating||IsExporting)throw new InvalidOperationException("工作区正在处理，未开始加载项目。");
        using var preparation=CancellationTokenSource.CreateLinkedTokenSource(token);
        _sessionPreparationCancellation=preparation;token=preparation.Token;
        _sessionLoading=true;OnPropertyChanged(nameof(IsLoading));RefreshSyncAvailability();
        try
        {
        // Validate the complete document before touching the active edit session.
        var fingerprint=await ColorStudioSessionStore.FingerprintAsync(path,token);
        var document = await ColorStudioSessionStore.LoadAsync(path, token);
        if(fingerprint!=await ColorStudioSessionStore.FingerprintAsync(path,token))throw new IOException("项目在读取期间改变，工作区未修改。");
        // Staging owns prepared targets until the complete transfer succeeds. The real
        // workspace remains unchanged during decode/render. Never create a second set.
        using var oldMasterLease=ActiveTarget?.LinearRawMaster is {} oldMaster?StudioGlobalResources.RetainBuffer(oldMaster.Image,(long)oldMaster.Image.Rgb32.Length*4):null;
        using var staged=_sessionStagingFactory();
        var expectedActive=ActiveTarget;var expectedRevision=Volatile.Read(ref _activationRevision);
        staged._skipSessionPreparation=true;
        await staged.LoadSessionCoreAsync(path,token,document);
        if(document.Targets.Count>0&&(staged.ActiveTarget is null||staged.ActiveTarget.Status==ReferenceTargetStatus.Failed))throw new InvalidDataException("项目激活失败，旧工作区保持。");
        if(staged.Targets.Any(t=>t.Status==ReferenceTargetStatus.Failed))throw new InvalidDataException("项目存在不可用照片，旧工作区保持。");
        if(fingerprint!=await ColorStudioSessionStore.FingerprintAsync(path,token))throw new IOException("项目在准备期间改变，工作区未修改。");
        Kitao.InstantFilm.Research.CandidateProcessingEvidence.Enter("SessionPrepared",token);
        foreach(var target in staged.Targets)
        {
            if(!await staged.ValidateTargetIdentityAsync(target,token))throw new InvalidDataException(target.Error);
            target.RawDefinition?.InstantFilm?.V3?.MultipleInput?.ValidateSource(token);
        }
        token.ThrowIfCancellationRequested();
        if(fingerprint!=await ColorStudioSessionStore.FingerprintAsync(path,token))throw new IOException("项目在移交前改变，工作区未修改。");
        if(_disposed||!ReferenceEquals(ActiveTarget,expectedActive)||expectedRevision!=Volatile.Read(ref _activationRevision))throw new OperationCanceledException("项目准备期间工作区已改变。",token);
        await LoadSessionCoreAsync(path,token,document,staged);
        _sessionFingerprints[Path.GetFullPath(path)]=fingerprint;
        }
        finally{_sessionPreparationCancellation=null;_sessionLoading=false;OnPropertyChanged(nameof(IsLoading));RefreshSyncAvailability();}
    }
    private bool _skipSessionPreparation;
    private async Task LoadSessionCoreAsync(string path,CancellationToken token,ColorStudioSessionDocument? prepared=null,ReferenceColorWorkspaceViewModel? staged=null)
    {
        var document=prepared??await ColorStudioSessionStore.LoadAsync(path,token);
        foreach(var item in document.Targets)if(!File.Exists(item.Path))throw new FileNotFoundException("项目源照片缺失，旧工作区保持。",item.Path);

        var restored = staged?.Targets.ToArray() ?? document.Targets.Select(item => new ReferenceTargetItem(item.Path, item.Id)
        {
            RawDefinition=item.RawDefinition,RawInput=item.RawInput,RestoredSession=true,EmbeddedVersionsAuthoritative=document.EmbeddedVersionsAuthoritative, AssetId = item.AssetId, LibraryId = item.LibraryId, SourceHash = item.SourceHash, IsSelected = item.Selected, AppliedLookSnapshot = item.Look, ColorAdjustmentStackSnapshot = item.Stack,
            FilmSettingsSnapshot = item.Film, EngineSnapshot = item.Engine, ExecutionModeSnapshot = item.ExecutionMode,
            Rating = item.AssetId is null ? item.SessionRating ?? 0 : 0, ColorLabel = item.AssetId is null ? item.SessionColorLabel : null
        }).ToArray();
        if(staged is null)for(var i=0;i<restored.Length;i++) { foreach(var version in document.Targets[i].EditVersions ?? []) restored[i].EditVersions.Add(version); restored[i].ActiveEditVersionId=document.Targets[i].ActiveEditVersionId;restored[i].LoadedCatalogRevision=document.Targets[i].CatalogRevision; }
        if(ActiveTarget is{} current)Editor.CopyCurrentLookTo([current]);
        var history=Editor.CaptureSessionHistory();
        var oldTargets=Targets.ToArray();var oldActive=ActiveTarget;var oldImage=Editor.MatchedImage;var oldSource=Editor.SourceImage;var oldSize=Editor.SourcePixelSize;var oldIndependent=Editor.IndependentRawMode;var oldEnabled=Editor.Enabled;var oldView=Editor.ViewMode;
        var oldFilter=FilterScope;var oldRating=MinimumRating;var oldLabel=ColorLabelFilter;var oldLayout=Layout;var oldSession=SessionPath;var oldAux=Editor.ContextRailOpen;var oldFailed=FailedTarget;var oldEngine=Editor.MatchEngine;var oldMode=Editor.MatchV4ExecutionMode;
        _sessionLoading = true; OnPropertyChanged(nameof(IsLoading)); RefreshSyncAvailability();
        try
        {
        Editor.StopProcessing(); _activationCancellation?.Cancel(); Interlocked.Increment(ref _activationRevision);
        await FlushMetadataAsync();token.ThrowIfCancellationRequested();if(_disposed)throw new OperationCanceledException(token); ActiveTarget = null; Targets.Clear();
        foreach (var target in restored) Targets.Add(target);
        if(!_skipSessionPreparation)Kitao.InstantFilm.Research.CandidateProcessingEvidence.Enter("SessionCommit",token);
        FilterScope = document.FilterScope; MinimumRating = document.MinimumRating; ColorLabelFilter = document.ColorLabelFilter;
        Layout = document.Layout ?? new(); Editor.ContextRailOpen = Layout.AuxiliaryOpen;
        var missing = 0;
        foreach (var target in restored)
        {
            token.ThrowIfCancellationRequested();

            if (!await ValidateTargetIdentityAsync(target, token)) { target.Status = ReferenceTargetStatus.Failed; missing++; continue; }
            if(staged is null && target.Id!=document.ActiveTargetId)await LoadThumbnailAsync(target, token);
        }
        await FlushMetadataAsync();token.ThrowIfCancellationRequested();
        if(restored.Any(t=>t.Status==ReferenceTargetStatus.Failed))throw new InvalidDataException("项目照片准备或激活失败，旧工作区保持。");
        var active = restored.FirstOrDefault(x => x.Id == document.ActiveTargetId && x.Status != ReferenceTargetStatus.Failed && File.Exists(x.Path)) ?? restored.FirstOrDefault(x => x.Status != ReferenceTargetStatus.Failed && File.Exists(x.Path));
        if (active is not null)
        {
            if(staged is null)await ActivateTargetAsync(active);
            else
            {
                ActiveTarget=active;TargetName=active.FileName;TargetImage=staged.TargetImage;
                Editor.RestoreTargetEngine(active.EngineSnapshot,active.ExecutionModeSnapshot);
                Editor.ApplyTargetSnapshot(active.AppliedLookSnapshot,active.FilmSettingsSnapshot,active.ColorAdjustmentStackSnapshot,render:false,targetIdentity:active.ActiveEditVersionId??active.Id);
                Editor.IndependentRawMode=staged.Editor.IndependentRawMode;
                await Editor.SetSourceAsync(active.AssetId,staged.Editor.SourceImage,token,render:false);
                if(staged.Editor.MatchedImage is {} image)Editor.PresentIndependentRaw(image);
                Editor.Enabled=staged.Editor.Enabled;Editor.ViewMode=staged.Editor.ViewMode;
                Editor.SetIndependentRawSize(staged.Editor.SourcePixelSize);
                RawExposureEv=staged.RawExposureEv;RawRedGain=staged.RawRedGain;RawGreenGain=staged.RawGreenGain;RawBlueGain=staged.RawBlueGain;RawUseCameraWhiteBalance=staged.RawUseCameraWhiteBalance;
                RawPipelineStatus=staged.RawPipelineStatus;InstantDimensions=staged.InstantDimensions;FailedTarget=staged.FailedTarget;
                foreach(var name in new[]{nameof(RawExposureEv),nameof(RawRedGain),nameof(RawGreenGain),nameof(RawBlueGain),nameof(RawUseCameraWhiteBalance),nameof(RawPipelineStatus),nameof(InstantDimensions)})OnPropertyChanged(name);
                RestoreInstantControls(active.RawDefinition?.InstantFilm);
                Kitao.InstantFilm.Research.CandidateProcessingEvidence.Enter("SessionTransfer",token);
                foreach(var item in restored)
                {
                    if(!await ValidateTargetIdentityAsync(item,token))throw new InvalidDataException(item.Error);
                    item.RawDefinition?.InstantFilm?.V3?.MultipleInput?.ValidateSource(token);
                }
                token.ThrowIfCancellationRequested();if(_disposed)throw new OperationCanceledException(token);
                await RefreshPreviewAnalysisAsync(token,Editor.MatchedImage??Editor.SourceImage);
            }
            token.ThrowIfCancellationRequested();if(active.Status==ReferenceTargetStatus.Failed)throw new InvalidDataException(active.Error??"项目激活失败");
        }
        else { TargetImage = null; TargetName = "尚未选择待调色照片"; Editor.ApplyTargetSnapshot(null,null,null,false); await Editor.SetSourceAsync(null, null, token); }
        RefreshFilmstrip(); SessionPath = Path.GetFullPath(path); OnPropertyChanged(nameof(SessionPath));
        _batchUndo.Clear();_batchRedo.Clear();RaiseBatchHistory();
        SetStatus("SessionRestored", restored.Length, missing);
        if(staged is not null)
        {
            // Ownership of these exact target instances is now held only by this workspace.
            staged.Targets.Clear();staged.ActiveTarget=null;
            _comparisonKey=staged._comparisonKey;_comparisonImage=staged._comparisonImage;
            foreach(var old in oldTargets){old.LinearRawMaster=null;old.RawMaster=null;}
        }
        }
        catch
        {
            if(!_disposed)_activationCancellation?.Cancel();Interlocked.Increment(ref _activationRevision);
            if(_disposed)
            {
                foreach(var item in oldTargets.Concat(restored)){item.LinearRawMaster=null;item.RawMaster=null;}
                throw;
            }
            Targets.Clear();foreach(var item in oldTargets)Targets.Add(item);
            FailedTarget=oldFailed;Editor.RestoreTargetEngine(oldEngine,oldMode);
            FilterScope=oldFilter;MinimumRating=oldRating;ColorLabelFilter=oldLabel;Layout=oldLayout;Editor.ContextRailOpen=oldAux;SessionPath=oldSession;
            _applyingActiveSnapshot=true;
            if(oldActive is not null)
            {
                ActiveTarget=oldActive;TargetImage=oldSource;TargetName=oldActive.FileName;
                Editor.RestoreTargetEngine(oldActive.EngineSnapshot,oldActive.ExecutionModeSnapshot);
                Editor.ApplyTargetSnapshot(oldActive.AppliedLookSnapshot,oldActive.FilmSettingsSnapshot,oldActive.ColorAdjustmentStackSnapshot,render:false,targetIdentity:oldActive.ActiveEditVersionId??oldActive.Id);
                await Editor.SetSourceAsync(oldActive.AssetId,oldSource,CancellationToken.None,render:false);Editor.StopProcessing();
                if(oldImage is not null){Editor.PresentIndependentRaw(oldImage);Editor.SetIndependentRawSize(oldSize);Editor.IndependentRawMode=oldIndependent;Editor.Enabled=oldEnabled;Editor.ViewMode=oldView;}
                RestoreInstantControls(oldActive.RawDefinition?.InstantFilm);
            }
            else {ActiveTarget=null;TargetImage=oldSource;TargetName="尚未选择待调色照片";await Editor.SetSourceAsync(null,oldSource,CancellationToken.None);}
            _applyingActiveSnapshot=false;Editor.RestoreSessionHistory(history);RefreshFilmstrip();OnPropertyChanged(nameof(SessionPath));throw;
        }
        finally { _applyingActiveSnapshot=false;_sessionLoading = false; OnPropertyChanged(nameof(IsLoading)); RefreshSyncAvailability(); }
    }
}
