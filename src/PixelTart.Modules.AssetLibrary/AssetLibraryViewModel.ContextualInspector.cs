using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace PixelTart.Modules.AssetLibrary;

public sealed partial class AssetLibraryViewModel
{
    private string _inspectorNote = "", _inspectorUrl = "", _inspectorColor = "", _collectionBytes = "正在统计…";
    private AssetFolder? _inspectorFolderTarget;
    private long _collectionSummaryGeneration;
    private long _annotationEditRevision;
    public string InspectorNote { get => _inspectorNote; set => SetProperty(ref _inspectorNote, value); }
    public string InspectorUrl { get => _inspectorUrl; set { if (SetProperty(ref _inspectorUrl, value)) _annotationEditRevision++; } }
    public string InspectorColor { get => _inspectorColor; set { if (SetProperty(ref _inspectorColor, value)) _annotationEditRevision++; } }
    public string CollectionTotalSize { get => _collectionBytes; private set => SetProperty(ref _collectionBytes, value); }
    public string InspectorSelectionSize => FormatInspectorBytes(SelectedAssets.Sum(asset => asset.FileSize));
    public IReadOnlyList<AssetVisualMatchView> InspectorSelectedCards => SelectedAssets.Take(18).Select(asset => new AssetVisualMatchView(asset) { Owner = this }).ToArray();
    public string InspectorFilterSummary => IsTemporaryVisualMode ? VisualModeLabel : P3QueryChips.Count > 0 ? string.Join(" · ", P3QueryChips.Select(chip => chip.Label)) : string.IsNullOrWhiteSpace(SearchText) ? "" : $"搜索：{SearchText}";
    public string InspectorHeading => SelectionCount switch { 0 => "集合概览", 1 => "素材详情", _ => "批量整理" };
    public IReadOnlyList<string> InspectorColors { get; } = ["", "红", "橙", "黄", "绿", "蓝", "紫"];
    public AssetFolder? InspectorFolderTarget { get => _inspectorFolderTarget; set { SetProperty(ref _inspectorFolderTarget, value); AddInspectorFolderCommand?.RaiseCanExecuteChanged(); } }
    public AsyncCommand SaveInspectorDetailsCommand { get; private set; } = null!;
    public AsyncCommand ApplyInspectorColorCommand { get; private set; } = null!;
    public AsyncCommand AddInspectorFolderCommand { get; private set; } = null!;
    public AsyncCommand AddInspectorTagsCommand { get; private set; } = null!;

    private void InitializeContextualInspector()
    {
        InitializeQuickTools();
        SaveInspectorDetailsCommand = new(() => InspectorMutationAsync(async () =>
        {
            if (SelectedAsset is not { } asset) return;
            var note = InspectorNote; var url = InspectorUrl;
            RememberBrowserMutationResult(await _repository.UpdateAssetMetadataAsync(asset.AssetId, comment: note, cancellationToken: _lifetimeCancellation.Token));
            await new AssetPresentationMetadataStore(_database).SaveAsync([asset.AssetId], url: url, token: _lifetimeCancellation.Token);
        }), () => IsReady && HasSingleSelection);
        ApplyInspectorColorCommand = new(() => InspectorMutationAsync(() => new AssetPresentationMetadataStore(_database).SaveAsync(SelectedAssetIds.ToArray(), color: InspectorColor, token: _lifetimeCancellation.Token)), () => IsReady && HasSelection);
        AddInspectorFolderCommand = new(() => InspectorMutationAsync(async () =>
        {
            if (InspectorFolderTarget is { } folder) RememberBrowserMutationResult(await _browserCommands.AddToFolderAsync(SelectedAssetIds.ToArray(), folder.FolderId, _lifetimeCancellation.Token));
        }), () => IsReady && HasSelection && InspectorFolderTarget is not null);
        AddInspectorTagsCommand = new(() => InspectorMutationAsync(async () =>
        {
            var ids = SelectedAssetIds.ToArray();
            var tags = await _repository.BatchCreateTagsAsync(TagInput, cancellationToken: _lifetimeCancellation.Token);
            RememberBrowserMutationResult(await _repository.AddTagsAsync(ids, tags.Select(tag => tag.TagId), _lifetimeCancellation.Token));
            TagInput = "";
        }), () => IsReady && HasSelection);
    }

    private async Task InspectorMutationAsync(Func<Task> action)
    {
        try
        {
            await action();
            await RefreshAsync();
            await _p2InspectorTask;
            Status = "已保存素材库信息，源文件未更改。";
        }
        catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or ArgumentException)
        { Status = $"未能保存：{exception.Message}"; }
    }

    private void NotifyContextualInspector()
    {
        OpenProjectPickerCommand.RaiseCanExecuteChanged(); OpenBookingPickerCommand.RaiseCanExecuteChanged();
        SelectionToolCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(QuickCompressLabel)); OnPropertyChanged(nameof(QuickExportLabel));
        InspectorNote = SelectedAsset?.Comment ?? ""; InspectorUrl = ""; InspectorColor = "";
        foreach (var property in new[] { nameof(InspectorHeading), nameof(InspectorSelectedCards), nameof(InspectorSelectionSize), nameof(InspectorFilterSummary) }) OnPropertyChanged(property);
        SaveInspectorDetailsCommand.RaiseCanExecuteChanged(); ApplyInspectorColorCommand.RaiseCanExecuteChanged(); AddInspectorFolderCommand.RaiseCanExecuteChanged(); AddInspectorTagsCommand.RaiseCanExecuteChanged();
        if (HasMultipleSelection && IsInspectorPaneCollapsed) IsInspectorPaneCollapsed = false;
    }

    private async Task RefreshPresentationMetadataAsync(IReadOnlyList<AssetItem> selected, long generation)
    {
        var editRevision = _annotationEditRevision;
        var store = new AssetPresentationMetadataStore(_database);
        var metadata = new List<AssetPresentationMetadata>();
        foreach (var asset in selected) metadata.Add(await store.GetAsync(asset.AssetId, _lifetimeCancellation.Token));
        if (generation != Volatile.Read(ref _inspectorGeneration) || editRevision != _annotationEditRevision) return;
        InspectorUrl = metadata.Count == 1 ? metadata[0].Url : "";
        InspectorColor = metadata.Select(item => item.Color).Distinct().Count() == 1 ? metadata[0].Color : "";
    }

    private async Task RefreshCollectionSizeAsync()
    {
        var generation = Interlocked.Increment(ref _collectionSummaryGeneration);
        var query = BuildQuery() with { PageSize = 500, Cursor = null };
        try
        {
            long bytes = 0;
            if (IsTemporaryVisualMode) bytes = AssetCards.Sum(card => card.Asset.FileSize);
            else
            {
                do
                {
                    var page = await _repository.QueryAsync(query, _lifetimeCancellation.Token);
                    if (generation != Volatile.Read(ref _collectionSummaryGeneration)) return;
                    bytes += page.Items.Sum(asset => asset.FileSize);
                    query = query with { Cursor = page.NextCursor };
                } while (query.Cursor is not null);
            }
            if (generation == Volatile.Read(ref _collectionSummaryGeneration)) CollectionTotalSize = FormatInspectorBytes(bytes);
            OnPropertyChanged(nameof(InspectorFilterSummary));
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested) { }
    }

    private static string FormatInspectorBytes(long bytes) => bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.##} GB" : bytes >= 1L << 20 ? $"{bytes / (double)(1L << 20):0.##} MB" : $"{bytes / 1024d:0.##} KB";
}
