using System.Collections.ObjectModel;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.OnlineSelection;
using RAWSelectionAssistant.Core.Utilities;
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

public sealed class OnlineSelectionAssetViewModel(SelectionAsset asset) : ObservableObject
{
    private SelectionAsset _asset = asset;
    private bool _isSelected;
    private bool _isFavorite;
    private string _customerNote = string.Empty;

    public Guid Id => _asset.Id;
    public Guid ProjectId => _asset.ProjectId;
    public string OriginalFileName => _asset.OriginalFileName;
    public string LocalSourcePath => _asset.LocalSourcePath;
    public string? ProxyJpegPath => _asset.ProxyJpegPath;
    public SelectionAssetStatus Status => _asset.Status;
    public string StatusText => SelectionDisplayText.AssetStatus(Status);
    public string? CloudAssetId => _asset.CloudAssetId;
    public bool IsCover => _asset.IsCover;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public bool IsFavorite { get => _isFavorite; set => SetProperty(ref _isFavorite, value); }
    public string CustomerNote { get => _customerNote; set => SetProperty(ref _customerNote, value ?? string.Empty); }

    public SelectionAsset ToModel() => _asset;
    public void Apply(SelectionAsset asset)
    {
        _asset = asset;
        OnPropertyChanged(string.Empty);
    }
}

public sealed class OnlineSelectionProjectViewModel : ObservableObject
{
    private readonly IOnlineSelectionProvider _provider;
    private readonly ISelectionWorkspaceStore _store;
    private readonly SelectionResultSyncService _syncService;
    private SelectionProject? _project;
    private SelectionRule? _rule;
    private OnlineSelectionProjectTab _selectedTab;
    private string _statusText = "请选择项目开始本地选片工作流。";
    private bool _isBusy;
    private SelectionFinalResult? _finalResult;

    public OnlineSelectionProjectViewModel(
        IOnlineSelectionProvider provider,
        ISelectionWorkspaceStore store,
        SelectionResultSyncService syncService)
    {
        _provider = provider;
        _store = store;
        _syncService = syncService;
        Assets = [];
        SelectTabCommand = new RelayCommand(parameter => SelectTab(parameter));
        AddAssetsCommand = new AsyncRelayCommand(parameter => ImportAssetsAsync(parameter as IEnumerable<string> ?? []), _ => !IsBusy && IsProjectOpen);
        RetryFailedCommand = new AsyncRelayCommand(_ => RetryFailedAsync(), _ => !IsBusy && Assets.Any(asset => asset.Status == SelectionAssetStatus.Failed));
        PublishCommand = new AsyncRelayCommand(_ => PublishAsync(), _ => !IsBusy && IsProjectOpen);
        SyncResultsCommand = new AsyncRelayCommand(_ => SyncResultsAsync(), _ => !IsBusy && IsProjectOpen && FinalResult is not null);
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
    public SelectionProject? Project { get => _project; private set { if (SetProperty(ref _project, value)) { OnPropertyChanged(nameof(IsProjectOpen)); OnPropertyChanged(nameof(ProjectStatusText)); } } }
    public SelectionRule? Rule { get => _rule; private set => SetProperty(ref _rule, value); }
    public SelectionFinalResult? FinalResult { get => _finalResult; private set => SetProperty(ref _finalResult, value); }
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
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
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

    public async Task OpenProjectAsync(SelectionProject project, SelectionRule? rule = null, IEnumerable<SelectionAsset>? assets = null, CancellationToken cancellationToken = default)
    {
        Project = project;
        Rule = rule ?? SelectionRule.Default(project.Id, project.TargetCount, project.DeadlineUtc);
        Assets.Clear();
        foreach (var asset in assets ?? []) Assets.Add(new OnlineSelectionAssetViewModel(asset));
        FinalResult = null;
        RaiseSummaries();
        await Task.CompletedTask;
    }

    public async Task ImportAssetsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
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
        IsBusy = true;
        try
        {
            var rawPaths = Assets.Select(asset => asset.LocalSourcePath).Where(path => !string.IsNullOrWhiteSpace(path));
            var directory = archiveDirectory ?? Path.Combine(Path.GetTempPath(), "PixelTart", "SelectionResults");
            var result = await _syncService.SynchronizeAsync(FinalResult, rawPaths, directory, cancellationToken).ConfigureAwait(true);
            StatusText = result.Message;
            if (result.State == SelectionSyncState.Completed) Project = Project with { Status = SelectionProjectStatus.ClientConfirmed, UpdatedAtUtc = DateTimeOffset.UtcNow };
        }
        finally { IsBusy = false; }
    }

