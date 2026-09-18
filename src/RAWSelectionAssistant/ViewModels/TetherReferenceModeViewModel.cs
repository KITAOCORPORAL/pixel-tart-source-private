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
    private readonly ReferenceLookPreviewService _preview = new();
    private CancellationTokenSource? _render;
    private BitmapSource? _source;
    private Guid? _assetId;
    private long _revision;
    private ReferenceLook? _selectedLook;
    private BitmapSource? _matchedImage;
    private bool _enabled;
    private bool _applyToFollowing;
    private bool _advancedExpanded;
    private string _viewMode = "左右对比";
    private double _splitPosition = .5;
    private string _statusText = "选择项目色彩方案后可进行现场监看仿色。";
    private bool _originalHeld;
    public Func<BitmapSource, CancellationToken, Task<BitmapSource>>? PostProcessor { get; set; }

    public TetherReferenceModeViewModel(ReferenceLookStore? store = null)
    {
        _store = store ?? new(Path.Combine(AppDataPaths.DataDirectory, "ProjectVisuals"));
        ApplyCommand = new AsyncRelayCommand(_ => EnableAndRenderAsync(), _ => SelectedLook is not null && _source is not null);
        ReloadCommand = new AsyncRelayCommand(_ => LoadAsync());
    }
    public ObservableCollection<ReferenceLook> Looks { get; } = [];
    public IReadOnlyList<string> ViewModes { get; } = ["原片", "仿色", "左右对比", "并排对比"];
    public AsyncRelayCommand ApplyCommand { get; }
    public AsyncRelayCommand ReloadCommand { get; }
    public ReferenceLook? SelectedLook { get => _selectedLook; set { if (SetProperty(ref _selectedLook, value)) { OnPropertyChanged(nameof(CurrentLookText)); CopyParameters(value?.Parameters ?? new()); _ = RenderAsync(); } } }
    public string CurrentLookText => SelectedLook?.Name ?? "未选择色彩方案";
    public BitmapSource? MatchedImage { get => _matchedImage; private set => SetProperty(ref _matchedImage, value); }
    public bool Enabled { get => _enabled; set { if (SetProperty(ref _enabled, value)) _ = RenderAsync(); } }
    public bool ApplyToFollowing { get => _applyToFollowing; set => SetProperty(ref _applyToFollowing, value); }
    public bool AdvancedExpanded { get => _advancedExpanded; set => SetProperty(ref _advancedExpanded, value); }
    public string ViewMode { get => _viewMode; set { if (SetProperty(ref _viewMode, value)) RaiseViewProperties(); } }
    public double SplitPosition { get => _splitPosition; set { if (SetProperty(ref _splitPosition, Math.Clamp(value, 0, 1))) { OnPropertyChanged(nameof(SplitLeft)); OnPropertyChanged(nameof(SplitRight)); } } }
    public System.Windows.GridLength SplitLeft => new(SplitPosition, System.Windows.GridUnitType.Star);
    public System.Windows.GridLength SplitRight => new(1 - SplitPosition, System.Windows.GridUnitType.Star);
    public bool ShowOriginal => _originalHeld || !Enabled || ViewMode == "原片";
    public bool ShowMatched => !_originalHeld && Enabled && ViewMode == "仿色";
    public bool ShowSplit => !_originalHeld && Enabled && ViewMode == "左右对比";
    public bool ShowSideBySide => !_originalHeld && Enabled && ViewMode == "并排对比";
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    public double MatchStrength { get => SelectedLook?.Parameters.MatchStrength ?? _match; set => SetParameter(value, p => p with { MatchStrength = value }, ref _match); }
    public double ToneStrength { get => SelectedLook?.Parameters.ToneStrength ?? _tone; set => SetParameter(value, p => p with { ToneStrength = value }, ref _tone); }
    public double ColorStrength { get => SelectedLook?.Parameters.ColorStrength ?? _color; set => SetParameter(value, p => p with { ColorStrength = value }, ref _color); }
    public double ContrastStrength { get => SelectedLook?.Parameters.ContrastStrength ?? _contrast; set => SetParameter(value, p => p with { ContrastStrength = value }, ref _contrast); }
    public double SaturationStrength { get => SelectedLook?.Parameters.SaturationStrength ?? _saturation; set => SetParameter(value, p => p with { SaturationStrength = value }, ref _saturation); }
    public double SkinProtection { get => SelectedLook?.Parameters.SkinProtection ?? _skin; set => SetParameter(value, p => p with { SkinProtection = value }, ref _skin); }
    public double HighlightProtection { get => SelectedLook?.Parameters.HighlightProtection ?? _highlight; set => SetParameter(value, p => p with { HighlightProtection = value }, ref _highlight); }
    private double _match=100,_tone=50,_color=70,_contrast=50,_saturation=50,_skin=60,_highlight=70;

    public async Task LoadAsync(CancellationToken token = default)
    {
        var selected = SelectedLook?.ReferenceLookId; var catalog = await _store.LoadAsync(token);
        Looks.Clear(); foreach (var look in catalog.Looks.OrderByDescending(item => item.UpdatedAt)) Looks.Add(look);
        SelectedLook = Looks.FirstOrDefault(item => item.ReferenceLookId == selected) ?? Looks.FirstOrDefault();
    }
    public async Task SetSourceAsync(Guid? assetId, BitmapSource? source, CancellationToken token = default)
    {
        _assetId = assetId; _source = source; MatchedImage = null; ApplyCommand.RaiseCanExecuteChanged();
        if (ApplyToFollowing && Enabled && source is not null) await RenderAsync(token);
    }
    public async Task SelectLookAsync(Guid? lookId)
    {
        if (lookId is null) return;
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
            _ = _store.SaveAsync(_selectedLook);
        }
        _ = DebouncedRenderAsync();
    }
    private async Task EnableAndRenderAsync() { if (!Enabled) { Enabled = true; return; } await RenderAsync(); }
    private void CopyParameters(ReferenceLookParameters value)
    { _match=value.MatchStrength;_tone=value.ToneStrength;_color=value.ColorStrength;_contrast=value.ContrastStrength;_saturation=value.SaturationStrength;_skin=value.SkinProtection;_highlight=value.HighlightProtection; foreach(var name in new[]{nameof(MatchStrength),nameof(ToneStrength),nameof(ColorStrength),nameof(ContrastStrength),nameof(SaturationStrength),nameof(SkinProtection),nameof(HighlightProtection)})OnPropertyChanged(name); }
    private async Task DebouncedRenderAsync()
    {
        var revision = Interlocked.Increment(ref _revision); await Task.Delay(80);
        if (revision == Volatile.Read(ref _revision)) await RenderAsync();
    }
    private async Task RenderAsync(CancellationToken outer = default)
    {
        var source = _source; var look = SelectedLook; var asset = _assetId;
        if (!Enabled || source is null || look is null) { MatchedImage = null; StatusText = "现场监看仿色未开启。"; RaiseViewProperties(); return; }
        _render?.Cancel(); _render?.Dispose(); _render = CancellationTokenSource.CreateLinkedTokenSource(outer);
        var revision = Interlocked.Increment(ref _revision); StatusText = "正在后台生成监看仿色…";
        try
        {
            var image = await _preview.RenderAsync(source, look, _render.Token);
            if (PostProcessor is not null) image = await PostProcessor(image, _render.Token);
            if (revision != Volatile.Read(ref _revision) || asset != _assetId) return;
            MatchedImage = image; StatusText = "现场监看仿色已更新；RAW/JPG 源文件未修改。"; RaiseViewProperties();
        }
        catch (OperationCanceledException) { }
        catch (Exception) { if (revision == Volatile.Read(ref _revision)) { MatchedImage = null; StatusText = "仿色未完成，继续显示原片；接片不受影响。"; RaiseViewProperties(); } }
    }
    private void RaiseViewProperties(){foreach(var name in new[]{nameof(ShowOriginal),nameof(ShowMatched),nameof(ShowSplit),nameof(ShowSideBySide)})OnPropertyChanged(name);}
    public void Dispose(){_render?.Cancel();_render?.Dispose();}
}
