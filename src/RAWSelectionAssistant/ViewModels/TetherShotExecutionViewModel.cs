using System.Collections.ObjectModel;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed class TetherShotExecutionViewModel : ObservableObject
{
    private readonly ProjectShotStore _store;
    private readonly ProjectShotExecution _execution = new();
    private Guid? _projectId;
    private ProjectShot? _current;
    private ShotReferenceKind _selectedKind = ShotReferenceKind.Lighting;
    private ProjectShotReference? _selectedReference;
    private string _statusText = "当前项目还没有可执行的拍摄参考。";
    public Func<Guid?, Task>? EffectiveLookChanged { get; set; }

    public TetherShotExecutionViewModel(ProjectShotStore? store = null)
    {
        _store = store ?? new(Path.Combine(AppDataPaths.DataDirectory, "ProjectShots"));
        PreviousShotCommand = new AsyncRelayCommand(_ => MoveAsync(-1), _ => Current is not null && CurrentIndex > 0);
        NextShotCommand = new AsyncRelayCommand(_ => MoveAsync(1), _ => Current is not null && CurrentIndex < Shots.Count - 1);
        MarkInProgressCommand = new AsyncRelayCommand(_ => SetShotStatusAsync(ProjectShotStatus.InProgress), _ => Current is not null);
        MarkShotCompletedCommand = new AsyncRelayCommand(_ => SetShotStatusAsync(ProjectShotStatus.Completed), _ => Current is not null);
        MarkPoseCompletedCommand = new AsyncRelayCommand(value => SetPoseCompletedAsync(value as ProjectShotReference), value => value is ProjectShotReference { Kind: ShotReferenceKind.Pose });
        TogglePinCommand = new AsyncRelayCommand(value => TogglePinAsync(value as ProjectShotReference), value => value is ProjectShotReference);
        SelectReferenceKindCommand = new RelayCommand(value => { if (value is ShotReferenceKind kind) SelectedKind = kind; });
    }

    public ObservableCollection<ProjectShot> Shots { get; } = [];
    public ObservableCollection<ProjectShotReference> VisibleReferences { get; } = [];
    public IReadOnlyList<ShotReferenceKind> ReferenceKinds { get; } = [ShotReferenceKind.Lighting, ShotReferenceKind.Pose, ShotReferenceKind.Storyboard, ShotReferenceKind.Styling];
    public ProjectShot? Current { get => _current; private set { if (SetProperty(ref _current, value)) { OnPropertyChanged(nameof(ShotPositionText)); OnPropertyChanged(nameof(Notes)); RefreshReferences(); RaiseCommands(); } } }
    public ProjectShotReference? SelectedReference { get => _selectedReference; set => SetProperty(ref _selectedReference, value); }
    public ShotReferenceKind SelectedKind { get => _selectedKind; set { if (SetProperty(ref _selectedKind, value)) RefreshReferences(); } }
    public string ShotPositionText => Current is null ? "无拍摄 Shot" : $"Shot {CurrentIndex + 1:00} / {Shots.Count:00} · {Current.Name}";
    public string? Notes => Current?.Notes;
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public int CurrentIndex => Current is null ? -1 : Shots.ToList().FindIndex(shot => shot.ShotId == Current.ShotId);
    public AsyncRelayCommand PreviousShotCommand { get; }
    public AsyncRelayCommand NextShotCommand { get; }
    public AsyncRelayCommand MarkInProgressCommand { get; }
    public AsyncRelayCommand MarkShotCompletedCommand { get; }
    public AsyncRelayCommand MarkPoseCompletedCommand { get; }
    public AsyncRelayCommand TogglePinCommand { get; }
    public RelayCommand SelectReferenceKindCommand { get; }

    public async Task LoadAsync(Guid? projectId, CancellationToken token = default)
    {
        _projectId = projectId; Shots.Clear(); _execution.Load([]); Current = null;
        if (projectId is null) { StatusText = "当前文件夹监看没有关联项目，拍摄参考不可用。"; return; }
        var catalog = await _store.LoadAsync(projectId.Value, token);
        foreach (var shot in catalog.Shots) Shots.Add(shot);
        _execution.Load(catalog.Shots); Current = _execution.Current;
        StatusText = Current is null ? "当前项目还没有可执行的拍摄参考。" : "拍摄参考只读取引用，不复制或修改原图。";
        await NotifyLookAsync();
    }

    private async Task MoveAsync(int delta) { Current = _execution.Move(delta); if (Current is not null && Current.Status == ProjectShotStatus.NotStarted) Current = _execution.SetStatus(ProjectShotStatus.InProgress); await SaveCurrentAsync(); await NotifyLookAsync(); }
    private async Task SetShotStatusAsync(ProjectShotStatus status) { Current = _execution.SetStatus(status); await SaveCurrentAsync(); }
    private async Task SetPoseCompletedAsync(ProjectShotReference? reference) { if (reference is null) return; Current = _execution.SetPoseStatus(reference.ReferenceId, PoseExecutionStatus.Completed, advance: true); await SaveCurrentAsync(); }
    private async Task TogglePinAsync(ProjectShotReference? reference) { if (reference is null) return; Current = _execution.SetPinned(reference.ReferenceId, !reference.IsPinned); await SaveCurrentAsync(); }
    private async Task SaveCurrentAsync() { if (Current is not null) { await _store.SaveAsync(Current); ReplaceCollection(Current); } }
    private Task NotifyLookAsync() => EffectiveLookChanged?.Invoke(Current?.ReferenceLookId) ?? Task.CompletedTask;
    private void ReplaceCollection(ProjectShot shot) { var index = Shots.ToList().FindIndex(item => item.ShotId == shot.ShotId); if (index >= 0) Shots[index] = shot; RefreshReferences(); RaiseCommands(); }
    private void RefreshReferences() { VisibleReferences.Clear(); if (Current is not null) foreach (var reference in Current.References.Where(item => item.Kind == SelectedKind)) VisibleReferences.Add(reference); SelectedReference = VisibleReferences.FirstOrDefault(item => item.PoseStatus == PoseExecutionStatus.Current) ?? VisibleReferences.FirstOrDefault(); }
    private void RaiseCommands() { PreviousShotCommand.RaiseCanExecuteChanged(); NextShotCommand.RaiseCanExecuteChanged(); MarkInProgressCommand.RaiseCanExecuteChanged(); MarkShotCompletedCommand.RaiseCanExecuteChanged(); }
}