    public void ApplyFinalResult(SelectionFinalResult result)
    {
        if (Project is null || result.SelectionProjectId != Project.Id) throw new ArgumentException("选片结果不属于当前项目。", nameof(result));
        FinalResult = result;
        foreach (var item in result.Items)
        {
            var asset = Assets.FirstOrDefault(candidate => candidate.Id == item.ImageId);
            if (asset is null) continue;
            asset.IsSelected = item.Selected;
            asset.IsFavorite = item.Favorite;
            asset.CustomerNote = item.CustomerNote ?? string.Empty;
        }
        RaiseSummaries();
        StatusText = $"客户已确认 {result.Items.Count(item => item.Selected)} 张照片，可同步归片。";
    }

    private async Task RetryFailedAsync()
    {
        foreach (var asset in Assets.Where(asset => asset.Status == SelectionAssetStatus.Failed))
            asset.Apply(asset.ToModel() with { Status = SelectionAssetStatus.Queued, LastErrorCode = null, UpdatedAtUtc = DateTimeOffset.UtcNow });
        StatusText = "失败照片已重新排队，可继续上传。";
        await Task.CompletedTask;
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
        }
        finally { IsBusy = false; }
    }

    private Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (Project is null || Rule is null) return Task.CompletedTask;
        return _store.SaveAsync(new SelectionWorkspaceSnapshot([Project], Assets.Select(asset => asset.ToModel()).ToArray(), [Rule], FinalResult is null ? [] : [FinalResult]), cancellationToken);
    }

    private void SelectTab(object? parameter)
    {
        if (parameter is OnlineSelectionTabItem item) SelectedTab = item.Value;
        else if (parameter is OnlineSelectionProjectTab tab) SelectedTab = tab;
        else if (parameter is string text && Enum.TryParse<OnlineSelectionProjectTab>(text, true, out var parsed)) SelectedTab = parsed;
    }

    private void RaiseSummaries()
    {
        OnPropertyChanged(nameof(SelectedCount)); OnPropertyChanged(nameof(FavoriteCount)); OnPropertyChanged(nameof(ReadyCount)); OnPropertyChanged(nameof(SelectionSummary));
    }
}

public sealed class OnlineSelectionViewModel : ObservableObject
{
    private readonly ISelectionWorkspaceStore _store;
    private readonly IOnlineSelectionProvider _provider;
    private readonly SelectionResultSyncService _syncService;
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
        SelectionResultSyncService? syncService = null)
    {
        _provider = provider ?? OnlineSelectionProviderFactory.CreateDefault();
        _store = store ?? new InMemorySelectionWorkspaceStore();
        _syncService = syncService ?? new SelectionResultSyncService(new FileNameNormalizer());
        Projects = [];
        ProjectPage = new OnlineSelectionProjectViewModel(_provider, _store, _syncService);
        CreateProjectCommand = new RelayCommand(_ => IsCreateModalOpen = true, _ => !IsBusy);
        CancelCreateCommand = new RelayCommand(_ => IsCreateModalOpen = false);
        CreateAndImportCommand = new AsyncRelayCommand(parameter => CreateProjectAsync(parameter as IEnumerable<string> ?? []), _ => !IsBusy);
        OpenProjectCommand = new AsyncRelayCommand(parameter => OpenProjectAsync(parameter as SelectionProject), _ => !IsBusy);
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync(), _ => !IsBusy);
    }

    public ObservableCollection<SelectionProject> Projects { get; }
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
        foreach (var project in snapshot.Projects) Projects.Add(project);
        OnPropertyChanged(nameof(HasProjects));
        StatusText = Projects.Count == 0 ? "尚未创建选片项目。" : $"共有 {Projects.Count} 个本地选片项目。";
    }

    public async Task CreateProjectAsync(IEnumerable<string> initialPaths, CancellationToken cancellationToken = default)
    {
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
            Projects.Add(project);
            SelectedProject = project;
            await ProjectPage.OpenProjectAsync(project, SelectionRule.Default(project.Id, targetCount, project.DeadlineUtc), cancellationToken: cancellationToken).ConfigureAwait(true);
            await ProjectPage.ImportAssetsAsync(initialPaths, cancellationToken).ConfigureAwait(true);
            IsCreateModalOpen = false;
            StatusText = "选片项目已创建，可以继续导入代理 JPG。";
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
        }
        finally { IsBusy = false; }
    }

    public async Task OpenProjectAsync(SelectionProject? project, CancellationToken cancellationToken = default)
    {
        if (project is null) return;
        IsBusy = true;
        try
        {
            var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(true);
            var assets = snapshot.Assets.Where(asset => asset.ProjectId == project.Id);
            var rule = snapshot.Rules.FirstOrDefault(item => item.ProjectId == project.Id);
            await ProjectPage.OpenProjectAsync(project, rule, assets, cancellationToken).ConfigureAwait(true);
            SelectedProject = project;
        }
        finally { IsBusy = false; }
    }
}
