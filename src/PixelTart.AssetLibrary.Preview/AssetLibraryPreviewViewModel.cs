using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Utilities;

namespace PixelTart.AssetLibrary.Preview;

public sealed class AssetLibraryPreviewViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IAssetLibraryRepository _repository;
    private readonly AssetVisualAnalysisService _visualAnalysis;
    private readonly AssetVisualAnalysisSelectionCoordinator _analysisCoordinator = new();
    private CancellationTokenSource? _analysisCancellation;
    private readonly DispatcherTimer _searchDebounce;
    private string _searchText = string.Empty;
    private string _status = "正在准备素材库";
    private string _tagInput = string.Empty;
    private string _folderSearch = string.Empty;
    private string _newFolderName = string.Empty;
    private string _smartFolderName = "精选参考";
    private string _smartRuleValue = "4";
    private string? _nextCursor;
    private AssetItem? _selectedAsset;
    private AssetFolder? _selectedFolder;
    private AssetTag? _selectedTag;
    private SmartFolder? _selectedSmartFolder;
    private AssetVisualAnalysisResult? _analysis;
    private bool _isAnalyzing;
    private int _paletteSize = 5;
    private double _thumbnailWidth = 180;
    private IReadOnlyList<Guid> _lastFolderIds = [];

    public AssetLibraryPreviewViewModel(string databasePath)
    {
        var database = new AssetLibraryDatabase(databasePath);
        _repository = new SqliteAssetLibraryRepository(database);
        _visualAnalysis = new(new SqliteAssetVisualAnalysisCache(database));
        _searchDebounce = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(280) };
        _searchDebounce.Tick += async (_, _) => { _searchDebounce.Stop(); await RefreshAsync(); };
        RefreshCommand = new(RefreshAsync); ImportCommand = new(ImportAsync); LoadMoreCommand = new(LoadMoreAsync, () => _nextCursor is not null);
        NewFolderCommand = new(NewFolderAsync); NewSubfolderCommand = new(NewSubfolderAsync, () => SelectedFolder is not null); BatchFolderCommand = new(BatchFolderAsync);
        NewTagCommand = new(NewTagAsync); ApplyTagsCommand = new(ApplyTagsAsync, () => SelectedAssets.Count > 0 && !string.IsNullOrWhiteSpace(TagInput));
        AddFolderCommand = new(AddFolderAsync, () => SelectedAssets.Count > 0 && SelectedFolder is not null); UndoCommand = new(UndoAsync, () => LastUndoToken is not null);
        SaveSmartFolderCommand = new(SaveSmartFolderAsync); RelinkCommand = new(RelinkAsync); RateCommand = new AsyncCommand<int>(value => RateSelectedAsync(value));
    }

    public ObservableCollection<AssetItem> Assets { get; } = [];
    public ObservableCollection<AssetItem> SelectedAssets { get; } = [];
    public ObservableCollection<AssetFolderTreeItem> FolderTree { get; } = [];
    public ObservableCollection<AssetFolder> Folders { get; } = [];
    public ObservableCollection<AssetFolder> ClassifierFolders { get; } = [];
    public ObservableCollection<AssetFolder> RecentFolders { get; } = [];
    public ObservableCollection<AssetFolder> FavoriteFolders { get; } = [];
    public ObservableCollection<AssetTag> Tags { get; } = [];
    public ObservableCollection<TagGroup> TagGroups { get; } = [];
    public ObservableCollection<SmartFolder> SmartFolders { get; } = [];
    public ObservableCollection<AssetTagUsageSummary> SelectedTagSummary { get; } = [];
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) { _searchDebounce.Stop(); _searchDebounce.Start(); } } }
    public string TagInput { get => _tagInput; set { if (SetProperty(ref _tagInput, value)) ApplyTagsCommand.RaiseCanExecuteChanged(); } }
    public string FolderSearch { get => _folderSearch; set { if (SetProperty(ref _folderSearch, value)) RefreshClassifierFolders(); } }
    public string NewFolderName { get => _newFolderName; set => SetProperty(ref _newFolderName, value); }
    public string SmartFolderName { get => _smartFolderName; set => SetProperty(ref _smartFolderName, value); }
    public string SmartRuleValue { get => _smartRuleValue; set => SetProperty(ref _smartRuleValue, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public int VisibleCount => Assets.Count;
    public int SelectionCount => SelectedAssets.Count;
    public bool HasMultipleSelection => SelectionCount > 1;
    public bool HasSingleSelection => SelectionCount == 1;
    public double ThumbnailWidth { get => _thumbnailWidth; set => SetProperty(ref _thumbnailWidth, Math.Clamp(value, 120, 280)); }
    public int PaletteSize { get => _paletteSize; set { if (SetProperty(ref _paletteSize, value) && SelectedAsset is not null) _ = AnalyzeSelectedAsync(); } }
    public AssetVisualAnalysisResult? Analysis { get => _analysis; private set { if (SetProperty(ref _analysis, value)) OnPropertyChanged(nameof(AnalysisStatus)); } }
    public bool IsAnalyzing { get => _isAnalyzing; private set { if (SetProperty(ref _isAnalyzing, value)) OnPropertyChanged(nameof(AnalysisStatus)); } }
    public string AnalysisStatus => HasMultipleSelection ? $"已选择 {SelectionCount} 张图片；不会用首张分析冒充整组。" : IsAnalyzing ? "正在分析视觉数据…" : Analysis is null ? "选择一张图片查看本地视觉统计" : Analysis.CacheHit ? "视觉分析已从缓存读取" : "视觉分析完成并缓存";
    public AssetLibraryUndoToken? LastUndoToken { get; private set; }
    public AssetItem? SelectedAsset { get => _selectedAsset; set { if (SetProperty(ref _selectedAsset, value)) { SyncSelection(value is null ? [] : [value]); } } }
    public AssetFolder? SelectedFolder { get => _selectedFolder; set { if (SetProperty(ref _selectedFolder, value)) { RaiseActions(); _ = RefreshAsync(); } } }
    public AssetTag? SelectedTag { get => _selectedTag; set { if (SetProperty(ref _selectedTag, value)) _ = RefreshAsync(); } }
    public SmartFolder? SelectedSmartFolder { get => _selectedSmartFolder; set { if (SetProperty(ref _selectedSmartFolder, value)) _ = RefreshAsync(); } }
    public AsyncCommand RefreshCommand { get; } public AsyncCommand ImportCommand { get; } public AsyncCommand LoadMoreCommand { get; }
    public AsyncCommand NewFolderCommand { get; } public AsyncCommand NewSubfolderCommand { get; } public AsyncCommand BatchFolderCommand { get; }
    public AsyncCommand NewTagCommand { get; } public AsyncCommand ApplyTagsCommand { get; } public AsyncCommand AddFolderCommand { get; }
    public AsyncCommand UndoCommand { get; } public AsyncCommand SaveSmartFolderCommand { get; } public AsyncCommand RelinkCommand { get; } public AsyncCommand<int> RateCommand { get; }

    public async Task InitializeAsync()
    {
        await _repository.InitializeAsync(); await RefreshFilterListsAsync();
        if (Folders.Count == 0) await SeedPreviewStructureAsync();
        await RefreshAsync();
        var journal = await _repository.ListUndoJournalAsync(1); LastUndoToken = journal.FirstOrDefault(x => !x.IsUndone)?.Token; RaiseActions();
    }

    public void SyncSelection(IEnumerable<AssetItem> items)
    {
        var selected = items.DistinctBy(x => x.AssetId).ToArray();
        SelectedAssets.Clear(); foreach (var item in selected) SelectedAssets.Add(item);
        if (selected.Length != 1) { _selectedAsset = selected.FirstOrDefault(); OnPropertyChanged(nameof(SelectedAsset)); _analysisCoordinator.ClearSelection(); Analysis = null; IsAnalyzing = false; }
        else if (_selectedAsset?.AssetId != selected[0].AssetId) { _selectedAsset = selected[0]; OnPropertyChanged(nameof(SelectedAsset)); }
        OnPropertyChanged(nameof(SelectionCount)); OnPropertyChanged(nameof(HasMultipleSelection)); OnPropertyChanged(nameof(HasSingleSelection)); OnPropertyChanged(nameof(AnalysisStatus));
        _ = RefreshSelectionSummaryAsync(); if (selected.Length == 1) _ = AnalyzeSelectedAsync(); RaiseActions();
    }

    private async Task RefreshAsync()
    {
        var page = await _repository.QueryAsync(BuildQuery()); Assets.Clear(); foreach (var asset in page.Items) Assets.Add(asset); _nextCursor = page.NextCursor;
        Status = page.RegexError is null ? $"共 {page.TotalCount:N0} 个素材，当前显示 {Assets.Count:N0} 个" : $"筛选错误：{page.RegexError}"; OnPropertyChanged(nameof(VisibleCount)); LoadMoreCommand.RaiseCanExecuteChanged();
    }

    private async Task LoadMoreAsync() { if (_nextCursor is null) return; var page = await _repository.QueryAsync(BuildQuery(_nextCursor)); foreach (var asset in page.Items) Assets.Add(asset); _nextCursor = page.NextCursor; Status = $"已加载 {Assets.Count:N0} 个素材"; OnPropertyChanged(nameof(VisibleCount)); LoadMoreCommand.RaiseCanExecuteChanged(); }

    private async Task ImportAsync()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Filter = "图片|*.jpg;*.jpeg;*.png;*.webp;*.tif;*.tiff;*.arw;*.cr2;*.cr3;*.nef;*.raf;*.dng|所有文件|*.*" };
        if (dialog.ShowDialog() != true) return; var result = await _repository.ImportAsync(dialog.FileNames.Select(path => new AssetImportRequest(path, ComputeContentHash: true))); Status = result.Cancelled ? "导入已取消" : $"已索引 {result.ImportedCount:N0} 项，跳过重复 {result.SkippedCount:N0} 项（未修改源文件）"; await RefreshAsync();
    }

    public async Task ImportDemoDirectoryAsync(string directory)
    {
        var files = Directory.EnumerateFiles(directory, "*.jpg", SearchOption.TopDirectoryOnly).Take(500).ToArray();
        if (files.Length == 0) return;
        var result = await _repository.ImportAsync(files.Select(path => new AssetImportRequest(path, ComputeContentHash: true)));
        Status = $"合成测试图库：新索引 {result.ImportedCount}，已存在 {result.SkippedCount}";
        await RefreshAsync();
    }

    private async Task NewFolderAsync() { var name = string.IsNullOrWhiteSpace(NewFolderName) ? UniqueName("新建文件夹", Folders.Select(x => x.Name)) : NewFolderName.Trim(); await _repository.SaveFolderAsync(new(Guid.NewGuid(), null, name)); NewFolderName = string.Empty; await RefreshFilterListsAsync(); Status = $"已创建文件夹：{name}"; }
    private async Task NewSubfolderAsync() { var name = string.IsNullOrWhiteSpace(NewFolderName) ? UniqueName("子文件夹", Folders.Where(x => x.ParentFolderId == SelectedFolder!.FolderId).Select(x => x.Name)) : NewFolderName.Trim(); await _repository.SaveFolderAsync(new(Guid.NewGuid(), SelectedFolder!.FolderId, name)); NewFolderName = string.Empty; await RefreshFilterListsAsync(); Status = $"已创建子文件夹：{SelectedFolder.Name} / {name}"; }
    private async Task BatchFolderAsync() { var result = await _repository.BatchCreateFoldersAsync("人体/身体\n人体/宗教\n参考/白棚\n参考/黑色\n灯光/硬光"); await RefreshFilterListsAsync(); Status = $"批量创建 {result.Created.Count} 个层级文件夹"; }
    private async Task NewTagAsync() { var tags = await _repository.BatchCreateTagsAsync(string.IsNullOrWhiteSpace(TagInput) ? "新标签" : TagInput); TagInput = string.Empty; await RefreshFilterListsAsync(); Status = $"已创建/找到 {tags.Count} 个标签"; }
    private async Task ApplyTagsAsync() { var tags = await _repository.BatchCreateTagsAsync(TagInput); var result = await _repository.AddTagsAsync(SelectedAssets.Select(x => x.AssetId), tags.Select(x => x.TagId)); LastUndoToken = result.UndoToken; Status = $"已为 {SelectionCount} 项添加 {tags.Count} 个标签"; TagInput = string.Empty; await RefreshFilterListsAsync(); await RefreshSelectionSummaryAsync(); RaiseActions(); }
    private async Task AddFolderAsync() { await ApplyFoldersAsync([SelectedFolder!.FolderId]); }

    public async Task ApplyFoldersAsync(IEnumerable<Guid> folderIds)
    {
        var ids = folderIds.Distinct().ToArray(); if (ids.Length == 0 || SelectedAssets.Count == 0) return;
        var result = await _repository.AddToFoldersAsync(SelectedAssets.Select(x => x.AssetId), ids);
        foreach (var folderId in ids) { var folder = Folders.FirstOrDefault(x => x.FolderId == folderId); if (folder is not null) { RecentFolders.Remove(folder); RecentFolders.Insert(0, folder); while (RecentFolders.Count > 6) RecentFolders.RemoveAt(RecentFolders.Count - 1); } }
        _lastFolderIds = ids; LastUndoToken = result.UndoToken; Status = $"已添加 {result.ChangedCount} 项 membership 到 {ids.Length} 个文件夹"; RaiseActions();
    }

    public async Task RepeatLastFolderMembershipAsync() { if (_lastFolderIds.Count == 0) { Status = "Shift+D：尚无上一次文件夹分类"; return; } await ApplyFoldersAsync(_lastFolderIds); Status = $"已重复上次分类：{_lastFolderIds.Count} 个文件夹"; }
    public async Task RateSelectedAsync(int rating) { rating = Math.Clamp(rating, 0, 5); var result = await _repository.UpdateAssetsMetadataAsync(SelectedAssets.Select(asset => asset.AssetId), rating: rating); LastUndoToken = result.UndoToken; Status = $"已将 {SelectionCount} 项评分设为 {rating}"; RaiseActions(); }
    private async Task UndoAsync() { if (LastUndoToken is null) return; Status = await _repository.UndoAsync(LastUndoToken) ? "已撤销上一项素材库操作" : "撤销记录已失效"; LastUndoToken = null; await RefreshFilterListsAsync(); RaiseActions(); }

    private async Task SaveSmartFolderAsync()
    {
        var folder = new SmartFolder(Guid.NewGuid(), string.IsNullOrWhiteSpace(SmartFolderName) ? "新智能文件夹" : SmartFolderName.Trim(), SmartFolderLogic.And);
        await _repository.SaveSmartFolderAsync(folder, [new(Guid.NewGuid(), folder.SmartFolderId, SmartFolderField.Rating, SmartFolderOperator.GreaterThanOrEqual, SmartRuleValue), new(Guid.NewGuid(), folder.SmartFolderId, SmartFolderField.IsMissing, SmartFolderOperator.IsFalse)]); await RefreshFilterListsAsync(); Status = $"已保存智能文件夹：{folder.Name}";
    }

    private async Task RelinkAsync() { var dialog = new OpenFolderDialog { Title = "选择素材新的根目录", Multiselect = false }; if (dialog.ShowDialog() != true) return; var result = await _repository.RelinkMissingAssetsAsync(new(dialog.FolderName)); Status = $"已重新连接 {result.RelinkedCount} 项，仍缺失 {result.StillMissingCount} 项"; await RefreshAsync(); }

    private async Task AnalyzeSelectedAsync()
    {
        var asset = SelectedAssets.Count == 1 ? SelectedAssets[0] : null; if (asset is null) return;
        _analysisCancellation?.Cancel(); _analysisCancellation?.Dispose(); _analysisCancellation = new(); var cancellationToken = _analysisCancellation.Token;
        IsAnalyzing = true; Analysis = null;
        try
        {
            var request = await WpfVisualAnalysisDecoder.DecodeAsync(asset, PaletteSize, cancellationToken);
            await _analysisCoordinator.AnalyzeSelectionAsync(asset.AssetId, token => _visualAnalysis.AnalyzeAsync(request, token), result =>
            {
                if (!cancellationToken.IsCancellationRequested && SelectedAssets.Count == 1 && SelectedAssets[0].AssetId == result.AssetId) Analysis = result;
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException) { Status = ex.Message; }
        finally { if (SelectedAssets.Count == 1 && SelectedAssets[0].AssetId == asset.AssetId) IsAnalyzing = false; }
    }

    private async Task RefreshSelectionSummaryAsync() { SelectedTagSummary.Clear(); if (SelectedAssets.Count == 0) return; foreach (var item in await _repository.GetTagUsageSummaryAsync(SelectedAssets.Select(x => x.AssetId))) SelectedTagSummary.Add(item); }
    private async Task RefreshFilterListsAsync()
    {
        Folders.Clear(); foreach (var folder in await _repository.ListFoldersAsync()) Folders.Add(folder); RefreshClassifierFolders();
        FolderTree.Clear(); foreach (var node in await _repository.GetFolderTreeAsync()) FolderTree.Add(node);
        Tags.Clear(); foreach (var tag in await _repository.ListTagsAsync()) Tags.Add(tag); TagGroups.Clear(); foreach (var group in await _repository.ListTagGroupsAsync()) TagGroups.Add(group);
        SmartFolders.Clear(); foreach (var folder in await _repository.ListSmartFoldersAsync()) SmartFolders.Add(folder);
        FavoriteFolders.Clear(); foreach (var folder in Folders.Where(x => !string.IsNullOrWhiteSpace(x.Color)).Take(6)) FavoriteFolders.Add(folder);
    }
    private void RefreshClassifierFolders() { ClassifierFolders.Clear(); foreach (var folder in Folders.Where(x => string.IsNullOrWhiteSpace(FolderSearch) || x.Name.Contains(FolderSearch, StringComparison.OrdinalIgnoreCase))) ClassifierFolders.Add(folder); }
    private async Task SeedPreviewStructureAsync() { await _repository.BatchCreateFoldersAsync("人体/身体\n人体/宗教\n参考/白棚\n参考/黑色\n灯光/硬光\n灯光/柔光"); var groups = new[] { new TagGroup(Guid.NewGuid(), "人物"), new TagGroup(Guid.NewGuid(), "视觉"), new TagGroup(Guid.NewGuid(), "概念") }; foreach (var group in groups) await _repository.SaveTagGroupAsync(group); await _repository.BatchCreateTagsAsync("身体,宗教,凝视", groups[2].TagGroupId); await _repository.BatchCreateTagsAsync("红,蓝,绿色", groups[1].TagGroupId); await RefreshFilterListsAsync(); }

    public void ClearFilters() { _selectedFolder = null; _selectedTag = null; _selectedSmartFolder = null; SearchText = string.Empty; OnPropertyChanged(nameof(SelectedFolder)); OnPropertyChanged(nameof(SelectedTag)); OnPropertyChanged(nameof(SelectedSmartFolder)); _ = RefreshAsync(); }
    public void FocusFolderClassifier() => Status = "F：快速分类器已打开；↑↓选择、Space多选、Enter确认、Esc关闭";
    private AssetLibraryQuery BuildQuery(string? cursor = null) => new(SearchText, SelectedFolder?.FolderId, SelectedTag?.TagId, SmartFolderId: SelectedSmartFolder?.SmartFolderId, PageSize: 120, Cursor: cursor);
    private static string UniqueName(string seed, IEnumerable<string> names) { var set = names.ToHashSet(StringComparer.OrdinalIgnoreCase); if (!set.Contains(seed)) return seed; for (var i = 2; ; i++) if (!set.Contains($"{seed} {i}")) return $"{seed} {i}"; }
    private void RaiseActions() { AddFolderCommand.RaiseCanExecuteChanged(); ApplyTagsCommand.RaiseCanExecuteChanged(); NewSubfolderCommand.RaiseCanExecuteChanged(); UndoCommand.RaiseCanExecuteChanged(); }
    public ValueTask DisposeAsync() { _searchDebounce.Stop(); _analysisCancellation?.Cancel(); _analysisCancellation?.Dispose(); _analysisCoordinator.ClearSelection(); return _repository.DisposeAsync(); }
}
