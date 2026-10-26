using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed record PlanningReferenceItem(PlanningDocumentReference Link, string? PreviewPath, bool SourceAvailable)
{
    public string? OriginalPath { get; init; }
    public string? PreviewStatus {get;init;}
    public string? PreviewDetails {get;init;}
    public System.Windows.Media.Imaging.BitmapSource? PreviewImage {get;init;}
    public bool ThumbnailCacheHit {get;init;}
    public Guid Id => Link.Source.ReferenceId;
    public string Title => Link.Source.Title ?? "未命名参考";
    public string Category => Link.Category;
    public bool IsHero => Link.IsHero;
    public bool IsMoodboard => Link.IsMoodboard;
    public string Group => Link.Group;
    public string Availability => PreviewStatus ?? (Link.ProjectCopy is not null ? (SourceAvailable ? "项目自有副本 · " + Category : "项目副本不可用，请检查文件") : SourceAvailable ? "外链/库引用 · " + Category : "源文件暂不可用");
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
    public async Task AddDocumentReferencesAsync(IEnumerable<ProjectShotReference> sources, bool importProjectCopy = false)
    {
        if (!HasProject) return;
        var category = ContentPage is "灯位图" or "灯光图" ? "灯光" : ContentPage is "服化道与道具" or "服化道" ? "造型" : ReferenceCategory == "全部" ? "其他" : ReferenceCategory;
        var additions = sources.Select(source => new PlanningDocumentReference(source.Normalize(), category)).ToArray();
        if (importProjectCopy) { await ImportProjectCopiesAsync(additions, []); return; }
        UpdateDocument(_document with { References = _document.References.Concat(additions).DistinctBy(item => item.Source.ReferenceId).ToArray() });
        await FlushAsync(); await RefreshDocumentReferencesAsync();
    }
    private CancellationTokenSource? _referenceRefreshCancellation;
    private long _referenceRefreshGeneration;
    public long ReferenceImageEpoch=>_referenceRefreshGeneration;
    private void InvalidateReferenceRefresh()
    {
        _referenceRefreshGeneration++;_referenceRefreshCancellation?.Cancel();AllProjectReferences.Clear();OnPropertyChanged(nameof(ReferenceImageEpoch));OnPropertyChanged(nameof(HeroReferences));
    }
    public Task RefreshDocumentReferencesAsync()=>RefreshDocumentReferencesAsync(CancellationToken.None);
    public async Task RefreshDocumentReferencesAsync(CancellationToken token)
    {
        var generation=++_referenceRefreshGeneration;_referenceRefreshCancellation?.Cancel();_referenceRefreshCancellation=new CancellationTokenSource();using var linked=CancellationTokenSource.CreateLinkedTokenSource(token,_referenceRefreshCancellation.Token);var cancel=linked.Token;
        var project=ProjectId;var document=PlanningDocumentId;AllProjectReferences.Clear();OnPropertyChanged(nameof(ReferenceImageEpoch));OnPropertyChanged(nameof(HeroReferences));
        try
        {
            await RefreshShotLinkReceiptsAsync();if(cancel.IsCancellationRequested||generation!=_referenceRefreshGeneration||ProjectId!=project||PlanningDocumentId!=document)return;
            var snapshot=_document;var shots=Shots.ToArray();var imported=ImportedPackageRoot is not null||IsPackageImported;var resolver=ReferencePathResolver;
            var references=snapshot.References.Concat(shots.SelectMany(shot=>shot.References.Select(source=>PlanningReferenceLinks.ForShot(snapshot,shot,source,ReferenceCategoryFor(source.Kind))))).DistinctBy(item=>item.Source.ReferenceId).ToArray();var rows=new List<PlanningReferenceItem>();
            foreach(var reference in references)
            {
                cancel.ThrowIfCancellationRequested();
                if(PlanningReferenceLinks.HasConflict(snapshot,shots,reference.Source.ReferenceId)){rows.Add(new(reference with{ProjectCopy=null,ReconnectedFile=null},null,false));continue;}
                if(reference.ProjectCopy is {} owned)
                {
                    var item=await Task.Run(()=>
                    {
                        try
                        {
                            var thumbnail=RAWSelectionAssistant.Services.PlanningOwnedReferenceImage.ReadThumbnail(owned,project??Guid.Empty,document??Guid.Empty,reference.Source.ReferenceId,AppDataPaths.CacheDirectory,cancel);
                            return new PlanningReferenceItem(reference,thumbnail.Path,true){OriginalPath=PlanningProjectCopyStorage.Resolve(owned),PreviewImage=thumbnail.Image,ThumbnailCacheHit=thumbnail.CacheHit};
                        }
                        catch(Exception error)when(error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException or System.Runtime.InteropServices.COMException)
                        {
                            new RAWSelectionAssistant.Core.Services.FileLogService().Error("策划自有参考预览失败",error);
                            // Separate intact-but-unrenderable from changed/missing; never return a stale cache path.
                            try{using var verified=PlanningProjectCopyStorage.OpenVerifiedRead(owned,cancel);return new PlanningReferenceItem(reference,null,true){OriginalPath=PlanningProjectCopyStorage.Resolve(owned),PreviewStatus="已保存但无法预览",PreviewDetails=error.ToString()};}
                            catch(Exception integrity)when(integrity is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException){return new PlanningReferenceItem(reference,null,false){PreviewStatus="项目副本不可用，请检查文件",PreviewDetails=integrity.ToString()};}
                        }
                    },cancel);rows.Add(item);continue;
                }
                var path=imported?null:reference.Source.ExternalReference;
                if(reference.ReconnectedFile is {} reconnected&&reference.Source.ExternalReference is {} original){try{path=PlanningExternalFile.ResolveVerified(reconnected,reference.Source.ReferenceId,"reference",original);}catch(Exception error)when(PackageError(error)){path=null;}}
                if(!imported&&reference.ReconnectedFile is null&&path is null&&resolver is not null)path=await resolver(reference.Source);cancel.ThrowIfCancellationRequested();
                var available=!string.IsNullOrWhiteSpace(path)&&File.Exists(path);var key=reference.ReconnectedFile is {} receipt?receipt.ReferenceId.ToString("N")+"/"+receipt.Sha256:reference.Source.LibraryId is Guid library?$"{library:N}/{reference.Source.AssetId:N}":reference.Source.ExternalReference??reference.Source.ReferenceId.ToString();var cache=Path.Combine(AppDataPaths.CacheDirectory,"PlanningPreviews",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))+".jpg");
                if(available&&(!File.Exists(cache)||File.GetLastWriteTimeUtc(path!)>File.GetLastWriteTimeUtc(cache)))
                {
                    try{Directory.CreateDirectory(Path.GetDirectoryName(cache)!);using var input=File.OpenRead(path!);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=1400;bitmap.StreamSource=input;bitmap.EndInit();bitmap.Freeze();var encoder=new JpegBitmapEncoder{QualityLevel=88};encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(cache);encoder.Save(output);}
                    catch(Exception error)when(error is IOException or NotSupportedException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException){}
                }
                rows.Add(new(reference,available?File.Exists(cache)?cache:path:null,available){OriginalPath=available?path:null});
            }
            cancel.ThrowIfCancellationRequested();if(generation!=_referenceRefreshGeneration||ProjectId!=project||PlanningDocumentId!=document)return;
            foreach(var row in rows)AllProjectReferences.Add(row);OnPropertyChanged(nameof(HeroReferences));PresentationChanged();
        }
        catch(OperationCanceledException)when(cancel.IsCancellationRequested){/* Superseded refresh never publishes rows. */}
    }
    public async Task UpdateReferenceAsync(PlanningReferenceItem item, string action, string? value = null)
    {
        if (!await FlushAsync()) return;
        var link = item.Link;
        if (action == "移除")
        {
            UpdateDocument(_document with { References = _document.References.Where(reference => reference.Source.ReferenceId != item.Id).ToArray(),ShotExternalLinks=_document.ShotExternalLinks.Where(link=>link.ReferenceId!=item.Id).ToArray() });
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
        if (item.OriginalPath is string path && File.Exists(path)) _dialogs.RevealFile(path);
        else ViewCapturedAssetsCommand.Execute(null);
    }
    public async Task<string?> ResolveOriginalAsync(PlanningReferenceItem item)
    {
        if(PlanningReferenceLinks.HasConflict(_document,Shots,item.Id))return null;
        if (item.Link.ProjectCopy is {} copy) { try { PlanningProjectCopyStorage.ReadVerified(copy); return PlanningProjectCopyStorage.Resolve(copy); } catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return null; } }
        if(item.Link.ReconnectedFile is {} reconnected && item.Link.Source.ExternalReference is {} original)
        { try { return PlanningExternalFile.ResolveVerified(reconnected,item.Id,"reference",original); } catch(Exception e)when(PackageError(e)){return null;} }
        if(IsPackageImported || ImportedPackageRoot is not null)return null;
        return item.Link.Source.ExternalReference ?? (ReferencePathResolver is null ? null : await ReferencePathResolver(item.Link.Source));
    }
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
        if(item.PlanningSource is not null)
        {SaveStatus="项目自有画布参考保留原策划身份；本入口不将其转为外链，请回原策划使用。";return;}
        var source = item.AssetId is Guid assetId && item.LibraryId != Guid.Empty
            ? new ProjectShotReference(Guid.NewGuid(), ShotReferenceKind.General, item.LibraryId, assetId, Title: item.Name)
            : new ProjectShotReference(Guid.NewGuid(), ShotReferenceKind.General, ExternalReference: item.SourcePath, Title: item.Name);
        await AddDocumentReferencesAsync([source]);
    }
    public async Task SendToCanvasAsync(PlanningReferenceItem? item = null)
    {
        if (ProjectId is not Guid id || !await FlushAsync()) return;
        var canvas = new CanvasDocument { Name = DocumentTitle + " · 视觉方向", ProjectId = id,StorageRoot=_canvases.DirectoryPath };
        var references = item is null ? AllProjectReferences.Where(reference => reference.IsMoodboard).ToArray() : [item];
        var objects=new List<CanvasObject>();
        foreach(var reference in references)
        {
            // Re-resolve current identity after editor flush. A stale card from a different
            // project/refresh must never transfer its bytes into this canvas.
            var current=AllProjectReferences.SingleOrDefault(r=>r.Id==reference.Id);
            if(current is null)throw new InvalidDataException("参考已不属于当前项目，请重新选择。");
            CanvasPlanningSource? owned=null;string path;
            if(current.Link.ProjectCopy is {} copy)
            {
                if(copy.ProjectId!=id||copy.DocumentId!=PlanningDocumentId||copy.ReferenceId!=current.Id)
                    throw new InvalidDataException("自有参考与当前项目身份不符，未发送画布。");
                owned=CanvasPlanningSource.From(copy);path="";
            }
            else path=await ResolveOriginalAsync(current)??throw new IOException("参考来源不可用，未使用缩略缓存发送画布。");
            objects.Add(new(){CanvasId=canvas.CanvasId,LibraryId=current.Link.Source.LibraryId??Guid.Empty,AssetId=current.Link.Source.AssetId,
                SourcePath=path,PlanningSource=owned,ContentHash=owned?.Copy.Sha256??"",Name=current.Title,
                X=objects.Count%3*350,Y=objects.Count/3*280});
        }
        if(ProjectId!=id)throw new InvalidOperationException("项目已切换，未发送画布。");
        canvas=canvas with{Objects=objects};await _canvases.SaveAsync(canvas);
        await _planning.AddVisualLinkAsync(id,new(Guid.NewGuid(),PlanningVisualLinkKind.FreeCanvas,canvas.CanvasId,canvas.Name));
        ProjectCanvases.Add(canvas);CanvasRequested?.Invoke(this,canvas);
    }
}
