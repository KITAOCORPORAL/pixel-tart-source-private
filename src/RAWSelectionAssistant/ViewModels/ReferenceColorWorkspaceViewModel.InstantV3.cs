using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Utilities;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
 public IReadOnlyList<string> InstantEngineVersions {get;}=["V2","V3"];
 private string _instantEngineVersion="V2";
 public string InstantEngineVersion {get=>_instantEngineVersion;set{if(_instantEngineVersion==value)return;_instantEngineVersion=value;OnPropertyChanged(nameof(InstantEngineVersion));OnPropertyChanged(nameof(IsInstantV3Selected));OnPropertyChanged(nameof(IsInstantV2Selected));OnPropertyChanged(nameof(PhotoGradeOriginalInputAllowed));}}
 public bool IsInstantV3Selected=>InstantEngineVersion=="V3";
 public bool IsInstantV2Selected=>!IsInstantV3Selected;
 private double _instantV3Exposure;
 public double InstantV3Exposure {get=>_instantV3Exposure;set{if(EqualityComparer<double>.Default.Equals(_instantV3Exposure,value))return;_instantV3Exposure=value;OnPropertyChanged(nameof(InstantV3Exposure));}}
 private double _instantV3Toe=.025;
 public double InstantV3Toe {get=>_instantV3Toe;set{if(EqualityComparer<double>.Default.Equals(_instantV3Toe,value))return;_instantV3Toe=value;OnPropertyChanged(nameof(InstantV3Toe));}}
 private double _instantV3Shoulder=.3;
 public double InstantV3Shoulder {get=>_instantV3Shoulder;set{if(EqualityComparer<double>.Default.Equals(_instantV3Shoulder,value))return;_instantV3Shoulder=value;OnPropertyChanged(nameof(InstantV3Shoulder));}}
 private double _instantV3Density=1;
 public double InstantV3Density {get=>_instantV3Density;set{if(EqualityComparer<double>.Default.Equals(_instantV3Density,value))return;_instantV3Density=value;OnPropertyChanged(nameof(InstantV3Density));}}
 private double _instantV3Softness=.1;
 public double InstantV3Softness {get=>_instantV3Softness;set{if(EqualityComparer<double>.Default.Equals(_instantV3Softness,value))return;_instantV3Softness=value;OnPropertyChanged(nameof(InstantV3Softness));}}
 private double _instantV3Diffusion=.07;
 public double InstantV3Diffusion {get=>_instantV3Diffusion;set{if(EqualityComparer<double>.Default.Equals(_instantV3Diffusion,value))return;_instantV3Diffusion=value;OnPropertyChanged(nameof(InstantV3Diffusion));}}
 private double _instantV3Texture=.0015;
 public double InstantV3Texture {get=>_instantV3Texture;set{if(EqualityComparer<double>.Default.Equals(_instantV3Texture,value))return;_instantV3Texture=value;OnPropertyChanged(nameof(InstantV3Texture));}}
 private double _instantV3Aging;
 public double InstantV3Aging {get=>_instantV3Aging;set{if(EqualityComparer<double>.Default.Equals(_instantV3Aging,value))return;_instantV3Aging=value;OnPropertyChanged(nameof(InstantV3Aging));}}
 public IReadOnlyList<string> InstantV3Stocks {get;}=["Base","Womb01351.Stock0","Womb01351.Stock1","Womb01351.Stock2","Womb01351.Stock3","Womb01351.Stock4","Womb01351.Stock5"];
 private string _instantV3Stock="Base";
 public string InstantV3Stock {get=>_instantV3Stock;set{if(EqualityComparer<string>.Default.Equals(_instantV3Stock,value))return;_instantV3Stock=value;OnPropertyChanged(nameof(InstantV3Stock));}}
 private double _instantV3Fog;
 public double InstantV3Fog {get=>_instantV3Fog;set{if(EqualityComparer<double>.Default.Equals(_instantV3Fog,value))return;_instantV3Fog=value;OnPropertyChanged(nameof(InstantV3Fog));}}
 private double _instantV3ShadowCast;
 public double InstantV3ShadowCast {get=>_instantV3ShadowCast;set{if(EqualityComparer<double>.Default.Equals(_instantV3ShadowCast,value))return;_instantV3ShadowCast=value;OnPropertyChanged(nameof(InstantV3ShadowCast));}}
 private double _instantV3Warmth;
 public double InstantV3Warmth {get=>_instantV3Warmth;set{if(EqualityComparer<double>.Default.Equals(_instantV3Warmth,value))return;_instantV3Warmth=value;OnPropertyChanged(nameof(InstantV3Warmth));}}
 private double _instantV3FrameDrift;
 public double InstantV3FrameDrift {get=>_instantV3FrameDrift;set{if(EqualityComparer<double>.Default.Equals(_instantV3FrameDrift,value))return;_instantV3FrameDrift=value;OnPropertyChanged(nameof(InstantV3FrameDrift));}}
 private double _instantV3Fade;
 public double InstantV3Fade {get=>_instantV3Fade;set{if(EqualityComparer<double>.Default.Equals(_instantV3Fade,value))return;_instantV3Fade=value;OnPropertyChanged(nameof(InstantV3Fade));}}
 public IReadOnlyList<string> InstantV3Patterns {get;}=["Plain","Leather","Dots","Stripes","Foil"];
 private string _instantV3Pattern="Plain";
 public string InstantV3Pattern {get=>_instantV3Pattern;set{if(EqualityComparer<string>.Default.Equals(_instantV3Pattern,value))return;_instantV3Pattern=value;OnPropertyChanged(nameof(InstantV3Pattern));}}
 private bool _instantV3Circle;
 public bool InstantV3Circle {get=>_instantV3Circle;set{if(EqualityComparer<bool>.Default.Equals(_instantV3Circle,value))return;_instantV3Circle=value;OnPropertyChanged(nameof(InstantV3Circle));}}
 private double _instantV3Reflection;
 public double InstantV3Reflection {get=>_instantV3Reflection;set{if(EqualityComparer<double>.Default.Equals(_instantV3Reflection,value))return;_instantV3Reflection=value;OnPropertyChanged(nameof(InstantV3Reflection));}}
 private double _instantV3ReflectionAngle;
 public double InstantV3ReflectionAngle {get=>_instantV3ReflectionAngle;set{if(EqualityComparer<double>.Default.Equals(_instantV3ReflectionAngle,value))return;_instantV3ReflectionAngle=value;OnPropertyChanged(nameof(InstantV3ReflectionAngle));}}
 private double _instantV3ReflectionWidth=.15;
 public double InstantV3ReflectionWidth {get=>_instantV3ReflectionWidth;set{if(EqualityComparer<double>.Default.Equals(_instantV3ReflectionWidth,value))return;_instantV3ReflectionWidth=value;OnPropertyChanged(nameof(InstantV3ReflectionWidth));}}
 private double _instantV3Zoom=1;
 public double InstantV3Zoom {get=>_instantV3Zoom;set{if(EqualityComparer<double>.Default.Equals(_instantV3Zoom,value))return;_instantV3Zoom=value;OnPropertyChanged(nameof(InstantV3Zoom));}}
 private double _instantV3Emboss;
 public double InstantV3Emboss {get=>_instantV3Emboss;set{if(EqualityComparer<double>.Default.Equals(_instantV3Emboss,value))return;_instantV3Emboss=value;OnPropertyChanged(nameof(InstantV3Emboss));}}
 private double _instantV3EdgeLine;
 public double InstantV3EdgeLine {get=>_instantV3EdgeLine;set{if(EqualityComparer<double>.Default.Equals(_instantV3EdgeLine,value))return;_instantV3EdgeLine=value;OnPropertyChanged(nameof(InstantV3EdgeLine));}}
 public IReadOnlyList<string> InstantV3DefectKinds {get;}=["None","Unspread","Bands","Fog","Islands","Crescent","BlueFailure"];
 private string _instantV3DefectKind="None";
 public string InstantV3DefectKind {get=>_instantV3DefectKind;set{if(EqualityComparer<string>.Default.Equals(_instantV3DefectKind,value))return;_instantV3DefectKind=value;OnPropertyChanged(nameof(InstantV3DefectKind));}}
 private double _instantV3EmulsionDefects;
 private double _instantV3WindowHighlight;
 public double InstantV3WindowHighlight {get=>_instantV3WindowHighlight;set{SetProperty(ref _instantV3WindowHighlight,value);}}
 private double _instantV3WindowPressed;
 public double InstantV3WindowPressed {get=>_instantV3WindowPressed;set{SetProperty(ref _instantV3WindowPressed,value);}}
 public double InstantV3EmulsionDefects {get=>_instantV3EmulsionDefects;set{if(EqualityComparer<double>.Default.Equals(_instantV3EmulsionDefects,value))return;_instantV3EmulsionDefects=value;OnPropertyChanged(nameof(InstantV3EmulsionDefects));}}
 private double _instantV3DefectAmount;
 public double InstantV3DefectAmount {get=>_instantV3DefectAmount;set{if(EqualityComparer<double>.Default.Equals(_instantV3DefectAmount,value))return;_instantV3DefectAmount=value;OnPropertyChanged(nameof(InstantV3DefectAmount));}}
 private double _instantV3Crystal;
 public double InstantV3Crystal {get=>_instantV3Crystal;set{if(EqualityComparer<double>.Default.Equals(_instantV3Crystal,value))return;_instantV3Crystal=value;OnPropertyChanged(nameof(InstantV3Crystal));}}
 private double _instantV3Leak;
 public double InstantV3Leak {get=>_instantV3Leak;set{if(EqualityComparer<double>.Default.Equals(_instantV3Leak,value))return;_instantV3Leak=value;OnPropertyChanged(nameof(InstantV3Leak));}}
 private double _instantV3Vignette;
 public double InstantV3Vignette {get=>_instantV3Vignette;set{if(EqualityComparer<double>.Default.Equals(_instantV3Vignette,value))return;_instantV3Vignette=value;OnPropertyChanged(nameof(InstantV3Vignette));}}
 private double _instantV3DevelopmentEdge;
 public double InstantV3DevelopmentEdge {get=>_instantV3DevelopmentEdge;set{if(EqualityComparer<double>.Default.Equals(_instantV3DevelopmentEdge,value))return;_instantV3DevelopmentEdge=value;OnPropertyChanged(nameof(InstantV3DevelopmentEdge));}}
 private double _instantV3PaperTexture=.002;
 public double InstantV3PaperTexture {get=>_instantV3PaperTexture;set{if(EqualityComparer<double>.Default.Equals(_instantV3PaperTexture,value))return;_instantV3PaperTexture=value;OnPropertyChanged(nameof(InstantV3PaperTexture));}}
 private double _instantV3PaperScratches;
 public double InstantV3PaperScratches {get=>_instantV3PaperScratches;set{if(EqualityComparer<double>.Default.Equals(_instantV3PaperScratches,value))return;_instantV3PaperScratches=value;OnPropertyChanged(nameof(InstantV3PaperScratches));}}
 private double _instantV3ScanDust;
 public double InstantV3ScanDust {get=>_instantV3ScanDust;set{if(EqualityComparer<double>.Default.Equals(_instantV3ScanDust,value))return;_instantV3ScanDust=value;OnPropertyChanged(nameof(InstantV3ScanDust));}}
 private ulong _instantV3Seed=20261017;
 public ulong InstantV3Seed {get=>_instantV3Seed;set{if(EqualityComparer<ulong>.Default.Equals(_instantV3Seed,value))return;_instantV3Seed=value;OnPropertyChanged(nameof(InstantV3Seed));}}
 private double _instantV3DefectX=.5;
 public double InstantV3DefectX {get=>_instantV3DefectX;set{SetProperty(ref _instantV3DefectX,value);}}
 private double _instantV3DefectY=.5;
 public double InstantV3DefectY {get=>_instantV3DefectY;set{SetProperty(ref _instantV3DefectY,value);}}
 private double _instantV3DefectAngle=0;
 public double InstantV3DefectAngle {get=>_instantV3DefectAngle;set{SetProperty(ref _instantV3DefectAngle,value);}}
 private double _instantV3DefectScale=1;
 public double InstantV3DefectScale {get=>_instantV3DefectScale;set{SetProperty(ref _instantV3DefectScale,value);}}
 private CancellationTokenSource? _secondSelectionCancellation;
 public CandidateMultipleInput? InstantSecondInput {get;private set;}
 public string InstantSecondSourceName=>InstantSecondInput is null?"—":System.IO.Path.GetFileName(InstantSecondInput.Path);
 private double _instantSecondWeight=.5;
 public double InstantSecondWeight {get=>_instantSecondWeight;set{SetProperty(ref _instantSecondWeight,value);}}
 private double _instantSecondCropX=.5;
 public double InstantSecondCropX {get=>_instantSecondCropX;set{SetProperty(ref _instantSecondCropX,value);}}
 private double _instantSecondCropY=.5;
 public double InstantSecondCropY {get=>_instantSecondCropY;set{SetProperty(ref _instantSecondCropY,value);}}
 private double _instantSecondZoom=1;
 public double InstantSecondZoom {get=>_instantSecondZoom;set{SetProperty(ref _instantSecondZoom,value);}}
 public IReadOnlyList<string> InstantSecondModes {get;}=["NormalizedMix","ExposureSum"];
 private string _instantSecondMode="NormalizedMix";
 public string InstantSecondMode {get=>_instantSecondMode;set{SetProperty(ref _instantSecondMode,value);}}
 public AsyncRelayCommand SelectSecondExposureCommand {get;private set;}=null!;
 public RelayCommand ClearSecondExposureCommand {get;private set;}=null!;
 public AsyncRelayCommand UndoInstantV3Command {get;private set;}=null!;
 public AsyncRelayCommand RedoInstantV3Command {get;private set;}=null!;
 private readonly Dictionary<Guid,Stack<Guid>> _instantV3Undo=[];
 private readonly Dictionary<Guid,Stack<Guid>> _instantV3Redo=[];
 private void InitializeInstantV3Commands()
 {
  SelectSecondExposureCommand=new(async _=>
  {
   if(ActiveTarget is not{} target)return;var revision=Volatile.Read(ref _activationRevision);
   var selected=_dialogs.ChooseFiles("Select second photo","Photo|*.png;*.tif;*.tiff;*.jpg;*.jpeg",false).FirstOrDefault();if(selected is null)return;
   _secondSelectionCancellation?.Cancel();_secondSelectionCancellation?.Dispose();_secondSelectionCancellation=new();var token=_secondSelectionCancellation.Token;
   try{var input=await Task.Run(()=>SecondExposureDecoder.Describe(selected,token),token);if(!_disposed&&ReferenceEquals(ActiveTarget,target)&&revision==Volatile.Read(ref _activationRevision)){InstantSecondInput=input;OnPropertyChanged(nameof(InstantSecondSourceName));}}
   catch(OperationCanceledException){}
   catch(Exception e)when(e is IOException or NotSupportedException or InvalidDataException or System.Runtime.InteropServices.COMException){StatusText="Second exposure: "+e.Message;}
  },_=>InstantEditingAllowed);
  ClearSecondExposureCommand=new(_=>{InstantSecondInput=null;OnPropertyChanged(nameof(InstantSecondSourceName));});
  UndoInstantV3Command=new(async _=>await MoveInstantVersion(_instantV3Undo,_instantV3Redo));
  RedoInstantV3Command=new(async _=>await MoveInstantVersion(_instantV3Redo,_instantV3Undo));
  InitializePhotoGradeHistoryCommands();
 }
 private async Task MoveInstantVersion(Dictionary<Guid,Stack<Guid>> from,Dictionary<Guid,Stack<Guid>> to)
 {
  if(ActiveTarget is not{} target||IsInstantGenerating||IsLoading||IsExporting||!from.TryGetValue(target.Id,out var stack)||stack.Count==0)return;
  if(target.ActiveEditVersionId is not Guid active)return;
  while(stack.Count>0&&!target.EditVersions.Any(v=>v.Id==stack.Peek()&&!v.Archived))stack.Pop();
  if(stack.Count==0)return;var next=stack.Peek();
  var definition=target.RawDefinition;var input=target.RawInput;var master=target.LinearRawMaster;var rawMaster=target.RawMaster;
  var look=target.AppliedLookSnapshot;var adjustments=target.ColorAdjustmentStackSnapshot;var film=target.FilmSettingsSnapshot;
  var engine=target.EngineSnapshot;var mode=target.ExecutionModeSnapshot;var status=target.Status;var error=target.Error;
  var image=Editor.MatchedImage;var size=Editor.SourcePixelSize;
  await SwitchEditVersionAsync(next);
  if(target.Status==ReferenceTargetStatus.Failed)
  {
   // A missing/changed second source must not consume undo/redo or replace the valid view.
   var diagnostic=target.Error;target.ActiveEditVersionId=active;target.RawDefinition=definition;target.RawInput=input;
   target.LinearRawMaster=master;target.RawMaster=rawMaster;target.AppliedLookSnapshot=look;target.ColorAdjustmentStackSnapshot=adjustments;
   target.FilmSettingsSnapshot=film;target.EngineSnapshot=engine;target.ExecutionModeSnapshot=mode;target.Status=status;target.Error=error;
   if(ReferenceEquals(ActiveTarget,target))
   {
    RestoreInstantControls(definition?.InstantFilm);
    if(image is not null){Editor.PresentIndependentRaw(image);Editor.SetIndependentRawSize(size);Editor.IndependentRawMode=definition is not null;}
   }
   StatusText="History restore failed; previous valid result retained: "+diagnostic;return;
  }
  if(target.ActiveEditVersionId==next){stack.Pop();if(!to.TryGetValue(target.Id,out var other))to[target.Id]=other=new();other.Push(active);}
 }
 private CandidateRecipe FreezeV3(InstantFilmDefinition film,bool roll)
 {
  var profile=film.FilmId switch{"itype-bw" or "600-bw" or "sx70-bw"=>"modern-bw-v3","sx70-color"=>"sx70-v3","reclaimed-blue600"=>"reclaimed-blue-v3","blue-duochrome600"=>"blue-duochrome-v3",_=>"modern-itype-v3"};
  if(roll)InstantV3Seed=BitConverter.ToUInt64(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8));
  var recipe=CandidateRecipe.Create(profile,InstantV3Seed);
  return (recipe with{MultipleInput=InstantSecondInput is{} second?second with{Weight=InstantSecondWeight,Mode=InstantSecondMode,CropX=InstantSecondCropX,CropY=InstantSecondCropY,Zoom=InstantSecondZoom}:null,Amount=InstantImageEffectsDisabled?0:InstantAmount,
   Response=recipe.Response with{ExposureEv=InstantV3Exposure,Toe=InstantV3Toe,Shoulder=InstantV3Shoulder,ApparentDensity=InstantV3Density},
   Optics=recipe.Optics with{DetailMix=InstantV3Softness,HighlightDiffusion=InstantV3Diffusion},
   Emulsion=recipe.Emulsion with{Microstructure=InstantV3Texture,DevelopmentUnevenness=InstantUneven*.026,DevelopmentDefects=InstantV3EmulsionDefects,ExpiredAging=InstantV3Aging},
   Surface=recipe.Surface with{Frame=InstantPaperFrame,BaseColor=InstantPaperColor,PaperTexture=InstantV3PaperTexture,PaperScratches=InstantV3PaperScratches,ScanDust=InstantV3ScanDust},Geometry=film.PaperGeometry??new(),
   Creative=new(Version:2,Stock:InstantV3Stock,ShadowCast:InstantV3ShadowCast,HighlightWarmth:InstantV3Warmth,Fog:InstantV3Fog,TemperatureAppearance:(InstantEnvironmentCelsius-21)/21,FrameDrift:InstantV3FrameDrift,Fade:InstantV3Fade,LensVignette:InstantV3Vignette,DevelopmentEdge:InstantV3DevelopmentEdge),
   Paper=new(Version:3,Pattern:InstantV3Pattern,Window:InstantV3Circle?"Circle":"Rectangle",Emboss:InstantV3Emboss,EdgeLine:InstantV3EdgeLine,Reflection:InstantV3Reflection,ReflectionAngle:InstantV3ReflectionAngle,ReflectionWidth:InstantV3ReflectionWidth,Zoom:InstantV3Zoom,WindowHighlight:InstantV3WindowHighlight,WindowPressed:InstantV3WindowPressed),
   Defects=new(Version:2,Kind:InstantV3DefectKind,Amount:InstantV3DefectAmount,Crystal:InstantV3Crystal,Leak:InstantV3Leak,Geometry:new(InstantV3DefectX,InstantV3DefectY,InstantV3DefectAngle,InstantV3DefectScale))}).Freeze();
 }
 private void ResetV3PhotoControls()
 {
  // Photo state, not a mutable cross-photo user default. No random seed is drawn here.
  InstantV3DefectX=.5;InstantV3DefectY=.5;InstantV3DefectAngle=0;InstantV3DefectScale=1;
  InstantSecondInput=null;InstantSecondWeight=.5;InstantSecondCropX=.5;InstantSecondCropY=.5;InstantSecondZoom=1;InstantSecondMode="NormalizedMix";OnPropertyChanged(nameof(InstantSecondSourceName));
  InstantV3Exposure=0;InstantV3Toe=.025;InstantV3Shoulder=.3;InstantV3Density=1;InstantV3Softness=.1;InstantV3Diffusion=.07;
  InstantV3Texture=.0015;InstantV3Aging=0;InstantV3Stock="Base";InstantV3Fog=0;InstantV3ShadowCast=0;InstantV3Warmth=0;InstantV3FrameDrift=0;InstantV3Fade=0;
  InstantV3Pattern="Plain";InstantV3Circle=false;InstantV3Reflection=0;InstantV3ReflectionAngle=0;InstantV3ReflectionWidth=.15;InstantV3Zoom=1;
  InstantV3WindowHighlight=0;InstantV3WindowPressed=0;InstantV3Emboss=0;InstantV3EdgeLine=0;InstantV3DefectKind="None";InstantV3DefectAmount=0;InstantV3EmulsionDefects=0;InstantV3Crystal=0;InstantV3Leak=0;
  InstantV3Vignette=0;InstantV3DevelopmentEdge=0;InstantV3PaperTexture=.002;InstantV3PaperScratches=0;InstantV3ScanDust=0;InstantV3Seed=20261017;
 }
 private void RestoreV3(InstantFilmDefinition? film)
 {
  ResetV3PhotoControls();InstantEngineVersion=film?.AlgorithmVersion==3?"V3":"V2";
  if(film?.V3 is{} r){InstantSecondInput=r.MultipleInput;if(r.MultipleInput is{} second){InstantSecondWeight=second.Weight;InstantSecondMode=second.Mode;InstantSecondCropX=second.CropX;InstantSecondCropY=second.CropY;InstantSecondZoom=second.Zoom;}OnPropertyChanged(nameof(InstantSecondSourceName));InstantV3Exposure=r.Response.ExposureEv;InstantV3Toe=r.Response.Toe;InstantV3Shoulder=r.Response.Shoulder;InstantV3Density=r.Response.ApparentDensity;InstantV3Softness=r.Optics.DetailMix;InstantV3Diffusion=r.Optics.HighlightDiffusion;InstantV3EmulsionDefects=r.Emulsion.DevelopmentDefects;InstantV3Texture=r.Emulsion.Microstructure;InstantV3Aging=r.Emulsion.ExpiredAging;InstantV3Seed=r.Random.RootSeed;
   InstantV3Stock=r.Creative?.Stock??"Base";InstantV3Fog=r.Creative?.Fog??0;InstantV3ShadowCast=r.Creative?.ShadowCast??0;InstantV3Warmth=r.Creative?.HighlightWarmth??0;InstantV3FrameDrift=r.Creative?.FrameDrift??0;InstantV3Fade=r.Creative?.Fade??0;InstantV3Vignette=r.Creative?.LensVignette??0;InstantV3DevelopmentEdge=r.Creative?.DevelopmentEdge??0;
   InstantV3WindowHighlight=r.Paper?.WindowHighlight??0;InstantV3WindowPressed=r.Paper?.WindowPressed??0;InstantV3Pattern=r.Paper?.Pattern??"Plain";InstantV3Circle=r.Paper?.Window=="Circle";InstantV3Reflection=r.Paper?.Reflection??0;InstantV3ReflectionAngle=r.Paper?.ReflectionAngle??0;InstantV3ReflectionWidth=r.Paper?.ReflectionWidth??.15;InstantV3Zoom=r.Paper?.Zoom??1;InstantV3Emboss=r.Paper?.Emboss??0;InstantV3EdgeLine=r.Paper?.EdgeLine??0;
   InstantV3DefectX=r.Defects?.Geometry?.CenterX??.5;InstantV3DefectY=r.Defects?.Geometry?.CenterY??.5;InstantV3DefectAngle=r.Defects?.Geometry?.AngleDegrees??0;InstantV3DefectScale=r.Defects?.Geometry?.Scale??1;InstantV3DefectKind=r.Defects?.Kind??"None";InstantV3DefectAmount=r.Defects?.Amount??0;InstantV3Crystal=r.Defects?.Crystal??0;InstantV3Leak=r.Defects?.Leak??0;InstantV3PaperTexture=r.Surface.PaperTexture;InstantV3PaperScratches=r.Surface.PaperScratches;InstantV3ScanDust=r.Surface.ScanDust;}
  foreach(var property in GetType().GetProperties().Where(p=>p.Name.StartsWith("InstantV3",StringComparison.Ordinal)))OnPropertyChanged(property.Name);
  foreach(var name in new[]{nameof(InstantEngineVersion),nameof(InstantV3Exposure),nameof(InstantV3Toe),nameof(InstantV3Shoulder),nameof(InstantV3Density),nameof(InstantV3Softness),nameof(InstantV3Diffusion),nameof(InstantV3Texture),nameof(InstantV3Aging),nameof(InstantV3Seed)})OnPropertyChanged(name);
 }
}
