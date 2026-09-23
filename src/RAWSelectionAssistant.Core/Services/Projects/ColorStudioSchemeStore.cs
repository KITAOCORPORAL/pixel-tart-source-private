using System.Collections.Concurrent;
using System.Text.Json;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Versioned schemes beside the existing reference catalog; atomic replacement preserves the last good file.</summary>
public sealed class ColorStudioSchemeStore(string directory)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    private string FilePath => Path.Combine(Path.GetFullPath(directory), "color-studio-schemes.json");
    private sealed record Catalog(int Version, IReadOnlyList<ColorStudioSchemeV2> Schemes);

    public async Task<IReadOnlyList<ColorStudioSchemeV2>> LoadAsync(CancellationToken token = default)
    {
        var gate = Gates.GetOrAdd(FilePath, _ => new(1, 1)); await gate.WaitAsync(token).ConfigureAwait(false);
        try { return (await ReadAsync(token).ConfigureAwait(false)).Schemes; }
        finally { gate.Release(); }
    }

    public Task SaveAsync(ColorStudioSchemeV2 scheme, CancellationToken token = default) => UpdateAsync(
        items => items.Where(item => item.Id != scheme.Id).Append(scheme.Normalize()).ToArray(), token);

    public Task DeleteAsync(Guid id, CancellationToken token = default) => UpdateAsync(
        items => items.Where(item => item.Id != id).ToArray(), token);

    private async Task<Catalog> ReadAsync(CancellationToken token)
    {
        if (!File.Exists(FilePath)) return new Catalog(2, []);
        await using var stream = File.OpenRead(FilePath);
        var catalog = await JsonSerializer.DeserializeAsync<Catalog>(stream, cancellationToken: token).ConfigureAwait(false)
            ?? throw new InvalidDataException("Color Studio scheme catalog is empty.");
        if (catalog.Version != 2 || catalog.Schemes is null) throw new InvalidDataException("Unsupported Color Studio scheme catalog.");
        return catalog with { Schemes = catalog.Schemes.Select(item => item.Normalize()).ToArray() };
    }

    private async Task UpdateAsync(Func<IReadOnlyList<ColorStudioSchemeV2>, IReadOnlyList<ColorStudioSchemeV2>> update, CancellationToken token)
    {
        var gate = Gates.GetOrAdd(FilePath, _ => new(1, 1)); await gate.WaitAsync(token).ConfigureAwait(false);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var current = await ReadAsync(token).ConfigureAwait(false);
            var catalog = new Catalog(2, update(current.Schemes).Select(item => item.Normalize()).ToArray());
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, catalog, cancellationToken: token).ConfigureAwait(false);
                await stream.FlushAsync(token).ConfigureAwait(false); stream.Flush(true);
            }
            token.ThrowIfCancellationRequested(); File.Move(temporary, FilePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); gate.Release(); }
    }
}
