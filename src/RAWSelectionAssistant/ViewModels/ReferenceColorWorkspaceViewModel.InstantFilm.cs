using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
    private CancellationTokenSource? _instantCancellation;
    public StudioProcessingFailure? LastProcessingFailure {get;private set;}
    public IReadOnlyList<InstantFilmProfile> InstantProfiles=>InstantFilmProfiles.Available;
    public IReadOnlyList<string> InstantTiers {get;}=["稳定出片","轻微偶然","实验胶片"];
    private InstantFilmProfile _draftSelectedInstantProfile=InstantFilmProfiles.Available[0];
    public InstantFilmProfile SelectedInstantProfile {get=>_draftSelectedInstantProfile;set{if(IsLightPrintLocked&&!_restoringLightPrintControls)return;if(SetProperty(ref _draftSelectedInstantProfile,value)){OnPropertyChanged(nameof(SelectedPhotographerPaperId));OnPropertyChanged(nameof(PhotographerPaperName));OnPropertyChanged(nameof(PhotographerPaperDetail));OnPropertyChanged(nameof(PaperTechnicalDetail));}}}
    public string InstantTier {get;set;}="稳定出片";
    public bool InstantDiceEnabled {get;private set;}
    private bool _draftInstantCameraAnimation=true;
    public bool InstantCameraAnimation {get=>_draftInstantCameraAnimation;set=>SetProperty(ref _draftInstantCameraAnimation,value);}
    // Presentation preference only; not serialized into the pixel recipe.
    private bool _instantDevelopmentDemo;
    public bool InstantDevelopmentDemo {get=>_instantDevelopmentDemo;set=>SetProperty(ref _instantDevelopmentDemo,value);}
    private bool _skipInstantDemo;
    public RelayCommand SkipInstantDemoCommand {get;private set;}=null!;
    public sealed record InstantApplyTimings(double DecodeMs,double PixelMs,double DemoMs,double CommitMs,bool MasterCacheHit,double CompareMs=0);
    public InstantApplyTimings? LastInstantApplyTimings {get;private set;}
    public string InstantCamera {get;private set;}="SX-70";
    public IReadOnlyList<string> InstantInputModes {get;}=["CurrentEdits","Original"];
    private string _draftInstantInputMode="CurrentEdits";
    public string InstantInputMode {get=>_draftInstantInputMode;set{if(IsLightPrintLocked&&!_restoringLightPrintControls)return;SetProperty(ref _draftInstantInputMode,value);}}
    public IReadOnlyList<string> InstantLayouts {get;}=["SquareV1","LegacyFree"];
    private string _draftInstantLayout="SquareV1";
    public string InstantLayout {get=>_draftInstantLayout;set{if(IsLightPrintLocked&&!_restoringLightPrintControls)return;SetProperty(ref _draftInstantLayout,value);}}
    public IReadOnlyList<string> InstantFitModes {get;}=["CropFill","FitEntire"];
    private string _draftInstantFit="CropFill";
    public string InstantFit {get=>_draftInstantFit;set{if(IsLightPrintLocked&&!_restoringLightPrintControls)return;SetProperty(ref _draftInstantFit,value);}}
    private double _draftInstantCropX=.5;
    public double InstantCropX {get=>_draftInstantCropX;set{if(IsLightPrintLocked&&!_restoringLightPrintControls)return;SetProperty(ref _draftInstantCropX,value);}}
    private double _draftInstantCropY=.5;
    public double InstantCropY {get=>_draftInstantCropY;set{if(IsLightPrintLocked&&!_restoringLightPrintControls)return;SetProperty(ref _draftInstantCropY,value);}}
    private bool _draftInstantImageEffectsDisabled=false;
    public bool InstantImageEffectsDisabled {get=>_draftInstantImageEffectsDisabled;set{if(IsLightPrintLocked&&!_restoringLightPrintControls)return;SetProperty(ref _draftInstantImageEffectsDisabled,value);}}
    public string InstantDimensions {get;private set;}="";
    public bool IsInstantGenerating {get;private set;}
    public bool InstantEditingAllowed=>!IsLightPrintLocked&&ActiveTarget is not null&&!IsLoading&&!IsExporting&&!IsInstantGenerating;
    private double _draftInstantUneven=.15;
    public double InstantUneven {get=>_draftInstantUneven;set{if(IsLightPrintLocked&&!_restoringLightPrintControls)return;SetProperty(ref _draftInstantUneven,value);}}
    public double InstantUnspread {get;set;}=.12;
    public double InstantRoller {get;set;}=.10;
    public double InstantArtLeak {get;set;}
    private double _draftInstantEnvironmentCelsius=21;
    public double InstantEnvironmentCelsius {get=>_draftInstantEnvironmentCelsius;set{if(IsLightPrintLocked&&!_restoringLightPrintControls)return;SetProperty(ref _draftInstantEnvironmentCelsius,value);}}
    public IReadOnlyList<string> InstantPaperColors {get;}=["Warm","Neutral","Cool"];
    private string _draftInstantPaperColor="Warm";
    public string InstantPaperColor {get=>_draftInstantPaperColor;set{if(IsLightPrintLocked&&!_restoringLightPrintControls)return;SetProperty(ref _draftInstantPaperColor,value);}}
    private bool _draftInstantPaperFrame=true;
    public bool InstantPaperFrame {get=>_draftInstantPaperFrame;set{if(IsLightPrintLocked&&!_restoringLightPrintControls)return;SetProperty(ref _draftInstantPaperFrame,value);}}
    private double _draftInstantBorder=.075;
    public double InstantBorder {get=>_draftInstantBorder;set{if(IsLightPrintLocked&&!_restoringLightPrintControls)return;SetProperty(ref _draftInstantBorder,value);}}
    private double _draftInstantBottomBorder=.25;
    public double InstantBottomBorder {get=>_draftInstantBottomBorder;set{if(IsLightPrintLocked&&!_restoringLightPrintControls)return;SetProperty(ref _draftInstantBottomBorder,value);}}
    private double _draftInstantAmount=1;
    public double InstantAmount {get=>_draftInstantAmount;set{if(IsLightPrintLocked&&!_restoringLightPrintControls)return;SetProperty(ref _draftInstantAmount,value);}}
    public InstantDevelopmentFrame? InstantDevelopment {get;private set;}
    public BitmapSource? InstantFinalPreview {get;private set;}
    public string InstantDirection {get;private set;}="Color";
    public AsyncRelayCommand GenerateInstantFilmCommand {get;private set;}=null!;
    public AsyncRelayCommand RollInstantFilmCommand {get;private set;}=null!;
    public AsyncRelayCommand ToggleInstantDiceCommand {get;private set;}=null!;
    public AsyncRelayCommand DisableInstantFilmCommand {get;private set;}=null!;
    public RelayCommand SelectInstantCameraCommand {get;private set;}=null!;
    public void ExitInstantPresentationForLifecycle(){ClearFrameGallery();CancelLightPrintTask(false);_paperPreviewCancellation?.Cancel();CancelComparison();_secondSelectionCancellation?.Cancel();_instantCancellation?.Cancel();}
    private void InitializeInstantFilmCommands()
    {
        InitializeInstantV3Commands();
        SkipInstantDemoCommand=new(_=>_skipInstantDemo=true,_=>IsInstantGenerating);
        bool Ready(object? _)=>!_disposed&&!IsInstantGenerating&&!IsLoading&&!IsExporting&&ActiveTarget is not null;
        GenerateInstantFilmCommand=new(_=>GenerateInstantFilmAsync(false),Ready);
        RollInstantFilmCommand=new(_=>GenerateInstantFilmAsync(true),_=>Ready(null)&&(InstantDiceEnabled||InstantEngineVersion=="V3"));
        ToggleInstantDiceCommand=new(async _=>{InstantDiceEnabled=!InstantDiceEnabled;OnPropertyChanged(nameof(InstantDiceEnabled));await GenerateInstantFilmAsync(InstantDiceEnabled);},Ready);
        DisableInstantFilmCommand=new(async _=>
        {
            if(ActiveTarget is not {} target||target.RawDefinition is not {} d)return;
            if(d.InstantFilm?.AlgorithmVersion==3)
            {
                // Use the same render-before-commit transaction and history as a V3 edit.
                InstantImageEffectsDisabled=true;
                await GenerateInstantFilmAsync(false);
                return;
            }
            CaptureEditVersion(target);target.ActiveEditVersionId=Guid.NewGuid();target.RawDefinition=d with{InstantFilm=(d.InstantFilm??new()) with{AlgorithmVersion=d.InstantFilm?.AlgorithmVersion??2,ImageEffectsDisabled=true,PaperFrame=InstantPaperFrame,V3=d.InstantFilm?.V3 is{} v3?v3 with{Amount=0}:null}};
            await ActivateTargetAsync(target);CaptureEditVersion(target);
        },Ready);
        SelectInstantCameraCommand=new(p=>{if(IsLightPrintLocked)return;if(p is "SX-70" or "i-2"){InstantCamera=(string)p;OnPropertyChanged(nameof(InstantCamera));}},_=>!IsInstantGenerating);
    }
    private void RestoreInstantControls(InstantFilmDefinition? film)
    {
        _restoringLightPrintControls=true;try
        {
        RestoreV3(film);
        SrgbExposureEv=ActiveTarget?.RawDefinition?.SrgbExposure?.ExposureEv??0;
        InstantInputMode=ActiveTarget?.RawDefinition?.InstantInput?.Mode??"CurrentEdits";
        if(film is null)
        {
            SelectedInstantProfile=InstantFilmProfiles.Available[0];InstantTier="稳定出片";InstantDiceEnabled=false;InstantImageEffectsDisabled=false;
            InstantLayout="SquareV1";InstantFit="CropFill";InstantCropX=.5;InstantCropY=.5;InstantAmount=1;InstantUneven=.15;InstantUnspread=.12;InstantRoller=.10;InstantArtLeak=0;InstantEnvironmentCelsius=21;InstantPaperColor="Warm";InstantPaperFrame=true;InstantBorder=.075;InstantBottomBorder=.25;
            foreach(var name in new[]{nameof(SelectedInstantProfile),nameof(InstantTier),nameof(InstantDiceEnabled),nameof(InstantImageEffectsDisabled),nameof(InstantLayout),nameof(InstantFit),nameof(InstantCropX),nameof(InstantCropY),nameof(InstantAmount),nameof(InstantUneven),nameof(InstantEnvironmentCelsius),nameof(InstantPaperColor),nameof(InstantPaperFrame),nameof(InstantBorder),nameof(InstantBottomBorder)})OnPropertyChanged(name);return;
        }
        SelectedInstantProfile=InstantFilmProfiles.Available.Single(x=>x.Id==film.FilmId);InstantTier=film.Tier;InstantDiceEnabled=film.DiceEnabled;
        InstantUneven=film.Uneven;InstantUnspread=film.Unspread;InstantRoller=film.Roller;InstantArtLeak=film.ArtLightLeak;InstantEnvironmentCelsius=film.EnvironmentCelsius;
        InstantPaperColor=film.PaperColor;InstantPaperFrame=film.PaperFrame;InstantBorder=film.Border;InstantBottomBorder=film.BottomBorder;InstantAmount=film.Amount;
        InstantImageEffectsDisabled=film.ImageEffectsDisabled;InstantLayout=film.PaperGeometry is null?"LegacyFree":"SquareV1";
        InstantFit=film.PaperGeometry?.Fit??"CropFill";InstantCropX=film.PaperGeometry?.CropX??.5;InstantCropY=film.PaperGeometry?.CropY??.5;
        if(film.PaperGeometry is {} paper){InstantBorder=paper.Side;InstantBottomBorder=paper.Bottom;}
        foreach(var name in new[]{nameof(InstantImageEffectsDisabled),nameof(InstantLayout),nameof(InstantFit),nameof(InstantCropX),nameof(InstantCropY)})OnPropertyChanged(name);
        foreach(var name in new[]{nameof(SelectedInstantProfile),nameof(InstantTier),nameof(InstantDiceEnabled),nameof(InstantUneven),nameof(InstantUnspread),nameof(InstantRoller),nameof(InstantArtLeak),nameof(InstantEnvironmentCelsius),nameof(InstantPaperColor),nameof(InstantPaperFrame),nameof(InstantBorder),nameof(InstantBottomBorder),nameof(InstantAmount)})OnPropertyChanged(name);
        }finally{if(ActiveTarget?.RawDefinition?.LightPrint is{} light){_selectedLightPrintFrameId=light.FrameId;InstantCamera=light.Camera;OnPropertyChanged(nameof(LightPrintCamera));OnPropertyChanged(nameof(SelectedLightPrintFrameId));}_restoringLightPrintControls=false;NotifyLightPrint();}

    }
    public System.Windows.Rect InstantImageRegion {get;private set;}=new(0,0,1,1);
    private void InstantChanged()
    {
        if(_disposed)return;
        foreach(var name in new[]{nameof(IsInstantGenerating),nameof(InstantEditingAllowed),nameof(InstantDevelopment),nameof(InstantFinalPreview),nameof(InstantDirection),nameof(InstantImageRegion)})OnPropertyChanged(name);
        GenerateInstantFilmCommand.RaiseCanExecuteChanged();RollInstantFilmCommand.RaiseCanExecuteChanged();ToggleInstantDiceCommand.RaiseCanExecuteChanged();DisableInstantFilmCommand.RaiseCanExecuteChanged();SelectInstantCameraCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(PhotoGradeRasterToolsEnabled));OnPropertyChanged(nameof(PhotoGradeCanSyncStep));
        StopProcessingCommand.RaiseCanExecuteChanged();SkipInstantDemoCommand?.RaiseCanExecuteChanged();
        PhotoGradeUndoCommand.RaiseCanExecuteChanged();PhotoGradeRedoCommand.RaiseCanExecuteChanged();ResetPhotoV3DraftCommand.RaiseCanExecuteChanged();
        ExportSelectedCommand.RaiseCanExecuteChanged();ExportAllCommand.RaiseCanExecuteChanged();PreparePublishingCommand.RaiseCanExecuteChanged();
    }
    public async Task GenerateInstantFilmAsync(bool roll)
    {
        if(_disposed||IsInstantGenerating||IsLoading||IsExporting||IsLightPrintLocked&&!_developingLightPrint||ActiveTarget is not {} target)return;
        _paperPreviewCancellation?.Cancel();await StopComparisonAsync();
        if(_disposed||IsLoading||IsExporting||IsInstantGenerating||!ReferenceEquals(ActiveTarget,target))return;
        // Establish and freeze the source edit version before async reconstruction starts.
        if(InstantEngineVersion=="V3"&&(target.ActiveEditVersionId is null||target.RawDefinition is null)||target.RawDefinition is null&&RawMatchTiff16ProductPipeline.IsRaw(target.Path)){Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);}
        var version=target.ActiveEditVersionId;var revision=Volatile.Read(ref _activationRevision);var previous=target.RawDefinition;
        var previousDice=previous?.InstantFilm?.DiceEnabled??false;
        var oldRawMaster=target.RawMaster;var oldWidth=target.PixelWidth;var oldHeight=target.PixelHeight;var oldThumb=target.Thumbnail;var oldSourceThumb=target.SourceThumbnail;var oldEnabled=Editor.Enabled;var oldView=Editor.ViewMode;var oldTargetImage=TargetImage;var oldFailed=FailedTarget;
        var oldInput=target.RawInput;var oldMaster=target.LinearRawMaster;using var oldMasterLease=oldMaster is null?null:StudioGlobalResources.RetainBuffer(oldMaster.Image,(long)oldMaster.Image.Rgb32.Length*4);var oldLook=target.AppliedLookSnapshot;var oldStack=target.ColorAdjustmentStackSnapshot;var oldFilm=target.FilmSettingsSnapshot;
        var oldImage=Editor.MatchedImage;var oldSource=Editor.SourceImage;var oldMode=Editor.IndependentRawMode;var oldSize=Editor.SourcePixelSize;var oldStatus=target.Status;var oldError=target.Error;var oldHistory=Editor.CaptureSessionHistory();var oldEditorSource=Editor.CapturePreparedSource();Guid? committedId=null;
        void Rollback()
        {
            if(committedId is not {} id||target.ActiveEditVersionId!=id)return;
            target.ActiveEditVersionId=version;target.RawDefinition=previous;target.RawInput=oldInput;target.RawMaster=_disposed?null:oldRawMaster;target.LinearRawMaster=_disposed||!ReferenceEquals(ActiveTarget,target)?null:oldMaster;target.PixelWidth=oldWidth;target.PixelHeight=oldHeight;target.Thumbnail=oldThumb;target.SourceThumbnail=oldSourceThumb;target.AppliedLookSnapshot=oldLook;target.ColorAdjustmentStackSnapshot=oldStack;target.FilmSettingsSnapshot=oldFilm;
            var entry=target.EditVersions.FirstOrDefault(v=>v.Id==id);if(entry is not null)target.EditVersions.Remove(entry);
            if(!_disposed&&!IsLoading&&ReferenceEquals(target,ActiveTarget)&&oldImage is not null){Editor.ApplyTargetSnapshot(oldLook,oldFilm,oldStack,render:false,targetIdentity:version??target.Id);Editor.RestorePreparedSource(oldEditorSource,target.AssetId);Editor.PresentIndependentRaw(oldImage);Editor.SetIndependentRawSize(oldSize);Editor.IndependentRawMode=oldMode;Editor.Enabled=oldEnabled;Editor.ViewMode=oldView;TargetImage=oldTargetImage;FailedTarget=oldFailed;Editor.RestoreSessionHistory(oldHistory);target.Status=oldStatus;target.Error=oldError;}
        }
        _skipInstantDemo=false;LastInstantApplyTimings=null;LastProcessingFailure=null;OnPropertyChanged(nameof(LastProcessingFailure));
        _instantCancellation?.Dispose();_instantCancellation=new();var token=_instantCancellation.Token;
        Editor.StopProcessing();InvalidateProfessionalPreview();IsInstantGenerating=true;InstantDevelopment=new(InstantDevelopmentStage.Computing,0,false,false);InstantFinalPreview=null;InstantChanged();
        try
        {
            var profile=SelectedInstantProfile;
            Editor.CopyCurrentLookTo([target]);
            var definition=BuildInstantDefinition(target,previous,version,roll);var film=definition.InstantFilm!;
            var camera=InstantCameraAnimation;InstantDirection=profile.Direction;
            if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);
            // Reuse the exact frozen full source used by legacy editing; never a proxy or a second native
            // decode on first conversion. After success the new rendered master replaces the old cache.
            var decodeWatch=Stopwatch.StartNew();var reusable=ReusableMaster(target,definition);var cacheHit=reusable is not null;
            var master=reusable??(definition.InputConfiguration=="LegacyRawEditsSrgbFloatV1"?await RebuildLegacyRawInputAsync(target.Path,definition,target.RawMaster,token):await DecodeProfessionalInputAsync(target.Path,definition,token));
            if(!string.Equals(master.Contract.SourceSha256,target.SourceHash??target.ObservedSourceHash,StringComparison.OrdinalIgnoreCase))throw new IOException("生成期间源文件身份改变。");
            var decodeMs=decodeWatch.Elapsed.TotalMilliseconds;var pixelWatch=Stopwatch.StartNew();
            using var presentationMaster=StudioGlobalResources.RetainBuffer(master.Image,(long)master.Image.Rgb32.Length*sizeof(float));
            var prepared=await PrepareProfessionalPreviewAsync(target,master,definition,token);
            InstantFinalPreview=prepared.Image;
            var original=ProfessionalRawPipeline.OrientedSize(master);var layout=InstantPaperLayout.Create(original.Width,original.Height,film);
            InstantDimensions=$"原尺寸 {original.Width} × {original.Height} · 相纸 {layout.Width} × {layout.Height}";OnPropertyChanged(nameof(InstantDimensions));
            InstantImageRegion=new(layout.Left/(double)layout.Width,layout.Top/(double)layout.Height,layout.WindowWidth/(double)layout.Width,layout.WindowHeight/(double)layout.Height);
            var pixelMs=prepared.RenderMs;
            Kitao.InstantFilm.Research.CandidateProcessingEvidence.Enter("DevelopmentAnimation",token);
            var watch=Stopwatch.StartNew();
            while(InstantDevelopmentDemo&&!_skipInstantDemo)
            {
                token.ThrowIfCancellationRequested();
                if(_disposed||revision!=Volatile.Read(ref _activationRevision)||!ReferenceEquals(ActiveTarget,target)||target.ActiveEditVersionId!=version||target.RawDefinition!=previous)throw new OperationCanceledException(token);
                InstantDevelopment=InstantDevelopmentTimeline.Sample(watch.Elapsed.TotalSeconds,camera);InstantChanged();
                if(InstantDevelopment.Stage==InstantDevelopmentStage.Completed)break;
                await Task.Delay(16,token);
            }
            var demoMs=watch.Elapsed.TotalMilliseconds;
            token.ThrowIfCancellationRequested();
            if(_disposed||revision!=Volatile.Read(ref _activationRevision)||!ReferenceEquals(ActiveTarget,target)||target.ActiveEditVersionId!=version||target.RawDefinition!=previous)throw new OperationCanceledException(token);
            if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);
            token.ThrowIfCancellationRequested();
            var commitWatch=Stopwatch.StartNew();
            // Commit only the ready, frozen result. Failure/cancellation leaves the old version and preview intact.
            Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);target.ActiveEditVersionId=Guid.NewGuid();committedId=target.ActiveEditVersionId;
            target.RawDefinition=definition;target.RawInput=master.Contract;target.LinearRawMaster=master;target.RawMaster=null;
            target.AppliedLookSnapshot=null;target.ColorAdjustmentStackSnapshot=null;target.FilmSettingsSnapshot=null;
            CaptureEditVersion(target);target.EditVersions[^1]=target.EditVersions[^1] with{Name=_developingLightPrint?$"光片 · {PhotographerPaperName} · {DateTimeOffset.Now:MMdd-HHmmss}":profile.Name+(film.AlgorithmVersion==3?" · 创作 V3":" · 创作 v2"),OriginVersionId=version};
            EnsurePreparedCurrent(target,definition,committedId,revision,token);await PresentProfessionalPreviewAsync(target,prepared,revision,token);if(target.Status==ReferenceTargetStatus.Failed)throw new IOException(target.Error);StatusText="即时胶片显影完成 · 创作近似，非实测相纸标定";
            LastInstantApplyTimings=new(decodeMs,pixelMs,demoMs,commitWatch.Elapsed.TotalMilliseconds,cacheHit,prepared.CompareMs);OnPropertyChanged(nameof(LastInstantApplyTimings));
            if(film.AlgorithmVersion==3){if(!_instantV3Undo.TryGetValue(target.Id,out var undo))_instantV3Undo[target.Id]=undo=new();if(version is Guid prior)undo.Push(prior);if(_instantV3Redo.TryGetValue(target.Id,out var redo))redo.Clear();}
        }
        catch(OperationCanceledException error){LastProcessingFailure=StudioProcessingFailure.From(error,"InstantRebuild");OnPropertyChanged(nameof(LastProcessingFailure));Rollback();if(ReferenceEquals(ActiveTarget,target))RestoreInstantControls(previous?.InstantFilm);StatusText="本次显影已退出，保留上一有效结果";InstantDiceEnabled=previousDice;}
        catch(Exception error)when(error is IOException or InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {LastProcessingFailure=StudioProcessingFailure.From(error,"InstantRebuild");OnPropertyChanged(nameof(LastProcessingFailure));Rollback();if(ReferenceEquals(ActiveTarget,target))RestoreInstantControls(previous?.InstantFilm);StatusText="即时胶片未完成，保留上一有效结果："+error.Message;InstantDiceEnabled=previousDice;}
        finally{IsInstantGenerating=false;if(!_disposed)UpdateComparisonAvailability();InstantDevelopment=null;InstantFinalPreview=null;OnPropertyChanged(nameof(InstantDiceEnabled));InstantChanged();}
    }
    private ProfessionalRawDefinition BuildInstantDefinition(ReferenceTargetItem target,ProfessionalRawDefinition? previous,Guid? version,bool roll)
    {
        var profile=SelectedInstantProfile;
            var film=new InstantFilmDefinition(true,profile.Id,profile.ChemicalGeneration,AlgorithmVersion:2,Amount:InstantAmount,PaperFrame:InstantPaperFrame,Border:InstantBorder,BottomBorder:InstantBottomBorder,Uneven:InstantUneven,Unspread:InstantUnspread,Roller:InstantRoller,ArtLightLeak:InstantArtLeak,EnvironmentCelsius:InstantEnvironmentCelsius,Tier:InstantTier){PaperColor=InstantPaperColor,ImageEffectsDisabled=InstantImageEffectsDisabled,PaperGeometry=InstantLayout=="LegacyFree"?null:new InstantPaperGeometry(Fit:InstantFit,CropX:InstantCropX,CropY:InstantCropY,Side:InstantBorder,Top:InstantBorder,Bottom:InstantBottomBorder)};
            if(InstantDiceEnabled&&InstantEngineVersion!="V3")
            {
                var snapshot=previous?.InstantFilm?.Snapshot;
                if(roll)snapshot=InstantFilmRandom.Roll(film,BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8)));
                else if(snapshot is null||snapshot.ProfileId!=film.FilmId||snapshot.Tier!=film.Tier)throw new InvalidDataException("相纸或档位改变后请显式掷骰；不会在渲染中自动重新随机。");
                film=film with{DiceEnabled=true,Snapshot=snapshot};
            }
            film=InstantFilmPipeline.Freeze(film);
            if(InstantEngineVersion=="V3")
            {
                var v3=FreezeV3(film,roll);var identity=v3.Profile;
                film=film with{AlgorithmVersion=3,FilmId=identity.LegacyId,ChemicalGeneration=InstantFilmProfiles.Available.Single(p=>p.Id==identity.LegacyId).ChemicalGeneration,V3=v3};
            }
            else if(InstantEngineVersion!="V2")throw new InvalidDataException("Unknown instant algorithm version");

            // Legacy RAW CurrentEdits is an explicit, persisted PV6 bridge; old definitions stay untouched.

            if(InstantInputMode is not("Original" or "CurrentEdits"))throw new InvalidDataException("请选择明确的输入来源。");
            var basis=InstantInputMode=="Original"?(RawMatchTiff16ProductPipeline.IsRaw(target.Path)?new ProfessionalRawDefinition():InstantFilmJpegInput.Definition):previous??InstantFilmJpegInput.Definition;
            if(previous is null&&RawMatchTiff16ProductPipeline.IsRaw(target.Path)&&InstantInputMode=="CurrentEdits")
                basis=LegacyRawInstantInput.Create(new InstantInputRecipe(2,"CurrentEdits",version,target.AppliedLookSnapshot,target.ColorAdjustmentStackSnapshot?.DeepClone(),target.FilmSettingsSnapshot)
                {LegacyRaw=new(1,target.ColorAdjustmentStackSnapshot?.ProcessingVersion??1,target.SourceHash??throw new InvalidDataException("Missing source identity."),Engine:target.EngineSnapshot,ExecutionMode:target.ExecutionModeSnapshot)});
            if(basis.InputConfiguration=="RenderedSrgbFloatV1"&&(previous is null||InstantInputMode=="Original"))
                basis=basis with{InstantInput=new(1,InstantInputMode,version,InstantInputMode=="CurrentEdits"?target.AppliedLookSnapshot:null,InstantInputMode=="CurrentEdits"?target.ColorAdjustmentStackSnapshot?.DeepClone():null,InstantInputMode=="CurrentEdits"?target.FilmSettingsSnapshot:null)};
            if(previous?.InstantInput?.ColorDocument is{} priorDocument&&InstantInputMode=="Original")
                basis=InstantFilmJpegInput.Definition with{RenderedInputVersion=2,InstantInput=new InstantInputRecipe(3,"Original",previous.InstantInput.OriginVersionId){ColorDocument=priorDocument with{LocalEdits=null,Geometry=null}}};
            if(previous?.ColorState is not null)
            {
                var frozen=InstantInputMode=="Original"?previous with{LocalEdits=null,Geometry=null}:previous;
                if(frozen.InstantInput is not null)throw new NotSupportedException("Nested document input unsupported");
                basis=InstantFilmJpegInput.Definition with{RenderedInputVersion=2,InstantInput=new InstantInputRecipe(3,InstantInputMode,version){ColorDocument=frozen with{LocalEdits=frozen.LocalEdits?.Freeze()}}};
            }
            if(InstantEngineVersion=="V3"&&InstantInputMode=="Original"&&!RawMatchTiff16ProductPipeline.IsRaw(target.Path))basis=basis with{RenderedInputVersion=2};
            if(_developingLightPrint)film=ApplyLightPrintFrame(film);
            var definition=basis with{InstantFilm=film,SrgbExposure=null,LightPrint=null};
            if(_developingLightPrint)definition=definition with{LightPrint=new(1,SelectedLightPrintFrameId,InstantCamera,DateTimeOffset.UtcNow)};
            definition.Validate();return definition;

    }

}
