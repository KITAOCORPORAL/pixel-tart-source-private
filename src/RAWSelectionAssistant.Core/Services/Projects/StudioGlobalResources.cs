namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Application-wide admission accounting, not an assertion about CLR/codec working set.</summary>
public static class StudioGlobalResources
{
    public const long MasterLimit = 1024L * 1024 * 1024;
    public const long DetailLimit = 192L * 1024 * 1024;
    public const long TotalLimit = 2L * 1024 * 1024 * 1024;
    private sealed record Entry(Guid Owner, string Kind, long Bytes, Action Evict, long Stamp);
    private static readonly object Gate = new();
    private static readonly Dictionary<(Guid, string), Entry> Entries = [];
    private static long stamp;

    public static bool Store(Guid owner, string kind, long bytes, Action evict)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        var limit = kind == "Master" ? MasterLimit : kind == "Detail" ? DetailLimit : TotalLimit;
        var callbacks = new List<Action>();
        bool admitted;
        lock (Gate)
        {
            Entries.Remove((owner, kind));
            while (bytes <= limit && (Entries.Values.Where(x => x.Kind == kind).Sum(x => x.Bytes) + bytes > limit || Entries.Values.Sum(x => x.Bytes) + bytes > TotalLimit))
            {
                var oldest = Entries.Values.Where(x => x.Kind != "Work").MinBy(x => x.Stamp);
                if (oldest is null) break;
                Entries.Remove((oldest.Owner, oldest.Kind)); callbacks.Add(oldest.Evict);
            }
            admitted = bytes <= limit && Entries.Values.Sum(x => x.Bytes) + bytes <= TotalLimit;
            if (admitted) Entries[(owner, kind)] = new(owner, kind, bytes, evict, ++stamp);
            else callbacks.Add(evict);
        }
        foreach (var action in callbacks) action();
        return admitted;
    }
    public static IDisposable ReserveWork(long bytes)
    {
        var owner = Guid.NewGuid();
        if (!Store(owner, "Work", bytes, () => { })) throw new InvalidOperationException("处理请求超过全应用资源预算；未降低尺寸或位深。");
        return new Reservation(owner);
    }
    private sealed class Reservation(Guid owner) : IDisposable
    { public void Dispose() => Release(owner, "Work"); }
    public static void Release(Guid owner, string kind) { lock (Gate) Entries.Remove((owner, kind)); }
    public static (long Masters, long Details, int Entries) Snapshot()
    { lock (Gate) return (Entries.Values.Where(x => x.Kind == "Master").Sum(x => x.Bytes), Entries.Values.Where(x => x.Kind == "Detail").Sum(x => x.Bytes), Entries.Count); }
    public static long AccountedBytes { get { lock (Gate) return Entries.Values.Sum(x => x.Bytes); } }
}
