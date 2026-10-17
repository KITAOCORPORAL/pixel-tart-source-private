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
        // Validate the complete document before touching the active edit session.
        var fingerprint=await ColorStudioSessionStore.FingerprintAsync(path,token);
        var document = await ColorStudioSessionStore.LoadAsync(path, token);
        if(fingerprint!=await ColorStudioSessionStore.FingerprintAsync(path,token))throw new IOException("项目在读取期间改变，工作区未修改。");
        // Prepare all required files and active processing in an isolated workspace first.
        using(var staged=_sessionStagingFactory())
        {
            staged._skipSessionPreparation=true;
            await staged.LoadSessionCoreAsync(path,token,document);
            if(document.Targets.Count>0&&(staged.ActiveTarget is null||staged.ActiveTarget.Status==ReferenceTargetStatus.Failed))throw new InvalidDataException("项目激活失败，旧工作区保持。");
            if(staged.Targets.Any(t=>t.Status==ReferenceTargetStatus.Failed))throw new InvalidDataException("项目存在不可用照片，旧工作区保持。");
        }
        if(fingerprint!=await ColorStudioSessionStore.FingerprintAsync(path,token))throw new IOException("项目在准备期间改变，工作区未修改。");
        Kitao.InstantFilm.Research.CandidateProcessingEvidence.Enter("SessionPrepared",token);
        await LoadSessionCoreAsync(path,token,document);
        _sessionFingerprints[Path.GetFullPath(path)]=fingerprint;
    }
    private bool _skipSessionPreparation;
    private async Task LoadSessionCoreAsync(string path,CancellationToken token,ColorStudioSessionDocument? prepared=null)
    {
        var document=prepared??await ColorStudioSessionStore.LoadAsync(path,token);
        foreach(var item in document.Targets)if(!File.Exists(item.Path))throw new FileNotFoundException("项目源照片缺失，旧工作区保持。",item.Path);

        var restored = document.Targets.Select(item => new ReferenceTargetItem(item.Path, item.Id)
        {
            RawDefinition=item.RawDefinition,RawInput=item.RawInput,RestoredSession=true,EmbeddedVersionsAuthoritative=document.EmbeddedVersionsAuthoritative, AssetId = item.AssetId, LibraryId = item.LibraryId, SourceHash = item.SourceHash, IsSelected = item.Selected, AppliedLookSnapshot = item.Look, ColorAdjustmentStackSnapshot = item.Stack,
            FilmSettingsSnapshot = item.Film, EngineSnapshot = item.Engine, ExecutionModeSnapshot = item.ExecutionMode,
            Rating = item.AssetId is null ? item.SessionRating ?? 0 : 0, ColorLabel = item.AssetId is null ? item.SessionColorLabel : null
        }).ToArray();
        for(var i=0;i<restored.Length;i++) { foreach(var version in document.Targets[i].EditVersions ?? []) restored[i].EditVersions.Add(version); restored[i].ActiveEditVersionId=document.Targets[i].ActiveEditVersionId;restored[i].LoadedCatalogRevision=document.Targets[i].CatalogRevision; }
        if(ActiveTarget is{} current)Editor.CopyCurrentLookTo([current]);
        var history=Editor.CaptureSessionHistory();
        var oldTargets=Targets.ToArray();var oldActive=ActiveTarget;var oldImage=Editor.MatchedImage;var oldSource=Editor.SourceImage;var oldSize=Editor.SourcePixelSize;
        var oldFilter=FilterScope;var oldRating=MinimumRating;var oldLabel=ColorLabelFilter;var oldLayout=Layout;var oldSession=SessionPath;var oldAux=Editor.ContextRailOpen;var oldFailed=FailedTarget;var oldEngine=Editor.MatchEngine;var oldMode=Editor.MatchV4ExecutionMode;
        _sessionLoading = true; OnPropertyChanged(nameof(IsLoading)); RefreshSyncAvailability();
        try
        {
        Editor.StopProcessing(); _activationCancellation?.Cancel(); Interlocked.Increment(ref _activationRevision);
        await FlushMetadataAsync(); ActiveTarget = null; Targets.Clear();
        foreach (var target in restored) Targets.Add(target);
        if(!_skipSessionPreparation)Kitao.InstantFilm.Research.CandidateProcessingEvidence.Enter("SessionCommit",token);
        FilterScope = document.FilterScope; MinimumRating = document.MinimumRating; ColorLabelFilter = document.ColorLabelFilter;
        Layout = document.Layout ?? new(); Editor.ContextRailOpen = Layout.AuxiliaryOpen;
        var missing = 0;
        foreach (var target in restored)
        {
            token.ThrowIfCancellationRequested();

            if (!await ValidateTargetIdentityAsync(target, token)) { target.Status = ReferenceTargetStatus.Failed; missing++; continue; }
            await LoadThumbnailAsync(target, token);
        }
        await FlushMetadataAsync();token.ThrowIfCancellationRequested();
        if(restored.Any(t=>t.Status==ReferenceTargetStatus.Failed))throw new InvalidDataException("项目照片准备或激活失败，旧工作区保持。");
        var active = restored.FirstOrDefault(x => x.Id == document.ActiveTargetId && x.Status != ReferenceTargetStatus.Failed && File.Exists(x.Path)) ?? restored.FirstOrDefault(x => x.Status != ReferenceTargetStatus.Failed && File.Exists(x.Path));
        if (active is not null){await ActivateTargetAsync(active);token.ThrowIfCancellationRequested();if(active.Status==ReferenceTargetStatus.Failed)throw new InvalidDataException(active.Error??"项目激活失败");}
        else { TargetImage = null; TargetName = "尚未选择待调色照片"; Editor.ApplyTargetSnapshot(null,null,null,false); await Editor.SetSourceAsync(null, null, token); }
        RefreshFilmstrip(); SessionPath = Path.GetFullPath(path); OnPropertyChanged(nameof(SessionPath));
        _batchUndo.Clear();_batchRedo.Clear();RaiseBatchHistory();
        SetStatus("SessionRestored", restored.Length, missing);
        }
        catch
        {
            _activationCancellation?.Cancel();Interlocked.Increment(ref _activationRevision);
            Targets.Clear();foreach(var item in oldTargets)Targets.Add(item);
            FailedTarget=oldFailed;Editor.RestoreTargetEngine(oldEngine,oldMode);
            FilterScope=oldFilter;MinimumRating=oldRating;ColorLabelFilter=oldLabel;Layout=oldLayout;Editor.ContextRailOpen=oldAux;SessionPath=oldSession;
            _applyingActiveSnapshot=true;
            if(oldActive is not null)
            {
                ActiveTarget=oldActive;TargetImage=oldSource;TargetName=oldActive.FileName;
                Editor.RestoreTargetEngine(oldActive.EngineSnapshot,oldActive.ExecutionModeSnapshot);
                Editor.ApplyTargetSnapshot(oldActive.AppliedLookSnapshot,oldActive.FilmSettingsSnapshot,oldActive.ColorAdjustmentStackSnapshot,render:false,targetIdentity:oldActive.ActiveEditVersionId??oldActive.Id);
                await Editor.SetSourceAsync(oldActive.AssetId,oldSource,CancellationToken.None);Editor.StopProcessing();
                if(oldImage is not null){Editor.PresentIndependentRaw(oldImage);Editor.SetIndependentRawSize(oldSize);Editor.IndependentRawMode=oldActive.RawDefinition is not null;}
                RestoreInstantControls(oldActive.RawDefinition?.InstantFilm);
            }
            else {ActiveTarget=null;TargetImage=oldSource;TargetName="尚未选择待调色照片";await Editor.SetSourceAsync(null,oldSource,CancellationToken.None);}
            _applyingActiveSnapshot=false;Editor.RestoreSessionHistory(history);RefreshFilmstrip();OnPropertyChanged(nameof(SessionPath));throw;
        }
        finally { _applyingActiveSnapshot=false;_sessionLoading = false; OnPropertyChanged(nameof(IsLoading)); RefreshSyncAvailability(); }
    }
}
