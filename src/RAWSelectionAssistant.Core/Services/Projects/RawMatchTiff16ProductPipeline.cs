using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using System.Security.Cryptography;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Color Studio's RAW master and TIFF16 route. RGB24 is exclusively a display/analysis adapter.</summary>
public sealed class RawMatchTiff16ProductPipeline(IRawDecoder decoder)
{
    public static bool IsRaw(string path) => RawToJpegDefaults.CandidateRawExtensions.Contains(Path.GetExtension(path));

    public Task<HighBitDepthImageBuffer> DecodeMasterAsync(string path, CancellationToken token = default) =>
        DecodeMasterCoreAsync(path,false,token);

    private async Task<HighBitDepthImageBuffer> DecodeMasterCoreAsync(string path,bool upright,CancellationToken token)
    {
        if (!IsRaw(path)) throw new RawDecodeException(ErrorCodeCatalog.UnsupportedFormat, "Not a candidate RAW file.");
        var decoded = await decoder.DecodeAsync(path, new RawToJpegOptions(AutoRotate: false, DecodeMode: RawDecodeMode.ProfessionalDecode), token).ConfigureAwait(false);
        if (!decoded.IsProfessionalPrecision || decoded.Channels != 3 || decoded.Rgb48Pixels!.Length != checked(decoded.Width * decoded.Height * 3))
            throw new RawDecodeException(ErrorCodeCatalog.DecodeFailed, "The camera RAW did not provide genuine 16-bit RGB output.");
        if (!string.Equals(decoded.Metadata.ColorSpace, "sRGB", StringComparison.OrdinalIgnoreCase))
            throw new RawDecodeException(ErrorCodeCatalog.DecodeFailed, "The RAW output color space is not supported by Color Studio.");
        return upright ? RawImageOrientation.OwnUprightRgb48(decoded,token) : HighBitDepthImageBuffer.FromRaw(decoded);
    }

    /// <summary>Same professional RGB48 decode and historical nearest proxy samples, but convert only
    /// selected samples to float instead of materializing a full RGB-float master. Viewing-only.</summary>
    public async Task<HighBitDepthImageBuffer> DecodeViewingProxyAsync(string path,int maximumEdge,CancellationToken token=default)
    {
        if(maximumEdge is <1 or >1600)throw new ArgumentOutOfRangeException(nameof(maximumEdge));
        if(!IsRaw(path))throw new NotSupportedException("Viewing RAW proxy requires RAW input");
        await ProfessionalRawResourcePolicy.HeavyWork.WaitAsync(token).ConfigureAwait(false);
        try
        {
            using var reservation=decoder is LibRawDecoder?null:StudioGlobalResources.ReserveWork(1024L*1024*1024);
            var decoded=decoder is LibRawDecoder native?await native.DecodeViewingProxyAsync(path,maximumEdge,token).ConfigureAwait(false):await decoder.DecodeAsync(path,new RawToJpegOptions(AutoRotate:false,DecodeMode:RawDecodeMode.ProfessionalDecode),token).ConfigureAwait(false);
            if(!decoded.IsProfessionalPrecision||decoded.Channels!=3||decoded.Rgb48Pixels!.Length!=checked(decoded.Width*decoded.Height*3)||decoded.Metadata.ColorSpace!="sRGB"||decoded.Metadata.Orientation is <1 or >8)throw new InvalidDataException("Unsupported viewing RAW precision/color/orientation");
            var scale=Math.Min(1,maximumEdge/(double)Math.Max(decoded.Width,decoded.Height));int width=Math.Max(1,(int)Math.Round(decoded.Width*scale)),height=Math.Max(1,(int)Math.Round(decoded.Height*scale));var values=new float[width*height*3];
            for(int y=0;y<height;y++){token.ThrowIfCancellationRequested();int sy=Math.Min(decoded.Height-1,(int)((y+.5)*decoded.Height/height));for(int x=0;x<width;x++){int sx=Math.Min(decoded.Width-1,(int)((x+.5)*decoded.Width/width));for(int c=0;c<3;c++)values[(y*width+x)*3+c]=decoded.Rgb48Pixels[(sy*decoded.Width+sx)*3+c]/65535f;}}
            return new(width,height,values,"16","sRGB",decoded.Metadata.Orientation,decoded.Metadata);
        }
        finally{ProfessionalRawResourcePolicy.HeavyWork.Release();}
    }

