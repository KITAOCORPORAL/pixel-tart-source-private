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
    public IReadOnlyList<InstantFilmProfile> InstantProfiles=>InstantFilmProfiles.Available;
    public IReadOnlyList<string> InstantTiers {get;}=["稳定出片","轻微偶然","实验胶片"];
    public InstantFilmProfile SelectedInstantProfile {get;set;}=InstantFilmProfiles.Available[0];
    public string InstantTier {get;set;}="稳定出片";
    public bool InstantDiceEnabled {get;private set;}
    public bool InstantCameraAnimation {get;set;}=true;
    public string InstantCamera {get;private set;}="SX-70/680";
    public bool IsInstantGenerating {get;private set;}
    public bool InstantEditingAllowed=>!IsInstantGenerating;
    public double InstantUneven {get;set;}=.15;
    public double InstantUnspread {get;set;}=.12;
    public double InstantRoller {get;set;}=.10;
    public double InstantArtLeak {get;set;}
    public double InstantEnvironmentCelsius {get;set;}=21;
    public IReadOnlyList<string> InstantPaperColors {get;}=["Warm","Neutral","Cool"];
    public string InstantPaperColor {get;set;}="Warm";
    public bool InstantPaperFrame {get;set;}=true;
    public double InstantBorder {get;set;}=.04;
    public double InstantBottomBorder {get;set;}=.16;
    public double InstantAmount {get;set;}=1;
    public InstantDevelopmentFrame? InstantDevelopment {get;private set;}
    public BitmapSource? InstantFinalPreview {get;private set;}
    public string InstantDirection {get;private set;}="Color";
    public AsyncRelayCommand GenerateInstantFilmCommand {get;private set;}=null!;
    public AsyncRelayCommand RollInstantFilmCommand {get;private set;}=null!;
    public AsyncRelayCommand ToggleInstantDiceCommand {get;private set;}=null!;
    public AsyncRelayCommand DisableInstantFilmCommand {get;private set;}=null!;
    public RelayCommand SelectInstantCameraCommand {get;private set;}=null!;
    public void ExitInstantPresentationForLifecycle()=>_instantCancellation?.Cancel();
    private void InitializeInstantFilmCommands()
    {
        bool Ready(object? _)=>!_disposed&&!IsInstantGenerating&&!IsLoading&&!IsExporting&&ActiveTarget is not null;
        GenerateInstantFilmCommand=new(_=>GenerateInstantFilmAsync(false),Ready);
        RollInstantFilmCommand=new(_=>GenerateInstantFilmAsync(true),_=>Ready(null)&&InstantDiceEnabled);
        ToggleInstantDiceCommand=new(async _=>{InstantDiceEnabled=!InstantDiceEnabled;OnPropertyChanged(nameof(InstantDiceEnabled));await GenerateInstantFilmAsync(InstantDiceEnabled);},Ready);
        DisableInstantFilmCommand=new(async _=>
        {
            if(ActiveTarget is not {} target||target.RawDefinition is not {} d)return;
            CaptureEditVersion(target);target.ActiveEditVersionId=Guid.NewGuid();target.RawDefinition=d with{InstantFilm=null};
            await ActivateTargetAsync(target);CaptureEditVersion(target);
        },Ready);
        SelectInstantCameraCommand=new(p=>{if(p is "SX-70/680" or "i-2"){InstantCamera=(string)p;OnPropertyChanged(nameof(InstantCamera));}},_=>!IsInstantGenerating);
    }
    private void RestoreInstantControls(InstantFilmDefinition? film)
    {
        if(film is null){InstantDiceEnabled=false;OnPropertyChanged(nameof(InstantDiceEnabled));return;}
        SelectedInstantProfile=InstantFilmProfiles.Available.Single(x=>x.Id==film.FilmId);InstantTier=film.Tier;InstantDiceEnabled=film.DiceEnabled;
        InstantUneven=film.Uneven;InstantUnspread=film.Unspread;InstantRoller=film.Roller;InstantArtLeak=film.ArtLightLeak;InstantEnvironmentCelsius=film.EnvironmentCelsius;
        InstantPaperColor=film.PaperColor;InstantPaperFrame=film.PaperFrame;InstantBorder=film.Border;InstantBottomBorder=film.BottomBorder;InstantAmount=film.Amount;
        foreach(var name in new[]{nameof(SelectedInstantProfile),nameof(InstantTier),nameof(InstantDiceEnabled),nameof(InstantUneven),nameof(InstantUnspread),nameof(InstantRoller),nameof(InstantArtLeak),nameof(InstantEnvironmentCelsius),nameof(InstantPaperColor),nameof(InstantPaperFrame),nameof(InstantBorder),nameof(InstantBottomBorder),nameof(InstantAmount)})OnPropertyChanged(name);
    }
    public System.Windows.Rect InstantImageRegion {get;private set;}=new(0,0,1,1);
    private void InstantChanged()
    {
        if(_disposed)return;
        foreach(var name in new[]{nameof(IsInstantGenerating),nameof(InstantEditingAllowed),nameof(InstantDevelopment),nameof(InstantFinalPreview),nameof(InstantDirection),nameof(InstantImageRegion)})OnPropertyChanged(name);
        GenerateInstantFilmCommand.RaiseCanExecuteChanged();RollInstantFilmCommand.RaiseCanExecuteChanged();ToggleInstantDiceCommand.RaiseCanExecuteChanged();DisableInstantFilmCommand.RaiseCanExecuteChanged();SelectInstantCameraCommand.RaiseCanExecuteChanged();
        ExportSelectedCommand.RaiseCanExecuteChanged();ExportAllCommand.RaiseCanExecuteChanged();PreparePublishingCommand.RaiseCanExecuteChanged();
    }
    public async Task GenerateInstantFilmAsync(bool roll)
    {
        if(_disposed||IsInstantGenerating||IsLoading||IsExporting||ActiveTarget is not {} target)return;
        var version=target.ActiveEditVersionId;var revision=Volatile.Read(ref _activationRevision);var previous=target.RawDefinition;
        var previousDice=previous?.InstantFilm?.DiceEnabled??false;
        var oldInput=target.RawInput;var oldMaster=target.LinearRawMaster;var oldLook=target.AppliedLookSnapshot;var oldStack=target.ColorAdjustmentStackSnapshot;var oldFilm=target.FilmSettingsSnapshot;
        var oldImage=Editor.MatchedImage;var oldSize=Editor.SourcePixelSize;Guid? committedId=null;
        void Rollback()
        {
            if(committedId is not {} id||target.ActiveEditVersionId!=id)return;
            target.ActiveEditVersionId=version;target.RawDefinition=previous;target.RawInput=oldInput;target.LinearRawMaster=oldMaster;target.AppliedLookSnapshot=oldLook;target.ColorAdjustmentStackSnapshot=oldStack;target.FilmSettingsSnapshot=oldFilm;
            var entry=target.EditVersions.FirstOrDefault(v=>v.Id==id);if(entry is not null)target.EditVersions.Remove(entry);
            if(!_disposed&&ReferenceEquals(target,ActiveTarget)&&oldImage is not null){Editor.PresentIndependentRaw(oldImage);Editor.SetIndependentRawSize(oldSize);Editor.IndependentRawMode=previous is not null;}
        }
        _instantCancellation?.Dispose();_instantCancellation=new();var token=_instantCancellation.Token;
        Editor.StopProcessing();InvalidateProfessionalPreview();IsInstantGenerating=true;InstantDevelopment=new(InstantDevelopmentStage.Computing,0,false,false);InstantFinalPreview=null;InstantChanged();
        try
        {
            var profile=SelectedInstantProfile;
            var film=new InstantFilmDefinition(true,profile.Id,profile.ChemicalGeneration,Amount:InstantAmount,PaperFrame:InstantPaperFrame,Border:InstantBorder,BottomBorder:InstantBottomBorder,Uneven:InstantUneven,Unspread:InstantUnspread,Roller:InstantRoller,ArtLightLeak:InstantArtLeak,EnvironmentCelsius:InstantEnvironmentCelsius,Tier:InstantTier){PaperColor=InstantPaperColor};
            if(InstantDiceEnabled)
            {
                var snapshot=previous?.InstantFilm?.Snapshot;
                if(roll)snapshot=InstantFilmRandom.Roll(film,BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8)));
                else if(snapshot is null||snapshot.ProfileId!=film.FilmId||snapshot.Tier!=film.Tier)throw new InvalidDataException("相纸或档位改变后请显式掷骰；不会在渲染中自动重新随机。");
                film=film with{DiceEnabled=true,Snapshot=snapshot};
            }
            film=InstantFilmPipeline.Freeze(film);
            var definition=(previous??(RawMatchTiff16ProductPipeline.IsRaw(target.Path)?new ProfessionalRawDefinition():InstantFilmJpegInput.Definition)) with{InstantFilm=film};definition.Validate();
            var camera=InstantCameraAnimation;InstantDirection=profile.Direction;
            if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);
            var master=target.LinearRawMaster is {} current&&current.Contract.DecodeKey==definition.DecodeKey?current:await DecodeProfessionalInputAsync(target.Path,definition,token);
            if(!string.Equals(master.Contract.SourceSha256,target.SourceHash??target.ObservedSourceHash,StringComparison.OrdinalIgnoreCase))throw new IOException("生成期间源文件身份改变。");
            var preview=await RenderProfessionalPreviewAsync(master,definition,1600,token);
            InstantFinalPreview=RawDisplayBitmapAdapter.ToBitmap(preview.ToVisualRgb24());
            var original=ProfessionalRawPipeline.OrientedSize(master);var layout=InstantFilmPipeline.Layout(original.Width,original.Height,film);
            InstantImageRegion=new(layout.Margin/(double)layout.Width,layout.Margin/(double)layout.Height,original.Width/(double)layout.Width,original.Height/(double)layout.Height);
            var watch=Stopwatch.StartNew();
            do
            {
                token.ThrowIfCancellationRequested();
                if(_disposed||revision!=Volatile.Read(ref _activationRevision)||!ReferenceEquals(ActiveTarget,target)||target.ActiveEditVersionId!=version||target.RawDefinition!=previous)throw new OperationCanceledException(token);
                InstantDevelopment=InstantDevelopmentTimeline.Sample(watch.Elapsed.TotalSeconds,camera);InstantChanged();
                if(InstantDevelopment.Stage==InstantDevelopmentStage.Completed)break;
                await Task.Delay(16,token);
            }while(true);
            if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);
            token.ThrowIfCancellationRequested();
            // Commit only the ready, frozen result. Failure/cancellation leaves the old version and preview intact.
            Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);target.ActiveEditVersionId=Guid.NewGuid();committedId=target.ActiveEditVersionId;
            target.RawDefinition=definition;target.RawInput=master.Contract;target.LinearRawMaster=master;target.RawMaster=null;
            target.AppliedLookSnapshot=null;target.ColorAdjustmentStackSnapshot=null;target.FilmSettingsSnapshot=null;
            CaptureEditVersion(target);target.EditVersions[^1]=target.EditVersions[^1] with{Name=profile.Name+" · 创作 v1",OriginVersionId=version};
            IsInstantGenerating=false;await ActivateTargetAsync(target);if(target.Status==ReferenceTargetStatus.Failed)throw new IOException(target.Error);StatusText="即时胶片显影完成 · 创作近似，非实测相纸标定";
        }
        catch(OperationCanceledException){Rollback();StatusText="本次显影已退出，保留上一有效结果";InstantDiceEnabled=previousDice;}
        catch(Exception error)when(error is IOException or InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {Rollback();StatusText="即时胶片未完成，保留上一有效结果："+error.Message;InstantDiceEnabled=previousDice;}
        finally{IsInstantGenerating=false;InstantDevelopment=null;InstantFinalPreview=null;OnPropertyChanged(nameof(InstantDiceEnabled));InstantChanged();}
    }
}
