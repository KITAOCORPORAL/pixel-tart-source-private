using System.Diagnostics;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace Kitao.InstantFilm.Research;

/// <summary>One frozen full-source coordinate/seed contract for preview, native region and streamed export.</summary>
public sealed class CandidateRenderSession : IDisposable
{
    private CandidateImageLease.BorrowedImage? source;
    private ICandidatePixelSource? provider;
    private IDisposable? externalPin;
    private readonly object gate=new();
    private bool retired;private int active;
    private IDisposable RetainSource()
    {
        lock(gate){if(retired||provider is null)throw new ObjectDisposedException(nameof(CandidateRenderSession));active++;return new Active(this);}
    }
    private sealed class Active(CandidateRenderSession owner):IDisposable
    {private CandidateRenderSession? held=owner;public void Dispose(){var value=Interlocked.Exchange(ref held,null);if(value is null)return;lock(value.gate){value.active--;if(value.retired&&value.active==0)value.ReleaseSource();}}}
    private void ReleaseSource(){source?.Dispose();source=null;externalPin?.Dispose();externalPin=null;provider=null;}

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
            provider=new BufferCandidateSource(image);
            Layout=InstantPaperLayout.Create(image.Width,image.Height,new InstantFilmDefinition(true,AlgorithmVersion:2,PaperFrame:Recipe.Surface.Frame){PaperGeometry=Recipe.Geometry});
        }
        catch{source.Dispose();source=null;throw;}
    }
    public CandidateRenderSession(ICandidatePixelSource pixels,CandidateRecipe recipe,IDisposable retainedMaster)
    {
        Recipe=recipe.Freeze();provider=pixels;externalPin=retainedMaster;
        try{Layout=InstantPaperLayout.Create(pixels.Width,pixels.Height,new InstantFilmDefinition(true,AlgorithmVersion:2,PaperFrame:Recipe.Surface.Frame){PaperGeometry=Recipe.Geometry});}
        catch{retainedMaster.Dispose();provider=null;externalPin=null;throw;}
    }
    private bool TrySource(int x,int y,out int sx,out int sy)
    {
        if(!Layout.TrySource(x,y,out sx,out sy))return false;
        if(Recipe.Paper is{Version:2,Zoom:>1} paper)
        {
            var centerX=Recipe.Geometry.CropX*Layout.SourceWidth;var centerY=Recipe.Geometry.CropY*Layout.SourceHeight;
            sx=Math.Clamp((int)Math.Floor(centerX+(sx+.5-centerX)/paper.Zoom),0,Layout.SourceWidth-1);
            sy=Math.Clamp((int)Math.Floor(centerY+(sy+.5-centerY)/paper.Zoom),0,Layout.SourceHeight-1);
        }
        return true;
    }
    private (double X,double Y) SourcePosition(int x,int y)
    {
        double sx=Layout.SourceX+(x-Layout.Left+.5)/Layout.Scale-.5;
        double sy=Layout.SourceY+(y-Layout.Top+.5)/Layout.Scale-.5;
        if(Recipe.Paper is{Zoom:>1} p){double cx=Recipe.Geometry.CropX*Layout.SourceWidth-.5,cy=Recipe.Geometry.CropY*Layout.SourceHeight-.5;sx=cx+(sx-cx)/p.Zoom;sy=cy+(sy-cy)/p.Zoom;}
        return(Math.Clamp(sx,0,Layout.SourceWidth-1),Math.Clamp(sy,0,Layout.SourceHeight-1));
    }
    public CandidateImageLease RenderRegion(int left,int top,int width,int height,CancellationToken token=default)
    {
        using var activeSource=RetainSource();var image=provider??throw new ObjectDisposedException(nameof(CandidateRenderSession));
        if(left<0||top<0||width<=0||height<=0||(long)left+width>Layout.Width||(long)top+height>Layout.Height)throw new ArgumentOutOfRangeException(nameof(left));
        token.ThrowIfCancellationRequested();var lease=StudioGlobalResources.ReserveWork(checked((long)width*height*3*sizeof(float)));
        try
        {
            var result=new float[checked(width*height*3)];
            for(int ty=0;ty<height;ty+=64)for(int tx=0;tx<width;tx+=256)
            {
                token.ThrowIfCancellationRequested();var tw=Math.Min(256,width-tx);var th=Math.Min(64,height-ty);
                int minX=int.MaxValue,minY=int.MaxValue,maxX=-1,maxY=-1;
                for(int y=0;y<th;y++)for(int x=0;x<tw;x++)if(TrySource(left+tx+x,top+ty+y,out var sx,out var sy))
                {if(Recipe.Paper is{Version:3}){var pos=SourcePosition(left+tx+x,top+ty+y);minX=Math.Min(minX,(int)Math.Floor(pos.X));minY=Math.Min(minY,(int)Math.Floor(pos.Y));maxX=Math.Max(maxX,Math.Min(image.Width-1,(int)Math.Floor(pos.X)+1));maxY=Math.Max(maxY,Math.Min(image.Height-1,(int)Math.Floor(pos.Y)+1));}else{minX=Math.Min(minX,sx);minY=Math.Min(minY,sy);maxX=Math.Max(maxX,sx);maxY=Math.Max(maxY,sy);}}
                long spatialStart=CandidatePerformanceProfile.Enabled?Stopwatch.GetTimestamp():0;
                using var region=maxX>=minX?CandidateSpatial.RenderRegion(image,Recipe,minX,minY,maxX-minX+1,maxY-minY+1,token):null;
                if(spatialStart!=0)CandidatePerformanceProfile.Add("full.spatial-response-optics-texture",spatialStart);
                using var borrow=region?.Borrow();
                long paperStart=CandidatePerformanceProfile.Enabled?Stopwatch.GetTimestamp():0;
                for(int y=0;y<th;y++)for(int x=0;x<tw;x++)
                {
                    var gx=left+tx+x;var gy=top+ty+y;ColorVector3 value;
                    if(TrySource(gx,gy,out var sx,out var sy))
                    {
                     var rgb=borrow!.Image.Rgb32;var rw=borrow.Image.Width;
                     ColorVector3 Read(int xx,int yy){var k=((yy-minY)*rw+xx-minX)*3;return new(rgb.Span[k],rgb.Span[k+1],rgb.Span[k+2]);}
                     if(Recipe.Paper is{Version:3}){var pos=SourcePosition(gx,gy);int x0=(int)Math.Floor(pos.X),y0=(int)Math.Floor(pos.Y),x1=Math.Min(image.Width-1,x0+1),y1=Math.Min(image.Height-1,y0+1);value=CandidateTone.Mix(CandidateTone.Mix(Read(x0,y0),Read(x1,y0),pos.X-x0),CandidateTone.Mix(Read(x0,y1),Read(x1,y1),pos.X-x0),pos.Y-y0);}
                     else value=Read(sx,sy);
                    }
                    else value=CandidateMaterial.Paper((gx+.5)/Layout.Width,(gy+.5)/Layout.Height,Recipe);
                    if(Recipe.Paper is not null)
                    {
                        bool photo=TrySource(gx,gy,out _,out _);var nx=(gx+.5)/Layout.Width;var ny=(gy+.5)/Layout.Height;
                        if(Recipe.Paper.Version<3&&Recipe.Paper.Window=="Circle"&&photo){var u=(gx-Layout.Left+.5)/Layout.WindowWidth-.5;var v=(gy-Layout.Top+.5)/Layout.WindowHeight-.5;if(u*u+v*v>.25){photo=false;value=CandidateMaterial.Paper(nx,ny,Recipe);}}
                        if(Recipe.Paper.Version==3)
                        {
                            var distance=CandidateExtensions.WindowDistance(gx+.5,gy+.5,Layout,Recipe.Paper.Window);
                            var coverage=Math.Clamp(distance+.5,0,1);
                            value=CandidateTone.Mix(CandidateMaterial.Paper(nx,ny,Recipe),value,photo?coverage:0);
                            value=CandidateExtensions.WindowRim(value,distance,Recipe.Paper);
                        }
                        value=CandidateExtensions.Surface(value,nx,ny,Recipe,photo,Layout.Width,Layout.Height);
                    }
                    value=CandidateMaterial.Scan(value,(gx+.5)/Layout.Width,(gy+.5)/Layout.Height,Recipe);
                    var o=((ty+y)*width+tx+x)*3;result[o]=(float)value.X;result[o+1]=(float)value.Y;result[o+2]=(float)value.Z;
                }
                if(paperStart!=0)CandidatePerformanceProfile.Add("full.paper-geometry-and-scan",paperStart);
            }
            return CandidateImageLease.Transfer(new(width,height,result,"16","sRGB",1),lease);
        }
        catch{lease.Dispose();throw;}
    }
    /// <summary>Borrow is valid only within callback; retaining requires caller-owned copied/leased pixels.</summary>
    public void StreamStrips(Action<int,HighBitDepthImageBuffer> consume,int stripHeight=32,CancellationToken token=default)
    {
        using var activeStream=RetainSource();
        if(stripHeight is <1 or >64)throw new ArgumentOutOfRangeException(nameof(stripHeight));
        for(int y=0;y<Layout.Height;y+=stripHeight)
        {token.ThrowIfCancellationRequested();using var region=RenderRegion(0,y,Layout.Width,Math.Min(stripHeight,Layout.Height-y),token);using var borrow=region.Borrow();consume(y,borrow.Image);}
    }
    public CandidateImageLease RenderPreview(int maximumEdge,CancellationToken token=default)
    {
        using var activePreview=RetainSource();
        if(maximumEdge<=0)throw new ArgumentOutOfRangeException(nameof(maximumEdge));
        var ratio=Math.Min(1,maximumEdge/(double)Math.Max(Layout.Width,Layout.Height));var width=Math.Max(1,(int)Math.Round(Layout.Width*ratio));var height=Math.Max(1,(int)Math.Round(Layout.Height*ratio));
        var bytes=checked((long)width*height*3*sizeof(float));var lease=StudioGlobalResources.ReserveWork(bytes);
        try
        {
            using var scratch=StudioGlobalResources.ReserveWork(checked((long)width*height*3*sizeof(double)));
            var sum=new double[checked(width*height*3)];double rx=Layout.Width/(double)width,ry=Layout.Height/(double)height;
            long renderingStart=CandidatePerformanceProfile.Enabled?Stopwatch.GetTimestamp():0;
            StreamStrips((top,image)=>
            {
                long resizeStart=CandidatePerformanceProfile.Enabled?Stopwatch.GetTimestamp():0;
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
                if(resizeStart!=0)CandidatePerformanceProfile.Add("full.linear-area-downsample",resizeStart);
            },32,token);
            if(renderingStart!=0)CandidatePerformanceProfile.Add("full.preview-total-including-downsample",renderingStart);
            var output=new float[sum.Length];for(int i=0;i<output.Length;i++)output[i]=(float)CandidateTone.Encode(sum[i]/(rx*ry));
            return CandidateImageLease.Transfer(new(width,height,output,"16","sRGB",1),lease);
        }
        catch{lease.Dispose();throw;}
    }
    public void Dispose(){lock(gate){if(retired)return;retired=true;if(active==0)ReleaseSource();}}
}