    public Task<FrozenRawMaster> DecodeFrozenMasterAsync(string path, CancellationToken token = default) =>
        DecodeFrozenMasterCoreAsync(path,false,token);

    public Task<FrozenRawMaster> DecodeUprightFrozenMasterAsync(string path,CancellationToken token = default) =>
        DecodeFrozenMasterCoreAsync(path,true,token);

    private async Task<FrozenRawMaster> DecodeFrozenMasterCoreAsync(string path,bool upright,CancellationToken token)
    {
        var image = await DecodeMasterCoreAsync(path,upright,token).ConfigureAwait(false);
        try
        {
            await using var stream = new FileStream(Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);
            var sourceSha256 = Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false));
            return new FrozenRawMaster(path, sourceSha256, Guid.NewGuid(), image);
        }
        catch { image.Dispose(); throw; } // Fresh, unpublished image is owned by this decode scope only.
    }

    public ColorStudioRenderResult Render(HighBitDepthImageBuffer master, ReferenceLook? look, ColorAdjustmentStack? stack, int maximumEdge = 0, CancellationToken token = default, PixelTartFilmSettings? film = null)
    {
        ArgumentNullException.ThrowIfNull(master);
        if (master.SourceBitDepth != "16" || !string.Equals(master.WorkingColorSpace, "sRGB", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("RAW processing requires 16-bit decoded sRGB input.");
        var working = maximumEdge > 0 ? CreateProxy(master, maximumEdge, token) : master;
        var state = ColorStudioEffectiveState.Resolve(look, stack, film) ?? (look is not null
            ? ColorStudioLegacyMigration.Migrate(look with { Film = film ?? look.Film }).Stack
            : new ColorAdjustmentStack([new(Guid.NewGuid(), ColorStudioNodeType.Preset, "原图", true)]));
        token.ThrowIfCancellationRequested();
        var display = ColorStudioProcessingAnalysis.RequiresSourceAnalysis(state,look) ? working.ToVisualRgb24(token) : new VisualPixelBuffer(1,1,new byte[3]);
        var analysis = ColorStudioProcessingAnalysis.Create(display, state, look, token);
        return new ColorStudioRenderPipeline().Render(working, analysis, look, state, token, captureNodeDiagnostics: false);
    }

    /// <summary>Display-only consumer with identical samples; never materialize an unused full float result.</summary>
    public VisualPixelBuffer RenderDisplay(HighBitDepthImageBuffer master,ReferenceLook? look,ColorAdjustmentStack? stack,
        int maximumEdge=0,CancellationToken token=default,PixelTartFilmSettings? film=null)
    {
        ArgumentNullException.ThrowIfNull(master);
        if(master.SourceBitDepth!="16"||!string.Equals(master.WorkingColorSpace,"sRGB",StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("RAW processing requires 16-bit decoded sRGB input.");
        token.ThrowIfCancellationRequested();
        var working=maximumEdge>0?CreateProxy(master,maximumEdge,token):master;
        using var proxyOwner=ReferenceEquals(working,master)?null:working;
        var state=ColorStudioEffectiveState.Resolve(look,stack,film)??(look is not null
            ?ColorStudioLegacyMigration.Migrate(look with{Film=film??look.Film}).Stack
            :new ColorAdjustmentStack([new(Guid.NewGuid(),ColorStudioNodeType.Preset,"原图",true)]));
        var renderer=new ColorStudioRenderPipeline();
        using var rows=renderer.TryCreatePointExportRows(working,state,token);
        if(rows is not null)
        {
            using var scratch=GraphPixelAllocation.Enter();
            var rgb=new byte[checked(working.Width*working.Height*3)];
            for(int y=0;y<working.Height;y++)rows.FillDisplayRow(y,rgb.AsMemory(y*working.Width*3,working.Width*3));
            return new(working.Width,working.Height,rgb);
        }
        using var processing=renderer.RenderForExport(working,look,state,token);
        return processing.Pixels.ToVisualRgb24(token);
    }

    public VisualPixelBuffer DisplaySource(HighBitDepthImageBuffer master, int maximumEdge, CancellationToken token = default) =>
        CreateProxy(master, maximumEdge, token).ToVisualRgb24();

    public HighBitDepthImageBuffer PreviewMaster(HighBitDepthImageBuffer master, int maximumEdge = 1600, CancellationToken token = default) =>
        CreateProxy(master, maximumEdge, token);

    public async Task<TiffExportResult> ExportAsync(string sourcePath, string destinationPath, ReferenceLook? look, ColorAdjustmentStack? stack, CancellationToken token = default, PixelTartFilmSettings? film = null, ReadOnlyMemory<byte> outputIcc = default)
    {
        var master = await DecodeFrozenMasterAsync(sourcePath, token).ConfigureAwait(false);
        return await ExportAsync(master, destinationPath, look, stack, token, film, outputIcc).ConfigureAwait(false);
    }

    public async Task<TiffExportResult> ExportAsync(FrozenRawMaster master, string destinationPath, ReferenceLook? look, ColorAdjustmentStack? stack, CancellationToken token = default, PixelTartFilmSettings? film = null, ReadOnlyMemory<byte> outputIcc = default)
    {
        ArgumentNullException.ThrowIfNull(master);
        await master.ValidateSourceAsync(token).ConfigureAwait(false);
        var image = master.Image;
        if (image.SourceBitDepth != "16" || !string.Equals(image.WorkingColorSpace, "sRGB", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("RAW processing requires 16-bit decoded sRGB input.");
        var state = ColorStudioEffectiveState.Resolve(look, stack, film) ?? (look is not null
            ? ColorStudioLegacyMigration.Migrate(look with { Film = film ?? look.Film }).Stack
            : new ColorAdjustmentStack([new(Guid.NewGuid(), ColorStudioNodeType.Preset, "原图", true)]));
        var renderer = new ColorStudioRenderPipeline();
        using var rows = renderer.TryCreatePointExportRows(image,state,token);
        if (rows is not null)
        {
            // Reuse only this export's bounded strip backing; no global cache or full-frame slot.
            using var scratch = GraphPixelAllocation.Enter();
            return await AtomicTiffWriter.WriteRowsAsync(destinationPath,image.Width,image.Height,rows.FillRow,
                new(TiffBitDepth.Sixteen,IccProfile:outputIcc,Software:"光镜",Orientation:image.Orientation),token,overwrite:false).ConfigureAwait(false);
        }
        using var result = renderer.RenderForExport(image, look, state, token);
        return await AtomicTiffWriter.WriteRgb48Async(destinationPath, result.Pixels,
            new(TiffBitDepth.Sixteen, IccProfile: outputIcc, Software: "光镜", Orientation: result.Pixels.Orientation), token, overwrite: false).ConfigureAwait(false);
    }

    private static HighBitDepthImageBuffer CreateProxy(HighBitDepthImageBuffer source, int maximumEdge, CancellationToken token)
    {
        var edge = Math.Max(source.Width, source.Height);
        if (edge <= maximumEdge) return source;
        var width = Math.Max(1, (int)Math.Round(source.Width * maximumEdge / (double)edge));
        var height = Math.Max(1, (int)Math.Round(source.Height * maximumEdge / (double)edge));
        var values = new float[checked(width * height * 3)];
        var original = source.ReadSamples();
        for (var y = 0; y < height; y++)
        {
            token.ThrowIfCancellationRequested();
            var sourceY = Math.Min(source.Height - 1, (int)((y + .5) * source.Height / height));
            for (var x = 0; x < width; x++)
            {
                var sourceX = Math.Min(source.Width - 1, (int)((x + .5) * source.Width / width));
                for(int c=0;c<3;c++)values[(y*width+x)*3+c]=original[(sourceY*source.Width+sourceX)*3+c];
            }
        }
        return new(width, height, values, source.SourceBitDepth, source.WorkingColorSpace, source.Orientation, source.Metadata);
    }
}
