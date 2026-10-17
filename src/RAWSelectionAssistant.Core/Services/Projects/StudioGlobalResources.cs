namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Admission accounting; retained task leases are not released by cache eviction or replacement.</summary>
public static class StudioGlobalResources
{
    public const long MasterLimit=1024L*1024*1024,DetailLimit=192L*1024*1024,TotalLimit=2L*1024*1024*1024;
    private sealed class Entry(Guid owner,string kind,long bytes,Action evict,long stamp)
    {
        public Guid Owner=owner;public string Kind=kind;public long Bytes=bytes;public Action Evict=evict;public long Stamp=stamp;
        public int Pins;public bool Retired;
    }
    private static readonly object Gate=new();
    private static readonly Dictionary<(Guid,string),Entry> Entries=[];
    private static readonly HashSet<Entry> Retired=[];
    private static long stamp;
    private static long Total()=>Entries.Values.Sum(x=>x.Bytes)+Retired.Sum(x=>x.Bytes);
    public static bool Store(Guid owner,string kind,long bytes,Action evict)
    {
        if(bytes<0)throw new ArgumentOutOfRangeException(nameof(bytes));
        var limit=kind=="Master"?MasterLimit:kind=="Detail"?DetailLimit:TotalLimit;
        var callbacks=new List<Action>();bool admitted;
        lock(Gate)
        {
            if(Entries.Remove((owner,kind),out var previous)){previous.Retired=true;if(previous.Pins>0)Retired.Add(previous);}
            while(bytes<=limit&&(Entries.Values.Where(x=>x.Kind==kind).Sum(x=>x.Bytes)+Retired.Where(x=>x.Kind==kind).Sum(x=>x.Bytes)+bytes>limit||Total()+bytes>TotalLimit))
            {
                var oldest=Entries.Values.Where(x=>x.Kind!="Work"&&x.Pins==0).MinBy(x=>x.Stamp);if(oldest is null)break;
                Entries.Remove((oldest.Owner,oldest.Kind));oldest.Retired=true;callbacks.Add(oldest.Evict);
            }
            admitted=bytes<=limit&&Total()+bytes<=TotalLimit&&Entries.Values.Where(x=>x.Kind==kind).Sum(x=>x.Bytes)+Retired.Where(x=>x.Kind==kind).Sum(x=>x.Bytes)+bytes<=limit;
            if(admitted)Entries[(owner,kind)]=new(owner,kind,bytes,evict,++stamp);else callbacks.Add(evict);
        }
        foreach(var action in callbacks)action();return admitted;
    }
    public static IDisposable Pin(Guid owner,string kind)
    {
        lock(Gate){if(!Entries.TryGetValue((owner,kind),out var entry))return new Lease(null);entry.Pins++;return new Lease(entry);}
    }
    private sealed class Lease(Entry? entry):IDisposable
    {
        private Entry? held=entry;
        public void Dispose(){lock(Gate){var value=held;held=null;if(value is null)return;value.Pins--;if(value.Pins==0&&value.Retired)Retired.Remove(value);}}
    }
    private sealed class BufferEntry(IDisposable lease){public IDisposable Lease=lease;public int Count;}
    private static readonly Dictionary<object,BufferEntry> BufferReservations=new(ReferenceEqualityComparer.Instance);
    public static IDisposable RetainBuffer(object buffer,long bytes)
    {
        lock(Gate)
        {
            if(!BufferReservations.TryGetValue(buffer,out var entry))BufferReservations[buffer]=entry=new(ReserveWork(bytes));
            entry.Count++;return new BufferBorrow(buffer);
        }
    }
    private sealed class BufferBorrow(object buffer):IDisposable
    {
        private object? held=buffer;
        public void Dispose(){lock(Gate){var value=held;held=null;if(value is null)return;var entry=BufferReservations[value];if(--entry.Count==0){BufferReservations.Remove(value);entry.Lease.Dispose();}}}
    }
    public static IDisposable ReserveWork(long bytes)
    {
        var owner=Guid.NewGuid();if(!Store(owner,"Work",bytes,()=>{}))throw new StudioBudgetException(bytes,Math.Max(0,TotalLimit-AccountedBytes));return new Reservation(owner);
    }
    private sealed class Reservation(Guid owner):IDisposable{public void Dispose()=>Release(owner,"Work");}
    public static void Release(Guid owner,string kind)
    {
        lock(Gate){if(!Entries.Remove((owner,kind),out var entry))return;entry.Retired=true;if(entry.Pins>0)Retired.Add(entry);}
    }
    public static (long Masters,long Details,int Entries) Snapshot()
    {lock(Gate){var all=Entries.Values.Concat(Retired);return(all.Where(x=>x.Kind=="Master").Sum(x=>x.Bytes),all.Where(x=>x.Kind=="Detail").Sum(x=>x.Bytes),Entries.Count+Retired.Count);}}
    public static long AccountedBytes{get{lock(Gate)return Total();}}
}
