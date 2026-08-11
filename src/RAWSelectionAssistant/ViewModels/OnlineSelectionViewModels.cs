using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.OnlineSelection;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public enum OnlineSelectionProjectTab
{
    Photos,
    ClientSelection,
    Settings,
    DeliveryResults
}

public sealed record OnlineSelectionTabItem(OnlineSelectionProjectTab Value, string Label);

public sealed class OnlineSelectionAssetViewModel : ObservableObject
{
    private SelectionAsset _asset;
    private bool _isSelected;
    private bool _isFavorite;
    private string _customerNote = string.Empty;
    private ImageSource? _thumbnail;

    public OnlineSelectionAssetViewModel(SelectionAsset asset)
    {
        _asset = asset;
        _thumbnail = LoadThumbnail(asset);
    }

    public Guid Id => _asset.Id;
    public Guid ProjectId => _asset.ProjectId;
    public string OriginalFileName => _asset.OriginalFileName;
    public string LocalSourcePath => _asset.LocalSourcePath;
    public string? ProxyJpegPath => _asset.ProxyJpegPath;
    public SelectionAssetStatus Status => _asset.Status;
    public string StatusText => SelectionDisplayText.AssetStatus(Status);
    public string? CloudAssetId => _asset.CloudAssetId;
    public bool IsCover => _asset.IsCover;
    public ImageSource? Thumbnail { get => _thumbnail; private set => SetProperty(ref _thumbnail, value); }
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public bool IsFavorite { get => _isFavorite; set => SetProperty(ref _isFavorite, value); }
    public string CustomerNote { get => _customerNote; set => SetProperty(ref _customerNote, value ?? string.Empty); }

    public SelectionAsset ToModel() => _asset;
    public void Apply(SelectionAsset asset)
    {
        _asset = asset;
        Thumbnail = LoadThumbnail(asset);
        OnPropertyChanged(string.Empty);
    }

