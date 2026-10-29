using System.Diagnostics;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace Kitao.InstantFilm.Research;

public sealed record CandidateSpatialPlan(int Halo,int MaximumTileWidth,int MaximumTileHeight,long ScratchBytes,string Boundary,string Domain);
/// <summary>Finite-support spatial stage. Halo scratch only, no unbudgeted full-frame intermediate.</summary>
public static class CandidateSpatial
{
    private static readonly int[] Weights=[1,4,6,4,1];
    public static CandidateSpatialPlan Plan(int tileWidth=256,int tileHeight=64)
    {
        if(tileWidth is <1 or >512||tileHeight is <1 or >128)throw new ArgumentOutOfRangeException(nameof(tileWidth));
        return new(2,tileWidth,tileHeight,checked((long)(tileWidth+4)*(tileHeight+4)*3*sizeof(double)),"ClampToSourceEdge","LinearSrgbD65AfterCreativeResponse");
    }
    public static CandidateImageLease RenderRegion(CandidateImageLease source,CandidateRecipe recipe,
        int left,int top,int width,int height,CancellationToken token=default,int tileWidth=256,int tileHeight=64)
    {
        using var borrow=source.Borrow();
        return RenderRegion(borrow.Image,recipe,left,top,width,height,token,tileWidth,tileHeight);
    }
    public static CandidateImageLease RenderRegion(HighBitDepthImageBuffer orientedDisplay,CandidateRecipe recipe,
        int left,int top,int width,int height,CancellationToken token=default,int tileWidth=256,int tileHeight=64)
    {
        if(orientedDisplay.Orientation!=1||orientedDisplay.WorkingColorSpace!="sRGB")throw new InvalidDataException("Candidate requires an already oriented sRGB display master.");
        return RenderRegion(new BufferCandidateSource(orientedDisplay),recipe,left,top,width,height,token,tileWidth,tileHeight);
    }
    public static CandidateImageLease RenderRegion(ICandidatePixelSource orientedDisplay,CandidateRecipe recipe,
        int left,int top,int width,int height,CancellationToken token=default,int tileWidth=256,int tileHeight=64)
    {
        bool profile=CandidatePerformanceProfile.Enabled;
        recipe=recipe.Freeze();var palette=recipe.Profile.Palette;var plan=Plan(tileWidth,tileHeight);
        var womb=recipe.ColorComponent is null&&recipe.Creative is{Version:2,Stock:not "Base"};
        var toneRecipe=womb?recipe with{Response=recipe.Response with{ExposureEv=0}}:recipe;
        var exposureGain=Math.Pow(2,recipe.Response.ExposureEv);
        if(left<0||top<0||width<=0||height<=0||left+width>orientedDisplay.Width||top+height>orientedDisplay.Height)throw new ArgumentOutOfRangeException(nameof(left));
        CandidateProcessingEvidence.Enter("PixelRender",token);
        // Output owned by caller; this method accounts output during construction and all halo scratch.
        var outputLease=StudioGlobalResources.ReserveWork(checked((long)width*height*3*sizeof(float)));
        try
        {
        using var haloLease=StudioGlobalResources.ReserveWork(checked(plan.ScratchBytes*2));
        var result=new float[checked(width*height*3)];
        var scratch=new double[checked((tileWidth+4)*(tileHeight+4)*3)];
        // Exact encoded source values already fetched for halo response. Reuse for the
        // original endpoint; no sampling/arithmetic/rounding changes or cross-tile cache.
        var encodedSource=new ColorVector3[checked((tileWidth+4)*(tileHeight+4))];
        for(var ty=0;ty<height;ty+=tileHeight)for(var tx=0;tx<width;tx+=tileWidth)
        {
            token.ThrowIfCancellationRequested();var tw=Math.Min(tileWidth,width-tx);var th=Math.Min(tileHeight,height-ty);var stride=(tw+4)*3;
            for(var y=-2;y<th+2;y++)
            {
                token.ThrowIfCancellationRequested();
                for(var x=-2;x<tw+2;x++)
                {
                    var sx=Math.Clamp(left+tx+x,0,orientedDisplay.Width-1);var sy=Math.Clamp(top+ty+y,0,orientedDisplay.Height-1);
                    long sampled=profile&&x==0?Stopwatch.GetTimestamp():0;
                    var sourcePixel=orientedDisplay.Read(sx,sy);
                    encodedSource[(y+2)*(tw+4)+x+2]=sourcePixel;
                    if(sampled!=0){CandidatePerformanceProfile.Add("sampled.double-source-fetch-interpolate",sampled);sampled=Stopwatch.GetTimestamp();}
                    if(recipe.Creative?.LensVignette is >0){var radial=Math.Pow((sx+.5)/orientedDisplay.Width-.5,2)+Math.Pow((sy+.5)/orientedDisplay.Height-.5,2);var linear=CandidateTone.Decode(sourcePixel);var gain=1-recipe.Creative.LensVignette*Math.Clamp(radial,0,.5);sourcePixel=CandidateTone.Encode(new ColorVector3(linear.X*gain,linear.Y*gain,linear.Z*gain));}
                    // Creative2 makes Womb a colour node rather than a late replacement.
                    // The standalone formula remains encoded-domain; exposure is linear and precedes it.
                    if(womb&&recipe.Creative is{} creative)
                    {
                        var exposed=CandidateTone.Decode(sourcePixel);var gain=exposureGain;
                        sourcePixel=CandidateTone.Encode(new ColorVector3(exposed.X*gain,exposed.Y*gain,exposed.Z*gain));
                        sourcePixel=Womb01351Reference.Apply(sourcePixel,creative.Stock);
                    }
                    var value=recipe.ColorComponent is{} component?InstantColorComponents.ApplyLinear(sourcePixel,component,recipe.Response.ExposureEv):CandidateTone.ApplyLinear(sourcePixel,toneRecipe,palette);
                    if(sampled!=0)CandidatePerformanceProfile.Add("sampled.tone-response",sampled);
                    var i=(y+2)*stride+(x+2)*3;scratch[i]=value.X;scratch[i+1]=value.Y;scratch[i+2]=value.Z;
                }
            }
            for(var y=0;y<th;y++)
            {
                token.ThrowIfCancellationRequested();
                for(var x=0;x<tw;x++)
                {
                    var i=(y+2)*stride+(x+2)*3;var v=new ColorVector3(scratch[i],scratch[i+1],scratch[i+2]);
                    long sampled=profile&&x==0?Stopwatch.GetTimestamp():0;
                    double r=0,g=0,b=0,hr=0,hg=0,hb=0;
                    for(var dy=-2;dy<=2;dy++)for(var dx=-2;dx<=2;dx++)
                    {
                        var j=i+dy*stride+dx*3;var weight=Weights[dy+2]*Weights[dx+2]/256d;
                        var highlight=Math.Clamp((.2126*scratch[j]+.7152*scratch[j+1]+.0722*scratch[j+2]-.45)/.55,0,1);
                        r+=weight*scratch[j];g+=weight*scratch[j+1];b+=weight*scratch[j+2];
                        hr+=weight*scratch[j]*highlight;hg+=weight*scratch[j+1]*highlight;hb+=weight*scratch[j+2]*highlight;
                    }
                    if(sampled!=0){CandidatePerformanceProfile.Add("sampled.optics-25tap",sampled);sampled=Stopwatch.GetTimestamp();}
                    var blurred=CandidateTone.Mix(v,new(r,g,b),recipe.Optics.DetailMix);
                    // Highlight-only spill is positive and restrained, not a second global blur.
                    var spread=recipe.Optics.HighlightDiffusion*(1-Math.Clamp(CandidateTone.Luma(v),0,1));
                    blurred=new(blurred.X+spread*hr,blurred.Y+spread*hg,blurred.Z+spread*hb);
                    var gx=left+tx+x;var gy=top+ty+y;
                    long materialSample=sampled!=0?Stopwatch.GetTimestamp():0;
                    var textured=CandidateMaterial.Emulsion(blurred,(gx+.5)/orientedDisplay.Width,(gy+.5)/orientedDisplay.Height,recipe,palette);
                    if(materialSample!=0){CandidatePerformanceProfile.Add("sampled.emulsion-noise",materialSample);materialSample=Stopwatch.GetTimestamp();}
                    var original=encodedSource[(y+2)*(tw+4)+x+2];
                    // Validate original even at zero strength. Exact endpoint; no EOTF/OETF roundtrip drift.
                    CandidateTone.Decode(original);
                    original=new(Math.Clamp(original.X,0,1),Math.Clamp(original.Y,0,1),Math.Clamp(original.Z,0,1));
                    var developed=CandidateTone.Encode(textured);
                    if(recipe.Creative is{Version:1,Stock:not "Base"} oldCreative && oldCreative.Stock is{} stock)developed=Womb01351Reference.Apply(original,stock);
                    developed=CandidateExtensions.Creative(developed,(gx+.5)/orientedDisplay.Width,(gy+.5)/orientedDisplay.Height,recipe,palette);
                    developed=CandidateExtensions.Defects(developed,(gx+.5)/orientedDisplay.Width,(gy+.5)/orientedDisplay.Height,recipe,palette);
                    if(materialSample!=0)CandidatePerformanceProfile.Add("sampled.original-fetch-creative-defects",materialSample);
                    var encoded=recipe.Amount==0?original:CandidateTone.Mix(original,developed,recipe.Amount);
                    if(sampled!=0)CandidatePerformanceProfile.Add("sampled.texture-creative-and-original",sampled);
                    var o=((ty+y)*width+tx+x)*3;result[o]=(float)encoded.X;result[o+1]=(float)encoded.Y;result[o+2]=(float)encoded.Z;
                }
            }
        }
        return CandidateImageLease.Transfer(new(width,height,result,"16","sRGB",1),outputLease);
        }
        catch{outputLease.Dispose();throw;}
    }
}
