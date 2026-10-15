using System.Diagnostics;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Models;
var nodeName=args[0];var mode=args[1];var path=args[2];
var sourcePath=args[3];var expected=args[4];using(var input=File.OpenRead(sourcePath)){var actual=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input));if(!actual.Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Authorized source hash mismatch");}
var decoded=await new RawMatchTiff16ProductPipeline(new LibRawDecoder()).DecodeFrozenMasterAsync(sourcePath);var source=RawImageOrientation.NormalizePixels(decoded.Image);decoded=null;
var width=source.Width;var height=source.Height;
ColorAdjustmentStackNode node=nodeName switch{
 "Details"=>new(Guid.NewGuid(),ColorStudioNodeType.Details,"Details",NumericParameters:new Dictionary<string,double>{{"clarity",15},{"structure",20},{"sharpen",30},{"luma_noise",12}}),
 "Structure"=>new(Guid.NewGuid(),ColorStudioNodeType.Develop,"Structure",NumericParameters:new Dictionary<string,double>{{"structure",25},{"detail",15}}),
 "Bloom"=>new(Guid.NewGuid(),ColorStudioNodeType.Film,"Bloom",FilmSettings:new(true,"PT-W01",BloomAmount:30,SpatialVersion:2)),
 "Halation"=>new(Guid.NewGuid(),ColorStudioNodeType.Film,"Halation",FilmSettings:new(true,"PT-W01",HalationAmount:30,SpatialVersion:2)),
 _=>new(Guid.NewGuid(),ColorStudioNodeType.ReferenceMatch,"ReferenceMatch")};
var visual=source.ToVisualRgb24();var analysis=VisualAnalysisEngine.Analyze(new(Guid.NewGuid(),"synthetic",visual));
var look=new ReferenceLook(Guid.NewGuid(),"synthetic",null,[new(Guid.NewGuid(),null,"Test","synthetic","hash",1,analysis)],new(),DateTimeOffset.UtcNow,DateTimeOffset.UtcNow);
var samples=new List<object>();var stop=new CancellationTokenSource();var gate=new object();var process=Process.GetCurrentProcess();var clock=Stopwatch.StartNew();var stage="baseline";
var sampler=Task.Run(async()=>{while(!stop.IsCancellationRequested){process.Refresh();lock(gate)samples.Add(new{wallMs=clock.Elapsed.TotalMilliseconds,stage,managed=GC.GetTotalMemory(false),workingSet=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,ledger=StudioGlobalResources.AccountedBytes});await Task.Delay(25);}});
await Task.Delay(500);var plan=SpatialLegacyRawRenderer.Plan(source,node,512,128);var watch=Stopwatch.StartNew();HighBitDepthImageBuffer? result=null;var status="MEASURED";string? error=null;
try{
 stage="render";
 if(mode=="cancel"){using var cancellation=new CancellationTokenSource();cancellation.CancelAfter(100);try{result=SpatialLegacyRawRenderer.RenderNode(source,node,nodeName=="ReferenceMatch"?look:null,token:cancellation.Token,tileWidth:512,tileHeight:128);status="COMPLETED_BEFORE_CANCEL";}catch(OperationCanceledException){status="CANCELLED";}}
 else result=mode=="tile"?SpatialLegacyRawRenderer.RenderNode(source,node,nodeName=="ReferenceMatch"?look:null,tileWidth:512,tileHeight:128):new RawMatchTiff16ProductPipeline(new Never()).Render(source,nodeName=="ReferenceMatch"?look:null,new([node])).ProcessingPixels;
}catch(StudioBudgetException e){status="BUDGET_REJECTED";error=e.Message;}catch(Exception e){status="FAILED";error=e.ToString();}
watch.Stop();var measuredMs=watch.Elapsed.TotalMilliseconds;string? pixelHash=null;double min=0,max=0;
if(result is not null){pixelHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(result.Rgb32.Span)));min=result.Rgb32.Span.Length==0?0:result.Rgb32.Span[0];max=min;foreach(var value in result.Rgb32.Span){min=Math.Min(min,value);max=Math.Max(max,value);}}
result=null;stage="natural-release-no-GC";await Task.Delay(3000);stop.Cancel();await sampler;
using(var input=File.OpenRead(sourcePath)){if(!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input)).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("source modified");}
File.WriteAllText(path,JsonSerializer.Serialize(new{nodeName,mode,status,error,sourcePath,sourceSha256=expected,width,height,parameters=node,plan,milliseconds=measuredMs,pixelHash,min,max,samples,nativeAllocations="UNAVAILABLE allocator attribution; working set/private bytes sampled, not native allocation ownership",releaseSeries="3s natural, no forced GC; source retained as baseline",oracle=mode=="full"?"existing full renderer":"separate same-input full run required",productionChanged=false},new JsonSerializerOptions{WriteIndented=true}));
sealed class Never:IRawDecoder{public RawDecoderCapability GetCapability()=>throw new NotSupportedException();public Task<RawDecodedImage> DecodeAsync(string path,RawToJpegOptions options,CancellationToken token=default)=>throw new NotSupportedException();}
