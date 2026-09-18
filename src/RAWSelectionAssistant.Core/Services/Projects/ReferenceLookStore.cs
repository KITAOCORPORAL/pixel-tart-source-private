using System.Collections.Concurrent;
using System.Text.Json;

namespace RAWSelectionAssistant.Core.Services.Projects;

public sealed record ReferenceLookCatalog(IReadOnlyList<ReferenceLook> Looks, IReadOnlyDictionary<Guid, Guid> ProjectDefaults, int Version = 1);
public sealed class ReferenceLookStore(string directory)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    private string FilePath => Path.Combine(Path.GetFullPath(directory), "reference-looks.json");
    public async Task<ReferenceLookCatalog> LoadAsync(CancellationToken token = default)
    {
        var gate = Gates.GetOrAdd(FilePath, _ => new(1, 1)); await gate.WaitAsync(token).ConfigureAwait(false);
        try { return await ReadAsync(token).ConfigureAwait(false); } finally { gate.Release(); }
    }
    public Task SaveAsync(ReferenceLook look, bool projectDefault = false, CancellationToken token = default)
    {
        look = look.Normalize();
        return UpdateAsync(current =>
        {
            var defaults = current.ProjectDefaults.ToDictionary();
            if (projectDefault && look.ProjectId is Guid project) defaults[project] = look.ReferenceLookId;
            return current with { Looks = current.Looks.Where(item => item.ReferenceLookId != look.ReferenceLookId).Append(look).ToArray(), ProjectDefaults = defaults };
        }, token);
    }
    public Task RemoveAsync(Guid id, CancellationToken token = default) => UpdateAsync(current => current with
    {
        Looks = current.Looks.Where(item => item.ReferenceLookId != id).ToArray(),
        ProjectDefaults = current.ProjectDefaults.Where(pair => pair.Value != id).ToDictionary()
    }, token);
    private async Task<ReferenceLookCatalog> ReadAsync(CancellationToken token)
    {
        if (!File.Exists(FilePath)) return new([], new Dictionary<Guid, Guid>());
        await using var stream = File.OpenRead(FilePath);
        var catalog = await JsonSerializer.DeserializeAsync<ReferenceLookCatalog>(stream, cancellationToken: token).ConfigureAwait(false)
            ?? throw new InvalidDataException("Look catalog is empty.");
        if (catalog.Version != 1) throw new InvalidDataException("Unsupported Look catalog version.");
        if (catalog.Looks is null || catalog.ProjectDefaults is null || catalog.Looks.Any(look => look is null || look.ReferenceSources is null || look.Parameters is null))
            throw new InvalidDataException("Incomplete Look catalog.");
        return catalog;
    }
    private async Task UpdateAsync(Func<ReferenceLookCatalog, ReferenceLookCatalog> update, CancellationToken token)
    {
        var gate = Gates.GetOrAdd(FilePath, _ => new(1, 1)); await gate.WaitAsync(token).ConfigureAwait(false);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var catalog = update(await ReadAsync(token).ConfigureAwait(false)); Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { await JsonSerializer.SerializeAsync(stream, catalog, cancellationToken: token).ConfigureAwait(false); await stream.FlushAsync(token).ConfigureAwait(false); stream.Flush(true); }
            token.ThrowIfCancellationRequested(); File.Move(temporary, FilePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); gate.Release(); }
    }
}
