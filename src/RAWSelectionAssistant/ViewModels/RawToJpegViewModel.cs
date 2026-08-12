using System.Collections.ObjectModel;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed class RawToJpegItemViewModel : ObservableObject
{
    private RawToJpegItemState _state = RawToJpegItemState.Failed;
    private string _status = "待处理";

    public RawToJpegItemViewModel(string path) => SourcePath = Path.GetFullPath(path);
    public string SourcePath { get; }
    public string DisplayName => Path.GetFileName(SourcePath);
    public RawToJpegItemState State { get => _state; internal set { if (SetProperty(ref _state, value)) OnPropertyChanged(nameof(StateText)); } }
    public string Status { get => _status; internal set => SetProperty(ref _status, value); }
    public string StateText => State switch
    {
        RawToJpegItemState.Completed => "已完成",
        RawToJpegItemState.NeedsAttention => "需要处理",
        RawToJpegItemState.PartiallyCompleted => "部分完成",
        RawToJpegItemState.Cancelled => "已取消",
        _ => "待处理"
    };
}

public sealed class RawToJpegViewModel : ObservableObject
{
    public const string RawFileFilter = "RAW 文件|*.arw;*.cr2;*.cr3;*.nef;*.nrw;*.raf;*.dng;*.rw2;*.orf;*.ori;*.pef;*.3fr;*.fff;*.iiq;*.srw;*.rwl|所有文件|*.*";
    private readonly IRawToJpegTaskCoordinator _coordinator;
    private readonly IDialogService _dialogs;
    private string _destinationDirectory = string.Empty;
    private int _jpegQuality = RawToJpegDefaults.DefaultQuality;
    private int? _longestEdge;
    private bool _useCameraWhiteBalance = true;
    private bool _preserveExif = true;
    private bool _autoRotate = true;
    private bool _isBusy;
    private double _progress;
    private string _statusText = "选择 RAW 文件开始转换；源文件不会被修改。";
    private Guid? _activeTaskId;
    private bool _cancelRequested;

    public RawToJpegViewModel(IRawToJpegTaskCoordinator coordinator, IDialogService dialogs)
    {
        _coordinator = coordinator;
        _dialogs = dialogs;
        var capability = coordinator.GetCapability();
        CapabilityText = capability.IsAvailable
            ? $"{capability.DecoderName} {capability.Version ?? ""}；已验证格式：{(capability.VerifiedExtensions.Count == 0 ? "尚未探测" : string.Join(", ", capability.VerifiedExtensions))}"
            : "RAW 解码器当前不可用；不会生成伪造 JPG。";
        AddFilesCommand = new RelayCommand(_ => AddFiles());
        ChooseDestinationCommand = new RelayCommand(_ => ChooseDestination());
        StartCommand = new AsyncRelayCommand(_ => StartAsync(), _ => CanStart);
        CancelCommand = new AsyncRelayCommand(_ => CancelAsync(), _ => _activeTaskId.HasValue);
    }

    public ObservableCollection<RawToJpegItemViewModel> Items { get; } = [];
    public string CapabilityText { get; }
    public ICommand AddFilesCommand { get; }
    public ICommand ChooseDestinationCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand CancelCommand { get; }
    public string DestinationDirectory { get => _destinationDirectory; set { if (SetProperty(ref _destinationDirectory, value)) RaiseCommands(); } }
    public int JpegQuality { get => _jpegQuality; set { if (SetProperty(ref _jpegQuality, Math.Clamp(value, RawToJpegDefaults.MinimumQuality, RawToJpegDefaults.MaximumQuality))) RaiseCommands(); } }
    public int? LongestEdge { get => _longestEdge; set { if (SetProperty(ref _longestEdge, value)) RaiseCommands(); } }
    public bool UseCameraWhiteBalance { get => _useCameraWhiteBalance; set => SetProperty(ref _useCameraWhiteBalance, value); }
    public bool PreserveExif { get => _preserveExif; set => SetProperty(ref _preserveExif, value); }
    public bool AutoRotate { get => _autoRotate; set => SetProperty(ref _autoRotate, value); }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) RaiseCommands(); } }
    public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool CanStart => !IsBusy && Items.Count > 0 && Directory.Exists(DestinationDirectory) && _coordinator.GetCapability().IsAvailable;

    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(IsCandidateFile).Distinct(StringComparer.OrdinalIgnoreCase))
            if (Items.All(item => !string.Equals(item.SourcePath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))) Items.Add(new(path));
        RaiseCommands();
    }

    private void AddFiles() => AddFiles(_dialogs.ChooseFiles("选择 RAW 文件", RawFileFilter, true));
    private void ChooseDestination()
    {
        var folder = _dialogs.ChooseFolder("选择 JPG 输出目录", DestinationDirectory);
        if (folder is not null) DestinationDirectory = folder;
    }

    private async Task StartAsync()
    {
        if (!CanStart) return;
        IsBusy = true;
        Progress = 0;
        _cancelRequested = false;
        try
        {
            var request = new RawToJpegBatchRequest(Items.Select(x => x.SourcePath).ToArray(), DestinationDirectory,
                new RawToJpegOptions(JpegQuality, LongestEdge, UseCameraWhiteBalance, PreserveExif: PreserveExif, AutoRotate: AutoRotate));
            var taskId = await _coordinator.StartAsync(request, CancellationToken.None).ConfigureAwait(true);
            _activeTaskId = taskId;
            StatusText = $"任务已提交：{taskId:N}";
            RaiseCommands();
            await _coordinator.WaitForCompletionAsync(taskId, CancellationToken.None).ConfigureAwait(true);
            var terminal = await _coordinator.GetTaskStateAsync(taskId, CancellationToken.None).ConfigureAwait(true);
            if (!_cancelRequested && terminal is { State: TaskLifecycleState.Completed }) StatusText = "RAW 转 JPG 任务已完成；源文件保持不变。";
            else if (!_cancelRequested) StatusText = terminal is null ? "任务状态暂不可用；请打开任务中心查看原因。" : $"任务{terminal.State}：{terminal.LastErrorMessage ?? "请打开任务中心查看原因。"}";
        }
        catch (OperationCanceledException) { StatusText = "已取消；源 RAW 保持不变。"; }
        catch (Exception) { StatusText = "任务提交失败；请检查输出目录和解码器状态。"; }
        finally
        {
            _activeTaskId = null;
            IsBusy = false;
        }
    }

    private async Task CancelAsync()
    {
        if (_activeTaskId is not Guid taskId) return;
        _cancelRequested = true;
        await _coordinator.CancelAsync(taskId, CancellationToken.None).ConfigureAwait(true);
        await _coordinator.WaitForCompletionAsync(taskId, CancellationToken.None).ConfigureAwait(true);
        StatusText = "已安全取消；已完成输出保留，源 RAW 保持不变。";
    }

    private static bool IsCandidateFile(string path) => File.Exists(path) && RawToJpegDefaults.CandidateRawExtensions.Contains(Path.GetExtension(path));
    private void RaiseCommands()
    {
        (StartCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CancelCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }
}
