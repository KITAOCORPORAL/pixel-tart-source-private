using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Graph2: ordered opaque adjustment-result RGB mixing. Source alpha belongs to caller, unchanged.
/// Encoded sRGB D65 only; use existing StudioLayerComposite (base then layer, opacity*mask).
/// No implicit averaging or linear-wide conversion. Missing required route yields source identity.</summary>
public static class StudioGraphRenderer
{
    public static HighBitDepthImageBuffer Render(HighBitDepthImageBuffer source,ColorAdjustmentStack stack,
        Func<HighBitDepthImageBuffer,ColorAdjustmentStackNode,HighBitDepthImageBuffer> effect,CancellationToken token=default,
        FilmTileCoordinates? tile=null,Func<int,int,(double X,double Y)>? sourceCoordinate=null,
        IReadOnlyDictionary<Guid,Func<double,double,double>>? masks=null,Action<Guid,HighBitDepthImageBuffer,HighBitDepthImageBuffer>? diagnostics=null,Action<Metrics>? observed=null,bool allowBands=true)
    {
        stack=stack.Normalize();var graph=stack.Graph??throw new ArgumentException("Graph missing");
        if(source.WorkingColorSpace!="sRGB")throw new NotSupportedException("并行图仅接受既有显示sRGB；线性宽域不隐式变换。");
        var route=graph.RoutedIds(stack.Nodes);if(!graph.GroupEnabled||graph.GroupAmount==0||route.Count==0)return source.Clone();
        if(allowBands&&diagnostics is null&&tile is null&&sourceCoordinate is null&&source.PixelCount>2000000&&CanBand(stack,route))
            return RenderBands(source,stack,effect,token,masks,observed);
        var byId=stack.Nodes.ToDictionary(n=>n.Id);
        var cache=new Dictionary<Guid,HighBitDepthImageBuffer>{{StudioNodeGraph.Source,source}};
        var uses=Consumers(graph,route);int peak=0,calls=0,released=0;
        using var budget=StudioGlobalResources.ReserveWork(EstimateWorkBytes(source,stack));
        void ReleaseConsumer(Guid id)
        {
            if(--uses[id]!=0||id==StudioNodeGraph.Source)return;
            if(cache.Remove(id,out var owned)){owned.Dispose();released++;}
        }
        try
        {
            foreach(var id in route)
            {
                token.ThrowIfCancellationRequested();var node=byId[id];var incoming=graph.Edges.Where(e=>e.To==id).ToArray();
                var input=cache[incoming.Single(e=>e.Input==(node.Type==ColorStudioNodeType.GraphMix?"base":"rgb")).From];
                HighBitDepthImageBuffer? next=null,rendered=null;
                try
                {
                    if(!node.Enabled)next=input.Clone();
                    else if(node.Type==ColorStudioNodeType.GraphMix)
                    {
                        var layer=cache[incoming.Single(e=>e.Input=="layer").From];
                        next=StudioLayerComposite.Apply(input,layer,node,token,tile,sourceCoordinate,masks?.GetValueOrDefault(id));
                        if(ReferenceEquals(next,layer)||ReferenceEquals(next,input))next=next.Clone();
                    }
                    else
                    {
                        calls++;rendered=effect(input,node);
                        next=StudioLayerComposite.Apply(input,rendered,node,token,tile,sourceCoordinate,masks?.GetValueOrDefault(id));
                        if(cache.Values.Any(v=>ReferenceEquals(v,next)))next=next.Clone();
                        if(!ReferenceEquals(next,rendered)&&!cache.Values.Any(v=>ReferenceEquals(v,rendered)))rendered.Dispose();
                        rendered=null;
                    }
                    cache.Add(id,next);next=null;peak=Math.Max(peak,cache.Count-1);
                    diagnostics?.Invoke(id,input,cache[id]);
                }
                finally
                {
                    next?.Dispose();if(rendered is not null&&!cache.Values.Any(v=>ReferenceEquals(v,rendered)))rendered.Dispose();
                }
                foreach(var edge in incoming)ReleaseConsumer(edge.From);
            }
            var finalId=graph.Edges.Single(e=>e.To==StudioNodeGraph.Output).From;var final=cache[finalId];
            token.ThrowIfCancellationRequested();
            if(graph.GroupAmount==1)
            {
                // Transfer the last owned buffer, not a redundant full-image copy.
                if(finalId==StudioNodeGraph.Source)return source.Clone();cache.Remove(finalId);return final;
            }
            var group=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"专业组"){CompositeVersion=1,Opacity=graph.GroupAmount};
            return StudioLayerComposite.Apply(source,final,group,token);
        }
        finally
        {
            foreach(var pair in cache)if(pair.Key!=StudioNodeGraph.Source)pair.Value.Dispose();
            observed?.Invoke(new(route.Count,calls,peak,released));
        }
    }
    private static bool CanBand(ColorAdjustmentStack stack,IReadOnlyList<Guid> route)
    {
        var ids=route.ToHashSet();return stack.Nodes.Where(n=>ids.Contains(n.Id)).All(n=>n.Type is ColorStudioNodeType.GraphMix or ColorStudioNodeType.BasicTone or ColorStudioNodeType.WhiteBalance or ColorStudioNodeType.Levels or ColorStudioNodeType.Curve or ColorStudioNodeType.ColorBalance or ColorStudioNodeType.SkinTone);
    }
    private static HighBitDepthImageBuffer RenderBands(HighBitDepthImageBuffer source,ColorAdjustmentStack stack,Func<HighBitDepthImageBuffer,ColorAdjustmentStackNode,HighBitDepthImageBuffer> effect,CancellationToken token,IReadOnlyDictionary<Guid,Func<double,double,double>>? masks,Action<Metrics>? observed)
    {
        // Halo0 only: supported tools are pointwise. Mask coordinates stay in the full upright source.
        using var budget=StudioGlobalResources.ReserveWork(checked((long)source.Rgb32.Length*4));
        var output=new float[source.Rgb32.Length];var compiled=stack.Nodes.Where(n=>n.LocalMask is not null).ToDictionary(n=>n.Id,n=>masks?.GetValueOrDefault(n.Id)??n.LocalMask!.Compile((double)source.Width/source.Height,token));
        const int rows=64;int calls=0,peak=0,released=0;var route=stack.Graph!.RoutedIds(stack.Nodes);
        try { Parallel.For(0,(source.Height+rows-1)/rows,new ParallelOptions{CancellationToken=token,MaxDegreeOfParallelism=4},band=>
        {
            int top=band*rows,height=Math.Min(rows,source.Height-top);token.ThrowIfCancellationRequested();
            using var input=new HighBitDepthImageBuffer(source.Width,height,source.Rgb32.Slice(top*source.Width*3,height*source.Width*3).ToArray(),source.SourceBitDepth,"sRGB",1);
            using var result=Render(input,stack,effect,token,new(0,top,source.Width,source.Height),masks:compiled,observed:m=>{Interlocked.Add(ref calls,m.EffectCalls);Interlocked.Add(ref released,m.ReleasedBeforeReturn);lock(output)peak=Math.Max(peak,m.PeakRetainedNodeBuffers);},allowBands:false);
            result.Rgb32.Span.CopyTo(output.AsSpan(top*source.Width*3,height*source.Width*3));
        });
        } catch(AggregateException error)
        {
            // Surface the actual effect failure to the established product IO/error boundary.
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.Flatten().InnerExceptions[0]).Throw();throw;
        }
        observed?.Invoke(new(route.Count,calls,peak,released));return new(source.Width,source.Height,output,source.SourceBitDepth,"sRGB",source.Orientation,source.Metadata);
    }
    public sealed record Metrics(int RoutedNodes,int EffectCalls,int PeakRetainedNodeBuffers,int ReleasedBeforeReturn);
    private static Dictionary<Guid,int> Consumers(StudioNodeGraph graph,IReadOnlyList<Guid> route)
    {
        var ids=route.ToHashSet();return graph.Edges.Where(e=>ids.Contains(e.To)||e.To==StudioNodeGraph.Output).GroupBy(e=>e.From).ToDictionary(g=>g.Key,g=>g.Count());
    }
    public static long EstimateWorkBytes(HighBitDepthImageBuffer source,ColorAdjustmentStack stack)
    {
        var graph=stack.Graph!;var route=graph.RoutedIds(stack.Nodes);var uses=Consumers(graph,route);var live=new HashSet<Guid>();int peak=3;
        foreach(var id in route)
        {
            // Existing frontier + effect + composite. Source is borrowed but counted conservatively.
            peak=Math.Max(peak,live.Count+3);live.Add(id);
            foreach(var edge in graph.Edges.Where(e=>e.To==id))if(--uses[edge.From]==0)live.Remove(edge.From);
        }
        return checked((long)source.Rgb32.Length*4*Math.Max(peak,live.Count+2));
    }
}
