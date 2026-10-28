namespace RAWSelectionAssistant.Core.Services.Projects;

public sealed record StudioGraphPosition(Guid NodeId,double X,double Y);
public sealed record StudioGraphEdge(Guid From,string Output,Guid To,string Input);
/// <summary>R54 graph v1 serial / v2 explicit base-layer RGB routes; no implicit branch mixing. Layout never defines render order.</summary>
public sealed record StudioNodeGraph(int Version,IReadOnlyList<StudioGraphPosition> Positions,IReadOnlyList<StudioGraphEdge> Edges)
{
    public bool GroupEnabled {get;init;}=true;
    public double GroupAmount {get;init;}=1;
    public static readonly Guid Source=Guid.Empty;
    public static readonly Guid Output=new("ffffffff-ffff-ffff-ffff-ffffffffffff");
    public static StudioNodeGraph FromSerial(IReadOnlyList<ColorAdjustmentStackNode> nodes)
    {
        var ids=new[]{Source}.Concat(nodes.Select(n=>n.Id)).Append(Output).ToArray();
        return new(1,ids.Select((id,i)=>new StudioGraphPosition(id,24+i*150,64)).ToArray(),ids.Zip(ids.Skip(1),(a,b)=>new StudioGraphEdge(a,"rgb",b,"rgb")).ToArray());
    }
    public StudioNodeGraph Freeze(IReadOnlyList<ColorAdjustmentStackNode> nodes)
    {
        var ids=nodes.Select(n=>n.Id).Append(Source).Append(Output).ToHashSet();
        if(Positions is null||Edges is null)throw new InvalidDataException("节点图集合缺失。");
        if(!double.IsFinite(GroupAmount)||GroupAmount is <0 or >1||Version is not(1 or 2)||nodes.Count>64||nodes.Any(n=>n.Id==Output)||Positions is null||Edges is null||Positions.Count!=ids.Count||Positions.Select(p=>p.NodeId).Distinct().Count()!=ids.Count||Positions.Any(p=>!ids.Contains(p.NodeId)||!double.IsFinite(p.X+p.Y)||Math.Abs(p.X)>100000||Math.Abs(p.Y)>100000))throw new InvalidDataException("节点图版本/布局无效。");
        var byId=nodes.ToDictionary(n=>n.Id);
        if(Edges.Count>192||Edges.Any(e=>!ids.Contains(e.From)||!ids.Contains(e.To)||e.From==Output||e.To==Source||e.From==e.To||e.Output!="rgb"||e.Input is not("rgb" or "base" or "layer")||((byId.GetValueOrDefault(e.To)?.Type==ColorStudioNodeType.GraphMix)?Version!=2||e.Input=="rgb":e.Input!="rgb"))||Edges.GroupBy(e=>(e.To,e.Input)).Any(g=>g.Count()>1)||Version==1&&(Edges.GroupBy(e=>e.To).Any(g=>g.Count()>1)||Edges.GroupBy(e=>e.From).Any(g=>g.Count()>1)))throw new InvalidDataException("节点图端口非法；合流必须显式使用base/layer混合节点。");
        if(Version==1&&nodes.Any(n=>n.Type==ColorStudioNodeType.GraphMix))throw new InvalidDataException("混合节点需要显式图版本2。");
        var state=new Dictionary<Guid,int>();
        void Visit(Guid id){if(state.TryGetValue(id,out var mark)){if(mark==1)throw new InvalidDataException("节点图不允许环路。");return;}state[id]=1;foreach(var edge in Edges.Where(e=>e.From==id))Visit(edge.To);state[id]=2;}
        foreach(var id in ids)Visit(id);
        return this with{Positions=Positions.Select(p=>p with{}).ToArray(),Edges=Edges.Select(e=>e with{}).ToArray()};
    }
    public IReadOnlyList<Guid> RoutedIds(IReadOnlyList<ColorAdjustmentStackNode> nodes)
    {
        Freeze(nodes);var byId=nodes.ToDictionary(n=>n.Id);var ordered=new List<Guid>();var seen=new HashSet<Guid>();
        bool Visit(Guid id){if(id==Source)return true;if(id==Output){var edge=Edges.SingleOrDefault(e=>e.To==id);return edge is not null&&Visit(edge.From);}if(seen.Contains(id))return true;
            var incoming=Edges.Where(e=>e.To==id).OrderBy(e=>e.Input=="base"?0:1).ToArray();
            if(incoming.Length!=(byId[id].Type==ColorStudioNodeType.GraphMix?2:1))return false;
            foreach(var edge in incoming)if(!Visit(edge.From))return false;seen.Add(id);ordered.Add(id);return true;}
        return Visit(Output)?ordered:[];
    }
    public bool HasMix=>Version==2;
    public static string DefaultInput(ColorAdjustmentStackNode? node)=>node?.Type==ColorStudioNodeType.GraphMix?"base":"rgb";

