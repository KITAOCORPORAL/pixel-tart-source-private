using System.Text.Json;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Publishing;

public enum ExportRecipeFormat { Jpeg, Png, Tiff }
public enum ExportRecipeBitDepth { Eight = 8, Sixteen = 16 }
public enum ExportRecipeResizeMode { Original, LongEdge, ShortEdge, Exact }
public enum ExportRecipeMetadataPolicy { Preserve, Strip, CopyrightOnly }
public sealed record ExportRecipe(
    Guid Id,
    string Name,
    ExportRecipeFormat Format = ExportRecipeFormat.Jpeg,
    ExportRecipeBitDepth BitDepth = ExportRecipeBitDepth.Eight,
    string ColorSpaceProfile = "sRGB",
    int Quality = 92,
    ExportRecipeResizeMode ResizeMode = ExportRecipeResizeMode.Original,
    int? LongEdge = null,
    int? ShortEdge = null,
    int? Width = null,
    int? Height = null,
    int Dpi = 300,
    ExportRecipeMetadataPolicy MetadataPolicy = ExportRecipeMetadataPolicy.Preserve,
    string FilenameTemplate = "{name}_{recipe}",
    string Destination = "Exports")
{
    public ExportRecipe Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("Export recipe identity and name are required.");
        if (Quality is < 1 or > 100 || Dpi is < 1 or > 2400) throw new ArgumentOutOfRangeException(nameof(Quality));
        if (Format == ExportRecipeFormat.Tiff && BitDepth != ExportRecipeBitDepth.Sixteen) throw new ArgumentException("TIFF recipes must declare 16-bit output.");
        if (ResizeMode == ExportRecipeResizeMode.LongEdge && LongEdge is not > 0) throw new ArgumentException("Long edge is required.");
        if (ResizeMode == ExportRecipeResizeMode.ShortEdge && ShortEdge is not > 0) throw new ArgumentException("Short edge is required.");
        if (ResizeMode == ExportRecipeResizeMode.Exact && (Width is not > 0 || Height is not > 0)) throw new ArgumentException("Exact dimensions are required.");
        if (!IsSafeRelativeComponent(Destination)) throw new ArgumentException("Destination is a relative export folder.");
        if (!IsSafeFilenameTemplate(FilenameTemplate)) throw new ArgumentException("FilenameTemplate 只能生成文件名，不能包含路径或未知 token。", nameof(FilenameTemplate));
        return this;
    }

    private static bool IsSafeRelativeComponent(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value) || value.Contains(':') || value.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return false;
        var normalized = value.Replace('\\', '/');
        if (normalized.Contains("//", StringComparison.Ordinal) || normalized.StartsWith('/')) return false;
        if (normalized.Split('/').Any(part => part is "" or "." or "..")) return false;
        return normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).All(part => part.Length > 0 && part.TrimEnd(' ', '.') == part && !ReservedWindowsName(part));
    }

    private static bool IsSafeFilenameTemplate(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.Contains('/') || value.Contains('\\') || value.Contains(':') || value.TrimEnd(' ', '.') != value) return false;
        var tokens = new[] { "{name}", "{recipe}" };
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '{') continue;
            var end = value.IndexOf('}', index + 1);
            if (end < 0 || !tokens.Contains(value[index..(end + 1)], StringComparer.OrdinalIgnoreCase)) return false;
            index = end;
        }
        if (value.Contains('}'))
        {
            var withoutTokens = value.Replace("{name}", "", StringComparison.OrdinalIgnoreCase).Replace("{recipe}", "", StringComparison.OrdinalIgnoreCase);
            if (withoutTokens.IndexOfAny(['{', '}']) >= 0) return false;
        }
        return !ReservedWindowsName(value.Replace("{name}", "name", StringComparison.OrdinalIgnoreCase).Replace("{recipe}", "recipe", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ReservedWindowsName(string value)
    {
        var stem = Path.GetFileNameWithoutExtension(value).TrimEnd(' ', '.');
        return new[] { "CON", "PRN", "AUX", "NUL" }.Contains(stem, StringComparer.OrdinalIgnoreCase)
            || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && stem[3] is >= '1' and <= '9');
    }
}

