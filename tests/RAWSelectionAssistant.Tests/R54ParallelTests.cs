using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public class R54ParallelTests
{
    static ColorAdjustmentStack Stack()=>new([new(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"A",NumericParameters:new Dictionary<string,double>{{"exposure",.5}}),new(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"B",NumericParameters:new Dictionary<string,double>{{"exposure",-.3}}),new(Guid.NewGuid(),ColorStudioNodeType.GraphMix,"混合"){CompositeVersion=1,Opacity=.35}],ProcessingVersion:2);
    static ColorAdjustmentStack Graph(){var s=Stack();var a=s.Nodes[0];var b=s.Nodes[1];var m=s.Nodes[2];return s with{Graph=StudioNodeGraph.FromSerial(s.Nodes) with{Version=2,Edges=[new(StudioNodeGraph.Source,"rgb",a.Id,"rgb"),new(StudioNodeGraph.Source,"rgb",b.Id,"rgb"),new(a.Id,"rgb",m.Id,"base"),new(b.Id,"rgb",m.Id,"layer"),new(m.Id,"rgb",StudioNodeGraph.Output,"rgb")]}};}
    static HighBitDepthImageBuffer Render(HighBitDepthImageBuffer source,ColorAdjustmentStack s)=>new ColorStudioRenderPipeline().Render(source,null!,null,s,captureNodeDiagnostics:false).ProcessingPixels!;
    [TestMethod]public void ParallelMixMatchesExistingOrderedCompositeWithMaskAndBypass()
    {
        using var src=new HighBitDepthImageBuffer(2,1,new float[]{.2f,.3f,.4f,.5f,.7f,.8f});var s=Graph();using var a=ColorStudioToolProcessor.Apply(src,s.Nodes[0]);using var b=ColorStudioToolProcessor.Apply(src,s.Nodes[1]);using var expected=StudioLayerComposite.Apply(a,b,s.Nodes[2]);using var actual=Render(src,s);CollectionAssert.AreEqual(expected.Rgb32.ToArray(),actual.Rgb32.ToArray());
        var off=s with{Nodes=s.Nodes.Select(n=>n.Type==ColorStudioNodeType.GraphMix?n with{Enabled=false}:n).ToArray()};using var bypass=Render(src,off);CollectionAssert.AreEqual(a.Rgb32.ToArray(),bypass.Rgb32.ToArray());
        var mask=new StudioLocalMask([new([new(.25,.5)],.3,0,1,1)]);s=s with{Nodes=s.Nodes.Select(n=>n.Type==ColorStudioNodeType.GraphMix?n with{LocalMask=mask}:n).ToArray()};using var exp=StudioLayerComposite.Apply(a,b,s.Nodes[2]);using var got=Render(src,s);CollectionAssert.AreEqual(exp.Rgb32.ToArray(),got.Rgb32.ToArray());
    }
    [TestMethod]public void ProfessionalGroupZeroIdentityAndOneFullRoute()
    {
        using var src=new HighBitDepthImageBuffer(1,1,new float[]{.2f,.3f,.4f});var s=Graph();using var zero=Render(src,s with{Graph=s.Graph! with{GroupAmount=0}});CollectionAssert.AreEqual(src.Rgb32.ToArray(),zero.Rgb32.ToArray());using var disabled=Render(src,s with{Graph=s.Graph! with{GroupEnabled=false}});CollectionAssert.AreEqual(src.Rgb32.ToArray(),disabled.Rgb32.ToArray());
        var text=ColorStudioSchemeSerializer.Serialize(new(Guid.NewGuid(),"并行",s,DateTimeOffset.UnixEpoch));var loaded=ColorStudioSchemeSerializer.Deserialize(text);using var a=Render(src,s);using var b=Render(src,loaded.Stack);CollectionAssert.AreEqual(a.Rgb32.ToArray(),b.Rgb32.ToArray());
    }
    [TestMethod]public void MissingMixInputIdentityAndCycleRejected()
    {
        var s=Graph();var g=s.Graph!;var missing=s with{Graph=g with{Edges=g.Edges.Where(e=>e.Input!="layer").ToArray()}};using var src=new HighBitDepthImageBuffer(1,1,new float[]{.2f,.3f,.4f});using var result=Render(src,missing);CollectionAssert.AreEqual(src.Rgb32.ToArray(),result.Rgb32.ToArray());
        Assert.ThrowsExactly<InvalidDataException>(()=>(g with{Edges=g.Edges.Where(e=>e.To!=s.Nodes[0].Id).Append(new(s.Nodes[2].Id,"rgb",s.Nodes[0].Id,"rgb")).ToArray()}).Freeze(s.Nodes));
    }
    [TestMethod]public void EmitFixedBlendCorpusForTwoIndependentImplementations()
    {
        var path=Environment.GetEnvironmentVariable("KITAO_R54_BLEND_CORPUS");if(path is null){Assert.Inconclusive("Explicit reference corpus path required");return;}var rows=new List<object>();var rng=new Random(54);
        foreach(var mode in new[]{StudioLayerBlend.Normal,StudioLayerBlend.Multiply,StudioLayerBlend.Screen,StudioLayerBlend.Darken,StudioLayerBlend.Lighten})foreach(double amount in new[]{0,.25,.5,1})for(int i=0;i<200;i++)
        {
            byte[] a=[(byte)rng.Next(256),(byte)rng.Next(256),(byte)rng.Next(256)],b=[(byte)rng.Next(256),(byte)rng.Next(256),(byte)rng.Next(256)];using var x=new HighBitDepthImageBuffer(1,1,a.Select(v=>v/255f).ToArray());using var y=new HighBitDepthImageBuffer(1,1,b.Select(v=>v/255f).ToArray());var n=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"reference"){CompositeVersion=2,Opacity=amount,Blend=mode};using var output=StudioLayerComposite.Apply(x,y,n);rows.Add(new{mode=mode.ToString(),amount,a,b,actual=output.Rgb32.ToArray()});
        }
        File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(rows));
    }

    [TestMethod]public void BoundedAndSpatialRoutesPreserveSameGraphBlendOnce()
    {
        using var src=new HighBitDepthImageBuffer(8,4,Enumerable.Range(0,96).Select(i=>(i%31)/40f).ToArray());var s=Graph();s=s with{Nodes=s.Nodes.Select((n,i)=>i==0?n with{CompositeVersion=1,Opacity=.6,LocalMask=new StudioLocalMask([new([new(.5,.5)],.4,.2,1,1)])}:n).ToArray()};using var expected=Render(src,s);using var bounded=BoundedLegacyRawRenderer.Render(src,null,s,null);using var spatial=SpatialLegacyRawRenderer.Render(src,null,s,null);CollectionAssert.AreEqual(expected.Rgb32.ToArray(),bounded.Rgb32.ToArray());CollectionAssert.AreEqual(expected.Rgb32.ToArray(),spatial.Rgb32.ToArray());
    }

}
