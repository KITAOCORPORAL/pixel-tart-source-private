using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Tests;
[TestClass][DoNotParallelize]
public class NodeProxyResponseTests
{
 [TestMethod]public void RiskAdmissionSeparatesHighFrequencyFineMasksGeometryAndSpatialTools()
 {
  var node=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"tone",NumericParameters:new Dictionary<string,double>{{"exposure",.2}});var d=new ProfessionalRawDefinition{LocalEdits=new(new([node],ProcessingVersion:2))};Assert.IsTrue(NodeProxySafety.CanApproximate(d,out _));Assert.IsFalse(NodeProxySafety.CanApproximate(d with{Geometry=new(QuarterTurns:1)},out _));Assert.IsFalse(NodeProxySafety.CanApproximate(d with{LocalEdits=new(new([node with{Type=ColorStudioNodeType.Details}],ProcessingVersion:2))},out _));Assert.IsFalse(NodeProxySafety.CanApproximate(d with{LocalEdits=new(new([node with{CompositeVersion=1,LocalMask=new([new([new(.5,.5)],.002,0,1,1)])}],ProcessingVersion:2))},out _));using var edge=new HighBitDepthImageBuffer(128,16,Enumerable.Range(0,128*16).SelectMany(i=>i%2==0?new[]{1f,1f,1f}:new[]{0f,0f,0f}).ToArray());Assert.IsTrue(NodeProxySafety.SourceHasHighFrequencyBoundary(edge));using var vertical=new HighBitDepthImageBuffer(8,128,Enumerable.Range(0,8*128).SelectMany(i=>i/8%2==0?new[]{1f,1f,1f}:new[]{0f,0f,0f}).ToArray());Assert.IsTrue(NodeProxySafety.SourceHasHighFrequencyBoundary(vertical));
 }
 [TestMethod]public async Task LatestOnlyCancelsAndRejectsStaleResultEvenIfRendererIgnoresToken()
 {
  var results=new List<int>();var started=new TaskCompletionSource();var gate=new TaskCompletionSource();var q=new LatestPreviewQueue<int,int>(async(v,t)=>{if(v==1){started.SetResult();await gate.Task;}return v;},(_,_,value)=>results.Add(value),latestOnly:true);q.Submit(1);await started.Task;q.Submit(2);q.Submit(3);gate.SetResult();await q.Work;CollectionAssert.AreEqual(new[]{3},results);
 }
 [TestMethod]public void InteractiveDependencyCacheIsExactInvalidatesByInputAndReleases()
 {
  var a=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.Preset,"A",NumericParameters:new Dictionary<string,double>{{"exposure",.1}});var b=a with{Id=Guid.NewGuid(),Name="B"};var s=new ColorAdjustmentStack([a,b],ProcessingVersion:2){Graph=StudioNodeGraph.FromSerial([a,b]) with{Version=2}};using var src=new HighBitDepthImageBuffer(128,80,Enumerable.Repeat(.3f,128*80*3).ToArray());var ledger=StudioGlobalResources.AccountedBytes;using(var cache=new GraphInteractiveCache()){int calls=0;HighBitDepthImageBuffer Run(ColorAdjustmentStack stack,string context)=>StudioGraphRenderer.Render(src,stack,(i,n)=>{calls++;return ColorStudioRenderPipeline.ApplyPointEffect(i,n);},interactiveCache:cache,interactiveContext:context);using var first=Run(s,"inputA");using var second=Run(s,"inputA");CollectionAssert.AreEqual(first.Rgb32.ToArray(),second.Rgb32.ToArray());Assert.AreEqual(3,calls,"upstream A reused, final output B intentionally not cached");using var changed=Run(s with{Nodes=[a,b with{NumericParameters=new Dictionary<string,double>{{"exposure",.2}}}]},"inputA");Assert.AreEqual(4,calls);using var inputChanged=Run(s,"inputB");Assert.AreEqual(6,calls);Assert.IsTrue(cache.Hits>=2);cache.Clear();}Assert.AreEqual(ledger,StudioGlobalResources.AccountedBytes);
 }
 [TestMethod]public void CacheBypassesFinalResolutionAndInvalidatesMaskGeometryAndSource()
 {
  var node=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.Preset,"A",NumericParameters:new Dictionary<string,double>{{"exposure",.1}});var final=node with{Id=Guid.NewGuid(),Name="B"};var stack=new ColorAdjustmentStack([node,final],ProcessingVersion:2){Graph=StudioNodeGraph.FromSerial([node,final]) with{Version=2}};
  using var source=new HighBitDepthImageBuffer(801,2,Enumerable.Repeat(.25f,801*2*3).ToArray());using var cache=new GraphInteractiveCache();int calls=0;HighBitDepthImageBuffer Run()=>StudioGraphRenderer.Render(source,stack,(i,n)=>{calls++;return ColorStudioRenderPipeline.ApplyPointEffect(i,n);},interactiveCache:cache,interactiveContext:"same");using var a=Run();using var b=Run();Assert.AreEqual(4,calls);Assert.AreEqual(0,cache.Hits);
  var key=GraphInteractiveCache.Key("source1/profile1/geometry1",node,["base"]);Assert.AreNotEqual(key,GraphInteractiveCache.Key("source2/profile1/geometry1",node,["base"]));Assert.AreNotEqual(key,GraphInteractiveCache.Key("source1/profile2/geometry1",node,["base"]));Assert.AreNotEqual(key,GraphInteractiveCache.Key("source1/profile1/geometry2",node,["base"]));Assert.AreNotEqual(key,GraphInteractiveCache.Key("source1/profile1/geometry1",node with{LocalMask=new([new([new(.5,.5)],.3,.5,1,1)])},["base"]));
 }
 [TestMethod]public void StrongCurvesAndLevelsFallBackWithoutChangingNumericalBudget()
 {
  foreach(var n in new[]{new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.Curve,"curve",NumericParameters:new Dictionary<string,double>{{"rgb_y2",.9}}),new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.Levels,"levels",NumericParameters:new Dictionary<string,double>{{"rgb_gamma",2}})})Assert.IsFalse(NodeProxySafety.CanApproximate(new ProfessionalRawDefinition{LocalEdits=new(new([n],ProcessingVersion:2))},out _));
  Assert.AreEqual(1.5,NodeProxySafety.MeanBudget);Assert.AreEqual(4,NodeProxySafety.P95Budget);Assert.AreEqual(32,NodeProxySafety.MaximumBudget);
 }

 [TestMethod]public void CancelledGraphCacheStaysBoundedAndRetiredGenerationCannotPublish()
 {
  var before=StudioGlobalResources.AccountedBytes;using(var cache=new GraphInteractiveCache()){using var input=new HighBitDepthImageBuffer(32,16,Enumerable.Repeat(.2f,32*16*3).ToArray());var generation=cache.Generation;for(int i=0;i<12;i++)cache.Store("item"+i,input,generation);Assert.IsNull(cache.Acquire("item0"));using(var lease=cache.Acquire("item11")){Assert.IsNotNull(lease);cache.Clear();Assert.AreEqual(.2f,lease.Buffer.Rgb32.Span[0]);}Assert.ThrowsExactly<OperationCanceledException>(()=>cache.Store("late",input,generation));
  var nodes=Enumerable.Range(0,4).Select(i=>new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.Preset,"n"+i)).ToArray();var stack=new ColorAdjustmentStack(nodes,ProcessingVersion:2){Graph=StudioNodeGraph.FromSerial(nodes) with{Version=2}};using var cancel=new CancellationTokenSource();int calls=0;Assert.ThrowsExactly<OperationCanceledException>(()=>{using var output=StudioGraphRenderer.Render(input,stack,(i,n)=>{if(++calls==2)cancel.Cancel();return i.Clone();},cancel.Token,interactiveCache:cache,interactiveContext:"cancel");});cache.Clear();}Assert.AreEqual(before,StudioGlobalResources.AccountedBytes);
 }

}
