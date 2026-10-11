using System.Windows;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class ReferenceColorWorkspaceViewModel
{
    private CancellationTokenSource? _professionalDetailCancellation;
    private long _professionalDetailRevision;
    private readonly LinkedList<(string Key,BitmapSource Before,BitmapSource After,Rect Region,long Bytes)> _professionalPreviewCache=new();
    public Task ProfessionalDetailWork {get;private set;}=Task.CompletedTask;
    public void RequestProfessionalViewport(Rect visible,double zoom,double dpiScale)
    {ProfessionalDetailWork=UpdateProfessionalViewportAsync(visible,zoom,dpiScale);StopProcessingCommand.RaiseCanExecuteChanged();}
    public async Task UpdateProfessionalViewportAsync(Rect visible,double zoom,double dpiScale,CancellationToken outer=default)
    {
        if(!double.IsFinite(zoom)||zoom<=0||!double.IsFinite(dpiScale)||dpiScale<=0)return;
        var revision=Interlocked.Increment(ref _professionalDetailRevision);
        _professionalDetailCancellation?.Cancel();_professionalDetailCancellation?.Dispose();
        _professionalDetailCancellation=CancellationTokenSource.CreateLinkedTokenSource(outer);var token=_professionalDetailCancellation.Token;
        if(ActiveTarget is not {} target||target.RawDefinition is not {} definition||target.LinearRawMaster is not {} master)return;
        Editor.ClearIndependentDetail();
        if(visible.IsEmpty||zoom*dpiScale<=1600d/Math.Max(master.Image.Width,master.Image.Height)*1.01)return;
        var version=target.ActiveEditVersionId;var size=ProfessionalRawPipeline.OrientedSize(master);
        var left=Math.Clamp((int)Math.Floor(visible.Left),0,size.Width-1);var top=Math.Clamp((int)Math.Floor(visible.Top),0,size.Height-1);
        var width=Math.Min(ProfessionalRawResourcePolicy.MaximumTileWidth,Math.Min(size.Width-left,Math.Max(1,(int)Math.Ceiling(visible.Right)-left)));
        var height=Math.Min(ProfessionalRawResourcePolicy.MaximumTilePixels/width,Math.Min(size.Height-top,Math.Max(1,(int)Math.Ceiling(visible.Bottom)-top)));
        var region=new Rect(left,top,width,height);
        var key=FormattableString.Invariant($"{target.SourceHash}|{definition.DecodeKey}|{definition.ExposureEv:R}|{version}|{zoom:R}|{dpiScale:R}|Native100V1|{left},{top},{width},{height}");
        RawPipelineStatus="正在请求原像素细节；代理仅作移动占位，不作为100%质量结果";OnPropertyChanged(nameof(RawPipelineStatus));
        try
        {
            await Task.Delay(140,token);
            var cached=_professionalPreviewCache.FirstOrDefault(x=>x.Key==key);
            BitmapSource before,after;
            if(cached.Before is not null){before=cached.Before;after=cached.After;}
            else
            {
                after=await Task.Run(()=>RawDisplayBitmapAdapter.ToBitmap(ProfessionalRawPipeline.RenderRegion(master,definition,left,top,width,height,token).ToVisualRgb24()),token);
                before=definition.ExposureEv==0?after:await Task.Run(()=>RawDisplayBitmapAdapter.ToBitmap(ProfessionalRawPipeline.RenderRegion(master,definition with{ExposureEv=0},left,top,width,height,token).ToVisualRgb24()),token);
            }
            token.ThrowIfCancellationRequested();if(_disposed||revision!=Volatile.Read(ref _professionalDetailRevision)||!ReferenceEquals(target,ActiveTarget)||target.RawDefinition!=definition||target.ActiveEditVersionId!=version)return;
            if(cached.Before is null){_professionalPreviewCache.AddFirst((key,before,after,region,(long)width*height*6));while(_professionalPreviewCache.Sum(x=>x.Bytes)>ProfessionalRawResourcePolicy.PreviewBudgetBytes||_professionalPreviewCache.Count>3)_professionalPreviewCache.RemoveLast();}
            Editor.PresentIndependentDetail(before,after,region);
            RawPipelineStatus=$"原像素细节 {left},{top} · {width}×{height}（超过分块预算的区域仍为代理） · 缩放{zoom*dpiScale:P0}；1600代理不作为细节依据";OnPropertyChanged(nameof(RawPipelineStatus));
        }
        catch(OperationCanceledException){}
        catch(Exception error)when(error is InvalidDataException or IOException or ArgumentException){if(revision==Volatile.Read(ref _professionalDetailRevision)){RawPipelineStatus="细节读取失败："+error.Message;OnPropertyChanged(nameof(RawPipelineStatus));}}
    }
    private void InvalidateProfessionalPreview()
    {
        Interlocked.Increment(ref _professionalDetailRevision);_professionalDetailCancellation?.Cancel();_professionalPreviewCache.Clear();Editor.ClearIndependentDetail();
    }
}
