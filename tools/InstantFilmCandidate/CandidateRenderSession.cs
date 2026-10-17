using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace Kitao.InstantFilm.Research;

/// <summary>One frozen full-source coordinate/seed contract for preview, native region and streamed export.</summary>
public sealed class CandidateRenderSession : IDisposable
{
    private CandidateImageLease.BorrowedImage? source;
    private readonly object gate=new();
    private CandidateImageLease.BorrowedImage RetainSource(){lock(gate)return source?.Retain()??throw new ObjectDisposedException(nameof(CandidateRenderSession));}
    public CandidateRecipe Recipe { get; }
    public InstantPaperLayout Layout { get; }
    public const string Coordinates="FullOrientedSourcePixelsRadius2_NormalizedFullCanvasNoise_AreaPreviewAfterFullEffectV1";
    public CandidateRenderSession(CandidateImageLease retainedSource,CandidateRecipe recipe)
    {
        Recipe=recipe.Freeze();source=retainedSource.Borrow();
        try
        {
            var image=source.Image;
            if(image.Orientation!=1||image.WorkingColorSpace!="sRGB")throw new InvalidDataException("Already oriented sRGB source required");
            Layout=InstantPaperLayout.Create(image.Width,image.Height,new InstantFilmDefinition(true,AlgorithmVersion:2,PaperFrame:Recipe.Surface.Frame){PaperGeometry=Recipe.Geometry});
        }
        catch{source.Dispose();source=null;throw;}
    }
    public CandidateImageLease RenderRegion(int left,int top,int width,int height,CancellationToken token=default)
    {
        using var activeSource=RetainSource();var image=activeSource.Image;
        if(left<0||top<0||width<=0||height<=0||(long)left+width>Layout.Width||(long)top+height>Layout.Height)throw new ArgumentOutOfRangeException(nameof(left));
        token.ThrowIfCancellationRequested();var lease=StudioGlobalResources.ReserveWork(checked((long)width*height*3*sizeof(float)));
        try
        {
            var result=new float[checked(width*height*3)];
            for(int ty=0;ty<height;ty+=64)for(int tx=0;tx<width;tx+=256)
            {
                token.ThrowIfCancellationRequested();var tw=Math.Min(256,width-tx);var th=Math.Min(64,height-ty);
                int minX=int.MaxValue,minY=int.MaxValue,maxX=-1,maxY=-1;
                for(int y=0;y<th;y++)for(int x=0;x<tw;x++)if(Layout.TrySource(left+tx+x,top+ty+y,out var sx,out var sy))
                {minX=Math.Min(minX,sx);minY=Math.Min(minY,sy);maxX=Math.Max(maxX,sx);maxY=Math.Max(maxY,sy);}
                using var region=maxX>=minX?CandidateSpatial.RenderRegion(image,Recipe,minX,minY,maxX-minX+1,maxY-minY+1,token):null;
                using var borrow=region?.Borrow();
                for(int y=0;y<th;y++)for(int x=0;x<tw;x++)
                {
                    var gx=left+tx+x;var gy=top+ty+y;ColorVector3 value;
                    if(Layout.TrySource(gx,gy,out var sx,out var sy))
                    {var p=((sy-minY)*borrow!.Image.Width+sx-minX)*3;var rgb=borrow.Image.Rgb32.Span;value=new(rgb[p],rgb[p+1],rgb[p+2]);}
                    else value=CandidateMaterial.Paper((gx+.5)/Layout.Width,(gy+.5)/Layout.Height,Recipe);
                    value=CandidateMaterial.Scan(value,(gx+.5)/Layout.Width,(gy+.5)/Layout.Height,Recipe);
                    var o=((ty+y)*width+tx+x)*3;result[o]=(float)value.X;result[o+1]=(float)value.Y;result[o+2]=(float)value.Z;
                }
            }
            return CandidateImageLease.Transfer(new(width,height,result,"16","sRGB",1),lease);
        }
        catch{lease.Dispose();throw;}
    }
    /// <summary>Borrow is valid only within callback; retaining requires caller-owned copied/leased pixels.</summary>
    public void StreamStrips(Action<int,HighBitDepthImageBuffer> consume,int stripHeight=32,CancellationToken token=default)
    {
        if(stripHeight is <1 or >64)throw new ArgumentOutOfRangeException(nameof(stripHeight));
        for(int y=0;y<Layout.Height;y+=stripHeight)
        {token.ThrowIfCancellationRequested();using var region=RenderRegion(0,y,Layout.Width,Math.Min(stripHeight,Layout.Height-y),token);using var borrow=region.Borrow();consume(y,borrow.Image);}
    }
    public CandidateImageLease RenderPreview(int maximumEdge,CancellationToken token=default)
    {
        if(maximumEdge<=0)throw new ArgumentOutOfRangeException(nameof(maximumEdge));
        var ratio=Math.Min(1,maximumEdge/(double)Math.Max(Layout.Width,Layout.Height));var width=Math.Max(1,(int)Math.Round(Layout.Width*ratio));var height=Math.Max(1,(int)Math.Round(Layout.Height*ratio));
        var bytes=checked((long)width*height*3*sizeof(float));var lease=StudioGlobalResources.ReserveWork(bytes);
        try
        {
            using var scratch=StudioGlobalResources.ReserveWork(checked((long)width*height*3*sizeof(double)));
            var sum=new double[checked(width*height*3)];double rx=Layout.Width/(double)width,ry=Layout.Height/(double)height;
            StreamStrips((top,image)=>
            {
                for(int y=0;y<image.Height;y++)
                {
                    token.ThrowIfCancellationRequested();int sy=top+y;
                    for(int py=(int)(sy/ry);py<=Math.Min(height-1,(int)((sy+1)/ry));py++)
                    {
                        var wy=Math.Max(0,Math.Min(sy+1,(py+1)*ry)-Math.Max(sy,py*ry));if(wy==0)continue;
                        for(int sx=0;sx<image.Width;sx++)for(int px=(int)(sx/rx);px<=Math.Min(width-1,(int)((sx+1)/rx));px++)
                        {var wx=Math.Max(0,Math.Min(sx+1,(px+1)*rx)-Math.Max(sx,px*rx));var p=(y*image.Width+sx)*3;var o=(py*width+px)*3;for(int c=0;c<3;c++)sum[o+c]+=CandidateTone.Decode(image.Rgb32.Span[p+c])*wx*wy;}
                    }
                }
            },32,token);
            var output=new float[sum.Length];for(int i=0;i<output.Length;i++)output[i]=(float)CandidateTone.Encode(sum[i]/(rx*ry));
            return CandidateImageLease.Transfer(new(width,height,output,"16","sRGB",1),lease);
        }
        catch{lease.Dispose();throw;}
    }
    public void Dispose(){lock(gate){source?.Dispose();source=null;}}
}
