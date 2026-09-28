namespace RAWSelectionAssistant.Core.Services.Sync;

public sealed record SyncEntityEnvelope(string EntityType, string StableId, long Revision, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? DeletedAtUtc, string DeviceId, IReadOnlyDictionary<string, string?> Fields);
public sealed record SyncConflict(string EntityType, string StableId, string Field, string LocalValue, string RemoteValue, bool RequiresResolution, string Reason);
public sealed record SyncApplyResult(bool Applied, bool Duplicate, bool TombstoneApplied, IReadOnlyList<SyncConflict> Conflicts);
public interface ISyncTransport { Task<IReadOnlyList<SyncEntityEnvelope>> PushAsync(IReadOnlyList<SyncEntityEnvelope> changes, CancellationToken token = default); Task<IReadOnlyList<SyncEntityEnvelope>> PullAsync(string deviceId, CancellationToken token = default); }
public sealed class DeterministicSyncTransport : ISyncTransport
{
    private readonly Dictionary<(string Type, string Id), SyncEntityEnvelope> _store = [];
    public List<SyncEntityEnvelope> Delivered { get; } = [];
    public Task<IReadOnlyList<SyncEntityEnvelope>> PushAsync(IReadOnlyList<SyncEntityEnvelope> changes, CancellationToken token = default)
    {
        foreach (var change in changes.OrderBy(item => item.UpdatedAtUtc).ThenBy(item => item.StableId, StringComparer.Ordinal))
        { if (!_store.TryGetValue((change.EntityType, change.StableId), out var current) || change.Revision > current.Revision) _store[(change.EntityType, change.StableId)] = change; }
        return Task.FromResult<IReadOnlyList<SyncEntityEnvelope>>(changes);
    }
    public Task<IReadOnlyList<SyncEntityEnvelope>> PullAsync(string deviceId, CancellationToken token = default) => Task.FromResult<IReadOnlyList<SyncEntityEnvelope>>(_store.Values.Where(item => !string.Equals(item.DeviceId, deviceId, StringComparison.Ordinal)).OrderBy(item => item.UpdatedAtUtc).ToArray());
}
public static class SyncConflictPolicy
{
    private static readonly HashSet<string> HighRisk = new(StringComparer.OrdinalIgnoreCase) { "start", "end", "price", "deposit", "paymentState", "clientId", "clientIdentity", "bookingDate" };
    public static bool IsHighRisk(string field) => HighRisk.Contains(field);
    public static SyncApplyResult Apply(SyncEntityEnvelope? local, SyncEntityEnvelope incoming)
    {
        if (local is not null && incoming.Revision <= local.Revision) return new(false, true, false, []);
        var conflicts = new List<SyncConflict>();
        if (local is not null) foreach (var field in local.Fields.Keys.Union(incoming.Fields.Keys, StringComparer.OrdinalIgnoreCase)) if (IsHighRisk(field) && local.Fields.GetValueOrDefault(field) != incoming.Fields.GetValueOrDefault(field)) conflicts.Add(new(incoming.EntityType, incoming.StableId, field, local.Fields.GetValueOrDefault(field) ?? "", incoming.Fields.GetValueOrDefault(field) ?? "", true, "High-risk booking field requires explicit resolution."));
        return new(true, false, incoming.DeletedAtUtc is not null, conflicts);
    }
}