public sealed class ExportRecipeStore(string filePath)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public string FilePath { get; } = Path.GetFullPath(filePath);
    public static IReadOnlyList<ExportRecipe> BuiltIns { get; } =
    [
        new(Guid.Parse("5cf7c4f6-e1a1-4a56-9cb2-1e4f0b70b8b1"), "Web / Social", ExportRecipeFormat.Jpeg, ExportRecipeBitDepth.Eight, "sRGB", 90, ExportRecipeResizeMode.LongEdge, LongEdge: 2048, Dpi: 72, FilenameTemplate: "{name}_web", Destination: "Web"),
        new(Guid.Parse("f7e4fce4-8d43-49fd-9f4c-1f7e44ce4f31"), "Client Full Resolution", ExportRecipeFormat.Jpeg, ExportRecipeBitDepth.Eight, "sRGB", 95, Destination: "Client"),
        new(Guid.Parse("15c14a24-86d3-46d8-b5c1-2f4a927ad4bd"), "TIFF16 Retouch", ExportRecipeFormat.Tiff, ExportRecipeBitDepth.Sixteen, "Adobe RGB (1998)", 100, Destination: "Retouch"),
        new(Guid.Parse("a9eb7c84-bb3d-4e0d-a1f4-53d6e8a0db2d"), "Print", ExportRecipeFormat.Tiff, ExportRecipeBitDepth.Sixteen, "Adobe RGB (1998)", 100, ExportRecipeResizeMode.Exact, Width: 7016, Height: 4961, Dpi: 300, Destination: "Print")
    ];
    public async Task<IReadOnlyList<ExportRecipe>> LoadAsync(CancellationToken token = default)
    {
        return await LoadCoreAsync(token).ConfigureAwait(false);
    }
    public async Task SaveAsync(ExportRecipe recipe, CancellationToken token = default)
    {
        recipe.Validate(); await _gate.WaitAsync(token).ConfigureAwait(false);
        try { if (BuiltIns.Any(item => item.Id == recipe.Id)) throw new InvalidOperationException("Built-in recipes must be saved as a custom copy."); var values = (await LoadCoreAsync(token).ConfigureAwait(false)).Where(item => item.Id != recipe.Id).Append(recipe).Where(item => !BuiltIns.Any(builtin => builtin.Id == item.Id)).ToArray(); Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); var tmp = FilePath + ".tmp"; await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true }), token).ConfigureAwait(false); File.Move(tmp, FilePath, true); }
        finally { _gate.Release(); }
    }
    public async Task DeleteAsync(Guid id, CancellationToken token = default)
    {
        if (BuiltIns.Any(item => item.Id == id)) throw new InvalidOperationException("Built-in recipes are editable copies and cannot be deleted.");
        await _gate.WaitAsync(token).ConfigureAwait(false); try { var values = (await LoadCoreAsync(token).ConfigureAwait(false)).Where(item => item.Id != id).Where(item => !BuiltIns.Any(builtin => builtin.Id == item.Id)).ToArray(); Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); var tmp = FilePath + ".tmp"; try { await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true }), token).ConfigureAwait(false); File.Move(tmp, FilePath, true); } finally { if (File.Exists(tmp)) File.Delete(tmp); } } finally { _gate.Release(); }
    }

    private async Task<IReadOnlyList<ExportRecipe>> LoadCoreAsync(CancellationToken token)
    {
        if (!File.Exists(FilePath)) return BuiltIns;
        try
        {
            var custom = JsonSerializer.Deserialize<ExportRecipe[]>(await File.ReadAllTextAsync(FilePath, token).ConfigureAwait(false)) ?? [];
            return BuiltIns.Concat(custom).GroupBy(item => item.Id).Select(group => group.Last()).ToArray();
        }
        catch (JsonException)
        {
            var backup = FilePath + ".corrupt-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
            try { File.Move(FilePath, backup, true); } catch { }
            return BuiltIns;
        }
    }
}
