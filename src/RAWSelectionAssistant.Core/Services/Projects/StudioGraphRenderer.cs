using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Same Graph2 numerical contract. Intermediate results live only through their last consumer;
/// every owned buffer has an admission lease, including the returned result. Source is borrowed.</summary>
public static class StudioGraphRenderer
{
    public static int PeakBackingBuffers(ColorAdjustmentStack stack)
    {
        var graph=stack.Graph??throw new ArgumentException("Graph missing");var route=graph.RoutedIds(stack.Nodes);if(route.Count==0||!graph.GroupEnabled||graph.GroupAmount==0)return 1;
        var by=stack.Nodes.ToDictionary(n=>n.Id);var active=route.ToHashSet();var uses=graph.Edges.Where(e=>active.Contains(e.To)||e.To==StudioNodeGraph.Output).GroupBy(e=>e.From).ToDictionary(g=>g.Key,g=>g.Count());int live=0,peak=0;
        foreach(var id in route){var n=by[id];peak=Math.Max(peak,live+(!n.Enabled||n.Type==ColorStudioNodeType.GraphMix||StudioLayerComposite.Identity(n)?1:2));live++;foreach(var e in graph.Edges.Where(e=>e.To==id))if(--uses[e.From]==0&&e.From!=StudioNodeGraph.Source)live--;}
        return Math.Max(peak,live+(graph.GroupAmount==1?0:1));
    }
    public static HighBitDepthImageBuffer Render(HighBitDepthImageBuffer source,ColorAdjustmentStack stack,
        Func<HighBitDepthImageBuffer,ColorAdjustmentStackNode,HighBitDepthImageBuffer> effect,CancellationToken token=default,
        FilmTileCoordinates? tile=null,Func<int,int,(double X,double Y)>? sourceCoordinate=null,
        IReadOnlyDictionary<Guid,Func<double,double,double>>? masks=null,Action<Guid,HighBitDepthImageBuffer,HighBitDepthImageBuffer>? diagnostics=null)
    {
        using var allocation=GraphPixelAllocation.EnterIfNeeded();
        stack=stack.Normalize();var graph=stack.Graph??throw new ArgumentException("Graph missing");
        if(source.WorkingColorSpace!="sRGB")throw new NotSupportedException("并行图仅接受既有显示sRGB；线性宽域不隐式变换。");
        var route=graph.RoutedIds(stack.Nodes);long bytes=checked((long)source.Rgb32.Length*sizeof(float));
        HighBitDepthImageBuffer Copy(HighBitDepthImageBuffer input){token.ThrowIfCancellationRequested();return input.Clone();}
        void Account(HighBitDepthImageBuffer buffer){if(!buffer.HasGraphBacking&&!buffer.OwnsReservation)buffer.TakeReservation(StudioGlobalResources.ReserveWork(bytes));}
        if(!graph.GroupEnabled||graph.GroupAmount==0||route.Count==0)return Copy(source);
        var byId=stack.Nodes.ToDictionary(n=>n.Id);var active=route.ToHashSet();
        var incoming=route.ToDictionary(id=>id,id=>graph.Edges.Where(e=>e.To==id).OrderBy(e=>e.Input=="base"?0:1).ToArray());
        var remaining=graph.Edges.Where(e=>active.Contains(e.To)||e.To==StudioNodeGraph.Output).GroupBy(e=>e.From).ToDictionary(g=>g.Key,g=>g.Count());
        var cache=new Dictionary<Guid,HighBitDepthImageBuffer>{{StudioNodeGraph.Source,source}};
        try
        {
            foreach(var id in route)
            {
                token.ThrowIfCancellationRequested();var node=byId[id];var edges=incoming[id];var input=cache[edges.Single(e=>e.Input==(node.Type==ColorStudioNodeType.GraphMix?"base":"rgb")).From];
                HighBitDepthImageBuffer? next=null,rendered=null;
                try
                {
                    if(!node.Enabled||node.Opacity==0&&StudioLayerComposite.Active(node))next=input.Clone();
                    else if(node.Type==ColorStudioNodeType.GraphMix)
                    {
                        var layer=cache[edges.Single(e=>e.Input=="layer").From];
                        next=StudioLayerComposite.Apply(input,layer,node,token,tile,sourceCoordinate,masks?.GetValueOrDefault(id));
                        if(ReferenceEquals(next,layer)||ReferenceEquals(next,input))next=next.Clone();
                    }
                    else
                    {
                        // Only a composited effect needs a second transient RGB buffer. Tools' internal
                        // scratch remains their existing responsibility, not N retained graph outputs.
                        rendered=effect(input,node);if(ReferenceEquals(rendered,input))rendered=input.Clone();Account(rendered);token.ThrowIfCancellationRequested();
                        next=StudioLayerComposite.Apply(input,rendered,node,token,tile,sourceCoordinate,masks?.GetValueOrDefault(id));
                        if(ReferenceEquals(next,input))next=next.Clone();
                        if(!ReferenceEquals(next,rendered))rendered.Dispose();rendered=null;
                    }
                    Account(next);cache.Add(id,next);next=null;
                    diagnostics?.Invoke(id,input,cache[id]); // callback borrows only for this invocation
                }
                finally{next?.Dispose();rendered?.Dispose();}
                foreach(var edge in edges)
                {
                    if(--remaining[edge.From]==0&&edge.From!=StudioNodeGraph.Source&&cache.Remove(edge.From,out var retired))retired.Dispose();
                }
            }
            var finalId=graph.Edges.Single(e=>e.To==StudioNodeGraph.Output).From;var final=cache[finalId];
            if(graph.GroupAmount==1){cache.Remove(finalId);return final;}
            var group=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"专业组"){CompositeVersion=1,Opacity=graph.GroupAmount};
            token.ThrowIfCancellationRequested();var result=StudioLayerComposite.Apply(source,final,group,token);Account(result);return result;
        }
        finally{foreach(var pair in cache)if(pair.Key!=StudioNodeGraph.Source)pair.Value.Dispose();}
    }
}
