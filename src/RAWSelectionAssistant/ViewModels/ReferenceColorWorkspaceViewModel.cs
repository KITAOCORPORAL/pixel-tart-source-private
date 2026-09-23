using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed class ReferenceColorWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly IDialogService _dialogs;
    private BitmapSource? _targetImage;
    private string _targetName = "尚未选择待调色照片";
    private string _statusText = "选择待调色照片，再添加希望借用色彩与影调的参考图片。源照片始终只读。";
    private bool _isLoading;
    private ReferenceTargetItem? _activeTarget;
    private long _activationRevision;
    private int _loadingActivations;
    private CancellationTokenSource? _exportCancellation;
    private int _exportCompleted;
    private int _exportTotal;
    private string _exportStatus = "";

    public ReferenceColorWorkspaceViewModel(IDialogService dialogs, IReferenceRenderBackend? renderBackend = null)
    {
        _dialogs = dialogs;
        Editor = new TetherReferenceModeViewModel(
            new ReferenceLookStore(Path.Combine(AppDataPaths.DataDirectory, "ProjectVisuals")), dialogs, allowReferenceManagement: true,
            renderBackend: renderBackend);
        Editor.Enabled = true;
        ChooseTargetCommand = new AsyncRelayCommand(_ => ChooseTargetAsync());
        StopProcessingCommand = new RelayCommand(_ => Editor.StopProcessing(), _ => Editor.IsBusy);
        SyncSelectedCommand = new RelayCommand(_ => Editor.CopyCurrentLookTo(SelectedTargets), _ => SelectedTargets.Any());
        SyncAllCommand = new RelayCommand(_ => Editor.CopyCurrentLookTo(Targets), _ => Targets.Count > 0);
        ActivateTargetCommand = new AsyncRelayCommand(value => value is ReferenceTargetItem target ? ActivateTargetAsync(target) : Task.CompletedTask);
        ExportSelectedCommand = new AsyncRelayCommand(_ => ExportAsync(SelectedTargets.ToArray()), _ => SelectedTargets.Any() && !IsExporting);
        ExportAllCommand = new AsyncRelayCommand(_ => ExportAsync(Targets.ToArray()), _ => Targets.Count > 0 && !IsExporting);
        StopExportCommand = new RelayCommand(_ => _exportCancellation?.Cancel(), _ => IsExporting);
        Editor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TetherReferenceModeViewModel.IsBusy)) StopProcessingCommand!.RaiseCanExecuteChanged();
        };
    }

    public TetherReferenceModeViewModel Editor { get; }
    public AsyncRelayCommand ChooseTargetCommand { get; }
    public RelayCommand StopProcessingCommand { get; }
    public RelayCommand SyncSelectedCommand { get; }
    public RelayCommand SyncAllCommand { get; }
    public AsyncRelayCommand ActivateTargetCommand { get; }
    public AsyncRelayCommand ExportSelectedCommand { get; }
    public AsyncRelayCommand ExportAllCommand { get; }
    public RelayCommand StopExportCommand { get; }
    public ObservableCollection<ReferenceTargetItem> Targets { get; } = [];
    public IEnumerable<ReferenceTargetItem> SelectedTargets => Targets.Where(item => item.IsSelected);
    public int SelectFilmstripTarget(int index, int anchor, bool shift, bool control)
    {
        if (index < 0 || index >= Targets.Count) return anchor;
        if (shift && anchor >= 0 && anchor < Targets.Count)
        {
            if (!control) foreach (var item in Targets) item.IsSelected = false;
            for (var i = Math.Min(anchor, index); i <= Math.Max(anchor, index); i++) Targets[i].IsSelected = true;
            return anchor;
        }
        if (control) Targets[index].IsSelected = !Targets[index].IsSelected;
        else { foreach (var item in Targets) item.IsSelected = false; Targets[index].IsSelected = true; }
        return index;
    }
    public ReferenceTargetItem? ActiveTarget { get => _activeTarget; private set => SetProperty(ref _activeTarget, value); }
    public BitmapSource? TargetImage { get => _targetImage; private set { if (SetProperty(ref _targetImage, value)) OnPropertyChanged(nameof(HasTarget)); } }
    public bool HasTarget => TargetImage is not null;
    public string TargetName { get => _targetName; private set => SetProperty(ref _targetName, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool IsExporting => _exportCancellation is not null;
    public int ExportCompleted { get => _exportCompleted; private set => SetProperty(ref _exportCompleted, value); }
    public int ExportTotal { get => _exportTotal; private set => SetProperty(ref _exportTotal, value); }
    public string ExportStatus { get => _exportStatus; private set => SetProperty(ref _exportStatus, value); }

    public async Task InitializeAsync(CancellationToken token = default) => await Editor.LoadAsync(token);

    public async Task AcceptContextAsync(Guid? projectId, Guid? assetId, BitmapSource? source, CancellationToken token = default)
    {
        await Editor.SetProjectAsync(projectId, token);
        if (source is null) return;
        TargetImage = source;
        TargetName = "当前联机照片";
        await Editor.SetSourceAsync(assetId, source, token);
        StatusText = "已从联机拍摄带入当前照片和色彩方案；取消或返回不会改写方案。";
    }

    public async Task LoadTargetAsync(string path)
    {
        var target = GetOrCreateTarget(path);
        target.IsSelected = true;
        await ActivateTargetAsync(target);
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
            await Task.Run(() =>
            {
                var decoded = new BitmapImage();
                decoded.BeginInit(); decoded.CacheOption = BitmapCacheOption.OnLoad; decoded.DecodePixelWidth = 320; decoded.UriSource = new Uri(item.Path); decoded.EndInit(); decoded.Freeze();
                item.Thumbnail = decoded; item.PixelWidth = decoded.PixelWidth; item.PixelHeight = decoded.PixelHeight; item.Status = ReferenceTargetStatus.Pending;
            }, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.IO.FileFormatException) { item.Status = ReferenceTargetStatus.Failed; item.Error = "缩略图无法读取"; }
    }

    private async Task ActivateTargetAsync(ReferenceTargetItem target)
    {
        var revision = Interlocked.Increment(ref _activationRevision);
        if (ActiveTarget is { } previous && !ReferenceEquals(previous, target))
            Editor.CopyCurrentLookTo([previous]);
        try
        {
            Editor.StopProcessing();
            Interlocked.Increment(ref _loadingActivations); IsLoading = true; StatusText = "正在载入活动预览…";
            var image = await Task.Run(() =>
            {
                var decoded = new BitmapImage(); decoded.BeginInit(); decoded.CacheOption = BitmapCacheOption.OnLoad; decoded.UriSource = new Uri(target.Path); decoded.EndInit(); decoded.Freeze(); return decoded;
            });
            if (revision != Volatile.Read(ref _activationRevision)) return;
            TargetImage = image; TargetName = target.FileName; ActiveTarget = target; target.IsActive = true;
            foreach (var other in Targets.Where(other => !ReferenceEquals(other, target))) other.IsActive = false;
            Editor.ApplyTargetSnapshot(target.AppliedLookSnapshot, target.FilmSettingsSnapshot);
            await Editor.SetSourceAsync(target.AssetId, image);
            if (revision != Volatile.Read(ref _activationRevision)) return;
            target.Status = target.AppliedLookSnapshot is null ? ReferenceTargetStatus.Pending : ReferenceTargetStatus.Adjusted;
            StatusText = "待调色照片已载入。左侧原片与仿色结果对比，参考图片显示在独立区域。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.IO.FileFormatException)
        { target.Status = ReferenceTargetStatus.Failed; target.Error = "照片无法读取"; if (revision == Volatile.Read(ref _activationRevision)) StatusText = "待调色照片无法读取；现有色彩方案保持不变。"; }
        finally { IsLoading = Interlocked.Decrement(ref _loadingActivations) > 0; }
    }

    public void Dispose() { _exportCancellation?.Cancel(); _exportCancellation?.Dispose(); Editor.Dispose(); }

    private async Task ChooseTargetAsync()
    {
        var paths = _dialogs.ChooseFiles("导入待调色照片（源文件只读）", "图片|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp", true);
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

    private async Task ExportAsync(IReadOnlyList<ReferenceTargetItem> items)
    {
        if (items.Count == 0 || IsExporting) return;
        var directory = _dialogs.ChooseFolder("选择批量导出目录", null);
        if (directory is null) return;
        if (ActiveTarget is { } active && items.Contains(active)) Editor.CopyCurrentLookTo([active]);
        var frozen = items.Select(item => (Item: item, Look: item.AppliedLookSnapshot is { } look ? look with { ReferenceSources = look.ReferenceSources.Select(source => source with { }).ToArray() } : null, Film: item.FilmSettingsSnapshot is { } film ? film with { } : null)).ToArray();
        _exportCancellation = new CancellationTokenSource(); ExportCompleted = 0; ExportTotal = frozen.Length; ExportStatus = $"0 / {ExportTotal}"; RaiseExportCommands();
        try
        {
            foreach (var frozenItem in frozen)
            {
                var item = frozenItem.Item; _exportCancellation.Token.ThrowIfCancellationRequested(); item.Status = ReferenceTargetStatus.Processing; ExportStatus = $"{ExportCompleted} / {ExportTotal} · {item.FileName}";
                var output = Path.Combine(directory, Path.GetFileNameWithoutExtension(item.FileName) + "_仿色.jpg"); var temp = output + ".tmp";
                try { var processed = await Editor.ProcessForExportAsync(item.Path, frozenItem.Look, frozenItem.Film, _exportCancellation.Token); await Task.Run(() => EncodeJpeg(processed, temp, _exportCancellation.Token), _exportCancellation.Token); File.Move(temp, output, overwrite: false); item.OutputPath = output; item.ExportStatus = ReferenceExportStatus.Succeeded; item.Status = ReferenceTargetStatus.Exported; }
                catch (OperationCanceledException) { TryDelete(temp); item.ExportStatus = ReferenceExportStatus.Cancelled; item.Status = ReferenceTargetStatus.Pending; throw; }
                catch { TryDelete(temp); item.ExportStatus = ReferenceExportStatus.Failed; item.Status = ReferenceTargetStatus.Failed; }
                ExportCompleted++; ExportStatus = $"{ExportCompleted} / {ExportTotal}";
            }
            StatusText = "批量导出已完成。";
        }
        catch (OperationCanceledException) { ExportStatus = $"已停止 · {ExportCompleted} / {ExportTotal}"; StatusText = "已停止导出；已完成文件保留，未完成文件已清理。"; }
        finally { _exportCancellation.Dispose(); _exportCancellation = null; RaiseExportCommands(); }
    }
    private static void EncodeJpeg(BitmapSource image, string output, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var encoder = new JpegBitmapEncoder { QualityLevel = 95 }; encoder.Frames.Add(BitmapFrame.Create(image)); using var stream = File.Create(output); encoder.Save(stream); token.ThrowIfCancellationRequested();
    }
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    private void RaiseExportCommands() { ExportSelectedCommand.RaiseCanExecuteChanged(); ExportAllCommand.RaiseCanExecuteChanged(); StopExportCommand.RaiseCanExecuteChanged(); }
}

public enum ReferenceTargetStatus { Pending, Processing, Synced, Adjusted, Exported, Failed }
public enum ReferenceExportStatus { None, Queued, Exporting, Succeeded, Failed, Cancelled }

public sealed class ReferenceTargetItem : ObservableObject
{
    private bool _isSelected;
    private bool _isActive;
    private ReferenceTargetStatus _status = ReferenceTargetStatus.Pending;
    public ReferenceTargetItem(string path) { Id = Guid.NewGuid(); Path = path; FileName = System.IO.Path.GetFileName(path); }
    public Guid Id { get; }
    public Guid? AssetId { get; set; }
    public string Path { get; }
    public string FileName { get; }
    public BitmapSource? Thumbnail { get; set; }
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    public long FileSize => File.Exists(Path) ? new FileInfo(Path).Length : 0;
    private int _rating;
    public int Rating { get => _rating; set => SetProperty(ref _rating, Math.Clamp(value, 0, 5)); }
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
    public ReferenceTargetStatus Status { get => _status; set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(StatusText)); } }
    public string StatusText => Status switch { ReferenceTargetStatus.Synced => "已同步", ReferenceTargetStatus.Adjusted => "已调整", ReferenceTargetStatus.Processing => "正在处理", ReferenceTargetStatus.Exported => "已导出", ReferenceTargetStatus.Failed => "失败", _ => "待处理" };
    public double? Progress { get; set; }
    public string? Error { get; set; }
    public ReferenceLook? AppliedLookSnapshot { get; set; }
    public PixelTartFilmSettings? FilmSettingsSnapshot { get; set; }
    public string? OutputPath { get; set; }
    public ReferenceExportStatus ExportStatus { get; set; }
}
