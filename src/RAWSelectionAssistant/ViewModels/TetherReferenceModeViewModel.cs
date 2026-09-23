using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed class TetherReferenceModeViewModel : ObservableObject, IDisposable
{
    private readonly ReferenceLookStore _store;
    private readonly IDialogService? _dialogs;
    private readonly IReferenceRenderBackend _preview;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _render;
    private BitmapSource? _source;
    private BitmapSource? _interactiveSource;
    private Guid? _assetId;
    private long _revision;
    private ReferenceLook? _selectedLook;
    private ReferenceLook? _persistedLook;
    private BitmapSource? _matchedImage;
    private bool _enabled;
    private bool _applyToFollowing;
    private bool _advancedExpanded;
    private string _viewMode = "左右对比";
    private double _splitPosition = .5;
    private string _statusText = "选择项目色彩方案后可进行现场监看仿色。";
    private bool _originalHeld;
    private bool _sourcePickerOpen;
    private ReferenceSourceCategory _selectedSourceCategory;
    private Guid? _projectId;
    private Guid? _projectDefaultLookId;
    private Guid? _sessionLookId;
    private readonly bool _allowReferenceManagement;
    private int _busyOperations;
    private bool _refreshingLookChoices;
    private bool _hasError;
    private PixelTartFilmSettings _filmSettings = new();
    private string _workspaceMode = "简洁";
    private string _workspaceSection = "仿色";
    private bool _focusView;
    private bool _contextRailOpen = true;
    public enum ProcessingState { Idle, Preparing, Analyzing, Matching, RenderingPreview, RenderingHighQuality, ApplyingFilm, BatchProcessing, Exporting, Cancelling, Cancelled, Failed }
    private ProcessingState _processingState;
    public ProcessingState State { get => _processingState; private set { if (SetProperty(ref _processingState, value)) { OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(IsCancelling)); } } }
    public bool IsCancelling => State == ProcessingState.Cancelling;
    public bool HasError { get => _hasError; private set => SetProperty(ref _hasError, value); }
    public bool IsBusy => _busyOperations > 0 || State is not ProcessingState.Idle and not ProcessingState.Cancelled and not ProcessingState.Failed;
    private void BeginBusy() { _busyOperations++; State = ProcessingState.RenderingHighQuality; }
    private void EndBusy() { _busyOperations = Math.Max(0, _busyOperations - 1); if (_busyOperations == 0 && State is not ProcessingState.Failed) State = ProcessingState.Idle; }
    public void StopProcessing()
    {
        if (!IsBusy) return;
        State = ProcessingState.Cancelling; StatusText = "正在停止…";
        _render?.Cancel();
        Interlocked.Increment(ref _revision);
    }
    public void CopyCurrentLookTo(IEnumerable<ReferenceTargetItem> targets)
    {
        var snapshot = SelectedLook?.Normalize();
        if (snapshot is null) return;
        foreach (var target in targets)
        {
            target.AppliedLookSnapshot = snapshot with { ReferenceSources = snapshot.ReferenceSources.ToArray() };
            target.FilmSettingsSnapshot = FilmSettings with { };
            target.Status = ReferenceTargetStatus.Synced;
        }
    }
    public void ApplyTargetSnapshot(ReferenceLook? look, PixelTartFilmSettings? film)
    {
        if (look is not null)
        {
            _selectedLook = look with { ReferenceSources = look.ReferenceSources.Select(source => source with { }).ToArray() };
            _persistedLook = _selectedLook;
            CopyParameters(_selectedLook.Parameters);
            RefreshReferenceSources();
            OnPropertyChanged(nameof(SelectedLook));
            OnPropertyChanged(nameof(CurrentLookText));
        }
        else SelectedLook = null;
        CopyFilm(film is not null ? film with { } : look?.Film ?? new PixelTartFilmSettings());
        _ = DebouncedRenderAsync();
    }
    public async Task<BitmapSource> ProcessForExportAsync(string path, ReferenceLook? snapshot, PixelTartFilmSettings? film, CancellationToken token)
    {
        var source = await Task.Run(() =>
        {
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(path); image.EndInit(); image.Freeze(); return image;
        }, token);
        // Batch export is driven by each target's frozen snapshot, never by the
        // currently active editor (which may belong to a different target).
        var look = snapshot;
        if (look is null) return source;
        var rendered = await _preview.RenderWithResultAsync(source, look, token);
        var output = rendered.Image;
        var settings = film ?? look.Film ?? new PixelTartFilmSettings();
        if (settings.Enabled) output = await _preview.ApplyFilmAsync(output, settings, token);
        if (PostProcessor is not null) output = await PostProcessor(output, token);
        return output;
    }
    public event EventHandler? FullEditorRequested;
    public Func<BitmapSource, CancellationToken, Task<BitmapSource>>? PostProcessor { get; set; }
    public IReadOnlyList<PixelTartFilmProfile> FilmProfiles => PixelTartFilmProfiles.All;
    public IReadOnlyList<PixelTartFilmTextureOption> FilmTextures => PixelTartFilmTextures.All;
    public PixelTartFilmSettings FilmSettings { get => _filmSettings; private set => SetProperty(ref _filmSettings, value); }
    public bool FilmEnabled { get => FilmSettings.Enabled; set => SetFilm(FilmSettings with { Enabled = value }); }
    public string FilmProfileId { get => FilmSettings.ProfileId; set => SetFilm(FilmSettings with { ProfileId = value }); }
    public double FilmProfileAmount { get => FilmSettings.ProfileAmount; set => SetFilm(FilmSettings with { ProfileAmount = value }); }
    public double FilmGrainAmount { get => FilmSettings.GrainAmount; set => SetFilm(FilmSettings with { GrainAmount = value }); }
    public double FilmGrainSize { get => FilmSettings.GrainSize; set => SetFilm(FilmSettings with { GrainSize = value }); }
    public double FilmHalationAmount { get => FilmSettings.HalationAmount; set => SetFilm(FilmSettings with { HalationAmount = value }); }
    public double FilmBloomAmount { get => FilmSettings.BloomAmount; set => SetFilm(FilmSettings with { BloomAmount = value }); }
    public double FilmVignetteAmount { get => FilmSettings.VignetteAmount; set => SetFilm(FilmSettings with { VignetteAmount = value }); }
    public double FilmSurfaceAmount { get => FilmSettings.SurfaceAmount; set => SetFilm(FilmSettings with { SurfaceAmount = value }); }
    public string FilmTextureId { get => FilmSettings.TextureId; set => SetFilm(FilmSettings with { TextureId = value }); }
    public double FilmTextureAmount { get => FilmSettings.TextureAmount; set => SetFilm(FilmSettings with { TextureAmount = value }); }
    public int FilmSeed { get => FilmSettings.Seed; set => SetFilm(FilmSettings with { Seed = value }); }
    public RelayCommand RegenerateFilmCommand { get; }
    public RelayCommand ToggleFocusViewCommand { get; }
    public RelayCommand ToggleContextRailCommand { get; }
    public IReadOnlyList<string> WorkspaceModes { get; } = ["简洁", "专业"];
    public IReadOnlyList<string> WorkspaceSections { get; } = ["调色", "仿色", "预设", "胶片", "输出"];
    public string WorkspaceSection { get => _workspaceSection; set => SetProperty(ref _workspaceSection, value); }
    public string WorkspaceMode { get => _workspaceMode; set { if (SetProperty(ref _workspaceMode, value)) { OnPropertyChanged(nameof(IsSimpleMode)); OnPropertyChanged(nameof(IsProMode)); } } }
    public bool IsSimpleMode => WorkspaceMode == "简洁";
    public bool IsProMode => WorkspaceMode == "专业";
    public bool FocusView { get => _focusView; set { if (SetProperty(ref _focusView, value)) { OnPropertyChanged(nameof(IsContextVisible)); OnPropertyChanged(nameof(IsLeftRailVisible)); } } }
    public bool ContextRailOpen { get => _contextRailOpen; set { if (SetProperty(ref _contextRailOpen, value)) OnPropertyChanged(nameof(IsContextVisible)); } }
    public bool IsContextVisible => !FocusView && ContextRailOpen;
    public bool IsLeftRailVisible => !FocusView;

    public TetherReferenceModeViewModel(ReferenceLookStore? store = null, IDialogService? dialogs = null, bool allowReferenceManagement = false,
        IReferenceRenderBackend? renderBackend = null)
    {
        _store = store ?? new(Path.Combine(AppDataPaths.DataDirectory, "ProjectVisuals"));
        _dialogs = dialogs;
        _preview = renderBackend ?? new ReferenceLookPreviewService();
        _allowReferenceManagement = allowReferenceManagement;
        ApplyCommand = new AsyncRelayCommand(_ => EnableAndRenderAsync(), _ => SelectedLook is not null && _source is not null);
        ReloadCommand = new AsyncRelayCommand(_ => LoadAsync());
        ExportCubeCommand = new AsyncRelayCommand(_ => ExportCubeAsync(), _ => SelectedLook is not null && _source is not null && _dialogs is not null && _allowReferenceManagement);
        SaveCurrentAdjustmentCommand = new AsyncRelayCommand(_ => SaveCurrentAdjustmentAsync(), _ => HasSessionAdjustment && SelectedLook is not null && _dialogs is IReferenceAdjustmentDialogService);
        RestoreSchemeCommand = new RelayCommand(_ => RestoreScheme(), _ => HasSessionAdjustment);
        OpenFullEditorCommand = new RelayCommand(_ => FullEditorRequested?.Invoke(this, EventArgs.Empty));
        ToggleSourcePickerCommand = new RelayCommand(_ => IsSourcePickerOpen = !IsSourcePickerOpen);
        SelectSourceCategoryCommand = new RelayCommand(value => { if (value is ReferenceSourceCategory category) SelectedSourceCategory = category; });
        ImportExternalReferenceCommand = new AsyncRelayCommand(_ => ImportExternalReferenceAsync(), _ => _dialogs is not null && _allowReferenceManagement);
        RemoveReferenceCommand = new AsyncRelayCommand(value => UpdateReferencesAsync(value as ReferenceSourceWeightViewModel, ReferenceEdit.Remove), value => _allowReferenceManagement && value is ReferenceSourceWeightViewModel && ReferenceSources.Count > 1);
        MoveReferenceUpCommand = new AsyncRelayCommand(value => UpdateReferencesAsync(value as ReferenceSourceWeightViewModel, ReferenceEdit.Up), value => _allowReferenceManagement && value is ReferenceSourceWeightViewModel item && ReferenceSources.IndexOf(item) > 0);
        MoveReferenceDownCommand = new AsyncRelayCommand(value => UpdateReferencesAsync(value as ReferenceSourceWeightViewModel, ReferenceEdit.Down), value => _allowReferenceManagement && value is ReferenceSourceWeightViewModel item && ReferenceSources.IndexOf(item) >= 0 && ReferenceSources.IndexOf(item) < ReferenceSources.Count - 1);
        SourceCategories = [new("项目色彩方案", null), new("灵感板", "Board"), new("自由画布", "Canvas"), new("素材库", "Asset"), new("最近使用", "Recent"), new("导入参考图", "External")];
        _selectedSourceCategory = SourceCategories[0];
        RegenerateFilmCommand = new RelayCommand(_ => SetFilm(FilmSettings with { Seed = Random.Shared.Next(0, int.MaxValue) }));
        ToggleFocusViewCommand = new RelayCommand(_ => FocusView = !FocusView);
        ToggleContextRailCommand = new RelayCommand(_ => ContextRailOpen = !ContextRailOpen);
    }
    public ObservableCollection<ReferenceLook> Looks { get; } = [];
    public ObservableCollection<ReferenceLook> SourceChoices { get; } = [];
    public ObservableCollection<ReferenceSourceWeightViewModel> ReferenceSources { get; } = [];
    public IReadOnlyList<ReferenceSourceCategory> SourceCategories { get; }
    public IReadOnlyList<string> ViewModes { get; } = ["原片", "仿色结果", "左右对比", "并排对比"];
    public AsyncRelayCommand ApplyCommand { get; }
    public AsyncRelayCommand ReloadCommand { get; }
    public AsyncRelayCommand ExportCubeCommand { get; }
    public AsyncRelayCommand SaveCurrentAdjustmentCommand { get; }
    public RelayCommand RestoreSchemeCommand { get; }
    public RelayCommand OpenFullEditorCommand { get; }
    public bool IsReferenceManagementEnabled => _allowReferenceManagement;
    public RelayCommand ToggleSourcePickerCommand { get; }
    public RelayCommand SelectSourceCategoryCommand { get; }
    public AsyncRelayCommand ImportExternalReferenceCommand { get; }
    public AsyncRelayCommand RemoveReferenceCommand { get; }
    public AsyncRelayCommand MoveReferenceUpCommand { get; }
    public AsyncRelayCommand MoveReferenceDownCommand { get; }
    public bool IsSourcePickerOpen { get => _sourcePickerOpen; set => SetProperty(ref _sourcePickerOpen, value); }
    public ReferenceSourceCategory SelectedSourceCategory { get => _selectedSourceCategory; set { if (SetProperty(ref _selectedSourceCategory, value)) RefreshSourceChoices(); } }
    public ReferenceLook? SelectedLook { get => _selectedLook; set { if (_refreshingLookChoices && value is null) return; if (SetProperty(ref _selectedLook, value)) { _persistedLook = value; _sessionLookId = value?.ReferenceLookId; OnPropertyChanged(nameof(CurrentLookText)); CopyParameters(value?.Parameters ?? new()); CopyFilm(value?.Film ?? new()); RefreshReferenceSources(); ApplyCommand.RaiseCanExecuteChanged(); ExportCubeCommand.RaiseCanExecuteChanged(); SaveCurrentAdjustmentCommand.RaiseCanExecuteChanged(); RestoreSchemeCommand.RaiseCanExecuteChanged(); IsSourcePickerOpen = false; _ = RenderAsync(); } } }
    public string CurrentLookText => SelectedLook?.Name ?? "未选择色彩方案";
    public BitmapSource? MatchedImage { get => _matchedImage; private set => SetProperty(ref _matchedImage, value); }
    public BitmapSource? SourceImage => _source;
    public bool HasSessionAdjustment => _persistedLook is not null && _selectedLook is not null &&
        (!_selectedLook.Parameters.Equals(_persistedLook.Parameters) || !FilmSettings.Equals(_persistedLook.Film ?? new()));
    public string SessionAdjustmentText => HasSessionAdjustment ? "本次拍摄已调整" : string.Empty;
    public bool Enabled { get => _enabled; set { if (SetProperty(ref _enabled, value)) _ = RenderAsync(); } }
    public bool ApplyToFollowing { get => _applyToFollowing; set => SetProperty(ref _applyToFollowing, value); }
    public bool AdvancedExpanded { get => _advancedExpanded; set => SetProperty(ref _advancedExpanded, value); }
    public string ViewMode { get => _viewMode; set { if (SetProperty(ref _viewMode, value)) RaiseViewProperties(); } }
    public double SplitPosition { get => _splitPosition; set { if (SetProperty(ref _splitPosition, Math.Clamp(value, 0, 1))) { OnPropertyChanged(nameof(SplitLeft)); OnPropertyChanged(nameof(SplitRight)); } } }
    public System.Windows.GridLength SplitLeft => new(SplitPosition, System.Windows.GridUnitType.Star);
    public System.Windows.GridLength SplitRight => new(1 - SplitPosition, System.Windows.GridUnitType.Star);
    public bool ShowOriginal => _originalHeld || !Enabled || MatchedImage is null || ViewMode == "原片";
    public bool ShowMatched => !_originalHeld && Enabled && MatchedImage is not null && (ViewMode == "仿色" || ViewMode == "仿色结果");
    public bool ShowSplit => !_originalHeld && Enabled && MatchedImage is not null && ViewMode == "左右对比";
    public bool ShowSideBySide => !_originalHeld && Enabled && MatchedImage is not null && ViewMode == "并排对比";
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    public double MatchStrength { get => SelectedLook?.Parameters.MatchStrength ?? _match; set => SetParameter(value, p => p with { MatchStrength = value }, ref _match); }
    public double ToneStrength { get => SelectedLook?.Parameters.ToneStrength ?? _tone; set => SetParameter(value, p => p with { ToneStrength = value }, ref _tone); }
    public double ColorStrength { get => SelectedLook?.Parameters.ColorStrength ?? _color; set => SetParameter(value, p => p with { ColorStrength = value }, ref _color); }
    public double ContrastStrength { get => SelectedLook?.Parameters.ContrastStrength ?? _contrast; set => SetParameter(value, p => p with { ContrastStrength = value }, ref _contrast); }
    public double SaturationStrength { get => SelectedLook?.Parameters.SaturationStrength ?? _saturation; set => SetParameter(value, p => p with { SaturationStrength = value }, ref _saturation); }
    public double SkinProtection { get => SelectedLook?.Parameters.SkinProtection ?? _skin; set => SetParameter(value, p => p with { SkinProtection = value }, ref _skin); }
    public double HighlightProtection { get => SelectedLook?.Parameters.HighlightProtection ?? _highlight; set => SetParameter(value, p => p with { HighlightProtection = value }, ref _highlight); }
    public double NeutralProtection { get => SelectedLook?.Parameters.NeutralProtection ?? _neutral; set => SetParameter(value, p => p with { NeutralProtection = value }, ref _neutral); }
    public bool KeepOriginalTone { get => SelectedLook?.Parameters.KeepOriginalTone ?? _keepOriginalTone; set { _keepOriginalTone = value; if (SelectedLook is not null) { _selectedLook = SelectedLook with { Parameters = SelectedLook.Parameters with { KeepOriginalTone = value }, UpdatedAt = DateTimeOffset.UtcNow }; OnPropertyChanged(nameof(SelectedLook)); OnPropertyChanged(nameof(HasSessionAdjustment)); OnPropertyChanged(nameof(SessionAdjustmentText)); SaveCurrentAdjustmentCommand.RaiseCanExecuteChanged(); RestoreSchemeCommand.RaiseCanExecuteChanged(); } OnPropertyChanged(); _ = DebouncedRenderAsync(); } }
    private double _match=100,_tone=50,_color=70,_contrast=50,_saturation=50,_skin=60,_highlight=70,_neutral=65;
    private bool _keepOriginalTone;

    public async Task LoadAsync(CancellationToken token = default)
    {
        var selected = SelectedLook?.ReferenceLookId ?? _sessionLookId;
        ReferenceLookCatalog catalog;
        try { catalog = await _store.LoadAsync(token); }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            new RAWSelectionAssistant.Core.Services.FileLogService().Error("色彩方案未能加载；保留原文件并恢复空方案列表。", exception);
            StatusText = "部分色彩方案未能加载，原文件已保留。可继续使用其他工作区。";
            Looks.Clear(); SelectedLook = null; RefreshSourceChoices();
            return;
        }
        // ItemsSource reset sends a transient null through SelectedItem's two-way binding.
        // It must not erase the selected scheme or its references during a catalog refresh.
        _refreshingLookChoices = true;
        try { Looks.Clear(); foreach (var look in catalog.Looks.OrderByDescending(item => item.UpdatedAt)) Looks.Add(look); }
        finally { _refreshingLookChoices = false; }
        _projectDefaultLookId = _projectId is Guid project && catalog.ProjectDefaults.TryGetValue(project, out var defaultId) ? defaultId : null;
        var effective = ReferenceLookResolver.Resolve(null, _projectDefaultLookId, selected);
        SelectedLook = Looks.FirstOrDefault(item => item.ReferenceLookId == effective) ?? Looks.FirstOrDefault(); _sessionLookId = SelectedLook?.ReferenceLookId;
        RefreshSourceChoices();
    }
    public async Task SetProjectAsync(Guid? projectId, CancellationToken token = default) { if (_projectId != projectId) { SelectedLook = null; _sessionLookId = null; } _projectId = projectId; await LoadAsync(token); }
    public async Task SetSourceAsync(Guid? assetId, BitmapSource? source, CancellationToken token = default)
    {
        _render?.Cancel(); Interlocked.Increment(ref _revision);
        _assetId = assetId; _source = source; _interactiveSource = source is null ? null : CreateInteractiveProxy(source, 1600); OnPropertyChanged(nameof(SourceImage)); MatchedImage = null; ApplyCommand.RaiseCanExecuteChanged(); ExportCubeCommand.RaiseCanExecuteChanged();
        RaiseViewProperties();
        if ((_allowReferenceManagement || ApplyToFollowing) && Enabled && source is not null) await RenderAsync(token);
    }

    public void SetResponsiveContext(bool compact)
    {
        if (compact && !FocusView && ContextRailOpen) ContextRailOpen = false;
    }
    public async Task SelectLookAsync(Guid? lookId)
    {
        if (lookId is null) { var fallback = ReferenceLookResolver.Resolve(null, _projectDefaultLookId, _sessionLookId); SelectedLook = Looks.FirstOrDefault(look => look.ReferenceLookId == fallback) ?? SelectedLook; return; }
        if (Looks.Count == 0) await LoadAsync();
        SelectedLook = Looks.FirstOrDefault(look => look.ReferenceLookId == lookId) ?? SelectedLook;
    }
    public void ResetSplit() => SplitPosition = .5;
    public void HoldOriginal(bool held) { if (_originalHeld == held) return; _originalHeld = held; RaiseViewProperties(); }
    private void SetParameter(double value, Func<ReferenceLookParameters, ReferenceLookParameters> update, ref double fallback)
    {
        value = Math.Clamp(value, 0, 100); fallback = value;
        if (SelectedLook is not null)
        {
            _selectedLook = SelectedLook with { Parameters = update(SelectedLook.Parameters), UpdatedAt = DateTimeOffset.UtcNow };
            OnPropertyChanged(nameof(SelectedLook)); OnPropertyChanged(nameof(CurrentLookText)); CopyParameters(_selectedLook.Parameters);
            OnPropertyChanged(nameof(HasSessionAdjustment)); OnPropertyChanged(nameof(SessionAdjustmentText)); SaveCurrentAdjustmentCommand.RaiseCanExecuteChanged(); RestoreSchemeCommand.RaiseCanExecuteChanged();
        }
        _ = DebouncedRenderAsync();
    }
    private async Task EnableAndRenderAsync() { if (!Enabled) { Enabled = true; return; } await RenderAsync(); }
    private async Task ImportExternalReferenceAsync()
    {
        if (_dialogs is null) return; var path = _dialogs.ChooseFiles("导入参考图（仅关联原位置）", "图片|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp", false).FirstOrDefault(); if (path is null) return;
        BeginBusy();
        try
        {
            StatusText = "正在分析参考图…"; var source = await _preview.AnalyzeExternalReferenceAsync(path, _lifetime.Token); var now = DateTimeOffset.UtcNow;
            var look = new ReferenceLook(Guid.NewGuid(), Path.GetFileNameWithoutExtension(path), _projectId, [source], new(), now, now);
            await _store.SaveAsync(look, token: _lifetime.Token); Looks.Insert(0, look); SelectedLook = look; StatusText = "参考图已安全关联；原文件未复制、未修改。";
        }
        catch (OperationCanceledException) { StatusText = "已停止；已恢复最后有效预览。"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException)
        { StatusText = "参考图无法读取；现有色彩方案保持不变。"; }
        finally { EndBusy(); }
    }

    private void RefreshSourceChoices()
    {
        _refreshingLookChoices = true;
        try
        {
        SourceChoices.Clear(); IEnumerable<ReferenceLook> choices = Looks;
        if (SelectedSourceCategory.Kind == "Recent") choices = choices.OrderByDescending(item => item.UpdatedAt).Take(8);
        else if (SelectedSourceCategory.Kind is { } kind) choices = choices.Where(item => item.ReferenceSources.Any(source => string.Equals(source.Kind, kind, StringComparison.OrdinalIgnoreCase)));
        foreach (var choice in choices) SourceChoices.Add(choice);
        }
        finally { _refreshingLookChoices = false; }
    }

    private void RefreshReferenceSources()
    {
        ReferenceSources.Clear(); if (SelectedLook is null) return;
        foreach (var source in SelectedLook.Normalize().ReferenceSources)
            ReferenceSources.Add(new(source, source.Weight * 100, UpdateReferenceWeightAsync));
        RaiseReferenceCommands();
    }

    private async Task UpdateReferenceWeightAsync(ReferenceSourceWeightViewModel edited, double percent)
    {
        if (!_allowReferenceManagement) return;
        var look = SelectedLook; if (look is null) return; var sources = look.ReferenceSources.ToArray(); var index = Array.FindIndex(sources, item => item.ContentHash == edited.Source.ContentHash && item.SourcePath == edited.Source.SourcePath); if (index < 0) return;
        // Slider values are percentages; the persisted model stores relative 0..1
        // weights. Normalize only after converting to the same unit as its peers.
        sources[index] = sources[index] with { Weight = Math.Clamp(percent / 100d, .001, 1d) }; await SaveReferencesAsync(look, sources);
    }

    private async Task UpdateReferencesAsync(ReferenceSourceWeightViewModel? edited, ReferenceEdit edit)
    {
        if (!_allowReferenceManagement) return;
        var look = SelectedLook; if (look is null || edited is null) return; var sources = look.ReferenceSources.ToList(); var index = sources.FindIndex(item => item.ContentHash == edited.Source.ContentHash && item.SourcePath == edited.Source.SourcePath); if (index < 0) return;
        if (edit == ReferenceEdit.Remove && sources.Count > 1) sources.RemoveAt(index);
        else if (edit == ReferenceEdit.Up && index > 0) (sources[index - 1], sources[index]) = (sources[index], sources[index - 1]);
        else if (edit == ReferenceEdit.Down && index < sources.Count - 1) (sources[index + 1], sources[index]) = (sources[index], sources[index + 1]);
        else return;
        await SaveReferencesAsync(look, sources);
    }

    private async Task SaveReferencesAsync(ReferenceLook look, IReadOnlyList<ReferenceLookSource> sources)
    {
        var updated = (look with { ReferenceSources = sources, UpdatedAt = DateTimeOffset.UtcNow }).Normalize();
        try { await _store.SaveAsync(updated, token: _lifetime.Token); }
        catch (OperationCanceledException) { return; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { HasError = true; StatusText = "参考调整未能保存；原方案已保留，请检查存储位置后重试。"; RefreshReferenceSources(); return; }
        var index = Looks.IndexOf(look); if (index >= 0) Looks[index] = updated; _persistedLook = updated; _selectedLook = updated; OnPropertyChanged(nameof(SelectedLook)); OnPropertyChanged(nameof(HasSessionAdjustment)); OnPropertyChanged(nameof(SessionAdjustmentText)); RefreshReferenceSources(); await DebouncedRenderAsync();
    }
    private async Task SaveCurrentAdjustmentAsync()
    {
        if (_selectedLook is null || _dialogs is not IReferenceAdjustmentDialogService picker) return;
        var choice = picker.ChooseReferenceAdjustmentSave(_persistedLook?.Name ?? _selectedLook.Name); if (choice is null) return;
        var now = DateTimeOffset.UtcNow;
        var updated = (choice.UpdateExisting ? _selectedLook with { Name = choice.Name, UpdatedAt = now } : _selectedLook with { ReferenceLookId = Guid.NewGuid(), Name = choice.Name, CreatedAt = now, UpdatedAt = now }).Normalize();
        await _store.SaveAsync(updated, choice.SetProjectDefault, _lifetime.Token);
        if (choice.UpdateExisting) { var index = Looks.IndexOf(_persistedLook ?? _selectedLook); if (index >= 0) Looks[index] = updated; }
        else Looks.Insert(0, updated);
        _persistedLook = updated; _selectedLook = updated; CopyParameters(updated.Parameters);
        StatusText = choice.UpdateExisting ? "当前色彩方案已按确认更新。" : "当前调整已另存为新色彩方案。"; OnPropertyChanged(nameof(SelectedLook)); OnPropertyChanged(nameof(CurrentLookText)); OnPropertyChanged(nameof(HasSessionAdjustment)); OnPropertyChanged(nameof(SessionAdjustmentText)); SaveCurrentAdjustmentCommand.RaiseCanExecuteChanged(); RestoreSchemeCommand.RaiseCanExecuteChanged();
    }
    private void RestoreScheme()
    {
        if (_persistedLook is null) return;
        _selectedLook = _persistedLook; CopyParameters(_persistedLook.Parameters); CopyFilm(_persistedLook.Film ?? new()); OnPropertyChanged(nameof(SelectedLook)); OnPropertyChanged(nameof(HasSessionAdjustment)); OnPropertyChanged(nameof(SessionAdjustmentText)); SaveCurrentAdjustmentCommand.RaiseCanExecuteChanged(); RestoreSchemeCommand.RaiseCanExecuteChanged(); _ = DebouncedRenderAsync();
    }
    private void RaiseReferenceCommands() { RemoveReferenceCommand.RaiseCanExecuteChanged(); MoveReferenceUpCommand.RaiseCanExecuteChanged(); MoveReferenceDownCommand.RaiseCanExecuteChanged(); }
    private async Task ExportCubeAsync()
    {
        var source = _source; var look = SelectedLook; if (source is null || look is null || _dialogs is null) return;
        var safeName = string.Concat(look.Name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var path = _dialogs.ChooseSaveFile("导出 3D LUT", "Cube LUT|*.cube", ".cube", $"{safeName}_65.cube"); if (path is null) return;
        BeginBusy();
        try
        {
            StatusText = "正在生成 65³ 3D LUT…"; var lut = await _preview.BuildExportLutAsync(source, look, _lifetime.Token);
            await ReferenceCubeLutBuilder.ExportAsync(lut, path, look.Name, _lifetime.Token); StatusText = "3D LUT 已独立导出；源照片和色彩方案未修改。";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { StatusText = "3D LUT 导出失败；源照片和色彩方案未修改。"; }
        finally { EndBusy(); }
    }
    private void CopyParameters(ReferenceLookParameters value)
    { _match=value.MatchStrength;_tone=value.ToneStrength;_color=value.ColorStrength;_contrast=value.ContrastStrength;_saturation=value.SaturationStrength;_skin=value.SkinProtection;_highlight=value.HighlightProtection;_neutral=value.NeutralProtection;_keepOriginalTone=value.KeepOriginalTone; foreach(var name in new[]{nameof(MatchStrength),nameof(ToneStrength),nameof(ColorStrength),nameof(ContrastStrength),nameof(SaturationStrength),nameof(SkinProtection),nameof(HighlightProtection),nameof(NeutralProtection),nameof(KeepOriginalTone)})OnPropertyChanged(name); }
    private async Task DebouncedRenderAsync()
    {
        var revision = Interlocked.Increment(ref _revision); await Task.Delay(100);
        if (revision == Volatile.Read(ref _revision)) await RenderAsync(interactive: true);
        await Task.Delay(300);
        if (revision + 1 == Volatile.Read(ref _revision)) await RenderAsync(interactive: false);
    }
    private async Task RenderAsync(CancellationToken outer = default, bool interactive = false)
    {
        var source = interactive ? _interactiveSource ?? _source : _source; var look = SelectedLook; var asset = _assetId;
        _render?.Cancel();
        var revision = Interlocked.Increment(ref _revision);
        if (!Enabled || source is null || look is null) { MatchedImage = null; StatusText = !Enabled ? "现场监看仿色未开启。" : source is null ? "请选择待调色照片。" : "请添加参考图片或选择色彩方案。"; RaiseViewProperties(); return; }
        _render?.Dispose(); _render = CancellationTokenSource.CreateLinkedTokenSource(outer, _lifetime.Token);
        var renderToken = _render.Token;
        HasError = false; StatusText = interactive ? "正在生成快速预览…" : "正在生成高质量预览…";
        BeginBusy();
        try
        {
            var rendered = await _preview.RenderWithResultAsync(source, look, renderToken); var image = rendered.Image;
            if (FilmSettings.Enabled) { StatusText = "正在应用胶片质感…"; image = await _preview.ApplyFilmAsync(image, FilmSettings, renderToken); }
            if (PostProcessor is not null) image = await PostProcessor(image, renderToken);
            if (revision != Volatile.Read(ref _revision) || asset != _assetId) return;
            MatchedImage = image; StatusText = rendered.DifferenceWarning ?? "现场监看仿色已更新；RAW/JPEG 源文件未修改。"; RaiseViewProperties();
        }
        catch (OperationCanceledException) { if (revision == Volatile.Read(ref _revision)) { State = ProcessingState.Cancelled; StatusText = "已停止处理。"; } }
        catch (Exception) { if (revision == Volatile.Read(ref _revision)) { State = ProcessingState.Failed; HasError = true; MatchedImage = null; StatusText = "仿色未完成，继续显示原片；接片不受影响。"; RaiseViewProperties(); } }
        finally { EndBusy(); }
    }
    private static BitmapSource CreateInteractiveProxy(BitmapSource source, int maximumEdge)
    {
        var edge = Math.Max(source.PixelWidth, source.PixelHeight); if (edge <= maximumEdge) return source;
        var scale = maximumEdge / (double)edge; var transformed = new TransformedBitmap(source, new System.Windows.Media.ScaleTransform(scale, scale)); transformed.Freeze(); return transformed;
    }
    private void SetFilm(PixelTartFilmSettings value)
    {
        value.Validate(); FilmSettings = value;
        if (_selectedLook is not null) _selectedLook = _selectedLook with { Film = value, UpdatedAt = DateTimeOffset.UtcNow };
        foreach (var name in new[] { nameof(FilmEnabled), nameof(FilmProfileId), nameof(FilmProfileAmount), nameof(FilmGrainAmount), nameof(FilmGrainSize), nameof(FilmHalationAmount), nameof(FilmBloomAmount), nameof(FilmVignetteAmount), nameof(FilmSurfaceAmount), nameof(FilmTextureId), nameof(FilmTextureAmount), nameof(FilmSeed), nameof(HasSessionAdjustment), nameof(SessionAdjustmentText) }) OnPropertyChanged(name);
        SaveCurrentAdjustmentCommand.RaiseCanExecuteChanged(); RestoreSchemeCommand.RaiseCanExecuteChanged();
        _ = DebouncedRenderAsync();
    }
    private void CopyFilm(PixelTartFilmSettings value)
    {
        FilmSettings = value;
        foreach (var name in new[] { nameof(FilmEnabled), nameof(FilmProfileId), nameof(FilmProfileAmount), nameof(FilmGrainAmount), nameof(FilmGrainSize), nameof(FilmHalationAmount), nameof(FilmBloomAmount), nameof(FilmVignetteAmount), nameof(FilmSurfaceAmount), nameof(FilmTextureId), nameof(FilmTextureAmount), nameof(FilmSeed) }) OnPropertyChanged(name);
    }
    private void RaiseViewProperties(){foreach(var name in new[]{nameof(ShowOriginal),nameof(ShowMatched),nameof(ShowSplit),nameof(ShowSideBySide)})OnPropertyChanged(name);}
    public void Dispose(){_lifetime.Cancel();_lifetime.Dispose();_render?.Cancel();_render?.Dispose();}
}

public sealed record ReferenceSourceCategory(string Label, string? Kind);
public sealed class ReferenceSourceWeightViewModel : ObservableObject
{
    private readonly Func<ReferenceSourceWeightViewModel, double, Task> _changed; private double _weightPercent;
    public ReferenceSourceWeightViewModel(ReferenceLookSource source, double weightPercent, Func<ReferenceSourceWeightViewModel, double, Task> changed) { Source=source;_weightPercent=weightPercent;_changed=changed; }
    public ReferenceLookSource Source { get; } public string Name => Source.Name; public string SourceLabel => Source.Kind switch { "Board"=>"灵感板", "Canvas"=>"自由画布", "External"=>"外部参考图", _=>"素材库" };
    public string AvailabilityText => string.IsNullOrWhiteSpace(Source.SourcePath) ? "使用已保存的参考分析" : File.Exists(Source.SourcePath) ? "参考文件可用" : "参考文件离线 · 使用已保存分析";
    public Task WeightUpdateTask { get; private set; } = Task.CompletedTask;
    public double WeightPercent { get=>_weightPercent; set { var bounded=Math.Clamp(value,.1,100); if(SetProperty(ref _weightPercent,bounded)) WeightUpdateTask=_changed(this,bounded); } }
}
internal enum ReferenceEdit { Remove, Up, Down }
