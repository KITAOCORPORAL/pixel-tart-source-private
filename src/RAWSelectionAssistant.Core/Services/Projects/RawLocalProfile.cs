using System.Collections.Concurrent;
using System.Diagnostics;
namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Opt-in phase timings; no per-pixel logging, not a scientific or native latency metric.</summary>
public static class RawLocalProfile
{
 private static readonly ConcurrentDictionary<string,(long Ticks,long Calls)> totals=new();
 public static bool Enabled=>Environment.GetEnvironmentVariable("KITAO_RAW_LOCAL_PROFILE")=="1";
 public static long Begin()=>Enabled?Stopwatch.GetTimestamp():0;
 public static void End(string phase,long started){if(started==0)return;var elapsed=Stopwatch.GetTimestamp()-started;totals.AddOrUpdate(phase,(elapsed,1),(_,old)=>(old.Ticks+elapsed,old.Calls+1));}
 public static void Reset()=>totals.Clear();
 public static object Snapshot()=>totals.OrderBy(x=>x.Key).Select(x=>new{phase=x.Key,milliseconds=x.Value.Ticks*1000d/Stopwatch.Frequency,calls=x.Value.Calls}).ToArray();
}
