using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
 private LatestPreviewQueue<FrameGallerySnapshot,IReadOnlyDictionary<string,BitmapSource>>? frameGallery;
 private long frameGalleryRevision;
 public IReadOnlyDictionary<string,BitmapSource> FramePreviews{get;private set;}=new Dictionary<string,BitmapSource>();
 public Task FramePreviewWork=>frameGallery?.Work??Task.CompletedTask;
 private sealed record FrameGallerySnapshot(Guid Target,Guid? Variant,ReferenceTargetItem Item,ProfessionalRawDefinition Definition,long Revision);
 private void ClearFrameGallery(){frameGalleryRevision++;frameGallery?.Invalidate();FramePreviews=new Dictionary<string,BitmapSource>();OnPropertyChanged(nameof(FramePreviews));}
 public void RequestFramePreviews()
 {
  if(_disposed||ActiveTarget is not{} target||IsLoading||IsExporting||IsInstantGenerating||IsLightPrintLocked)return;
  ProfessionalRawDefinition definition;
  try{definition=BuildInstantDefinition(target,target.RawDefinition,target.ActiveEditVersionId,false);}
  catch(Exception e)when(e is IOException or InvalidOperationException or ArgumentException or NotSupportedException){LightPrintStatus="相框缩略未更新："+e.Message;return;}
  ClearFrameGallery();var snapshot=new FrameGallerySnapshot(target.Id,target.ActiveEditVersionId,target,definition,frameGalleryRevision);
  frameGallery??=new(async(s,token)=>
  {
   if(!await ValidateTargetIdentityAsync(s.Item,token))throw new IOException(s.Item.Error);
   var master=ReusableMaster(s.Item,s.Definition)??await DecodeProfessionalInputAsync(s.Item.Path,s.Definition,token);
   using var owner=ReferenceEquals(master.Image,s.Item.LinearRawMaster?.Image)?null:master.Image;
   using var pin=StudioGlobalResources.RetainBuffer(master.Image,(long)master.Image.Rgb32.Length*4);
   // Bounded shared source derivative before paper. Every card then uses the same production V3 session.
   using var source=await RenderProfessionalPreviewAsync(master,s.Definition with{InstantFilm=null,LightPrint=null},192,token,"FrameGalleryInput");
   var baseDefinition=InstantFilmJpegInput.Definition with{RenderedInputVersion=2};
   var small=new ProfessionalRawMaster(source,master.Contract with{DecodeKey=baseDefinition.DecodeKey,OutputSpace="sRGB",Gamma="sRGB",WhiteBalanceApplied=false,CameraMatrixApplied=false});
   var images=new Dictionary<string,BitmapSource>();
   foreach(var frame in LightPrintFrameChoices.Where(f=>f.Shippable))
   {
    token.ThrowIfCancellationRequested();var film=FreezeGalleryFrame(s.Definition.InstantFilm!,frame);
    var d=baseDefinition with{InstantFilm=film};using var pixels=await Task.Run(()=>ProfessionalRawPipeline.RenderPreview(small,d,128,token),token);
    var bitmap=RawDisplayBitmapAdapter.ToBitmap(pixels.ToVisualRgb24(token));bitmap.Freeze();images.Add(frame.Id,bitmap);
   }
   return images;
  },(_,s,images)=>{if(_disposed||ActiveTarget?.Id!=s.Target||ActiveTarget.ActiveEditVersionId!=s.Variant||s.Revision!=frameGalleryRevision)return;FramePreviews=images;OnPropertyChanged(nameof(FramePreviews));},e=>LightPrintStatus="相框缩略未完成，未显示旧照片："+e.Message);
  frameGallery.Submit(snapshot);
 }
 private static InstantFilmDefinition FreezeGalleryFrame(InstantFilmDefinition film,LightPrintFrame frame)
 {
  var r=film.V3??throw new NotSupportedException("相框缩略要求V3");var art=frame.Id.StartsWith("orig.",StringComparison.Ordinal)?OriginalFrameLibrary.Find(frame.Id):null;
  double aspect=art is null?1:(art.X1-art.X0)*art.Width/((art.Y1-art.Y0)*art.Height);
  return film with{PaperFrame=true,V3=r with{FrameArtwork=art,Surface=r.Surface with{Frame=true,BaseColor=frame.Color},Geometry=r.Geometry with{WindowAspect=aspect},Paper=(r.Paper??new()) with{Version=3,Window=frame.Window,Pattern=frame.Pattern}}};
 }
}
