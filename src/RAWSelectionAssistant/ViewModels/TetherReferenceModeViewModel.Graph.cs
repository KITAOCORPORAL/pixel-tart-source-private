using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class TetherReferenceModeViewModel
{
    public StudioNodeGraph GraphView=>AdjustmentStack.Graph??StudioNodeGraph.FromSerial(AdjustmentStack.Nodes);
    public bool CommitGraph(StudioNodeGraph graph,bool render=true)
    {
        if(ReadOnlyTarget||AdjustmentNodes.Count==0){StatusText="请先选择可编辑变体并添加调色节点。";return false;}
        try
        {
            graph=graph.Freeze(AdjustmentStack.Nodes);
            if(graph.Version==2&&LinearDocumentEditing)throw new InvalidDataException("线性宽域并行尚未准入；保持现有处理域。");
            var previous=EffectiveEditingStack();var old=previous.Graph??StudioNodeGraph.FromSerial(previous.Nodes);
            foreach(var n in previous.Nodes.Where(n=>n.IsLocked))
                if(!old.Edges.Where(e=>e.From==n.Id||e.To==n.Id).SequenceEqual(graph.Edges.Where(e=>e.From==n.Id||e.To==n.Id))||old.Positions.Single(p=>p.NodeId==n.Id)!=graph.Positions.Single(p=>p.NodeId==n.Id))throw new InvalidDataException("锁定节点不能移动或改接线。");
            if(System.Text.Json.JsonSerializer.Serialize(old)==System.Text.Json.JsonSerializer.Serialize(graph)&&previous.Graph is not null)return true;
            if(_editTransactionBefore is null){_undoStacks.Push(previous);_undoSelections.Push(_selectedAdjustmentNodeId);}
            _redoStacks.Clear();_redoSelections.Clear();AdjustmentStack=(previous with{Version=3,Graph=graph}).Normalize();
            if(render)_=DebouncedRenderAsync();StatusText=graph.RoutedIds(previous.Nodes).Count==0?"输出未接源图：显示原图，未连接节点已保留。":"节点路由已更新。";return true;
        }
        catch(Exception e)when(e is InvalidDataException or ArgumentException){StatusText=e.Message;return false;}
    }
    private bool CommitGraphStack(ColorAdjustmentStack next)
    {
        if(ReadOnlyTarget||LinearDocumentEditing){StatusText="原片只读或线性宽域尚未支持并行。";return false;}
        try{next=next.Normalize();var old=EffectiveEditingStack();foreach(var locked in old.Nodes.Where(n=>n.IsLocked))
            if(!next.Nodes.Any(n=>n.Id==locked.Id&&System.Text.Json.JsonSerializer.Serialize(n)==System.Text.Json.JsonSerializer.Serialize(locked))||!GraphView.Edges.Where(e=>e.From==locked.Id||e.To==locked.Id).SequenceEqual(next.Graph!.Edges.Where(e=>e.From==locked.Id||e.To==locked.Id)))throw new InvalidDataException("锁定节点保持，未改路由。");
            _undoStacks.Push(old);_undoSelections.Push(_selectedAdjustmentNodeId);_redoStacks.Clear();_redoSelections.Clear();AdjustmentStack=next;_ =DebouncedRenderAsync();return true;}
        catch(Exception e)when(e is InvalidDataException or ArgumentException){StatusText=e.Message;return false;}
    }
    public bool AddParallelGraphBranch()
    {
        if(SelectedAdjustmentNode is not{IsLocked:false,Type:not ColorStudioNodeType.GraphMix} selected){StatusText="请选择可编辑调色节点。";return false;}
        var graph=GraphView;var outgoing=graph.Edges.Where(e=>e.From==selected.Id).ToArray();var incoming=graph.Edges.SingleOrDefault(e=>e.To==selected.Id);
        if(outgoing.Length!=1||incoming is null){StatusText="请选择具有单一下游的已连接调色节点。";return false;}
        var branch=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"并行调色"){CompositeVersion=1};
        var mix=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.GraphMix,"图层混合"){CompositeVersion=1,Opacity=.5};
        var p=graph.Positions.Single(p=>p.NodeId==selected.Id);var edges=graph.Edges.Where(e=>e!=outgoing[0]).ToList();
        edges.Add(new(incoming.From,"rgb",branch.Id,"rgb"));edges.Add(new(selected.Id,"rgb",mix.Id,"base"));edges.Add(new(branch.Id,"rgb",mix.Id,"layer"));edges.Add(new(mix.Id,"rgb",outgoing[0].To,outgoing[0].Input));
        var downstream=new HashSet<Guid>();var pending=new Queue<Guid>();pending.Enqueue(outgoing[0].To);
        while(pending.TryDequeue(out var id))if(downstream.Add(id))foreach(var edge in graph.Edges.Where(e=>e.From==id))pending.Enqueue(edge.To);
        var locked=AdjustmentNodes.Where(n=>n.IsLocked).Select(n=>n.Id).ToHashSet();
        var positions=graph.Positions.Select(pos=>downstream.Contains(pos.NodeId)&&!locked.Contains(pos.NodeId)&&pos.X>=p.X+140?pos with{X=pos.X+300}:pos).ToList();
        void Place(Guid id,double x,double y){while(positions.Any(pos=>Math.Abs(pos.X-x)<148&&Math.Abs(pos.Y-y)<104))y+=110;positions.Add(new(id,x,y));}
        Place(branch.Id,p.X,p.Y+110);Place(mix.Id,p.X+160,p.Y+52);
        var next=AdjustmentStack with{Nodes=AdjustmentNodes.Concat(new[]{branch,mix}).ToArray(),ProcessingVersion=2,Graph=graph with{Version=2,Edges=edges,Positions=positions.ToArray()}};
        if(!CommitGraphStack(next))return false;SelectedAdjustmentNode=branch;return true;
    }
    public bool DeleteGraphNode()
    {
        if(SelectedAdjustmentNode is not{IsLocked:false} node||AdjustmentNodes.Count<=1)return false;
        if(AdjustmentStack.Graph?.Version!=2){DeleteAdjustmentNodeCommand.Execute(null);return true;}
        var graph=GraphView;var incoming=graph.Edges.FirstOrDefault(e=>e.To==node.Id&&e.Input==(node.Type==ColorStudioNodeType.GraphMix?"base":"rgb"));
        var outgoing=graph.Edges.Where(e=>e.From==node.Id).ToArray();var edges=graph.Edges.Where(e=>e.From!=node.Id&&e.To!=node.Id).ToList();
        // Delete reconnects downstream to primary/base input. Deleting a branch bypasses its effect, not the sibling.
        if(incoming is not null)edges.AddRange(outgoing.Select(e=>new StudioGraphEdge(incoming.From,"rgb",e.To,e.Input)));
        return CommitGraphStack(AdjustmentStack with{Nodes=AdjustmentNodes.Where(n=>n.Id!=node.Id).ToArray(),Graph=graph with{Edges=edges,Positions=graph.Positions.Where(p=>p.NodeId!=node.Id).ToArray()}});
    }
    public bool HasProfessionalRouting=>AdjustmentStack.Graph?.Version==2;

    public bool ProfessionalGroupEnabled {get=>AdjustmentStack.Graph?.GroupEnabled??true;set{if(AdjustmentStack.Graph is{} g&&g.GroupEnabled!=value)CommitGraph(g with{GroupEnabled=value});}}
    public double ProfessionalGroupAmount {get=>AdjustmentStack.Graph?.GroupAmount??1;set{if(AdjustmentStack.Graph is{} g&&double.IsFinite(value)&&g.GroupAmount!=Math.Clamp(value,0,1))CommitGraph(g with{GroupAmount=Math.Clamp(value,0,1)});}}
    public bool AddGraphSerialNode(string type)
    {
        if(AdjustmentStack.Graph?.Version!=2){AddAdjustmentNodeCommand.Execute(type);return true;}
        if(!Enum.TryParse<ColorStudioNodeType>(type,out var t)||!(ColorStudioToolCatalog.IsTool(t)||t==ColorStudioNodeType.ColorRange))return false;
        var graph=GraphView;var source=SelectedAdjustmentNode?.Id??StudioNodeGraph.Source;var outgoing=graph.Edges.Where(e=>e.From==source).ToArray();if(outgoing.Length!=1){StatusText="请选择具有单一下游的节点作为串行插入点。";return false;}
        var node=new ColorAdjustmentStackNode(Guid.NewGuid(),t,t==ColorStudioNodeType.ColorRange?"颜色范围":ToolName(t),NumericParameters:t==ColorStudioNodeType.ColorRange?new Dictionary<string,double>{{"range_version",2}}:null);var p=graph.Positions.Single(p=>p.NodeId==source);var edge=outgoing[0];
        var next=AdjustmentStack with{Nodes=AdjustmentNodes.Append(node).ToArray(),Graph=graph with{Positions=graph.Positions.Append(new(node.Id,p.X+150,p.Y+92)).ToArray(),Edges=graph.Edges.Where(e=>e!=edge).Concat(new[]{new StudioGraphEdge(source,"rgb",node.Id,"rgb"),new StudioGraphEdge(node.Id,"rgb",edge.To,edge.Input)}).ToArray()}};
        if(!CommitGraphStack(next))return false;SelectedAdjustmentNode=node;return true;
    }

    public bool DuplicateGraphNode()
    {
        if(SelectedAdjustmentNode is not{IsLocked:false,Type:not ColorStudioNodeType.GraphMix} selected)return false;
        if(AdjustmentStack.Graph?.Version!=2){DuplicateAdjustmentNodeCommand.Execute(null);return true;}
        var graph=GraphView;var outgoing=graph.Edges.Where(e=>e.From==selected.Id).ToArray();if(outgoing.Length!=1)return false;var edge=outgoing[0];var node=selected.Normalize() with{Id=Guid.NewGuid(),Name=selected.Name+" 副本"};var p=graph.Positions.Single(p=>p.NodeId==selected.Id);
        var next=AdjustmentStack with{Nodes=AdjustmentNodes.Append(node).ToArray(),Graph=graph with{Positions=graph.Positions.Append(new(node.Id,p.X+150,p.Y+92)).ToArray(),Edges=graph.Edges.Where(e=>e!=edge).Concat(new[]{new StudioGraphEdge(selected.Id,"rgb",node.Id,"rgb"),new StudioGraphEdge(node.Id,"rgb",edge.To,edge.Input)}).ToArray()}};return CommitGraphStack(next);
    }

    public void ResetGraphNode(){if(SelectedAdjustmentNode is{Type:ColorStudioNodeType.GraphMix,IsLocked:false} node)ChangeStack(nodes=>nodes.Select(n=>n.Id==node.Id?n with{Opacity=.5,Blend=StudioLayerBlend.Normal,LocalMask=null}:n).ToArray());else ResetAdjustmentNodeCommand.Execute(null);}
    private ColorAdjustmentStackNode? graphSettingsClipboard;
    public void CopyGraphSettings(){if(SelectedAdjustmentNode is{} n)graphSettingsClipboard=n.Normalize();}
    public void PasteGraphSettings(){if(graphSettingsClipboard is not{} copied||SelectedAdjustmentNode is not{IsLocked:false} selected||copied.Type!=selected.Type){StatusText="请选择相同类型的可编辑节点。";return;}ChangeStack(nodes=>nodes.Select(n=>n.Id==selected.Id?copied with{Id=selected.Id,Name=selected.Name,UserLocked=selected.UserLocked,SystemLocked=selected.SystemLocked}:n).ToArray());}

    public void SetGraphLabel(int label){if(label is <0 or >5||SelectedAdjustmentNode is not{IsLocked:false} selected)return;ChangeStack(nodes=>nodes.Select(n=>n.Id==selected.Id?n with{GraphLabel=label}:n).ToArray(),render:false);}

    public string SelectedNodeTypeLabel=>SelectedAdjustmentNode?.Type switch{ColorStudioNodeType.GraphMix=>"⊕ 混合",ColorStudioNodeType.Film=>"▣ 胶片",null=>"未选择节点",_=>"◐ 调色"};

}
