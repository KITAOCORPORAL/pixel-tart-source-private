using System.Collections.ObjectModel;
using System.Diagnostics;
using RAWSelectionAssistant.Core.Models;

namespace PixelTart.Modules.AssetLibrary;

public sealed partial class AssetLibraryViewModel
{
    private CancellationTokenSource? _p3SmartFolderLoadCancellation;
    private CancellationTokenSource? _p3SmartFolderPreviewCancellation;
    private long _p3SmartFolderLoadGeneration;
    private long _p3SmartFolderPreviewGeneration;
    private Guid? _p3SmartFolderId;
    private SmartFolder? _p3SmartFolderSnapshot;
    private AssetQueryDocument _p3SmartFolderDocument = new() { Scope = AssetQueryScope.AllAssets };
    private P3QueryNodeView _p3SmartFolderRoot = null!;
    private bool _p3SmartFolderOpen;
    private bool _p3SmartFolderLoading;
    private bool _p3SmartFolderPreviewLoading;
    private bool _p3SmartFolderSuppressChanges;
    private bool _p3SmartFolderIsValid = true;
    private string _p3SmartFolderName = "新智能文件夹";
    private string _p3SmartFolderDescription = string.Empty;
    private string _p3SmartFolderValidationMessage = string.Empty;
    private string _p3SmartFolderPreviewStatus = "修改规则后会自动预览。";
    private int _p3SmartFolderPreviewCount;
    private long _p3SmartFolderPreviewMilliseconds;
    private AssetLibrarySortField _p3SmartFolderSortField = AssetLibrarySortField.AddedAt;
    private AssetLibrarySortDirection _p3SmartFolderSortDirection = AssetLibrarySortDirection.Descending;
    private bool _p3SmartFolderIncludeArchived;

    public ObservableCollection<AssetItem> P3SmartFolderPreviewItems { get; } = [];

    public P3QueryNodeView P3SmartFolderRoot
    {
        get => _p3SmartFolderRoot;
        private set
        {
            if (!SetProperty(ref _p3SmartFolderRoot, value)) return;
            OnPropertyChanged(nameof(P3SmartFolderRoots));
        }
    }

    public IReadOnlyList<P3QueryNodeView> P3SmartFolderRoots => P3SmartFolderRoot is null ? [] : [P3SmartFolderRoot];
    public bool P3SmartFolderOpen
    {
        get => _p3SmartFolderOpen;
        private set
        {
            if (!SetProperty(ref _p3SmartFolderOpen, value)) return;
            OnPropertyChanged(nameof(P3SmartFolderClosed));
        }
    }
    public bool P3SmartFolderClosed => !P3SmartFolderOpen;
    public bool P3SmartFolderLoading { get => _p3SmartFolderLoading; private set { if (SetProperty(ref _p3SmartFolderLoading, value)) RaiseP3SmartFolderCommands(); } }
    public bool P3SmartFolderPreviewLoading { get => _p3SmartFolderPreviewLoading; private set => SetProperty(ref _p3SmartFolderPreviewLoading, value); }
    public bool P3SmartFolderIsEditing => _p3SmartFolderId is not null;
    public bool P3SmartFolderIsArchived => _p3SmartFolderSnapshot?.IsArchived == true;
    public string P3SmartFolderArchiveLabel => P3SmartFolderIsArchived ? "恢复" : "归档";
    public string P3SmartFolderTitle => P3SmartFolderIsEditing ? "编辑智能文件夹" : "新建智能文件夹";

    public string P3SmartFolderName
    {
        get => _p3SmartFolderName;
        set { if (SetProperty(ref _p3SmartFolderName, value ?? string.Empty)) RaiseP3SmartFolderCommands(); }
    }

    public string P3SmartFolderDescription
    {
        get => _p3SmartFolderDescription;
        set => SetProperty(ref _p3SmartFolderDescription, value ?? string.Empty);
    }

