using System.Collections.Concurrent;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.Projects;

public sealed record ProjectVisualSource(Guid LibraryId, Guid AssetId, string ContentHash,
    string Kind = "Asset", Guid? ContainerId = null);
public sealed record ProjectPaletteReference(IReadOnlyList<DominantColor> Colors,
    IReadOnlyList<ProjectVisualSource> Sources, DateTimeOffset UpdatedAt, string AnalysisVersion, int Version = 1);
public sealed record ProjectToneTarget(ElevenZoneDistribution Zones, IReadOnlyList<double> Histogram,
    IReadOnlyList<ProjectVisualSource> Sources, DateTimeOffset UpdatedAt, string AnalysisVersion, int Version = 1);
public sealed record ProjectVisualReferences(Guid ProjectId, ProjectPaletteReference? DefaultPalette = null,
    ProjectToneTarget? DefaultToneTarget = null, int Version = 1);

/// <summary>Reference-only project data. Per-path serialization prevents competing store
/// instances from losing palette/tone updates. Atomic replacement preserves the previous file on failure.</summary>
public sealed class ProjectVisualReferenceStore(string directory)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    private string FilePath(Guid id) => Path.Combine(Path.GetFullPath(directory), $"{id:N}.visual.json");
    public async Task<ProjectVisualReferences> LoadAsync(Guid projectId, CancellationToken token = default)
    {
        var path = FilePath(projectId); var gate = Gates.GetOrAdd(path, _ => new(1, 1));
        await gate.WaitAsync(token).ConfigureAwait(false);
        try { return await ReadAsync(projectId, path, token).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    public Task SavePaletteAsync(Guid projectId, ProjectPaletteReference palette, CancellationToken token = default)
    {
        if (palette.Colors.Count == 0 || palette.Sources.Count == 0) throw new ArgumentException("Palette and sources are required.", nameof(palette));
        return UpdateAsync(projectId, current => current with { DefaultPalette = palette }, token);
    }
    public Task SaveToneAsync(Guid projectId, ProjectToneTarget tone, CancellationToken token = default)
    {
        if (tone.Zones.Ratios.Count != 11 || tone.Histogram.Count != 256 || tone.Sources.Count == 0 ||
            tone.Zones.Ratios.Concat(tone.Histogram).Any(value => !double.IsFinite(value) || value < 0))
            throw new ArgumentException("A valid tone distribution and sources are required.", nameof(tone));
        return UpdateAsync(projectId, current => current with { DefaultToneTarget = tone }, token);
    }
    private static async Task<ProjectVisualReferences> ReadAsync(Guid id, string path, CancellationToken token)
    {
        if (!File.Exists(path)) return new(id);
        await using var stream = File.OpenRead(path);
        var data = await JsonSerializer.DeserializeAsync<ProjectVisualReferences>(stream, cancellationToken: token).ConfigureAwait(false)
            ?? throw new InvalidDataException("Project visual data is empty.");
        if (data.ProjectId != id || data.Version != 1) throw new InvalidDataException("Project visual data has an unsupported identity/version.");
        return data;
    }
    private async Task UpdateAsync(Guid id, Func<ProjectVisualReferences, ProjectVisualReferences> update, CancellationToken token)
    {
        if (id == Guid.Empty) throw new ArgumentException("A project is required.", nameof(id));
        var path = FilePath(id); var gate = Gates.GetOrAdd(path, _ => new(1, 1));
        await gate.WaitAsync(token).ConfigureAwait(false);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var updated = update(await ReadAsync(id, path, token).ConfigureAwait(false));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, updated, cancellationToken: token).ConfigureAwait(false);
                await stream.FlushAsync(token).ConfigureAwait(false); stream.Flush(true);
            }
            token.ThrowIfCancellationRequested(); File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); gate.Release(); }
    }
}
