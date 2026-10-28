using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Services.Export;
namespace RAWSelectionAssistant.Tests;
[TestClass][DoNotParallelize]public sealed class R54HomeStabilityTests
{
 public static ColorAdjustmentStack Graph(string kind)
 {
  ColorAdjustmentStackNode Tool(int i)=>new(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"曝光"+i,NumericParameters:new Dictionary<string,double>{{"exposure",.02*(i+1)}});
  var nodes=new List<ColorAdjustmentStackNode>();var edges=new List<StudioGraphEdge>();Guid previous=StudioNodeGraph.Source;
  if(kind=="serial"){for(int i=0;i<10;i++){var n=Tool(i);nodes.Add(n);edges.Add(new(previous,"rgb",n.Id,"rgb"));previous=n.Id;}}
  else {var ancestor=Tool(0);nodes.Add(ancestor);edges.Add(new(previous,"rgb",ancestor.Id,"rgb"));previous=ancestor.Id;for(int i=0;i<(kind=="shared"?1:5);i++){var a=Tool(i+1);var b=Tool(i+2);var mix=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.GraphMix,"混合"+i){CompositeVersion=1,Opacity=.4};nodes.AddRange([a,b,mix]);edges.AddRange([new(previous,"rgb",a.Id,"rgb"),new(ancestor.Id,"rgb",b.Id,"rgb"),new(a.Id,"rgb",mix.Id,"base"),new(b.Id,"rgb",mix.Id,"layer")]);previous=mix.Id;}}
  edges.Add(new(previous,"rgb",StudioNodeGraph.Output,"rgb"));return new(nodes,ProcessingVersion:2){Graph=StudioNodeGraph.FromSerial(nodes) with{Version=2,Edges=edges}};
 }
 [TestMethod]public async Task AuthorizedTwentyMpGraphMemoryAndFullExport()
 {
  var output=Environment.GetEnvironmentVariable("KITAO_R54_LARGE");if(output is null){Assert.Inconclusive("Explicit household large-image run");return;}Directory.CreateDirectory(output);var path="N:/pixart/RAW/Real/canon/TNAN8886.CR3";Assert.AreEqual("B7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401",Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path))));var pipe=new RawMatchTiff16ProductPipeline(new LibRawDecoder());var clock=Stopwatch.StartNew();using var source=await pipe.DecodeMasterAsync(path);var decodeMs=clock.Elapsed.TotalMilliseconds;Assert.IsTrue((long)source.Width*source.Height>=20000000);var rows=new List<object>();
  foreach(var kind in new[]{"serial","shared","mix-chain"})
  {
   var stack=Graph(kind);long peak=0,accounted=0;using var cts=new CancellationTokenSource();var sample=Task.Run(async()=>{while(!cts.IsCancellationRequested){using var p=Process.GetCurrentProcess();peak=Math.Max(peak,p.WorkingSet64);accounted=Math.Max(accounted,StudioGlobalResources.AccountedBytes);try{await Task.Delay(10,cts.Token);}catch(OperationCanceledException){}}});
   string? error=null;double? first=null,fine=null,export=null;int applied=0;StudioGraphRenderer.Metrics? metrics=null;try{clock.Restart();using(var proxy=pipe.PreviewMaster(source,800)){using var preview=StudioGraphRenderer.Render(proxy,stack,(input,n)=>{applied++;return ColorStudioToolProcessor.Apply(input,n);});first=clock.Elapsed.TotalMilliseconds;}
   clock.Restart();using(var result=StudioGraphRenderer.Render(source,stack,(input,n)=>ColorStudioToolProcessor.Apply(input,n),observed:m=>metrics=m)){fine=clock.Elapsed.TotalMilliseconds;var file=Path.Combine(output,kind+".tif");clock.Restart();await AtomicTiffWriter.WriteRgb48Async(file,result,new(Software:"Guangjing R54 household graph stability"));export=clock.Elapsed.TotalMilliseconds;using var stream=File.OpenRead(file);var info=TiffStreamValidator.Validate(stream);Assert.AreEqual(source.Width,info.Width);Assert.AreEqual(source.Height,info.Height);Assert.AreEqual(16,info.BitsPerSample);foreach(var(x,y)in new[]{(0,0),(source.Width/2,source.Height/2),(source.Width-1,source.Height-1)}){using var one=new HighBitDepthImageBuffer(1,1,source.Rgb32.Slice((y*source.Width+x)*3,3).ToArray());using var expected=StudioGraphRenderer.Render(one,stack,(input,n)=>ColorStudioToolProcessor.Apply(input,n),tile:new(x,y,source.Width,source.Height));for(int c=0;c<3;c++)Assert.AreEqual(expected.Rgb32.Span[c],result.Rgb32.Span[(y*source.Width+x)*3+c],0);}}}
   catch(StudioBudgetException e){error=e.ToString();}
   finally{cts.Cancel();await sample;GC.Collect();GC.WaitForPendingFinalizers();}
   rows.Add(new{kind,decodeMs,source.Width,source.Height,nodeCount=stack.Nodes.Count,firstMs=first,fineMs=fine,fullEncodeValidateMs=export,peakWorkingSet=peak,peakAccounted=accounted,afterAccounted=StudioGlobalResources.AccountedBytes,error,effectCallsAtProxy=applied,metrics,sampledFullGraphPixelsExact=error is null});
  }
  await File.WriteAllTextAsync(Path.Combine(output,"RESULT.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
  if(Environment.GetEnvironmentVariable("KITAO_R54_REQUIRE_LARGE_SUCCESS")=="1")Assert.IsTrue(rows.All(row=>System.Text.Json.JsonSerializer.SerializeToElement(row).GetProperty("error").ValueKind==JsonValueKind.Null),"Final large rendering/export must all succeed; raw failures retained");
 }
 [TestMethod]public void FrontierExactnessSharedAncestorCancellationExceptionAndSourceOwnership()
 {
  using var source=new HighBitDepthImageBuffer(24,16,Enumerable.Range(0,24*16*3).Select(i=>(i%157)/200f).ToArray());var original=source.Rgb32.ToArray();
  foreach(var kind in new[]{"serial","shared","mix-chain"})
  {
   var stack=Graph(kind);int calls=0;StudioGraphRenderer.Metrics? metrics=null;
   using var actual=StudioGraphRenderer.Render(source,stack,(input,n)=>{calls++;return ColorStudioToolProcessor.Apply(input,n);},observed:m=>metrics=m);
   var cache=new Dictionary<Guid,HighBitDepthImageBuffer>{{StudioNodeGraph.Source,source}};
   try{foreach(var id in stack.Graph!.RoutedIds(stack.Nodes)){var n=stack.Nodes.Single(n=>n.Id==id);var edges=stack.Graph.Edges.Where(e=>e.To==id).ToArray();var input=cache[edges.Single(e=>e.Input==(n.Type==ColorStudioNodeType.GraphMix?"base":"rgb")).From];cache[id]=n.Type==ColorStudioNodeType.GraphMix?StudioLayerComposite.Apply(input,cache[edges.Single(e=>e.Input=="layer").From],n):ColorStudioToolProcessor.Apply(input,n);}var output=cache[stack.Graph.Edges.Single(e=>e.To==StudioNodeGraph.Output).From];CollectionAssert.AreEqual(output.Rgb32.ToArray(),actual.Rgb32.ToArray());}
   finally{foreach(var pair in cache)if(pair.Key!=StudioNodeGraph.Source)pair.Value.Dispose();}
   Assert.AreEqual(stack.Nodes.Count(n=>n.Type!=ColorStudioNodeType.GraphMix),calls);Assert.IsTrue(metrics!.PeakRetainedNodeBuffers<=4);Assert.AreEqual(0,StudioGlobalResources.AccountedBytes);
   for(int cycle=0;cycle<3;cycle++){using var cancelled=new CancellationTokenSource();Assert.Throws<OperationCanceledException>(()=>StudioGraphRenderer.Render(source,stack,(input,n)=>{cancelled.Cancel();cancelled.Token.ThrowIfCancellationRequested();return input.Clone();},cancelled.Token));Assert.AreEqual(0,StudioGlobalResources.AccountedBytes);Assert.Throws<IOException>(()=>StudioGraphRenderer.Render(source,stack,(input,n)=>throw new IOException("synthetic effect failure")));Assert.AreEqual(0,StudioGlobalResources.AccountedBytes);CollectionAssert.AreEqual(original,source.Rgb32.ToArray());}
  }
 }
 [TestMethod]public void LargeHaloZeroBandsMatchPointSamplesMasksAndGroupExactly()
 {
  int w=1600,h=1300;using var src=new HighBitDepthImageBuffer(w,h,Enumerable.Range(0,w*h*3).Select(i=>(i%113)/150f).ToArray());var stack=Graph("mix-chain");stack=stack with{Nodes=stack.Nodes.Select((n,i)=>i==0?n with{CompositeVersion=1,Opacity=.65,LocalMask=new([new([new(.2,.2),new(.8,.7)],.2,.4,.7,.8)])}:n).ToArray(),Graph=stack.Graph! with{GroupAmount=.7}};
  using var full=StudioGraphRenderer.Render(src,stack,(input,n)=>ColorStudioToolProcessor.Apply(input,n));
  foreach(var(x,y)in new[]{(0,0),(400,900),(1599,1299)}){using var pixel=new HighBitDepthImageBuffer(1,1,src.Rgb32.Slice((y*w+x)*3,3).ToArray());using var expected=StudioGraphRenderer.Render(pixel,stack,(input,n)=>ColorStudioToolProcessor.Apply(input,n),tile:new(x,y,w,h));for(int c=0;c<3;c++)Assert.AreEqual(expected.Rgb32.Span[c],full.Rgb32.Span[(y*w+x)*3+c],0);}
  Assert.AreEqual(0,StudioGlobalResources.AccountedBytes);
  using var cancelled=new CancellationTokenSource();Assert.Throws<OperationCanceledException>(()=>StudioGraphRenderer.Render(src,stack,(input,n)=>{cancelled.Cancel();cancelled.Token.ThrowIfCancellationRequested();return input.Clone();},cancelled.Token));Assert.AreEqual(0,StudioGlobalResources.AccountedBytes);
  Assert.Throws<IOException>(()=>StudioGraphRenderer.Render(src,stack,(input,n)=>throw new IOException("band effect failed")));Assert.AreEqual(0,StudioGlobalResources.AccountedBytes);Assert.AreEqual(w*h*3,src.Rgb32.Length);
 }
 [TestMethod]public void OriginalFrameNearestQualityAuditDoesNotChangeFrozenV1()
 {
  var output=Environment.GetEnvironmentVariable("KITAO_R54_FRAME_AUDIT");if(output is null){Assert.Inconclusive("Explicit quality audit output required");return;}Directory.CreateDirectory(output);var rows=new List<object>();
  foreach(var frame in OriginalFrameLibrary.Frames)
  {
   var art=OriginalFrameLibrary.Find(frame.Id);var rgba=OriginalFrameLibrary.Load(art);int transparent=0,partial=0,edge=0;for(int y=0;y<art.Height;y++)for(int x=0;x<art.Width;x++){int i=(y*art.Width+x)*4;byte a=rgba[i+3];if(a==0)transparent++;else if(a<255)partial++;if(x+1<art.Width&&a!=rgba[i+7])edge++;}
   int w=128,h=Math.Max(1,(int)Math.Round(128d*art.Height/art.Width));var nearest=new double[w*h*4];var bilinear=new double[nearest.Length];
   for(int y=0;y<h;y++)for(int x=0;x<w;x++)
   {
    double fx=(x+.5)*art.Width/w-.5,fy=(y+.5)*art.Height/h-.5;int x0=(int)Math.Floor(fx),y0=(int)Math.Floor(fy);double dx=fx-x0,dy=fy-y0;int n=(Math.Clamp((int)((y+.5)*art.Height/h),0,art.Height-1)*art.Width+Math.Clamp((int)((x+.5)*art.Width/w),0,art.Width-1))*4;
    for(int c=0;c<4;c++){nearest[(y*w+x)*4+c]=c==3?rgba[n+3]/255d:rgba[n+c]/255d*rgba[n+3]/255d;double Read(int xx,int yy){int i=(Math.Clamp(yy,0,art.Height-1)*art.Width+Math.Clamp(xx,0,art.Width-1))*4;return c==3?rgba[i+3]/255d:rgba[i+c]/255d*rgba[i+3]/255d;}bilinear[(y*w+x)*4+c]=(Read(x0,y0)*(1-dx)+Read(x0+1,y0)*dx)*(1-dy)+(Read(x0,y0+1)*(1-dx)+Read(x0+1,y0+1)*dx)*dy;}
   }
   var diff=nearest.Zip(bilinear,(a,b)=>Math.Abs(a-b)).ToArray();rows.Add(new{frame.Id,art.Width,art.Height,art.Sha256,transparent,partialAlpha=partial,horizontalAlphaEdges=edge,shrinkWidth=w,shrinkHeight=h,nearestVsPremultipliedBilinearMean=diff.Average(),nearestVsPremultipliedBilinearMax=diff.Max(),rendererVersion=1,changed=false,note="Quality diagnostic not calibrated ground truth; no straight-RGB interpolation across transparent edges; V1 nearest kept frozen"});
  }
  File.WriteAllText(Path.Combine(output,"RESULT.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
 }

}
