using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Utilities;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
 public IReadOnlyList<string> InstantEngineVersions {get;}=["V2","V3"];
 public string InstantEngineVersion {get;set;}="V2";
 public double InstantV3Exposure {get;set;}
 public double InstantV3Toe {get;set;}=.025;
 public double InstantV3Shoulder {get;set;}=.3;
 public double InstantV3Density {get;set;}=1;
 public double InstantV3Softness {get;set;}=.1;
 public double InstantV3Diffusion {get;set;}=.07;
 public double InstantV3Texture {get;set;}=.0015;
 public double InstantV3Aging {get;set;}
 public IReadOnlyList<string> InstantV3Stocks {get;}=["Base","Womb01351.Stock0","Womb01351.Stock1","Womb01351.Stock2","Womb01351.Stock3","Womb01351.Stock4","Womb01351.Stock5"];
 public string InstantV3Stock {get;set;}="Base";
 public double InstantV3Fog {get;set;}
 public double InstantV3ShadowCast {get;set;}
 public double InstantV3Warmth {get;set;}
 public double InstantV3FrameDrift {get;set;}
 public double InstantV3Fade {get;set;}
 public IReadOnlyList<string> InstantV3Patterns {get;}=["Plain","Leather","Dots","Stripes","Foil"];
 public string InstantV3Pattern {get;set;}="Plain";
 public bool InstantV3Circle {get;set;}
 public double InstantV3Reflection {get;set;}
 public double InstantV3ReflectionAngle {get;set;}
 public double InstantV3ReflectionWidth {get;set;}=.15;
 public double InstantV3Zoom {get;set;}=1;
 public double InstantV3Emboss {get;set;}
 public double InstantV3EdgeLine {get;set;}
 public IReadOnlyList<string> InstantV3DefectKinds {get;}=["None","Unspread","Bands","Fog","Islands","Crescent","BlueFailure"];
 public string InstantV3DefectKind {get;set;}="None";
 public double InstantV3DefectAmount {get;set;}
 public double InstantV3Crystal {get;set;}
 public double InstantV3Leak {get;set;}
 public double InstantV3Vignette {get;set;}
 public double InstantV3DevelopmentEdge {get;set;}
 public double InstantV3PaperTexture {get;set;}=.002;
 public double InstantV3PaperScratches {get;set;}
 public double InstantV3ScanDust {get;set;}
 public ulong InstantV3Seed {get;set;}=20261017;
 public AsyncRelayCommand UndoInstantV3Command {get;private set;}=null!;
 public AsyncRelayCommand RedoInstantV3Command {get;private set;}=null!;
 private readonly Dictionary<Guid,Stack<Guid>> _instantV3Undo=[];
 private readonly Dictionary<Guid,Stack<Guid>> _instantV3Redo=[];
 private void InitializeInstantV3Commands()
 {
  UndoInstantV3Command=new(async _=>await MoveInstantVersion(_instantV3Undo,_instantV3Redo));
  RedoInstantV3Command=new(async _=>await MoveInstantVersion(_instantV3Redo,_instantV3Undo));
 }
 private async Task MoveInstantVersion(Dictionary<Guid,Stack<Guid>> from,Dictionary<Guid,Stack<Guid>> to)
 {
  if(ActiveTarget is not{} target||IsInstantGenerating||IsLoading||IsExporting||!from.TryGetValue(target.Id,out var stack)||stack.Count==0)return;
  if(!to.TryGetValue(target.Id,out var other))to[target.Id]=other=new();if(target.ActiveEditVersionId is not Guid active)return;other.Push(active);await SwitchEditVersionAsync(stack.Pop());
 }
 private CandidateRecipe FreezeV3(InstantFilmDefinition film,bool roll)
 {
  var profile=film.FilmId switch{"itype-bw" or "600-bw" or "sx70-bw"=>"modern-bw-v3","sx70-color"=>"sx70-v3","reclaimed-blue600"=>"reclaimed-blue-v3","blue-duochrome600"=>"blue-duochrome-v3",_=>"modern-itype-v3"};
  if(roll)InstantV3Seed=BitConverter.ToUInt64(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8));
  var recipe=CandidateRecipe.Create(profile,InstantV3Seed);
  return (recipe with{Amount=InstantImageEffectsDisabled?0:InstantAmount,
   Response=recipe.Response with{ExposureEv=InstantV3Exposure,Toe=InstantV3Toe,Shoulder=InstantV3Shoulder,ApparentDensity=InstantV3Density},
   Optics=recipe.Optics with{DetailMix=InstantV3Softness,HighlightDiffusion=InstantV3Diffusion},
   Emulsion=recipe.Emulsion with{Microstructure=InstantV3Texture,DevelopmentUnevenness=InstantUneven*.026,DevelopmentDefects=InstantUnspread,ExpiredAging=InstantV3Aging},
   Surface=recipe.Surface with{Frame=InstantPaperFrame,BaseColor=InstantPaperColor,PaperTexture=InstantV3PaperTexture,PaperScratches=InstantV3PaperScratches,ScanDust=InstantV3ScanDust},Geometry=film.PaperGeometry??new(),
   Creative=new(Stock:InstantV3Stock,ShadowCast:InstantV3ShadowCast,HighlightWarmth:InstantV3Warmth,Fog:InstantV3Fog,TemperatureAppearance:(InstantEnvironmentCelsius-21)/21,FrameDrift:InstantV3FrameDrift,Fade:InstantV3Fade,LensVignette:InstantV3Vignette,DevelopmentEdge:InstantV3DevelopmentEdge),
   Paper=new(Version:2,Pattern:InstantV3Pattern,Window:InstantV3Circle?"Circle":"Rectangle",Emboss:InstantV3Emboss,EdgeLine:InstantV3EdgeLine,Reflection:InstantV3Reflection,ReflectionAngle:InstantV3ReflectionAngle,ReflectionWidth:InstantV3ReflectionWidth,Zoom:InstantV3Zoom),
   Defects=new(Kind:InstantV3DefectKind,Amount:InstantV3DefectAmount,Crystal:InstantV3Crystal,Leak:InstantV3Leak)}).Freeze();
 }
 private void RestoreV3(InstantFilmDefinition? film)
 {
  InstantEngineVersion=film?.AlgorithmVersion==3?"V3":"V2";
  if(film?.V3 is{} r){InstantV3Exposure=r.Response.ExposureEv;InstantV3Toe=r.Response.Toe;InstantV3Shoulder=r.Response.Shoulder;InstantV3Density=r.Response.ApparentDensity;InstantV3Softness=r.Optics.DetailMix;InstantV3Diffusion=r.Optics.HighlightDiffusion;InstantV3Texture=r.Emulsion.Microstructure;InstantV3Aging=r.Emulsion.ExpiredAging;InstantV3Seed=r.Random.RootSeed;
   InstantV3Stock=r.Creative?.Stock??"Base";InstantV3Fog=r.Creative?.Fog??0;InstantV3ShadowCast=r.Creative?.ShadowCast??0;InstantV3Warmth=r.Creative?.HighlightWarmth??0;InstantV3FrameDrift=r.Creative?.FrameDrift??0;InstantV3Fade=r.Creative?.Fade??0;InstantV3Vignette=r.Creative?.LensVignette??0;InstantV3DevelopmentEdge=r.Creative?.DevelopmentEdge??0;
   InstantV3Pattern=r.Paper?.Pattern??"Plain";InstantV3Circle=r.Paper?.Window=="Circle";InstantV3Reflection=r.Paper?.Reflection??0;InstantV3ReflectionAngle=r.Paper?.ReflectionAngle??0;InstantV3ReflectionWidth=r.Paper?.ReflectionWidth??.15;InstantV3Zoom=r.Paper?.Zoom??1;InstantV3Emboss=r.Paper?.Emboss??0;InstantV3EdgeLine=r.Paper?.EdgeLine??0;
   InstantV3DefectKind=r.Defects?.Kind??"None";InstantV3DefectAmount=r.Defects?.Amount??0;InstantV3Crystal=r.Defects?.Crystal??0;InstantV3Leak=r.Defects?.Leak??0;InstantV3PaperTexture=r.Surface.PaperTexture;InstantV3PaperScratches=r.Surface.PaperScratches;InstantV3ScanDust=r.Surface.ScanDust;}
  foreach(var property in GetType().GetProperties().Where(p=>p.Name.StartsWith("InstantV3",StringComparison.Ordinal)))OnPropertyChanged(property.Name);
  foreach(var name in new[]{nameof(InstantEngineVersion),nameof(InstantV3Exposure),nameof(InstantV3Toe),nameof(InstantV3Shoulder),nameof(InstantV3Density),nameof(InstantV3Softness),nameof(InstantV3Diffusion),nameof(InstantV3Texture),nameof(InstantV3Aging),nameof(InstantV3Seed)})OnPropertyChanged(name);
 }
}
