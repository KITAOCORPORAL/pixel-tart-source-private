using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace StructureScratchExperiment;
public static class CandidateTiles
{
    public static long LastOwnedScratchBytes {get;private set;}
    private sealed class Workspace(int width,int height)
    {public readonly float[] Input=new float[width*height*3];public readonly OwnedDevelopScratch.Scratch Develop=new(width*height);public long Bytes=>(long)width*height*12+Develop.Bytes;}
    public static HighBitDepthImageBuffer Render(HighBitDepthImageBuffer source,ColorAdjustmentStackNode node,CancellationToken token=default,int tileWidth=512,int tileHeight=128,Action<string>? stage=null)
    {
        LastOwnedScratchBytes=0;if(tileWidth<=0||tileHeight<=0)throw new ArgumentOutOfRangeException();
        node.Normalize();var plan=SpatialLegacyRawRenderer.Plan(source,node,tileWidth,tileHeight);
        using var admission=StudioGlobalResources.ReserveWork(plan.RequiredBytes);token.ThrowIfCancellationRequested();
        var output=new float[source.Rgb32.Length];var scratch=new Dictionary<(int,int),Workspace>();
        try
        {
            for(int y=0;y<source.Height;y+=tileHeight)for(int x=0;x<source.Width;x+=tileWidth)
            {
                token.ThrowIfCancellationRequested();int cw=Math.Min(tileWidth,source.Width-x),ch=Math.Min(tileHeight,source.Height-y);
                int left=Math.Max(0,x-plan.Halo),top=Math.Max(0,y-plan.Halo),right=Math.Min(source.Width,x+cw+plan.Halo),bottom=Math.Min(source.Height,y+ch+plan.Halo),w=right-left,h=bottom-top;
                if(!scratch.TryGetValue((w,h),out var workspace)){workspace=new(w,h);scratch[(w,h)]=workspace;LastOwnedScratchBytes+=workspace.Bytes;}
                for(int row=0;row<h;row++)source.Rgb32.Span.Slice(((top+row)*source.Width+left)*3,w*3).CopyTo(workspace.Input.AsSpan(row*w*3));
                var input=new HighBitDepthImageBuffer(w,h,workspace.Input,source.SourceBitDepth,source.WorkingColorSpace,1);
                var rendered=OwnedDevelopScratch.ApplyRegion(input,node,source.Width,source.Height,token,workspace.Develop);
                for(int row=0;row<ch;row++)rendered.Rgb32.Span.Slice(((y-top+row)*w+x-left)*3,cw*3).CopyTo(output.AsSpan(((y+row)*source.Width+x)*3));
            }
            return new(source.Width,source.Height,output,source.SourceBitDepth,source.WorkingColorSpace,source.Orientation,source.Metadata);
        }
        finally{scratch.Clear();}
    }
}
