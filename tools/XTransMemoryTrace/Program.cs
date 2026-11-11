using System.Text.Json;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
if(args.Length!=2)return 2;
var output=Path.GetFullPath(args[1]);if(File.Exists(output))throw new IOException("Preserve allocation report");
var etlx=TraceLog.CreateFromEventPipeDataFile(Path.GetFullPath(args[0]));
using var log=new TraceLog(etlx);
var allocations=new List<object>();var totals=new Dictionary<string,(long Count,long SampledBytes,long MaxObject)>();long missingStacks=0;
foreach(var e in log.Events)
{
 if(e is not GCAllocationTickTraceData a)continue;
 var stack=new List<string>();var call=e.CallStack();while(call is not null&&stack.Count<50){var ca=call.CodeAddress;stack.Add(ca.Method?.FullMethodName??ca.ToString());call=call.Caller;}
 if(stack.Count==0)missingStacks++;
 long size=0;try{size=Convert.ToInt64(a.PayloadByName("ObjectSize"));}catch{}
 var key=a.TypeName??"unknown";var previous=totals.GetValueOrDefault(key);totals[key]=(previous.Count+1,previous.SampledBytes+a.AllocationAmount64,Math.Max(previous.MaxObject,size));
 if(size>1024*1024||a.AllocationAmount64>16*1024*1024)allocations.Add(new{utc=e.TimeStamp.ToUniversalTime().ToString("O"),relativeMs=e.TimeStampRelativeMSec,e.ProcessID,e.ThreadID,type=a.TypeName,kind=a.AllocationKind.ToString(),objectSize=size,sampledAllocationAmount=a.AllocationAmount64,stack});
}
var report=new{source=Path.GetFileName(args[0]),profile="gc-verbose",traceEventPackage="3.1.23",authority="Sampled GCAllocationTick events; sampled weights are not a complete allocation census",missingStacks,totals=totals.OrderByDescending(p=>p.Value.SampledBytes).Select(p=>new{type=p.Key,count=p.Value.Count,sampledBytes=p.Value.SampledBytes,maxObjectSize=p.Value.MaxObject}),largeAllocations=allocations};
File.WriteAllText(output,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true})+"\n");Console.WriteLine(JsonSerializer.Serialize(new{types=totals.Count,largeSamples=allocations.Count,missingStacks}));return 0;
