using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class TetherReferenceModeViewModel
{
    private Guid? _historyTarget;
    private readonly Dictionary<Guid, TargetHistory> _targetHistories = [];
    private sealed record TargetHistory(ColorAdjustmentStack[] Undo, ColorAdjustmentStack[] Redo, Guid?[] UndoSelections,
        Guid?[] RedoSelections, Guid? SelectedNode, ColorAdjustmentStack? PersistedStack, ColorStudioSchemeV2? AppliedScheme, string SchemeName);
    private static ColorAdjustmentStack? CloneHistoryStack(ColorAdjustmentStack? stack) => stack is null ? null : stack.Nodes.Count == 0 ? stack with { Nodes = [] } : stack.DeepClone();
    private void CaptureTargetHistory()
    {
        CommitEditTransaction();
        if (_historyTarget is not Guid id) return;
        _targetHistories[id] = new(_undoStacks.Select(x => CloneHistoryStack(x)!).ToArray(), _redoStacks.Select(x => CloneHistoryStack(x)!).ToArray(),
            _undoSelections.ToArray(), _redoSelections.ToArray(), _selectedAdjustmentNodeId, CloneHistoryStack(_persistedStack), _appliedColorScheme, ColorSchemeName);
    }
    private void BeginTargetHistory(Guid target)
    {
        if (_historyTarget == target) return;
        CaptureTargetHistory(); _historyTarget = target;
        _undoStacks.Clear(); _redoStacks.Clear(); _undoSelections.Clear(); _redoSelections.Clear();
        _editTransactionBefore = null; _editTransactionSelection = null; _persistedStack = null; _appliedColorScheme = null; ColorSchemeName = "新色彩方案";
    }
    private void RestoreTargetHistory(Guid target)
    {
        _undoStacks.Clear(); _redoStacks.Clear(); _undoSelections.Clear(); _redoSelections.Clear();
        if (_targetHistories.TryGetValue(target, out var history))
        {
            foreach (var item in history.Undo.Reverse()) _undoStacks.Push(CloneHistoryStack(item)!);
            foreach (var item in history.Redo.Reverse()) _redoStacks.Push(CloneHistoryStack(item)!);
            foreach (var item in history.UndoSelections.Reverse()) _undoSelections.Push(item);
            foreach (var item in history.RedoSelections.Reverse()) _redoSelections.Push(item);
            _selectedAdjustmentNodeId = AdjustmentStack.Nodes.Any(x => x.Id == history.SelectedNode) ? history.SelectedNode : AdjustmentStack.Nodes.FirstOrDefault()?.Id;
            _persistedStack = CloneHistoryStack(history.PersistedStack); _appliedColorScheme = history.AppliedScheme;
            ColorSchemeName = history.SchemeName;
        }
        OnPropertyChanged(nameof(SelectedAdjustmentNode)); RaiseAdjustmentCommands(); NotifySchemeState();
    }
    public void ClearTargetHistories()
    {
        _historyTarget = null; _targetHistories.Clear(); _undoStacks.Clear(); _redoStacks.Clear(); _undoSelections.Clear(); _redoSelections.Clear();
        _editTransactionBefore = null; _editTransactionSelection = null; _persistedStack = null; _appliedColorScheme = null; ColorSchemeName = "新色彩方案"; RaiseAdjustmentCommands(); NotifySchemeState();
    }
    public void RestoreTargetEngine(ColorStudioMatchEngine engine, MatchV4ExecutionMode executionMode)
    {
        _matchEngine = engine; _matchV4ExecutionMode = executionMode; _matchV4Session = null; _matchV4SessionKey = null;
        OnPropertyChanged(nameof(MatchEngine)); OnPropertyChanged(nameof(MatchV4ExecutionMode)); OnPropertyChanged(nameof(IsMatchV4Beta)); OnPropertyChanged(nameof(MatchV4Status));
    }
    public static bool SupportsExperimentalV4Stack(ColorAdjustmentStack? stack, PixelTartFilmSettings? film) =>
        film?.Enabled != true && (stack is null || (stack.Nodes.All(node => !node.Enabled || node.Type == ColorStudioNodeType.ReferenceMatch)
            && stack.Nodes.Count(node => node.Enabled && node.Type == ColorStudioNodeType.ReferenceMatch) <= 1));
    public static ReferenceLook? ResolveExperimentalV4Look(ReferenceLook? look, ColorAdjustmentStack? stack)
    {
        if (look is null || stack is null || stack.Nodes.Count == 0) return look;
        var node = stack.Nodes.FirstOrDefault(x => x.Enabled && x.Type == ColorStudioNodeType.ReferenceMatch);
        if (node is null) return null;
        double Value(string key, double fallback) => node.NumericParameters.TryGetValue(key, out var value) ? value : fallback;
        var p = look.Parameters;
        return look with { Parameters = p with
        {
            MatchStrength = Value("match_strength", p.MatchStrength), KeepOriginalTone = Value("keep_original_tone", p.KeepOriginalTone ? 1 : 0) >= .5,
            SkinProtection = Value("skin_protection", p.SkinProtection), HighlightProtection = Value("highlight_protection", p.HighlightProtection),
            NeutralProtection = Value("neutral_protection", p.NeutralProtection)
        } };
    }
}
