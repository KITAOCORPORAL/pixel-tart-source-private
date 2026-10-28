using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public class R54GraphTests
{
    static ColorAdjustmentStackNode Node(string name,double exposure)=>new(Guid.NewGuid(),ColorStudioNodeType.BasicTone,name,NumericParameters:new Dictionary<string,double>{{"exposure",exposure}});
    static HighBitDepthImageBuffer Input()=>new(3,1,new float[]{.01f,.03f,.05f,.2f,.3f,.4f,.8f,.7f,.6f});
    static float[] Render(ColorAdjustmentStack stack){using var input=Input();var r=new ColorStudioRenderPipeline().Render(input,null!,null,stack,captureNodeDiagnostics:false);using var pixels=r.ProcessingPixels!;return pixels.Rgb32.ToArray();}
    [TestMethod] public void LayoutDoesNotChangeHistoricalSerialPixelsOrOrder()
    {
        var a=Node("曝光",.7);var b=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.Curve,"曲线",NumericParameters:new Dictionary<string,double>{{"rgb_y1",.1},{"rgb_y2",.7}});var s=new ColorAdjustmentStack([a,b],ProcessingVersion:2);
        var g=StudioNodeGraph.FromSerial(s.Nodes);var routed=s with{Graph=g.Move(a.Id,100,200).Move(b.Id,-100,20)};
        CollectionAssert.AreEqual(Render(s),Render(routed));Assert.AreEqual(2,s.Normalize().Version);Assert.AreEqual(3,routed.Normalize().Version);Assert.IsNull(s.Graph);CollectionAssert.AreEqual(new[]{a.Id,b.Id},StudioGraphRouting.Nodes(routed).Select(n=>n.Id).ToArray());
    }
    [TestMethod] public void ConnectionsDriveActualPixelsNotListOrder()
    {
        var a=Node("曝光",1);var b=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.Levels,"色阶",NumericParameters:new Dictionary<string,double>{{"rgb_black",.1},{"rgb_gamma",1.6}});var s=new ColorAdjustmentStack([a,b],ProcessingVersion:2);var g=StudioNodeGraph.FromSerial(s.Nodes) with{Edges=[new(StudioNodeGraph.Source,"rgb",b.Id,"rgb"),new(b.Id,"rgb",a.Id,"rgb"),new(a.Id,"rgb",StudioNodeGraph.Output,"rgb")]};
        var actual=Render(s with{Graph=g});var expected=Render(s with{Nodes=[b,a]});CollectionAssert.AreEqual(expected,actual);Assert.IsTrue(actual.Zip(Render(s),(x,y)=>Math.Abs(x-y)).Max()>1e-4);
    }
    [TestMethod] public void DisconnectMeansIdentityAndDetachedNodesPersist()
    {
        var a=Node("曝光",1);var s=new ColorAdjustmentStack([a],ProcessingVersion:2);var g=StudioNodeGraph.FromSerial(s.Nodes);g=g.Disconnect(g.Edges.Last());using var input=Input();CollectionAssert.AreEqual(input.Rgb32.ToArray(),Render(s with{Graph=g}));Assert.AreEqual(1,(s with{Graph=g}).Normalize().Nodes.Count);
    }
    [TestMethod] public void InvalidPortsCycleAndBranchesRejected()
    {
        var a=Node("A",0);var b=Node("B",0);var nodes=new[]{a,b};var g=StudioNodeGraph.FromSerial(nodes);
        Assert.ThrowsExactly<InvalidDataException>(()=>(g with{Edges=[new(a.Id,"rgb",b.Id,"rgb"),new(b.Id,"rgb",a.Id,"rgb")]}).Freeze(nodes));
        Assert.ThrowsExactly<InvalidDataException>(()=>(g with{Edges=[new(StudioNodeGraph.Source,"mask",a.Id,"rgb")]}).Freeze(nodes));
        Assert.ThrowsExactly<InvalidDataException>(()=>(g with{Edges=[new(StudioNodeGraph.Source,"rgb",a.Id,"rgb"),new(StudioNodeGraph.Source,"rgb",b.Id,"rgb")]}).Freeze(nodes));
    }
    [TestMethod] public void InsertDeleteAndSerializationAreExplicit()
    {
        var a=Node("A",.4);var b=Node("B",-.3);var c=Node("C",.2);var s=new ColorAdjustmentStack([a,b,c],ProcessingVersion:2);var g=StudioNodeGraph.FromSerial(s.Nodes);g=g.Insert(c.Id,g.Edges.First(e=>e.From==a.Id));
        CollectionAssert.AreEqual(new[]{a.Id,c.Id,b.Id},g.RoutedIds(s.Nodes).ToArray());s=s with{Graph=g};var scheme=new ColorStudioSchemeV2(Guid.NewGuid(),"图",s,DateTimeOffset.UnixEpoch);var loaded=ColorStudioSchemeSerializer.Deserialize(ColorStudioSchemeSerializer.Serialize(scheme));CollectionAssert.AreEqual(Render(s),Render(loaded.Stack));
        var removed=s with{Nodes=[a,b],Graph=g.Reconcile(s.Nodes,[a,b])};CollectionAssert.AreEqual(new[]{a.Id,b.Id},removed.Graph!.RoutedIds(removed.Nodes).ToArray());
    }
    [TestMethod] public void LayoutPersistenceClampsInvalidViewport()
    {
        var layout=new ColorStudioLayout{R54GraphHeight=double.NaN,R54GraphZoom=20,R54GraphCollapsed=true};var loaded=layout.Normalize();Assert.AreEqual(216,loaded.R54GraphHeight);Assert.AreEqual(2,loaded.R54GraphZoom);Assert.IsTrue(loaded.R54GraphCollapsed);
    }
}