    public StudioNodeGraph Move(Guid id,double x,double y)=>this with{Positions=Positions.Select(p=>p.NodeId==id?p with{X=x,Y=y}:p).ToArray()};
    public StudioNodeGraph Connect(Guid from,Guid to,string input="rgb")=>this with{Edges=Edges.Where(e=>(Version==2||e.From!=from)&&!(e.To==to&&e.Input==input)).Append(new(from,"rgb",to,input)).ToArray()};
    public StudioNodeGraph Disconnect(StudioGraphEdge edge)=>this with{Edges=Edges.Where(e=>e!=edge).ToArray()};
    public StudioNodeGraph Insert(Guid id,StudioGraphEdge edge)
    {
        if(id==Source||id==Output||id==edge.From||id==edge.To)return this;
        if(Edges.Count(e=>e.To==id)>1||Edges.Count(e=>e.From==id)>1)throw new InvalidDataException("分叉/混合节点请用端口明确重接，未自动拆路由。");
        var incoming=Edges.FirstOrDefault(e=>e.To==id);var outgoing=Edges.FirstOrDefault(e=>e.From==id);
        var list=Edges.Where(e=>e.From!=id&&e.To!=id&&e!=edge).ToList();
        if(incoming is not null&&outgoing is not null)list.Add(new(incoming.From,"rgb",outgoing.To,outgoing.Input));
        list.Add(new(edge.From,"rgb",id,"rgb"));list.Add(new(id,"rgb",edge.To,edge.Input));return this with{Edges=list};
    }
    public StudioNodeGraph Reconcile(IReadOnlyList<ColorAdjustmentStackNode> before,IReadOnlyList<ColorAdjustmentStackNode> after)
    {
        if(before.Select(n=>n.Id).SequenceEqual(after.Select(n=>n.Id)))return Freeze(after);
        if(Version==2){if(!before.Select(n=>n.Id).ToHashSet().SetEquals(after.Select(n=>n.Id)))throw new InvalidDataException("复杂路由增删需使用节点图操作；未压平成串行。");return Freeze(after);}
        var known=before.Select(n=>n.Id).ToHashSet();var retained=after.Select(n=>n.Id).ToHashSet();var graph=this;
        foreach(var removed in before.Where(n=>!retained.Contains(n.Id)))
        {
            var incoming=graph.Edges.FirstOrDefault(e=>e.To==removed.Id);var outgoing=graph.Edges.FirstOrDefault(e=>e.From==removed.Id);
            var edges=graph.Edges.Where(e=>e.From!=removed.Id&&e.To!=removed.Id).ToList();if(incoming is not null&&outgoing is not null)edges.Add(new(incoming.From,"rgb",outgoing.To,outgoing.Input));
            graph=graph with{Edges=edges,Positions=graph.Positions.Where(p=>p.NodeId!=removed.Id).ToArray()};
        }
        foreach(var added in after.Where(n=>!known.Contains(n.Id)))
        {
            int index=after.ToList().FindIndex(n=>n.Id==added.Id);var preceding=after.Take(index).LastOrDefault(n=>graph.Positions.Any(p=>p.NodeId==n.Id));var parent=preceding?.Id??Source;
            var pos=graph.Positions.Single(p=>p.NodeId==parent);var outgoing=graph.Edges.FirstOrDefault(e=>e.From==parent);
            var edges=graph.Edges.ToList();if(outgoing is not null)edges.Remove(outgoing);edges.Add(new(parent,"rgb",added.Id,"rgb"));if(outgoing is not null)edges.Add(new(added.Id,"rgb",outgoing.To,"rgb"));
            graph=graph with{Positions=graph.Positions.Append(new(added.Id,pos.X+150,pos.Y+90)).ToArray(),Edges=edges};
        }
        if(known.SetEquals(retained))
        {
            // Explicit legacy reorder rearranges only nodes already routed to the output; detached drafts stay detached.
            var active=RoutedIds(before).ToHashSet();var ordered=after.Where(n=>active.Contains(n.Id)).Select(n=>n.Id).ToArray();
            if(active.Count>0){var ids=new[]{Source}.Concat(ordered).Append(Output).ToArray();graph=graph with{Edges=graph.Edges.Where(e=>e.From!=Source&&e.To!=Output&&!active.Contains(e.From)&&!active.Contains(e.To)).Concat(ids.Zip(ids.Skip(1),(a,b)=>new StudioGraphEdge(a,"rgb",b,"rgb"))).ToArray()};}
        }
        return graph.Freeze(after);
    }
}
public static class StudioGraphRouting
{
    public static IReadOnlyList<ColorAdjustmentStackNode> Nodes(ColorAdjustmentStack stack)
    {
        if(stack.Graph is null)return stack.Nodes;
        var byId=stack.Nodes.ToDictionary(n=>n.Id);return stack.Graph.RoutedIds(stack.Nodes).Select(id=>byId[id]).ToArray();
    }
}
