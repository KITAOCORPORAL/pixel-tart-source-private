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
        recipe=recipe.Freeze();var palette=recipe.Profile.Palette;var plan=Plan(tileWidth,tileHeight);
        if(orientedDisplay.Orientation!=1||orientedDisplay.WorkingColorSpace!="sRGB")throw new InvalidDataException("Candidate requires an already oriented sRGB display master.");
        if(left<0||top<0||width<=0||height<=0||left+width>orientedDisplay.Width||top+height>orientedDisplay.Height)throw new ArgumentOutOfRangeException(nameof(left));
        token.ThrowIfCancellationRequested();
        // Output owned by caller; this method accounts output during construction and all halo scratch.
        var outputLease=StudioGlobalResources.ReserveWork(checked((long)width*height*3*sizeof(float)));
        try
        {
        using var haloLease=StudioGlobalResources.ReserveWork(plan.ScratchBytes);
        var result=new float[checked(width*height*3)];
        var scratch=new double[checked((tileWidth+4)*(tileHeight+4)*3)];
        for(var ty=0;ty<height;ty+=tileHeight)for(var tx=0;tx<width;tx+=tileWidth)
        {
            token.ThrowIfCancellationRequested();var tw=Math.Min(tileWidth,width-tx);var th=Math.Min(tileHeight,height-ty);var stride=(tw+4)*3;
            for(var y=-2;y<th+2;y++)
            {
                token.ThrowIfCancellationRequested();
                for(var x=-2;x<tw+2;x++)
                {
                    var sx=Math.Clamp(left+tx+x,0,orientedDisplay.Width-1);var sy=Math.Clamp(top+ty+y,0,orientedDisplay.Height-1);
                    var sourceIndex=(sy*orientedDisplay.Width+sx)*3;var input=orientedDisplay.Rgb32.Span;
                    var value=CandidateTone.ApplyLinear(new(input[sourceIndex],input[sourceIndex+1],input[sourceIndex+2]),recipe,palette);
                    var i=(y+2)*stride+(x+2)*3;scratch[i]=value.X;scratch[i+1]=value.Y;scratch[i+2]=value.Z;
                }
            }
            for(var y=0;y<th;y++)
            {
                token.ThrowIfCancellationRequested();
                for(var x=0;x<tw;x++)
                {
                    var i=(y+2)*stride+(x+2)*3;var v=new ColorVector3(scratch[i],scratch[i+1],scratch[i+2]);
                    double r=0,g=0,b=0,hr=0,hg=0,hb=0;
                    for(var dy=-2;dy<=2;dy++)for(var dx=-2;dx<=2;dx++)
                    {
                        var j=i+dy*stride+dx*3;var weight=Weights[dy+2]*Weights[dx+2]/256d;
                        var highlight=Math.Clamp((.2126*scratch[j]+.7152*scratch[j+1]+.0722*scratch[j+2]-.45)/.55,0,1);
                        r+=weight*scratch[j];g+=weight*scratch[j+1];b+=weight*scratch[j+2];
                        hr+=weight*scratch[j]*highlight;hg+=weight*scratch[j+1]*highlight;hb+=weight*scratch[j+2]*highlight;
                    }
                    var blurred=CandidateTone.Mix(v,new(r,g,b),recipe.Optics.DetailMix);
                    // Highlight-only spill is positive and restrained, not a second global blur.
                    var spread=recipe.Optics.HighlightDiffusion*(1-Math.Clamp(CandidateTone.Luma(v),0,1));
                    blurred=new(blurred.X+spread*hr,blurred.Y+spread*hg,blurred.Z+spread*hb);
                    var gx=left+tx+x;var gy=top+ty+y;
                    var textured=CandidateMaterial.Emulsion(blurred,(gx+.5)/orientedDisplay.Width,(gy+.5)/orientedDisplay.Height,recipe,palette);
                    var originalIndex=(gy*orientedDisplay.Width+gx)*3;var source=orientedDisplay.Rgb32.Span;
                    var original=new ColorVector3(source[originalIndex],source[originalIndex+1],source[originalIndex+2]);
                    // Validate original even at zero strength. Exact endpoint; no EOTF/OETF roundtrip drift.
                    CandidateTone.Decode(original);
                    original=new(Math.Clamp(original.X,0,1),Math.Clamp(original.Y,0,1),Math.Clamp(original.Z,0,1));
                    var encoded=recipe.Amount==0?original:CandidateTone.Mix(original,CandidateTone.Encode(textured),recipe.Amount);
                    var o=((ty+y)*width+tx+x)*3;result[o]=(float)encoded.X;result[o+1]=(float)encoded.Y;result[o+2]=(float)encoded.Z;
                }
            }
        }
        return CandidateImageLease.Transfer(new(width,height,result,"16","sRGB",1),outputLease);
        }
        catch{outputLease.Dispose();throw;}
    }
}
