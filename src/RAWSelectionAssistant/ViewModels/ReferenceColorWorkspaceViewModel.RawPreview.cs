using System.Windows;using System.Windows.Media.Imaging;using RAWSelectionAssistant.Core.Services.Projects;using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
    private CancellationTokenSource? _professionalDetailCancellation;private long _professionalDetailRevision;private readonly Guid _previewOwner=Guid.NewGuid();
    private readonly SynchronizationContext? _previewContext=SynchronizationContext.Current;
    private readonly LinkedList<(string Key,BitmapSource Before,BitmapSource After,Rect Region,long Bytes)> _professionalPreviewCache=new();
    public Task ProfessionalDetailWork {get;private set;}=Task.CompletedTask;
    public void RequestProfessionalViewport(Rect visible,double zoom,double dpiScale){ProfessionalDetailWork=UpdateProfessionalViewportAsync(visible,zoom,dpiScale);StopProcessingCommand.RaiseCanExecuteChanged();}
    public async Task UpdateProfessionalViewportAsync(Rect visible,double zoom,double dpiScale,CancellationToken outer=default)
    {
        if(!double.IsFinite(zoom)||zoom<=0||!double.IsFinite(dpiScale)||dpiScale<=0)return;
        var revision=Interlocked.Increment(ref _professionalDetailRevision);_professionalDetailCancellation?.Cancel();_professionalDetailCancellation?.Dispose();_professionalDetailCancellation=CancellationTokenSource.CreateLinkedTokenSource(outer);var token=_professionalDetailCancellation.Token;
        if(ActiveTarget is not {} target||target.RawDefinition is not {} definition||target.LinearRawMaster is not {} master)return;
        using var retainedMaster=StudioGlobalResources.Pin(target.MasterResourceOwner,"Master");
        Editor.ClearIndependentDetail();if(visible.IsEmpty||zoom*dpiScale<=1600d/Math.Max(master.Image.Width,master.Image.Height)*1.01)return;
        var version=target.ActiveEditVersionId;var size=ProfessionalRawPipeline.OutputSize(master,definition);var left=Math.Clamp((int)Math.Floor(visible.Left),0,size.Width-1);var top=Math.Clamp((int)Math.Floor(visible.Top),0,size.Height-1);var right=Math.Clamp((int)Math.Ceiling(visible.Right),left+1,size.Width);var bottom=Math.Clamp((int)Math.Ceiling(visible.Bottom),top+1,size.Height);
        var tiles=new List<StudioDetailTile>();long retained=0;RawPipelineStatus="代理占位 · 正在分块补齐可见原像素";OnPropertyChanged(nameof(RawPipelineStatus));
        try
        {
            await Task.Delay(140,token);
            for(var y=top;y<bottom;y+=512)for(var x=left;x<right;x+=512)
            {
                token.ThrowIfCancellationRequested();var w=Math.Min(512,right-x);var h=Math.Min(512,bottom-y);var bytes=(long)w*h*6;if(retained+bytes>ProfessionalRawResourcePolicy.PreviewBudgetBytes)continue;
                var key=System.Text.Json.JsonSerializer.Serialize(new{target.SourceHash,Definition=definition,version,zoom,dpiScale,quality="NativeTilesV2",x,y,w,h});
                var cached=_professionalPreviewCache.FirstOrDefault(t=>t.Key==key);BitmapSource before,after;
                if(cached.Before is not null){before=cached.Before;after=cached.After;}else
                {await ProfessionalRawResourcePolicy.HeavyWork.WaitAsync(token);try{using var reservation=StudioGlobalResources.ReserveWork((long)master.Image.Rgb32.Length*4+32L*1024*1024);after=await Task.Run(()=>{using var result=ProfessionalRawPipeline.RenderRegion(master,definition,x,y,w,h,token);if(definition.ColorState?.Version==2){using var converted=new RAWSelectionAssistant.Core.Services.Color.ControlledIcc.DocumentRenderResultV1(result.Clone(),definition.ColorState.Working,target.ActiveEditVersionId,"Detail");using var cms=new RAWSelectionAssistant.Core.Services.Color.ControlledIcc.LcmsRgbEngine();using var srgb=converted.ConvertTo(cms.StandardProfile("sRGB"),token);return RawDisplayBitmapAdapter.ToBitmap(srgb.Pixels.ToVisualRgb24());}return RawDisplayBitmapAdapter.ToBitmap(result.ToVisualRgb24());},token);before=definition.ExposureEv==0&&definition.InstantFilm?.AlgorithmVersion!=3?after:await Task.Run(()=>{using var result=ProfessionalRawPipeline.RenderRegion(master,(definition.InstantFilm?.AlgorithmVersion==3?Kitao.InstantFilm.Research.InstantV3ProductPipeline.Original(definition):definition with{ExposureEv=0}),x,y,w,h,token);if(definition.ColorState?.Version==2){using var converted=new RAWSelectionAssistant.Core.Services.Color.ControlledIcc.DocumentRenderResultV1(result.Clone(),definition.ColorState.Working,target.ActiveEditVersionId,"Detail");using var cms=new RAWSelectionAssistant.Core.Services.Color.ControlledIcc.LcmsRgbEngine();using var srgb=converted.ConvertTo(cms.StandardProfile("sRGB"),token);return RawDisplayBitmapAdapter.ToBitmap(srgb.Pixels.ToVisualRgb24());}return RawDisplayBitmapAdapter.ToBitmap(result.ToVisualRgb24());},token);}finally{ProfessionalRawResourcePolicy.HeavyWork.Release();}}
                if(_disposed||revision!=Volatile.Read(ref _professionalDetailRevision)||!ReferenceEquals(target,ActiveTarget)||target.RawDefinition!=definition||target.ActiveEditVersionId!=version)return;
                var region=new Rect(x,y,w,h);tiles.Add(new(region,before,after));retained+=bytes;
                if(cached.Before is null)_professionalPreviewCache.AddFirst((key,before,after,region,bytes));while(_professionalPreviewCache.Count>0&&retained+_professionalPreviewCache.Sum(t=>t.Bytes)>ProfessionalRawResourcePolicy.PreviewBudgetBytes)_professionalPreviewCache.RemoveLast();
                var weak=new WeakReference<ReferenceColorWorkspaceViewModel>(this);StudioGlobalResources.Store(_previewOwner,"Detail",retained+_professionalPreviewCache.Sum(t=>t.Bytes),()=>{if(weak.TryGetTarget(out var owner)){void Evict(){owner.InvalidateProfessionalPreview();owner.RawPipelineStatus="全局预算已释放细节，当前显示代理";owner.OnPropertyChanged(nameof(RawPipelineStatus));}if(owner._previewContext is {} context)context.Post(_=>Evict(),null);else Evict();}});
                token.ThrowIfCancellationRequested();if(revision!=Volatile.Read(ref _professionalDetailRevision))return;Editor.PresentIndependentDetail(before,after,region);Editor.PresentIndependentTiles(tiles.ToArray());
            }
            var requested=new Rect(left,top,right-left,bottom-top);var filled=tiles.Sum(t=>{var intersection=Rect.Intersect(t.Region,requested);return intersection.IsEmpty?0:intersection.Width*intersection.Height;});RawPipelineStatus=filled==(right-left)*(bottom-top)?$"可见视口100%原像素已补齐 · {tiles.Count}块":$"原像素已补{tiles.Count}块；超过64MiB预算区域仍为代理";OnPropertyChanged(nameof(RawPipelineStatus));
        }
        catch(OperationCanceledException){}catch(Exception error)when(error is InvalidDataException or IOException or ArgumentException or InvalidOperationException){if(revision==Volatile.Read(ref _professionalDetailRevision)){RawPipelineStatus="细节失败："+error.Message;OnPropertyChanged(nameof(RawPipelineStatus));}}
    }
    private void InvalidateProfessionalPreview(){Interlocked.Increment(ref _professionalDetailRevision);_professionalDetailCancellation?.Cancel();_professionalPreviewCache.Clear();StudioGlobalResources.Release(_previewOwner,"Detail");Editor.ClearIndependentDetail();}
}
