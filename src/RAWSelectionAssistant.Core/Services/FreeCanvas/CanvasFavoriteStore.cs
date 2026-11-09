using System.Text.Json;

namespace RAWSelectionAssistant.Core.Services.FreeCanvas;

/// <summary>Library identities only: favorites never copy pixels or change photo ratings.</summary>
public sealed record CanvasFavorite(Guid LibraryId, Guid AssetId);

public sealed class CanvasFavoriteStore(string directory)
{
    private string FilePath => Path.Combine(Path.GetFullPath(directory), "canvas-favorites.json");
    public async Task<IReadOnlySet<CanvasFavorite>> LoadAsync(CancellationToken token = default)
    {
        if (!File.Exists(FilePath)) return new HashSet<CanvasFavorite>();
        var state = JsonSerializer.Deserialize<State>(await File.ReadAllTextAsync(FilePath, token).ConfigureAwait(false))
            ?? throw new InvalidDataException("素材收藏文件为空。");
        if (state.Version != 1 || state.Items is null || state.Items.Any(item => item is null || item.LibraryId == Guid.Empty || item.AssetId == Guid.Empty))
            throw new InvalidDataException("素材收藏文件无效，原文件已保留。");
        return state.Items.ToHashSet();
    }
    public async Task SetAsync(IEnumerable<CanvasFavorite> values, bool favorite, CancellationToken token = default)
    {
        var selected = values.Distinct().ToArray();
        if (selected.Any(item => item.LibraryId == Guid.Empty || item.AssetId == Guid.Empty))
            throw new InvalidDataException("只有素材库中的有效素材可以收藏。");
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        // A competing writer fails visibly; never lose an update or overwrite invalid state.
        using var writeLock = new FileStream(FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var items = (await LoadAsync(token).ConfigureAwait(false)).ToHashSet();
        foreach (var item in selected) { if (favorite) items.Add(item); else items.Remove(item); }
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new State(1, items.OrderBy(item => item.LibraryId).ThenBy(item => item.AssetId).ToArray()), new JsonSerializerOptions { WriteIndented = true }), token).ConfigureAwait(false);
            File.Move(temporary, FilePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private sealed record State(int Version, CanvasFavorite[] Items);
}
