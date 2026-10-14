using System.Diagnostics;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Models;
var nodeName=args[0];var mode=args[1];var path=args[2];const int width=513,height=257;
var source=new HighBitDepthImageBuffer(width,height,Enumerable.Range(0,width*height*3).Select(i=>(i%991)/991f).ToArray(),"16","sRGB");
ColorAdjustmentStackNode node=nodeName switch{
 "Details"=>new(Guid.NewGuid(),ColorStudioNodeType.Details,"Details",NumericParameters:new Dictionary<string,double>{{"clarity",15},{"structure",20},{"sharpen",30},{"luma_noise",12}}),
 "Structure"=>new(Guid.NewGuid(),ColorStudioNodeType.Develop,"Structure",NumericParameters:new Dictionary<string,double>{{"structure",25},{"detail",15}}),
 "Bloom"=>new(Guid.NewGuid(),ColorStudioNodeType.Film,"Bloom",FilmSettings:new(true,"PT-W01",BloomAmount:30,SpatialVersion:2)),
 "Halation"=>new(Guid.NewGuid(),ColorStudioNodeType.Film,"Halation",FilmSettings:new(true,"PT-W01",HalationAmount:30,SpatialVersion:2)),
 _=>new(Guid.NewGuid(),ColorStudioNodeType.ReferenceMatch,"ReferenceMatch")};
var visual=source.ToVisualRgb24();var analysis=VisualAnalysisEngine.Analyze(new(Guid.NewGuid(),"synthetic",visual));
var look=new ReferenceLook(Guid.NewGuid(),"synthetic",null,[new(Guid.NewGuid(),null,"Test","synthetic","hash",1,analysis)],new(),DateTimeOffset.UtcNow,DateTimeOffset.UtcNow);
var samples=new List<object>();var stop=new CancellationTokenSource();var gate=new object();var process=Process.GetCurrentProcess();var sampler=Task.Run(async()=>{while(!stop.IsCancellationRequested){process.Refresh();lock(gate)samples.Add(new{ms=process.TotalProcessorTime.TotalMilliseconds,managed=GC.GetTotalMemory(false),workingSet=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,ledger=StudioGlobalResources.AccountedBytes});await Task.Delay(10);}});
var plan=SpatialLegacyRawRenderer.Plan(source,node,67,64);var watch=Stopwatch.StartNew();HighBitDepthImageBuffer? result;
try{result=mode=="tile"?SpatialLegacyRawRenderer.RenderNode(source,node,nodeName=="ReferenceMatch"?look:null,tileWidth:67,tileHeight:64):new RawMatchTiff16ProductPipeline(new Never()).Render(source,nodeName=="ReferenceMatch"?look:null,new([node])).ProcessingPixels;}
catch(StudioBudgetException e){stop.Cancel();await sampler;File.WriteAllText(path,JsonSerializer.Serialize(new{nodeName,mode,status="BUDGET_REJECTED",error=e.Message}));return;}
watch.Stop();var measuredMs=watch.Elapsed.TotalMilliseconds;var resultValues=result!.Rgb32.ToArray();result=null;
GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Task.Delay(500);stop.Cancel();await sampler;
var oracle=new RawMatchTiff16ProductPipeline(new Never()).Render(source,nodeName=="ReferenceMatch"?look:null,new([node])).ProcessingPixels!;double max=0;for(int i=0;i<resultValues.Length;i++)max=Math.Max(max,Math.Abs(resultValues[i]-oracle.Rgb32.Span[i]));
File.WriteAllText(path,JsonSerializer.Serialize(new{nodeName,mode,status="PASS_SYNTHETIC_SMALL_ONLY",width,height,halo=plan.Halo,requiredLedgerBytes=plan.RequiredBytes,totalLimit=StudioGlobalResources.TotalLimit,milliseconds=measuredMs,maxAbsoluteError=max,oracle="Independent existing full renderer",samples,nativeAllocations="Not individually attributed; privateBytes minus managed is not native allocator authority",largeReal="NOT_RUN",releaseSeries="500ms forced-GC diagnostic only; not desktop memory return"},new JsonSerializerOptions{WriteIndented=true}));
sealed class Never:IRawDecoder{public RawDecoderCapability GetCapability()=>throw new NotSupportedException();public Task<RawDecodedImage> DecodeAsync(string path,RawToJpegOptions options,CancellationToken token=default)=>throw new NotSupportedException();}

