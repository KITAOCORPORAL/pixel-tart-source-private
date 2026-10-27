using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.ViewModels;
public sealed record StudioLayerThumbnailContext(IReadOnlyDictionary<Guid,BitmapSource> Outputs,IReadOnlyDictionary<Guid,BitmapSource> Masks,long Revision)
{public IReadOnlyDictionary<Guid,BitmapSource> Inputs{get;init;}=new Dictionary<Guid,BitmapSource>();}
public sealed partial class ReferenceColorWorkspaceViewModel
{
 private LatestPreviewQueue<ThumbnailSnapshot,StudioLayerThumbnailContext>? layerThumbnailQueue;
 internal string? LayerThumbnailFailure{get;private set;}
 private long thumbnailRevision;private Guid? thumbnailTarget,thumbnailVariant;
 public StudioLayerThumbnailContext? LayerThumbnailContext{get;private set;}
 public BitmapSource? SceneExposureMaskPreview=>LayerThumbnailContext?.Masks.GetValueOrDefault(Guid.Empty);
 internal Task LayerThumbnailWork=>layerThumbnailQueue?.Work??Task.CompletedTask;
 private sealed record ThumbnailSnapshot(Guid Target,Guid? Variant,BitmapSource? Raster,ProfessionalRawMaster? Master,ProfessionalRawDefinition? Definition,ColorAdjustmentStack Stack,ReferenceLook? Reference,long Revision);
 private void QueueLayerThumbnails()
 {
  if(_disposed||ActiveTarget is not{} target)return;
  if(Editor.AdjustmentStack.Nodes.Count==0&&target.RawDefinition?.SceneExposure?.Mask is null){layerThumbnailQueue?.Invalidate();LayerThumbnailContext=null;OnPropertyChanged(nameof(LayerThumbnailContext));OnPropertyChanged(nameof(SceneExposureMaskPreview));return;}
  if(thumbnailTarget!=target.Id||thumbnailVariant!=target.ActiveEditVersionId){layerThumbnailQueue?.Invalidate();LayerThumbnailContext=null;OnPropertyChanged(nameof(LayerThumbnailContext));OnPropertyChanged(nameof(SceneExposureMaskPreview));thumbnailTarget=target.Id;thumbnailVariant=target.ActiveEditVersionId;}
  LayerThumbnailFailure=null;var snapshot=new ThumbnailSnapshot(target.Id,target.ActiveEditVersionId,Editor.SourceImage,target.LinearRawMaster,target.RawDefinition,Editor.AdjustmentStack.Nodes.Count==0?Editor.AdjustmentStack:Editor.AdjustmentStack.DeepClone(),Editor.SelectedLook,++thumbnailRevision);
  layerThumbnailQueue??=new(async(s,token)=>{
   using var pin=s.Master is{} master?StudioGlobalResources.RetainBuffer(master.Image,(long)master.Image.Rgb32.Length*4):null;
   return await Task.Run(()=>BuildLayerThumbnails(s,token),token);
  },(_,s,result)=>{if(_disposed||ActiveTarget?.Id!=s.Target||ActiveTarget.ActiveEditVersionId!=s.Variant||s.Revision!=thumbnailRevision)return;LayerThumbnailContext=result;OnPropertyChanged(nameof(LayerThumbnailContext));OnPropertyChanged(nameof(SceneExposureMaskPreview));},error=>{LayerThumbnailFailure=error.ToString();});
  layerThumbnailQueue.Submit(snapshot);
 }
 private static StudioLayerThumbnailContext BuildLayerThumbnails(ThumbnailSnapshot s,CancellationToken token)
 {
  var inputs=new Dictionary<Guid,BitmapSource>();var outputs=new Dictionary<Guid,BitmapSource>();var masks=new Dictionary<Guid,BitmapSource>();BitmapSource? basis=null;
  if(s.Master is{} master&&s.Definition is{} raw&&raw.InstantFilm is null&&(raw.ProcessingVersion==4&&!raw.IsRenderedInput||raw.ProcessingVersion==5&&raw.RenderedInputVersion==2)){if(raw.IsRenderedInput){using var pixels=ProfessionalRawLocalRenderer.Preview(master,raw with{LocalEdits=null},192,token);basis=RawDisplayBitmapAdapter.ToBitmap(pixels.ToVisualRgb24(token));}else{using var samples=SceneExposureInteractiveV1.Prepare(master,raw,192,token);using var pixels=SceneExposureInteractiveV1.Render(master,raw with{LocalEdits=null},samples,192,token);basis=RawDisplayBitmapAdapter.ToBitmap(pixels.ToVisualRgb24(token));}}
  else if(s.Master is not null)return new(outputs,masks,s.Revision); // Never treat an already rendered RAW final as raw input.
  else if(s.Raster is{} source){double scale=Math.Min(1,192d/Math.Max(source.PixelWidth,source.PixelHeight));var small=new TransformedBitmap(source,new ScaleTransform(scale,scale));small.Freeze();basis=small;}
  if(basis is null)return new(outputs,masks,s.Revision);
  var prefix=new List<ColorAdjustmentStackNode>();var oriented=s.Master is{} m?ProfessionalRawPipeline.OrientedSize(m):(basis.PixelWidth,basis.PixelHeight);double aspect=(double)oriented.Item1/oriented.Item2;
  if(s.Definition?.SceneExposure?.Mask is{} sceneMask){var sample=sceneMask.Compile(aspect,token);int w=64,h=Math.Max(1,(int)Math.Round(64/aspect));if(h>64){w=Math.Max(1,(int)Math.Round(64*aspect));h=64;}var a=new byte[w*h];for(int y=0;y<h;y++){token.ThrowIfCancellationRequested();for(int x=0;x<w;x++)a[y*w+x]=(byte)Math.Round(sample((x+.5)/w,(y+.5)/h)*255);}var preview=BitmapSource.Create(w,h,96,96,PixelFormats.Gray8,null,a,w);preview.Freeze();masks[Guid.Empty]=preview;}
  foreach(var node in s.Stack.Nodes.Take(64)){token.ThrowIfCancellationRequested();inputs[node.Id]=prefix.Count==0?basis:outputs[prefix[^1].Id];prefix.Add(node);var stack=s.Stack with{Nodes=prefix.ToArray()};
   if(s.Master is{} rm&&s.Definition is{} rd){using var basePixels=BitmapPixels(basis);using var result=ProfessionalRawLocalRenderer.Fast(rm,rd with{LocalEdits=new(stack,rd.IsRenderedInput?2:1)},basePixels,token);outputs[node.Id]=RawDisplayBitmapAdapter.ToBitmap(result.ToVisualRgb24(token));}
   else outputs[node.Id]=ColorStudioBitmapRenderer.Render(basis,stack,s.Reference,token);
   var sample=node.LocalMask?.Compile(aspect,token);double ratio=(double)basis.PixelWidth/basis.PixelHeight;int w=ratio>=1?64:Math.Max(1,(int)Math.Round(64*ratio)),h=ratio>=1?Math.Max(1,(int)Math.Round(64/ratio)):64;var a=new byte[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++){var p=(s.Definition?.Geometry??new()).SourceTuple((x+.5)/w,(y+.5)/h,aspect);a[y*w+x]=(byte)Math.Round((sample?.Invoke(p.X,p.Y)??1)*255);}var mask=BitmapSource.Create(w,h,96,96,PixelFormats.Gray8,null,a,w);mask.Freeze();masks[node.Id]=mask;
  }
  return new(outputs,masks,s.Revision){Inputs=inputs};
 }
 private static RAWSelectionAssistant.Core.Services.Color.HighBitDepthImageBuffer BitmapPixels(BitmapSource image){var rgb=new FormatConvertedBitmap(image,PixelFormats.Rgb24,null,0);var a=new byte[rgb.PixelWidth*rgb.PixelHeight*3];rgb.CopyPixels(a,rgb.PixelWidth*3,0);return new(rgb.PixelWidth,rgb.PixelHeight,a.Select(v=>v/255f).ToArray(),"8","sRGB");}
}