    public bool P3SmartFolderIsValid
    {
        get => _p3SmartFolderIsValid;
        private set
        {
            if (!SetProperty(ref _p3SmartFolderIsValid, value)) return;
            OnPropertyChanged(nameof(P3SmartFolderHasError));
            RaiseP3SmartFolderCommands();
        }
    }

    public bool P3SmartFolderHasError => !P3SmartFolderIsValid || !string.IsNullOrWhiteSpace(P3SmartFolderValidationMessage);
    public string P3SmartFolderValidationMessage
    {
        get => _p3SmartFolderValidationMessage;
        private set
        {
            if (SetProperty(ref _p3SmartFolderValidationMessage, value ?? string.Empty))
                OnPropertyChanged(nameof(P3SmartFolderHasError));
        }
    }
    public string P3SmartFolderPreviewStatus { get => _p3SmartFolderPreviewStatus; private set => SetProperty(ref _p3SmartFolderPreviewStatus, value); }
    public int P3SmartFolderPreviewCount { get => _p3SmartFolderPreviewCount; private set => SetProperty(ref _p3SmartFolderPreviewCount, value); }
    public long P3SmartFolderPreviewMilliseconds { get => _p3SmartFolderPreviewMilliseconds; private set => SetProperty(ref _p3SmartFolderPreviewMilliseconds, value); }
    public AssetLibrarySortField P3SmartFolderSortField
    {
        get => _p3SmartFolderSortField;
        set
        {
            if (!Enum.IsDefined(value) || !SetProperty(ref _p3SmartFolderSortField, value)) return;
            if (!_p3SmartFolderSuppressChanges) ValidateAndScheduleP3SmartFolderPreview();
        }
    }
    public AssetLibrarySortDirection P3SmartFolderSortDirection
    {
        get => _p3SmartFolderSortDirection;
        set
        {
            if (!Enum.IsDefined(value) || !SetProperty(ref _p3SmartFolderSortDirection, value)) return;
            if (!_p3SmartFolderSuppressChanges) ValidateAndScheduleP3SmartFolderPreview();
        }
    }
    public bool P3SmartFolderIncludeArchived
    {
        get => _p3SmartFolderIncludeArchived;
        set
        {
            if (!SetProperty(ref _p3SmartFolderIncludeArchived, value)) return;
            if (!_p3SmartFolderSuppressChanges) ValidateAndScheduleP3SmartFolderPreview();
        }
    }
    public string P3SmartFolderPreservedScopeAndSearch =>
        $"范围：{(_p3SmartFolderDocument.Scope == AssetQueryScope.AllAssets ? "全部素材" : "当前范围")}；" +
        $"搜索条件：{EffectiveP3SearchClauses(_p3SmartFolderDocument).Count()} 段";
    public IReadOnlyList<P3QueryOption<AssetLibrarySortField>> P3SmartFolderSortFieldOptions { get; } =
    [
        new(AssetLibrarySortField.AddedAt, "导入时间"), new(AssetLibrarySortField.CaptureTime, "拍摄时间"),
        new(AssetLibrarySortField.FileName, "文件名"), new(AssetLibrarySortField.FileSize, "文件大小"),
        new(AssetLibrarySortField.Rating, "评分"), new(AssetLibrarySortField.Color, "颜色"),
        new(AssetLibrarySortField.VisualAnalysis, "视觉分析")
    ];
    public IReadOnlyList<P3QueryOption<AssetLibrarySortDirection>> P3SmartFolderSortDirectionOptions { get; } =
    [
        new(AssetLibrarySortDirection.Ascending, "升序"),
        new(AssetLibrarySortDirection.Descending, "降序")
    ];

    public AssetCommand NewP3SmartFolderCommand { get; private set; } = null!;
    public AsyncCommand SaveP3SmartFolderCommand { get; private set; } = null!;
    public AssetCommand CancelP3SmartFolderCommand { get; private set; } = null!;
    public AsyncCommand CopyP3SmartFolderCommand { get; private set; } = null!;
    public AsyncCommand ToggleArchiveP3SmartFolderCommand { get; private set; } = null!;
    public AsyncCommand RetryP3SmartFolderPreviewCommand { get; private set; } = null!;

