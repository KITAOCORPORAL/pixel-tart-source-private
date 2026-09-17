using System.Text.Json;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Publishing;

public sealed class PublishingPresetStore(string filePath)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public string FilePath { get; } = Path.GetFullPath(filePath);

    public async Task<IReadOnlyList<PublishingPreset>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath)) return BuiltIns;
        var values = JsonSerializer.Deserialize<PublishingPreset[]>(await File.ReadAllTextAsync(FilePath, cancellationToken).ConfigureAwait(false)) ?? [];
        return BuiltIns.Concat(values).GroupBy(item => item.Id).Select(group => group.Last()).ToArray();
    }

    public async Task SaveAsync(PublishingPreset preset, CancellationToken cancellationToken = default)
    {
        preset.Validate(); await _gate.WaitAsync(cancellationToken).ConfigureAwait(false); string? temporary = null;
        try
        {
            var current = (await LoadAsync(cancellationToken).ConfigureAwait(false)).Where(item => !BuiltIns.Any(builtIn => builtIn.Id == item.Id)).ToList();
            current.RemoveAll(item => item.Id == preset.Id); current.Add(preset);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, FilePath, true);
        }
        finally { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); _gate.Release(); }
    }

    public static IReadOnlyList<PublishingPreset> BuiltIns { get; } =
    [
        new(Guid.Parse("1879307e-4604-4a3d-aa11-58f86b7b7501"), "Instagram", new(new(true, PublishingSizeMode.LongestEdge, 2048, JpegQuality: 88), Suffix: "_instagram"), DateTimeOffset.UnixEpoch),
        new(Guid.Parse("98824d96-b15b-4a12-b30e-27baa155624c"), "小红书", new(new(true, PublishingSizeMode.LongestEdge, 2560, JpegQuality: 90), Suffix: "_xhs"), DateTimeOffset.UnixEpoch),
        new(Guid.Parse("c1e1cb0a-a0e0-4baa-9d27-b38e88d4eb5a"), "客户预览", new(new(true, PublishingSizeMode.LongestEdge, 3000, JpegQuality: 92), Suffix: "_preview"), DateTimeOffset.UnixEpoch),
        new(Guid.Parse("195fa209-48f6-4844-a76d-ac20f36b9101"), "作品集", new(new(false, PublishingSizeMode.Original), Suffix: "_portfolio"), DateTimeOffset.UnixEpoch)
    ];
}

public sealed class ProjectPublishingDefaultStore(string filePath)
{
    private readonly string _filePath = Path.GetFullPath(filePath);
    public async Task<Guid?> GetAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(item => item.ProjectId == projectId)?.DefaultPublishingPresetId;
    public async Task SetAsync(ProjectPublishingDefaults value, CancellationToken cancellationToken = default)
    {
        var current = (await LoadAsync(cancellationToken).ConfigureAwait(false)).ToList(); current.RemoveAll(item => item.ProjectId == value.ProjectId); current.Add(value);
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!); var temporary = _filePath + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false); File.Move(temporary, _filePath, true);
    }
    private async Task<IReadOnlyList<ProjectPublishingDefaults>> LoadAsync(CancellationToken cancellationToken) => !File.Exists(_filePath) ? [] : JsonSerializer.Deserialize<ProjectPublishingDefaults[]>(await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false)) ?? [];
}
