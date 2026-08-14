using System.Collections.ObjectModel;
using Microsoft.Win32;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Utilities;

namespace PixelTart.AssetLibrary.Preview;

public sealed class AssetLibraryPreviewViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IAssetLibraryRepository _repository;
    private string _searchText = string.Empty;
    private string _status = "正在准备素材库";
    private string? _nextCursor;
    private AssetItem? _selectedAsset;
    private AssetFolder? _selectedFolder;
    private AssetTag? _selectedTag;
    private SmartFolder? _selectedSmartFolder;

    public AssetLibraryPreviewViewModel(string databasePath)
    {
        _repository = new SqliteAssetLibraryRepository(databasePath);
        RefreshCommand = new AsyncCommand(() => RefreshAsync());
        ImportCommand = new AsyncCommand(ImportAsync);
        LoadMoreCommand = new AsyncCommand(LoadMoreAsync, () => _nextCursor is not null);
        NewFolderCommand = new AsyncCommand(NewFolderAsync);
        NewTagCommand = new AsyncCommand(NewTagAsync);
        AddFolderCommand = new AsyncCommand(AddFolderAsync, () => SelectedAsset is not null && SelectedFolder is not null);
        AddTagCommand = new AsyncCommand(AddTagAsync, () => SelectedAsset is not null && SelectedTag is not null);
        UndoCommand = new AsyncCommand(UndoAsync, () => LastUndoToken is not null);
    }

    public ObservableCollection<AssetItem> Assets { get; } = [];
    public ObservableCollection<AssetFolder> Folders { get; } = [];
    public ObservableCollection<AssetTag> Tags { get; } = [];
    public ObservableCollection<SmartFolder> SmartFolders { get; } = [];
    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public int VisibleCount => Assets.Count;
    public AssetLibraryUndoToken? LastUndoToken { get; private set; }
    public AssetItem? SelectedAsset { get => _selectedAsset; set { if (SetProperty(ref _selectedAsset, value)) RaiseActions(); } }
    public AssetFolder? SelectedFolder { get => _selectedFolder; set { if (SetProperty(ref _selectedFolder, value)) { RaiseActions(); _ = RefreshAsync(); } } }
    public AssetTag? SelectedTag { get => _selectedTag; set { if (SetProperty(ref _selectedTag, value)) { RaiseActions(); _ = RefreshAsync(); } } }
    public SmartFolder? SelectedSmartFolder { get => _selectedSmartFolder; set { if (SetProperty(ref _selectedSmartFolder, value)) _ = RefreshAsync(); } }
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand ImportCommand { get; }
    public AsyncCommand LoadMoreCommand { get; }
    public AsyncCommand NewFolderCommand { get; }
    public AsyncCommand NewTagCommand { get; }
    public AsyncCommand AddFolderCommand { get; }
    public AsyncCommand AddTagCommand { get; }
    public AsyncCommand UndoCommand { get; }

    public async Task InitializeAsync()
    {
        await _repository.InitializeAsync();
        await RefreshFilterListsAsync();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var page = await _repository.QueryAsync(BuildQuery());
        Assets.Clear(); foreach (var asset in page.Items) Assets.Add(asset);
        _nextCursor = page.NextCursor;
        Status = page.RegexError is null ? $"共 {page.TotalCount:N0} 个素材，当前显示 {Assets.Count:N0} 个" : $"筛选错误：{page.RegexError}";
        OnPropertyChanged(nameof(VisibleCount)); LoadMoreCommand.RaiseCanExecuteChanged();
    }

    private async Task LoadMoreAsync()
    {
        if (_nextCursor is null) return;
        var page = await _repository.QueryAsync(BuildQuery(_nextCursor));
        foreach (var asset in page.Items) Assets.Add(asset);
        _nextCursor = page.NextCursor; Status = $"已加载 {Assets.Count:N0} 个素材"; OnPropertyChanged(nameof(VisibleCount)); LoadMoreCommand.RaiseCanExecuteChanged();
    }

    private async Task ImportAsync()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Filter = "图片与视频|*.jpg;*.jpeg;*.png;*.webp;*.tif;*.tiff;*.arw;*.cr2;*.cr3;*.nef;*.raf;*.dng;*.mp4;*.mov|所有文件|*.*" };
        if (dialog.ShowDialog() != true) return;
        var result = await _repository.ImportAsync(dialog.FileNames.Select(path => new AssetImportRequest(path)));
        Status = result.Cancelled ? "导入已取消" : $"已索引 {result.ImportedCount:N0} 个素材（未修改源文件）";
        await RefreshAsync();
    }

    private async Task NewFolderAsync()
    {
        var name = UniqueName("新建文件夹", Folders.Select(x => x.Name));
        var folder = await _repository.SaveFolderAsync(new(Guid.NewGuid(), null, name)); Folders.Add(folder); Status = $"已创建虚拟文件夹：{name}";
    }

    private async Task NewTagAsync()
    {
        var name = UniqueName("新标签", Tags.Select(x => x.Name));
        var tag = await _repository.SaveTagAsync(new(Guid.NewGuid(), name)); Tags.Add(tag); Status = $"已创建标签：{name}";
    }

    private async Task AddFolderAsync()
    {
        var result = await _repository.AddToFolderAsync([SelectedAsset!.AssetId], SelectedFolder!.FolderId); LastUndoToken = result.UndoToken; Status = $"已加入虚拟文件夹：{SelectedFolder.Name}"; RaiseActions();
    }

    private async Task AddTagAsync()
    {
        var result = await _repository.AddTagsAsync([SelectedAsset!.AssetId], [SelectedTag!.TagId]); LastUndoToken = result.UndoToken; Status = $"已添加标签：{SelectedTag.Name}"; RaiseActions();
    }

    private async Task UndoAsync()
    {
        if (LastUndoToken is null) return;
        Status = await _repository.UndoAsync(LastUndoToken) ? "已撤销上一项素材库操作" : "撤销记录已失效"; LastUndoToken = null; RaiseActions();
    }

    public void ClearFilters()
    {
        _selectedFolder = null; _selectedTag = null; _selectedSmartFolder = null; SearchText = string.Empty;
        OnPropertyChanged(nameof(SelectedFolder)); OnPropertyChanged(nameof(SelectedTag)); OnPropertyChanged(nameof(SelectedSmartFolder)); _ = RefreshAsync();
    }

    private async Task RefreshFilterListsAsync()
    {
        Folders.Clear(); foreach (var folder in await _repository.ListFoldersAsync()) Folders.Add(folder);
        Tags.Clear(); foreach (var tag in await _repository.ListTagsAsync()) Tags.Add(tag);
        SmartFolders.Clear(); foreach (var folder in await _repository.ListSmartFoldersAsync()) SmartFolders.Add(folder);
    }

    private AssetLibraryQuery BuildQuery(string? cursor = null) => new(SearchText, SelectedFolder?.FolderId, SelectedTag?.TagId, SmartFolderId: SelectedSmartFolder?.SmartFolderId, PageSize: 80, Cursor: cursor);
    private static string UniqueName(string seed, IEnumerable<string> names) { var set = names.ToHashSet(StringComparer.OrdinalIgnoreCase); if (!set.Contains(seed)) return seed; for (var i = 2; ; i++) if (!set.Contains($"{seed} {i}")) return $"{seed} {i}"; }
    private void RaiseActions() { AddFolderCommand.RaiseCanExecuteChanged(); AddTagCommand.RaiseCanExecuteChanged(); UndoCommand.RaiseCanExecuteChanged(); }
    public ValueTask DisposeAsync() => _repository.DisposeAsync();
}
