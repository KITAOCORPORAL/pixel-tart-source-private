using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class TetherReferenceModeViewModel : ObservableObject, IDisposable
{
    private readonly ReferenceLookStore _store;
    private readonly ColorStudioSchemeStore _schemeStore;
    private readonly IDialogService? _dialogs;
    private readonly IReferenceRenderBackend _preview;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _render;
    private BitmapSource? _source;
    private BitmapSource? _interactiveSource;
    private Guid? _assetId;
    private long _revision;
    private int _pendingDebounceWork;
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
    private ColorAdjustmentStack _adjustmentStack = new(Array.Empty<ColorAdjustmentStackNode>());
    private Guid? _selectedAdjustmentNodeId;
    private bool _showSelection;
    private string _sampleMode = "普通取样";
    private bool _isSampling;
    private readonly Stack<ColorAdjustmentStack> _undoStacks = new();
    private readonly Stack<ColorAdjustmentStack> _redoStacks = new();
    private readonly Stack<Guid?> _undoSelections = new();
    private readonly Stack<Guid?> _redoSelections = new();
    private ColorAdjustmentStack? _persistedStack;
    private ColorStudioSchemeV2? _selectedColorScheme;
    private ColorAdjustmentStack? _editTransactionBefore;
    private Guid? _editTransactionSelection;
    private bool _syncingStack;
    public enum ProcessingState { Idle, Preparing, Analyzing, Matching, RenderingPreview, RenderingHighQuality, ApplyingFilm, BatchProcessing, Exporting, Cancelling, Cancelled, Failed }
    private ProcessingState _processingState;
    public ProcessingState State { get => _processingState; private set { if (SetProperty(ref _processingState, value)) { OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(IsCancelling)); OnPropertyChanged(nameof(IsSettled)); } } }
    public bool IsCancelling => State == ProcessingState.Cancelling;
    public bool HasError { get => _hasError; private set => SetProperty(ref _hasError, value); }
    public bool IsBusy => _busyOperations > 0 || State is not ProcessingState.Idle and not ProcessingState.Cancelled and not ProcessingState.Failed;
    public bool IsSettled => State == ProcessingState.Idle && !IsBusy && Volatile.Read(ref _pendingDebounceWork) == 0;
    internal string[] NativeRenderedOrder { get; private set; } = [];
    internal int NativeUndoCount => _undoStacks.Count;
    internal int NativeRedoCount => _redoStacks.Count;
    private void BeginBusy() { Interlocked.Increment(ref _busyOperations); State = ProcessingState.RenderingHighQuality; OnPropertyChanged(nameof(IsSettled)); }
    private void EndBusy()
    {
        if (Interlocked.Decrement(ref _busyOperations) != 0) return;
        if (State == ProcessingState.Cancelling) { State = ProcessingState.Cancelled; StatusText = "已停止处理。"; }
        else if (State is not ProcessingState.Failed and not ProcessingState.Cancelled) State = ProcessingState.Idle;
        OnPropertyChanged(nameof(IsSettled));
    }
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
        if (snapshot is null && AdjustmentStack.Nodes.Count == 0) return;
        foreach (var target in targets)
        {
            target.AppliedLookSnapshot = snapshot is null ? null : snapshot with { ReferenceSources = snapshot.ReferenceSources.ToArray() };
            target.FilmSettingsSnapshot = FilmSettings with { };
            target.ColorAdjustmentStackSnapshot = AdjustmentStack.Nodes.Count > 0 ? AdjustmentStack.DeepClone() : null;
            target.Status = ReferenceTargetStatus.Synced;
        }
    }
    public void ApplyTargetSnapshot(ReferenceLook? look, PixelTartFilmSettings? film, ColorAdjustmentStack? stack = null)
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
        AdjustmentStack = stack?.DeepClone() ?? new ColorAdjustmentStack(Array.Empty<ColorAdjustmentStackNode>());
        _selectedAdjustmentNodeId = AdjustmentStack.Nodes.FirstOrDefault()?.Id; OnPropertyChanged(nameof(SelectedAdjustmentNode));
        _ = DebouncedRenderAsync();
    }
    public async Task<BitmapSource> ProcessForExportAsync(string path, ReferenceLook? snapshot, PixelTartFilmSettings? film, CancellationToken token, ColorAdjustmentStack? stack = null)
    {
        var source = await Task.Run(() =>
        {
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(path); image.EndInit(); image.Freeze(); return image;
        }, token);
        // Batch export is driven by each target's frozen snapshot, never by the
        // currently active editor (which may belong to a different target).
        if (stack is not null) return await Task.Run(() => ColorStudioBitmapRenderer.Render(source, stack, snapshot, token), token);
        var look = snapshot;
        if (look is null) return source;
        var rendered = await _preview.RenderWithResultAsync(source, look, token);
        var output = rendered.Image;
        var settings = film ?? look.Film ?? new PixelTartFilmSettings();
        if (settings.Enabled) output = await _preview.ApplyFilmAsync(output, settings, token);
        if (PostProcessor is not null) output = await PostProcessor(output, token);
        return output;
    }
    public Task<BitmapSource> PreviewColorStudioAsync(BitmapSource source, ColorAdjustmentStack stack, ReferenceLook? reference, CancellationToken token = default) =>
        Task.Run(() => ColorStudioBitmapRenderer.Render(source, stack, reference, token), token);
    public Task<BitmapSource> PreviewSelectionAsync(BitmapSource source, CancellationToken token = default)
    {
        var node = SelectedAdjustmentNode;
        return node is null || node.Type != ColorStudioNodeType.ColorRange
            ? Task.FromResult(source)
            : Task.Run(() => ColorStudioBitmapRenderer.ShowSelection(source, node, token), token);
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
    public RelayCommand AddAdjustmentNodeCommand { get; }
    public RelayCommand DeleteAdjustmentNodeCommand { get; }
    public RelayCommand DuplicateAdjustmentNodeCommand { get; }
    public RelayCommand MoveAdjustmentNodeUpCommand { get; }
    public RelayCommand MoveAdjustmentNodeDownCommand { get; }
    public RelayCommand ResetAdjustmentNodeCommand { get; }
    public RelayCommand ToggleAdjustmentNodeCommand { get; }
    public RelayCommand RenameAdjustmentNodeCommand { get; }
    public RelayCommand UndoAdjustmentCommand { get; }
    public RelayCommand RedoAdjustmentCommand { get; }
    public RelayCommand BeginEditTransactionCommand { get; }
    public RelayCommand CommitEditTransactionCommand { get; }
    public RelayCommand AddSampleCommand { get; }
    public RelayCommand SubtractSampleCommand { get; }
    public RelayCommand ClearSamplesCommand { get; }
    public RelayCommand StartSamplingCommand { get; }
    public RelayCommand CancelSamplingCommand { get; }
    public AsyncRelayCommand SaveColorSchemeCommand { get; }
    public AsyncRelayCommand SaveColorSchemeAsCommand { get; }
    public AsyncRelayCommand DeleteColorSchemeCommand { get; }
    public RelayCommand ApplyColorSchemeCommand { get; }
    public ObservableCollection<ColorStudioSchemeV2> ColorSchemes { get; } = [];
    public ColorStudioSchemeV2? SelectedColorScheme { get => _selectedColorScheme; set { if (SetProperty(ref _selectedColorScheme, value)) { if (value is not null) { ColorSchemeName = value.Name; OnPropertyChanged(nameof(ColorSchemeName)); } ApplyColorSchemeCommand?.RaiseCanExecuteChanged(); DeleteColorSchemeCommand?.RaiseCanExecuteChanged(); } } }
    public string ColorSchemeName { get; set; } = "新色彩方案";
    public IReadOnlyList<string> WorkspaceModes { get; } = ["简洁", "专业"];
    public IReadOnlyList<string> WorkspaceSections { get; } = ["调色", "仿色", "预设", "胶片", "输出"];
    public string WorkspaceSection { get => _workspaceSection; set { if (SetProperty(ref _workspaceSection, value)) OnPropertyChanged(nameof(IsNodeSection)); } }
    public string WorkspaceMode { get => _workspaceMode; set { if (SetProperty(ref _workspaceMode, value)) { if (value == "专业") EnsureProfessionalStack(); OnPropertyChanged(nameof(IsSimpleMode)); OnPropertyChanged(nameof(IsProMode)); } } }
    public bool IsSimpleMode => WorkspaceMode == "简洁";
    public bool IsProMode => WorkspaceMode == "专业";
    public bool IsNodeSection => WorkspaceSection is "调色" or "仿色";
    public bool FocusView { get => _focusView; set { if (SetProperty(ref _focusView, value)) { OnPropertyChanged(nameof(IsContextVisible)); OnPropertyChanged(nameof(IsLeftRailVisible)); } } }
    public bool ContextRailOpen { get => _contextRailOpen; set { if (SetProperty(ref _contextRailOpen, value)) OnPropertyChanged(nameof(IsContextVisible)); } }
    public bool IsContextVisible => !FocusView && ContextRailOpen;
    public bool IsLeftRailVisible => !FocusView;
    public ColorAdjustmentStack AdjustmentStack { get => _adjustmentStack; private set { if (SetProperty(ref _adjustmentStack, value)) { OnPropertyChanged(nameof(AdjustmentNodes)); OnPropertyChanged(nameof(SelectedAdjustmentNode)); RaiseAdjustmentCommands(); SyncSimpleFromStack(); OnPropertyChanged(nameof(HasSessionAdjustment)); OnPropertyChanged(nameof(SessionAdjustmentText)); RestoreSchemeCommand?.RaiseCanExecuteChanged(); } } }
    public IReadOnlyList<ColorAdjustmentStackNode> AdjustmentNodes => AdjustmentStack.Nodes;
    public ColorAdjustmentStackNode? SelectedAdjustmentNode
    {
        get => AdjustmentStack.Nodes.FirstOrDefault(node => node.Id == _selectedAdjustmentNodeId);
        set
        {
            // ItemsSource replacement transiently clears WPF selection. Keep the canonical ID when it still exists.
            if (value is null && AdjustmentStack.Nodes.Any(node => node.Id == _selectedAdjustmentNodeId)) return;
            _selectedAdjustmentNodeId = value?.Id; OnPropertyChanged(); RaiseAdjustmentCommands();
        }
    }
    public bool SelectedNodeEnabled { get => SelectedAdjustmentNode?.Enabled == true; set { if (SelectedAdjustmentNode is { } selected && value != selected.Enabled) ChangeStack(nodes => nodes.Select(node => node.Id == selected.Id ? node with { Enabled = value } : node).ToArray()); OnPropertyChanged(); } }
    public string SelectedNodeName { get => SelectedAdjustmentNode?.Name ?? string.Empty; set { if (!string.Equals(value, SelectedNodeName, StringComparison.Ordinal)) RenameSelectedAdjustmentNode(value); } }
    public bool IsRenamingNode { get; set; }
    public string PendingNodeName { get; set; } = "";
    public RelayCommand StartNodeRenameCommand { get; }
    public RelayCommand CommitNodeRenameCommand { get; }
    public RelayCommand CancelNodeRenameCommand { get; }
    public bool AddAdjustmentOpen { get; set; }
    public RelayCommand ToggleAddAdjustmentCommand { get; }
    public bool KeepOriginalLuminance { get => SelectedAdjustmentNode?.NumericParameters.TryGetValue("keep_original_luminance", out var value) == true && value >= .5; set { if (SelectedAdjustmentNode is { Type: ColorStudioNodeType.ColorRange } selected) SetSelectedNumeric("keep_original_luminance", value ? 1 : 0); OnPropertyChanged(); } }
    public double RangeStrength { get => SelectedNumeric("strength", 100); set => SetSelectedNumeric("strength", value); }
    public double RangeRadius { get => SelectedNumeric("range", .12) * 100; set => SetSelectedNumeric("range", value / 100); }
    public double RangeSoftness { get => SelectedNumeric("softness", .08) * 100; set => SetSelectedNumeric("softness", value / 100); }
    public double RangeHue { get => SelectedNumeric("hue", 0); set => SetSelectedNumeric("hue", value); }
    public double RangeSaturation { get => SelectedNumeric("saturation", 0); set => SetSelectedNumeric("saturation", value); }
    public double RangeChroma { get => SelectedNumeric("chroma", 0); set => SetSelectedNumeric("chroma", value); }
    public double RangeLightness { get => SelectedNumeric("lightness", 0); set => SetSelectedNumeric("lightness", value); }
    public double TransitionAmount { get => SelectedNumeric("amount", .25) * 100; set => SetSelectedNumeric("amount", value / 100); }
    public IReadOnlyList<VisualRgb24> PositiveSamples => SelectedAdjustmentNode?.Samples ?? [];
    public IReadOnlyList<VisualRgb24> NegativeSamples => SelectedAdjustmentNode?.NegativeSamples ?? [];
    public RelayCommand RemovePositiveSampleCommand { get; }
    public RelayCommand RemoveNegativeSampleCommand { get; }
    private double SelectedNumeric(string key, double fallback) => SelectedAdjustmentNode?.NumericParameters.TryGetValue(key, out var value) == true ? value : fallback;
    public bool ShowSelection { get => _showSelection; set { if (SetProperty(ref _showSelection, value)) { OnPropertyChanged(); _ = RenderAsync(); } } }
    public IReadOnlyList<string> SampleModes { get; } = ["普通取样", "增加取样", "减少取样"];
    public string SampleMode { get => _sampleMode; set => SetProperty(ref _sampleMode, value); }
    public bool IsSampling { get => _isSampling; private set => SetProperty(ref _isSampling, value); }

    public TetherReferenceModeViewModel(ReferenceLookStore? store = null, IDialogService? dialogs = null, bool allowReferenceManagement = false,
        IReferenceRenderBackend? renderBackend = null, ColorStudioSchemeStore? schemeStore = null)
    {
        _store = store ?? new(Path.Combine(AppDataPaths.DataDirectory, "ProjectVisuals"));
        _schemeStore = schemeStore ?? new ColorStudioSchemeStore(Path.Combine(AppDataPaths.DataDirectory, "ProjectVisuals"));
        _dialogs = dialogs;
        _preview = renderBackend ?? new ReferenceLookPreviewService();
        _allowReferenceManagement = allowReferenceManagement;
        ApplyCommand = new AsyncRelayCommand(_ => EnableAndRenderAsync(), _ => (SelectedLook is not null || AdjustmentStack.Nodes.Count > 0) && _source is not null);
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
        AddAdjustmentNodeCommand = new RelayCommand(value => AddAdjustmentNode(value as string ?? "ColorRange"));
        ToggleAddAdjustmentCommand = new RelayCommand(_ => { AddAdjustmentOpen = !AddAdjustmentOpen; OnPropertyChanged(nameof(AddAdjustmentOpen)); });
        DeleteAdjustmentNodeCommand = new RelayCommand(_ => DeleteSelectedAdjustmentNode(), _ => SelectedAdjustmentNode is not null && AdjustmentStack.Nodes.Count > 1);
        DuplicateAdjustmentNodeCommand = new RelayCommand(_ => DuplicateSelectedAdjustmentNode(), _ => SelectedAdjustmentNode is { Type: not ColorStudioNodeType.ReferenceMatch and not ColorStudioNodeType.Film });
        MoveAdjustmentNodeUpCommand = new RelayCommand(_ => MoveSelectedAdjustmentNode(-1), _ => SelectedAdjustmentNode is { } node && AdjustmentStack.Nodes.ToList().IndexOf(node) > 0);
        MoveAdjustmentNodeDownCommand = new RelayCommand(_ => MoveSelectedAdjustmentNode(1), _ => SelectedAdjustmentNode is { } node && AdjustmentStack.Nodes.ToList().IndexOf(node) < AdjustmentStack.Nodes.Count - 1);
        ResetAdjustmentNodeCommand = new RelayCommand(_ => ResetSelectedAdjustmentNode(), _ => SelectedAdjustmentNode is not null);
        ToggleAdjustmentNodeCommand = new RelayCommand(_ => SelectedNodeEnabled = !SelectedNodeEnabled, _ => SelectedAdjustmentNode is not null);
        RenameAdjustmentNodeCommand = new RelayCommand(value => RenameSelectedAdjustmentNode(value as string ?? SelectedNodeName), _ => SelectedAdjustmentNode is not null);
        StartNodeRenameCommand = new RelayCommand(_ => { PendingNodeName = SelectedNodeName; IsRenamingNode = true; OnPropertyChanged(nameof(PendingNodeName)); OnPropertyChanged(nameof(IsRenamingNode)); });
        CommitNodeRenameCommand = new RelayCommand(_ => { RenameSelectedAdjustmentNode(PendingNodeName); IsRenamingNode = false; OnPropertyChanged(nameof(IsRenamingNode)); });
        CancelNodeRenameCommand = new RelayCommand(_ => { IsRenamingNode = false; OnPropertyChanged(nameof(IsRenamingNode)); });
        UndoAdjustmentCommand = new RelayCommand(_ => UndoAdjustment(), _ => _undoStacks.Count > 0);
        RedoAdjustmentCommand = new RelayCommand(_ => RedoAdjustment(), _ => _redoStacks.Count > 0);
        BeginEditTransactionCommand = new RelayCommand(_ => BeginEditTransaction());
        CommitEditTransactionCommand = new RelayCommand(_ => CommitEditTransaction());
        AddSampleCommand = new RelayCommand(value => SampleMode = value as string ?? "增加取样");
        SubtractSampleCommand = new RelayCommand(_ => SampleMode = "减少取样");
        ClearSamplesCommand = new RelayCommand(_ => ClearSamples(), _ => SelectedAdjustmentNode is { } node && (node.Samples.Count > 0 || node.NegativeSamples.Count > 0));
        RemovePositiveSampleCommand = new RelayCommand(value => RemoveSample(value, false));
        RemoveNegativeSampleCommand = new RelayCommand(value => RemoveSample(value, true));
        StartSamplingCommand = new RelayCommand(value => { SampleMode = value as string ?? "普通取样"; IsSampling = true; StatusText = "正在取样；点击照片取色，按 Esc 退出。"; });
        CancelSamplingCommand = new RelayCommand(_ => { IsSampling = false; StatusText = "已退出取样。"; });
        SaveColorSchemeCommand = new AsyncRelayCommand(_ => SaveColorSchemeAsync(false));
        SaveColorSchemeAsCommand = new AsyncRelayCommand(_ => SaveColorSchemeAsync(true));
        DeleteColorSchemeCommand = new AsyncRelayCommand(_ => DeleteColorSchemeAsync(), _ => SelectedColorScheme is not null);
        ApplyColorSchemeCommand = new RelayCommand(_ => ApplyColorScheme(), _ => SelectedColorScheme is not null);
        InitializeSchemeInteractions();
    }
    private void EnsureProfessionalStack()
    {
        if (AdjustmentStack.Nodes.Count > 0) return;
        var nodes = new List<ColorAdjustmentStackNode>();
        if (SelectedLook is not null) nodes.Add(ColorStudioLegacyMigration.Migrate(SelectedLook).Stack.Nodes[0]);
        nodes.Add(new(Guid.NewGuid(), ColorStudioNodeType.ColorRange, "颜色范围"));
        nodes.Add(new(Guid.NewGuid(), ColorStudioNodeType.TransitionBlend, "色彩过渡"));
        nodes.Add(new(Guid.NewGuid(), ColorStudioNodeType.Film, "胶片", FilmSettings.Enabled, FilmSettings: FilmSettings));
        AdjustmentStack = new ColorAdjustmentStack(nodes).Normalize(); _selectedAdjustmentNodeId = AdjustmentStack.Nodes[0].Id; OnPropertyChanged(nameof(SelectedAdjustmentNode));
    }
    private async Task SaveColorSchemeAsync(bool saveAs)
    {
        if (AdjustmentStack.Nodes.Count == 0) EnsureProfessionalStack();
        var name = string.IsNullOrWhiteSpace(ColorSchemeName) ? "新色彩方案" : ColorSchemeName.Trim();
        var scheme = new ColorStudioSchemeV2(saveAs || _appliedColorScheme is null ? Guid.NewGuid() : _appliedColorScheme.Id,
            name, AdjustmentStack.DeepClone(), DateTimeOffset.UtcNow);
        try
        {
            await _schemeStore.SaveAsync(scheme, _lifetime.Token);
            var old = ColorSchemes.FirstOrDefault(item => item.Id == scheme.Id); if (old is not null) ColorSchemes.Remove(old);
            ColorSchemes.Insert(0, scheme); SelectedColorScheme = scheme; _persistedStack = scheme.Stack.DeepClone();
            _appliedColorScheme = scheme; HasError = false; NotifySchemeState(); StatusText = "色彩方案已保存。";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        { HasError = true; StatusText = "色彩方案未能保存；原文件已保留。"; }
    }
    private async Task DeleteColorSchemeAsync()
    {
        if (SelectedColorScheme is not { } scheme) return;
        try
        {
            await _schemeStore.DeleteAsync(scheme.Id, _lifetime.Token);
            var current = ColorSchemes.FirstOrDefault(item => item.Id == scheme.Id);
            if (current is not null) ColorSchemes.Remove(current);
            SelectedColorScheme = null; StatusText = "色彩方案已删除。";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { HasError = true; StatusText = "删除失败；原色彩方案已保留。"; }
    }
    private void ApplyColorScheme()
    {
        if (SelectedColorScheme is not { } scheme) return;
        AdjustmentStack = scheme.Stack.DeepClone(); _selectedAdjustmentNodeId = AdjustmentStack.Nodes.FirstOrDefault()?.Id;
        _persistedStack = scheme.Stack.DeepClone(); ColorSchemeName = scheme.Name; OnPropertyChanged(nameof(SelectedAdjustmentNode));
        _appliedColorScheme = scheme; NotifySchemeState();
        _ = RenderAsync(); StatusText = "色彩方案已应用。";
    }
    private void ChangeStack(Func<IReadOnlyList<ColorAdjustmentStackNode>, IReadOnlyList<ColorAdjustmentStackNode>> change)
    {
        var previous = AdjustmentStack; var nodes = change(previous.Nodes).ToArray(); if (nodes.Length == 0) return;
        if (previous.Nodes.SequenceEqual(nodes)) return;
        if (_editTransactionBefore is null) { _undoStacks.Push(previous); _undoSelections.Push(_selectedAdjustmentNodeId); }
        _redoStacks.Clear(); _redoSelections.Clear(); AdjustmentStack = (previous with { Nodes = nodes }).Normalize(); _ = RenderAsync();
    }
    public void BeginEditTransaction()
    {
        if (_editTransactionBefore is not null) return;
        _editTransactionBefore = AdjustmentStack.DeepClone(); _editTransactionSelection = _selectedAdjustmentNodeId;
    }
    public void CommitEditTransaction()
    {
        if (_editTransactionBefore is not { } before) return;
        _editTransactionBefore = null;
        if (!before.Nodes.SequenceEqual(AdjustmentStack.Nodes)) { _undoStacks.Push(before); _undoSelections.Push(_editTransactionSelection); }
        _editTransactionSelection = null;
        RaiseAdjustmentCommands();
    }
    private void AddAdjustmentNode(string type)
    {
        var nodeType = Enum.TryParse<ColorStudioNodeType>(type, true, out var parsed) ? parsed : ColorStudioNodeType.ColorRange;
        AddAdjustmentOpen = false; OnPropertyChanged(nameof(AddAdjustmentOpen));
        if (nodeType is ColorStudioNodeType.ReferenceMatch or ColorStudioNodeType.Film && AdjustmentStack.Nodes.Any(node => node.Type == nodeType)) return;
        if (nodeType == ColorStudioNodeType.ReferenceMatch && SelectedLook is null) return;
        var node = new ColorAdjustmentStackNode(Guid.NewGuid(), nodeType, nodeType switch { ColorStudioNodeType.ReferenceMatch => "参考仿色", ColorStudioNodeType.Film => "胶片", ColorStudioNodeType.TransitionBlend => "色彩过渡", _ => "颜色范围" }, true, FilmSettings: nodeType == ColorStudioNodeType.Film ? FilmSettings : null);
        ChangeStack(nodes => [.. nodes, node]); _selectedAdjustmentNodeId = node.Id; OnPropertyChanged(nameof(SelectedAdjustmentNode));
    }
    private void DeleteSelectedAdjustmentNode() { if (SelectedAdjustmentNode is { } selected && AdjustmentStack.Nodes.Count > 1) ChangeStack(nodes => nodes.Where(node => node.Id != selected.Id).ToArray()); }
    private void DuplicateSelectedAdjustmentNode() { if (SelectedAdjustmentNode is { Type: not ColorStudioNodeType.ReferenceMatch and not ColorStudioNodeType.Film } selected) { var copy = selected.Normalize() with { Id = Guid.NewGuid(), Name = selected.Name + " 副本" }; ChangeStack(nodes => nodes.SelectMany(node => node.Id == selected.Id ? new[] { node, copy } : new[] { node }).ToArray()); _selectedAdjustmentNodeId = copy.Id; OnPropertyChanged(nameof(SelectedAdjustmentNode)); } }
    private void MoveSelectedAdjustmentNode(int delta) { if (SelectedAdjustmentNode is not { } selected) return; ChangeStack(nodes => { var list = nodes.ToList(); var index = list.FindIndex(node => node.Id == selected.Id); var next = Math.Clamp(index + delta, 0, list.Count - 1); (list[index], list[next]) = (list[next], list[index]); return list; }); }
    public void MoveAdjustmentNode(Guid sourceId, Guid destinationId)
    {
        if (sourceId == destinationId) return;
        ChangeStack(nodes =>
        {
            var list = nodes.ToList(); var from = list.FindIndex(node => node.Id == sourceId); var to = list.FindIndex(node => node.Id == destinationId);
            if (from < 0 || to < 0) return nodes;
            var moving = list[from]; list.RemoveAt(from); list.Insert(to, moving); return list;
        });
    }
    public void InsertAdjustmentNode(Guid sourceId, Guid destinationId, bool after)
    {
        if (sourceId == destinationId) return;
        ChangeStack(nodes =>
        {
            var list = nodes.ToList(); var source = list.Find(node => node.Id == sourceId);
            if (source is null || list.All(node => node.Id != destinationId)) return nodes;
            list.Remove(source);
            var index = list.FindIndex(node => node.Id == destinationId);
            list.Insert(index + (after ? 1 : 0), source); return list;
        });
    }
    private void ResetSelectedAdjustmentNode() { if (SelectedAdjustmentNode is { } selected) ChangeStack(nodes => nodes.Select(node => node.Id == selected.Id ? selected with { NumericParameters = new Dictionary<string, double>(), Samples = Array.Empty<VisualRgb24>(), NegativeSamples = Array.Empty<VisualRgb24>(), FilmSettings = selected.Type == ColorStudioNodeType.Film ? new PixelTartFilmSettings() : null } : node).ToArray()); }
    private void ClearSamples() { if (SelectedAdjustmentNode is { Type: ColorStudioNodeType.ColorRange } selected) ChangeStack(nodes => nodes.Select(node => node.Id == selected.Id ? node with { Samples = Array.Empty<VisualRgb24>(), NegativeSamples = Array.Empty<VisualRgb24>() } : node).ToArray()); }
    private void RemoveSample(object? value, bool negative)
    {
        if (SelectedAdjustmentNode is not { Type: ColorStudioNodeType.ColorRange } selected || value is not VisualRgb24 sample) return;
        var samples = negative ? selected.NegativeSamples : selected.Samples;
        var index = Array.FindIndex(samples.ToArray(), item => item.Equals(sample)); if (index < 0) return;
        ChangeStack(nodes => nodes.Select(node => node.Id != selected.Id ? node : negative ? node.RemoveNegativeSampleAt(index) : node.RemoveSampleAt(index)).ToArray());
    }
    public void AddDisplayedSample(VisualRgb24 sample)
    {
        if (SelectedAdjustmentNode is not { Type: ColorStudioNodeType.ColorRange } selected) return;
        ChangeStack(nodes => nodes.Select(node => node.Id == selected.Id ? SampleMode switch
        {
            "减少取样" => node.AddNegativeSample(sample),
            _ => node.AddSample(sample)
        } : node).ToArray());
    }
    public void CompleteDisplayedSample(VisualRgb24 sample)
    {
        AddDisplayedSample(sample);
        IsSampling = SampleMode != "普通取样";
        StatusText = "取样已加入当前颜色范围节点。";
    }
    private void UndoAdjustment() { if (_undoStacks.Count == 0) return; _redoStacks.Push(AdjustmentStack); _redoSelections.Push(_selectedAdjustmentNodeId); _selectedAdjustmentNodeId = _undoSelections.Pop(); AdjustmentStack = _undoStacks.Pop(); OnPropertyChanged(nameof(SelectedAdjustmentNode)); _ = RenderAsync(); }
    private void RedoAdjustment() { if (_redoStacks.Count == 0) return; _undoStacks.Push(AdjustmentStack); _undoSelections.Push(_selectedAdjustmentNodeId); _selectedAdjustmentNodeId = _redoSelections.Pop(); AdjustmentStack = _redoStacks.Pop(); OnPropertyChanged(nameof(SelectedAdjustmentNode)); _ = RenderAsync(); }
    private void SetSelectedNumeric(string key, double value)
    {
        if (SelectedAdjustmentNode is not { } selected) return;
        ChangeStack(nodes => nodes.Select(node => node.Id == selected.Id ? node with { NumericParameters = new Dictionary<string, double>(node.NumericParameters) { [key] = value } } : node).ToArray());
    }
    private void SetNodeNumeric(ColorStudioNodeType type, string key, double value)
    {
        if (_syncingStack || AdjustmentStack.Nodes.Count == 0) return;
        var index = AdjustmentStack.Nodes.ToList().FindIndex(node => node.Type == type);
        if (index < 0) return;
        var node = AdjustmentStack.Nodes[index];
        if (node.NumericParameters.TryGetValue(key, out var existing) && existing == value) return;
        ChangeStack(nodes => nodes.Select((item, position) => position == index ? item with { NumericParameters = new Dictionary<string, double>(item.NumericParameters) { [key] = value } } : item).ToArray());
    }
    private void SyncStackFromSimple()
    {
        if (_syncingStack || SelectedLook is null || AdjustmentStack.Nodes.Count == 0) return;
        var migrated = ColorStudioLegacyMigration.Migrate(SelectedLook).Stack.Nodes[0].NumericParameters;
        ChangeStack(nodes => nodes.Select(node => node.Type switch
        {
            ColorStudioNodeType.ReferenceMatch => node with { NumericParameters = new Dictionary<string, double>(migrated) },
            ColorStudioNodeType.Film => node with { FilmSettings = FilmSettings },
            _ => node
        }).ToArray());
    }
    private void SyncSimpleFromStack()
    {
        if (_syncingStack || AdjustmentStack.Nodes.Count == 0) return;
        _syncingStack = true;
        try
        {
            var reference = AdjustmentStack.Nodes.FirstOrDefault(node => node.Type == ColorStudioNodeType.ReferenceMatch);
            if (reference is not null && SelectedLook is not null)
            {
                double Get(string key, double fallback) => reference.NumericParameters.TryGetValue(key, out var value) ? value : fallback;
                var old = SelectedLook.Parameters;
                _selectedLook = SelectedLook with { Parameters = old with
                {
                    MatchStrength = Get("match_strength", old.MatchStrength), ToneStrength = Get("tone_strength", old.ToneStrength),
                    ColorStrength = Get("color_strength", old.ColorStrength), ContrastStrength = Get("contrast_strength", old.ContrastStrength),
                    SaturationStrength = Get("saturation_strength", old.SaturationStrength), SkinProtection = Get("skin_protection", old.SkinProtection),
                    HighlightProtection = Get("highlight_protection", old.HighlightProtection), NeutralProtection = Get("neutral_protection", old.NeutralProtection),
                    KeepOriginalTone = Get("keep_original_tone", old.KeepOriginalTone ? 1 : 0) >= .5
                } };
                CopyParameters(_selectedLook.Parameters); OnPropertyChanged(nameof(SelectedLook));
            }
            var film = AdjustmentStack.Nodes.FirstOrDefault(node => node.Type == ColorStudioNodeType.Film);
            if (film?.FilmSettings is { } settings) { CopyFilm(settings); if (_selectedLook is not null) _selectedLook = _selectedLook with { Film = settings }; }
        }
        finally { _syncingStack = false; }
    }
    public void RenameSelectedAdjustmentNode(string name)
    {
        if (SelectedAdjustmentNode is not { } selected || string.IsNullOrWhiteSpace(name)) return;
        ChangeStack(nodes => nodes.Select(node => node.Id == selected.Id ? node with { Name = name.Trim() } : node).ToArray());
    }
    private void RaiseAdjustmentCommands() { DeleteAdjustmentNodeCommand?.RaiseCanExecuteChanged(); DuplicateAdjustmentNodeCommand?.RaiseCanExecuteChanged(); MoveAdjustmentNodeUpCommand?.RaiseCanExecuteChanged(); MoveAdjustmentNodeDownCommand?.RaiseCanExecuteChanged(); ResetAdjustmentNodeCommand?.RaiseCanExecuteChanged(); ToggleAdjustmentNodeCommand?.RaiseCanExecuteChanged(); RenameAdjustmentNodeCommand?.RaiseCanExecuteChanged(); ClearSamplesCommand?.RaiseCanExecuteChanged(); UndoAdjustmentCommand?.RaiseCanExecuteChanged(); RedoAdjustmentCommand?.RaiseCanExecuteChanged(); OnPropertyChanged(nameof(SelectedNodeEnabled)); OnPropertyChanged(nameof(SelectedNodeName)); OnPropertyChanged(nameof(KeepOriginalLuminance)); foreach (var name in new[] { nameof(RangeStrength), nameof(RangeRadius), nameof(RangeSoftness), nameof(RangeHue), nameof(RangeSaturation), nameof(RangeChroma), nameof(RangeLightness), nameof(TransitionAmount), nameof(PositiveSamples), nameof(NegativeSamples) }) OnPropertyChanged(name); }
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
    public ReferenceLook? SelectedLook { get => _selectedLook; set { if (_refreshingLookChoices && value is null) return; if (SetProperty(ref _selectedLook, value)) { _persistedLook = value; _sessionLookId = value?.ReferenceLookId; OnPropertyChanged(nameof(CurrentLookText)); CopyParameters(value?.Parameters ?? new()); CopyFilm(value?.Film ?? new()); if (AdjustmentStack.Nodes.Count > 0 && value is not null) SyncStackFromSimple(); RefreshReferenceSources(); ApplyCommand.RaiseCanExecuteChanged(); ExportCubeCommand.RaiseCanExecuteChanged(); SaveCurrentAdjustmentCommand.RaiseCanExecuteChanged(); RestoreSchemeCommand.RaiseCanExecuteChanged(); IsSourcePickerOpen = false; _ = RenderAsync(); } } }
    public string CurrentLookText => SelectedLook?.Name ?? "未选择色彩方案";
    public BitmapSource? MatchedImage { get => _matchedImage; private set { if (SetProperty(ref _matchedImage, value)) OnPropertyChanged(nameof(EffectiveViewMode)); } }
    public BitmapSource? SourceImage => _source;
    public bool HasSessionAdjustment => (_persistedLook is not null && _selectedLook is not null &&
        (!_selectedLook.Parameters.Equals(_persistedLook.Parameters) || !FilmSettings.Equals(_persistedLook.Film ?? new()))) ||
        (AdjustmentStack.Nodes.Count > 0 && (_persistedStack is null ||
            ColorStudioSchemeSerializer.ComputeHash(new ColorStudioSchemeV2(Guid.Empty, "session", AdjustmentStack, DateTimeOffset.UnixEpoch)) !=
            ColorStudioSchemeSerializer.ComputeHash(new ColorStudioSchemeV2(Guid.Empty, "session", _persistedStack, DateTimeOffset.UnixEpoch))));
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
    public string EffectiveViewMode => ShowOriginal ? "原片" : ViewMode;
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    public double MatchStrength { get => SelectedLook?.Parameters.MatchStrength ?? _match; set => SetParameter(value, p => p with { MatchStrength = value }, ref _match, "match_strength"); }
    public double ToneStrength { get => SelectedLook?.Parameters.ToneStrength ?? _tone; set => SetParameter(value, p => p with { ToneStrength = value }, ref _tone, "tone_strength"); }
    public double ColorStrength { get => SelectedLook?.Parameters.ColorStrength ?? _color; set => SetParameter(value, p => p with { ColorStrength = value }, ref _color, "color_strength"); }
    public double ContrastStrength { get => SelectedLook?.Parameters.ContrastStrength ?? _contrast; set => SetParameter(value, p => p with { ContrastStrength = value }, ref _contrast, "contrast_strength"); }
    public double SaturationStrength { get => SelectedLook?.Parameters.SaturationStrength ?? _saturation; set => SetParameter(value, p => p with { SaturationStrength = value }, ref _saturation, "saturation_strength"); }
    public double SkinProtection { get => SelectedLook?.Parameters.SkinProtection ?? _skin; set => SetParameter(value, p => p with { SkinProtection = value }, ref _skin, "skin_protection"); }
    public double HighlightProtection { get => SelectedLook?.Parameters.HighlightProtection ?? _highlight; set => SetParameter(value, p => p with { HighlightProtection = value }, ref _highlight, "highlight_protection"); }
    public double NeutralProtection { get => SelectedLook?.Parameters.NeutralProtection ?? _neutral; set => SetParameter(value, p => p with { NeutralProtection = value }, ref _neutral, "neutral_protection"); }
    public bool KeepOriginalTone { get => SelectedLook?.Parameters.KeepOriginalTone ?? _keepOriginalTone; set { _keepOriginalTone = value; if (SelectedLook is not null) { _selectedLook = SelectedLook with { Parameters = SelectedLook.Parameters with { KeepOriginalTone = value }, UpdatedAt = DateTimeOffset.UtcNow }; OnPropertyChanged(nameof(SelectedLook)); OnPropertyChanged(nameof(HasSessionAdjustment)); OnPropertyChanged(nameof(SessionAdjustmentText)); SaveCurrentAdjustmentCommand.RaiseCanExecuteChanged(); RestoreSchemeCommand.RaiseCanExecuteChanged(); } OnPropertyChanged(); SetNodeNumeric(ColorStudioNodeType.ReferenceMatch, "keep_original_tone", value ? 1 : 0); _ = DebouncedRenderAsync(); } }
    private double _match=100,_tone=50,_color=70,_contrast=50,_saturation=50,_skin=60,_highlight=70,_neutral=65;
    private bool _keepOriginalTone;

    public async Task LoadAsync(CancellationToken token = default)
    {
        try
        {
            var schemes = await _schemeStore.LoadAsync(token);
            ColorSchemes.Clear(); foreach (var scheme in schemes.OrderByDescending(item => item.UpdatedAtUtc)) ColorSchemes.Add(scheme);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        { StatusText = "色彩方案未能加载；原文件已保留。"; }
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
    private void SetParameter(double value, Func<ReferenceLookParameters, ReferenceLookParameters> update, ref double fallback, string nodeKey)
    {
        value = Math.Clamp(value, 0, 100); fallback = value;
        if (SelectedLook is not null)
        {
            _selectedLook = SelectedLook with { Parameters = update(SelectedLook.Parameters), UpdatedAt = DateTimeOffset.UtcNow };
            OnPropertyChanged(nameof(SelectedLook)); OnPropertyChanged(nameof(CurrentLookText)); CopyParameters(_selectedLook.Parameters);
            OnPropertyChanged(nameof(HasSessionAdjustment)); OnPropertyChanged(nameof(SessionAdjustmentText)); SaveCurrentAdjustmentCommand.RaiseCanExecuteChanged(); RestoreSchemeCommand.RaiseCanExecuteChanged();
        }
        SetNodeNumeric(ColorStudioNodeType.ReferenceMatch, nodeKey, value);
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
        if (_persistedLook is not null) { _selectedLook = _persistedLook; CopyParameters(_persistedLook.Parameters); CopyFilm(_persistedLook.Film ?? new()); OnPropertyChanged(nameof(SelectedLook)); }
        if (_persistedStack is not null) AdjustmentStack = _persistedStack.DeepClone();
        OnPropertyChanged(nameof(HasSessionAdjustment)); OnPropertyChanged(nameof(SessionAdjustmentText)); SaveCurrentAdjustmentCommand.RaiseCanExecuteChanged(); RestoreSchemeCommand.RaiseCanExecuteChanged(); _ = DebouncedRenderAsync();
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
        Interlocked.Increment(ref _pendingDebounceWork);
        try
        {
            var revision = Interlocked.Increment(ref _revision); await Task.Delay(100);
            if (revision == Volatile.Read(ref _revision)) await RenderAsync(interactive: true);
            await Task.Delay(300);
            if (revision + 1 == Volatile.Read(ref _revision)) await RenderAsync(interactive: false);
        }
        finally { Interlocked.Decrement(ref _pendingDebounceWork); OnPropertyChanged(nameof(IsSettled)); }
    }
    private async Task RenderAsync(CancellationToken outer = default, bool interactive = false)
    {
        var source = interactive ? _interactiveSource ?? _source : _source; var look = SelectedLook; var asset = _assetId;
        var stack = AdjustmentStack.Nodes.Count > 0 ? AdjustmentStack.DeepClone() : AdjustmentStack;
        var selection = ShowSelection ? SelectedAdjustmentNode : null;
        _render?.Cancel();
        var revision = Interlocked.Increment(ref _revision);
        if (!Enabled || source is null || (look is null && AdjustmentStack.Nodes.Count == 0)) { MatchedImage = null; StatusText = !Enabled ? "现场监看仿色未开启。" : source is null ? "请选择待调色照片。" : "请添加参考图片或选择色彩方案。"; RaiseViewProperties(); return; }
        _render?.Dispose(); _render = CancellationTokenSource.CreateLinkedTokenSource(outer, _lifetime.Token);
        var renderToken = _render.Token;
        HasError = false; StatusText = interactive ? "正在生成快速预览…" : "正在生成高质量预览…";
        BeginBusy();
        var previousFrame = MatchedImage;
        try
        {
            BitmapSource image;
            if (stack.Nodes.Count > 0)
            {
                StatusText = "正在应用调整节点…";
                image = await Task.Run(() => ColorStudioBitmapRenderer.Render(source, stack, look, renderToken), renderToken);
                if (selection is { Type: ColorStudioNodeType.ColorRange } selected)
                {
                    StatusText = "正在显示选区…";
                    image = await Task.Run(() => ColorStudioBitmapRenderer.RenderSelection(source, stack, look, selected, renderToken), renderToken);
                }
            }
            else
            {
                var rendered = await _preview.RenderWithResultAsync(source, look!, renderToken); image = rendered.Image;
                if (FilmSettings.Enabled) { StatusText = "正在应用胶片质感…"; image = await _preview.ApplyFilmAsync(image, FilmSettings, renderToken); }
            }
            if (PostProcessor is not null) image = await PostProcessor(image, renderToken);
            if (revision != Volatile.Read(ref _revision) || asset != _assetId) return;
            if (ColorStudioAcceptanceFixture.Requested) NativeRenderedOrder = stack.Nodes.Select(node => node.Name).ToArray();
            MatchedImage = image; StatusText = "现场监看仿色已更新；RAW/JPEG 源文件未修改。"; RaiseViewProperties();
        }
        catch (OperationCanceledException) { if (revision == Volatile.Read(ref _revision)) { State = ProcessingState.Cancelled; StatusText = "已停止处理。"; } }
        catch (Exception) { if (revision == Volatile.Read(ref _revision)) { State = ProcessingState.Failed; HasError = true; MatchedImage = previousFrame; StatusText = "处理失败，请重试。已保留上一张有效预览。"; RaiseViewProperties(); } }
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
        if (!_syncingStack && AdjustmentStack.Nodes.Count > 0)
            ChangeStack(nodes => nodes.Select(node => node.Type == ColorStudioNodeType.Film ? node with { FilmSettings = value, Enabled = value.Enabled } : node).ToArray());
        _ = DebouncedRenderAsync();
    }
    private void CopyFilm(PixelTartFilmSettings value)
    {
        FilmSettings = value;
        foreach (var name in new[] { nameof(FilmEnabled), nameof(FilmProfileId), nameof(FilmProfileAmount), nameof(FilmGrainAmount), nameof(FilmGrainSize), nameof(FilmHalationAmount), nameof(FilmBloomAmount), nameof(FilmVignetteAmount), nameof(FilmSurfaceAmount), nameof(FilmTextureId), nameof(FilmTextureAmount), nameof(FilmSeed) }) OnPropertyChanged(name);
    }
    private void RaiseViewProperties(){foreach(var name in new[]{nameof(ShowOriginal),nameof(ShowMatched),nameof(ShowSplit),nameof(ShowSideBySide),nameof(EffectiveViewMode)})OnPropertyChanged(name);}
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
