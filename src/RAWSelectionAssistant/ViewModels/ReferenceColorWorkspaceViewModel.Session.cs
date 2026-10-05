using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class ReferenceColorWorkspaceViewModel
{
    public AsyncRelayCommand SaveSessionCommand { get; private set; } = null!;
    public AsyncRelayCommand OpenSessionCommand { get; private set; } = null!;
    public string? SessionPath { get; private set; }
    private bool _sessionLoading;
    private void InitializeSessionCommands()
    {
        UndoBatchAdjustmentCommand = new RelayCommand(_ => RestoreBatchAdjustment(false), _ => _batchUndo.Count > 0 && !IsLoading && !IsExporting);
        RedoBatchAdjustmentCommand = new RelayCommand(_ => RestoreBatchAdjustment(true), _ => _batchRedo.Count > 0 && !IsLoading && !IsExporting);
        SaveSessionCommand = new AsyncRelayCommand(async _ =>
        {
            var path = _dialogs.ChooseSaveFile("保存 Color Studio 工作文件", "Color Studio 工作文件|*.ptstudio.json", ".ptstudio.json", "色彩工作文件.ptstudio.json");
            if (path is null) return;
            try { await SaveSessionAsync(path); } catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException) { StatusText = "保存未完成：" + error.Message; }
        }, _ => Targets.Count > 0 && !IsLoading && !IsExporting);
        OpenSessionCommand = new AsyncRelayCommand(async _ =>
        {
            var path = _dialogs.ChooseFiles("打开 Color Studio 工作文件", "Color Studio 工作文件|*.ptstudio.json|JSON|*.json", false).FirstOrDefault();
            if (path is null) return;
            if (Targets.Count > 0 && !_dialogs.Confirm("打开工作文件将替换当前批次。请先保存尚需保留的调整。源文件和素材库标记不会改变。", "打开工作文件")) return;
            try { await LoadSessionAsync(path); } catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException) { StatusText = "工作文件未能打开：" + error.Message; }
        }, _ => !IsLoading && !IsExporting);
    }
    public async Task SaveSessionAsync(string path, CancellationToken token = default)
    {
        if (ActiveTarget is { } active) Editor.CopyCurrentLookTo([active]);
        await FlushMetadataAsync();
        var document = new ColorStudioSessionDocument(Targets.Select(item => new ColorStudioSessionTarget(item.Id, item.Path, item.AssetId, item.IsSelected,
            item.AppliedLookSnapshot, item.ColorAdjustmentStackSnapshot, item.FilmSettingsSnapshot, item.EngineSnapshot, item.ExecutionModeSnapshot,
            item.AssetId is null ? item.Rating : null, item.AssetId is null ? item.ColorLabel : null)).ToArray(), ActiveTarget?.Id, FilterScope, MinimumRating, ColorLabelFilter);
        await ColorStudioSessionStore.SaveAsync(path, document, token);
        SessionPath = Path.GetFullPath(path); OnPropertyChanged(nameof(SessionPath)); StatusText = "工作文件已保存；原片保持只读。";
    }
    public async Task LoadSessionAsync(string path, CancellationToken token = default)
    {
        // Validate the complete document before touching the active edit session.
        var document = await ColorStudioSessionStore.LoadAsync(path, token);
        var restored = document.Targets.Select(item => new ReferenceTargetItem(item.Path, item.Id)
        {
            AssetId = item.AssetId, IsSelected = item.Selected, AppliedLookSnapshot = item.Look, ColorAdjustmentStackSnapshot = item.Stack,
            FilmSettingsSnapshot = item.Film, EngineSnapshot = item.Engine, ExecutionModeSnapshot = item.ExecutionMode,
            Rating = item.AssetId is null ? item.SessionRating ?? 0 : 0, ColorLabel = item.AssetId is null ? item.SessionColorLabel : null
        }).ToArray();
        _sessionLoading = true; OnPropertyChanged(nameof(IsLoading)); RefreshSyncAvailability();
        try
        {
        Editor.StopProcessing(); _activationCancellation?.Cancel(); Interlocked.Increment(ref _activationRevision);
        await FlushMetadataAsync(); ActiveTarget = null; Editor.ClearTargetHistories(); Targets.Clear(); _batchUndo.Clear(); _batchRedo.Clear(); RaiseBatchHistory();
        foreach (var target in restored) Targets.Add(target);
        FilterScope = document.FilterScope; MinimumRating = document.MinimumRating; ColorLabelFilter = document.ColorLabelFilter;
        var missing = 0;
        foreach (var target in restored)
        {
            token.ThrowIfCancellationRequested();
            if (!File.Exists(target.Path)) { target.Status = ReferenceTargetStatus.Failed; target.Error = "源文件暂不可用，调整已保留"; missing++; continue; }
            await LoadThumbnailAsync(target, token);
        }
        await FlushMetadataAsync();
        var active = restored.FirstOrDefault(x => x.Id == document.ActiveTargetId && File.Exists(x.Path)) ?? restored.FirstOrDefault(x => File.Exists(x.Path));
        if (active is not null) await ActivateTargetAsync(active);
        else { TargetImage = null; TargetName = "尚未选择待调色照片"; Editor.ApplyTargetSnapshot(null,null,null,false); await Editor.SetSourceAsync(null, null, token); }
        RefreshFilmstrip(); SessionPath = Path.GetFullPath(path); OnPropertyChanged(nameof(SessionPath));
        StatusText = $"已恢复 {restored.Length} 张照片的工作文件" + (missing > 0 ? $"；{missing} 个源文件暂不可用，调整已保留。" : "。");
        }
        finally { _sessionLoading = false; OnPropertyChanged(nameof(IsLoading)); RefreshSyncAvailability(); }
    }
}