    private static ImageSource? LoadThumbnail(SelectionAsset asset)
    {
        var path = File.Exists(asset.ProxyJpegPath) ? asset.ProxyJpegPath : asset.LocalSourcePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        var extension = Path.GetExtension(path);
        if (!new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff" }.Contains(extension, StringComparer.OrdinalIgnoreCase)) return null;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 360;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}

public sealed class OnlineSelectionProjectListItemViewModel
{
    public OnlineSelectionProjectListItemViewModel(SelectionProject project) => Project = project;
    public SelectionProject Project { get; }
    public string Name => Project.Name;
    public string ClientDisplayName => Project.ClientDisplayName;
    public string StatusText => SelectionDisplayText.ProjectStatus(Project.Status);
    public string SelectionTargetText => $"目标 {Project.TargetCount} 张";
    public string DeadlineText => Project.DeadlineUtc is null ? "未设置截止时间" : $"截止 {Project.DeadlineUtc.Value.ToLocalTime():yyyy-MM-dd}";
}

public sealed class OnlineSelectionProjectViewModel : ObservableObject
{
    private static readonly ConditionalWeakTable<ISelectionWorkspaceStore, SemaphoreSlim> StoreWriteGates = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileStoreWriteGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly IOnlineSelectionProvider _provider;
    private readonly ISelectionWorkspaceStore _store;
    private readonly SelectionResultSyncService _syncService;
    private readonly SelectionProxyJpegService? _proxyService;
    private readonly string? _proxyRootDirectory;
    private readonly IDialogService? _dialogs;
    private SelectionProject? _project;
    private SelectionRule? _rule;
    private OnlineSelectionProjectTab _selectedTab;
    private string _statusText = "请选择项目开始本地选片工作流。";
    private bool _isBusy;
    private SelectionFinalResult? _finalResult;

    public OnlineSelectionProjectViewModel(
        IOnlineSelectionProvider provider,
        ISelectionWorkspaceStore store,
        SelectionResultSyncService syncService,
        SelectionProxyJpegService? proxyService = null,
        string? proxyRootDirectory = null,
        IDialogService? dialogService = null)
    {
        _provider = provider;
        _store = store;
        _syncService = syncService;
        _proxyService = proxyService;
        _proxyRootDirectory = string.IsNullOrWhiteSpace(proxyRootDirectory)
            ? store is JsonSelectionWorkspaceStore jsonStore
                ? Path.Combine(Path.GetDirectoryName(jsonStore.FilePath)!, "Proxies")
                : null
            : Path.GetFullPath(proxyRootDirectory);
        _dialogs = dialogService;
        Assets = [];
        SelectTabCommand = new RelayCommand(parameter => SelectTab(parameter));
        AddAssetsCommand = new AsyncRelayCommand(parameter => parameter is IEnumerable<string> paths
            ? ImportAssetsAsync(paths)
            : ChooseAndImportAssetsAsync(), _ => !IsBusy && IsProjectOpen);
        RetryFailedCommand = new AsyncRelayCommand(_ => RetryFailedAsync(), _ => !IsBusy && Assets.Any(asset => asset.Status == SelectionAssetStatus.Failed));
        PublishCommand = new AsyncRelayCommand(_ => PublishAsync(), _ => !IsBusy && IsProjectOpen);
        SyncResultsCommand = new AsyncRelayCommand(_ => ChooseAndSyncResultsAsync(), _ => !IsBusy && IsProjectOpen && FinalResult is not null);
        DeleteCloudAssetCommand = new AsyncRelayCommand(parameter => DeleteCloudAssetAsync(parameter as OnlineSelectionAssetViewModel), _ => !IsBusy && IsProjectOpen);
    }

    public ObservableCollection<OnlineSelectionAssetViewModel> Assets { get; }
    public IReadOnlyList<OnlineSelectionTabItem> Tabs { get; } =
    [
        new(OnlineSelectionProjectTab.Photos, "照片"),
        new(OnlineSelectionProjectTab.ClientSelection, "客户选片"),
        new(OnlineSelectionProjectTab.Settings, "设置"),
        new(OnlineSelectionProjectTab.DeliveryResults, "交付结果")
    ];
    public IOnlineSelectionProvider Provider => _provider;
    public SelectionProject? Project { get => _project; private set { if (SetProperty(ref _project, value)) { OnPropertyChanged(nameof(IsProjectOpen)); OnPropertyChanged(nameof(ProjectStatusText)); RefreshCommandStates(); } } }
    public SelectionRule? Rule { get => _rule; private set => SetProperty(ref _rule, value); }
    public SelectionFinalResult? FinalResult { get => _finalResult; private set { if (SetProperty(ref _finalResult, value)) RefreshCommandStates(); } }
    public OnlineSelectionProjectTab SelectedTab { get => _selectedTab; private set { if (SetProperty(ref _selectedTab, value)) { OnPropertyChanged(nameof(SelectedTabText)); } } }
    public string SelectedTabText => SelectedTab switch
    {
        OnlineSelectionProjectTab.Photos => "照片",
        OnlineSelectionProjectTab.ClientSelection => "客户选片",
        OnlineSelectionProjectTab.Settings => "设置",
        OnlineSelectionProjectTab.DeliveryResults => "交付结果",
        _ => "照片"
    };
    public string ProjectStatusText => Project is null ? "未选择项目" : SelectionDisplayText.ProjectStatus(Project.Status);
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) RefreshCommandStates(); } }
    public bool IsProjectOpen => Project is not null;
    public bool IsServiceConfigured => _provider.IsConfigured;
    public string ServiceStatusText => _provider.IsConfigured ? "在线服务已配置" : "在线选片服务尚未配置";
    public int SelectedCount => Assets.Count(asset => asset.IsSelected);
    public int FavoriteCount => Assets.Count(asset => asset.IsFavorite);
    public int ReadyCount => Assets.Count(asset => asset.Status == SelectionAssetStatus.Ready);
    public string SelectionSummary => Project is null ? "尚未创建选片项目" : $"已选 {SelectedCount}/{Project.TargetCount}";

    public ICommand SelectTabCommand { get; }
    public ICommand AddAssetsCommand { get; }
    public ICommand RetryFailedCommand { get; }
    public ICommand PublishCommand { get; }
    public ICommand SyncResultsCommand { get; }
    public ICommand DeleteCloudAssetCommand { get; }

