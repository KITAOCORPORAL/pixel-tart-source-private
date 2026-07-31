using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Core.Models;

public sealed class MediaIndexSnapshot
{
    public List<MediaFileRecord> Files { get; init; } = [];
    public Dictionary<string, List<MediaFileRecord>> ByNameAndExtension { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<MediaFileRecord>> ByNumberAndExtension { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<MediaFileRecord>> ByFullName { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<MediaFileRecord>> ByNumericId { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, MediaFileRecord> ByFullPath { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<MediaFileRecord>> BySourceRoot { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int SkippedDirectoryCount { get; set; }

    public static MediaIndexSnapshot Create(IEnumerable<MediaFileRecord> files)
    {
        var snapshot = new MediaIndexSnapshot();
        foreach (var file in files)
        {
            snapshot.Files.Add(file);
            snapshot.ByFullPath[file.FullPath] = file;
            Add(snapshot.BySourceRoot, file.SourceRoot, file);
            Add(snapshot.ByNameAndExtension, Composite(file.NormalizedName, file.Extension), file);
            if (file.NumericId.Length > 0)
            {
                Add(snapshot.ByNumberAndExtension, Composite(file.NumericId, file.Extension), file);
                Add(snapshot.ByNumericId, file.NumericId, file);
            }
            Add(snapshot.ByFullName, file.NormalizedName, file);
        }
        return snapshot;
    }

    public IReadOnlyList<MediaFileRecord> FindByNameAndExtensions(string name, IEnumerable<string> extensions) =>
        extensions.SelectMany(extension => ByNameAndExtension.GetValueOrDefault(Composite(name, extension)) ?? []).DistinctBy(x => x.FullPath, StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<MediaFileRecord> FindByNumberAndExtensions(string number, IEnumerable<string> extensions) =>
        extensions.SelectMany(extension => ByNumberAndExtension.GetValueOrDefault(Composite(number, extension)) ?? []).DistinctBy(x => x.FullPath, StringComparer.OrdinalIgnoreCase).ToList();

    public MediaFileRecord? FindByFullPath(string fullPath) => ByFullPath.GetValueOrDefault(fullPath);

    public IReadOnlyList<MediaFileRecord> FindBySourceRoot(string sourceRoot) =>
        BySourceRoot.GetValueOrDefault(sourceRoot) ?? [];

    public static string Composite(string value, string extension) => $"{value}\u001F{MediaExtensionPolicy.NormalizeExtension(extension)}";

    private static void Add(Dictionary<string, List<MediaFileRecord>> map, string key, MediaFileRecord file)
    {
        if (!map.TryGetValue(key, out var list)) map[key] = list = [];
        list.Add(file);
    }
}