    private void InitializeP3SmartFolderEditor()
    {
        P3SmartFolderRoot = P3QueryNodeView.CreateRoot(OnP3SmartFolderTreeChanged, "SmartFolder");
        NewP3SmartFolderCommand = new(() => OpenP3SmartFolderEditor(null));
        SaveP3SmartFolderCommand = new(SaveP3SmartFolderAsync,
            () => IsReady && !P3SmartFolderLoading && P3SmartFolderIsValid && !string.IsNullOrWhiteSpace(P3SmartFolderName));
        CancelP3SmartFolderCommand = new(CloseP3SmartFolderEditor);
        CopyP3SmartFolderCommand = new(CopyP3SmartFolderAsync,
            () => IsReady && P3SmartFolderIsEditing && !P3SmartFolderLoading);
        ToggleArchiveP3SmartFolderCommand = new(ToggleArchiveP3SmartFolderAsync,
            () => IsReady && P3SmartFolderIsEditing && !P3SmartFolderLoading);
        RetryP3SmartFolderPreviewCommand = new(PreviewP3SmartFolderNowAsync,
            () => IsReady && P3SmartFolderOpen && P3SmartFolderIsValid);
    }

    internal void OpenP3SmartFolderEditor(SmartFolder? folder)
    {
        CancelP3SmartFolderWork();
        P3SmartFolderOpen = true;
        _p3SmartFolderId = folder?.SmartFolderId;
        _p3SmartFolderSnapshot = folder;
        SetP3SmartFolderDocument(new AssetQueryDocument
        {
            Scope = AssetQueryScope.AllAssets,
            SortField = SortField,
            SortDirection = SortDirection
        });
        P3SmartFolderName = folder?.Name ?? UniqueName("新智能文件夹", SmartFolders.Select(item => item.Name));
        P3SmartFolderDescription = folder?.Description ?? string.Empty;
        P3SmartFolderPreviewItems.Clear();
        P3SmartFolderPreviewCount = 0;
        P3SmartFolderPreviewMilliseconds = 0;
        OnPropertyChanged(nameof(P3SmartFolderIsEditing));
        OnPropertyChanged(nameof(P3SmartFolderIsArchived));
        OnPropertyChanged(nameof(P3SmartFolderArchiveLabel));
        OnPropertyChanged(nameof(P3SmartFolderTitle));
        RaiseP3SmartFolderCommands();

        if (folder is null)
        {
            ReplaceP3SmartFolderRoot(AssetQueryNode.Group(AssetQueryLogic.All));
            ValidateAndScheduleP3SmartFolderPreview();
            return;
        }

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        _p3SmartFolderLoadCancellation = cancellation;
        var generation = Interlocked.Increment(ref _p3SmartFolderLoadGeneration);
        P3SmartFolderLoading = true;
        P3SmartFolderPreviewStatus = "正在载入已保存规则…";
        _ = LoadP3SmartFolderAsync(folder, generation, cancellation);
    }

    private async Task LoadP3SmartFolderAsync(SmartFolder folder, long generation, CancellationTokenSource cancellation)
    {
        try
        {
            var saved = await _repository.GetSmartFolderQueryDocumentAsync(folder.SmartFolderId, cancellation.Token);
            if (!IsCurrentP3SmartFolderLoad(folder.SmartFolderId, generation, cancellation)) return;
            if (saved is null)
            {
                P3SmartFolderValidationMessage = "此旧智能文件夹没有通用规则文档；请在旧编辑器确认条件后另存。";
                P3SmartFolderIsValid = false;
                ReplaceP3SmartFolderRoot(AssetQueryNode.Group(AssetQueryLogic.All));
                return;
            }
            SetP3SmartFolderDocument(saved.Document);
            ReplaceP3SmartFolderRoot(saved.Document.RootGroup);
            P3SmartFolderValidationMessage = string.Empty;
            P3SmartFolderIsValid = true;
            ValidateAndScheduleP3SmartFolderPreview();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception) when (IsCurrentP3SmartFolderLoad(folder.SmartFolderId, generation, cancellation))
        {
            P3SmartFolderValidationMessage = $"规则载入失败：{exception.Message}";
            P3SmartFolderIsValid = false;
            P3SmartFolderPreviewStatus = "规则未载入，未扩大为全部素材。";
        }
        finally
        {
            if (ReferenceEquals(_p3SmartFolderLoadCancellation, cancellation))
            {
                _p3SmartFolderLoadCancellation = null;
                P3SmartFolderLoading = false;
            }
            cancellation.Dispose();
        }
    }

