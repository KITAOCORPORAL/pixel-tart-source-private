using System.IO;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace PixelTart.Modules.AssetLibrary;

public sealed partial class AssetLibraryViewModel
{
    public string CanvasDirectory => Path.Combine(Path.GetDirectoryName(_databasePath)!, "canvases");
    public Guid CanvasLibraryId => _libraryIdForTray;
    public IVisualAnalysisService VisualAnalysisService => _visualAnalysis;
    public Task<AssetItem?> GetAssetForAnalysisAsync(Guid assetId)=>_repository.GetAssetAsync(assetId,_lifetimeCancellation.Token);
    public CanvasObject CanvasReference(AssetItem asset) => new()
    {
        LibraryId = _libraryIdForTray, AssetId = asset.AssetId, ContentHash = asset.ContentHash ?? "",
        SourcePath = GetDisplaySourcePath(asset), Name = asset.DisplayName,
        SourceWidth = asset.Width ?? 1200, SourceHeight = asset.Height ?? 800,
        Width = 300, Height = 300d * (asset.Height ?? 800) / Math.Max(1, asset.Width ?? 1200)
    };
    public async Task<IReadOnlyList<CanvasObject>> CanvasBoardReferencesAsync(IEnumerable<InspirationTrayCardView> cards)
    {
        var result = new List<CanvasObject>();
        foreach (var card in cards)
        {
            var asset = card.Entry.Reference.LibraryId == _libraryIdForTray ? await _repository.GetAssetAsync(card.AssetId, _lifetimeCancellation.Token) : null;
            result.Add(asset is not null ? CanvasReference(asset) : new CanvasObject { LibraryId=card.Entry.Reference.LibraryId,AssetId=card.AssetId,ContentHash=card.ContentHash,SourcePath=card.ThumbnailPath??"",Name=card.AssetLabel,SourceWidth=1200,SourceHeight=800,Width=300,Height=200 });
        }
        return result;
    }
    public async Task<IReadOnlyList<CanvasObject>> LoadCanvasSourcesAsync(string source, string search, Guid? projectId)
    {
        if(source=="最近使用")return (await new CanvasDocumentStore(CanvasDirectory).ListAsync(token:_lifetimeCancellation.Token)).SelectMany(document=>document.Objects).Where(item=>!item.IsText&&(string.IsNullOrWhiteSpace(search)||item.Name.Contains(search,StringComparison.CurrentCultureIgnoreCase))).DistinctBy(item=>(item.LibraryId,item.AssetId)).Take(100).ToArray();
        IReadOnlyList<CanvasObject> result;
        if(source is "灵感板" or "临时收集")
        {
            await RefreshInspirationTrayAsync();await RefreshCollectionsAsync();
            var cards=source=="临时收集"?InspirationTrayCards.AsEnumerable():ActiveCollectionCards.AsEnumerable();
            if(source=="灵感板"&&!cards.Any())
            {
                var entries=new List<InspirationTrayCardView>();
                foreach(var collection in InspirationCollections)
                    foreach(var entry in await ((SqliteInspirationTrayService)_inspirationTray).ListCollectionEntriesAsync(collection.CollectionId,_lifetimeCancellation.Token))entries.Add(await ResolveInspirationCardAsync(entry));
                cards=entries;
            }
            result=await CanvasBoardReferencesAsync(cards);
        }
        else
        {
            var query=new AssetLibraryQuery(PageSize:500, SearchText:search);
            var assets=new List<AssetItem>();
            do{var page=await _repository.QueryAsync(query,_lifetimeCancellation.Token);assets.AddRange(page.Items);query=query with{Cursor=page.NextCursor};}while(query.Cursor is not null);
            if(source=="当前项目")
            {
                if(projectId is null)return [];
                var ids=(await _repository.ListProjectAssetLinksAsync(projectId:projectId,cancellationToken:_lifetimeCancellation.Token)).Select(link=>link.AssetId).ToHashSet();assets=assets.Where(asset=>ids.Contains(asset.AssetId)).ToList();
            }
            result=assets.Select(CanvasReference).ToArray();
        }
        return result.Where(item=>string.IsNullOrWhiteSpace(search)||item.Name.Contains(search,StringComparison.CurrentCultureIgnoreCase)).DistinctBy(item=>(item.LibraryId,item.AssetId)).ToArray();
    }
    public async Task SaveCanvasBoardAsync(IReadOnlyList<CanvasObject> objects, Guid? targetBoard, Guid? projectId)
    {
        var references=objects.Where(item=>item.AssetId is not null&&item.ContentHash.Length==64).Select(item=>new AssetLibraryStableReference(item.LibraryId,item.AssetId!.Value,item.ContentHash)).Distinct().ToArray();
        if(references.Length==0)throw new InvalidOperationException("这些素材尚未完成引用校验。");
        var service=(SqliteInspirationTrayService)_inspirationTray;
        var boardId=targetBoard??(await service.CreateCollectionAsync($"画布灵感 {DateTime.Now:MMdd-HHmmss}",projectId,_lifetimeCancellation.Token)).CollectionId;
        await service.AddRangeAsync(references,"free-canvas",_lifetimeCancellation.Token);
        var set=references.ToHashSet();var entries=await service.ListAsync(_lifetimeCancellation.Token);
        await service.AddEntriesToCollectionAsync(boardId,entries.Where(entry=>set.Contains(entry.Reference)).Select(entry=>entry.TrayEntryId),_lifetimeCancellation.Token);
        await RefreshCollectionsAsync();await RefreshInspirationTrayAsync();
    }
    public async Task<IReadOnlyList<(Guid Id,string Name)>> CanvasBoardsAsync(){await RefreshCollectionsAsync();return InspirationCollections.Select(item=>(item.CollectionId,item.Name)).ToArray();}
    public async Task<IReadOnlyList<(Guid? Id,string Name)>> CanvasProjectsAsync()
    {
        var projects=await new SqliteProjectRepository(new PixelTartDatabase(_productDatabasePath)).ListAsync(_lifetimeCancellation.Token);
        return new (Guid? Id,string Name)[]{(null,"无项目")}.Concat(projects.Select(project=>((Guid?)project.Id,project.Name))).ToArray();
    }
    public async Task RevealCanvasAssetAsync(CanvasObject item)
    {
        if(item.LibraryId!=_libraryIdForTray){Status="请先切换到此照片所属的素材库。";return;}
        if(item.AssetId is not Guid id||await _repository.GetAssetAsync(id,_lifetimeCancellation.Token) is not {} asset)return;
        ClearFilters();SearchText=asset.DisplayName;await RefreshAsync();SyncSelection([asset]);IsCollectionPanelOpen=false;
    }
    public async Task<CanvasPalette> AnalyzeCanvasPaletteAsync(IReadOnlyList<CanvasObject> objects,int count)
    {
        var analyses=new List<AssetVisualAnalysisResult>();var ids=new List<Guid>();
        foreach(var item in objects.Where(item=>item.IsImage))
        {
            if(item.AssetId is not Guid id||item.LibraryId!=_libraryIdForTray||await _repository.GetAssetAsync(id,_lifetimeCancellation.Token) is not {} asset)continue;
            var request=await WpfVisualAnalysisDecoder.DecodeAsync(asset,count,_lifetimeCancellation.Token);
            analyses.Add(await _visualAnalysis.AnalyzeAsync(request,_lifetimeCancellation.Token));ids.Add(id);
        }
        if(analyses.Count==0)throw new InvalidOperationException("所选对象没有可分析的本地素材引用。");
        var palette=analyses.Count==1?analyses[0].Palette:_visualAnalysis.Combine(analyses,count,_lifetimeCancellation.Token).Palette;
        return new(palette.Select(color=>new CanvasPaletteColor(color.Hex,color.Hue,color.Saturation,color.Lightness,color.Weight)).ToArray(),ids,analyses.Count>1);
    }
    public async Task<VisualAnalysisSurfacePayload> AnalyzeAssetsForSurfaceAsync(IReadOnlyList<AssetItem> assets,int count)
    {
        if(assets.Count==0)throw new ArgumentException("至少需要一张照片。",nameof(assets));
        var items=new List<VisualAnalysisSurfaceItem>();
        foreach(var asset in assets.DistinctBy(item=>item.AssetId))
        {
            var request=await WpfVisualAnalysisDecoder.DecodeAsync(asset,count,_lifetimeCancellation.Token);
            var analysis=await _visualAnalysis.AnalyzeAsync(request,_lifetimeCancellation.Token);
            items.Add(new(asset,analysis,request.Pixels));
        }
        var combined=_visualAnalysis.Combine(items.Select(item=>item.Analysis),count,_lifetimeCancellation.Token);
        return new(items,combined);
    }
}

public sealed record VisualAnalysisSurfaceItem(AssetItem Asset,AssetVisualAnalysisResult Analysis,VisualPixelBuffer Pixels);
public sealed record VisualAnalysisSurfacePayload(IReadOnlyList<VisualAnalysisSurfaceItem> Items,CombinedVisualAnalysisResult Aggregate);
