using System.Text.Json;

namespace RAWSelectionAssistant.Core.Services.FreeCanvas;

public sealed record CanvasCrop(double X = 0, double Y = 0, double Width = 1, double Height = 1)
{
    public CanvasCrop Normalize() => new(Math.Clamp(X, 0, .99), Math.Clamp(Y, 0, .99), Math.Clamp(Width, .01, 1 - Math.Clamp(X, 0, .99)), Math.Clamp(Height, .01, 1 - Math.Clamp(Y, 0, .99)));
}

public sealed record CanvasObject
{
    public Guid ObjectId { get; init; } = Guid.NewGuid();
    public Guid CanvasId { get; init; }
    public Guid LibraryId { get; init; }
    public Guid? AssetId { get; init; }
    public string SourcePath { get; init; } = "";
    public string ContentHash { get; init; } = "";
    public string Name { get; init; } = "";
    public double SourceWidth { get; init; } = 1;
    public double SourceHeight { get; init; } = 1;
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; } = 320;
    public double Height { get; init; } = 240;
    public double Rotation { get; init; }
    public bool FlipX { get; init; }
    public bool FlipY { get; init; }
    public CanvasCrop CropRect { get; init; } = new();
    public int ZIndex { get; init; }
    public bool Locked { get; init; }
    public Guid? GroupId { get; init; }
    public string? Text { get; init; }
    public double FontSize { get; init; } = 28;
    public string TextColor { get; init; } = "#E6E9ED";
    public bool IsText => Text is not null;
}

public sealed record CanvasDocument
{
    public int SchemaVersion { get; init; } = 1;
    public Guid CanvasId { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "自由画布";
    public Guid? ProjectId { get; init; }
    public Guid? PlanningSectionId { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<CanvasObject> Objects { get; init; } = [];
}

/// <summary>Atomic reference-only documents. This service never writes an object's SourcePath.</summary>
public sealed class CanvasDocumentStore(string directory)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public async Task SaveAsync(CanvasDocument document, CancellationToken token = default)
    {
        Validate(document);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        string? temporary = null;
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var target = Path.Combine(DirectoryPath, document.CanvasId.ToString("N") + ".json");
            if(document.Objects.Any(item=>!string.IsNullOrWhiteSpace(item.SourcePath)&&string.Equals(Path.GetFullPath(item.SourcePath),target,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("画布保存位置不能覆盖源文件。");
            temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }), token).ConfigureAwait(false);
            File.Move(temporary, target, true);
        }
        finally { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); _gate.Release(); }
    }
    public async Task<CanvasDocument?> LoadAsync(Guid id, CancellationToken token = default)
    {
        var path = Path.Combine(DirectoryPath, id.ToString("N") + ".json");
        if (!File.Exists(path)) return null;
        var document = JsonSerializer.Deserialize<CanvasDocument>(await File.ReadAllTextAsync(path, token).ConfigureAwait(false)) ?? throw new InvalidDataException("画布文件为空。");
        Validate(document);
        if (document.CanvasId != id) throw new InvalidDataException("画布标识不匹配。");
        return document;
    }
    public async Task<IReadOnlyList<CanvasDocument>> ListAsync(Guid? projectId = null, CancellationToken token = default)
    {
        if (!Directory.Exists(DirectoryPath)) return [];
        var result = new List<CanvasDocument>();
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.json"))
            if (Guid.TryParse(Path.GetFileNameWithoutExtension(path), out var id) && await LoadAsync(id, token).ConfigureAwait(false) is { } document && (projectId is null || document.ProjectId == projectId)) result.Add(document);
        return result.OrderByDescending(item => item.UpdatedAt).ToArray();
    }
    public static void Validate(CanvasDocument document)
    {
        if (document.SchemaVersion != 1 || document.CanvasId == Guid.Empty || document.Objects.Count > 10000 || document.Objects.Select(item => item.ObjectId).Distinct().Count() != document.Objects.Count) throw new InvalidDataException("画布数据无效。");
        foreach (var item in document.Objects)
            if (item.CanvasId != document.CanvasId || item.ObjectId == Guid.Empty || !new[] { item.X, item.Y, item.Width, item.Height, item.Rotation, item.FontSize, item.SourceWidth, item.SourceHeight,item.CropRect.X, item.CropRect.Y, item.CropRect.Width, item.CropRect.Height }.All(double.IsFinite) || item.Width <= 0 || item.Height <= 0 || item.SourceWidth<=0||item.SourceHeight<=0||item.FontSize<=0||item.CropRect != item.CropRect.Normalize()) throw new InvalidDataException("画布对象数据无效。");
    }
}
