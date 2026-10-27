using Kitao.InstantFilm.Research;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.ViewModels;
public sealed record R53VariantFrame(Guid Id,string Name,BitmapSource Image);
public sealed partial class ReferenceColorWorkspaceViewModel
{
    private readonly Dictionary<string,BitmapSource> r53CompareCache=[];
    private CancellationTokenSource? _r53CompareCancellation;private long _r53CompareRevision;private readonly SemaphoreSlim _r53CompareGate=new(1,1);
    public IReadOnlyList<R53VariantFrame> R53CompareFrames{get;private set;}=[];
    public string R53MultiCompareStatus{get;private set;}="";
    public async Task PrepareR53VariantComparisonAsync(IReadOnlyList<Guid> ids,CancellationToken token=default)
    {
        if(_disposed)throw new ObjectDisposedException(nameof(ReferenceColorWorkspaceViewModel));
        _r53CompareCancellation?.Cancel();var revision=++_r53CompareRevision;R53CompareFrames=[];OnPropertyChanged(nameof(R53CompareFrames));
        if(IsLoading||IsExporting||IsInstantGenerating)throw new InvalidOperationException("当前正在切换照片或处理任务，请稍后对比。");
        if(ids.Count is <2 or >4||ids.Distinct().Count()!=ids.Count||ActiveTarget is not{} target)throw new ArgumentException("请选择2到4个不同变体。");
        Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);var versions=ids.Select(id=>target.EditVersions.Single(v=>v.Id==id&&!v.Archived)).ToArray();var cts=CancellationTokenSource.CreateLinkedTokenSource(token);_r53CompareCancellation=cts;
        R53MultiCompareStatus="正在准备变体对比…";OnPropertyChanged(nameof(R53MultiCompareStatus));
        try
        {
            await _r53CompareGate.WaitAsync(cts.Token);try
            {
                if(!await ValidateTargetIdentityAsync(target,cts.Token))throw new IOException(target.Error);
                BitmapSource? source=null;var frames=new List<R53VariantFrame>();
                foreach(var version in versions)
                {
                    cts.Token.ThrowIfCancellationRequested();BitmapSource result;var cacheKey=System.Text.Json.JsonSerializer.Serialize(new{TargetId=target.Id,target.SourceHash,VersionId=version.Id,version.Stack,version.Look,version.Film,version.RawDefinition,edge=1200});if(r53CompareCache.TryGetValue(cacheKey,out var cached)){frames.Add(new(version.Id,version.Name,cached));continue;}
                    if(version.RawDefinition is{} raw)
                    {
                        if(target.LinearRawMaster is not{} master)throw new InvalidOperationException("当前RAW缓存不可用，请先激活照片。");if(raw.DecodeKey!=master.Contract.DecodeKey)throw new NotSupportedException("当前对比不并发重解码不同RAW输入定义；请先统一输入设置。");
                        var pixels=await RenderProfessionalPreviewAsync(master,raw,1200,cts.Token,"R53VariantCompare");try{result=StudioQuickExport.ApplyOrientation(RawDisplayBitmapAdapter.ToBitmap(pixels.ToVisualRgb24(cts.Token)),pixels.Orientation);}finally{if(InstantV3ProductPipeline.IsV3(raw))pixels.Dispose();}
                    }
                    else if(version.Stack is{} stack){source??=await Task.Run(()=>StudioQuickExport.LoadCompareProxy(target.Path,1200,cts.Token),cts.Token);result=await Task.Run(()=>ColorStudioBitmapRenderer.Render(source,stack,version.Look,cts.Token),cts.Token);}
                    else if(version.Look is{} look){source??=await Task.Run(()=>StudioQuickExport.LoadCompareProxy(target.Path,1200,cts.Token),cts.Token);result=await Task.Run(()=>ColorStudioBitmapRenderer.Render(source,ColorStudioLegacyMigration.Migrate(look with{Film=version.Film??look.Film}).Stack,look,cts.Token),cts.Token);}
                    else result=source??=await Task.Run(()=>StudioQuickExport.LoadCompareProxy(target.Path,1200,cts.Token),cts.Token);
                    cts.Token.ThrowIfCancellationRequested();if(_disposed||revision!=_r53CompareRevision)return;while(r53CompareCache.Count>=4||r53CompareCache.Values.Sum(x=>(long)x.PixelWidth*x.PixelHeight*4)+(long)result.PixelWidth*result.PixelHeight*4>32L*1024*1024){if(r53CompareCache.Count==0)break;r53CompareCache.Remove(r53CompareCache.Keys.First());}r53CompareCache[cacheKey]=result;frames.Add(new(version.Id,version.Name,result));
                }
                if(_disposed||revision!=_r53CompareRevision||!ReferenceEquals(ActiveTarget,target)||IsLoading)return;R53CompareFrames=frames;OnPropertyChanged(nameof(R53CompareFrames));R53MultiCompareStatus="对比就绪（1200px视图；不改变主变体）";
            }finally{_r53CompareGate.Release();}
        }
        catch(OperationCanceledException){if(revision==_r53CompareRevision)R53MultiCompareStatus="对比已取消";}
        finally{if(ReferenceEquals(_r53CompareCancellation,cts))_r53CompareCancellation=null;cts.Dispose();OnPropertyChanged(nameof(R53MultiCompareStatus));}
    }
    private void ClearR53CompareCache(){CloseR53VariantComparison();r53CompareCache.Clear();}
    public void CloseR53VariantComparison(){_r53CompareCancellation?.Cancel();_r53CompareRevision++;R53CompareFrames=[];OnPropertyChanged(nameof(R53CompareFrames));}
}
