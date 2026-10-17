using System.Diagnostics;
using System.Text.Json;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using Kitao.InstantFilm.Research;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
    // Only frozen display bitmaps are cached; never a second full-size float source.
    private string? _comparisonKey;
    private BitmapSource? _comparisonImage;
    private sealed record PreparedProfessionalPreview(ProfessionalRawMaster Master, ProfessionalRawDefinition Definition,
        string SourceHash, Guid TargetId, Guid? SourceVersion, long Revision, BitmapSource Image, BitmapSource Before, double RenderMs, double CompareMs);
    private async Task<PreparedProfessionalPreview> PrepareProfessionalPreviewAsync(ReferenceTargetItem target,
        ProfessionalRawMaster master, ProfessionalRawDefinition definition, CancellationToken token)
    {
        if(master.Contract.DecodeKey!=definition.DecodeKey || !string.Equals(master.Contract.SourceSha256,
            target.SourceHash??target.ObservedSourceHash,StringComparison.OrdinalIgnoreCase))throw new IOException("预览输入身份或 DecodeKey 不一致。");
        using var masterLease=StudioGlobalResources.RetainBuffer(master.Image,(long)master.Image.Rgb32.Length*4);
        var renderWatch=Stopwatch.StartNew();
        var rendered=await RenderProfessionalPreviewAsync(master,definition,1600,token);
        var renderMs=renderWatch.Elapsed.TotalMilliseconds;
        BitmapSource image;
        try { image=StudioQuickExport.ApplyOrientation(RawDisplayBitmapAdapter.ToBitmap(rendered.ToVisualRgb24(token)),rendered.Orientation); }
        finally { if(InstantV3ProductPipeline.IsV3(definition))rendered.Dispose(); }
        var original=InstantV3ProductPipeline.IsV3(definition)?InstantV3ProductPipeline.Original(definition):definition with{ExposureEv=0,SrgbExposure=null};
        var key=JsonSerializer.Serialize(new{master.Contract.SourceSha256,DecodeKey=ComparisonIdentity(original).DecodeKey,master.Image.Width,master.Image.Height,master.Image.Orientation,Definition=ComparisonIdentity(original),Edge=1600,Quality="DisplayLinearBoxBandsV1"});
        var watch=Stopwatch.StartNew();BitmapSource before;
        if(_comparisonKey==key&&_comparisonImage is {} cached)before=cached;
        else if(original==definition)before=image;
        else
        {
            var source=await RenderProfessionalPreviewAsync(master,original,1600,token,"ComparePreview");
            try { before=StudioQuickExport.ApplyOrientation(RawDisplayBitmapAdapter.ToBitmap(source.ToVisualRgb24(token)),source.Orientation); }
            finally { if(InstantV3ProductPipeline.IsV3(original))source.Dispose(); }
        }
        token.ThrowIfCancellationRequested();
        CandidateProcessingEvidence.Enter("PreviewPrepared",token);
        if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);
        definition.InstantFilm?.V3?.MultipleInput?.ValidateSource(token);
        return new(master,definition,master.Contract.SourceSha256,target.Id,target.ActiveEditVersionId,Volatile.Read(ref _activationRevision),image,before,renderMs,watch.Elapsed.TotalMilliseconds);
    }
    // Original raster recipe lineage is history, not input pixels. Keep persisted DecodeKey
    // unchanged; explicitly rebind only after proving all pixel-affecting input fields match.
    private static ProfessionalRawDefinition ComparisonIdentity(ProfessionalRawDefinition definition) =>
        definition.RenderedInputVersion==2 && definition.InstantInput is {Mode:"Original",Look:null,Stack:null,Film:null,LegacyRaw:null} input
            ? definition with{InstantInput=input with{OriginVersionId=null}} : definition;
    private static ProfessionalRawMaster? ReusableMaster(ReferenceTargetItem target,ProfessionalRawDefinition definition)
    {
        var master=target.LinearRawMaster;
        if(master is null||!string.Equals(master.Contract.SourceSha256,target.SourceHash??target.ObservedSourceHash,StringComparison.OrdinalIgnoreCase))return null;
        if(master.Contract.DecodeKey==definition.DecodeKey)return master;
        var previous=target.RawDefinition;
        if(previous is null||master.Contract.DecodeKey!=previous.DecodeKey||previous.RenderedInputVersion!=2||definition.RenderedInputVersion!=2
            ||previous.InstantInput?.Mode!="Original"||definition.InstantInput?.Mode!="Original"
            ||ComparisonIdentity(previous).DecodeKey!=ComparisonIdentity(definition).DecodeKey)return null;
        return master with{Contract=master.Contract with{DecodeKey=definition.DecodeKey}};
    }
    private void EnsurePreparedCurrent(ReferenceTargetItem target, ProfessionalRawDefinition? prior, Guid? version, long revision, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(_disposed||revision!=Volatile.Read(ref _activationRevision)||!ReferenceEquals(ActiveTarget,target)||target.RawDefinition!=prior||target.ActiveEditVersionId!=version)
            throw new OperationCanceledException("准备结果已过期，未提交。",token);
    }
}