    private bool IsCurrentP3SmartFolderLoad(Guid id, long generation, CancellationTokenSource cancellation) =>
        Volatile.Read(ref _disposeStarted) == 0 && P3SmartFolderOpen && _p3SmartFolderId == id &&
        generation == Volatile.Read(ref _p3SmartFolderLoadGeneration) &&
        ReferenceEquals(_p3SmartFolderLoadCancellation, cancellation) && !cancellation.IsCancellationRequested;

    private void ReplaceP3SmartFolderRoot(AssetQueryNode root)
    {
        _p3SmartFolderSuppressChanges = true;
        try { P3SmartFolderRoot = P3QueryNodeView.FromModel(root, OnP3SmartFolderTreeChanged, "SmartFolder"); }
        finally { _p3SmartFolderSuppressChanges = false; }
    }

    private void SetP3SmartFolderDocument(AssetQueryDocument document)
    {
        _p3SmartFolderSuppressChanges = true;
        try
        {
            _p3SmartFolderDocument = document;
            P3SmartFolderSortField = document.SortField;
            P3SmartFolderSortDirection = document.SortDirection;
            P3SmartFolderIncludeArchived = document.IncludeArchived;
            OnPropertyChanged(nameof(P3SmartFolderPreservedScopeAndSearch));
        }
        finally { _p3SmartFolderSuppressChanges = false; }
    }

    private void OnP3SmartFolderTreeChanged()
    {
        if (_p3SmartFolderSuppressChanges) return;
        ValidateAndScheduleP3SmartFolderPreview();
    }

    private AssetQueryValidationResult NormalizeP3SmartFolderDocument(out AssetQueryDocument document)
    {
        document = _p3SmartFolderDocument with
        {
            RootGroup = P3SmartFolderRoot.ToModel(),
            SortField = P3SmartFolderSortField,
            SortDirection = P3SmartFolderSortDirection,
            IncludeArchived = P3SmartFolderIncludeArchived
        };
        var result = AssetQueryDocumentCodec.Normalize(document);
        if (result.Document is not null) document = result.Document;
        P3SmartFolderIsValid = result.IsValid;
        P3SmartFolderValidationMessage = result.IsValid ? string.Empty : result.ErrorMessage;
        return result;
    }

    private void ValidateAndScheduleP3SmartFolderPreview()
    {
        var validation = NormalizeP3SmartFolderDocument(out _);
        if (!validation.IsValid)
        {
            CancelP3SmartFolderPreview();
            P3SmartFolderPreviewStatus = "请先修正规则，再查看预览。";
            return;
        }
        ScheduleP3SmartFolderPreview();
    }

    private void ScheduleP3SmartFolderPreview()
    {
        CancelP3SmartFolderPreview();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        _p3SmartFolderPreviewCancellation = cancellation;
        var generation = Interlocked.Increment(ref _p3SmartFolderPreviewGeneration);
        _ = LoadP3SmartFolderPreviewAsync(generation, cancellation, delay: true);
    }

    private Task PreviewP3SmartFolderNowAsync()
    {
        CancelP3SmartFolderPreview();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        _p3SmartFolderPreviewCancellation = cancellation;
        var generation = Interlocked.Increment(ref _p3SmartFolderPreviewGeneration);
        return LoadP3SmartFolderPreviewAsync(generation, cancellation, delay: false);
    }

