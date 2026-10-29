using System.Runtime.InteropServices;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Core.Services.Color;
/// <summary>Exact-length, graph-local RGB backing reuse. Every retained backing array (including free
/// slots) keeps its lease. Closing the scope drops free slots; escaped output releases on Dispose.</summary>
public static class GraphPixelAllocation
{
    private static readonly AsyncLocal<Scope?> Current=new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<float[],Slot> Slots=new();
    private sealed class Slot(Scope owner,float[] values,IDisposable reservation)
    {public Scope Owner=owner;public float[] Values=values;public IDisposable? Reservation=reservation;public bool Claimed;}
    public sealed class Scope:IDisposable
    {
        private readonly Scope? prior;private readonly Dictionary<int,Stack<Slot>> free=[];private readonly List<Slot> all=[];private bool closed;
        internal Scope(){prior=Current.Value;Current.Value=this;}
        internal float[] Rent(int length)
        {
            lock(free){if(free.TryGetValue(length,out var list)&&list.Count>0){var slot=list.Pop();slot.Claimed=false;return slot.Values;}}
            var lease=StudioGlobalResources.ReserveWork(checked((long)length*sizeof(float)));try{var a=new float[length];var slot=new Slot(this,a,lease);Slots.Add(a,slot);lock(free)all.Add(slot);return a;}catch{lease.Dispose();throw;}
        }
        private void Return(Slot slot)
        {
            lock(free){if(!slot.Claimed)return;slot.Claimed=false;if(closed){slot.Reservation?.Dispose();slot.Reservation=null;return;}if(!free.TryGetValue(slot.Values.Length,out var list))free[slot.Values.Length]=list=new();list.Push(slot);}
        }
        public void Dispose(){Current.Value=prior;lock(free){closed=true;foreach(var slot in all.Where(x=>!x.Claimed)){slot.Reservation?.Dispose();slot.Reservation=null;}free.Clear();all.Clear();}}
        internal IDisposable? Claim(ReadOnlyMemory<float> values)
        {
            if(!MemoryMarshal.TryGetArray(values,out ArraySegment<float> a)||a.Array is null||a.Offset!=0||a.Count!=a.Array.Length||!Slots.TryGetValue(a.Array,out var slot))return null;
            lock(free){if(slot.Claimed)throw new InvalidOperationException("Graph backing already owned");slot.Claimed=true;return new ReturnLease(slot);}
        }
        private sealed class ReturnLease(Slot slot):IDisposable
        {private Slot? held=slot;public void Dispose(){var s=Interlocked.Exchange(ref held,null);if(s is not null)s.Owner.Return(s);}}
    }
    public static Scope Enter()=>new();
    private sealed class Borrowed:IDisposable{public void Dispose(){}}
    public static IDisposable EnterIfNeeded()=>Current.Value is null?new Scope():new Borrowed();
    public static float[] Allocate(int length)=>Current.Value?.Rent(length)??new float[length];
    public static float[] Copy(ReadOnlyMemory<float> values){var a=Allocate(values.Length);values.Span.CopyTo(a);return a;}
    internal static IDisposable? Claim(ReadOnlyMemory<float> values)=>Current.Value?.Claim(values);
}
