using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using System.Text.Json;
namespace StructureScratchExperiment;
public static class SelfTests
{
    public static void Run()
    {
        var rows=new List<object>();var random=new Random(416);
        foreach(var (w,h) in new[]{(9,7),(17,13),(513,129),(1201,311),(67,515)})
        foreach(var settings in new[]{(25d,15d),(0d,15d),(25d,0d),(100d,-100d)})
        {
            var pixels=Enumerable.Range(0,w*h*3).Select(_=>(float)random.NextDouble()).ToArray();var source=new HighBitDepthImageBuffer(w,h,pixels);
            var node=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.Develop,"Structure",NumericParameters:new Dictionary<string,double>{{"structure",settings.Item1},{"detail",settings.Item2}});
            var tile=SpatialLegacyRawRenderer.RenderNode(source,node,tileWidth:127,tileHeight:61);var candidate=CandidateTiles.Render(source,node,tileWidth:127,tileHeight:61);var full=ColorStudioDevelop.Apply(source,node);
            if(!tile.Rgb32.Span.SequenceEqual(candidate.Rgb32.Span))throw new InvalidOperationException("Candidate differs from old tile");
            double maximum=0,edge=0,roi=0;for(int y=0;y<h;y++)for(int x=0;x<w;x++)for(int c=0;c<3;c++){int i=(y*w+x)*3+c;double d=Math.Abs(full.Rgb32.Span[i]-candidate.Rgb32.Span[i]);maximum=Math.Max(maximum,d);if(x==0||y==0||x==w-1||y==h-1)edge=Math.Max(edge,d);if(x>=w/4&&x<w*3/4&&y>=h/4&&y<h*3/4)roi=Math.Max(roi,d);}
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();try{CandidateTiles.Render(source,node,cancelled.Token);throw new InvalidOperationException("Cancel ignored");}catch(OperationCanceledException){}
            rows.Add(new{w,h,structure=settings.Item1,detail=settings.Item2,oldTileExact=true,fullMax=maximum,edgeMax=edge,roiMax=roi,halo=SpatialLegacyRawRenderer.Plan(source,node,127,61).Halo,cancel="CANCELLED"});
        }
        Console.WriteLine(JsonSerializer.Serialize(new{status="PASSED_OLD_TILE_EXACT",cases=rows,productionAdmission=false}));
    }
}
