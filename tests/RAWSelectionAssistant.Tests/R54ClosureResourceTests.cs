using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass][DoNotParallelize]
public class R54ClosureResourceTests
{
    static ColorAdjustmentStack Graph()
    {
        var nodes=Enumerable.Range(0,6).Select(i=>new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.Preset,"P"+i,NumericParameters:new Dictionary<string,double>{{"exposure",.1*(i%2==0?1:-1)}})).ToArray();
        var mix=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.GraphMix,"M"){CompositeVersion=1,Opacity=.35};var all=nodes.Append(mix).ToArray();var g=StudioNodeGraph.FromSerial(all) with{Version=2,Edges=[new(StudioNodeGraph.Source,"rgb",nodes[0].Id,"rgb"),new(nodes[0].Id,"rgb",nodes[1].Id,"rgb"),new(nodes[1].Id,"rgb",nodes[2].Id,"rgb"),new(nodes[0].Id,"rgb",nodes[3].Id,"rgb"),new(nodes[3].Id,"rgb",nodes[4].Id,"rgb"),new(nodes[2].Id,"rgb",mix.Id,"base"),new(nodes[4].Id,"rgb",mix.Id,"layer"),new(mix.Id,"rgb",nodes[5].Id,"rgb"),new(nodes[5].Id,"rgb",StudioNodeGraph.Output,"rgb")]};return new(all,ProcessingVersion:2){Graph=g};
    }
    [TestMethod]public void LastConsumerDisposalSharedAncestorAndEscapedOutputLease()
    {
        var s=Graph();using var input=new HighBitDepthImageBuffer(64,64,Enumerable.Repeat(.3f,64*64*3).ToArray());var before=StudioGlobalResources.AccountedBytes;var calls=new Dictionary<Guid,int>();
        for(int repeat=0;repeat<4;repeat++)
        {
            var result=StudioGraphRenderer.Render(input,s,(a,n)=>{calls[n.Id]=calls.GetValueOrDefault(n.Id)+1;return ColorStudioRenderPipeline.ApplyPointEffect(a,n);});Assert.AreEqual(before+input.Rgb32.Length*4L,StudioGlobalResources.AccountedBytes,"Only escaped result remains leased");Assert.IsTrue(result.Rgb32.Span.ToArray().All(float.IsFinite));result.Dispose();Assert.AreEqual(before,StudioGlobalResources.AccountedBytes);
        }
        Assert.IsTrue(calls.Values.All(n=>n==4),"Shared ancestor rendered once per invocation");
    }
    [TestMethod]public void CancellationExceptionAndUnusedTemporaryReleaseEveryReservation()
    {
        var s=Graph();using var input=new HighBitDepthImageBuffer(48,32,new float[48*32*3]);var before=StudioGlobalResources.AccountedBytes;using var cancel=new CancellationTokenSource();int calls=0;
        Assert.ThrowsExactly<OperationCanceledException>(()=>StudioGraphRenderer.Render(input,s,(a,n)=>{var b=ColorStudioRenderPipeline.ApplyPointEffect(a,n);if(++calls==3)cancel.Cancel();return b;},cancel.Token));Assert.AreEqual(before,StudioGlobalResources.AccountedBytes);
        Assert.ThrowsExactly<InvalidOperationException>(()=>StudioGraphRenderer.Render(input,s,(a,n)=>{if(++calls>4)throw new InvalidOperationException("injected");return ColorStudioRenderPipeline.ApplyPointEffect(a,n);}));Assert.AreEqual(before,StudioGlobalResources.AccountedBytes);
    }
    [TestMethod]public void MasksBypassGroupAndReferenceAllRetainedOracleAreBitExact()
    {
        var s=Graph();s=s with{Graph=s.Graph! with{GroupAmount=.7},Nodes=s.Nodes.Select((n,i)=>i==1?n with{Enabled=false}:i==3?n with{CompositeVersion=1,Opacity=.6,LocalMask=new StudioLocalMask([new([new(.5,.5)],.4,.5,1,1)])}:n).ToArray()};using var input=new HighBitDepthImageBuffer(64,32,Enumerable.Range(0,64*32*3).Select(i=>(i%251)/255f).ToArray());
        using var actual=StudioGraphRenderer.Render(input,s,(a,n)=>ColorStudioRenderPipeline.ApplyPointEffect(a,n));using var expected=Retained(input,s);CollectionAssert.AreEqual(expected.Rgb32.ToArray(),actual.Rgb32.ToArray());
    }
    static HighBitDepthImageBuffer Retained(HighBitDepthImageBuffer input,ColorAdjustmentStack stack)
    {
        var values=new Dictionary<Guid,HighBitDepthImageBuffer>{{StudioNodeGraph.Source,input}};var g=stack.Graph!;try{foreach(var id in g.RoutedIds(stack.Nodes)){var n=stack.Nodes.Single(n=>n.Id==id);var edges=g.Edges.Where(e=>e.To==id).ToArray();var a=values[edges.Single(e=>e.Input==(n.Type==ColorStudioNodeType.GraphMix?"base":"rgb")).From];if(!n.Enabled){values[id]=a.Clone();continue;}using var b=n.Type==ColorStudioNodeType.GraphMix?values[edges.Single(e=>e.Input=="layer").From].Clone():ColorStudioRenderPipeline.ApplyPointEffect(a,n);var c=StudioLayerComposite.Apply(a,b,n);values[id]=ReferenceEquals(c,b)?b.Clone():c;}var final=values[g.Edges.Single(e=>e.To==StudioNodeGraph.Output).From];var group=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"G"){CompositeVersion=1,Opacity=g.GroupAmount};return StudioLayerComposite.Apply(input,final,group);}finally{foreach(var p in values)if(p.Key!=StudioNodeGraph.Source)p.Value.Dispose();}
    }
}
