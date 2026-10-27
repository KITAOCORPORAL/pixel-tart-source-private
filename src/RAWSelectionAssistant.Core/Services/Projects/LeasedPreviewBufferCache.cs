using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Cache owns buffers; eviction retires ownership, final borrower releases bytes and Dispose.</summary>
public sealed class LeasedPreviewBufferCache(int capacity=2):IDisposable
{
 private sealed class Entry(HighBitDepthImageBuffer buffer,IDisposable reservation){public HighBitDepthImageBuffer Buffer=buffer;public IDisposable Reservation=reservation;public int Readers;public bool Retired;}
 private long generation;private bool disposed;public long Generation{get{lock(gate)return generation;}}
 private readonly object gate=new();private readonly Dictionary<string,Entry> entries=[];
 public sealed class Lease:IDisposable{private Action? release;public HighBitDepthImageBuffer Buffer{get;}internal Lease(HighBitDepthImageBuffer buffer,Action release){Buffer=buffer;this.release=release;}public void Dispose()=>Interlocked.Exchange(ref release,null)?.Invoke();}
 public Lease? Acquire(string key){lock(gate){if(!entries.TryGetValue(key,out var e))return null;return Borrow(e);}}
 public Lease StoreAndAcquire(string key,HighBitDepthImageBuffer buffer,long? expectedGeneration=null){lock(gate){if(disposed||expectedGeneration is{} expected&&expected!=generation){buffer.Dispose();throw new OperationCanceledException("Preview cache context retired");}if(entries.TryGetValue(key,out var existing)){buffer.Dispose();return Borrow(existing);}IDisposable reservation;try{reservation=StudioGlobalResources.ReserveWork((long)buffer.Rgb32.Length*4);}catch{buffer.Dispose();throw;}if(entries.Count>=capacity){var first=entries.First();entries.Remove(first.Key);Retire(first.Value);}var e=new Entry(buffer,reservation);entries.Add(key,e);return Borrow(e);}}
 private Lease Borrow(Entry e){e.Readers++;return new(e.Buffer,()=>{lock(gate){e.Readers--;if(e.Retired&&e.Readers==0)Release(e);}});}
 private static void Release(Entry e){e.Buffer.Dispose();e.Reservation.Dispose();}
 private static void Retire(Entry e){e.Retired=true;if(e.Readers==0)Release(e);}
 public void Clear(){lock(gate){generation++;foreach(var e in entries.Values)Retire(e);entries.Clear();}}
 public void Dispose(){lock(gate){disposed=true;Clear();}}
}
