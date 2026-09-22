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

    public ReferenceColorWorkspaceViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
        Editor = new TetherReferenceModeViewModel(
            new ReferenceLookStore(Path.Combine(AppDataPaths.DataDirectory, "ProjectVisuals")), dialogs, allowReferenceManagement: true);
        Editor.Enabled = true;
        ChooseTargetCommand = new AsyncRelayCommand(_ => ChooseTargetAsync());
        StopProcessingCommand = new RelayCommand(_ => Editor.StopProcessing(), _ => Editor.IsBusy);
        SyncSelectedCommand = new RelayCommand(_ => Editor.CopyCurrentLookTo(SelectedTargets), _ => SelectedTargets.Any());
        SyncAllCommand = new RelayCommand(_ => Editor.CopyCurrentLookTo(Targets), _ => Targets.Count > 0);
        ActivateTargetCommand = new AsyncRelayCommand(value => value is ReferenceTargetItem target ? ActivateTargetAsync(target) : Task.CompletedTask);
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
    public ObservableCollection<ReferenceTargetItem> Targets { get; } = [];
    public IEnumerable<ReferenceTargetItem> SelectedTargets => Targets.Where(item => item.IsSelected);
    public ReferenceTargetItem? ActiveTarget { get => _activeTarget; private set => SetProperty(ref _activeTarget, value); }
    public BitmapSource? TargetImage { get => _targetImage; private set { if (SetProperty(ref _targetImage, value)) OnPropertyChanged(nameof(HasTarget)); } }
    public bool HasTarget => TargetImage is not null;
    public string TargetName { get => _targetName; private set => SetProperty(ref _targetName, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

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
        await ActivateTargetAsync(GetOrCreateTarget(path));
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
        try
        {
            IsLoading = true; StatusText = "正在载入活动预览…";
            var image = await Task.Run(() =>
            {
                var decoded = new BitmapImage(); decoded.BeginInit(); decoded.CacheOption = BitmapCacheOption.OnLoad; decoded.UriSource = new Uri(target.Path); decoded.EndInit(); decoded.Freeze(); return decoded;
            });
            TargetImage = image; TargetName = target.FileName; ActiveTarget = target; target.IsActive = true; target.IsSelected = true;
            foreach (var other in Targets.Where(other => !ReferenceEquals(other, target))) other.IsActive = false;
            Editor.ApplyTargetSnapshot(target.AppliedLookSnapshot, target.FilmSettingsSnapshot);
            await Editor.SetSourceAsync(target.AssetId, image);
            target.Status = target.AppliedLookSnapshot is null ? ReferenceTargetStatus.Pending : ReferenceTargetStatus.Adjusted;
            StatusText = "待调色照片已载入。左侧原片与仿色结果对比，参考图片显示在独立区域。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.IO.FileFormatException) { target.Status = ReferenceTargetStatus.Failed; target.Error = "照片无法读取"; StatusText = "待调色照片无法读取；现有色彩方案保持不变。"; }
        finally { IsLoading = false; }
    }

    public void Dispose() => Editor.Dispose();

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
            if (items.Length > 0) await ActivateTargetAsync(items[0]);
        }
        finally { IsLoading = false; }
    }
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
    public int Rating { get; set; }
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
