using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
    private readonly IProfessionalRawDecoder _professionalDecoder;
    public AsyncRelayCommand CreateProfessionalRawCommand {get;private set;}=null!;
    public AsyncRelayCommand ApplyProfessionalRawCommand {get;private set;}=null!;
    public bool IsProfessionalRaw=>ActiveTarget?.RawDefinition is not null;
    public bool IsLegacyRaw=>!IsProfessionalRaw;
    public double RawExposureEv {get;set;}
    public double RawRedGain {get;set;}=1;
    public double RawGreenGain {get;set;}=1;
    public double RawBlueGain {get;set;}=1;
    public bool RawUseCameraWhiteBalance {get;set;}=true;
    public string RawPipelineStatus {get;private set;}="PV4 · 线性 ProPhoto / LibRaw，相机白平衡只在解码应用一次";
    private void InitializeProfessionalRawCommands()
    {
        CreateProfessionalRawCommand=new AsyncRelayCommand(async _=>
        {
            if(ActiveTarget is not {} target||IsLoading||IsExporting)return;
            if(!RawMatchTiff16ProductPipeline.IsRaw(target.Path)){StatusText="JPG 是已渲染图像，请使用原有调色；新 RAW 版本只接受 RAW。";return;}
            var sourceVersion=target.ActiveEditVersionId;var revision=Volatile.Read(ref _activationRevision);
            try
            {
                var definition=new ProfessionalRawDefinition();
                var master=await _professionalDecoder.DecodeAsync(target.Path,definition);
                if(_disposed||!ReferenceEquals(ActiveTarget,target)||target.ActiveEditVersionId!=sourceVersion||revision!=Volatile.Read(ref _activationRevision))return;
                if(!string.Equals(master.Contract.SourceSha256,target.SourceHash,StringComparison.OrdinalIgnoreCase))throw new IOException("RAW 源身份改变。");
                Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);CancelTargetThumbnail(target.Id);Editor.StopProcessing();
                target.LinearRawMaster=master;target.RawMaster=null;target.RawInput=master.Contract;
                target.ActiveEditVersionId=Guid.NewGuid();target.RawDefinition=definition;target.AppliedLookSnapshot=null;target.ColorAdjustmentStackSnapshot=null;target.FilmSettingsSnapshot=null;
                CaptureEditVersion(target);target.EditVersions[^1]=target.EditVersions[^1] with{Name="RAW 基础 v4",OriginVersionId=sourceVersion,OriginIdentity=StudioVersionRepository.Identity(target.LibraryId,target.AssetId,target.SourceHash!)};
                await ActivateTargetAsync(target);
            }
            catch(Exception e)when(e is InvalidDataException or IOException or ArgumentException or NotSupportedException){StatusText="未创建新 RAW 版本："+e.Message;}
        });
        ApplyProfessionalRawCommand=new AsyncRelayCommand(async _=>
        {
            if(ActiveTarget is not {} target||target.RawDefinition is null||IsLoading||IsExporting)return;
            try
            {
                var previous=target.RawDefinition;var version=target.ActiveEditVersionId;var revision=Volatile.Read(ref _activationRevision);
                var next=new ProfessionalRawDefinition(WhiteBalance:RawUseCameraWhiteBalance?"Camera":"CameraGains",RedGain:RawRedGain,GreenGain:RawGreenGain,BlueGain:RawBlueGain,ExposureEv:RawExposureEv).Validate();
                var master=target.LinearRawMaster?.Contract.DecodeKey==next.DecodeKey?target.LinearRawMaster:await _professionalDecoder.DecodeAsync(target.Path,next);
                if(_disposed||!ReferenceEquals(ActiveTarget,target)||target.ActiveEditVersionId!=version||target.RawDefinition!=previous||revision!=Volatile.Read(ref _activationRevision))return;
                if(!string.Equals(master.Contract.SourceSha256,target.SourceHash,StringComparison.OrdinalIgnoreCase))throw new IOException("RAW 源身份改变。");
                target.RawDefinition=next;target.LinearRawMaster=master;target.RawInput=master.Contract;await ActivateTargetAsync(target);CaptureEditVersion(target);
            }
            catch(Exception e)when(e is InvalidDataException or IOException or ArgumentException or NotSupportedException){StatusText="未应用 RAW 参数，保留上一版本："+e.Message;}
        });
    }
    private static string ProcessingFingerprint(ReferenceLook? look,ColorAdjustmentStack? stack,PixelTartFilmSettings? film,ProfessionalRawDefinition? definition)
    {
        // Preserve legacy pending-receipt identity byte-for-byte; only PV4 adds a new definition.
        var serialized=definition is null ? System.Text.Json.JsonSerializer.Serialize(new{Look=look,Stack=stack,Film=film}) :
            System.Text.Json.JsonSerializer.Serialize(new{Look=look,Stack=stack,Film=film,RawDefinition=definition});
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(serialized)));
    }
    private async Task ActivateProfessionalRawAsync(ReferenceTargetItem target,long revision,CancellationToken token)
    {
        InvalidateProfessionalPreview();
        CancelTargetThumbnail(target.Id);
        var definition=target.RawDefinition!.Validate();
        RawPipelineStatus="正在解码新 RAW 版本…";OnPropertyChanged(nameof(RawPipelineStatus));
        if(target.LinearRawMaster is null||target.LinearRawMaster.Contract.DecodeKey!=definition.DecodeKey)
            {var decoded=await _professionalDecoder.DecodeAsync(target.Path,definition,token);token.ThrowIfCancellationRequested();if(revision!=Volatile.Read(ref _activationRevision)||target.RawDefinition!=definition)return;target.LinearRawMaster=decoded;}
        var master=target.LinearRawMaster;
        if(!string.Equals(master.Contract.SourceSha256,target.SourceHash,StringComparison.OrdinalIgnoreCase))throw new IOException("RAW 解码源身份改变，未呈现结果。");
        var quick=await Task.Run(()=>ProfessionalRawPipeline.Render(master,definition,800,token),token);
        if(revision!=Volatile.Read(ref _activationRevision)||target.RawDefinition!=definition)return;
        if(ReferenceEquals(ActiveTarget,target)&&Editor.IndependentRawMode)
        {Editor.PresentIndependentRaw(StudioQuickExport.ApplyOrientation(RawDisplayBitmapAdapter.ToBitmap(quick.ToVisualRgb24()),quick.Orientation));RawPipelineStatus="快速800代理 · 正在更新线性光抗混叠高质量预览";OnPropertyChanged(nameof(RawPipelineStatus));}
        var rendered=await Task.Run(()=>ProfessionalRawPipeline.RenderPreview(master,definition,1600,token),token);
        var original=definition.ExposureEv==0?rendered:await Task.Run(()=>ProfessionalRawPipeline.RenderPreview(master,definition with{ExposureEv=0},1600,token),token);
        if(revision!=Volatile.Read(ref _activationRevision)||target.RawDefinition!=definition)return;
        var image=StudioQuickExport.ApplyOrientation(RawDisplayBitmapAdapter.ToBitmap(rendered.ToVisualRgb24()),rendered.Orientation);var before=StudioQuickExport.ApplyOrientation(RawDisplayBitmapAdapter.ToBitmap(original.ToVisualRgb24()),original.Orientation);
        target.RawInput=master.Contract;target.PixelWidth=master.Image.Width;target.PixelHeight=master.Image.Height;target.SourceHash=master.Contract.SourceSha256.ToLowerInvariant();
        QueueMetadata(()=>HydrateAssetMetadataAsync(target));await FlushMetadataAsync();
        if(revision!=Volatile.Read(ref _activationRevision))return;
        TrimProfessionalMasters(target);
        ActiveTarget=target;TargetImage=image;TargetName=target.FileName;target.IsActive=true;foreach(var other in Targets.Where(x=>!ReferenceEquals(x,target)))other.IsActive=false;
        Editor.IndependentRawMode=true;await Editor.SetSourceAsync(target.AssetId,before,token);Editor.StopProcessing();if(revision!=Volatile.Read(ref _activationRevision)||token.IsCancellationRequested)return;Editor.PresentIndependentRaw(image);var oriented=ProfessionalRawPipeline.OrientedSize(master);Editor.SetIndependentRawSize(new(oriented.Width,oriented.Height));
        var thumb=new TransformedBitmap(image,new System.Windows.Media.ScaleTransform(Math.Min(1,320d/image.PixelWidth),Math.Min(1,320d/image.PixelWidth)));thumb.Freeze();target.Thumbnail=thumb;target.SourceThumbnail=before;target.Status=ReferenceTargetStatus.Adjusted;target.Error=null;if(ReferenceEquals(FailedTarget,target))FailedTarget=null;
        RawExposureEv=definition.ExposureEv;RawRedGain=definition.RedGain;RawGreenGain=definition.GreenGain;RawBlueGain=definition.BlueGain;RawUseCameraWhiteBalance=definition.WhiteBalance=="Camera";
        foreach(var name in new[]{nameof(IsProfessionalRaw),nameof(IsLegacyRaw),nameof(RawExposureEv),nameof(RawRedGain),nameof(RawGreenGain),nameof(RawBlueGain),nameof(RawUseCameraWhiteBalance)})OnPropertyChanged(name);
        RefreshSyncAvailability();
        RawPipelineStatus=$"PV4 · {master.Contract.Model} · LibRaw Linear ProPhoto D65 → sRGB · 解码饱和通道 {master.Contract.SaturatedChannelSamples:N0}；不代表未裁切传感器";OnPropertyChanged(nameof(RawPipelineStatus));
        StatusText="新 RAW 版本预览完成；未适配 HDR/仿色/胶片已禁用";CaptureEditVersion(target);await RefreshPreviewAnalysisAsync(token,image);
    }
    private async Task ExportProfessionalRawAsync(ReferenceTargetItem target,ProfessionalRawDefinition definition,string temporary,StudioExportFormat format,CancellationToken token)
    {
        if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);
        var master=target.LinearRawMaster?.Contract.DecodeKey==definition.DecodeKey?target.LinearRawMaster:await _professionalDecoder.DecodeAsync(target.Path,definition,token);
        if(!string.Equals(master!.Contract.SourceSha256,target.SourceHash,StringComparison.OrdinalIgnoreCase))throw new IOException("RAW 导出源身份改变。");
        target.LastProfessionalOutput=await ProfessionalRawExport.WriteAsync(master,definition,temporary,format,token);
        TrimProfessionalMasters(ActiveTarget??target);
    }
    private void TrimProfessionalMasters(ReferenceTargetItem keep)
    {
        foreach(var item in Targets.Where(x=>!ReferenceEquals(x,keep))){item.LinearRawMaster=null;if(item.RawDefinition is not null){item.RawMaster=null;item.SourceThumbnail=null;}}
    }
}
