using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Tethering;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.ViewModels;

public sealed class TetherColorViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan LutRenderTimeout = TimeSpan.FromSeconds(15);
    private readonly IDialogService _dialogs;
    private readonly ILutParser _parser;
    private readonly ILutPresetStore _presetStore;
    private readonly ILutPreviewService _previewService;
    private readonly ILutCacheService _cache;
    private readonly IDisplayTopologyService _topology;
    private readonly IDisplayColorCoordinator _displayColors;
    private readonly IMonitorPreferenceStore _monitorPreferences;
    private readonly LutRenderRequestCoordinator _renderRequests = new();
    private readonly ClientMonitorCoordinator _clientCoordinator = new();
    private readonly CancellationTokenSource _lifetime = new();
    private BitmapSource? _sourceImage;
    private BitmapSource? _displayImage;
    private Guid? _sourceAssetId;
    private string _sourceProxyVersion = "none";
    private LutPresetReference? _selectedLut;
    private string _lutSearch = string.Empty;
    private int _lutStrengthPercent = 100;
    private bool _lutEnabled = true;
    private bool _showBefore;
    private bool _isSplitView;
    private double _splitPosition = .5;
    private bool _isRendering;
    private string _lutStatus = "未选择LUT。输入色彩空间未知；监看仅供现场参考。";
    private string _colorProfileStatus = "正在检测当前显示器ICC…";
    private DisplayColorProfile? _mainProfile;
    private MonitorDisplayInfo? _selectedClientDisplay;
    private ClientMonitorFollowMode _clientFollowMode = ClientMonitorFollowMode.FollowMainSelection;
    private string _clientMonitorStatus = "客户监看未开启";
    private bool _showClientFileName;
    private bool _showClientTechnicalMetadata;
    private bool _showClientRating;
    private bool _showClientControls = true;
    private bool _clientFavorite;
    private string? _clientNote;
    private string _workingSpace = "sRGB";
    private string _untaggedImageInterpretation = "sRGB";
    private int _lutCacheLimitMegabytes = 512;
    private string _disconnectBehavior = "安全撤回客户窗口并保留会话";
    private string _clientProxyQuality = "平衡（最长边1600）";
    private bool _settingsLoading;
    private ClientMonitorWindow? _clientWindow;
    private ClientMonitorViewModel? _clientViewModel;
    private Func<bool, string?, Task>? _saveClientAnnotation;
    private Func<Guid, CancellationToken, Task<BitmapSource?>>? _loadAssetImage;

    public TetherColorViewModel(
        IDialogService dialogs,
        ILutParser? parser = null,
        ILutPresetStore? presetStore = null,
        ILutPreviewService? previewService = null,
        ILutCacheService? cache = null,
        IDisplayTopologyService? topology = null,
        IDisplayColorCoordinator? displayColors = null,
        IMonitorPreferenceStore? monitorPreferences = null)
    {
        _dialogs = dialogs;
        _parser = parser ?? new CubeLutParser();
        _presetStore = presetStore ?? new JsonLutPresetStore(AppDataPaths.TetherColorSettingsDirectory, _parser);
        _previewService = previewService ?? new CpuLutPreviewService();
        _cache = cache ?? new LutPreviewCacheService();
        _topology = topology ?? new WindowsDisplayTopologyService();
        _displayColors = displayColors ?? new DisplayColorCoordinator(new WindowsDisplayProfileService(), new MemoryColorProfileCache());
        _monitorPreferences = monitorPreferences ?? new JsonMonitorPreferenceStore(AppDataPaths.TetherColorSettingsDirectory);
        LutView = CollectionViewSource.GetDefaultView(LutPresets);
        LutView.Filter = FilterLut;
        ImportLutCommand = new AsyncRelayCommand(_ => ImportAsync());
        ToggleLutCommand = new AsyncRelayCommand(_ => ToggleLutAsync(), _ => SelectedLut is not null && SourceImage is not null);
        ToggleBeforeCommand = new RelayCommand(_ => ShowBefore = !ShowBefore, _ => SelectedLut is not null);
        ToggleSplitCommand = new RelayCommand(_ => IsSplitView = !IsSplitView, _ => SelectedLut is not null);
        ToggleFavoriteLutCommand = new AsyncRelayCommand(_ => ToggleFavoriteAsync(), _ => SelectedLut is not null);
        SetSessionDefaultCommand = new AsyncRelayCommand(_ => SetDefaultAsync(false), _ => SelectedLut is not null);
        SetProjectDefaultCommand = new AsyncRelayCommand(_ => SetDefaultAsync(true), _ => SelectedLut is not null);
        RevalidateLutCommand = new AsyncRelayCommand(_ => RevalidateAsync(), _ => SelectedLut is not null);
        RelocateLutCommand = new AsyncRelayCommand(_ => RelocateAsync(), _ => SelectedLut is not null);
        RemoveLutReferenceCommand = new AsyncRelayCommand(_ => RemoveReferenceAsync(), _ => SelectedLut is not null);
        RevealLutCommand = new RelayCommand(_ => { if (SelectedLut is not null) _dialogs.RevealFile(SelectedLut.SourcePath); }, _ => SelectedLut is not null && File.Exists(SelectedLut.SourcePath));
        OpenClientMonitorCommand = new AsyncRelayCommand(_ => OpenClientMonitorAsync(), _ => SelectedClientDisplay is not null);
        CloseClientMonitorCommand = new RelayCommand(_ => CloseClientMonitor());
        SaveClientNoteCommand = new AsyncRelayCommand(_ => SaveClientAnnotationAsync());
        ToggleClientFavoriteCommand = new AsyncRelayCommand(_ => ToggleClientFavoriteAsync());
        _topology.TopologyChanged += TopologyChanged;
        RefreshDisplays();
    }

    public ObservableCollection<LutPresetReference> LutPresets { get; } = [];
    public ICollectionView LutView { get; }
    public ObservableCollection<MonitorDisplayInfo> Displays { get; } = [];
    public IReadOnlyList<TetherChoice<LutInputInterpretation>> InputInterpretations { get; } =
    [
        new(LutInputInterpretation.Unknown, "输入色彩空间未知"), new(LutInputInterpretation.SrgbDisplay, "sRGB显示LUT"),
        new(LutInputInterpretation.SonySLog3, "Sony S-Log3"), new(LutInputInterpretation.CanonLog, "Canon Log"),
        new(LutInputInterpretation.NikonNLog, "Nikon N-Log"), new(LutInputInterpretation.FujifilmFLog, "Fujifilm F-Log"), new(LutInputInterpretation.Other, "其他/未知")
    ];
    public IReadOnlyList<TetherChoice<ClientMonitorFollowMode>> ClientFollowModes { get; } =
    [
        new(ClientMonitorFollowMode.FollowMainSelection, "跟随主选中"), new(ClientMonitorFollowMode.FollowLatest, "跟随最新"), new(ClientMonitorFollowMode.Locked, "独立锁定")
    ];

    public AsyncRelayCommand ImportLutCommand { get; }
    public AsyncRelayCommand ToggleLutCommand { get; }
    public RelayCommand ToggleBeforeCommand { get; }
    public RelayCommand ToggleSplitCommand { get; }
    public AsyncRelayCommand ToggleFavoriteLutCommand { get; }
    public AsyncRelayCommand SetSessionDefaultCommand { get; }
    public AsyncRelayCommand SetProjectDefaultCommand { get; }
    public AsyncRelayCommand RevalidateLutCommand { get; }
    public AsyncRelayCommand RelocateLutCommand { get; }
    public AsyncRelayCommand RemoveLutReferenceCommand { get; }
    public RelayCommand RevealLutCommand { get; }
    public AsyncRelayCommand OpenClientMonitorCommand { get; }
    public RelayCommand CloseClientMonitorCommand { get; }
    public AsyncRelayCommand SaveClientNoteCommand { get; }
    public AsyncRelayCommand ToggleClientFavoriteCommand { get; }

    public BitmapSource? SourceImage { get => _sourceImage; private set => SetProperty(ref _sourceImage, value); }
    public BitmapSource? DisplayImage { get => _displayImage; private set { if (SetProperty(ref _displayImage, value)) UpdateClientImage(); } }
    public LutPresetReference? SelectedLut { get => _selectedLut; set { if (SetProperty(ref _selectedLut, value)) { OnPropertyChanged(nameof(CurrentLutText)); OnPropertyChanged(nameof(SelectedInputInterpretation)); OnPropertyChanged(nameof(InputSpaceWarning)); RaiseCommands(); _ = RenderAsync(); } } }
    public LutInputInterpretation SelectedInputInterpretation
    {
        get => SelectedLut?.InputInterpretation ?? LutInputInterpretation.Unknown;
        set
        {
            if (SelectedLut is null || SelectedLut.InputInterpretation == value) return;
            ReplaceOrAdd(SelectedLut = SelectedLut with { InputInterpretation = value });
            OnPropertyChanged();
            OnPropertyChanged(nameof(InputSpaceWarning));
            _ = SavePresetListAsync();
            _ = RenderAsync();
        }
    }
    public string LutSearch { get => _lutSearch; set { if (SetProperty(ref _lutSearch, value)) LutView.Refresh(); } }
    public int LutStrengthPercent { get => _lutStrengthPercent; set { if (SetProperty(ref _lutStrengthPercent, Math.Clamp(value, 0, 100))) { OnPropertyChanged(nameof(LutStrengthText)); _ = RenderAsync(); } } }
    public string LutStrengthText => $"{LutStrengthPercent}%";
    public bool LutEnabled { get => _lutEnabled; set { if (SetProperty(ref _lutEnabled, value)) { OnPropertyChanged(nameof(LutToggleText)); _ = RenderAsync(); } } }
    public string LutToggleText => LutEnabled ? "LUT 已开启" : "LUT 已关闭";
    public bool ShowBefore { get => _showBefore; set { if (SetProperty(ref _showBefore, value)) { OnPropertyChanged(nameof(VisibleImage)); OnPropertyChanged(nameof(BeforeAfterText)); UpdateClientImage(); } } }
    public string BeforeAfterText => ShowBefore ? "正在查看原图（松开/再次点击恢复）" : "查看LUT前原图";
    public bool IsSplitView { get => _isSplitView; set { if (SetProperty(ref _isSplitView, value)) { OnPropertyChanged(nameof(SplitViewText)); OnPropertyChanged(nameof(IsLutSingleView)); } } }
    public bool IsLutSingleView => !IsSplitView;
    public string SplitViewText => IsSplitView ? "退出LUT分屏" : "LUT分屏对比";
    public double SplitPosition { get => _splitPosition; set { if (SetProperty(ref _splitPosition, Math.Clamp(value, .05, .95))) OnPropertyChanged(nameof(SplitLeftWidth)); } }
    public GridLength SplitLeftWidth => new(SplitPosition, GridUnitType.Star);
    public bool IsRendering { get => _isRendering; private set => SetProperty(ref _isRendering, value); }
    public string LutStatus { get => _lutStatus; private set => SetProperty(ref _lutStatus, value); }
    public string CurrentLutText => SelectedLut?.DisplayName ?? "未选择LUT";
    public string InputSpaceWarning => SelectedLut is null ? "未选择LUT。" : SelectedLut.InputInterpretation == LutInputInterpretation.Unknown ? "输入色彩空间未知；不保证色彩准确，仅用于现场监看参考。" : $"用户指定输入解释：{InputInterpretations.First(item => item.Value == SelectedLut.InputInterpretation).Label}；阶段D不执行Log显影。";
    public BitmapSource? VisibleImage => ShowBefore ? SourceImage : DisplayImage ?? SourceImage;
    public string ColorProfileStatus { get => _colorProfileStatus; private set => SetProperty(ref _colorProfileStatus, value); }
    public DisplayColorProfile? MainProfile { get => _mainProfile; private set => SetProperty(ref _mainProfile, value); }
    public MonitorDisplayInfo? SelectedClientDisplay { get => _selectedClientDisplay; set { if (SetProperty(ref _selectedClientDisplay, value)) RaiseCommands(); } }
    public ClientMonitorFollowMode ClientFollowMode { get => _clientFollowMode; set { if (SetProperty(ref _clientFollowMode, value)) { _clientCoordinator.SetFollowMode(value); if (_clientViewModel is not null) _clientViewModel.FollowMode = value; _ = SaveMonitorPreferenceAsync(); } } }
    public string ClientMonitorStatus { get => _clientMonitorStatus; private set { if (SetProperty(ref _clientMonitorStatus, value)) OnPropertyChanged(nameof(IsClientMonitorOpen)); } }
    public bool IsClientMonitorOpen => _clientWindow is not null;
    public bool ShowClientFileName { get => _showClientFileName; set { if (SetProperty(ref _showClientFileName, value)) { if (_clientViewModel is not null) _clientViewModel.ShowIdentifier = value; _ = SaveMonitorPreferenceAsync(); } } }
    public bool ShowClientTechnicalMetadata { get => _showClientTechnicalMetadata; set { if (SetProperty(ref _showClientTechnicalMetadata, value)) { if (_clientViewModel is not null) _clientViewModel.ShowTechnicalMetadata = value; _ = SaveMonitorPreferenceAsync(); } } }
    public bool ShowClientRating { get => _showClientRating; set { if (SetProperty(ref _showClientRating, value)) { if (_clientViewModel is not null) _clientViewModel.ShowRating = value; _ = SaveMonitorPreferenceAsync(); } } }
    public bool ShowClientControls { get => _showClientControls; set { if (SetProperty(ref _showClientControls, value)) { if (_clientViewModel is not null) _clientViewModel.ShowClientControls = value; _ = SaveMonitorPreferenceAsync(); } } }
    public bool ClientFavorite { get => _clientFavorite; set => SetProperty(ref _clientFavorite, value); }
    public string? ClientNote { get => _clientNote; set => SetProperty(ref _clientNote, value); }
    public string WorkingSpace { get => _workingSpace; set { if (SetProperty(ref _workingSpace, string.IsNullOrWhiteSpace(value) ? "sRGB" : value)) PersistColorSettings(); } }
    public string UntaggedImageInterpretation { get => _untaggedImageInterpretation; set { if (SetProperty(ref _untaggedImageInterpretation, string.IsNullOrWhiteSpace(value) ? "sRGB" : value)) PersistColorSettings(); } }
    public int LutCacheLimitMegabytes { get => _lutCacheLimitMegabytes; set { if (SetProperty(ref _lutCacheLimitMegabytes, Math.Clamp(value, 64, 4096))) PersistColorSettings(); } }
    public string DisconnectBehavior { get => _disconnectBehavior; set { if (SetProperty(ref _disconnectBehavior, value)) PersistColorSettings(); } }
    public string ClientProxyQuality { get => _clientProxyQuality; set { if (SetProperty(ref _clientProxyQuality, value)) PersistColorSettings(); } }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _settingsLoading = true;
        var settings = await _presetStore.LoadAsync(cancellationToken);
        ReplacePresets(settings.LutPresets);
        LutStrengthPercent = settings.DefaultLutStrengthPercent;
        WorkingSpace = settings.WorkingSpace;
        UntaggedImageInterpretation = settings.UntaggedImageInterpretation;
        LutCacheLimitMegabytes = (int)Math.Clamp(settings.LutCacheLimitBytes / (1024 * 1024), 64, 4096);
        SelectedLut = LutPresets.FirstOrDefault(item => item.Id == settings.SessionDefaultLutId) ?? LutPresets.FirstOrDefault(item => item.Id == settings.ProjectDefaultLutId);
        var preference = await _monitorPreferences.LoadAsync(cancellationToken);
        if (preference is not null)
        {
            SelectedClientDisplay = Displays.FirstOrDefault(item => item.StableKey == preference.StableDisplayKey);
            ClientFollowMode = preference.FollowMode; ShowClientFileName = preference.ShowFileName; ShowClientTechnicalMetadata = preference.ShowTechnicalMetadata; ShowClientRating = preference.ShowRating; ShowClientControls = preference.ShowClientControls;
        }
        var main = Displays.FirstOrDefault(item => item.IsPrimary) ?? Displays.FirstOrDefault();
        if (main is not null)
        {
            MainProfile = await _displayColors.ResolveAsync(main, cancellationToken);
            ColorProfileStatus = DescribeProfile(MainProfile);
        }
        else ColorProfileStatus = "未检测到显示器；监看将回退sRGB。";
        _settingsLoading = false;
    }

    public void AttachClientAnnotationHandler(Func<bool, string?, Task> handler) => _saveClientAnnotation = handler;
    public void AttachAssetImageLoader(Func<Guid, CancellationToken, Task<BitmapSource?>> loader) => _loadAssetImage = loader;
    public void UpdateAnnotation(bool favorite, string? clientNote) { ClientFavorite = favorite; ClientNote = clientNote; if (_clientViewModel is not null) { _clientViewModel.IsFavorite = favorite; _clientViewModel.ClientNote = clientNote; } }

    public async Task SetSourceAsync(Guid? assetId, string proxyVersion, BitmapSource? image, CancellationToken cancellationToken = default)
    {
        _sourceAssetId = assetId; _sourceProxyVersion = proxyVersion; SourceImage = image;
        _clientCoordinator.OnMainSelection(assetId);
        await RenderAsync(cancellationToken);
    }

    public void NotifyLatest(Guid assetId)
    {
        var state = _clientCoordinator.OnReady(assetId);
        if (_clientViewModel is not null) _clientViewModel.NewAssetCount = state.NewAssetCount;
        if (ClientFollowMode == ClientMonitorFollowMode.FollowLatest) _ = LoadLatestForClientAsync(assetId);
    }

    public void ApplyReviewState(string state)
    {
        if ((state is "LutImported" or "Lut1D" or "Lut3D") && LutPresets.Count == 0)
        {
            var oneDimensional = state == "Lut1D";
            var identity = oneDimensional
                ? new LutDefinition("Pixel Tart 1D", LutKind.OneDimensional, 2, new(0, 0, 0), new(1, 1, 1), [new(0,0,0),new(1,.94f,.82f)])
                : new LutDefinition("Pixel Tart Warm", LutKind.ThreeDimensional, 2, new(0, 0, 0), new(1, 1, 1), [new(0,0,0),new(1,.05f,.02f),new(.03f,1,.02f),new(1,1,.08f),new(.02f,.04f,1),new(1,.08f,1),new(.06f,1,1),new(1,.94f,.82f)]);
            var synthetic = new LutPresetReference(Guid.Parse("23000000-0000-0000-0000-00000000000D"), oneDimensional ? "Pixel Tart 1D" : "Pixel Tart Warm 3D", "[合成测试LUT]", "[合成测试LUT]", "synthetic-lut-fingerprint", identity.Kind, identity.Size, identity.DomainMin, identity.DomainMax, true, DateTimeOffset.UtcNow, LutValidationStatus.Valid);
            LutPresets.Add(synthetic); SelectedLut = synthetic;
        }
        LutStrengthPercent = state == "LutStrength50" ? 50 : 100;
        IsSplitView = state == "LutSplitView";
        ShowBefore = state == "LutBeforeAfter";
        if (state == "LutInvalid") LutStatus = "LUT已损坏或不受支持，已回退未套LUT代理图。";
        if (state == "ColorProfileFallback") ColorProfileStatus = "未配置或ICC异常，已回退sRGB；软件不能替代显示器校准。";
        if (state.StartsWith("ClientMonitor", StringComparison.Ordinal)) ClientMonitorStatus = state switch { "ClientMonitorDisconnected" => "客户显示器未连接；联机会话继续。", "ClientMonitorReconnected" => "客户显示器已重新连接，可恢复监看。", _ => "客户监看开发版验证" };
    }

    public void Dispose()
    {
        CloseClientMonitor(); _topology.TopologyChanged -= TopologyChanged; if (_topology is IDisposable disposable) disposable.Dispose(); _renderRequests.Dispose(); _lifetime.Cancel(); _lifetime.Dispose();
    }

    private async Task RenderAsync(CancellationToken cancellationToken = default)
    {
        var source = SourceImage;
        if (source is null) { DisplayImage = null; return; }
        if (!LutEnabled || SelectedLut is null || ShowBefore) { DisplayImage = source; LutStatus = SelectedLut is null ? "未选择LUT。输入色彩空间未知；监看仅供现场参考。" : "LUT已绕过，显示原始代理图。"; return; }
        if (!File.Exists(SelectedLut.SourcePath) && !SelectedLut.SourcePath.StartsWith("[合成", StringComparison.Ordinal)) { DisplayImage = source; LutStatus = "LUT文件丢失或暂时不可访问，请重新定位。已回退原图。"; return; }
        var request = _renderRequests.Begin(CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken).Token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(request.Token);
        timeout.CancelAfter(LutRenderTimeout);
        IsRendering = true;
        try
        {
            LutDefinition? definition;
            if (SelectedLut.SourcePath.StartsWith("[合成", StringComparison.Ordinal)) definition = new("Pixel Tart Warm", LutKind.ThreeDimensional, 2, new(0,0,0), new(1,1,1), [new(0,0,0),new(1,.05f,.02f),new(.03f,1,.02f),new(1,1,.08f),new(.02f,.04f,1),new(1,.08f,1),new(.06f,1,1),new(1,.94f,.82f)]);
            else { var parsed = await _parser.ParseAsync(SelectedLut.SourcePath, timeout.Token); if (!parsed.Success || parsed.Definition is null) { if (_renderRequests.IsCurrent(request.Version)) { DisplayImage = source; LutStatus = parsed.Message ?? "LUT验证失败，已回退原图。"; } return; } definition = parsed.Definition; }
            var display = Displays.FirstOrDefault(item => item.IsPrimary) ?? Displays.FirstOrDefault();
            var profile = display is null ? null : await _displayColors.ResolveAsync(display, request.Token);
            var cacheKey = new LutCacheKey(_sourceAssetId ?? Guid.Empty, _sourceProxyVersion, SelectedLut.FileFingerprint, SelectedLut.InputInterpretation, LutStrengthPercent, display?.StableKey ?? "no-display", profile?.ProfileFingerprint ?? "srgb-fallback", 1);
            var opaqueKey = _cache.CreateOpaqueKey(cacheKey);
            var cachedPath = _cache.Resolve(opaqueKey);
            LutPreviewRenderResult rendered;
            if (cachedPath is not null) rendered = new(LutBitmapEncoding.Load(cachedPath), profile?.Status != DisplayProfileStatus.Detected, "已从LUT代理缓存加载。");
            else { rendered = await _previewService.RenderAsync(source, definition, LutStrengthPercent / 100d, profile, timeout.Token); using var encoded = LutBitmapEncoding.EncodePng(rendered.Image); await _cache.StoreAsync(opaqueKey, encoded, timeout.Token); }
            if (!_renderRequests.IsCurrent(request.Version)) return;
            DisplayImage = rendered.Image; LutStatus = rendered.StatusText;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !request.Token.IsCancellationRequested) { if (_renderRequests.IsCurrent(request.Version)) { DisplayImage = source; LutStatus = "LUT处理超时，已回退未套LUT代理图；接片继续。"; } }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException) { if (_renderRequests.IsCurrent(request.Version)) { DisplayImage = source; LutStatus = "LUT处理失败，已回退未套LUT代理图；接片继续。"; } }
        finally { if (_renderRequests.IsCurrent(request.Version)) IsRendering = false; }
    }

    private async Task ImportAsync()
    {
        var path = _dialogs.ChooseFiles("导入.cube监看LUT（仅关联原位置）", "Cube LUT|*.cube", false).FirstOrDefault(); if (path is null) return;
        try { var preset = await _presetStore.ImportAsync(path, cancellationToken: _lifetime.Token); ReplaceOrAdd(preset); SelectedLut = preset; LutStatus = "LUT已验证并仅关联原位置；未复制、未修改源文件。"; }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { LutStatus = "LUT无效或暂时不可访问，未导入；当前监看保持原图。"; }
    }
    private async Task ToggleLutAsync() { LutEnabled = !LutEnabled; await RenderAsync(); }
    private async Task ToggleFavoriteAsync() { if (SelectedLut is null) return; ReplaceOrAdd(SelectedLut = SelectedLut with { IsFavorite = !SelectedLut.IsFavorite }); await SavePresetListAsync(); }
    private async Task SetDefaultAsync(bool project) { if (SelectedLut is null) return; var settings = await _presetStore.LoadAsync(_lifetime.Token); await _presetStore.SaveAsync(project ? settings with { ProjectDefaultLutId = SelectedLut.Id } : settings with { SessionDefaultLutId = SelectedLut.Id }, _lifetime.Token); LutStatus = project ? "已设为当前项目默认LUT。" : "已设为当前会话默认LUT。"; }
    private async Task RevalidateAsync() { if (SelectedLut is null) return; if (!File.Exists(SelectedLut.SourcePath)) { ReplaceOrAdd(SelectedLut = SelectedLut with { ValidationStatus = LutValidationStatus.Missing, LastValidatedAtUtc = DateTimeOffset.UtcNow }); LutStatus = "LUT文件丢失，请重新定位。"; return; } var parsed = await _parser.ParseAsync(SelectedLut.SourcePath, _lifetime.Token); ReplaceOrAdd(SelectedLut = SelectedLut with { ValidationStatus = parsed.Success ? LutValidationStatus.Valid : LutValidationStatus.Invalid, LastValidatedAtUtc = DateTimeOffset.UtcNow }); await SavePresetListAsync(); await RenderAsync(); }
    private async Task RelocateAsync() { if (SelectedLut is null) return; var path = _dialogs.ChooseFiles("重新定位.cube LUT", "Cube LUT|*.cube", false).FirstOrDefault(); if (path is null) return; try { ReplaceOrAdd(SelectedLut = await _presetStore.RelocateAsync(SelectedLut.Id, path, _lifetime.Token)); LutStatus = "LUT已重新定位并通过验证。"; await RenderAsync(); } catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { LutStatus = "重新定位的文件未通过验证，原引用保持不变。"; } }
    private async Task RemoveReferenceAsync() { if (SelectedLut is null || !_dialogs.Confirm("仅从像素蛋挞中移除LUT引用，不会删除电脑中的LUT文件。", "移除LUT引用")) return; var id = SelectedLut.Id; await _presetStore.RemoveReferenceAsync(id, _lifetime.Token); LutPresets.Remove(LutPresets.First(item => item.Id == id)); SelectedLut = null; LutStatus = "LUT引用已移除，电脑中的.cube文件未删除。"; }
    private async Task SavePresetListAsync() { var settings = await _presetStore.LoadAsync(_lifetime.Token); await _presetStore.SaveAsync(settings with { LutPresets = LutPresets.ToArray() }, _lifetime.Token); }
    private void ReplacePresets(IEnumerable<LutPresetReference> presets) { LutPresets.Clear(); foreach (var preset in presets.OrderByDescending(item => item.IsFavorite).ThenByDescending(item => item.LastUsedAtUtc)) LutPresets.Add(preset); }
    private void ReplaceOrAdd(LutPresetReference preset) { var existing = LutPresets.FirstOrDefault(item => item.Id == preset.Id); if (existing is null) LutPresets.Add(preset); else LutPresets[LutPresets.IndexOf(existing)] = preset; LutView.Refresh(); }
    private bool FilterLut(object value) => value is LutPresetReference preset && (string.IsNullOrWhiteSpace(LutSearch) || preset.DisplayName.Contains(LutSearch, StringComparison.OrdinalIgnoreCase));

    private async Task OpenClientMonitorAsync()
    {
        if (SelectedClientDisplay is null) { ClientMonitorStatus = "请选择客户显示器。"; return; }
        var live = _topology.FindByStableKey(SelectedClientDisplay.StableKey);
        if (live is null) { ClientMonitorStatus = "客户显示器未连接；不会在主屏自动全屏打开。"; return; }
        CloseClientMonitor();
        _clientCoordinator.SetFollowMode(ClientFollowMode);
        var state = _clientCoordinator.Open(_sourceAssetId, true);
        var clientImage = await RenderForDisplayAsync(live, _lifetime.Token);
        _clientViewModel = new ClientMonitorViewModel { DisplayImage = clientImage ?? SourceImage, FollowMode = ClientFollowMode, ShowIdentifier = ShowClientFileName, ShowTechnicalMetadata = ShowClientTechnicalMetadata, ShowRating = ShowClientRating, ShowClientControls = ShowClientControls, IsFavorite = ClientFavorite, ClientNote = ClientNote, SimplifiedFileNumber = CreateSimplifiedFileNumber(_sourceAssetId), LutName = SelectedLut?.DisplayName ?? "无LUT", StatusText = state.StatusText };
        _clientViewModel.SaveRequested += ClientSaveRequested;
        _clientViewModel.ClearRequested += ClientClearRequested;
        _clientWindow = new ClientMonitorWindow { DataContext = _clientViewModel, WindowStartupLocation = WindowStartupLocation.Manual, Left = live.Left * 96d / live.DpiX, Top = live.Top * 96d / live.DpiY, Width = live.Width * 96d / live.DpiX, Height = live.Height * 96d / live.DpiY };
        _clientWindow.Closed += ClientWindowClosed;
        _clientWindow.Show();
        ClientMonitorStatus = $"客户监看已开启 · {live.FriendlyName} · {ClientFollowModes.First(item => item.Value == ClientFollowMode).Label}";
        OnPropertyChanged(nameof(IsClientMonitorOpen));
        await SaveMonitorPreferenceAsync();
    }
    private void CloseClientMonitor() { var window = _clientWindow; _clientWindow = null; if (window is not null) { window.Closed -= ClientWindowClosed; window.Close(); } DetachClientEvents(); _clientViewModel = null; _clientCoordinator.Close(); ClientMonitorStatus = "客户监看未开启；联机会话不受影响。"; OnPropertyChanged(nameof(IsClientMonitorOpen)); }
    private void ClientWindowClosed(object? sender, EventArgs e) { _clientWindow = null; DetachClientEvents(); _clientViewModel = null; _clientCoordinator.Close(); ClientMonitorStatus = "客户窗口已关闭；联机会话继续。"; OnPropertyChanged(nameof(IsClientMonitorOpen)); }
    private void DetachClientEvents() { if (_clientViewModel is null) return; _clientViewModel.SaveRequested -= ClientSaveRequested; _clientViewModel.ClearRequested -= ClientClearRequested; }
    private async void ClientSaveRequested(object? sender, EventArgs e) { if (_clientViewModel is null) return; ClientFavorite = _clientViewModel.IsFavorite; ClientNote = _clientViewModel.ClientNote; await SaveClientAnnotationAsync(); }
    private async void ClientClearRequested(object? sender, EventArgs e) { if (_clientViewModel is null || !_dialogs.Confirm("清空本条客户备注？只会清空当前照片的本地标注，不会删除或修改照片。", "清空客户备注")) return; _clientViewModel.ClientNote = null; ClientNote = null; await SaveClientAnnotationAsync(); }
    private async Task SaveClientAnnotationAsync() { if (_saveClientAnnotation is null) { ClientMonitorStatus = "当前照片不可保存客户标注。"; return; } try { await _saveClientAnnotation(ClientFavorite, ClientNote); ClientMonitorStatus = "客户收藏和备注已保存到现有TetherAnnotations。"; } catch { ClientMonitorStatus = "客户标注未保存，数据库暂时不可用；照片未改变。"; } }
    private async Task ToggleClientFavoriteAsync() { ClientFavorite = !ClientFavorite; if (_clientViewModel is not null) _clientViewModel.IsFavorite = ClientFavorite; await SaveClientAnnotationAsync(); }
    private async Task LoadLatestForClientAsync(Guid assetId) { if (_loadAssetImage is null || _clientViewModel is null || SelectedClientDisplay is null) return; try { var image = await _loadAssetImage(assetId, _lifetime.Token); var live = _topology.FindByStableKey(SelectedClientDisplay.StableKey); if (_clientCoordinator.Snapshot().AssetId == assetId && image is not null && live is not null) { _clientViewModel.DisplayImage = await RenderForDisplayAsync(live, image, assetId, _lifetime.Token); _clientViewModel.SimplifiedFileNumber = CreateSimplifiedFileNumber(assetId); _clientViewModel.LutName = SelectedLut?.DisplayName ?? "无LUT"; } } catch (OperationCanceledException) { } }
    private void UpdateClientImage() { if (_clientViewModel is null) return; var state = _clientCoordinator.Snapshot(); if (state.FollowMode == ClientMonitorFollowMode.FollowMainSelection) _ = RefreshClientDisplayAsync(); }
    private void TopologyChanged(object? sender, EventArgs e) { Application.Current?.Dispatcher.Invoke(() => { var key = SelectedClientDisplay?.StableKey; RefreshDisplays(); SelectedClientDisplay = key is null ? Displays.FirstOrDefault(item => !item.IsPrimary) : Displays.FirstOrDefault(item => item.StableKey == key); if (key is not null && SelectedClientDisplay is null) { CloseClientMonitor(); _clientCoordinator.Disconnect(); ClientMonitorStatus = "客户显示器未连接；客户窗口已安全撤回，Watch Folder和任务继续。"; } else if (key is not null) { _clientCoordinator.Reconnect(); ClientMonitorStatus = "客户显示器已重新连接，可手动恢复监看。"; } }); }
    private void RefreshDisplays() { var selected = SelectedClientDisplay?.StableKey; Displays.Clear(); foreach (var display in _topology.GetDisplays()) Displays.Add(display); SelectedClientDisplay = Displays.FirstOrDefault(item => item.StableKey == selected) ?? Displays.FirstOrDefault(item => !item.IsPrimary) ?? Displays.FirstOrDefault(); }
    private async Task SaveMonitorPreferenceAsync() { if (SelectedClientDisplay is null) return; await _monitorPreferences.SaveAsync(new(SelectedClientDisplay.StableKey, SelectedClientDisplay.FriendlyName, ClientFollowMode, true, ShowClientFileName, ShowClientTechnicalMetadata, ShowClientRating, ShowClientControls, SelectedLut?.Id, LutStrengthPercent / 100d, SelectedClientDisplay.Left, SelectedClientDisplay.Top, SelectedClientDisplay.Width, SelectedClientDisplay.Height, SelectedClientDisplay.DpiX), _lifetime.Token); }
    private async Task RefreshClientDisplayAsync() { if (_clientViewModel is null || SelectedClientDisplay is null) return; var live = _topology.FindByStableKey(SelectedClientDisplay.StableKey); if (live is null) return; try { _clientViewModel.DisplayImage = await RenderForDisplayAsync(live, _lifetime.Token) ?? SourceImage; } catch (OperationCanceledException) { } }
    private Task<BitmapSource?> RenderForDisplayAsync(MonitorDisplayInfo display, CancellationToken cancellationToken)
        => RenderForDisplayAsync(display, SourceImage, _sourceAssetId ?? Guid.Empty, cancellationToken);
    private async Task<BitmapSource?> RenderForDisplayAsync(MonitorDisplayInfo display, BitmapSource? source, Guid assetId, CancellationToken cancellationToken)
    {
        if (source is null) return null;
        var profile = await _displayColors.ResolveAsync(display, cancellationToken);
        if (!LutEnabled || SelectedLut is null || ShowBefore) return new WpfColorConversionService().ConvertToDisplay(source, profile, out _);
        LutDefinition? definition;
        if (SelectedLut.SourcePath.StartsWith("[合成", StringComparison.Ordinal)) definition = new("Pixel Tart Warm", LutKind.ThreeDimensional, 2, new(0,0,0), new(1,1,1), [new(0,0,0),new(1,.05f,.02f),new(.03f,1,.02f),new(1,1,.08f),new(.02f,.04f,1),new(1,.08f,1),new(.06f,1,1),new(1,.94f,.82f)]);
        else { var parsed = await _parser.ParseAsync(SelectedLut.SourcePath, cancellationToken); if (!parsed.Success || parsed.Definition is null) return new WpfColorConversionService().ConvertToDisplay(source, profile, out _); definition = parsed.Definition; }
        var cacheKey = new LutCacheKey(assetId, _sourceProxyVersion, SelectedLut.FileFingerprint, SelectedLut.InputInterpretation, LutStrengthPercent, display.StableKey, profile.ProfileFingerprint ?? "srgb-fallback", 1);
        var opaque = _cache.CreateOpaqueKey(cacheKey); var cached = _cache.Resolve(opaque); if (cached is not null) return LutBitmapEncoding.Load(cached);
        var rendered = await _previewService.RenderAsync(source, definition, LutStrengthPercent / 100d, profile, cancellationToken); using var encoded = LutBitmapEncoding.EncodePng(rendered.Image); await _cache.StoreAsync(opaque, encoded, cancellationToken); return rendered.Image;
    }
    private void PersistColorSettings() { if (_settingsLoading) return; _ = SaveColorSettingsAsync(); }
    private async Task SaveColorSettingsAsync() { try { var settings = await _presetStore.LoadAsync(_lifetime.Token); await _presetStore.SaveAsync(settings with { WorkingSpace = WorkingSpace, UntaggedImageInterpretation = UntaggedImageInterpretation, LutCacheLimitBytes = LutCacheLimitMegabytes * 1024L * 1024L, DefaultLutStrengthPercent = LutStrengthPercent }, _lifetime.Token); } catch (OperationCanceledException) { } }
    private static string DescribeProfile(DisplayColorProfile profile) => profile.Status == DisplayProfileStatus.Detected ? $"{profile.ProfileName} · {profile.ColorSpaceHint} · 已检测（系统默认）" : $"{profile.ProfileName} · {profile.Status}；已回退sRGB。系统有ICC不代表显示器已经校准。";
    private static string CreateSimplifiedFileNumber(Guid? assetId) => assetId is null ? "照片 --" : $"照片 {assetId.Value:N}"[..9].ToUpperInvariant();
    private void RaiseCommands() { ToggleLutCommand.RaiseCanExecuteChanged(); ToggleBeforeCommand.RaiseCanExecuteChanged(); ToggleSplitCommand.RaiseCanExecuteChanged(); ToggleFavoriteLutCommand.RaiseCanExecuteChanged(); SetSessionDefaultCommand.RaiseCanExecuteChanged(); SetProjectDefaultCommand.RaiseCanExecuteChanged(); RevalidateLutCommand.RaiseCanExecuteChanged(); RelocateLutCommand.RaiseCanExecuteChanged(); RemoveLutReferenceCommand.RaiseCanExecuteChanged(); RevealLutCommand.RaiseCanExecuteChanged(); OpenClientMonitorCommand.RaiseCanExecuteChanged(); }
}

