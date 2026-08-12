using System.Collections.ObjectModel;
using System.Windows;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Tasks;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed class TaskCenterViewModel : ObservableObject
{
    private readonly ITaskEngine _engine;
    private readonly IRecoveryCoordinator? _recovery;
    private readonly SynchronizationContext? _context;
    private TaskSnapshotViewModel? _selectedTask;

    public TaskCenterViewModel(ITaskEngine engine, IRecoveryCoordinator? recovery = null)
    {
        _engine = engine;
        _recovery = recovery;
        _context = SynchronizationContext.Current;
        foreach (var snapshot in engine.Current) Upsert(snapshot);
        engine.SnapshotChanged += (_, snapshot) => Dispatch(() => Upsert(snapshot));
        PauseCommand = new AsyncRelayCommand(parameter => ActAsync(parameter, _engine.PauseAsync), parameter => Resolve(parameter)?.CanPause == true);
        ResumeCommand = new AsyncRelayCommand(parameter => ActAsync(parameter, _engine.ResumeAsync), parameter => Resolve(parameter)?.CanResume == true);
        CancelCommand = new AsyncRelayCommand(parameter => ActAsync(parameter, _engine.CancelAsync), parameter => Resolve(parameter)?.CanCancel == true);
        RetryCommand = new AsyncRelayCommand(RetryAsync, parameter => Resolve(parameter)?.CanRetry == true);
        ResolveAttentionCommand = new AsyncRelayCommand(parameter => ResolveAttentionAsync(parameter), parameter => Resolve(parameter)?.CanResolveAttention == true);
        RollbackCommand = new AsyncRelayCommand(RollbackAsync, parameter => Resolve(parameter)?.CanRollback == true && _recovery is not null);
        AbandonCommand = new AsyncRelayCommand(AbandonAsync, parameter => Resolve(parameter)?.CanAbandon == true && _recovery is not null);
        ClearCompletedCommand = new RelayCommand(_ => ClearCompleted(), _ => Tasks.Any(x => x.IsTerminal));
        SelectTaskCommand = new RelayCommand(parameter => SelectedTask = Resolve(parameter));
    }

    public ObservableCollection<TaskSnapshotViewModel> Tasks { get; } = [];
    public TaskSnapshotViewModel? SelectedTask { get => _selectedTask; set => SetProperty(ref _selectedTask, value); }
    public int ActiveCount => Tasks.Count(x => !x.IsTerminal);
    public int AttentionCount => Tasks.Count(x => x.State == TaskLifecycleState.NeedsAttention);
    public bool HasTasks => Tasks.Count > 0;
    public bool HasNoTasks => !HasTasks;
    public IReadOnlyList<TaskSnapshotViewModel> VisibleTasks => Tasks.Where(item => !item.IsTerminal).Concat(Tasks.Where(item => item.IsTerminal).Take(2)).ToArray();
    public string EmptyMessage => "暂无待处理任务";
    public AsyncRelayCommand PauseCommand { get; }
    public AsyncRelayCommand ResumeCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }
    public AsyncRelayCommand RetryCommand { get; }
    public AsyncRelayCommand ResolveAttentionCommand { get; }
    public AsyncRelayCommand RollbackCommand { get; }
    public AsyncRelayCommand AbandonCommand { get; }
    public RelayCommand ClearCompletedCommand { get; }
    public RelayCommand SelectTaskCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        foreach (var runtime in await _engine.LoadHistoryAsync(200, cancellationToken))
        {
            Upsert(new TaskProgressSnapshot(runtime.Definition.Id, runtime.Definition.ProjectId, runtime.Definition.DisplayName, runtime.State, runtime.Progress, runtime.CurrentStep, runtime.CurrentFile, runtime.ResultSummary, null, null, runtime.LastErrorCode, runtime.LastErrorMessage, runtime.LastUpdatedAt));
        }
    }

    private async Task ActAsync(object? parameter, Func<Guid, CancellationToken, Task> action)
    {
        var item = Resolve(parameter);
        if (item is null) return;
        await action(item.Id, CancellationToken.None);
    }

    private TaskSnapshotViewModel? Resolve(object? parameter) => parameter as TaskSnapshotViewModel ?? SelectedTask;

    private async Task RetryAsync(object? parameter)
    {
        var item = Resolve(parameter);
        if (item is null) return;
        if (item.State == TaskLifecycleState.Interrupted && _recovery is not null) await _recovery.RetryFailedAsync(item.Id, false, CancellationToken.None);
        else await _engine.RetryAsync(item.Id, CancellationToken.None);
        await InitializeAsync();
    }

    private async Task RollbackAsync(object? parameter)
    {
        var item = Resolve(parameter);
        if (item is null || _recovery is null) return;
        await _recovery.RollbackSafeOutputsAsync(item.Id, CancellationToken.None);
        await InitializeAsync();
    }

    private async Task AbandonAsync(object? parameter)
    {
        var item = Resolve(parameter);
        if (item is null || _recovery is null) return;
        await _recovery.AbandonAsync(item.Id, CancellationToken.None);
        await InitializeAsync();
    }

    private async Task ResolveAttentionAsync(object? parameter)
    {
        var item = Resolve(parameter);
        if (item is null) return;
        try { await _engine.ResolveAttentionAsync(item.Id, "continue", CancellationToken.None); }
        catch (KeyNotFoundException) when (_recovery is not null) { await _recovery.RetryFailedAsync(item.Id, false, CancellationToken.None); }
        await InitializeAsync();
    }

    private void Upsert(TaskProgressSnapshot snapshot)
    {
        var item = Tasks.FirstOrDefault(x => x.Id == snapshot.TaskId);
        if (item is null)
        {
            item = new TaskSnapshotViewModel(snapshot);
            Tasks.Insert(0, item);
        }
        else item.Update(snapshot);
        SelectedTask ??= item;
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(AttentionCount));
        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(HasNoTasks));
        OnPropertyChanged(nameof(VisibleTasks));
        RaiseCommands();
    }

    private void ClearCompleted()
    {
        foreach (var item in Tasks.Where(x => x.IsTerminal).ToArray()) Tasks.Remove(item);
        SelectedTask = Tasks.FirstOrDefault();
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(AttentionCount));
        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(HasNoTasks));
        OnPropertyChanged(nameof(VisibleTasks));
        RaiseCommands();
    }

    private void RaiseCommands()
    {
        PauseCommand.RaiseCanExecuteChanged(); ResumeCommand.RaiseCanExecuteChanged(); CancelCommand.RaiseCanExecuteChanged(); RetryCommand.RaiseCanExecuteChanged(); ResolveAttentionCommand.RaiseCanExecuteChanged(); RollbackCommand.RaiseCanExecuteChanged(); AbandonCommand.RaiseCanExecuteChanged(); ClearCompletedCommand.RaiseCanExecuteChanged();
    }
    private void Dispatch(Action action) { if (_context is null || SynchronizationContext.Current == _context) action(); else _context.Post(_ => action(), null); }
}

