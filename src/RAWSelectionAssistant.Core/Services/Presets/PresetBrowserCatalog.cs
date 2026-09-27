namespace RAWSelectionAssistant.Core.Services.Presets;

public enum PresetProviderKind { PixelTart, AdobeXmp, CaptureOne, User }

public sealed record PresetBrowserItem(
    string Id,
    string Name,
    PresetProviderKind Provider,
    string Category,
    bool IsFavorite = false,
    DateTimeOffset? LastUsedUtc = null,
    IReadOnlyDictionary<string, double>? Parameters = null);

public sealed record PresetBrowserQuery(string Search = "", string? Category = null,
    PresetProviderKind? Provider = null, bool FavoritesOnly = false, bool RecentOnly = false);

/// <summary>Lazy, allocation-bounded browser catalog. Preview state is separate from commit state.</summary>
public sealed class PresetBrowserCatalog
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PresetBrowserItem> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _recent = [];

    public int Count { get { lock (_gate) return _items.Count; } }
    public void Upsert(PresetBrowserItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Name)) throw new ArgumentException("Preset identity is required.", nameof(item));
        lock (_gate) _items[item.Id] = item;
    }

    public bool Remove(string id) { lock (_gate) return _items.Remove(id); }

    public IReadOnlyList<PresetBrowserItem> Query(PresetBrowserQuery? query = null)
    {
        query ??= new(); var text = query.Search.Trim();
        lock (_gate)
        {
            var recent = _recent.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return _items.Values.Where(item =>
                (text.Length == 0 || item.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || item.Category.Contains(text, StringComparison.OrdinalIgnoreCase)) &&
                (query.Category is null || string.Equals(query.Category, item.Category, StringComparison.OrdinalIgnoreCase)) &&
                (query.Provider is null || query.Provider == item.Provider) && (!query.FavoritesOnly || item.IsFavorite) && (!query.RecentOnly || recent.Contains(item.Id)))
                .OrderByDescending(item => item.IsFavorite).ThenByDescending(item => item.LastUsedUtc).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    public void SetFavorite(string id, bool favorite)
    {
        lock (_gate) if (_items.TryGetValue(id, out var item)) _items[id] = item with { IsFavorite = favorite };
    }

    public void MarkUsed(string id)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(id, out var item)) return;
            _items[id] = item with { LastUsedUtc = DateTimeOffset.UtcNow };
            _recent.Remove(id); _recent.AddFirst(id);
            while (_recent.Count > 32) _recent.RemoveLast();
        }
    }
}

public sealed class PresetPreviewCoordinator
{
    private readonly object _gate = new();
    private CancellationTokenSource? _pending;
    private long _revision;
    public long Revision => Volatile.Read(ref _revision);

    public async Task<T?> PreviewLatestAsync<T>(Func<CancellationToken, Task<T>> preview, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(preview);
        CancellationTokenSource linked;
        var revision = Interlocked.Increment(ref _revision);
        lock (_gate)
        {
            _pending?.Cancel(); _pending?.Dispose();
            linked = CancellationTokenSource.CreateLinkedTokenSource(token); _pending = linked;
        }
        try
        {
            var result = await preview(linked.Token).ConfigureAwait(false);
            return revision == Revision && !linked.IsCancellationRequested ? result : default;
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { return default; }
        finally { lock (_gate) if (ReferenceEquals(_pending, linked)) _pending = null; linked.Dispose(); }
    }
}
