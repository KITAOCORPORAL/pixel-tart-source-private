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
    public bool IsLinearProfessionalRaw=>ActiveTarget?.RawDefinition?.InputConfiguration=="LibRaw021LinearProPhotoD65V1";
    public bool IsLegacyRaw=>!IsProfessionalRaw;
    public double RawExposureEv {get;set;}
    public double RawRedGain {get;set;}=1;
    public double RawGreenGain {get;set;}=1;
    public double RawBlueGain {get;set;}=1;
    public bool RawUseCameraWhiteBalance {get;set;}=true;
    public string RawPipelineStatus {get;private set;}="PV4 · 线性 ProPhoto / LibRaw，相机白平衡只在解码应用一次";
    private async Task<ProfessionalRawMaster> DecodeProfessionalInputAsync(string path,ProfessionalRawDefinition definition,CancellationToken token=default)
    {
        using var decodeMeasurement=StudioPreviewDiagnostics.Measure("DecodeMaster");
        if(definition.SrgbExposure is not null||definition.RenderedInputVersion==2)return await RenderedSrgbInput.ReadAsync(path,definition,token);
        if(definition.InputConfiguration=="LegacyRawEditsSrgbFloatV1")return await RebuildLegacyRawInputAsync(path,definition,null,token);
        if(definition.InputConfiguration!="RenderedSrgbFloatV1")return await _professionalDecoder.DecodeAsync(path,definition,token);
        if(definition.InstantInput is not {Mode:"CurrentEdits"} recipe)return await InstantFilmJpegInput.ReadAsync(path,definition,token);
        using var source=System.IO.File.OpenRead(path);
        var sourceHash=Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(source,token));
        using var reservation=StudioGlobalResources.ReserveWork(StudioGlobalResources.MasterLimit);
        var rendered=await Editor.ProcessForExportAsync(path,recipe.Look,recipe.Film,token,recipe.Stack);
        token.ThrowIfCancellationRequested();
        return InstantFilmJpegInput.FromRendered(rendered,sourceHash,definition);
    }
    private async Task<ProfessionalRawMaster> RebuildLegacyRawInputAsync(string path,ProfessionalRawDefinition definition,FrozenRawMaster? frozen,CancellationToken token)
    {
        definition.Validate();await ProfessionalRawResourcePolicy.HeavyWork.WaitAsync(token);
        try
        {
            if(frozen is null)
            {
                using var decodeWork=StudioGlobalResources.ReserveWork(StudioGlobalResources.MasterLimit);
                frozen=await _rawPipeline.DecodeFrozenMasterAsync(path,token);
            }
            else await frozen.ValidateSourceAsync(token);
            token.ThrowIfCancellationRequested();
            return await Task.Run(()=>LegacyRawInstantInput.Render(frozen,definition,_rawPipeline,token),token);
        }
        finally{ProfessionalRawResourcePolicy.HeavyWork.Release();}
    }
    private void InitializeProfessionalRawCommands()
    {
        InitializeInstantFilmCommands();
        CreateProfessionalRawCommand=new AsyncRelayCommand(async _=>
        {
            if(ActiveTarget is not {} target||IsLoading||IsExporting||IsInstantGenerating)return;
            if(!RawMatchTiff16ProductPipeline.IsRaw(target.Path)){StatusText="JPG 是已渲染图像，请使用原有调色；新 RAW 版本只接受 RAW。";return;}
            await StopComparisonAsync();
            var sourceVersion=target.ActiveEditVersionId;var revision=Volatile.Read(ref _activationRevision);
            try
            {
                var definition=new ProfessionalRawDefinition();
                var master=await _professionalDecoder.DecodeAsync(target.Path,definition);
                if(_disposed||!ReferenceEquals(ActiveTarget,target)||target.ActiveEditVersionId!=sourceVersion||revision!=Volatile.Read(ref _activationRevision))return;
                if(!string.Equals(master.Contract.SourceSha256,target.SourceHash,StringComparison.OrdinalIgnoreCase))throw new IOException("RAW 源身份改变。");
        using var activeMasterLease=StudioGlobalResources.Pin(target.MasterResourceOwner,"Master");
                Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);CancelTargetThumbnail(target.Id);Editor.StopProcessing();
                target.LinearRawMaster=master;target.RawMaster=null;target.RawInput=master.Contract;
                target.ActiveEditVersionId=Guid.NewGuid();target.RawDefinition=definition;target.AppliedLookSnapshot=null;target.ColorAdjustmentStackSnapshot=null;target.FilmSettingsSnapshot=null;
                CaptureEditVersion(target);target.EditVersions[^1]=target.EditVersions[^1] with{Name="RAW 基础 v4",OriginVersionId=sourceVersion,OriginIdentity=StudioVersionRepository.Identity(target.LibraryId,target.AssetId,target.SourceHash!)};
                await ActivateTargetAsync(target);
            }
            catch(Exception e)when(e is InvalidDataException or IOException or ArgumentException or NotSupportedException or InvalidOperationException){LastProcessingFailure=StudioProcessingFailure.From(e,"ProfessionalRaw");OnPropertyChanged(nameof(LastProcessingFailure));StatusText="未创建新 RAW 版本："+e.Message;}
        });
        ApplyProfessionalRawCommand=new AsyncRelayCommand(async _=>
        {
            if(ActiveTarget is not {} target||target.RawDefinition is null||target.RawDefinition.IsRenderedInput||IsLoading||IsExporting||IsInstantGenerating||Editor.ReadOnlyTarget||target.RawDefinition.SceneExposure?.Locked==true)return;
            try
            {
                var previous=target.RawDefinition;var version=target.ActiveEditVersionId;var revision=Volatile.Read(ref _activationRevision);
                var next=previous with{WhiteBalance=RawUseCameraWhiteBalance?"Camera":"CameraGains",RedGain=RawRedGain,GreenGain=RawGreenGain,BlueGain=RawBlueGain,ExposureEv=RawExposureEv};next=next.Validate();
                var master=target.LinearRawMaster?.Contract.DecodeKey==next.DecodeKey?target.LinearRawMaster:await _professionalDecoder.DecodeAsync(target.Path,next);
                if(_disposed||!ReferenceEquals(ActiveTarget,target)||target.ActiveEditVersionId!=version||target.RawDefinition!=previous||revision!=Volatile.Read(ref _activationRevision))return;
                if(!string.Equals(master.Contract.SourceSha256,target.SourceHash,StringComparison.OrdinalIgnoreCase))throw new IOException("RAW 源身份改变。");
                RememberRawLocal(target,previous);target.RawDefinition=next;target.LinearRawMaster=master;target.RawInput=master.Contract;await ActivateTargetAsync(target);CaptureEditVersion(target);
            }
            catch(Exception e)when(e is InvalidDataException or IOException or ArgumentException or NotSupportedException or InvalidOperationException){LastProcessingFailure=StudioProcessingFailure.From(e,"ProfessionalRaw");OnPropertyChanged(nameof(LastProcessingFailure));StatusText="未应用 RAW 参数，保留上一版本："+e.Message;}
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
        if(target.RawDefinition?.InputConfiguration=="LegacyRawEditsSrgbFloatV1")target.RawMaster=null;
        var definition=target.RawDefinition!.Validate();
        RawPipelineStatus="正在解码新 RAW 版本…";OnPropertyChanged(nameof(RawPipelineStatus));
        var master=target.LinearRawMaster;
        if(master is null||master.Contract.DecodeKey!=definition.DecodeKey||!string.Equals(master.Contract.SourceSha256,target.SourceHash??target.ObservedSourceHash,StringComparison.OrdinalIgnoreCase))
        {master=await DecodeProfessionalInputAsync(target.Path,definition,token);token.ThrowIfCancellationRequested();if(revision!=Volatile.Read(ref _activationRevision)||target.RawDefinition!=definition)return;target.LinearRawMaster=master;}
        if(!string.Equals(master.Contract.SourceSha256,target.SourceHash,StringComparison.OrdinalIgnoreCase))throw new IOException("RAW 解码源身份改变，未呈现结果。");
        var prepared=await PrepareProfessionalPreviewAsync(target,master,definition,token);
        if(revision!=Volatile.Read(ref _activationRevision)||target.RawDefinition!=definition)return;
        await PresentProfessionalPreviewAsync(target,prepared,revision,token);
    }
    private async Task PresentProfessionalPreviewAsync(ReferenceTargetItem target,PreparedProfessionalPreview prepared,long revision,CancellationToken token)
    {
        var master=prepared.Master;var definition=prepared.Definition;var image=prepared.Image;var before=prepared.Before;
        token.ThrowIfCancellationRequested();
        if(_disposed||revision!=Volatile.Read(ref _activationRevision)||prepared.Revision!=revision||prepared.TargetId!=target.Id||target.RawDefinition!=definition)throw new OperationCanceledException(token);
        target.RawInput=master.Contract;target.PixelWidth=master.Image.Width;target.PixelHeight=master.Image.Height;target.SourceHash=master.Contract.SourceSha256.ToLowerInvariant();
        var sourceDimensions=ProfessionalRawPipeline.OrientedSize(master);var paperDimensions=ProfessionalRawPipeline.OutputSize(master,definition);
        InstantDimensions=$"原尺寸 {sourceDimensions.Width} × {sourceDimensions.Height} · 相纸 {paperDimensions.Width} × {paperDimensions.Height}";OnPropertyChanged(nameof(InstantDimensions));
        QueueMetadata(()=>HydrateAssetMetadataAsync(target));await FlushMetadataAsync();
        if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);
        definition.InstantFilm?.V3?.MultipleInput?.ValidateSource(token);
        if(_disposed||revision!=Volatile.Read(ref _activationRevision))throw new OperationCanceledException(token);
        TrimProfessionalMasters(target);
        ActiveTarget=target;TargetImage=image;TargetName=target.FileName;target.IsActive=true;foreach(var other in Targets.Where(x=>!ReferenceEquals(x,target)))other.IsActive=false;
        Editor.RestoreTargetEngine(target.EngineSnapshot,target.ExecutionModeSnapshot);
        Editor.RawLocalEditing=definition.InstantFilm is null&&(definition.ProcessingVersion==4&&!definition.IsRenderedInput||definition.ProcessingVersion==5&&definition.RenderedInputVersion==2);OnPropertyChanged(nameof(RawLocalEditing));OnPropertyChanged(nameof(DarkroomAdjustmentEnabled));OnPropertyChanged(nameof(RasterAvailabilityReason));
        Editor.ApplyTargetSnapshot(target.AppliedLookSnapshot,target.FilmSettingsSnapshot,definition.LocalEdits?.Stack??definition.LightPrint?.WindowAdjustments??target.ColorAdjustmentStackSnapshot,render:false,targetIdentity:target.ActiveEditVersionId??target.Id);
        using var presentationMeasurement=StudioPreviewDiagnostics.Measure("Present");
        Editor.IndependentRawMode=true;await Editor.SetSourceAsync(target.AssetId,before,token,render:false);Editor.StopProcessing();if(_disposed||revision!=Volatile.Read(ref _activationRevision)||token.IsCancellationRequested)throw new OperationCanceledException(token);Editor.PresentIndependentRaw(image);var oriented=ProfessionalRawPipeline.OutputSize(master,definition);Editor.SetIndependentRawSize(new(oriented.Width,oriented.Height));
        var thumb=new TransformedBitmap(image,new System.Windows.Media.ScaleTransform(Math.Min(1,320d/image.PixelWidth),Math.Min(1,320d/image.PixelWidth)));thumb.Freeze();target.Thumbnail=thumb;target.SourceThumbnail=before;target.Status=ReferenceTargetStatus.Adjusted;target.Error=null;if(ReferenceEquals(FailedTarget,target))FailedTarget=null;
        RawExposureEv=definition.ExposureEv;RawRedGain=definition.RedGain;RawGreenGain=definition.GreenGain;RawBlueGain=definition.BlueGain;RawUseCameraWhiteBalance=definition.WhiteBalance=="Camera";
        foreach(var name in new[]{nameof(PhotoGradeRasterToolsEnabled),nameof(IsProfessionalRaw),nameof(IsLinearProfessionalRaw),nameof(IsLegacyRaw),nameof(RawExposureEv),nameof(RawRedGain),nameof(RawGreenGain),nameof(RawBlueGain),nameof(RawUseCameraWhiteBalance)})OnPropertyChanged(name);
        RestoreInstantControls(definition.InstantFilm);RefreshSyncAvailability();
        RawPipelineStatus=definition.SrgbExposure is not null?"Rendered sRGB D65 → Linear sRGB exposure v1 → sRGB encode/clip；非场景RAW/显示ICC":definition.InputConfiguration=="LegacyRawEditsSrgbFloatV1"?"PV6 · 旧RAW完整编辑 → 已渲染sRGB · 不重复白平衡/曝光/显示转换":definition.InputConfiguration=="RenderedSrgbFloatV1"?"PV5 · 完整JPEG → WIC sRGB · 不重复应用白平衡；创作非标定":$"PV4 · {master.Contract.Model} · LibRaw Linear ProPhoto D65 → sRGB · 解码饱和通道 {master.Contract.SaturatedChannelSamples:N0}；不代表未裁切传感器";OnPropertyChanged(nameof(RawPipelineStatus));
        StatusText="原尺寸管线预览完成；即时胶片为创作近似，非相纸标定";
        Kitao.InstantFilm.Research.CandidateProcessingEvidence.Enter("PreviewCommit",token);
        if(_disposed||revision!=Volatile.Read(ref _activationRevision))throw new OperationCanceledException(token);
        if(before is not null){_comparisonKey=ComparisonKey(master,ComparisonDefinition(definition));_comparisonImage=before;}
        UpdateComparisonAvailability();
        CaptureEditVersion(target);EnsureR53Variants(target);if(Editor.RawLocalEditing)WarmRawFastBase(master,definition);NotifySceneExposure();QueueLayerThumbnails();await RefreshPreviewAnalysisAsync(token,image);
        token.ThrowIfCancellationRequested();if(_disposed||revision!=Volatile.Read(ref _activationRevision)||target.RawDefinition!=definition)throw new OperationCanceledException(token);
    }
    private async Task ExportProfessionalRawAsync(ReferenceTargetItem target,ProfessionalRawDefinition definition,string temporary,StudioExportFormat format,CancellationToken token)
    {
        if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);
        var master=target.LinearRawMaster?.Contract.DecodeKey==definition.DecodeKey?target.LinearRawMaster:await DecodeProfessionalInputAsync(target.Path,definition,token);
        if(!string.Equals(master!.Contract.SourceSha256,target.SourceHash,StringComparison.OrdinalIgnoreCase))throw new IOException("RAW 导出源身份改变。");
        var actualFormat=format==StudioExportFormat.Source&&definition.InputConfiguration=="RenderedSrgbFloatV1"?StudioExportFormat.Jpeg:format;
        using var retainedMaster=StudioGlobalResources.Pin(target.MasterResourceOwner,"Master");
        target.LastProfessionalOutput=await ProfessionalRawExport.WriteAsync(master,definition,temporary,actualFormat,token);
        TrimProfessionalMasters(ActiveTarget??target);
    }
    private void TrimProfessionalMasters(ReferenceTargetItem keep)
    {
        foreach(var item in Targets.Where(x=>!ReferenceEquals(x,keep))){item.LinearRawMaster=null;if(item.RawDefinition is not null){item.RawMaster=null;item.SourceThumbnail=null;}}
    }
}
