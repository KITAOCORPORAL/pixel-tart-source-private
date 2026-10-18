using System.Collections.Concurrent;
using System.Diagnostics;
namespace Kitao.InstantFilm.Research;
/// <summary>Opt-in sampled CPU stage attribution, not a benchmark or production scheduler.</summary>
public sealed class CandidatePerformanceProfile:IDisposable
{
 private static readonly AsyncLocal<CandidatePerformanceProfile?> Slot=new();
 private readonly CandidatePerformanceProfile? previous;
 private readonly ConcurrentDictionary<string,(long Ticks,long Samples)> totals=new();
 public static bool Enabled=>Slot.Value is not null;
 public CandidatePerformanceProfile(){previous=Slot.Value;Slot.Value=this;}
 public static void Add(string name,long start){if(Slot.Value is{} owner){long ticks=Stopwatch.GetTimestamp()-start;owner.totals.AddOrUpdate(name,(ticks,1),(_,v)=>(v.Ticks+ticks,v.Samples+1));}}
 public object[] Results()=>totals.OrderBy(x=>x.Key).Select(x=>(object)new{stage=x.Key,elapsedMs=x.Value.Ticks*1000d/Stopwatch.Frequency,samples=x.Value.Samples}).ToArray();
 public void Dispose()=>Slot.Value=previous;
}
