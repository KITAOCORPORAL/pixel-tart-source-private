using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
namespace RAWSelectionAssistant.Core.Services.Projects;

public sealed record SpatialNodePlan(string Node,int Halo,int TileWidth,int TileHeight,long RequiredBytes,bool GlobalAnalysis,string Boundary);
/// <summary>Explicit SpatialBandsV2 contract. Full-frame statistics for match; finite-support neighbors for spatial nodes.</summary>
public static class SpatialLegacyRawRenderer
{
    public const string Contract="LegacyRawSpatialBandsV2";
    public static SpatialNodePlan Plan(HighBitDepthImageBuffer source,ColorAdjustmentStackNode node,int tileWidth=512,int tileHeight=128)
    {
        var scale=Math.Max(source.Width,source.Height)/1600d;var min=Math.Min(source.Width,source.Height);
        double P(string key)=>ColorStudioToolCatalog.Value(node,key);
        var halo=node.Type switch
        {
            ColorStudioNodeType.Details=>(int)Math.Ceiling(Math.Max(2*Math.Max(.1,scale),0)+Math.Max((P("radius")+.5)*scale,20.5*scale))+3,
            ColorStudioNodeType.Develop=>Math.Max(node.NumericParameters.GetValueOrDefault("structure")!=0?Math.Max(1,(int)Math.Round(min*.01)):0,node.NumericParameters.GetValueOrDefault("detail")!=0?1:0),
            ColorStudioNodeType.Film=>FilmHalo(Film(node),min),
            _=>0
        };
        var tw=Math.Min(source.Width,tileWidth+2*halo);var th=Math.Min(source.Height,tileHeight+2*halo);
        // Source/current/result at most three float RGB frames; tile scratch is conservative for OKLab double arrays.
        var bytes=checked((long)source.Width*source.Height*36+(long)tw*th*(node.Type==ColorStudioNodeType.Details?1600:160)+64L*1024*1024);
        return new(node.Name,halo,tileWidth,tileHeight,bytes,node.Type==ColorStudioNodeType.ReferenceMatch,"Global edge clamp; legacy Develop truncated running-sum edge; interior crop excludes artificial tile boundary");
    }
    private static int FilmHalo(PixelTartFilmSettings film,int min)
    {
        if(film.HalationAmount==0&&film.BloomAmount==0)return 0;
        if(film.SpatialVersion==1){var radius=Math.Clamp(min/320,2,8);return Math.Max(film.BloomAmount>0?radius:0,film.HalationAmount>0?Math.Max(2,radius/2):0);}
        return Math.Max(film.BloomAmount>0?(int)Math.Ceiling(Math.Max(.25,min/320d*.55)*3):0,film.HalationAmount>0?(int)Math.Ceiling(Math.Max(.25,min/640d*.55)*3):0);
    }
    internal static PixelTartFilmSettings Film(ColorAdjustmentStackNode n)=>n.FilmSettings is {} f?f with{Enabled=true}:new(true,"PT-W01",n.NumericParameters.GetValueOrDefault("profile_amount",70),n.NumericParameters.GetValueOrDefault("grain_amount"),n.NumericParameters.GetValueOrDefault("grain_size",35),n.NumericParameters.GetValueOrDefault("halation_amount"),n.NumericParameters.GetValueOrDefault("bloom_amount"),n.NumericParameters.GetValueOrDefault("vignette_amount"));
    public static HighBitDepthImageBuffer RenderNode(HighBitDepthImageBuffer source,ColorAdjustmentStackNode node,ReferenceLook? look=null,CancellationToken token=default,Action<string>? stage=null,int tileWidth=512,int tileHeight=128)
    {
        if(tileWidth<=0||tileHeight<=0)throw new ArgumentOutOfRangeException(nameof(tileWidth));
        if(source.Orientation!=1||source.WorkingColorSpace!="sRGB")throw new InvalidDataException("Spatial contract requires upright rendered sRGB.");
        node.Normalize();var plan=Plan(source,node,tileWidth,tileHeight);using var admission=StudioGlobalResources.ReserveWork(plan.RequiredBytes);token.ThrowIfCancellationRequested();
        if(node.Type==ColorStudioNodeType.ReferenceMatch){var effect=Reference(source,node,look??throw new InvalidDataException("Reference Match requires frozen look."),token,stage);return StudioLayerComposite.Apply(source,effect,node,token);}
        var output=new float[source.Rgb32.Length];var halo=plan.Halo;
        var detailsWorkspaces=new Dictionary<(int,int),ColorStudioToolProcessor.DetailsTileWorkspace>();
        for(var y=0;y<source.Height;y+=tileHeight)for(var x=0;x<source.Width;x+=tileWidth)
        {
            token.ThrowIfCancellationRequested();var cw=Math.Min(tileWidth,source.Width-x);var ch=Math.Min(tileHeight,source.Height-y);
            var left=Math.Max(0,x-halo);var top=Math.Max(0,y-halo);var right=Math.Min(source.Width,x+cw+halo);var bottom=Math.Min(source.Height,y+ch+halo);
            var w=right-left;var h=bottom-top;var rgb=new float[w*h*3];for(var row=0;row<h;row++)source.Rgb32.Span.Slice(((top+row)*source.Width+left)*3,w*3).CopyTo(rgb.AsSpan(row*w*3));
            var input=new HighBitDepthImageBuffer(w,h,rgb,source.SourceBitDepth,source.WorkingColorSpace,1);
            ColorStudioToolProcessor.DetailsTileWorkspace? scratch=null;if(node.Type==ColorStudioNodeType.Details&&!detailsWorkspaces.TryGetValue((w,h),out scratch)){scratch=new(w,h);detailsWorkspaces[(w,h)]=scratch;}
            var uncomposited=node with{CompositeVersion=0,Opacity=1,Blend=StudioLayerBlend.Normal,LocalMask=null};
            var rendered=node.Type switch
            {
                ColorStudioNodeType.Details=>ColorStudioToolProcessor.ApplyDetails(input,node,token,source.Width,source.Height,scratch),
                ColorStudioNodeType.Develop=>ColorStudioDevelop.ApplyRegion(input,node,source.Width,source.Height,token),
                ColorStudioNodeType.Film=>PixelTartFilmPipeline.ApplyCore(input,Film(node),token,left,top,source.Width,source.Height),
                _=>new ColorStudioRenderPipeline().Render(input,ColorStudioProcessingAnalysis.Create(new VisualPixelBuffer(1,1,new byte[3]),new([node]),look,token),look,new([uncomposited]),token,captureNodeDiagnostics:false,tile:new(left,top,source.Width,source.Height)).ProcessingPixels!
            };
            rendered=StudioLayerComposite.Apply(input,rendered,node,token,new(left,top,source.Width,source.Height));
            for(var row=0;row<ch;row++)rendered.Rgb32.Span.Slice(((y-top+row)*w+x-left)*3,cw*3).CopyTo(output.AsSpan(((y+row)*source.Width+x)*3));
            stage?.Invoke("Tile:"+node.Type+":"+x+":"+y);
        }
        return new(source.Width,source.Height,output,source.SourceBitDepth,source.WorkingColorSpace,source.Orientation,source.Metadata);
    }
    private static HighBitDepthImageBuffer Reference(HighBitDepthImageBuffer source,ColorAdjustmentStackNode node,ReferenceLook look,CancellationToken token,Action<string>? stage)
    {
        double P(string name,double fallback)=>node.NumericParameters.GetValueOrDefault(name,fallback);
        var p=look.Parameters with{MatchStrength=P("match_strength",look.Parameters.MatchStrength),ToneStrength=P("tone_strength",look.Parameters.ToneStrength),ColorStrength=P("color_strength",look.Parameters.ColorStrength),ContrastStrength=P("contrast_strength",look.Parameters.ContrastStrength),SaturationStrength=P("saturation_strength",look.Parameters.SaturationStrength),KeepOriginalTone=P("keep_original_tone",look.Parameters.KeepOriginalTone?1:0)>=.5,SkinProtection=P("skin_protection",look.Parameters.SkinProtection),HighlightProtection=P("highlight_protection",look.Parameters.HighlightProtection),NeutralProtection=P("neutral_protection",look.Parameters.NeutralProtection)};
        if(p.MatchStrength==0)return source.Clone();
        var frozen=look with{Parameters=p};var stack=new ColorAdjustmentStack([node]);var analysis=ColorStudioProcessingAnalysis.Create(source.ToVisualRgb24(token),stack,look,token);
        stage?.Invoke("GlobalStatistics");var transform=new ReferenceLookMatcher().BuildTransformBounded(source,analysis,frozen,token);var output=new float[source.Rgb32.Length];
        for(var y=0;y<source.Height;y++){token.ThrowIfCancellationRequested();for(var x=0;x<source.Width;x++){var i=(y*source.Width+x)*3;var value=transform.ApplyFloat(source.Rgb32.Span[i],source.Rgb32.Span[i+1],source.Rgb32.Span[i+2]);output[i]=value.R;output[i+1]=value.G;output[i+2]=value.B;}}return new(source.Width,source.Height,output,source.SourceBitDepth,source.WorkingColorSpace,source.Orientation,source.Metadata);
    }
    public static HighBitDepthImageBuffer Render(HighBitDepthImageBuffer source,ReferenceLook? look,ColorAdjustmentStack? stack,PixelTartFilmSettings? film,CancellationToken token=default,Action<string>? stage=null)
    {
        var state=ColorStudioEffectiveState.Resolve(look,stack,film)??(look is null?new([new(Guid.NewGuid(),ColorStudioNodeType.Preset,"Original")]):ColorStudioLegacyMigration.Migrate(look with{Film=film??look.Film}).Stack);
        if(state.Graph?.Version==2)return StudioGraphRenderer.Render(source,state,(input,node)=>RenderNode(input,node with{CompositeVersion=0,Opacity=1,Blend=StudioLayerBlend.Normal,LocalMask=null},look,token,stage),token);
        var current=source;foreach(var node in StudioGraphRouting.Nodes(state).Where(n=>n.Enabled)){token.ThrowIfCancellationRequested();current=RenderNode(current,node,look,token,stage);}return current;
    }
}
