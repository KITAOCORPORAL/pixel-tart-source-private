using System.Collections.Concurrent;using System.Diagnostics;
namespace RAWSelectionAssistant.Services;
internal static class ViewingTableDiagnostics
{
 static readonly ConcurrentDictionary<string,(long Ticks,long Calls)> values=new();internal static bool Enabled=>Environment.GetEnvironmentVariable("KITAO_VIEWING_PROFILE")=="1";
 internal static IDisposable Measure(string phase)=>Enabled?new Scope(phase):Empty.Instance;
 sealed class Scope(string phase):IDisposable{readonly long start=Stopwatch.GetTimestamp();int retired;public void Dispose(){if(Interlocked.Exchange(ref retired,1)!=0)return;long elapsed=Stopwatch.GetTimestamp()-start;values.AddOrUpdate(phase,(elapsed,1),(_,v)=>(v.Ticks+elapsed,v.Calls+1));}}
 sealed class Empty:IDisposable{internal static readonly Empty Instance=new();public void Dispose(){}}
 internal static void Reset()=>values.Clear();internal static object Snapshot()=>values.OrderBy(p=>p.Key).Select(p=>new{phase=p.Key,milliseconds=p.Value.Ticks*1000d/Stopwatch.Frequency,calls=p.Value.Calls}).ToArray();
}
