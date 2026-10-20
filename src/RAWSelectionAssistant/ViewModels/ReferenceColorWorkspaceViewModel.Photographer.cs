using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;
using Kitao.InstantFilm.Research;
namespace RAWSelectionAssistant.ViewModels;
public sealed record PhotographerPaper(string ProfileId,string Name,string Description,string SharedResponse)
{
 public BitmapSource? Preview {get;internal set;}
 public string Group=>ProfileId=="itype-bw"?"黑白":ProfileId is "reclaimed-blue600" or "blue-duochrome600"?"实验性":"彩色";
}
public sealed record PhotographerComparison(string Label,string Value);
public sealed partial class ReferenceColorWorkspaceViewModel
{
 public string PhotographerComparisonValue {get=>Editor.ViewMode=="仿色"?"仿色结果":Editor.ViewMode;set=>Editor.ViewMode=value;}
 public IReadOnlyList<PhotographerComparison> PhotographerComparisonModes {get;}=[new("效果图","仿色结果"),new("配方基准","原片"),new("左右对比","左右对比"),new("并排对比","并排对比")];
 public IReadOnlyList<PhotographerPaper> PhotographerPapers {get;private set;}=[
  new("itype-color","经典彩色","i-Type / 600 彩色 · 当前共用色彩响应","i-Type 与 600 彩色共用；没有另造差异"),
  new("itype-bw","柔调黑白","i-Type / 600 / SX-70 黑白 · 共用响应","三种黑白型号当前共用"),
  new("sx70-color","复古暖彩","SX-70 彩色 · 创作模拟","不是历史 Time-Zero 标定"),
  new("reclaimed-blue600","浅蓝单色","Reclaimed Blue · 蓝到浅色","创作模拟，无实测配对标定"),
  new("blue-duochrome600","深蓝双色","Blue Duochrome · 黑到蓝色","创作模拟，无实测配对标定")];
 public string PaperTechnicalDetail=>$"ID {SelectedInstantProfile.Id} · {SelectedInstantProfile.ChemicalGeneration} · {SelectedInstantProfile.Evidence}";
 public string SelectedPhotographerPaperId {get=>SelectedInstantProfile.Id;set{if(InstantFilmProfiles.Available.FirstOrDefault(x=>x.Id==value) is{} p)SelectedInstantProfile=p;}}
 public string PhotographerPaperName=>PhotographerPapers.FirstOrDefault(x=>x.ProfileId==SelectedInstantProfile.Id)?.Name??SelectedInstantProfile.Name;
 public string PaperPreviewAvailabilityReason=>ActiveTarget is null?"先导入照片。":RawMatchTiff16ProductPipeline.IsRaw(ActiveTarget.Path)?"RAW 相纸缩略图暂不可用；可从已解码照片或 JPEG／TIFF 试用，完整应用仍按原输入准入。":"缩略图只比较同一输入的色彩，不含纸框、装饰或当前编辑。";
 public string PhotographerPaperDetail=>PhotographerPapers.FirstOrDefault(x=>x.ProfileId==SelectedInstantProfile.Id)?.Description??"历史型号 · 保留原配方";
 public bool DarkroomAdjustmentEnabled=>PhotoGradeRasterToolsEnabled||ActiveTarget?.RawDefinition?.LightPrint is not null;
 public string RasterAvailabilityReason=>ActiveTarget is null?"先导入照片，开始基础调整或选择相纸。":ActiveTarget?.RawDefinition?.LightPrint is not null?"仅调画面窗口，相框保持不变；调好后点应用到光片窗口。":IsProfessionalRaw?"当前历史效果版本；可撤销返回普通编辑，或新建光片。":IsLoading||IsExporting||IsInstantGenerating?"当前任务进行中，完成或取消后可编辑。":"基础调色与曲线修改当前编辑；光片先实时预览再显影。";
 public string PhotographerComparisonHelp=>IsProfessionalRaw?"配方基准保留第二张照片、构图与部分纸面；不是第一张未处理照片。":"配方基准为当前源照片；效果图显示当前调整。";
 private string _paperPreviewStatus="缩略图为软件真实渲染；点“预览这些相纸”使用当前照片比较色彩。";
 public string PaperPreviewStatus {get=>_paperPreviewStatus;private set=>SetProperty(ref _paperPreviewStatus,value);}
 public AsyncRelayCommand PreviewPhotographerPapersCommand {get;private set;}=null!;
 private CancellationTokenSource? _paperPreviewCancellation;
 private void ClearPhotographerPreviews()
 {
  _paperPreviewCancellation?.Cancel();foreach(var choice in PhotographerPapers)choice.Preview=null;
  PhotographerPapers=PhotographerPapers.ToArray();OnPropertyChanged(nameof(PhotographerPapers));PaperPreviewStatus="点“预览这些相纸”，使用当前照片比较色彩。";
 }
 private void InitializePhotographerCommands()
 {
  PreviewPhotographerPapersCommand=new(async _=>
  {
   if(ActiveTarget is not{} target||RawMatchTiff16ProductPipeline.IsRaw(target.Path)||IsLoading||IsExporting||IsInstantGenerating)return;
   await StopComparisonAsync();var revision=Volatile.Read(ref _activationRevision);var version=target.ActiveEditVersionId;
   _paperPreviewCancellation?.Cancel();using var cancellation=new CancellationTokenSource();_paperPreviewCancellation=cancellation;var token=cancellation.Token;
   try
   {
    PaperPreviewStatus="正在用同一张照片渲染相纸色彩；不改变当前效果与历史…";
    if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);
    var basis=InstantFilmJpegInput.Definition with{RenderedInputVersion=2};
    // A reduced gallery input is labelled preview only; full apply/export still use the original.
    var source=await Task.Run(()=>StudioQuickExport.Load(target.Path,384),token);
    var master=InstantFilmJpegInput.FromRendered(source,target.SourceHash??target.ObservedSourceHash!,basis);
    using var owner=master.Image;
    var recipes=PhotographerPapers.Select(choice=>
    {
     var p=InstantFilmProfiles.Available.Single(x=>x.Id==choice.ProfileId);
     return(choice,profile:p,recipe:CleanColorPreview(FreezeV3(new InstantFilmDefinition(true,p.Id,p.ChemicalGeneration),false),master.Image.Width/(double)master.Image.Height));
    }).ToArray();
    var images=new List<(PhotographerPaper,BitmapSource)>();
    foreach(var (choice,profile,recipe) in recipes)
    {
     token.ThrowIfCancellationRequested();
     var film=new InstantFilmDefinition(true,recipe.Profile.LegacyId,profile.ChemicalGeneration,AlgorithmVersion:3,PaperFrame:false){V3=recipe};
     var definition=basis with{InstantFilm=film};
     using var pixels=await Task.Run(()=>ProfessionalRawPipeline.RenderPreview(master,definition,240,token),token);
     images.Add((choice,RawDisplayBitmapAdapter.ToBitmap(pixels.ToVisualRgb24(token))));
    }
    if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);
    if(_disposed||revision!=Volatile.Read(ref _activationRevision)||!ReferenceEquals(ActiveTarget,target)||version!=target.ActiveEditVersionId)return;
    foreach(var (choice,image) in images)choice.Preview=image;
    PhotographerPapers=PhotographerPapers.ToArray();
    OnPropertyChanged(nameof(PhotographerPapers));PaperPreviewStatus=$"同源色彩预览 · 不含纸框／装饰／颗粒，强度 100%，曝光 {recipes[0].recipe.Response.ExposureEv:0.##} EV。点击相纸选择，再点应用。";
   }
   catch(OperationCanceledException){PaperPreviewStatus="相纸预览已取消，当前编辑保持。";}
   catch(Exception e)when(e is IOException or NotSupportedException or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException){PaperPreviewStatus="此输入暂不能生成相纸缩略图："+e.Message+"。仍可使用已支持的完整应用入口。";}
   finally{if(ReferenceEquals(_paperPreviewCancellation,cancellation))_paperPreviewCancellation=null;}
  },_=>ActiveTarget is not null&&!RawMatchTiff16ProductPipeline.IsRaw(ActiveTarget.Path)&&!IsLoading&&!IsExporting&&!IsInstantGenerating);
 }
 public static CandidateRecipe CleanColorPreview(CandidateRecipe recipe,double aspect)=>recipe with
 {
  Amount=1,MultipleInput=null,Optics=recipe.Optics with{DetailMix=0,HighlightDiffusion=0},
  Emulsion=recipe.Emulsion with{Microstructure=0,DevelopmentUnevenness=0,DevelopmentDefects=0,ExpiredAging=0},
  Surface=recipe.Surface with{Frame=false,PaperTexture=0,PaperScratches=0,ScanDust=0},
  Geometry=new InstantPaperGeometry(WindowAspect:Math.Clamp(aspect,.25,4),Side:0,Top:0,Bottom:0),Creative=null,Defects=null,Paper=null
 };
}
