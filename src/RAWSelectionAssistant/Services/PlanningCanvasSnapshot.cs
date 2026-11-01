using System.IO;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using PixelTart.Modules.AssetLibrary.FreeCanvas;
namespace RAWSelectionAssistant.Services;

/// <summary>Versioned adapter over the canvas renderer, not another compositor. Legacy saved PNGs are never recomputed.</summary>
public static class PlanningCanvasSnapshot
{
 public const int RenderVersion=3;
 public const string PreviewParameters="JPEG:source>4194304;requestEdge=1024;nativeClosest<=3072,4194304px;ICC=sRGB;EXIF=afterICC;cache=task32MiB,8;outputCenters=v2";
 public const string SamplingContract="CanvasJpegNative1024.ArtboardCenters.v3";
 public static string Render(CanvasDocument document)=>CanvasPixelRenderer.EncodePngBase64(CanvasPixelRenderer.RenderPhotoPreview(document,out _));
 private static readonly SemaphoreSlim Workers=new(1,1);
 public static async Task<string> RenderAsync(CanvasDocument document,CancellationToken token=default)
 {
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(20));token=deadline.Token;await Workers.WaitAsync(token);try{
   var completion=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
   var thread=new Thread(()=>{try{token.ThrowIfCancellationRequested();completion.SetResult(Render(document,token));}catch(Exception e){completion.TrySetException(e);}}){IsBackground=true,Name="PlanningCanvasSnapshot"};
   thread.SetApartmentState(ApartmentState.STA);thread.Start();return await completion.Task;
  }finally{Workers.Release();}
 }
 private static string Render(CanvasDocument document,CancellationToken token)=>CanvasPixelRenderer.EncodePngBase64(CanvasPixelRenderer.RenderPhotoPreview(document,out _,token:token),token);
}