    public async Task OpenProjectAsync(
        SelectionProject project,
        SelectionRule? rule = null,
        IEnumerable<SelectionAsset>? assets = null,
        SelectionFinalResult? finalResult = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Project = project;
        Rule = rule ?? SelectionRule.Default(project.Id, project.TargetCount, project.DeadlineUtc);
        Assets.Clear();
        var recoveredInterruptedProxy = false;
        foreach (var asset in assets ?? [])
        {
            var interruptedProxy = (asset.Status is SelectionAssetStatus.Queued or SelectionAssetStatus.Uploading) &&
                                   string.IsNullOrWhiteSpace(asset.ProxyJpegPath);
            var recovered = interruptedProxy
                ? asset with
                {
                    Status = SelectionAssetStatus.Failed,
                    LastErrorCode = OnlineSelectionErrorCodes.ProxyGenerationFailed,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                }
                : asset;
            recoveredInterruptedProxy |= interruptedProxy;
            Assets.Add(new OnlineSelectionAssetViewModel(recovered));
        }
        ApplyFinalResultCore(finalResult);
        RaiseSummaries();
        if (recoveredInterruptedProxy)
        {
            StatusText = "上次代理 JPG 生成未完成，已标记为可重试。";
            await SaveAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    public async Task ImportAssetsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        if (_proxyService is null || _proxyRootDirectory is null)
        {
            await ImportAssetsWithoutProxyAsync(paths, cancellationToken).ConfigureAwait(true);
            return;
        }
        if (Project is null) return;
        IsBusy = true;
        try
        {
            var existing = Assets.Select(asset => Path.GetFullPath(asset.LocalSourcePath))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var order = Assets.Count;
            foreach (var pathValue in paths ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(pathValue)) continue;
                string path;
                try { path = Path.GetFullPath(pathValue); }
                catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    continue;
                }
                if (!File.Exists(path) || !existing.Add(path)) continue;
                var now = DateTimeOffset.UtcNow;
                var model = new SelectionAsset(Guid.NewGuid(), Project.Id, Path.GetFileName(path), path, null,
                    SelectionAssetStatus.Queued, order++, false, now, now);
                var item = new OnlineSelectionAssetViewModel(model);
                Assets.Add(item);
                await SaveAsync(cancellationToken).ConfigureAwait(true);
                await GenerateProxyAsync(item, cancellationToken).ConfigureAwait(true);
                await SaveAsync(cancellationToken).ConfigureAwait(true);
            }

            var failed = Assets.Count(asset => asset.Status == SelectionAssetStatus.Failed);
            StatusText = Assets.Count == 0
                ? "尚未导入照片。"
                : failed == 0
                    ? $"已导入 {Assets.Count} 张照片，代理 JPG 已就绪。"
                    : $"已导入 {Assets.Count} 张照片，其中 {failed} 张代理生成失败，可单独重试。";
            RaiseSummaries();
            await SaveAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ImportAssetsWithoutProxyAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        if (Project is null) return;
        var existing = Assets.Select(asset => Path.GetFullPath(asset.LocalSourcePath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var order = Assets.Count;
        foreach (var pathValue in paths ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(pathValue)) continue;
            var path = Path.GetFullPath(pathValue);
            if (!File.Exists(path) || !existing.Add(path)) continue;
            var now = DateTimeOffset.UtcNow;
            var asset = new SelectionAsset(Guid.NewGuid(), Project.Id, Path.GetFileName(path), path, null, SelectionAssetStatus.LocalOnly, order++, false, now, now);
            Assets.Add(new OnlineSelectionAssetViewModel(asset));
        }
        StatusText = Assets.Count == 0 ? "尚未导入照片。" : $"已导入 {Assets.Count} 张本地照片；RAW 需先生成代理 JPG。";
        RaiseSummaries();
        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task ChooseAndImportAssetsAsync()
    {
        if (_dialogs is null) return Task.CompletedTask;
        var paths = _dialogs.ChooseFiles("选择在线选片照片",
            "照片与RAW|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.arw;*.cr2;*.cr3;*.dng;*.nef;*.nrw;*.orf;*.pef;*.raf;*.rw2;*.srw|所有文件|*.*",
            true);
        return ImportAssetsAsync(paths);
    }

    private async Task GenerateProxyAsync(OnlineSelectionAssetViewModel item, CancellationToken cancellationToken)
    {
        if (_proxyService is null || _proxyRootDirectory is null || Project is null) return;
        SelectionProxyResult result;
        try
        {
            var outputDirectory = Path.Combine(_proxyRootDirectory, Project.Id.ToString("N"));
            result = await _proxyService.GenerateAsync(item.LocalSourcePath, outputDirectory,
                SelectionProxyOptions.OnlineDefault, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            result = new SelectionProxyResult(SelectionProxyState.Failed, null, 0,
                "代理图生成未完成，源文件保持不变。", OnlineSelectionErrorCodes.ProxyGenerationFailed);
        }
        var now = DateTimeOffset.UtcNow;
        item.Apply(result.State == SelectionProxyState.Ready && !string.IsNullOrWhiteSpace(result.OutputPath)
            ? item.ToModel() with
            {
                ProxyJpegPath = result.OutputPath,
                ProxyBytes = result.Bytes,
                Status = SelectionAssetStatus.Ready,
                LastErrorCode = null,
                UpdatedAtUtc = now
            }
            : item.ToModel() with
            {
                ProxyJpegPath = null,
                ProxyBytes = null,
                Status = SelectionAssetStatus.Failed,
                LastErrorCode = result.ErrorCode ?? OnlineSelectionErrorCodes.ProxyGenerationFailed,
                UpdatedAtUtc = now
            });
        RaiseSummaries();
    }

    private async Task ChooseAndSyncResultsAsync()
    {
        if (_dialogs is null)
        {
            await SyncResultsAsync().ConfigureAwait(true);
            return;
        }
        var directory = _dialogs.ChooseFolder("选择选片结果归档目录", Project?.LocalSourceDirectory);
        if (directory is null)
        {
            StatusText = "未选择归档目录，选片结果尚未同步。";
            return;
        }
        await SyncResultsAsync(directory).ConfigureAwait(true);
    }

    public async Task PublishAsync(CancellationToken cancellationToken = default)
    {
        if (Project is null || Rule is null) return;
        IsBusy = true;
        try
        {
            var validation = SelectionProjectValidator.ValidateForPublish(Project, Rule, Assets.Select(asset => asset.ToModel()));
            if (!validation.IsValid) { StatusText = validation.Message; return; }
            var publish = new SelectionPublish(Guid.NewGuid(), Project.Id, Project.PublicId, 1, DateTimeOffset.UtcNow, Rule.AccessExpiresAtUtc);
            var result = await _provider.PublishProjectAsync(Project.Id, publish, cancellationToken).ConfigureAwait(true);
            StatusText = result.Success ? "项目已发布；客户可通过受保护链接进入选片。" : result.Message;
            if (result.Success) Project = Project with { Status = SelectionProjectStatus.Published, UpdatedAtUtc = DateTimeOffset.UtcNow };
            await SaveAsync(cancellationToken).ConfigureAwait(true);
        }
        finally { IsBusy = false; }
    }

    public async Task SyncResultsAsync(string? archiveDirectory = null, CancellationToken cancellationToken = default)
    {
        if (Project is null || FinalResult is null) return;
        if (string.IsNullOrWhiteSpace(archiveDirectory))
        {
            StatusText = "请选择明确的选片结果归档目录。";
            return;
        }
        IsBusy = true;
        try
        {
            var rawPaths = Assets.Select(asset => asset.LocalSourcePath).Where(path => !string.IsNullOrWhiteSpace(path));
            var result = await _syncService.SynchronizeAsync(FinalResult, rawPaths, archiveDirectory, cancellationToken).ConfigureAwait(true);
            StatusText = result.Message;
            if (result.State == SelectionSyncState.Completed) Project = Project with { Status = SelectionProjectStatus.ClientConfirmed, UpdatedAtUtc = DateTimeOffset.UtcNow };
            await SaveAsync(cancellationToken).ConfigureAwait(true);
        }
        finally { IsBusy = false; }
    }

    public async Task ApplyFinalResultAsync(SelectionFinalResult result, CancellationToken cancellationToken = default)
    {
        if (Project is null || result.SelectionProjectId != Project.Id) throw new ArgumentException("选片结果不属于当前项目。", nameof(result));
        ApplyFinalResult(result);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    public void ApplyFinalResult(SelectionFinalResult result)
    {
        if (Project is null || result.SelectionProjectId != Project.Id) throw new ArgumentException("选片结果不属于当前项目。", nameof(result));
        ApplyFinalResultCore(result);
        RaiseSummaries();
        StatusText = $"客户已确认 {result.Items.Count(item => item.Selected)} 张照片，可同步归片。";
    }

    private void ApplyFinalResultCore(SelectionFinalResult? result)
    {
        FinalResult = result;
        foreach (var asset in Assets)
        {
            var item = result?.Items.FirstOrDefault(candidate => candidate.ImageId == asset.Id);
            asset.IsSelected = item?.Selected == true;
            asset.IsFavorite = item?.Favorite == true;
            asset.CustomerNote = item?.CustomerNote ?? string.Empty;
        }
    }

    private async Task RetryFailedAsync()
    {
        if (_proxyService is null || _proxyRootDirectory is null)
        {
            await RetryFailedWithoutProxyAsync().ConfigureAwait(true);
            return;
        }
        IsBusy = true;
        try
        {
            foreach (var asset in Assets.Where(asset => asset.Status == SelectionAssetStatus.Failed).ToArray())
            {
                asset.Apply(asset.ToModel() with
                {
                    Status = SelectionAssetStatus.Queued,
                    LastErrorCode = null,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
                await GenerateProxyAsync(asset, CancellationToken.None).ConfigureAwait(true);
                await SaveAsync().ConfigureAwait(true);
            }
            var failed = Assets.Count(asset => asset.Status == SelectionAssetStatus.Failed);
            StatusText = failed == 0 ? "失败照片的代理图已重新生成。" : $"仍有 {failed} 张照片需要处理。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RetryFailedWithoutProxyAsync()
    {
        foreach (var asset in Assets.Where(asset => asset.Status == SelectionAssetStatus.Failed))
            asset.Apply(asset.ToModel() with { Status = SelectionAssetStatus.Queued, LastErrorCode = null, UpdatedAtUtc = DateTimeOffset.UtcNow });
        StatusText = "失败照片已重新排队，可继续上传。";
        await SaveAsync().ConfigureAwait(true);
    }

    private async Task DeleteCloudAssetAsync(OnlineSelectionAssetViewModel? asset)
    {
        if (Project is null || asset is null) return;
        IsBusy = true;
        try
        {
            var result = await _provider.DeleteCloudAssetAsync(Project.Id, asset.Id).ConfigureAwait(true);
            if (result.Success) asset.Apply(asset.ToModel() with { Status = SelectionAssetStatus.DeletedCloudCopy, CloudAssetId = null, UpdatedAtUtc = DateTimeOffset.UtcNow });
            StatusText = result.Success ? "云端副本已删除，本地文件未删除。" : result.Message;
            if (result.Success) await SaveAsync().ConfigureAwait(true);
        }
        finally { IsBusy = false; }
    }

    private Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var project = Project;
        var rule = Rule;
        if (project is null || rule is null) return Task.CompletedTask;
        var assets = Assets.Select(item => item.ToModel()).ToArray();
        var finalResult = FinalResult;
        return MergeAndSaveAsync(project, rule, assets, finalResult, cancellationToken);
    }

    private async Task MergeAndSaveAsync(
        SelectionProject project,
        SelectionRule rule,
        IReadOnlyList<SelectionAsset> projectAssets,
        SelectionFinalResult? finalResult,
        CancellationToken cancellationToken)
    {
        var gate = GetStoreWriteGate(_store);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            var projects = snapshot.Projects.Where(item => item.Id != project.Id).Append(project).ToArray();
            var assets = snapshot.Assets.Where(item => item.ProjectId != project.Id).Concat(projectAssets).ToArray();
            var rules = snapshot.Rules.Where(item => item.ProjectId != project.Id).Append(rule).ToArray();
            var results = finalResult is null
                ? snapshot.FinalResults.ToArray()
                : snapshot.FinalResults.Where(item => item.SelectionProjectId != project.Id).Append(finalResult).ToArray();
            await _store.SaveAsync(new(projects, assets, rules, results), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private static SemaphoreSlim GetStoreWriteGate(ISelectionWorkspaceStore store) =>
        store is JsonSelectionWorkspaceStore jsonStore
            ? FileStoreWriteGates.GetOrAdd(jsonStore.FilePath, static _ => new SemaphoreSlim(1, 1))
            : StoreWriteGates.GetValue(store, static _ => new SemaphoreSlim(1, 1));

    private void SelectTab(object? parameter)
    {
        if (parameter is OnlineSelectionTabItem item) SelectedTab = item.Value;
        else if (parameter is OnlineSelectionProjectTab tab) SelectedTab = tab;
        else if (parameter is string text && Enum.TryParse<OnlineSelectionProjectTab>(text, true, out var parsed)) SelectedTab = parsed;
    }

    private void RaiseSummaries()
    {
        OnPropertyChanged(nameof(SelectedCount)); OnPropertyChanged(nameof(FavoriteCount)); OnPropertyChanged(nameof(ReadyCount)); OnPropertyChanged(nameof(SelectionSummary));
        RefreshCommandStates();
    }

    private void RefreshCommandStates()
    {
        (AddAssetsCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RetryFailedCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (PublishCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (SyncResultsCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (DeleteCloudAssetCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }
}

public sealed class OnlineSelectionViewModel : ObservableObject
{
    private readonly ISelectionWorkspaceStore _store;
    private readonly IOnlineSelectionProvider _provider;
    private readonly SelectionResultSyncService _syncService;
    private readonly SelectionProxyJpegService? _proxyService;
    private readonly string? _proxyRootDirectory;
    private readonly IDialogService? _dialogs;
    private SelectionProject? _selectedProject;
    private bool _isCreateModalOpen;
    private bool _isBusy;
    private string _projectName = string.Empty;
    private string _clientName = string.Empty;
    private string _targetCountText = "30";
    private DateTime? _deadline = DateTime.Today.AddDays(14);
    private string _statusText = string.Empty;

    public OnlineSelectionViewModel(
        IOnlineSelectionProvider? provider = null,
        ISelectionWorkspaceStore? store = null,
        SelectionResultSyncService? syncService = null,
        SelectionProxyJpegService? proxyService = null,
        string? proxyRootDirectory = null,
        IDialogService? dialogService = null)
    {
        _provider = provider ?? OnlineSelectionProviderFactory.CreateDefault();
        _store = store ?? new InMemorySelectionWorkspaceStore();
        _syncService = syncService ?? new SelectionResultSyncService(new FileNameNormalizer());
        _proxyService = proxyService;
        _proxyRootDirectory = proxyRootDirectory;
        _dialogs = dialogService;
        Projects = [];
        ProjectPage = new OnlineSelectionProjectViewModel(_provider, _store, _syncService,
            _proxyService, _proxyRootDirectory, _dialogs);
        CreateProjectCommand = new RelayCommand(_ => IsCreateModalOpen = true, _ => !IsBusy);
        CancelCreateCommand = new RelayCommand(_ => IsCreateModalOpen = false);
        CreateAndImportCommand = new AsyncRelayCommand(parameter => parameter is IEnumerable<string> paths
            ? CreateProjectAsync(paths)
            : CreateProjectFromCommandAsync(), _ => !IsBusy);
        OpenProjectCommand = new AsyncRelayCommand(parameter => parameter switch
        {
            OnlineSelectionProjectListItemViewModel item => OpenProjectAsync(item.Project),
            SelectionProject project => OpenProjectAsync(project),
            _ => Task.CompletedTask
        }, _ => !IsBusy);
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync(), _ => !IsBusy);
    }

    public ObservableCollection<OnlineSelectionProjectListItemViewModel> Projects { get; }
    public OnlineSelectionProjectViewModel ProjectPage { get; }
    public IOnlineSelectionProvider Provider => _provider;
    public string ServiceStatusText => _provider.IsConfigured ? "在线服务已配置" : "在线选片服务尚未配置";
    public bool IsServiceConfigured => _provider.IsConfigured;
    public SelectionProject? SelectedProject { get => _selectedProject; private set => SetProperty(ref _selectedProject, value); }
    public bool IsCreateModalOpen { get => _isCreateModalOpen; private set => SetProperty(ref _isCreateModalOpen, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string ProjectName { get => _projectName; set => SetProperty(ref _projectName, value ?? string.Empty); }
    public string ClientName { get => _clientName; set => SetProperty(ref _clientName, value ?? string.Empty); }
    public string TargetCountText { get => _targetCountText; set => SetProperty(ref _targetCountText, value ?? string.Empty); }
    public DateTime? Deadline { get => _deadline; set => SetProperty(ref _deadline, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool HasProjects => Projects.Count > 0;

    public ICommand CreateProjectCommand { get; }
    public ICommand CancelCreateCommand { get; }
    public ICommand CreateAndImportCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand RefreshCommand { get; }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(true);
        Projects.Clear();
        foreach (var project in snapshot.Projects) Projects.Add(new(project));
        OnPropertyChanged(nameof(HasProjects));
        StatusText = Projects.Count == 0 ? "尚未创建选片项目。" : $"共有 {Projects.Count} 个本地选片项目。";
    }

    private async Task CreateProjectFromCommandAsync()
    {
        if (!TryValidateCreateInput()) return;
        var paths = _dialogs?.ChooseFiles("选择在线选片照片",
            "照片与RAW|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.arw;*.cr2;*.cr3;*.dng;*.nef;*.nrw;*.orf;*.pef;*.raf;*.rw2;*.srw|所有文件|*.*",
            true) ?? [];
        await CreateProjectAsync(paths).ConfigureAwait(true);
    }

    private bool TryValidateCreateInput()
    {
        if (!int.TryParse(TargetCountText, out var targetCount) || targetCount <= 0)
        {
            StatusText = "目标数量必须是大于零的数字。";
            return false;
        }
        var project = SelectionProjectFactory.CreateDraft(ProjectName, ClientName, targetCount,
            Deadline?.ToUniversalTime());
        var validation = SelectionProjectValidator.ValidateDraft(project);
        if (validation.IsValid) return true;
        StatusText = validation.Message;
        return false;
    }

    public async Task CreateProjectAsync(IEnumerable<string> initialPaths, CancellationToken cancellationToken = default)
    {
        var selectedPaths = (initialPaths ?? []).Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
        if (!int.TryParse(TargetCountText, out var targetCount) || targetCount <= 0)
        {
            StatusText = "目标数量必须是大于零的数字。";
            return;
        }
        var project = SelectionProjectFactory.CreateDraft(ProjectName, ClientName, targetCount, Deadline?.ToUniversalTime());
        var validation = SelectionProjectValidator.ValidateDraft(project);
        if (!validation.IsValid) { StatusText = validation.Message; return; }
        IsBusy = true;
        try
        {
            Projects.Add(new(project));
            SelectedProject = project;
            await ProjectPage.OpenProjectAsync(project, SelectionRule.Default(project.Id, targetCount, project.DeadlineUtc), cancellationToken: cancellationToken).ConfigureAwait(true);
            await ProjectPage.ImportAssetsAsync(selectedPaths, cancellationToken).ConfigureAwait(true);
            IsCreateModalOpen = false;
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
            var importedCount = ProjectPage.Assets.Count;
            var failedCount = ProjectPage.Assets.Count(asset => asset.Status == SelectionAssetStatus.Failed);
            StatusText = selectedPaths.Length == 0
                ? "选片项目已创建为本地草稿；尚未选择照片。"
                : importedCount == 0
                    ? "选片项目已创建为本地草稿；没有可访问的照片被导入。"
                    : failedCount == 0
                        ? $"选片项目已创建并导入 {importedCount} 张照片，代理 JPG 已就绪。"
                        : $"选片项目已创建并导入 {importedCount} 张照片，其中 {failedCount} 张代理生成失败。";
        }
        finally { IsBusy = false; }
    }

    public Task OpenProjectAsync(OnlineSelectionProjectListItemViewModel? item, CancellationToken cancellationToken = default) =>
        OpenProjectAsync(item?.Project, cancellationToken);

    public async Task OpenProjectAsync(SelectionProject? project, CancellationToken cancellationToken = default)
    {
        if (project is null) return;
        IsBusy = true;
        try
        {
            var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(true);
            var assets = snapshot.Assets.Where(asset => asset.ProjectId == project.Id);
            var rule = snapshot.Rules.FirstOrDefault(item => item.ProjectId == project.Id);
            var finalResult = snapshot.FinalResults.FirstOrDefault(item => item.SelectionProjectId == project.Id);
            await ProjectPage.OpenProjectAsync(project, rule, assets, finalResult, cancellationToken).ConfigureAwait(true);
            SelectedProject = project;
        }
        finally { IsBusy = false; }
    }
}