    private async Task LoadP3SmartFolderPreviewAsync(long generation, CancellationTokenSource cancellation, bool delay)
    {
        try
        {
            if (delay) await Task.Delay(TimeSpan.FromMilliseconds(280), cancellation.Token);
            if (!ValidateP3SmartFolderDocument(out var document)) return;
            P3SmartFolderPreviewLoading = true;
            P3SmartFolderPreviewStatus = "正在计算预览…";
            var clock = Stopwatch.StartNew();
            var query = BuildQuery() with { Cursor = null, PageSize = 6, SmartFolderId = null, Document = document };
            var page = await _repository.QueryAsync(query, cancellation.Token);
            clock.Stop();
            if (!IsCurrentP3SmartFolderPreview(generation, cancellation)) return;
            P3SmartFolderPreviewItems.Clear();
            foreach (var item in page.Items) P3SmartFolderPreviewItems.Add(item);
            P3SmartFolderPreviewCount = page.TotalCount;
            P3SmartFolderPreviewMilliseconds = clock.ElapsedMilliseconds;
            if (!string.IsNullOrWhiteSpace(page.RegexError))
            {
                P3SmartFolderIsValid = false;
                P3SmartFolderValidationMessage = page.RegexError;
                P3SmartFolderPreviewStatus = "预览失败，未修改正式智能文件夹。";
            }
            else P3SmartFolderPreviewStatus = $"预览 {page.TotalCount:N0} 项 · {clock.ElapsedMilliseconds:N0} 毫秒";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception) when (IsCurrentP3SmartFolderPreview(generation, cancellation))
        {
            P3SmartFolderPreviewStatus = $"预览失败：{exception.Message}";
        }
        finally
        {
            if (ReferenceEquals(_p3SmartFolderPreviewCancellation, cancellation))
            {
                _p3SmartFolderPreviewCancellation = null;
                P3SmartFolderPreviewLoading = false;
            }
            cancellation.Dispose();
        }
    }

    private bool ValidateP3SmartFolderDocument(out AssetQueryDocument document)
    {
        var validation = NormalizeP3SmartFolderDocument(out document);
        return validation.IsValid && validation.Document is not null;
    }

    private bool IsCurrentP3SmartFolderPreview(long generation, CancellationTokenSource cancellation) =>
        Volatile.Read(ref _disposeStarted) == 0 && P3SmartFolderOpen &&
        generation == Volatile.Read(ref _p3SmartFolderPreviewGeneration) &&
        ReferenceEquals(_p3SmartFolderPreviewCancellation, cancellation) && !cancellation.IsCancellationRequested;

    private async Task SaveP3SmartFolderAsync()
    {
        if (!ValidateP3SmartFolderDocument(out var document)) return;
        var repositorySaveCompleted = false;
        try
        {
            var referenceErrors = await _repository.ValidateQueryReferencesAsync(document, _lifetimeCancellation.Token);
            if (referenceErrors.Count != 0)
            {
                P3SmartFolderIsValid = false;
                P3SmartFolderValidationMessage = string.Join("；", referenceErrors.Select(error => error.Message));
                return;
            }
            var folder = _p3SmartFolderSnapshot is null
                ? new SmartFolder(_p3SmartFolderId ?? Guid.NewGuid(), P3SmartFolderName.Trim(), Description: P3SmartFolderDescription.Trim())
                : _p3SmartFolderSnapshot with { Name = P3SmartFolderName.Trim(), Description = P3SmartFolderDescription.Trim() };
            var saved = await _repository.SaveSmartFolderQueryDocumentAsync(folder, document, _lifetimeCancellation.Token);
            repositorySaveCompleted = true;
            _p3SmartFolderId = saved.SmartFolderId;
            _p3SmartFolderSnapshot = saved;
            _p3SmartFolderDocument = document;
            await RefreshFilterListsAsync(_lifetimeCancellation.Token);
            Status = $"已保存智能文件夹：{saved.Name}";
            P3SmartFolderValidationMessage = string.Empty;
            OnPropertyChanged(nameof(P3SmartFolderIsEditing));
            OnPropertyChanged(nameof(P3SmartFolderTitle));
            RaiseP3SmartFolderCommands();
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            // Keep the complete editor document and user-entered name/description
            // intact so the exact same operation can be retried.
            P3SmartFolderIsValid = true;
            P3SmartFolderValidationMessage = repositorySaveCompleted
                ? $"智能文件夹已保存，但列表刷新失败，可重试刷新：{exception.Message}"
                : $"保存失败，编辑内容已保留，可重试：{exception.Message}";
            P3SmartFolderPreviewStatus = repositorySaveCompleted
                ? "正式智能文件夹已保存；当前列表可能尚未刷新。"
                : "保存未完成，正式智能文件夹未改变。";
        }
    }