public sealed class TaskSnapshotViewModel : ObservableObject
{
    private TaskProgressSnapshot _snapshot;
    public TaskSnapshotViewModel(TaskProgressSnapshot snapshot) => _snapshot = snapshot;
    public Guid Id => _snapshot.TaskId;
    public string DisplayName => _snapshot.DisplayName;
    public TaskLifecycleState State => _snapshot.State;
    public string StateLabel => State switch
    {
        TaskLifecycleState.Pending => "等待开始",
        TaskLifecycleState.Preparing => "准备中",
        TaskLifecycleState.Scanning => "扫描中",
        TaskLifecycleState.Validating => "验证中",
        TaskLifecycleState.WaitingForConfirmation => "等待确认",
        TaskLifecycleState.Running => "处理中",
        TaskLifecycleState.Pausing => "正在暂停",
        TaskLifecycleState.Paused => "已暂停",
        TaskLifecycleState.NeedsAttention => "等待确认",
        TaskLifecycleState.Retrying => "正在重试",
        TaskLifecycleState.Cancelling => "正在取消",
        TaskLifecycleState.Cancelled => "已取消",
        TaskLifecycleState.PartiallyCompleted => "部分完成",
        TaskLifecycleState.Failed => "处理失败",
        TaskLifecycleState.Completed => "已完成",
        TaskLifecycleState.Interrupted => "已中断",
        _ => "未知状态"
    };
    public double Progress => _snapshot.Progress;
    public string ProgressText => $"{Math.Clamp(Progress, 0, 100):0}%";
    public string SourceModuleText => DisplayName switch
    {
        var name when name.Contains("联机", StringComparison.OrdinalIgnoreCase) => "来源：联机拍摄",
        var name when name.Contains("压缩", StringComparison.OrdinalIgnoreCase) => "来源：批量压缩",
        var name when name.Contains("归片", StringComparison.OrdinalIgnoreCase) => "来源：归片工作区",
        var name when name.Contains("复制", StringComparison.OrdinalIgnoreCase) => "来源：文件复制",
        _ => "来源：本地任务"
    };
    public string UpdatedAtText => $"更新 {_snapshot.UpdatedAt.ToLocalTime():MM-dd HH:mm}";
    public string CurrentStep => _snapshot.CurrentStep;
    public string CurrentFile => string.IsNullOrWhiteSpace(_snapshot.CurrentFile) ? string.Empty : Path.GetFileName(_snapshot.CurrentFile);
    public string ResultText => $"成功 {_snapshot.Summary.Succeeded} · 失败 {_snapshot.Summary.Failed} · 跳过 {_snapshot.Summary.Skipped}";
    public string ErrorSummary => _snapshot.ErrorMessage ?? string.Empty;
    public bool IsFailure => State is TaskLifecycleState.Failed or TaskLifecycleState.NeedsAttention or TaskLifecycleState.PartiallyCompleted or TaskLifecycleState.Cancelled;
    public string TaskIdText => $"TaskId: {Id:N}";
    public string DiagnosticText => string.Join(Environment.NewLine, new[] { TaskIdText, $"State: {State}", $"Progress: {ProgressText}", $"Summary: {ResultText}", string.IsNullOrWhiteSpace(_snapshot.ErrorCode) ? null : $"ErrorCode: {_snapshot.ErrorCode}", string.IsNullOrWhiteSpace(_snapshot.ErrorMessage) ? null : $"Error: {_snapshot.ErrorMessage}" }.Where(x => x is not null)!);
    public void CopyDiagnostics() => Clipboard.SetText(DiagnosticText);
    public RelayCommand CopyDiagnosticsCommand => new(_ => CopyDiagnostics());
    public bool IsTerminal => TaskStateMachine.IsTerminal(State);
    public bool CanPause => State is TaskLifecycleState.Running or TaskLifecycleState.Scanning;
    public bool CanResume => State == TaskLifecycleState.Paused;
    public bool CanCancel => !IsTerminal && State != TaskLifecycleState.Cancelling;
    public bool CanRetry => State is TaskLifecycleState.Failed or TaskLifecycleState.PartiallyCompleted or TaskLifecycleState.Cancelled or TaskLifecycleState.Interrupted;
    public bool CanResolveAttention => State == TaskLifecycleState.NeedsAttention;
    public bool CanRollback => State is TaskLifecycleState.Interrupted or TaskLifecycleState.Failed or TaskLifecycleState.PartiallyCompleted;
    public bool CanAbandon => State is TaskLifecycleState.Interrupted or TaskLifecycleState.NeedsAttention;
    public bool HasMoreActions => CanCancel || CanRetry || CanResolveAttention || CanRollback || CanAbandon;
    public void Update(TaskProgressSnapshot snapshot) { _snapshot = snapshot; OnPropertyChanged(string.Empty); }
}

public sealed class TaskDetailsViewModel(TaskSnapshotViewModel task) { public TaskSnapshotViewModel Task { get; } = task; }
public sealed class RecoveryCenterViewModel { public ObservableCollection<TaskSnapshotViewModel> InterruptedTasks { get; } = []; }
public sealed class NotificationCenterViewModel(INotificationCenter center) { public INotificationCenter Center { get; } = center; }
public sealed class DatabaseRecoveryViewModel(IDatabaseRecoveryService recoveryService) { public IDatabaseRecoveryService RecoveryService { get; } = recoveryService; }

public interface INavigationService { string CurrentPage { get; } void Navigate(string page); }
public sealed class NavigationService : ObservableObject, INavigationService
{
    private string _currentPage = "Workbench";
    public string CurrentPage => _currentPage;
    public void Navigate(string page) { if (string.IsNullOrWhiteSpace(page) || _currentPage == page) return; _currentPage = page; OnPropertyChanged(nameof(CurrentPage)); }
}
