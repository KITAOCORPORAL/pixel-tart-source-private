using System.Text.Json;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Portable edit session: links to source files, never a second asset metadata database.</summary>
public sealed record ColorStudioSessionTarget(Guid Id, string Path, Guid? AssetId, bool Selected,
    ReferenceLook? Look, ColorAdjustmentStack? Stack, PixelTartFilmSettings? Film,
    ColorStudioMatchEngine Engine = ColorStudioMatchEngine.Stable, MatchV4ExecutionMode ExecutionMode = MatchV4ExecutionMode.Auto,
    int? SessionRating = null, string? SessionColorLabel = null);

public sealed record ColorStudioSessionDocument(IReadOnlyList<ColorStudioSessionTarget> Targets, Guid? ActiveTargetId,
    string FilterScope = "All", int MinimumRating = 0, string ColorLabelFilter = "All", int Version = 1)
{
    public ColorStudioSessionDocument Normalize()
    {
        if (Version != 1 || Targets is null || Targets.Count > 10000) throw new InvalidDataException("不支持的 Color Studio 工作文件版本或照片数量。");
        if (Targets.Any(x => x is null || x.Id == Guid.Empty || string.IsNullOrWhiteSpace(x.Path)) || Targets.Select(x => x.Id).Distinct().Count() != Targets.Count)
            throw new InvalidDataException("工作文件中包含无效或重复的照片身份。");
        if (ActiveTargetId is Guid active && Targets.All(x => x.Id != active)) throw new InvalidDataException("当前照片不在工作文件中。");
        var targets = Targets.Select(x => x with
        {
            Path = System.IO.Path.GetFullPath(x.Path), Look = x.Look?.Normalize(), Stack = x.Stack?.DeepClone(),
            SessionRating = x.AssetId is null ? Math.Clamp(x.SessionRating ?? 0, 0, 5) : null,
            SessionColorLabel = x.AssetId is null ? x.SessionColorLabel : null,
            Engine = Enum.IsDefined(x.Engine) ? x.Engine : throw new InvalidDataException("不支持的处理引擎。"),
            ExecutionMode = Enum.IsDefined(x.ExecutionMode) ? x.ExecutionMode : throw new InvalidDataException("不支持的执行模式。")
        }).ToArray();
        foreach (var target in targets) target.Film?.Validate();
        return this with { Targets = targets, MinimumRating = Math.Clamp(MinimumRating, 0, 5) };
    }
}

public static class ColorStudioSessionStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static async Task SaveAsync(string path, ColorStudioSessionDocument document, CancellationToken token = default)
    {
        document = document.Normalize(); path = System.IO.Path.GetFullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { await JsonSerializer.SerializeAsync(stream, document, Options, token); await stream.FlushAsync(token); stream.Flush(true); }
            token.ThrowIfCancellationRequested(); File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static async Task<ColorStudioSessionDocument> LoadAsync(string path, CancellationToken token = default)
    {
        await using var stream = File.OpenRead(path);
        return (await JsonSerializer.DeserializeAsync<ColorStudioSessionDocument>(stream, Options, token) ?? throw new InvalidDataException("工作文件为空。")).Normalize();
    }
}
