using System.Text.Json;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Portable edit session: links to source files, never a second asset metadata database.</summary>
public sealed record ColorStudioSessionTarget(Guid Id, string Path, Guid? AssetId, bool Selected,
    ReferenceLook? Look, ColorAdjustmentStack? Stack, PixelTartFilmSettings? Film,
    ColorStudioMatchEngine Engine = ColorStudioMatchEngine.Stable, MatchV4ExecutionMode ExecutionMode = MatchV4ExecutionMode.Auto,
    int? SessionRating = null, string? SessionColorLabel = null, Guid? LibraryId = null, string? SourceHash = null, IReadOnlyList<StudioEditVersion>? EditVersions = null, Guid? ActiveEditVersionId = null,long? CatalogRevision=null);

public sealed record ColorStudioLayout(double AuxiliaryWidth = 290, double EditingWidth = 340,
    bool AuxiliaryOpen = true, bool EditingOpen = true, int AuxiliaryMode = 0, int ToolMode = 0,
    string FilmstripDock = "Bottom", string FilmstripView = "Grid", bool FilmstripOpen = true)
{
    public ColorStudioLayout Normalize() => this with
    {
        AuxiliaryWidth = double.IsFinite(AuxiliaryWidth) ? Math.Clamp(AuxiliaryWidth, 240, 440) : 290,
        EditingWidth = double.IsFinite(EditingWidth) ? Math.Clamp(EditingWidth, 312, 440) : 340,
        AuxiliaryMode = Math.Clamp(AuxiliaryMode, 0, 3), ToolMode = Math.Clamp(ToolMode, 0, 4),
        FilmstripDock = FilmstripDock == "Right" ? "Right" : "Bottom",
        FilmstripView = FilmstripView is "List" or "Slideshow" ? FilmstripView : "Grid"
    };
}

public sealed record ColorStudioSessionDocument(IReadOnlyList<ColorStudioSessionTarget> Targets, Guid? ActiveTargetId,
    string FilterScope = "All", int MinimumRating = 0, string ColorLabelFilter = "All", int Version = 1,
    ColorStudioLayout? Layout = null)
{
    public ColorStudioSessionDocument Normalize()
    {
        if (Version != 1 || Targets is null || Targets.Count > 10000) throw new InvalidDataException("不支持的 Color Studio 工作文件版本或照片数量。");
        if (Targets.Any(x => x is null || x.Id == Guid.Empty || string.IsNullOrWhiteSpace(x.Path)) || Targets.Select(x => x.Id).Distinct().Count() != Targets.Count)
            throw new InvalidDataException("工作文件中包含无效或重复的照片身份。");
        if (ActiveTargetId is Guid active && Targets.All(x => x.Id != active)) throw new InvalidDataException("当前照片不在工作文件中。");
        if (Targets.Any(x => x.LibraryId == Guid.Empty || (x.LibraryId.HasValue && !x.AssetId.HasValue) ||
            (x.SourceHash is not null && (x.SourceHash.Trim().Length != 64 || !x.SourceHash.Trim().All(Uri.IsHexDigit)))))
            throw new InvalidDataException("素材身份或 SHA-256 无效。");
        var targets = Targets.Select(x => x with
        {
            Path = System.IO.Path.GetFullPath(x.Path), Look = x.Look?.Normalize(), Stack = x.Stack?.DeepClone(),
            SessionRating = x.AssetId is null ? Math.Clamp(x.SessionRating ?? 0, 0, 5) : null,
            SessionColorLabel = x.AssetId is null ? x.SessionColorLabel : null,
            Engine = Enum.IsDefined(x.Engine) ? x.Engine : throw new InvalidDataException("不支持的处理引擎。"),
            ExecutionMode = Enum.IsDefined(x.ExecutionMode) ? x.ExecutionMode : throw new InvalidDataException("不支持的执行模式。")
        }).ToArray();
        foreach (var target in targets)
        {
            target.Film?.Validate();
            if (target.EditVersions is { } versions && (versions.Select(v=>v.Id).Distinct().Count()!=versions.Count || versions.Any(v=>v.Id==Guid.Empty || v.LibraryId!=target.LibraryId || v.AssetId!=target.AssetId || v.SourceHash!=target.SourceHash) || (target.ActiveEditVersionId is Guid activeVersion && !versions.Any(v=>v.Id==activeVersion)))) throw new InvalidDataException("编辑版本身份无效。");
            foreach(var version in target.EditVersions ?? []) { if(string.IsNullOrWhiteSpace(version.Name) || !Enum.IsDefined(version.Engine) || !Enum.IsDefined(version.Mode))throw new InvalidDataException("编辑版本参数无效。");version.Stack?.Normalize(); version.Film?.Validate(); }
        }
        return this with { Targets = targets, MinimumRating = Math.Clamp(MinimumRating, 0, 5), Layout = Layout?.Normalize() };
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
