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
        IReadOnlyDictionary<Guid,Func<double,double,double>>? masks=null,Action<Guid,HighBitDepthImageBuffer,HighBitDepthImageBuffer>? diagnostics=null)
    {
        stack=stack.Normalize();var graph=stack.Graph??throw new ArgumentException("Graph missing");
        if(source.WorkingColorSpace!="sRGB")throw new NotSupportedException("并行图仅接受既有显示sRGB；线性宽域不隐式变换。");
        var route=graph.RoutedIds(stack.Nodes);if(!graph.GroupEnabled||graph.GroupAmount==0||route.Count==0)return source.Clone();
        var byId=stack.Nodes.ToDictionary(n=>n.Id);var cache=new Dictionary<Guid,HighBitDepthImageBuffer>{{StudioNodeGraph.Source,source}};
        // Bounded node count and explicit budget; branch buffers released after final copy.
        using var budget=StudioGlobalResources.ReserveWork(checked((long)source.Rgb32.Length*4*(route.Count+3)));
        try
        {
            foreach(var id in route)
            {
                token.ThrowIfCancellationRequested();var node=byId[id];var incoming=graph.Edges.Where(e=>e.To==id).ToArray();
                var input=cache[incoming.Single(e=>e.Input==(node.Type==ColorStudioNodeType.GraphMix?"base":"rgb")).From];HighBitDepthImageBuffer next;
                if(!node.Enabled)next=input.Clone();
                else if(node.Type==ColorStudioNodeType.GraphMix){var layer=cache[incoming.Single(e=>e.Input=="layer").From];next=StudioLayerComposite.Apply(input,layer,node,token,tile,sourceCoordinate,masks?.GetValueOrDefault(id));if(ReferenceEquals(next,layer))next=layer.Clone();}
                else {var rendered=effect(input,node);next=StudioLayerComposite.Apply(input,rendered,node,token,tile,sourceCoordinate,masks?.GetValueOrDefault(id));if(!ReferenceEquals(next,rendered))rendered.Dispose();}
                cache[id]=next;diagnostics?.Invoke(id,input,next);
            }
            var final=cache[graph.Edges.Single(e=>e.To==StudioNodeGraph.Output).From];
            if(graph.GroupAmount==1)return final.Clone();
            var group=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"专业组"){CompositeVersion=1,Opacity=graph.GroupAmount};return StudioLayerComposite.Apply(source,final,group,token);
        }
        finally{foreach(var pair in cache)if(pair.Key!=StudioNodeGraph.Source)pair.Value.Dispose();}
    }
}
