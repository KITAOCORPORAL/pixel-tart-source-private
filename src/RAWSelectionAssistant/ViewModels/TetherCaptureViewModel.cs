using System.Collections.ObjectModel;
using System.Windows;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Tethering;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed class TetherCaptureViewModel : ObservableObject, IAsyncDisposable
{
    private readonly WatchFolderCameraAdapter _adapter;
    private readonly ITetherSessionRepository _sessionRepository;
    private readonly ITetherAssetRepository _assetRepository;
    private readonly ITetherProxyCache _proxyCache;
    private readonly IDialogService _dialogs;
    private ICameraSession? _activeSession;
    private string _watchDirectory = string.Empty;
    private string _projectDestination = string.Empty;
    private string _backupDestination = string.Empty;
    private bool _importExisting;
    private bool _copyToProject;
    private bool _copyToBackup;
    private bool _verifySha256;
    private bool _isRunning;
    private bool _isBusy;
    private int _existingCandidateCount;
    private int _queueDepth;
    private bool _hasRecoverableSession;
    private string _statusText = "尚未启动。默认 Provider 为 None。";
    private TetherAssetItemViewModel? _selectedAsset;

    public TetherCaptureViewModel(WatchFolderCameraAdapter adapter, ITetherSessionRepository sessionRepository, ITetherAssetRepository assetRepository,
        ITetherProxyCache proxyCache, IDialogService dialogs)
    {
        _adapter = adapter;
        _sessionRepository = sessionRepository;
        _assetRepository = assetRepository;
        _proxyCache = proxyCache;
        _dialogs = dialogs;
        ChooseWatchFolderCommand = new RelayCommand(_ => ChooseWatchFolder(), _ => !IsRunning && !IsBusy);
        PreviewExistingCommand = new RelayCommand(_ => CountExisting(), _ => !IsRunning && Directory.Exists(WatchDirectory));
        ChooseProjectDestinationCommand = new RelayCommand(_ => ChooseProjectDestination(), _ => !IsRunning && !IsBusy);
        ChooseBackupDestinationCommand = new RelayCommand(_ => ChooseBackupDestination(), _ => !IsRunning && !IsBusy);
        StartCommand = new AsyncRelayCommand(_ => StartAsync(), _ => !IsRunning && !IsBusy && Directory.Exists(WatchDirectory));
        StopCommand = new AsyncRelayCommand(_ => StopAsync(), _ => IsRunning && !IsBusy);
        ReconcileCommand = new AsyncRelayCommand(_ => ReconcileAsync(), _ => IsRunning && !IsBusy);
        ClearProxyCacheCommand = new AsyncRelayCommand(_ => ClearProxyCacheAsync(), _ => !IsBusy);
        RevealAssetCommand = new RelayCommand(value => RevealAsset(value as TetherAssetItemViewModel), value => value is TetherAssetItemViewModel);
    }

    public ObservableCollection<TetherAssetItemViewModel> Assets { get; } = [];
    public RelayCommand ChooseWatchFolderCommand { get; }
    public RelayCommand PreviewExistingCommand { get; }
    public RelayCommand ChooseProjectDestinationCommand { get; }
    public RelayCommand ChooseBackupDestinationCommand { get; }
    public AsyncRelayCommand StartCommand { get; }
    public AsyncRelayCommand StopCommand { get; }
    public AsyncRelayCommand ReconcileCommand { get; }
    public AsyncRelayCommand ClearProxyCacheCommand { get; }
    public RelayCommand RevealAssetCommand { get; }

    public string WatchDirectory { get => _watchDirectory; set { if (SetProperty(ref _watchDirectory, value)) { ExistingCandidateCount = 0; RefreshCommands(); } } }
    public string ProjectDestination { get => _projectDestination; set { if (SetProperty(ref _projectDestination, value)) RefreshCommands(); } }
    public string BackupDestination { get => _backupDestination; set { if (SetProperty(ref _backupDestination, value)) RefreshCommands(); } }
    public bool ImportExisting { get => _importExisting; set => SetProperty(ref _importExisting, value); }
    public bool CopyToProject { get => _copyToProject; set { if (SetProperty(ref _copyToProject, value)) { OnPropertyChanged(nameof(CopyStatusText)); RefreshCommands(); } } }
    public bool CopyToBackup { get => _copyToBackup; set { if (SetProperty(ref _copyToBackup, value)) { OnPropertyChanged(nameof(CopyStatusText)); RefreshCommands(); } } }
    public bool VerifySha256 { get => _verifySha256; set => SetProperty(ref _verifySha256, value); }
    public bool IsRunning { get => _isRunning; private set { if (SetProperty(ref _isRunning, value)) { OnPropertyChanged(nameof(ProviderText)); RefreshCommands(); } } }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) RefreshCommands(); } }
    public int ExistingCandidateCount { get => _existingCandidateCount; private set => SetProperty(ref _existingCandidateCount, value); }
    public int QueueDepth { get => _queueDepth; private set => SetProperty(ref _queueDepth, value); }
    public bool HasRecoverableSession { get => _hasRecoverableSession; private set { if (SetProperty(ref _hasRecoverableSession, value)) OnPropertyChanged(nameof(StartButtonText)); } }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string StartButtonText => HasRecoverableSession ? "恢复目录并继续" : "启动看守";
    public string ProviderText => IsRunning ? "Watch Folder" : "None";
    public bool IncludeSubdirectories => false;
    public int ReadyCount => Assets.Count(item => item.Record.ProcessingState is TetherProcessingState.Ready or TetherProcessingState.Copied);
    public int AttentionCount => Assets.Count(item => item.Record.ProcessingState is TetherProcessingState.NeedsAttention or TetherProcessingState.PartiallyCompleted);
    public int DiscoveredCount => Assets.Count;
    public int WaitingStableCount => Assets.Count(item => item.Record.StabilityState is TetherStabilityState.Pending or TetherStabilityState.Probing);
    public int FailedCount => Assets.Count(item => item.Record.ProcessingState == TetherProcessingState.Failed || item.Record.PreviewState == TetherPreviewState.Failed);
    public string CopyStatusText => !CopyToProject && !CopyToBackup ? "复制：关闭" : $"复制：{Assets.Count(item => item.Record.ProcessingState == TetherProcessingState.Copied)} 完成 / {AttentionCount} 需处理";

    public TetherAssetItemViewModel? SelectedAsset
    {
        get => _selectedAsset;
        set => SetProperty(ref _selectedAsset, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var recovered = await _adapter.RecoverLatestAsync(cancellationToken);
        if (recovered is not null)
        {
            Attach(recovered);
            ApplySnapshot(new(recovered.Session, await LoadAssetsAsync(recovered.Session.Id, cancellationToken), 0, true));
            StatusText = "已恢复上次未停止的看守会话，并按数据库状态继续。";
            return;
        }

        var pending = (await _sessionRepository.ListActiveAsync(cancellationToken)).FirstOrDefault();
        if (pending is not null)
        {
            WatchDirectory = pending.WatchDirectory;
            HasRecoverableSession = true;
            StatusText = "上次看守目录暂时不可访问。源文件和数据库记录均未删除。";
        }
    }

    public async Task StopAsync()
    {
        if (_activeSession is null) return;
        IsBusy = true;
        try
        {
            await _activeSession.StopAsync();
            Detach(_activeSession);
            await _activeSession.DisposeAsync();
            _activeSession = null;
            IsRunning = false;
            StatusText = "看守已停止。已发现文件和复制结果仍然保留。";
        }
        catch (Exception) { StatusText = "停止会话时遇到问题，请在任务中心检查。"; }
        finally { IsBusy = false; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_activeSession is not null)
        {
            Detach(_activeSession);
            await _activeSession.DisposeAsync();
            _activeSession = null;
        }
    }

    private async Task StartAsync()
    {
        if (CopyToProject && string.IsNullOrWhiteSpace(ProjectDestination)) { _dialogs.ShowError("请先选择项目资料目录，或关闭项目复制。"); return; }
        if (CopyToBackup && string.IsNullOrWhiteSpace(BackupDestination)) { _dialogs.ShowError("请先选择独立备份目录，或关闭独立备份。"); return; }
        IsBusy = true;
        try
        {
            var session = await _adapter.StartAsync(new(WatchDirectory, ImportExisting: ImportExisting, CopyToProject: CopyToProject,
                ProjectDestination: ProjectDestination, CopyToBackup: CopyToBackup, BackupDestination: BackupDestination, VerifySha256: VerifySha256));
            Attach(session);
            Assets.Clear();
            StatusText = ImportExisting ? "看守已启动，正在检查顶层已有文件。" : "看守已启动，只接收本次开始后创建的顶层文件。";
        }
        catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusText = "看守未启动。请检查目录状态和复制选项。";
            _dialogs.ShowError(StatusText);
        }
        finally { IsBusy = false; }
    }

    private void Attach(ICameraSession session)
    {
        _activeSession = session;
        HasRecoverableSession = false;
        session.SnapshotChanged += Session_SnapshotChanged;
        WatchDirectory = session.Session.WatchDirectory;
        ProjectDestination = session.Session.ProjectDestination ?? string.Empty;
        BackupDestination = session.Session.BackupDestination ?? string.Empty;
        ImportExisting = session.Session.ImportExisting;
        CopyToProject = session.Session.CopyToProject;
        CopyToBackup = session.Session.CopyToBackup;
        IsRunning = session.Session.State == TetherSessionState.Running;
    }

    private void Detach(ICameraSession session) => session.SnapshotChanged -= Session_SnapshotChanged;

    private void Session_SnapshotChanged(object? sender, TetherSessionSnapshot snapshot)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess()) dispatcher.BeginInvoke(() => ApplySnapshot(snapshot));
        else ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(TetherSessionSnapshot snapshot)
    {
        var selectedId = SelectedAsset?.Record.Id;
        Assets.Clear();
        foreach (var asset in snapshot.Assets) Assets.Add(new(asset, _proxyCache.ResolvePath(asset.ProxyCacheKey)));
        SelectedAsset = selectedId.HasValue ? Assets.FirstOrDefault(item => item.Record.Id == selectedId.Value) : Assets.FirstOrDefault();
        QueueDepth = snapshot.QueueDepth;
        IsRunning = snapshot.Session.State == TetherSessionState.Running;
        StatusText = snapshot.Session.State switch
        {
            TetherSessionState.Running when snapshot.ReconciliationPending => "正在进行顶层目录补偿核对。",
            TetherSessionState.Running => "看守运行中。所有文件会先通过稳定检测。",
            TetherSessionState.NeedsAttention => "会话需要处理；不会删除或移动任何源文件。",
            _ => "看守已停止。"
        };
        OnPropertyChanged(nameof(ReadyCount));
        OnPropertyChanged(nameof(AttentionCount));
        OnPropertyChanged(nameof(DiscoveredCount));
        OnPropertyChanged(nameof(WaitingStableCount));
        OnPropertyChanged(nameof(FailedCount));
        OnPropertyChanged(nameof(CopyStatusText));
    }

    private async Task ReconcileAsync()
    {
        if (_activeSession is null) return;
        IsBusy = true;
        try { await _activeSession.ReconcileAsync(); StatusText = "顶层目录核对完成。"; }
        catch (Exception) { StatusText = "目录核对未完成，源文件保持不变。"; }
        finally { IsBusy = false; }
    }

    private async Task ClearProxyCacheAsync()
    {
        IsBusy = true;
        try { await _proxyCache.ClearAsync(); StatusText = "联机预览缓存已清理；原文件和数据库记录未改变。"; }
        catch (Exception) { StatusText = "部分缓存暂时无法清理；原文件未受影响。"; }
        finally { IsBusy = false; }
    }

    private void ChooseWatchFolder()
    {
        var selected = _dialogs.ChooseFolder("选择看守文件夹", WatchDirectory);
        if (!string.IsNullOrWhiteSpace(selected)) { WatchDirectory = selected; CountExisting(); }
    }

    private void ChooseProjectDestination()
    {
        var selected = _dialogs.ChooseFolder("选择项目资料目录", ProjectDestination);
        if (!string.IsNullOrWhiteSpace(selected)) ProjectDestination = selected;
    }

    private void ChooseBackupDestination()
    {
        var selected = _dialogs.ChooseFolder("选择独立备份目录", BackupDestination);
        if (!string.IsNullOrWhiteSpace(selected)) BackupDestination = selected;
    }

    private void CountExisting()
    {
        try { ExistingCandidateCount = Directory.EnumerateFiles(WatchDirectory, "*", SearchOption.TopDirectoryOnly).Count(path => WatchFolderPathPolicy.IsCandidate(WatchDirectory, path)); }
        catch { ExistingCandidateCount = 0; }
    }

    private void RevealAsset(TetherAssetItemViewModel? item)
    {
        if (item is not null) _dialogs.RevealFile(item.Record.SourcePath);
    }

    private async Task<IReadOnlyList<TetherAssetRecord>> LoadAssetsAsync(Guid sessionId, CancellationToken cancellationToken)
        => await _assetRepository.ListBySessionAsync(sessionId, cancellationToken);

    private void RefreshCommands()
    {
        ChooseWatchFolderCommand.RaiseCanExecuteChanged(); PreviewExistingCommand.RaiseCanExecuteChanged();
        ChooseProjectDestinationCommand.RaiseCanExecuteChanged(); ChooseBackupDestinationCommand.RaiseCanExecuteChanged();
        StartCommand.RaiseCanExecuteChanged(); StopCommand.RaiseCanExecuteChanged(); ReconcileCommand.RaiseCanExecuteChanged();
        ClearProxyCacheCommand.RaiseCanExecuteChanged();
    }
}

public sealed class TetherAssetItemViewModel
{
    public TetherAssetItemViewModel(TetherAssetRecord record, string? proxyPath)
    {
        Record = record;
        ProxyPath = proxyPath;
    }

    public TetherAssetRecord Record { get; }
    public string? ProxyPath { get; }
    public string FileName => Record.FileName;
    public bool IsRaw => Record.MediaKind == TetherMediaKind.Raw;
    public string MediaKindText => Record.MediaKind == TetherMediaKind.Raw ? "RAW（安全占位）" : "预览图";
    public string StateText => Record.ProcessingState switch
    {
        TetherProcessingState.Copied => "已安全复制",
        TetherProcessingState.PartiallyCompleted => "部分完成",
        TetherProcessingState.NeedsAttention => "需要处理",
        TetherProcessingState.Ready => "已就绪",
        _ => Record.StabilityState == TetherStabilityState.Probing ? "稳定检测中" : Record.ProcessingState.ToString()
    };
    public string PairText => Record.PairedAssetId.HasValue ? "JPG/RAW 已配对" : "未配对";
}
