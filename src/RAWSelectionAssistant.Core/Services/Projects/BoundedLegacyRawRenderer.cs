using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record FilmTileCoordinates(int Left,int Top,int FullWidth,int FullHeight);
public sealed record LegacyMemoryPlan(bool Bounded,int RowBand,long RequiredBytes,string[] FullImageNodes);

/// <summary>Exact row-band execution only for pointwise nodes (halo0); no claim that spatial nodes stream.</summary>
public static class BoundedLegacyRawRenderer
{
    public static LegacyMemoryPlan Plan(int width,int height,ReferenceLook? look,ColorAdjustmentStack? stack,PixelTartFilmSettings? film)
    {
        var state=ColorStudioEffectiveState.Resolve(look,stack,film)??(look is not null?ColorStudioLegacyMigration.Migrate(look with{Film=film??look.Film}).Stack:new ColorAdjustmentStack([new(Guid.NewGuid(),ColorStudioNodeType.Preset,"Original")]));
        var full=state.Nodes.Where(n=>n.Enabled&&(n.Type is ColorStudioNodeType.ReferenceMatch or ColorStudioNodeType.Details||n.Type==ColorStudioNodeType.Develop&&(n.NumericParameters.GetValueOrDefault("structure")!=0||n.NumericParameters.GetValueOrDefault("detail")!=0)||n.Type==ColorStudioNodeType.Film&&((n.FilmSettings?.HalationAmount??n.NumericParameters.GetValueOrDefault("halation_amount"))>0||(n.FilmSettings?.BloomAmount??n.NumericParameters.GetValueOrDefault("bloom_amount"))>0))).Select(n=>n.Name+":"+n.Type).ToArray();
        const int rows=64;var pixels=(long)width*height;
        return new(full.Length==0,rows,full.Length==0?checked(pixels*36+(long)width*Math.Min(rows,height)*(state.Graph?.Version==2?Math.Max(96,(StudioGraphRenderer.PeakBackingBuffers(state)+2)*12*4):96)+32L*1024*1024):checked(pixels*72+64L*1024*1024),full);
    }
    public static HighBitDepthImageBuffer Render(HighBitDepthImageBuffer upright,ReferenceLook? look,ColorAdjustmentStack? stack,PixelTartFilmSettings? film,CancellationToken token=default,Action<string>? stage=null)
    {
        var plan=Plan(upright.Width,upright.Height,look,stack,film);if(!plan.Bounded)throw new NotSupportedException("Full image nodes: "+string.Join(",",plan.FullImageNodes));
        var state=ColorStudioEffectiveState.Resolve(look,stack,film)??(look is not null?ColorStudioLegacyMigration.Migrate(look with{Film=film??look.Film}).Stack:new ColorAdjustmentStack([new(Guid.NewGuid(),ColorStudioNodeType.Preset,"Original")]));
        if(state.Nodes.Any(n=>n.Type==ColorStudioNodeType.ReferenceMatch))throw new NotSupportedException("Reference match requires its existing full-image analysis path");
        var masks=state.Nodes.Where(n=>n.LocalMask is not null).ToDictionary(n=>n.Id,n=>n.LocalMask!.Compile((double)upright.Width/upright.Height,token));
        var output=new float[upright.Rgb32.Length];
        Parallel.For(0,(upright.Height+plan.RowBand-1)/plan.RowBand,new ParallelOptions{CancellationToken=token,MaxDegreeOfParallelism=4},bandIndex=>
        {
            using var graphAllocation=state.Graph?.Version==2?GraphPixelAllocation.Enter():null;
            var top=bandIndex*plan.RowBand;
            token.ThrowIfCancellationRequested();var height=Math.Min(plan.RowBand,upright.Height-top);var count=upright.Width*height*3;
            using var band=new HighBitDepthImageBuffer(upright.Width,height,upright.Rgb32.Slice(top*upright.Width*3,count).ToArray(),"16","sRGB",1);
            var analysis=ColorStudioProcessingAnalysis.Create(new VisualPixelBuffer(1,1,new byte[3]),state,look,token);
            using var result=new ColorStudioRenderPipeline().Render(band,analysis,look,state,token,captureNodeDiagnostics:false,tile:new(0,top,upright.Width,upright.Height),compiledMasks:masks).ProcessingPixels!;
            result.Rgb32.Span.CopyTo(output.AsSpan(top*upright.Width*3,count));
            if(top==0||top+height==upright.Height)stage?.Invoke("RowBand:"+top+":"+height);
        });
        return new(upright.Width,upright.Height,output,"16","sRGB",1,upright.Metadata);
    }
}
