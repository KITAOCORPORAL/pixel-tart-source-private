using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class ReferenceColorWorkspaceViewModel
{
    private sealed record TargetAdjustment(ReferenceLook? Look, ColorAdjustmentStack? Stack, PixelTartFilmSettings? Film, ColorStudioMatchEngine Engine, MatchV4ExecutionMode Mode);
    private readonly Stack<Dictionary<Guid, TargetAdjustment>> _batchUndo = new();
    private readonly Stack<Dictionary<Guid, TargetAdjustment>> _batchRedo = new();
    private bool _applyingActiveSnapshot;
    public RelayCommand UndoBatchAdjustmentCommand { get; private set; } = null!;
    public RelayCommand RedoBatchAdjustmentCommand { get; private set; } = null!;
    private static TargetAdjustment CaptureAdjustment(ReferenceTargetItem item) => new(
        item.AppliedLookSnapshot is { } look ? look with { ReferenceSources = look.ReferenceSources.Select(x => x with { }).ToArray() } : null,
        item.ColorAdjustmentStackSnapshot?.DeepClone(), item.FilmSettingsSnapshot is { } film ? film with { } : null, item.EngineSnapshot, item.ExecutionModeSnapshot);
    private void RememberBatchAdjustment(IEnumerable<ReferenceTargetItem> items)
    {
        var targets = items.ToArray();
        if (targets.Length == 0) return;
        if (ActiveTarget is { } active && targets.Contains(active)) Editor.CopyCurrentLookTo([active]);
        _batchUndo.Push(targets.ToDictionary(x => x.Id, CaptureAdjustment)); _batchRedo.Clear(); RaiseBatchHistory();
    }
    private void RestoreBatchAdjustment(bool redo)
    {
        var source = redo ? _batchRedo : _batchUndo; var destination = redo ? _batchUndo : _batchRedo;
        if (source.Count == 0) return;
        var snapshot = source.Pop(); var items = Targets.Where(x => snapshot.ContainsKey(x.Id)).ToArray();
        if (ActiveTarget is { } active && items.Contains(active)) Editor.CopyCurrentLookTo([active]);
        destination.Push(items.ToDictionary(x => x.Id, CaptureAdjustment));
        foreach (var item in items)
        {
            var saved = snapshot[item.Id]; item.AppliedLookSnapshot = saved.Look; item.ColorAdjustmentStackSnapshot = saved.Stack?.DeepClone();
            item.FilmSettingsSnapshot = saved.Film; item.EngineSnapshot = saved.Engine; item.ExecutionModeSnapshot = saved.Mode;
            item.Status = ReferenceTargetStatus.Adjusted;
        }
        RefreshActiveSnapshot(items); RaiseBatchHistory();
        _ = ShowSyncFeedbackAsync($"已{(redo ? "重做" : "撤销")} {items.Length} 张照片的批次调整；标记和照片归属保持不变。");
    }
    private void RaiseBatchHistory() { UndoBatchAdjustmentCommand?.RaiseCanExecuteChanged(); RedoBatchAdjustmentCommand?.RaiseCanExecuteChanged(); }
    private void RefreshActiveSnapshot(IEnumerable<ReferenceTargetItem> targets)
    {
        if (ActiveTarget is not { } active || !targets.Contains(active)) return;
        _applyingActiveSnapshot = true;
        try
        {
            Editor.RestoreTargetEngine(active.EngineSnapshot, active.ExecutionModeSnapshot);
            Editor.ApplyTargetSnapshot(active.AppliedLookSnapshot, active.FilmSettingsSnapshot, active.ColorAdjustmentStackSnapshot, render: true);
        }
        finally { _applyingActiveSnapshot = false; }
    }
    private void SyncCurrentTo(IReadOnlyList<ReferenceTargetItem> targets)
    {
        if (ActiveTarget is null || targets.Count == 0) return;
        RememberBatchAdjustment(targets); Editor.CopyCurrentLookTo(targets);
        _ = ShowSyncFeedbackAsync($"已从 {ActiveTarget.FileName} 同步完整调整到 {targets.Count} 张可见所选照片；评分、色标和素材关系未改变。");
    }
}
