using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

if(args.Length<2)throw new ArgumentException("output root, mode: cold/warm3/published/correctness");
var output=Path.GetFullPath(args[0]);var mode=args[1];Directory.CreateDirectory(output);
var persistent=args.Length>2;var fixture=persistent?Path.GetFullPath(args[2]):Path.Combine(Path.GetTempPath(),"RAWSelectionAssistant.Tests","CompanySwitch-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
var service=new AssetLibraryContainerService();var onePath=Path.Combine(fixture,"A","中文 库一.ptlibrary");var twoPath=Path.Combine(fixture,"B","库二.ptlibrary");
var one=Directory.Exists(onePath)?await service.OpenAsync(onePath):await service.CreateAsync(onePath,"库一");var two=Directory.Exists(twoPath)?await service.OpenAsync(twoPath):await service.CreateAsync(twoPath,"库二");
// A reused fixture must have the same empty-cache initial state as Context.CreateAsync; only the diagnostic tool clears this private in-process cache.
if(persistent){var cache=typeof(AssetLibraryContainerService).GetField("ValidatedContainers",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;cache.GetType().GetMethod("Clear")!.Invoke(cache,null);}
var assembly=typeof(AssetLibraryContainerService).Assembly;var assemblyPath=assembly.Location;
var identity=new {mode,fixture,temp=Path.GetTempPath(),source=Environment.GetEnvironmentVariable("KITAO_PRODUCT_SOURCE_SHA"),assembly=assemblyPath,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assemblyPath))),one=one.LibraryId,two=two.LibraryId,onePath=one.ContainerPath,twoPath=two.ContainerPath,metadataOutsideSwitchStopwatch=true,thresholdMs=30000};
await File.WriteAllTextAsync(Path.Combine(output,"identity.json"),JsonSerializer.Serialize(identity,new JsonSerializerOptions{WriteIndented=true}));
var rounds=mode=="warm3"?3:1;if(mode=="warm3"){await service.OpenAsync(one.ContainerPath);await service.OpenAsync(two.ContainerPath);}
var all=new List<object>();
for(var repeat=1;repeat<=rounds;repeat++)
{
 var observations=new List<JsonElement>();IDisposable? observer=null;
 var diagnostics=assembly.GetType("RAWSelectionAssistant.Core.Services.AssetLibrary.AssetLibraryOpenDiagnostics");
 if(diagnostics is not null)
 {
  Action<string> callback=json=>{using var doc=JsonDocument.Parse(json);observations.Add(doc.RootElement.Clone());};
  observer=(IDisposable?)diagnostics.GetMethod("Observe")!.Invoke(null,[callback]);
 }
 var ticks=new long[10000];var ids=new Guid[10000];using var process=Process.GetCurrentProcess();process.Refresh();var cpu=process.TotalProcessorTime;var io=ReadIo(process.Handle);var allocated=GC.GetTotalAllocatedBytes(false);var collections=new[]{GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
 var watch=Stopwatch.StartNew();
 for(int index=0;index<10000;index++){var start=Stopwatch.GetTimestamp();var descriptor=index%2==0?await service.OpenAsync(one.ContainerPath):await service.OpenAsync(two.ContainerPath);ticks[index]=Stopwatch.GetTimestamp()-start;ids[index]=descriptor.LibraryId;}
 watch.Stop();var switchMs=watch.Elapsed.TotalMilliseconds;process.Refresh();var cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds;var endIo=ReadIo(process.Handle);var alloc=GC.GetTotalAllocatedBytes(false)-allocated;observer?.Dispose();
 var metadata=Stopwatch.StartNew();var estimates=Enumerable.Range(0,100000).Select(index=>(Id:index,Hash:(index%90000).ToString())).GroupBy(item=>item.Hash).Count();metadata.Stop();
 var ms=ticks.Select(t=>t*1000d/Stopwatch.Frequency).OrderBy(x=>x).ToArray();double Quantile(double q)=>ms[Math.Clamp((int)Math.Ceiling(q*ms.Length)-1,0,ms.Length-1)];
 var correct=ids.Select((id,index)=>id==(index%2==0?one.LibraryId:two.LibraryId)).All(x=>x);
 var result=new{repeat,mode,iterations=10000,totalMs=switchMs,thresholdMs=30000,thresholdPass=switchMs<30000,identityCorrect=correct,metadataRows=100000,unique=estimates,metadataMs=metadata.Elapsed.TotalMilliseconds,metadataInsideSwitchTimer=false,cpuMs,allocationBytes=alloc,io=Difference(io,endIo),p50Ms=Quantile(.50),p95Ms=Quantile(.95),p99Ms=Quantile(.99),p999Ms=Quantile(.999),maxMs=ms[^1],collections=new[]{GC.CollectionCount(0)-collections[0],GC.CollectionCount(1)-collections[1],GC.CollectionCount(2)-collections[2]},instrumented=observer is not null,physicalDiskWait="UNAVAILABLE: process I/O counters are logical transferred bytes, not physical disk latency",lockWait="No writer lease acquired by OpenAsync; SQLite waits included in cold SqliteOpen/commands only if diagnostics present"};all.Add(result);
 await File.WriteAllTextAsync(Path.Combine(output,$"round-{repeat}.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
 await File.WriteAllLinesAsync(Path.Combine(output,$"calls-{repeat}.csv"),new[]{"index,elapsed_ms,library_id"}.Concat(ticks.Select((t,index)=>$"{index},{(t*1000d/Stopwatch.Frequency).ToString("R",System.Globalization.CultureInfo.InvariantCulture)},{ids[index]}")));
 if(observations.Count>0)await File.WriteAllTextAsync(Path.Combine(output,$"stages-{repeat}.json"),JsonSerializer.Serialize(observations));Console.WriteLine(JsonSerializer.Serialize(result));
}
await File.WriteAllTextAsync(Path.Combine(output,"results.json"),JsonSerializer.Serialize(all,new JsonSerializerOptions{WriteIndented=true}));
await using(await service.AcquireWriteLeaseAsync(one)){}await using(await service.AcquireWriteLeaseAsync(one)){}await using(await service.AcquireWriteLeaseAsync(two)){}
if(!persistent)Directory.Delete(fixture,true);
static Io? ReadIo(IntPtr h)=>GetProcessIoCounters(h,out var io)?io:null;
static object Difference(Io? a,Io? b)=>a is{} x&&b is{} y?new{readOps=y.ReadOperationCount-x.ReadOperationCount,writeOps=y.WriteOperationCount-x.WriteOperationCount,otherOps=y.OtherOperationCount-x.OtherOperationCount,readBytes=y.ReadTransferCount-x.ReadTransferCount,writeBytes=y.WriteTransferCount-x.WriteTransferCount,otherBytes=y.OtherTransferCount-x.OtherTransferCount}:new{unavailable=true};
[DllImport("kernel32.dll",SetLastError=true)]static extern bool GetProcessIoCounters(IntPtr h,out Io io);
[StructLayout(LayoutKind.Sequential)]struct Io{public ulong ReadOperationCount,WriteOperationCount,OtherOperationCount,ReadTransferCount,WriteTransferCount,OtherTransferCount;}
