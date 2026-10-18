using System.Diagnostics;
using System.Windows.Media.Imaging;
using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
    private CancellationTokenSource? _comparisonCancellation;
    private long _comparisonRevision;
    private bool _isComparisonPreparing;
    private string _comparisonStatus="";
    public bool IsComparisonPreparing {get=>_isComparisonPreparing;private set{SetProperty(ref _isComparisonPreparing,value);OnPropertyChanged(nameof(ComparisonStatus));StopProcessingCommand?.RaiseCanExecuteChanged();}}
    public string ComparisonStatus {get=>_comparisonStatus;private set=>SetProperty(ref _comparisonStatus,value);}
    public bool IsComparisonReady=>!Editor.ComparisonUnavailable;
    public Task ComparisonWork {get;private set;}=Task.CompletedTask;
    public double? LastComparisonMilliseconds {get;private set;}
    public AsyncRelayCommand PrepareComparisonCommand {get;private set;}=null!;
    private void InitializeComparison()
    {
        PrepareComparisonCommand=new(async _=>await RequestComparisonAsync(),_=>!_disposed&&!IsLoading&&!IsExporting&&!IsInstantGenerating&&!IsComparisonPreparing&&Editor.ComparisonUnavailable&&ActiveTarget?.RawDefinition is not null);
    }
    private void CancelComparison()
    {
        Interlocked.Increment(ref _comparisonRevision);_comparisonCancellation?.Cancel();
        IsComparisonPreparing=false;
        if(Editor is not null&&Editor.ComparisonUnavailable)ComparisonStatus="配方基准对照未就绪；可继续编辑或按需准备（保留第二曝光及几何）。";
        PrepareComparisonCommand?.RaiseCanExecuteChanged();
    }
    private async Task StopComparisonAsync()
    {
        CancelComparison();await ComparisonWork;
    }
    private void UpdateComparisonAvailability()
    {
        OnPropertyChanged(nameof(IsComparisonReady));
        if(ActiveTarget?.RawDefinition is null){ComparisonStatus="原片对照使用当前照片源；配方基准仅适用于新管线。";PrepareComparisonCommand?.RaiseCanExecuteChanged();return;}
        ComparisonStatus=Editor.ComparisonUnavailable?"效果已就绪。配方基准对照未准备；选择对照模式或点击准备，保留第二曝光及几何。":"配方基准对照已就绪（保留第二曝光及几何／纸面参数）。";
        PrepareComparisonCommand?.RaiseCanExecuteChanged();
    }
    public Task RequestComparisonAsync()
    {
        if(_disposed||IsLoading||IsExporting||IsInstantGenerating||IsComparisonPreparing||!ComparisonWork.IsCompleted||!Editor.ComparisonUnavailable||ActiveTarget is not{} target||target.RawDefinition is not{} definition)return ComparisonWork;
        var master=target.LinearRawMaster;
        if(master is null){ComparisonStatus="对照输入缓存已释放，请重新激活当前照片。";return Task.CompletedTask;}
        var version=target.ActiveEditVersionId;var activation=Volatile.Read(ref _activationRevision);var revision=Interlocked.Increment(ref _comparisonRevision);
        var cts=new CancellationTokenSource();_comparisonCancellation=cts;IsComparisonPreparing=true;LastComparisonMilliseconds=null;
        ComparisonStatus="正在准备当前配方基准对照；效果已可见，可继续操作。";PrepareComparisonCommand.RaiseCanExecuteChanged();
        ComparisonWork=RunAsync();return ComparisonWork;
        async Task RunAsync()
        {
            var token=cts.Token;var clock=Stopwatch.StartNew();
            try
            {
                await Task.Yield();token.ThrowIfCancellationRequested();
                using var retained=StudioGlobalResources.RetainBuffer(master.Image,(long)master.Image.Rgb32.Length*4);
                if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);
                if(master.Contract.DecodeKey!=definition.DecodeKey||!string.Equals(master.Contract.SourceSha256,target.SourceHash,StringComparison.OrdinalIgnoreCase))throw new IOException("对照输入身份不一致");
                CandidateProcessingEvidence.Enter("ComparisonStarted",token);
                var original=ComparisonDefinition(definition);
                var pixels=await RenderProfessionalPreviewAsync(master,original,1600,token,"ComparePreview");
                BitmapSource before;
                try{before=StudioQuickExport.ApplyOrientation(RawDisplayBitmapAdapter.ToBitmap(pixels.ToVisualRgb24(token)),pixels.Orientation);}
                finally{if(InstantV3ProductPipeline.IsV3(original))pixels.Dispose();}
                CandidateProcessingEvidence.Enter("ComparisonPrepared",token);
                if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);
                definition.InstantFilm?.V3?.MultipleInput?.ValidateSource(token);token.ThrowIfCancellationRequested();
                if(_disposed||revision!=Volatile.Read(ref _comparisonRevision)||activation!=Volatile.Read(ref _activationRevision)||!ReferenceEquals(ActiveTarget,target)||target.ActiveEditVersionId!=version||target.RawDefinition!=definition)return;
                _comparisonKey=ComparisonKey(master,original);_comparisonImage=before;target.SourceThumbnail=before;
                Editor.PresentComparisonSource(before);LastComparisonMilliseconds=clock.Elapsed.TotalMilliseconds;OnPropertyChanged(nameof(LastComparisonMilliseconds));UpdateComparisonAvailability();
                CandidateProcessingEvidence.Enter("ComparisonVisible",token);
            }
            catch(OperationCanceledException){}
            catch(Exception e)when(e is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException or System.Runtime.InteropServices.COMException)
            {if(!_disposed&&revision==Volatile.Read(ref _comparisonRevision))ComparisonStatus="对照未完成，效果保留："+e.Message;}
            finally
            {
                if(ReferenceEquals(_comparisonCancellation,cts))_comparisonCancellation=null;cts.Dispose();
                if(revision==Volatile.Read(ref _comparisonRevision)){IsComparisonPreparing=false;PrepareComparisonCommand?.RaiseCanExecuteChanged();}
            }
        }
    }
    private static ProfessionalRawDefinition ComparisonDefinition(ProfessionalRawDefinition definition)=>InstantV3ProductPipeline.IsV3(definition)?InstantV3ProductPipeline.Original(definition):definition with{ExposureEv=0,SrgbExposure=null};
    private static string ComparisonKey(ProfessionalRawMaster master,ProfessionalRawDefinition original)=>System.Text.Json.JsonSerializer.Serialize(new{master.Contract.SourceSha256,DecodeKey=ComparisonIdentity(original).DecodeKey,master.Image.Width,master.Image.Height,master.Image.Orientation,Definition=ComparisonIdentity(original),Edge=1600,Quality="DisplayLinearBoxBandsV1"});
}
