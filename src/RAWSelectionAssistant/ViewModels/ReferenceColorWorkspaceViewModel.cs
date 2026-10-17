using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class ReferenceColorWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly IDialogService _dialogs;
    private readonly RawMatchTiff16ProductPipeline _rawPipeline;
    private readonly MatchV4ProductExecutor? _matchV4Executor;
    private BitmapSource? _targetImage;
    private string _targetName = "尚未选择待调色照片";
    private string _statusText = "选择待调色照片，再添加希望借用色彩与影调的参考图片。源照片始终只读。";
    private bool _isLoading;
    private ReferenceTargetItem? _activeTarget;
    private long _activationRevision;
    private CancellationTokenSource? _activationCancellation;
    private readonly LinkedList<(string Path, long Stamp, BitmapSource Image)> _sourceCache = new();
    private int _loadingActivations;
    private CancellationTokenSource? _exportCancellation;
    private int _exportCompleted;
    private int _exportTotal;
    private string _exportStatus = "";
    private string _exportFailureSummary = "";
    private object?[] _statusArguments = [], _exportArguments = [];
    private void SetStatus(string key, params object?[] args) { _statusText = key; _statusArguments = args; OnPropertyChanged(nameof(StatusText)); }
    private void SetExportStatus(string key, params object?[] args) { _exportStatus = key; _exportArguments = args; OnPropertyChanged(nameof(ExportStatus)); }
    private string? _lastExportDirectory;
    private ReferenceTargetItem? _failedTarget;
    private bool _nodeSyncOpen;
    private sealed record PendingSync(string SourceName, ReferenceTargetItem[] Targets, int VisibleSelected, int HiddenSelected, TargetAdjustment Adjustment);
    private PendingSync? _pendingSync;
    public int PendingSyncTargetCount => _pendingSync?.Targets.Length ?? 0;
    public string PendingSyncSummary => _pendingSync is { } request
        ? string.Format(StudioLocalizationService.Current["SyncFrozenSummary"], request.SourceName, request.VisibleSelected, request.Targets.Length, request.HiddenSelected)
        : "";
    private void FreezeSync()
    {
        if (ActiveTarget is null || IsProfessionalRaw) { _pendingSync = null; return; }
        var snapshot = new ReferenceTargetItem(ActiveTarget.Path);
        Editor.CopyCurrentLookTo([snapshot]);
        _pendingSync = new(ActiveTarget.FileName, SyncDestinationTargets.ToArray(), SelectedTargetCount, HiddenSelectedTargetCount, CaptureAdjustment(snapshot));
        OnPropertyChanged(nameof(PendingSyncSummary)); OnPropertyChanged(nameof(PendingSyncTargetCount));
    }
    private string _syncFeedback = "";
    private long _syncFeedbackRevision;
    private readonly HashSet<Guid> _nodeSyncSelection = [];
    private readonly ObservableCollection<NodeSyncChoice> _nodeSyncChoices = [];
    private readonly ObservableCollection<AdjustmentSyncChoice> _adjustmentSyncChoices = [];
    private ColorSpaceVisualizationModel? _colorSpaceModel;
    private VisualPixelBuffer? _colorSpaceSourceBuffer;
    private long _colorSpaceRevision;
    private IReadOnlyList<int> _highlightedPixels = [];
    private readonly Func<IAssetLibraryRepository?>? _assetRepositoryFactory;
    private readonly HashSet<Guid> _hydratingMetadata = [];
    private readonly HashSet<ReferenceTargetItem> _observedTargets = [];
    private Task _metadataWork = Task.CompletedTask;
    private bool _disposed;

    private ReferenceTargetItem? _copiedAdjustments;
    public Action<IReadOnlyList<string>>? OpenPublishing { get; set; }
    public bool HasCopiedAdjustments => _copiedAdjustments is not null;
    public RelayCommand CopyAdjustmentsCommand { get; }
    public RelayCommand ApplyAdjustmentsCommand { get; }
    public AsyncRelayCommand PreparePublishingCommand { get; }

    public void CopyCurrentAdjustments()
    {
        if (ActiveTarget is null || IsProfessionalRaw) return;
        _copiedAdjustments = new ReferenceTargetItem(ActiveTarget.Path);
        Editor.CopyCurrentLookTo([_copiedAdjustments]);
        OnPropertyChanged(nameof(HasCopiedAdjustments));
        ApplyAdjustmentsCommand.RaiseCanExecuteChanged();
        StatusText = "已复制当前调整；评分、颜色和素材关系不会复制。";
    }

    public void ApplyCopiedAdjustments()
    {
        if (_copiedAdjustments is not { } source) return;
        var destinations = SelectedTargets.Where(t=>t.RawDefinition is null).ToArray();
        RememberBatchAdjustment(destinations);
        foreach (var item in destinations)
        {
            item.AppliedLookSnapshot = source.AppliedLookSnapshot is { } look ? look with { ReferenceSources = look.ReferenceSources.Select(reference => reference with { }).ToArray() } : null;
            item.ColorAdjustmentStackSnapshot = source.ColorAdjustmentStackSnapshot?.DeepClone();
            item.FilmSettingsSnapshot = source.FilmSettingsSnapshot is { } film ? film with { } : null;
            item.EngineSnapshot = source.EngineSnapshot; item.ExecutionModeSnapshot = source.ExecutionModeSnapshot;
            item.Status = ReferenceTargetStatus.Synced;
        }
        RefreshActiveSnapshot(destinations);
    }

    public async Task RemoveSelectedFromBatchAsync()
    {
        var selected = SelectedTargets.ToArray();
        if (selected.Length == 0 || IsExporting || IsInstantGenerating) return;
        if (!_dialogs.Confirm(StudioLocalizationService.Current.Format("RemoveBatchConfirm", selected.Length), StudioLocalizationService.Current["移除批次照片"])) return;
        var activeRemoved = ActiveTarget is not null && selected.Contains(ActiveTarget);
        foreach (var item in selected) Targets.Remove(item);
        if (activeRemoved)
        {
            ActiveTarget = null;
            if (Targets.FirstOrDefault() is { } next) { next.IsSelected = true; await ActivateTargetAsync(next); }
            else { TargetImage = null; TargetName = "尚未选择待调色照片"; await Editor.SetSourceAsync(null, null); }
        }
    }

    private async Task PreparePublishingAsync()
    {
        if (OpenPublishing is null || IsExporting || IsInstantGenerating) return;
        var items = SelectedTargets.ToArray(); if (items.Length == 0) return;
        if (ActiveTarget is { } active && items.Contains(active)) Editor.CopyCurrentLookTo([active]);
        foreach(var target in items) CaptureEditVersion(target);
        var frozen = items.Select(item => (Item: item, VersionId: item.ActiveEditVersionId!.Value, Look: item.AppliedLookSnapshot, Stack: item.ColorAdjustmentStackSnapshot?.DeepClone(), Film: item.FilmSettingsSnapshot, Engine: item.EngineSnapshot, Mode: item.ExecutionModeSnapshot, RawDefinition:item.RawDefinition, RawInput:item.RawInput)).ToArray();
        var directory = Path.Combine(Path.GetTempPath(), "PixelTartPublishing", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _exportCancellation = new CancellationTokenSource(); RaiseExportCommands();
        var paths = new List<string>(); var failures = new List<string>();
        try
        {
            foreach (var item in frozen)
            {
                _exportCancellation.Token.ThrowIfCancellationRequested();
                try
                {
                    if (!await ValidateTargetIdentityAsync(item.Item, _exportCancellation.Token)) throw new IOException(item.Item.Error);
                    var raw = RawMatchTiff16ProductPipeline.IsRaw(item.Item.Path);
                    var itemDirectory = Path.Combine(directory, item.Item.Id.ToString());
                    Directory.CreateDirectory(itemDirectory);
                    var path = Path.Combine(itemDirectory, Path.GetFileNameWithoutExtension(item.Item.Path) + (raw || item.RawDefinition is not null ? ".tif" : ".png"));
                    if(item.RawDefinition is {} professional)await ExportProfessionalRawAsync(item.Item,professional,path,StudioExportFormat.Tiff,_exportCancellation.Token);
                    else if (raw)
                    {
                        var master = item.Item.RawMaster ??= await DecodeStudioRawAsync(item.Item.Path, _exportCancellation.Token);
                        await ExportFrozenRawAsync(master, path, item.Look, item.Stack, item.Film, item.Engine, item.Mode, _exportCancellation.Token);
                    }
                    else
                    {
                        if (item.Engine == ColorStudioMatchEngine.MatchV4Beta) throw new NotSupportedException("V4 Beta 输出需要 RAW 高精度源；请切换稳定 V3。");
                        var bitmap = await Editor.ProcessForExportAsync(item.Item.Path, item.Look, item.Film, _exportCancellation.Token, item.Stack);
                        await Task.Run(() => { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); encoder.Save(file); }, _exportCancellation.Token);
                    }
                    await StudioExportReceiptStore.WriteAsync(path,new(Guid.NewGuid(),item.Item.LibraryId,item.Item.AssetId,item.Item.SourceHash,item.VersionId,item.RawDefinition?.ProcessingVersion ?? item.Stack?.ProcessingVersion??1,"Publishing intermediate",path,"",DateTimeOffset.UtcNow,item.RawDefinition is null ? null : new ExportRecipeSnapshot(1,".tif",16,"Original",item.Item.LastProfessionalOutput?.Width??item.Item.PixelWidth,item.Item.LastProfessionalOutput?.Height??item.Item.PixelHeight,null,null,"None","sRGB","TIFF orientation preserved","Not applied",RawDefinition:item.RawDefinition,RawInput:item.RawInput,OutputValidation:System.Text.Json.JsonSerializer.Serialize(item.Item.LastProfessionalOutput))),_exportCancellation.Token);
                    paths.Add(path); SetExportStatus("PrepareProgress", paths.Count + failures.Count, items.Length);
                }
                catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidOperationException)
                { failures.Add(item.Item.FileName); }
            }
            ExportFailureSummary = failures.Count == 0 ? "" : StudioLocalizationService.Current.Format("PrepareFailures", string.Join(", ", failures));
            SetExportStatus("PrepareFinished", paths.Count, items.Length, failures.Count);
            if (failures.Count > 0) _dialogs.ShowInfo(ExportStatus + "\n" + ExportFailureSummary);
            if (paths.Count > 0) OpenPublishing(paths);
        }
        catch (OperationCanceledException) { ExportStatus = "已停止准备发布"; }
        finally { _exportCancellation.Dispose(); _exportCancellation = null; RaiseExportCommands(); }
    }

    public ReferenceColorWorkspaceViewModel(IDialogService dialogs, IReferenceRenderBackend? renderBackend = null, IRawDecoder? rawDecoder = null,
        MatchV4ProductExecutor? matchV4Executor = null,
        Func<IAssetLibraryRepository?>? assetRepositoryFactory = null, StudioVersionRepository? versionRepository = null, IProfessionalRawDecoder? professionalRawDecoder=null)
    {
        SecondExposureDecoder.EnsureRegistered();
        _professionalDecoder=professionalRawDecoder??new ProfessionalRawDecoder();
        _versionRepository=versionRepository;
        _dialogs = dialogs;
        PropertyChangedEventManager.AddHandler(StudioLocalizationService.Current, OnStudioLanguageChanged, nameof(StudioLocalizationService.Language));
        _matchV4Executor = matchV4Executor;
        _assetRepositoryFactory = assetRepositoryFactory;
        _rawPipeline = new RawMatchTiff16ProductPipeline(rawDecoder ?? new LibRawDecoder());
        InitializeFilmstrip();
        InitializeSessionCommands();
        Editor = new TetherReferenceModeViewModel(
            new ReferenceLookStore(Path.Combine(AppDataPaths.DataDirectory, "ProjectVisuals")), dialogs, allowReferenceManagement: true,
            renderBackend: renderBackend, matchV4Executor: matchV4Executor);
        Editor.Enabled = true;
        InitializeRangeSelection();
        BuildColorSpaceModelCommand = new AsyncRelayCommand(_ => BuildColorSpaceModelAsync(), _ => Editor.SourceImage is not null);
        ChooseTargetCommand = new AsyncRelayCommand(_ => ChooseTargetAsync());
        StopProcessingCommand = new RelayCommand(_ => {Editor.StopProcessing();_activationCancellation?.Cancel();Interlocked.Increment(ref _activationRevision);InvalidateProfessionalPreview();}, _ => Editor.IsBusy || IsLoading || !ProfessionalDetailWork.IsCompleted);
        CopyAdjustmentsCommand = new RelayCommand(_ => CopyCurrentAdjustments(), _ => ActiveTarget is not null && !IsLoading && !IsProfessionalRaw);
        ApplyAdjustmentsCommand = new RelayCommand(_ => ApplyCopiedAdjustments(), _ => HasCopiedAdjustments && SelectedTargets.Any() && !IsLoading);
        PreparePublishingCommand = new AsyncRelayCommand(_ => PreparePublishingAsync(), _ => OpenPublishing is not null && SelectedTargets.Any() && !IsExporting && !IsInstantGenerating);
        SyncSelectedCommand = new RelayCommand(_ => SyncCurrentTo(SyncDestinationTargets), _ => !IsProfessionalRaw && ActiveTarget is not null && SyncDestinationTargets.Count > 0 && !IsLoading);
        SyncAllCommand = new RelayCommand(_ => SyncCurrentTo(VisibleTargets.Where(x => !ReferenceEquals(x, ActiveTarget)).ToArray()), _ => ActiveTarget is not null && VisibleTargets.Any(x => !ReferenceEquals(x, ActiveTarget)) && !IsLoading);
        OpenNodeSyncCommand = new RelayCommand(_ => { FreezeSync(); _nodeSyncSelection.Clear(); _nodeSyncChoices.Clear(); foreach (var node in _pendingSync?.Adjustment.Stack?.Nodes ?? []) { _nodeSyncSelection.Add(node.Id); _nodeSyncChoices.Add(new NodeSyncChoice(node.Id, node.Name, true)); } NodeSyncOpen = true; }, _ => CanSyncSelectedNodes);
        ToggleNodeSyncCommand = new RelayCommand(value => { if (value is not NodeSyncChoice choice) return; choice.Selected = !choice.Selected; if (choice.Selected) _nodeSyncSelection.Add(choice.Id); else _nodeSyncSelection.Remove(choice.Id); });
        ConfirmNodeSyncCommand = new RelayCommand(_ =>
        {
            if (IsLoading || _pendingSync is not { Targets.Length: > 0, Adjustment.Stack: not null } request || request.Targets.Any(x => !Targets.Contains(x))) return;
            var selected = _nodeSyncChoices.Where(choice => choice.Selected).Select(choice => choice.Id).ToHashSet();
            if (selected.Count == 0) return;
            var targets = request.Targets;
            SyncSelectedColorNodes(request.Adjustment.Stack, selected, targets, request.Adjustment);
            NodeSyncOpen = false;
            _ = ShowSyncFeedbackAsync(StudioLocalizationService.Current.Format("SyncedNodes", selected.Count, targets.Length));
        });
        CancelNodeSyncCommand = new RelayCommand(_ => NodeSyncOpen = false);
        OpenAdjustmentCopyCommand = new RelayCommand(_ =>
        {
            FreezeSync();
            _adjustmentSyncChoices.Clear();
            foreach (var type in Enum.GetValues<ColorStudioNodeType>())
                _adjustmentSyncChoices.Add(new AdjustmentSyncChoice(type, AdjustmentTypeName(type), true));
            AdjustmentCopyOpen = true;
        }, _ => CanSyncSelectedNodes);
        ConfirmAdjustmentCopyCommand = new RelayCommand(_ =>
        {
            if (IsLoading || _pendingSync is not { Targets.Length: > 0, Adjustment.Stack: not null } request || request.Targets.Any(x => !Targets.Contains(x))) return;
            var selected = _adjustmentSyncChoices.Where(choice => choice.Selected).Select(choice => choice.Type).ToHashSet();
            if (selected.Count == 0) return;
            var targets = request.Targets;
            ApplySelectedAdjustments(selected, targets, request.Adjustment);
            AdjustmentCopyOpen = false;
            _ = ShowSyncFeedbackAsync(StudioLocalizationService.Current.Format("SyncedTypes", selected.Count, targets.Length));
        }, _ => !IsLoading && _pendingSync?.Targets.Length > 0 && _adjustmentSyncChoices.Any(choice => choice.Selected));
        CancelAdjustmentCopyCommand = new RelayCommand(_ => AdjustmentCopyOpen = false);
        ActivateTargetCommand = new AsyncRelayCommand(value => value is ReferenceTargetItem target ? ActivateTargetAsync(target) : Task.CompletedTask, allowConcurrent: true);
        ExportSelectedCommand = new AsyncRelayCommand(_ => ExportAsync(SelectedTargets.ToArray()), _ => SelectedTargets.Any() && !IsExporting && !IsInstantGenerating);
        ExportAllCommand = new AsyncRelayCommand(_ => ExportAsync(VisibleTargets.ToArray()), _ => VisibleTargets.Count > 0 && !IsExporting && !IsInstantGenerating);
        StopExportCommand = new RelayCommand(_ => _exportCancellation?.Cancel(), _ => IsExporting);
        RetryFailedTargetCommand = new AsyncRelayCommand(_ => FailedTarget is null ? Task.CompletedTask : ActivateTargetAsync(FailedTarget), _ => FailedTarget is not null && !IsLoading);
        RetryFailedExportCommand = new AsyncRelayCommand(_ => RetryFailedExportsAsync(), _ => Targets.Any(item => item.ExportStatus is ReferenceExportStatus.Failed or ReferenceExportStatus.Partial) && !IsExporting && !string.IsNullOrWhiteSpace(_lastExportDirectory));
        Editor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TetherReferenceModeViewModel.IsBusy)) StopProcessingCommand!.RaiseCanExecuteChanged();
            if (args.PropertyName is nameof(TetherReferenceModeViewModel.IsProMode) or nameof(TetherReferenceModeViewModel.AdjustmentStack)) RefreshSyncAvailability();
            if (args.PropertyName is nameof(TetherReferenceModeViewModel.SourceImage) or nameof(TetherReferenceModeViewModel.MatchedImage) or nameof(TetherReferenceModeViewModel.EffectiveViewMode))
                SchedulePreviewAnalysis();
            if (args.PropertyName is nameof(TetherReferenceModeViewModel.MatchedImage) or nameof(TetherReferenceModeViewModel.AdjustmentStack)
                or nameof(TetherReferenceModeViewModel.SelectedLook) or nameof(TetherReferenceModeViewModel.FilmSettings)
                or nameof(TetherReferenceModeViewModel.MatchEngine) or nameof(TetherReferenceModeViewModel.MatchV4ExecutionMode)
                && !IsLoading && !_applyingActiveSnapshot && ActiveTarget is { } thumbnailTarget)
            {
                Editor.CopyCurrentLookTo([thumbnailTarget]);
                thumbnailTarget.Status = ReferenceTargetStatus.Adjusted;
                ScheduleTargetThumbnail(thumbnailTarget);
            }
        };
        Targets.CollectionChanged += (_, args) =>
        {
            foreach (var removed in _observedTargets.Where(item => !Targets.Contains(item)).ToArray())
            {
                removed.PropertyChanged -= OnTargetSelectionChanged;
                removed.PropertyChanged -= OnTargetMetadataChanged;
                removed.PropertyChanged -= OnTargetAdjustmentChanged;
                CancelTargetThumbnail(removed.Id);
                _observedTargets.Remove(removed);
            }
            if (args.NewItems is not null) foreach (ReferenceTargetItem target in args.NewItems)
            {
                _observedTargets.Add(target);
                target.PropertyChanged += OnTargetSelectionChanged;
                target.PropertyChanged += OnTargetMetadataChanged;
                target.PropertyChanged += OnTargetAdjustmentChanged;
                QueueMetadata(() => HydrateAssetMetadataAsync(target));
            }
            RefreshFilmstrip();
        };
    }

    public TetherReferenceModeViewModel Editor { get; }
    private void OnStudioLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(FilmstripSummary)); OnPropertyChanged(nameof(SyncSummary)); OnPropertyChanged(nameof(PendingSyncSummary));
        OnPropertyChanged(nameof(FilterScopes)); OnPropertyChanged(nameof(AnalysisLabel)); OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(QuickExportFormatSummary));
        OnPropertyChanged(nameof(ExportStatus));
    }
    public AsyncRelayCommand BuildColorSpaceModelCommand { get; }
    public ColorSpaceVisualizationModel? ColorSpaceModel
    {
        get => _colorSpaceModel;
        private set { if (SetProperty(ref _colorSpaceModel, value)) OnPropertyChanged(nameof(HasColorSpaceModel)); }
    }
    public bool HasColorSpaceModel => ColorSpaceModel is not null;
    public IReadOnlyList<int> HighlightedPixels { get => _highlightedPixels; private set => SetProperty(ref _highlightedPixels, value); }
    public int HighlightImageWidth => _colorSpaceSourceBuffer?.Width ?? 0;
    public int HighlightImageHeight => _colorSpaceSourceBuffer?.Height ?? 0;
    public async Task BuildColorSpaceModelAsync(CancellationToken token = default)
    {
        await RefreshPreviewAnalysisAsync(token);
    }

    public void HighlightImageSample(VisualRgb24 sample)
    {
        HighlightColor(OklabColorSpace.FromSrgb(sample));
    }
    public void HighlightCloudSelection(int pointIndex)
    {
        if (pointIndex < 0) { ClearColorSpaceHighlight(); return; }
        if (ColorSpaceModel is not { } model || _colorSpaceSourceBuffer is null || pointIndex >= model.Source.Points.Count) return;
        HighlightColor(model.Source.Points[pointIndex].Lab);
    }
    public void ClearColorSpaceHighlight() { _selectedInspectionColor = null; HighlightedPixels = []; HighlightWeights = []; OnPropertyChanged(nameof(HighlightedPixels)); ColorSpaceSelectionChanged?.Invoke(this, ColorSpaceSelection.None); }
    public event EventHandler<ColorSpaceSelection>? ColorSpaceSelectionChanged;
    private static VisualPixelBuffer ToVisualBuffer(BitmapSource source)
    {
        // Preview-only mask/model stays bounded; never mutate or resample export input.
        var edge = Math.Max(source.PixelWidth, source.PixelHeight);
        if (edge > 768) source = new TransformedBitmap(source, new ScaleTransform(768d / edge, 768d / edge));
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var bytes = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(bytes, converted.PixelWidth * 4, 0);
        var rgb = new byte[converted.PixelWidth * converted.PixelHeight * 3]; var alpha = new byte[rgb.Length / 3];
        for (var i = 0; i < alpha.Length; i++) { rgb[i*3]=bytes[i*4+2]; rgb[i*3+1]=bytes[i*4+1]; rgb[i*3+2]=bytes[i*4]; alpha[i]=bytes[i*4+3]; }
        return new(converted.PixelWidth, converted.PixelHeight, rgb, alpha);
    }
    public AsyncRelayCommand ChooseTargetCommand { get; }
    public RelayCommand StopProcessingCommand { get; }
    public RelayCommand SyncSelectedCommand { get; }
    public RelayCommand SyncAllCommand { get; }
    public RelayCommand OpenNodeSyncCommand { get; }
    public RelayCommand ToggleNodeSyncCommand { get; }
    public RelayCommand ConfirmNodeSyncCommand { get; }
    public RelayCommand CancelNodeSyncCommand { get; }
    public RelayCommand OpenAdjustmentCopyCommand { get; }
    public RelayCommand ConfirmAdjustmentCopyCommand { get; }
    public RelayCommand CancelAdjustmentCopyCommand { get; }
    public bool CanSyncSelectedNodes => !IsProfessionalRaw && Editor.IsProMode && ActiveTarget is not null && SyncDestinationTargets.Count > 0 && Editor.AdjustmentNodes.Count > 0 && !IsLoading;
    public string SyncFeedback { get => _syncFeedback; private set { if (SetProperty(ref _syncFeedback, value)) OnPropertyChanged(nameof(HasSyncFeedback)); } }
    public bool HasSyncFeedback => !string.IsNullOrEmpty(SyncFeedback);
    private async Task ShowSyncFeedbackAsync(string message)
    {
        var revision = ++_syncFeedbackRevision; SyncFeedback = message;
        await Task.Delay(5000);
        if (revision == _syncFeedbackRevision) SyncFeedback = "";
    }
    private void OnTargetSelectionChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName is nameof(ReferenceTargetItem.IsSelected) or nameof(ReferenceTargetItem.Rating) or nameof(ReferenceTargetItem.ColorLabel)) RefreshFilmstrip(); }
    private void OnTargetMetadataChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed || sender is not ReferenceTargetItem target || _hydratingMetadata.Contains(target.Id)) return;
        if (e.PropertyName == nameof(ReferenceTargetItem.AssetId))
        {
            target.MetadataDatabasePath = null;
            QueueMetadata(() => HydrateAssetMetadataAsync(target));
            return;
        }
        if (target.AssetId is not Guid assetId) return;
        // Capture each requested value before yielding. Rating and color writes share a FIFO,
        // so neither a later click nor a refresh can overtake an earlier database mutation.
        if (e.PropertyName == nameof(ReferenceTargetItem.Rating))
        {
            var rating = target.Rating;
            var expected=target.AnnotationSnapshot;
            QueueMetadata(() => PersistAssetMetadataAsync(target, assetId, rating, null,expected));
        }
        else if (e.PropertyName == nameof(ReferenceTargetItem.ColorLabel))
        {
            var color = target.ColorLabel ?? "";
            var expected=target.AnnotationSnapshot;
            QueueMetadata(() => PersistAssetMetadataAsync(target, assetId, null, color,expected));
        }
    }
    public Func<string, Task>? AssetMetadataSaved { get; set; }
    public Task FlushMetadataAsync() => _metadataWork;
    private void QueueMetadata(Func<Task> operation) => _metadataWork = RunMetadataAsync(_metadataWork, operation);
    private async Task RunMetadataAsync(Task previous, Func<Task> operation)
    {
        try
        {
            await previous;
            await operation();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or Microsoft.Data.Sqlite.SqliteException)
        { StatusText = "素材库标记未能保存或读取，请重试。"; }
    }
    private IAssetLibraryRepository? OpenMetadataRepository(ReferenceTargetItem target) =>
        target.MetadataDatabasePath is { } path ? new SqliteAssetLibraryRepository(path) : _assetRepositoryFactory?.Invoke();
    private async Task PersistAssetMetadataAsync(ReferenceTargetItem target, Guid assetId, int? rating, string? color,PhotographerAnnotation? capturedExpected)
    {
        if (target.AssetId != assetId) return;
        if (target.LibraryId.HasValue && !await ValidateTargetIdentityAsync(target, CancellationToken.None)) { target.MetadataStatus = "身份未确认，标注未写入"; return; }
        await using var repository = OpenMetadataRepository(target);
        if (repository is null) { target.MetadataStatus = "素材库不可用，尚未保存"; return; }
        await repository.InitializeAsync();
        if (await repository.GetAssetAsync(assetId) is not { } metadataAsset ||
            (target.LibraryId is null && !string.Equals(Path.GetFullPath(metadataAsset.SourcePath), Path.GetFullPath(target.Path), StringComparison.OrdinalIgnoreCase)))
        {
            target.MetadataStatus = "素材库中未找到此照片，尚未保存";
            return;
        }
        target.MetadataDatabasePath = repository.DatabasePath;
        if(capturedExpected is not {} expected){target.MetadataStatus="标注未读取，请刷新后重试";return;}
        while(target.OwnAnnotationWrites.TryGetValue(expected.Revision,out var own)&&own.Revision>expected.Revision)expected=own;
        var written=await new PhotographerAnnotationStore(new AssetLibraryDatabase(repository.DatabasePath)).CompareExchangeAsync(expected,expected with{Rating=rating??expected.Rating,Color=color??expected.Color});
        if(written.UndoToken is {} undo){_annotationUndo.Push((repository.DatabasePath,undo));_annotationRedo.Clear();}
        if(written.Applied)target.OwnAnnotationWrites[expected.Revision]=written.Current;
        target.AnnotationSnapshot=written.Current;
        target.MetadataStatus=written.Applied?"已保存到素材库":"标注冲突，已刷新权威状态";
        if(!written.Applied){_hydratingMetadata.Add(target.Id);try{target.Rating=written.Current.Rating;target.ColorLabel=written.Current.Color;}finally{_hydratingMetadata.Remove(target.Id);}}        if (AssetMetadataSaved is { } refresh) await refresh(repository.DatabasePath);
    }
    private async Task HydrateAssetMetadataAsync(ReferenceTargetItem target)
    {
        if (target.AssetId is not Guid assetId) { target.MetadataStatus = "仅本次会话 · 未加入素材库"; return; }
        try
        {
            if (target.LibraryId.HasValue && !await ValidateTargetIdentityAsync(target, CancellationToken.None)) { target.MetadataStatus = "身份未确认，未读取其他素材标注"; return; }
            await using var repository = OpenMetadataRepository(target);
            if (repository is null) { target.MetadataStatus = "素材库不可用"; return; }
            await repository.InitializeAsync();
            var asset = await repository.GetAssetAsync(assetId);
            if (target.AssetId != assetId) return;
            if (asset is null || (target.LibraryId is null && !string.Equals(Path.GetFullPath(asset.SourcePath), Path.GetFullPath(target.Path), StringComparison.OrdinalIgnoreCase))) { target.MetadataStatus = "素材库中未确认此照片身份"; return; }
            target.MetadataDatabasePath = repository.DatabasePath;
            _hydratingMetadata.Add(target.Id);
            target.AnnotationSnapshot=new(asset.AssetId,asset.Rating,asset.ColorLabel,asset.Comment,asset.AnnotationRevision);
            target.Rating = asset.Rating;
            target.ColorLabel = string.IsNullOrWhiteSpace(asset.ColorLabel) ? null : asset.ColorLabel;
            target.MetadataStatus = "素材库标记";
        }
        finally { _hydratingMetadata.Remove(target.Id); }
    }
    private void RefreshSyncAvailability()
    {
        OnPropertyChanged(nameof(PhotoGradeRasterToolsEnabled));OnPropertyChanged(nameof(PhotoGradeCanSyncStep));OnPropertyChanged(nameof(InstantEditingAllowed));
        OnPropertyChanged(nameof(SelectedTargetCount)); OnPropertyChanged(nameof(CanSyncSelectedNodes));
        OnPropertyChanged(nameof(VisibleTargetCount)); OnPropertyChanged(nameof(HiddenSelectedTargetCount)); OnPropertyChanged(nameof(FilmstripSummary)); OnPropertyChanged(nameof(SyncSummary));
        SetSelectedRatingCommand?.RaiseCanExecuteChanged(); SetSelectedColorLabelCommand?.RaiseCanExecuteChanged();
        SaveSessionCommand?.RaiseCanExecuteChanged(); OpenSessionCommand?.RaiseCanExecuteChanged(); RaiseBatchHistory();
        OpenNodeSyncCommand?.RaiseCanExecuteChanged(); SyncSelectedCommand?.RaiseCanExecuteChanged(); SyncAllCommand?.RaiseCanExecuteChanged();
        CopyAdjustmentsCommand?.RaiseCanExecuteChanged(); ApplyAdjustmentsCommand?.RaiseCanExecuteChanged(); PreparePublishingCommand?.RaiseCanExecuteChanged();
        OpenAdjustmentCopyCommand?.RaiseCanExecuteChanged(); ConfirmAdjustmentCopyCommand?.RaiseCanExecuteChanged();
        ExportSelectedCommand?.RaiseCanExecuteChanged(); ExportAllCommand?.RaiseCanExecuteChanged();
    }
    public bool NodeSyncOpen { get => _nodeSyncOpen; set { if (value) AdjustmentCopyOpen = false; SetProperty(ref _nodeSyncOpen, value); } }
    public ObservableCollection<NodeSyncChoice> NodeSyncChoices => _nodeSyncChoices;
    public ObservableCollection<AdjustmentSyncChoice> AdjustmentSyncChoices => _adjustmentSyncChoices;
    public bool AdjustmentCopyOpen { get => _adjustmentCopyOpen; set { if (value) NodeSyncOpen = false; SetProperty(ref _adjustmentCopyOpen, value); } }
    private bool _adjustmentCopyOpen;
    public int SelectedTargetCount => SelectedTargets.Count();
    public void SyncSelectedColorNodes(ColorAdjustmentStack source, IReadOnlySet<Guid> selectedNodeIds, IEnumerable<ReferenceTargetItem> targets)
        => SyncSelectedColorNodes(source, selectedNodeIds, targets, null);
    private void SyncSelectedColorNodes(ColorAdjustmentStack source, IReadOnlySet<Guid> selectedNodeIds, IEnumerable<ReferenceTargetItem> targets, TargetAdjustment? frozen)
    {
        var destinations = targets.Where(t=>t.RawDefinition is null).ToArray();
        var selected = source.Normalize().Nodes.Where(node => selectedNodeIds.Contains(node.Id)).ToArray();
        if (selected.Length == 0) return;
        RememberBatchAdjustment(destinations);
        foreach (var target in destinations)
        {
            target.ColorAdjustmentStackSnapshot = target.ColorAdjustmentStackSnapshot is { } stack
                ? stack.SyncSelectedFrom(source, selectedNodeIds)
                : (source with { Nodes = selected }).DeepClone();
            if (selected.Any(x => x.Type == ColorStudioNodeType.ReferenceMatch))
            {
                var look = frozen is null ? Editor.SelectedLook?.Normalize() : frozen.Look;
                target.AppliedLookSnapshot = look is null ? null : look with { ReferenceSources = look.ReferenceSources.Select(x => x with { }).ToArray() };
                target.EngineSnapshot = frozen?.Engine ?? Editor.MatchEngine; target.ExecutionModeSnapshot = frozen?.Mode ?? Editor.MatchV4ExecutionMode;
            }
            if (selected.Any(x => x.Type == ColorStudioNodeType.Film)) target.FilmSettingsSnapshot = frozen is null ? Editor.FilmSettings with { } : frozen.Film;
            target.Status = ReferenceTargetStatus.Synced;
        }
        RefreshActiveSnapshot(destinations);
    }
    private void ApplySelectedAdjustments(IReadOnlySet<ColorStudioNodeType> selectedTypes, IEnumerable<ReferenceTargetItem> targets, TargetAdjustment frozen)
    {
        var sourceStack = frozen.Stack!;
        var destinations = targets.Where(t=>t.RawDefinition is null).ToArray();
        RememberBatchAdjustment(destinations);
        var look = frozen.Look;
        foreach (var target in destinations)
        {
            target.ColorAdjustmentStackSnapshot = target.ColorAdjustmentStackSnapshot is { } existing
                ? existing.SyncSelectedByTypeFrom(sourceStack, selectedTypes)
                : sourceStack.Nodes.Where(node => selectedTypes.Contains(node.Type)).Select(node => node.Normalize()).ToArray() is { Length: > 0 } selected
                    ? (sourceStack with { Nodes = selected }).Normalize()
                    : null;
            if (selectedTypes.Contains(ColorStudioNodeType.ReferenceMatch))
            {
                target.AppliedLookSnapshot = look is null ? null : look with { ReferenceSources = look.ReferenceSources.Select(source => source with { }).ToArray() };
                target.EngineSnapshot = frozen.Engine; target.ExecutionModeSnapshot = frozen.Mode;
            }
            if (selectedTypes.Contains(ColorStudioNodeType.Film)) target.FilmSettingsSnapshot = frozen.Film;
            target.Status = ReferenceTargetStatus.Synced;
        }
        RefreshActiveSnapshot(destinations);
    }
    private static string AdjustmentTypeName(ColorStudioNodeType type) => type switch
    {
        ColorStudioNodeType.ReferenceMatch => "参考仿色",
        ColorStudioNodeType.ColorRange => "颜色范围",
        ColorStudioNodeType.Film => "胶片",
        ColorStudioNodeType.TransitionBlend => "色彩过渡",
        ColorStudioNodeType.Preset => "预设",
        ColorStudioNodeType.Develop => "影调与细节",
        ColorStudioNodeType.WhiteBalance => "白平衡",
        ColorStudioNodeType.BasicTone => "基础与 HDR 影调",
        ColorStudioNodeType.ColorBalance => "色彩平衡",
        ColorStudioNodeType.Levels => "色阶",
        ColorStudioNodeType.Curve => "曲线",
        ColorStudioNodeType.Details => "细节",
        ColorStudioNodeType.SkinTone => "肤色均匀化",
        _ => type.ToString()
    };
    public AsyncRelayCommand ActivateTargetCommand { get; }
    public AsyncRelayCommand ExportSelectedCommand { get; }
    public AsyncRelayCommand ExportAllCommand { get; }
    public RelayCommand StopExportCommand { get; }
    public AsyncRelayCommand RetryFailedTargetCommand { get; }
    public AsyncRelayCommand RetryFailedExportCommand { get; }
    public ObservableCollection<ReferenceTargetItem> Targets { get; } = [];
    public IEnumerable<ReferenceTargetItem> SelectedTargets => VisibleTargets.Where(item => item.IsSelected);
    public int SelectFilmstripTarget(int index, int anchor, bool shift, bool control)
    {
        if (anchor >= 0 && anchor < VisibleTargets.Count) _selectionAnchorId = VisibleTargets[anchor].Id;
        return SelectVisibleFilmstripTarget(index, shift, control);
    }
    public ReferenceTargetItem? ActiveTarget { get => _activeTarget; private set { if (SetProperty(ref _activeTarget, value)) RefreshSyncAvailability(); } }
    public BitmapSource? TargetImage { get => _targetImage; private set { if (SetProperty(ref _targetImage, value)) OnPropertyChanged(nameof(HasTarget)); } }
    public bool HasTarget => TargetImage is not null;
    public string TargetName { get => _targetName; private set => SetProperty(ref _targetName, value); }
    public string StatusText { get => _statusArguments.Length == 0 ? StudioLocalizationService.Current[_statusText] : StudioLocalizationService.Current.Format(_statusText, _statusArguments); private set { _statusArguments = []; SetProperty(ref _statusText, value); } }
    public bool IsLoading { get => _isLoading || _sessionLoading; private set { if (SetProperty(ref _isLoading, value)) RefreshSyncAvailability(); } }
    public bool IsExporting => _exportCancellation is not null;
    public int ExportCompleted { get => _exportCompleted; private set => SetProperty(ref _exportCompleted, value); }
    public int ExportTotal { get => _exportTotal; private set => SetProperty(ref _exportTotal, value); }
    public string ExportStatus { get => _exportArguments.Length == 0 ? StudioLocalizationService.Current[_exportStatus] : StudioLocalizationService.Current.Format(_exportStatus, _exportArguments); private set { _exportArguments = []; SetProperty(ref _exportStatus, value); } }
    public string ExportFailureSummary { get => _exportFailureSummary; private set => SetProperty(ref _exportFailureSummary, value); }
    public ReferenceTargetItem? FailedTarget { get => _failedTarget; private set { if (SetProperty(ref _failedTarget, value)) { OnPropertyChanged(nameof(HasFailedTarget)); RetryFailedTargetCommand.RaiseCanExecuteChanged(); } } }
    public bool HasFailedTarget => FailedTarget is not null;
    public bool CanRetryFailedExport => Targets.Any(item => item.ExportStatus is ReferenceExportStatus.Failed or ReferenceExportStatus.Partial) && !IsExporting && !string.IsNullOrWhiteSpace(_lastExportDirectory);

    public async Task InitializeAsync(CancellationToken token = default)
    {
        await Editor.LoadAsync(token);
        foreach (var target in Targets) QueueMetadata(() => HydrateAssetMetadataAsync(target));
        await FlushMetadataAsync();
    }

    public async Task AcceptContextAsync(Guid? projectId, Guid? assetId, BitmapSource? source, CancellationToken token = default)
    {
        await Editor.SetProjectAsync(projectId, token);
        if (source is null) return;
        TargetImage = source;
        TargetName = "当前联机照片";
        await Editor.SetSourceAsync(assetId, source, token);
        StatusText = "已从联机拍摄带入当前照片和色彩方案；取消或返回不会改写方案。";
    }

    public async Task LoadTargetAsync(string path, Guid? assetId = null, Guid? libraryId = null, string? sourceHash = null)
    {
        var target = GetOrCreateTarget(path);
        if ((target.LibraryId.HasValue && libraryId.HasValue && target.LibraryId != libraryId) ||
            (target.AssetId.HasValue && assetId.HasValue && target.AssetId != assetId) ||
            (target.SourceHash is not null && sourceHash is not null && !string.Equals(target.SourceHash, sourceHash, StringComparison.OrdinalIgnoreCase)))
        { target.Status = ReferenceTargetStatus.Failed; target.Error = "路径对应的素材身份已变化，保留原编辑目标"; FailedTarget = target; StatusText = target.Error; return; }
        if (assetId is not null) target.AssetId = assetId;
        target.LibraryId = libraryId ?? target.LibraryId;
        target.SourceHash ??= sourceHash;
        target.IsSelected = true;
        // RAW thumbnail and activation must share one session-owned master.  Starting both
        // operations concurrently would allow the ??= assignment below to race and decode
        // the same nondeterministic source twice.
        if (RawMatchTiff16ProductPipeline.IsRaw(target.Path))
        {
            // Discover the selected persistent processing definition before choosing a decoder.
            if(_versionRepository is not null && target.VersionCatalog is null)
            {
                try
                {
                    if(!await ValidateTargetIdentityAsync(target,CancellationToken.None)){FailedTarget=target;target.Status=ReferenceTargetStatus.Failed;StatusText=target.Error??"素材身份不可用";return;}
                    target.SourceHash ??= target.ObservedSourceHash;
                    await LoadPersistentVersionsAsync(target,CancellationToken.None);
                }
                catch(Exception error)when(error is InvalidDataException or IOException or InvalidOperationException)
                {FailedTarget=target;target.Status=ReferenceTargetStatus.Failed;target.Error=error.Message;StatusText=error.Message;return;}
            }
            if (target.Thumbnail is null)
                await LoadThumbnailAsync(target, CancellationToken.None);
            await ActivateTargetAsync(target);
            return;
        }

        // Reserve activation order before asynchronous thumbnail work can complete out of order.
        var activation = ActivateTargetAsync(target);
        var thumbnail = target.Thumbnail is null ? LoadThumbnailAsync(target, CancellationToken.None) : Task.CompletedTask;
        await Task.WhenAll(activation, thumbnail);
    }

    private ReferenceTargetItem GetOrCreateTarget(string path)
    {
        var existing = Targets.FirstOrDefault(target => string.Equals(target.Path, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;
        var item = new ReferenceTargetItem(path);
        Targets.Add(item);
        return item;
    }

    private async Task LoadThumbnailAsync(ReferenceTargetItem item, CancellationToken token)
    {
        try
        {
            if(item.RawDefinition is {} definition)
            {
                var master=item.LinearRawMaster??await DecodeProfessionalInputAsync(item.Path,definition,token);
                if(!string.Equals(master.Contract.SourceSha256,item.SourceHash,StringComparison.OrdinalIgnoreCase))throw new IOException("RAW 缩略图源身份改变。");
                item.LinearRawMaster=master;item.RawInput=master.Contract;
                using var rendered=await Task.Run(()=>ProfessionalRawPipeline.RenderPreview(master,definition,320,token),token);
                item.Thumbnail=StudioQuickExport.ApplyOrientation(RawDisplayBitmapAdapter.ToBitmap(rendered.ToVisualRgb24()),rendered.Orientation);
                item.PixelWidth=master.Image.Width;item.PixelHeight=master.Image.Height;item.Status=ReferenceTargetStatus.Adjusted;
                if(!ReferenceEquals(item,ActiveTarget))item.LinearRawMaster=null;
                return;
            }
            if (RawMatchTiff16ProductPipeline.IsRaw(item.Path))
            {
                var master = item.RawMaster ??= await DecodeStudioRawAsync(item.Path, token);
                var pixels = _rawPipeline.DisplaySource(master.Image, 320, token);
                item.SourceThumbnail = RawDisplayBitmapAdapter.ToBitmap(pixels); item.Thumbnail = item.SourceThumbnail; ScheduleTargetThumbnail(item);
                item.PixelWidth = master.Width; item.PixelHeight = master.Height; item.Status = ReferenceTargetStatus.Pending;
                return;
            }
            await Task.Run(() =>
            {
                var decoded = new BitmapImage();
                decoded.BeginInit(); decoded.CacheOption = BitmapCacheOption.OnLoad; decoded.DecodePixelWidth = 320; decoded.UriSource = new Uri(item.Path); decoded.EndInit(); decoded.Freeze();
                item.SourceThumbnail = decoded; item.Thumbnail = decoded; item.PixelWidth = decoded.PixelWidth; item.PixelHeight = decoded.PixelHeight; item.Status = ReferenceTargetStatus.Pending;
            }, token);
            ScheduleTargetThumbnail(item);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidOperationException) { item.Status = ReferenceTargetStatus.Failed; item.Error = ex is RawDecodeException ? "RAW 相机文件无法进行 16 位解码" : "缩略图无法读取"; }
    }

    private async Task ActivateTargetAsync(ReferenceTargetItem target)
    {
        _secondSelectionCancellation?.Cancel();if(IsInstantGenerating)_instantCancellation?.Cancel();
        InvalidateProfessionalPreview();foreach(var other in Targets.Where(x=>!ReferenceEquals(x,target)))other.LinearRawMaster=null;if(target.RawDefinition is null)target.LinearRawMaster=null;
        var revision = Interlocked.Increment(ref _activationRevision);
        _activationCancellation?.Cancel(); _activationCancellation?.Dispose();
        _activationCancellation = new CancellationTokenSource();
        var token = _activationCancellation.Token;
        if (ActiveTarget is { } previous && !ReferenceEquals(previous, target))
            Editor.CopyCurrentLookTo([previous]);
        try
        {
            Editor.StopProcessing();
            Interlocked.Increment(ref _loadingActivations); IsLoading = true; StopProcessingCommand.RaiseCanExecuteChanged();StatusText = "正在载入活动预览…";
            if (!await ValidateTargetIdentityAsync(target, token)) { target.Status = ReferenceTargetStatus.Failed; FailedTarget = target; StatusText = target.Error ?? "素材身份不可用"; RetryFailedTargetCommand.RaiseCanExecuteChanged(); return; }
            if(target.RawDefinition is not null)
            {
                target.SourceHash ??= target.ObservedSourceHash;await LoadPersistentVersionsAsync(target,token);
                if(revision!=Volatile.Read(ref _activationRevision))return;
                if(target.RawDefinition is not null){await ActivateProfessionalRawAsync(target,revision,token);return;}
            }
            Editor.IndependentRawMode=false;
            var isRaw = RawMatchTiff16ProductPipeline.IsRaw(target.Path);
            var rawMaster = isRaw ? target.RawMaster ??= await DecodeStudioRawAsync(target.Path, token) : null;
            var rawProxy = rawMaster is null ? null : _rawPipeline.PreviewMaster(rawMaster.Image);
            var image = rawProxy is not null ? RawDisplayBitmapAdapter.ToBitmap(rawProxy.ToVisualRgb24()) : await LoadSourcePreviewAsync(target.Path, token);
            if (revision != Volatile.Read(ref _activationRevision)) return;
            target.SourceHash ??= target.ObservedSourceHash;
            await LoadPersistentVersionsAsync(target,token);
            if(revision!=Volatile.Read(ref _activationRevision))return;
            if(target.RawDefinition is not null){target.RawMaster=null;await ActivateProfessionalRawAsync(target,revision,token);return;}
            QueueMetadata(() => HydrateAssetMetadataAsync(target));
            await FlushMetadataAsync();
            if (revision != Volatile.Read(ref _activationRevision)) return;
            OnPropertyChanged(nameof(IsProfessionalRaw));OnPropertyChanged(nameof(PhotoGradeRasterToolsEnabled));OnPropertyChanged(nameof(IsLegacyRaw));
            Editor.RestoreTargetEngine(target.EngineSnapshot, target.ExecutionModeSnapshot);
            Editor.ApplyTargetSnapshot(target.AppliedLookSnapshot, target.FilmSettingsSnapshot, target.ColorAdjustmentStackSnapshot, render: false, targetIdentity: target.ActiveEditVersionId ?? target.Id);
            TargetImage = image; TargetName = target.FileName; ActiveTarget = target; RestoreInstantControls(target.RawDefinition?.InstantFilm); target.IsActive = true;OnPropertyChanged(nameof(IsProfessionalRaw));OnPropertyChanged(nameof(PhotoGradeRasterToolsEnabled));OnPropertyChanged(nameof(IsLegacyRaw));
            foreach (var other in Targets.Where(other => !ReferenceEquals(other, target))) other.IsActive = false;
            await Editor.SetSourceAsync(target.AssetId, image, token, rawPreviewMaster: rawProxy, frozenRawMaster: rawMaster);
            if (revision != Volatile.Read(ref _activationRevision)) return;
            target.Status = target.AppliedLookSnapshot is null && target.ColorAdjustmentStackSnapshot is null ? ReferenceTargetStatus.Pending : ReferenceTargetStatus.Adjusted;
            StatusText = "待调色照片已载入。中央显示目标照片；参考图片位于独立辅助栏。";
            Editor.CopyCurrentLookTo([target]);
            target.Status = target.AppliedLookSnapshot is null && target.ColorAdjustmentStackSnapshot is null ? ReferenceTargetStatus.Pending : ReferenceTargetStatus.Adjusted;
            ScheduleTargetThumbnail(target);
            if (ReferenceEquals(FailedTarget, target)) FailedTarget = null;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidOperationException)
        { if(revision!=Volatile.Read(ref _activationRevision))return; target.Status = ReferenceTargetStatus.Failed; target.Error = target.RawDefinition is not null ? "专业 RAW 失败："+ex.Message : ex is RawDecodeException ? "RAW 相机文件无法进行 16 位解码" : "照片无法读取"; if (revision == Volatile.Read(ref _activationRevision)) { FailedTarget = target; StatusText = target.Error + "；现有色彩方案保持不变。请重试。"; } }
        finally { IsLoading = Interlocked.Decrement(ref _loadingActivations) > 0; StopProcessingCommand.RaiseCanExecuteChanged(); }
    }

    private async Task<BitmapSource> LoadSourcePreviewAsync(string path, CancellationToken token)
    {
        var stamp = File.GetLastWriteTimeUtc(path).Ticks;
        var cached = _sourceCache.FirstOrDefault(item => item.Path == path && item.Stamp == stamp);
        if (cached.Image is not null) return cached.Image;
        var image = await Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            var decoded = StudioQuickExport.Load(path);
            token.ThrowIfCancellationRequested(); return decoded;
        }, token);
        _sourceCache.AddFirst((path, stamp, image));
        // Bound cached decoded sources. Exports continue to use frozen target inputs.
        while (_sourceCache.Count > 3 || (_sourceCache.Count > 1 && _sourceCache.Sum(item => (long)item.Image.PixelWidth * item.Image.PixelHeight * 4) > 128L * 1024 * 1024))
            _sourceCache.RemoveLast();
        return image;
    }

    public void Dispose()
    {
        if(_disposed)return;
        _disposed = true;
        PropertyChangedEventManager.RemoveHandler(StudioLocalizationService.Current, OnStudioLanguageChanged, nameof(StudioLocalizationService.Language));
        CancelRangeSelection();
        _analysisCancellation?.Cancel(); _analysisCache.Clear();
        _secondSelectionCancellation?.Cancel();_secondSelectionCancellation?.Dispose();
        _instantCancellation?.Cancel();_instantCancellation?.Dispose();
        ClearHqPreview();InvalidateProfessionalPreview();_professionalDetailCancellation?.Dispose();foreach(var target in Targets){target.LinearRawMaster=null;target.RawMaster=null;}
        _activationCancellation?.Cancel(); _activationCancellation?.Dispose(); _sourceCache.Clear();
        foreach (var target in _observedTargets)
        {
            target.PropertyChanged -= OnTargetMetadataChanged;
            target.PropertyChanged -= OnTargetSelectionChanged;
            target.PropertyChanged -= OnTargetAdjustmentChanged;
        }
        _observedTargets.Clear();
        foreach (var id in _thumbnailJobs.Keys.ToArray()) CancelTargetThumbnail(id);
        // The running export owns disposal in its finally block; closing only signals cancellation.
        _exportCancellation?.Cancel(); Editor.Dispose();
    }

    private async Task ChooseTargetAsync()
    {
        var paths = _dialogs.ChooseFiles(StudioLocalizationService.Current["导入待调色照片（源文件只读）"], StudioLocalizationService.Current["ImagesRaw"] + "|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp;*.cr2;*.cr3;*.nef;*.arw;*.raf;*.rw2;*.orf;*.dng;*.pef;*.nrw;*.ori;*.3fr;*.fff;*.iiq;*.srw;*.rwl;*.x3f", true);
        if (paths.Count == 0) return;
        IsLoading = true;
        try
        {
            var items = paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(GetOrCreateTarget).ToArray();
            using var gate = new SemaphoreSlim(3, 3);
            await Task.WhenAll(items.Select(async item => { await gate.WaitAsync(); try { await LoadThumbnailAsync(item, CancellationToken.None); } finally { gate.Release(); } }));
            if (items.Length > 0) { items[0].IsSelected = true; await ActivateTargetAsync(items[0]); }
        }
        finally { IsLoading = false; }
    }

    private async Task ExportAsync(IReadOnlyList<ReferenceTargetItem> items, string? retryDirectory = null)
    {
        if (items.Count == 0 || IsExporting || IsInstantGenerating) return;
        var format=retryDirectory is null ? QuickExportFormat : _lastExportFormat;
        var acceptanceDirectory = Environment.GetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_EXPORT_DIRECTORY");
        var directory = retryDirectory ?? (string.IsNullOrWhiteSpace(acceptanceDirectory) ? _dialogs.ChooseFolder(StudioLocalizationService.Current.Format("QuickExportTitle", items.Count, string.Join(" / ", items.Select(x => StudioQuickExport.Extension(x.Path,format).TrimStart('.').ToUpperInvariant()).Distinct())), null) : acceptanceDirectory);
        if (directory is null) return;
        _lastExportDirectory = directory;
        _lastExportFormat = format;
        if (ActiveTarget is { } active && items.Contains(active)) Editor.CopyCurrentLookTo([active]);
        foreach(var target in items) CaptureEditVersion(target);
        var frozen = items.Select(item => (Item: item, VersionId: item.ActiveEditVersionId!.Value, LibraryId: item.LibraryId, AssetId: item.AssetId, SourceHash: item.SourceHash, Look: item.AppliedLookSnapshot is { } look ? look with { ReferenceSources = look.ReferenceSources.Select(source => source with { }).ToArray() } : null, Film: item.FilmSettingsSnapshot is { } film ? film with { } : null, Stack: item.ColorAdjustmentStackSnapshot?.Normalize(), Engine: item.EngineSnapshot, Mode: item.ExecutionModeSnapshot, RawDefinition:item.RawDefinition, RawInput:item.RawInput)).ToArray();
        _exportCancellation = new CancellationTokenSource(); ExportCompleted = 0; ExportTotal = frozen.Length; ExportFailureSummary = ""; ExportStatus = $"0 / {ExportTotal} · {directory} · {format} · sRGB"; RaiseExportCommands();
        var failed = new List<string>();
        try
        {
            foreach (var frozenItem in frozen)
            {
                var item = frozenItem.Item; _exportCancellation.Token.ThrowIfCancellationRequested(); item.Status = ReferenceTargetStatus.Processing; ExportStatus = $"{ExportCompleted} / {ExportTotal} · {item.FileName}";
                var isRaw = RawMatchTiff16ProductPipeline.IsRaw(item.Path);
                var output = Path.Combine(directory, Path.GetFileNameWithoutExtension(item.FileName) + ("_仿色" + StudioQuickExport.Extension(item.Path,format))); var temp = output + ".tmp";
                try
                {
                    var pendingReceipt=(await ExportRecovery.FindRecoverableAsync(directory,_exportCancellation.Token)).FirstOrDefault(x=>string.Equals(x.OutputPath,Path.GetFullPath(output),StringComparison.OrdinalIgnoreCase));
                    if(File.Exists(output)||pendingReceipt is not null)
                    {                        var adjustmentHash=ProcessingFingerprint(frozenItem.Look,frozenItem.Stack,frozenItem.Film,frozenItem.RawDefinition);
                        if(pendingReceipt?.RecipeSnapshot?.AdjustmentFingerprint!=adjustmentHash)throw new IOException("调整参数已变化，不能恢复旧成片清单。");
                        var recovery=await ExportRecovery.RecoverAsync(output,frozenItem.VersionId,frozenItem.SourceHash,_exportCancellation.Token);
                        if(recovery.State=="Completed"){item.OutputPath=output;item.ExportStatus=ReferenceExportStatus.Succeeded;item.Status=ReferenceTargetStatus.Exported;ExportCompleted++;continue;}
                        throw new IOException("输出/清单冲突："+recovery.Error);
                    }
                    if (!await ValidateTargetIdentityAsync(item, _exportCancellation.Token)) throw new IOException(item.Error);
                    if (Environment.GetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_EXPORT_FAIL_ONCE") == "1")
                    {
                        Environment.SetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_EXPORT_FAIL_ONCE", "done");
                        throw new IOException("Synthetic production export failure");
                    }
                    if(frozenItem.RawDefinition is {} professional)await ExportProfessionalRawAsync(item,professional,temp,format,_exportCancellation.Token);
                    else if (isRaw)
                    {
                        // AtomicTiffWriter validates the temporary TIFF before the destination becomes visible.
                        var master = item.RawMaster ??= await DecodeStudioRawAsync(item.Path, _exportCancellation.Token);
                        if(StudioQuickExport.Extension(item.Path,format)==".tif")
                        {
                            await ExportFrozenRawAsync(master, temp, frozenItem.Look, frozenItem.Stack, frozenItem.Film, frozenItem.Engine, frozenItem.Mode, _exportCancellation.Token);

                        }
                        else
                        {
                            var intermediate=temp+".rendered.tif";
                            try
                            {
                                await ExportFrozenRawAsync(master, intermediate, frozenItem.Look, frozenItem.Stack, frozenItem.Film, frozenItem.Engine, frozenItem.Mode, _exportCancellation.Token);
                                await Task.Run(()=>StudioQuickExport.Encode(StudioQuickExport.Load(intermediate),item.Path,temp,format,_exportCancellation.Token),_exportCancellation.Token);

                            }
                            finally { TryDelete(intermediate); }
                        }
                    }
                    else
                    {
                        if (frozenItem.Engine == ColorStudioMatchEngine.MatchV4Beta) throw new NotSupportedException("V4 Beta 输出需要 RAW 高精度源；请切换稳定 V3。");
                        var processed = await Editor.ProcessForExportAsync(item.Path, frozenItem.Look, frozenItem.Film, _exportCancellation.Token, frozenItem.Stack);
                        await Task.Run(() => StudioQuickExport.Encode(processed, item.Path, temp, format, _exportCancellation.Token), _exportCancellation.Token);

                    }
                    var encoded=frozenItem.RawDefinition is null ? StudioQuickExport.Load(temp) : null;
                    var actual=frozenItem.RawDefinition is null?null:item.LastProfessionalOutput;
                    var versionId = frozenItem.VersionId;
                    await StudioExportReceiptStore.CommitOutputAsync(temp,output,new(Guid.NewGuid(),frozenItem.LibraryId,frozenItem.AssetId,frozenItem.SourceHash,versionId,frozenItem.RawDefinition?.ProcessingVersion ?? frozenItem.Stack?.ProcessingVersion ?? 1,format.ToString(),output,"",DateTimeOffset.UtcNow, new ExportRecipeSnapshot(1,Path.GetExtension(output),actual?.BitDepth ?? (encoded!.Format.BitsPerPixel>32?16:8),"Original",actual?.Width ?? encoded!.PixelWidth,actual?.Height ?? encoded!.PixelHeight,null,Path.GetExtension(output)==".jpg"?95:null,Path.GetExtension(output)==".tif"?(isRaw?"None":"Zip"):actual is not null && Path.GetExtension(output)==".jpg"?"JPEG 4:4:4":"Codec default","sRGB",(isRaw && Path.GetExtension(output)==".tif" ? "TIFF source orientation preserved; other metadata discarded" : "Orientation normalized; source metadata discarded"),"Not applied",AdjustmentFingerprint:ProcessingFingerprint(frozenItem.Look,frozenItem.Stack,frozenItem.Film,frozenItem.RawDefinition),RawDefinition:frozenItem.RawDefinition,RawInput:frozenItem.RawInput,OutputValidation:actual is null?null:System.Text.Json.JsonSerializer.Serialize(actual))),_exportCancellation.Token);
                    item.OutputPath = output; item.ExportStatus = ReferenceExportStatus.Succeeded; item.Status = ReferenceTargetStatus.Exported;
                }
                catch (OperationCanceledException) { TryDelete(temp); item.ExportStatus = ReferenceExportStatus.Cancelled; item.Status = ReferenceTargetStatus.Pending; throw; }
                catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidOperationException)
                { TryDelete(temp); item.Error = frozenItem.RawDefinition is not null ? "PV4 导出失败："+ex.Message : File.Exists(output+".kitao-export.pending.json") ? "成片已完成，清单待恢复；重试只验证并补清单" : ex is NotSupportedException ? "当前图像或调整不支持此导出格式" : ex is RawDecodeException ? "RAW 相机文件无法进行 16 位解码" : "导出失败，源文件未修改"; item.ExportStatus = File.Exists(output+".kitao-export.pending.json")?ReferenceExportStatus.Partial:ReferenceExportStatus.Failed; item.Status = ReferenceTargetStatus.Failed; failed.Add(item.FileName); }
                ExportCompleted++; ExportStatus = $"{ExportCompleted} / {ExportTotal} · {directory} · {Path.GetExtension(output).ToUpperInvariant()}";
            }
            ExportFailureSummary = failed.Count == 0 ? "" : StudioLocalizationService.Current.Format("ExportFailures", failed.Count, string.Join(", ", frozen.Where(entry => entry.Item.ExportStatus is ReferenceExportStatus.Failed or ReferenceExportStatus.Partial).Select(entry => entry.Item.FileName + " (" + StudioLocalizationService.Current[entry.Item.Error ?? ""] + ")")));
            SetStatus("ExportFinished", ExportCompleted - failed.Count, failed.Count);
        }
        catch (OperationCanceledException) { SetExportStatus("ExportStopped", ExportCompleted, ExportTotal); StatusText = "已停止导出；已完成文件保留，未完成文件已清理。"; }
        finally { _exportCancellation.Dispose(); _exportCancellation = null; RaiseExportCommands(); }
    }
    private async Task RetryFailedExportsAsync()
    {
        var failed = Targets.Where(item => item.ExportStatus is ReferenceExportStatus.Failed or ReferenceExportStatus.Partial).ToArray();
        if (failed.Length == 0 || string.IsNullOrWhiteSpace(_lastExportDirectory)) return;
        await ExportAsync(failed, _lastExportDirectory);
    }
    private static void EncodeJpeg(BitmapSource image, string output, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var encoder = new JpegBitmapEncoder { QualityLevel = 95 }; encoder.Frames.Add(BitmapFrame.Create(image)); using var stream = File.Create(output); encoder.Save(stream); token.ThrowIfCancellationRequested();
    }
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    private void RaiseExportCommands() { OnPropertyChanged(nameof(IsExporting)); RefreshSyncAvailability(); OnPropertyChanged(nameof(CanRetryFailedExport)); StopExportCommand.RaiseCanExecuteChanged(); RetryFailedExportCommand.RaiseCanExecuteChanged(); }
}

