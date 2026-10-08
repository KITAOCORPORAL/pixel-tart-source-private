using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Utilities;
using RAWSelectionAssistant.Services;

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
            var path = _dialogs.ChooseSaveFile(StudioLocalizationService.Current["SessionSaveTitle"], StudioLocalizationService.Current["SessionFilter"] + "|*.ptstudio.json", ".ptstudio.json", "ColorStudio.ptstudio.json");
            if (path is null) return;
            try { await SaveSessionAsync(path); } catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException) { StatusText = StudioLocalizationService.Current.Format("SessionSaveFailed", error.Message); }
        }, _ => Targets.Count > 0 && !IsLoading && !IsExporting);
        OpenSessionCommand = new AsyncRelayCommand(async _ =>
        {
            var path = _dialogs.ChooseFiles(StudioLocalizationService.Current["SessionOpenTitle"], StudioLocalizationService.Current["SessionFilter"] + "|*.ptstudio.json|JSON|*.json", false).FirstOrDefault();
            if (path is null) return;
            if (Targets.Count > 0 && !_dialogs.Confirm(StudioLocalizationService.Current["SessionReplace"], StudioLocalizationService.Current["SessionOpenTitle"])) return;
            try { await LoadSessionAsync(path); } catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException) { StatusText = StudioLocalizationService.Current.Format("SessionOpenFailed", error.Message); }
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
        SessionPath = Path.GetFullPath(path); OnPropertyChanged(nameof(SessionPath)); StatusText = "SessionSaved";
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
        SetStatus("SessionRestored", restored.Length, missing);
        }
        finally { _sessionLoading = false; OnPropertyChanged(nameof(IsLoading)); RefreshSyncAvailability(); }
    }
}
