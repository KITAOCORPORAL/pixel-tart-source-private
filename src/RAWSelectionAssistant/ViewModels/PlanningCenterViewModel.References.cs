using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed record PlanningReferenceItem(PlanningDocumentReference Link, string? PreviewPath, bool SourceAvailable)
{
    public Guid Id => Link.Source.ReferenceId;
    public string Title => Link.Source.Title ?? "未命名参考";
    public string Category => Link.Category;
    public bool IsHero => Link.IsHero;
    public bool IsMoodboard => Link.IsMoodboard;
    public string Group => Link.Group;
    public string Availability => SourceAvailable ? Category : "源文件暂不可用";
}

public sealed partial class PlanningCenterViewModel
{
    public Func<string, string, Guid?, Task<IReadOnlyList<CanvasObject>>>? ReferenceSourceLoader { get; set; }
    public Func<ProjectShotReference, Task<string?>>? ReferencePathResolver { get; set; }
    public string SourceSearch { get; set; } = "";
    public System.Collections.ObjectModel.ObservableCollection<CanvasObject> SourceCandidates { get; } = [];
    public CanvasObject? SelectedSourceCandidate { get; set; }
    public string SourceCategory { get; set; } = "素材库";
    public IReadOnlyList<string> SourceCategories { get; } = ["素材库", "灵感板", "自由画布"];
    private static string ReferenceCategoryFor(ShotReferenceKind kind) => kind switch
    { ShotReferenceKind.Lighting => "灯光", ShotReferenceKind.Styling => "造型", ShotReferenceKind.Pose => "姿势", ShotReferenceKind.Storyboard => "场景", _ => "其他" };
    public async Task AddDocumentReferencesAsync(IEnumerable<ProjectShotReference> sources)
    {
        if (!HasProject) return;
        var category = ContentPage == "灯光图" ? "灯光" : ContentPage == "服化道" ? "造型" : ReferenceCategory == "全部" ? "其他" : ReferenceCategory;
        var additions = sources.Select(source => new PlanningDocumentReference(source.Normalize(), category)).ToArray();
        UpdateDocument(_document with { References = _document.References.Concat(additions).DistinctBy(item => item.Source.ReferenceId).ToArray() });
        await FlushAsync(); await RefreshDocumentReferencesAsync();
    }
    public async Task RefreshDocumentReferencesAsync()
    {
        var references = _document.References.Concat(Shots.SelectMany(shot => shot.References)
            .Select(source => new PlanningDocumentReference(source, ReferenceCategoryFor(source.Kind)))).DistinctBy(item => item.Source.ReferenceId).ToArray();
        AllProjectReferences.Clear();
        foreach (var reference in references)
        {
            var path = reference.Source.ExternalReference;
            if (path is null && ReferencePathResolver is not null) path = await ReferencePathResolver(reference.Source);
            var available = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
            var key = reference.Source.LibraryId is Guid library ? $"{library:N}/{reference.Source.AssetId:N}" : reference.Source.ExternalReference ?? reference.Source.ReferenceId.ToString();
            var cache = Path.Combine(AppDataPaths.CacheDirectory, "PlanningPreviews", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".jpg");
            if (available && (!File.Exists(cache) || File.GetLastWriteTimeUtc(path!) > File.GetLastWriteTimeUtc(cache)))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
                    using var input = File.OpenRead(path!);
                    var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 1400; bitmap.StreamSource = input; bitmap.EndInit(); bitmap.Freeze();
                    var encoder = new JpegBitmapEncoder { QualityLevel = 88 }; encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var output = File.Create(cache); encoder.Save(output);
                }
                catch (Exception error) when (error is IOException or NotSupportedException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException) { }
            }
            AllProjectReferences.Add(new(reference, File.Exists(cache) ? cache : available ? path : null, available));
        }
        OnPropertyChanged(nameof(HeroReferences)); PresentationChanged();
    }
    public async Task UpdateReferenceAsync(PlanningReferenceItem item, string action, string? value = null)
    {
        if (!await FlushAsync()) return;
        var link = item.Link;
        if (action == "移除")
        {
            UpdateDocument(_document with { References = _document.References.Where(reference => reference.Source.ReferenceId != item.Id).ToArray() });
            foreach (var shot in Shots.Where(shot => shot.References.Any(reference => reference.ReferenceId == item.Id)).ToArray())
            {
                var updated = shot with { References = shot.References.Where(reference => reference.ReferenceId != item.Id).ToArray() };
                _pendingShots[shot.ShotId] = updated;
            }
        }
        else
        {
            link = action switch
            {
                "封面" => link with { IsHero = !link.IsHero },
                "情绪板" => link with { IsMoodboard = !link.IsMoodboard },
                "分类" => link with { Category = value ?? "其他" },
                "分组" => link with { Group = value ?? "整体氛围", IsMoodboard = StylingGroups.Contains(value ?? "") ? link.IsMoodboard : true, Category = StylingGroups.Contains(value ?? "") ? "造型" : link.Category },
                _ => link
            };
            UpdateDocument(_document with { References = _document.References.Where(reference => reference.Source.ReferenceId != item.Id).Append(link).ToArray() });
        }
        await FlushAsync(); await RefreshDocumentReferencesAsync();
    }
    public async Task MoveDocumentReferenceAsync(PlanningReferenceItem item, int delta)
    {
        var links = AllProjectReferences.Select(reference => reference.Link).ToList();
        var index = links.FindIndex(reference => reference.Source.ReferenceId == item.Id); var next = index + delta;
        if (index < 0 || next < 0 || next >= links.Count) return;
        (links[index], links[next]) = (links[next], links[index]);
        UpdateDocument(_document with { References = links }); await FlushAsync(); await RefreshDocumentReferencesAsync();
    }
    public async Task UseReferenceForShotAsync(PlanningReferenceItem item, ShotReferenceKind kind)
    {
        if (SelectedShot is null) { ContentPage = "镜头清单"; SaveStatus = "请先选择或新建一个镜头，再关联参考。"; return; }
        if (!await FlushAsync()) return;
        var shot = SelectedShot;
        var updated = shot with { References = shot.References.Where(reference => reference.ReferenceId != item.Id).Append(item.Link.Source with { Kind = kind }).ToArray() };
        await _shots.SaveAsync(updated); ReplaceShot(updated); await RefreshDocumentReferencesAsync();
        SaveStatus = "已关联到当前镜头";
    }
    public void OpenReferenceSource(PlanningReferenceItem item)
    {
        if (item.Link.Source.ExternalReference is string path && File.Exists(path)) _dialogs.RevealFile(path);
        else ViewCapturedAssetsCommand.Execute(null);
    }
    public async Task<string?> ResolveOriginalAsync(PlanningReferenceItem item) =>
        item.Link.Source.ExternalReference ?? (ReferencePathResolver is null ? null : await ReferencePathResolver(item.Link.Source));
    public async Task UseReferenceForColorAsync(PlanningReferenceItem item)
    {
        if (ProjectId is not Guid projectId || !await FlushAsync()) return;
        var path = await ResolveOriginalAsync(item);
        if (path is null || !File.Exists(path)) { SaveStatus = "源文件暂不可用，恢复来源后再用于参考仿色。"; return; }
        var source = await new RAWSelectionAssistant.Services.ReferenceLookPreviewService().AnalyzeExternalReferenceAsync(path, CancellationToken.None);
        var now = DateTimeOffset.UtcNow;
        var look = new ReferenceLook(Guid.NewGuid(), item.Title, projectId, [source], new(), now, now);
        await _looks.SaveAsync(look);
        ColorReferenceRequested?.Invoke(this, (projectId, look.ReferenceLookId));
    }
    public async Task SearchReferenceSourcesAsync()
    {
        SourceCandidates.Clear();
        if (SourceCategory == "自由画布")
        {
            foreach (var canvas in await _canvases.ListAsync())
                foreach (var item in canvas.Objects.Where(item => item.IsImage && (string.IsNullOrWhiteSpace(SourceSearch) || item.Name.Contains(SourceSearch, StringComparison.CurrentCultureIgnoreCase)))) SourceCandidates.Add(item);
            if (ReferenceSourceLoader is not null)
                foreach (var item in await ReferenceSourceLoader(SourceCategory, SourceSearch, ProjectId))
                    if (!SourceCandidates.Any(existing => existing.CanvasId == item.CanvasId && existing.ObjectId == item.ObjectId)) SourceCandidates.Add(item);
        }
        else if (ReferenceSourceLoader is not null)
            foreach (var item in await ReferenceSourceLoader(SourceCategory, SourceSearch, ProjectId)) SourceCandidates.Add(item);
    }
    public async Task AddSourceCandidateAsync()
    {
        if (SelectedSourceCandidate is not { } item) return;
        var source = item.AssetId is Guid assetId && item.LibraryId != Guid.Empty
            ? new ProjectShotReference(Guid.NewGuid(), ShotReferenceKind.General, item.LibraryId, assetId, Title: item.Name)
            : new ProjectShotReference(Guid.NewGuid(), ShotReferenceKind.General, ExternalReference: item.SourcePath, Title: item.Name);
        await AddDocumentReferencesAsync([source]);
    }
    public async Task SendToCanvasAsync(PlanningReferenceItem? item = null)
    {
        if (ProjectId is not Guid id || !await FlushAsync()) return;
        var canvas = new CanvasDocument { Name = DocumentTitle + " · 视觉方向", ProjectId = id };
        var references = item is null ? AllProjectReferences.Where(reference => reference.IsMoodboard).ToArray() : [item];
        canvas = canvas with { Objects = references.Select((reference, index) => new CanvasObject
        {
            CanvasId = canvas.CanvasId, LibraryId = reference.Link.Source.LibraryId ?? Guid.Empty, AssetId = reference.Link.Source.AssetId,
            SourcePath = reference.Link.Source.ExternalReference ?? reference.PreviewPath ?? "", Name = reference.Title,
            X = index % 3 * 350, Y = index / 3 * 280
        }).ToArray() };
        await _canvases.SaveAsync(canvas);
        await _planning.AddVisualLinkAsync(id, new(Guid.NewGuid(), PlanningVisualLinkKind.FreeCanvas, canvas.CanvasId, canvas.Name));
        ProjectCanvases.Add(canvas); CanvasRequested?.Invoke(this, canvas);
    }
}