public enum ReferenceTargetStatus { Pending, Processing, Synced, Adjusted, Exported, Failed }
public sealed class NodeSyncChoice(Guid id, string name, bool selected) : ObservableObject
{
    private bool _selected = selected;
    public System.Collections.ObjectModel.ObservableCollection<StudioEditVersion> EditVersions { get; } = [];
    public bool RestoredSession {get;set;}
    public long? LoadedCatalogRevision {get;set;}
    public StudioVersionCatalog? VersionCatalog { get; set; }
    public Guid? ActiveEditVersionId { get; set; }
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public bool Selected { get => _selected; set => SetProperty(ref _selected, value); }
}
public sealed class AdjustmentSyncChoice(ColorStudioNodeType type, string name, bool selected) : ObservableObject
{
    private bool _selected = selected;
    public ColorStudioNodeType Type { get; } = type;
    public string Name { get; } = name;
    public bool Selected { get => _selected; set => SetProperty(ref _selected, value); }
}
public enum ReferenceExportStatus { None, Queued, Exporting, Succeeded, Failed, Cancelled, Partial }

public sealed class ReferenceTargetItem : ObservableObject
{
    private bool _isSelected;
    private bool _isActive;
    private ReferenceTargetStatus _status = ReferenceTargetStatus.Pending;
    public ReferenceTargetItem(string path, Guid? id = null) { Id = id ?? Guid.NewGuid(); Path = path; }
    public System.Collections.ObjectModel.ObservableCollection<StudioEditVersion> EditVersions { get; } = [];
    public bool RestoredSession {get;set;}
    public long? LoadedCatalogRevision {get;set;}
    public StudioVersionCatalog? VersionCatalog { get; set; }
    public Guid? ActiveEditVersionId { get; set; }
    public Guid Id { get; }
    private Guid? _assetId;
    internal Dictionary<long,PhotographerAnnotation> OwnAnnotationWrites {get;}=[];
    public PhotographerAnnotation? AnnotationSnapshot { get; set; }
    public Guid? AssetId { get => _assetId; set => SetProperty(ref _assetId, value); }
    public Guid? LibraryId { get; set; }
    public string? SourceHash { get; set; }
    internal string? ObservedSourceHash { get; set; }
    internal string? MetadataDatabasePath { get; set; }
    private string _metadataStatus = "仅本次会话 · 未加入素材库";
    public string MetadataStatus { get => _metadataStatus; internal set => SetProperty(ref _metadataStatus, value); }
    public string Path { get; internal set; }
    public string FileName => System.IO.Path.GetFileName(Path);
    private BitmapSource? _thumbnail;
    public BitmapSource? Thumbnail { get => _thumbnail; set => SetProperty(ref _thumbnail, value); }
    internal BitmapSource? SourceThumbnail { get; set; }
    private string _thumbnailStatus = "原图缩略预览";
    public string ThumbnailStatus { get => _thumbnailStatus; internal set => SetProperty(ref _thumbnailStatus, value); }
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    public long FileSize => File.Exists(Path) ? new FileInfo(Path).Length : 0;
    private int _rating;
    private string? _colorLabel;
    public int Rating { get => _rating; set => SetProperty(ref _rating, Math.Clamp(value, 0, 5)); }
    /// <summary>Session-level color marker shown in the professional filmstrip. The asset model remains the source of truth when an AssetId is present.</summary>
    public string? ColorLabel
    {
        get => _colorLabel;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (!SetProperty(ref _colorLabel, normalized)) return;
            OnPropertyChanged(nameof(ColorLabelBrush));
            OnPropertyChanged(nameof(ColorLabelAccessibleName));
        }
    }
    public Brush ColorLabelBrush => ColorLabel switch
    {
        "红" => new SolidColorBrush(Color.FromRgb(0xD9, 0x5C, 0x5C)),
        "橙" => new SolidColorBrush(Color.FromRgb(0xD9, 0x89, 0x4A)),
        "黄" => new SolidColorBrush(Color.FromRgb(0xD9, 0xB4, 0x4A)),
        "绿" => new SolidColorBrush(Color.FromRgb(0x62, 0xB8, 0x7A)),
        "蓝" => new SolidColorBrush(Color.FromRgb(0x5E, 0x93, 0xD6)),
        "紫" => new SolidColorBrush(Color.FromRgb(0xA4, 0x76, 0xC8)),
        _ => new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
    };
    public string ColorLabelAccessibleName => string.IsNullOrWhiteSpace(ColorLabel) ? "无颜色标记" : $"颜色标记：{ColorLabel}";
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
    public ReferenceTargetStatus Status { get => _status; set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(StatusText)); } }
    public string StatusText => Status switch { ReferenceTargetStatus.Synced => "已同步", ReferenceTargetStatus.Adjusted => "已调整", ReferenceTargetStatus.Processing => "正在处理", ReferenceTargetStatus.Exported => "已导出", ReferenceTargetStatus.Failed => "失败", _ => "待处理" };
    public double? Progress { get; set; }
    public string? Error { get; set; }
    private ReferenceLook? _appliedLookSnapshot;
    private PixelTartFilmSettings? _filmSettingsSnapshot;
    private ColorAdjustmentStack? _colorAdjustmentStackSnapshot;
    private ColorStudioMatchEngine _engineSnapshot;
    private MatchV4ExecutionMode _executionModeSnapshot;
    public ReferenceLook? AppliedLookSnapshot { get => _appliedLookSnapshot; set => SetProperty(ref _appliedLookSnapshot, value); }
    public PixelTartFilmSettings? FilmSettingsSnapshot { get => _filmSettingsSnapshot; set => SetProperty(ref _filmSettingsSnapshot, value); }
    public ColorAdjustmentStack? ColorAdjustmentStackSnapshot { get => _colorAdjustmentStackSnapshot; set => SetProperty(ref _colorAdjustmentStackSnapshot, value); }
    public ColorStudioMatchEngine EngineSnapshot { get => _engineSnapshot; set => SetProperty(ref _engineSnapshot, value); }
    public MatchV4ExecutionMode ExecutionModeSnapshot { get => _executionModeSnapshot; set => SetProperty(ref _executionModeSnapshot, value); }
    public string? OutputPath { get; set; }
    /// <summary>Session-owned RAW master shared by thumbnail, preview, Match and TIFF export.</summary>
    public ProfessionalOutputInspection? LastProfessionalOutput {get;set;}
    public ProfessionalRawDefinition? RawDefinition {get;set;}
    public ProfessionalRawContract? RawInput {get;set;}
    private ProfessionalRawMaster? _linearRawMaster;
    public ProfessionalRawMaster? LinearRawMaster
    {
        get=>_linearRawMaster;
        set{_linearRawMaster=value;if(value is null){StudioGlobalResources.Release(Id,"Master");return;}var weak=new WeakReference<ReferenceTargetItem>(this);StudioGlobalResources.Store(Id,"Master",(long)value.Image.Rgb32.Length*sizeof(float),()=>{if(weak.TryGetTarget(out var target))if(ReferenceEquals(target._linearRawMaster,value))target._linearRawMaster=null;});StudioGlobalResources.BindRetainedBuffer(value.Image,Id,"Master");}
    }
    public FrozenRawMaster? RawMaster { get; set; }
    public ReferenceExportStatus ExportStatus { get; set; }
}