public sealed class ClientMonitorViewModel : ObservableObject
{
    private BitmapSource? _displayImage;
    private ClientMonitorFollowMode _followMode;
    private bool _showIdentifier;
    private bool _showTechnicalMetadata;
    private bool _showRating;
    private bool _showClientControls = true;
    private bool _isFavorite;
    private string? _clientNote;
    private int _newAssetCount;
    private string _statusText = "客户监看";
    private string _simplifiedFileNumber = "照片 --";
    private string _lutName = "无LUT";
    public event EventHandler? SaveRequested;
    public event EventHandler? ClearRequested;
    public BitmapSource? DisplayImage { get => _displayImage; set => SetProperty(ref _displayImage, value); }
    public ClientMonitorFollowMode FollowMode { get => _followMode; set { if (SetProperty(ref _followMode, value)) OnPropertyChanged(nameof(FollowModeText)); } }
    public string FollowModeText => FollowMode switch { ClientMonitorFollowMode.FollowLatest => "跟随最新", ClientMonitorFollowMode.Locked => "独立锁定", _ => "跟随主选中" };
    public bool ShowIdentifier { get => _showIdentifier; set => SetProperty(ref _showIdentifier, value); }
    public bool ShowTechnicalMetadata { get => _showTechnicalMetadata; set => SetProperty(ref _showTechnicalMetadata, value); }
    public bool ShowRating { get => _showRating; set => SetProperty(ref _showRating, value); }
    public bool ShowClientControls { get => _showClientControls; set => SetProperty(ref _showClientControls, value); }
    public bool IsFavorite { get => _isFavorite; set => SetProperty(ref _isFavorite, value); }
    public string? ClientNote { get => _clientNote; set => SetProperty(ref _clientNote, value); }
    public int NewAssetCount { get => _newAssetCount; set { if (SetProperty(ref _newAssetCount, value)) OnPropertyChanged(nameof(NewAssetText)); } }
    public string NewAssetText => NewAssetCount > 0 ? $"有 {NewAssetCount} 张新照片" : string.Empty;
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
    public string SimplifiedFileNumber { get => _simplifiedFileNumber; set => SetProperty(ref _simplifiedFileNumber, value); }
    public string TechnicalSummary { get; set; } = "监看代理 · sRGB工作空间";
    public string RatingText { get; set; } = "未评级";
    public string LutName { get => _lutName; set => SetProperty(ref _lutName, value); }
    public RelayCommand ToggleFavoriteCommand => new(_ => { IsFavorite = !IsFavorite; SaveRequested?.Invoke(this, EventArgs.Empty); });
    public RelayCommand SaveNoteCommand => new(_ => SaveRequested?.Invoke(this, EventArgs.Empty));
    public RelayCommand ClearNoteCommand => new(_ => ClearRequested?.Invoke(this, EventArgs.Empty));
}