    private async Task CopyP3SmartFolderAsync()
    {
        if (_p3SmartFolderId is not Guid id) return;
        SmartFolder? copy = null;
        try
        {
            copy = await _repository.CopySmartFolderAsync(id, cancellationToken: _lifetimeCancellation.Token);
            await RefreshFilterListsAsync(_lifetimeCancellation.Token);
            OpenP3SmartFolderEditor(copy);
            Status = $"已复制智能文件夹：{copy.Name}";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            P3SmartFolderIsValid = true;
            P3SmartFolderValidationMessage = copy is null
                ? $"复制失败，当前编辑内容已保留，可重试：{exception.Message}"
                : $"副本已创建，但列表刷新失败，可重试刷新：{exception.Message}";
        }
    }

    private async Task ToggleArchiveP3SmartFolderAsync()
    {
        if (_p3SmartFolderId is not Guid id || _p3SmartFolderSnapshot is null) return;
        var target = !_p3SmartFolderSnapshot.IsArchived;
        var mutationCompleted = false;
        try
        {
            var result = await _repository.SetSmartFolderArchivedAsync(id, target, _lifetimeCancellation.Token);
            mutationCompleted = true;
            RememberP3MetadataResult(result);
            _p3SmartFolderSnapshot = _p3SmartFolderSnapshot with { IsArchived = target };
            OnPropertyChanged(nameof(P3SmartFolderIsArchived));
            OnPropertyChanged(nameof(P3SmartFolderArchiveLabel));
            await RefreshFilterListsAsync(_lifetimeCancellation.Token);
            Status = target ? "智能文件夹已归档，可随时恢复。" : "智能文件夹已恢复。";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            P3SmartFolderIsValid = true;
            P3SmartFolderValidationMessage = mutationCompleted
                ? $"归档状态已更新，但列表刷新失败，可重试刷新：{exception.Message}"
                : $"归档操作失败，当前编辑内容已保留，可重试：{exception.Message}";
        }
    }

    private void CloseP3SmartFolderEditor()
    {
        P3SmartFolderOpen = false;
        CancelP3SmartFolderWork();
        P3SmartFolderPreviewItems.Clear();
        P3SmartFolderPreviewStatus = "已取消编辑，正式智能文件夹未改变。";
    }

    private void CancelP3SmartFolderPreview()
    {
        var cancellation = Interlocked.Exchange(ref _p3SmartFolderPreviewCancellation, null);
        cancellation?.Cancel();
        Interlocked.Increment(ref _p3SmartFolderPreviewGeneration);
        P3SmartFolderPreviewLoading = false;
    }

    private void CancelP3SmartFolderWork()
    {
        var load = Interlocked.Exchange(ref _p3SmartFolderLoadCancellation, null);
        load?.Cancel();
        Interlocked.Increment(ref _p3SmartFolderLoadGeneration);
        CancelP3SmartFolderPreview();
        P3SmartFolderLoading = false;
    }

    private void DisposeP3SmartFolderEditor() => CancelP3SmartFolderWork();

    private void RaiseP3SmartFolderCommands()
    {
        SaveP3SmartFolderCommand?.RaiseCanExecuteChanged();
        CopyP3SmartFolderCommand?.RaiseCanExecuteChanged();
        ToggleArchiveP3SmartFolderCommand?.RaiseCanExecuteChanged();
        RetryP3SmartFolderPreviewCommand?.RaiseCanExecuteChanged();
    }
}
