using System.Collections.ObjectModel;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.BatchCompression;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed class BatchCompressionItemViewModel : ObservableObject
{
    private BatchCompressionItemState _state = BatchCompressionItemState.Failed;
    private string _statusText = "待处理";

    public BatchCompressionItemViewModel(string sourcePath) => SourcePath = Path.GetFullPath(sourcePath);
    public string SourcePath { get; }
    public string DisplayName => Path.GetFileName(SourcePath);
    public BatchCompressionItemState State
    {
        get => _state;
        internal set
        {
            if (SetProperty(ref _state, value)) OnPropertyChanged(nameof(StateText));
        }
    }
    public string StatusText { get => _statusText; internal set => SetProperty(ref _statusText, value); }
    public string StateText => State switch
    {
        BatchCompressionItemState.Completed => "已完成",
        BatchCompressionItemState.NeedsAttention => "需要处理",
        BatchCompressionItemState.PartiallyCompleted => "部分完成",
        BatchCompressionItemState.Cancelled => "已取消",
        _ => "待处理"
    };
}

public sealed class BatchCompressionViewModel : ObservableObject
{
    private readonly IBatchCompressionTaskCoordinator _coordinator;
    private readonly IDialogService _dialogs;
    private string _destinationDirectory = string.Empty;
    private int _jpegQuality = BatchCompressionDefaults.DefaultJpegQuality;
    private int _longestEdge = BatchCompressionDefaults.DefaultLongestEdge;
    private bool _preserveMetadata = true;
    private bool _preserveIccProfile = true;
    private bool _isBusy;
    private double _progress;
    private string _statusText = "添加照片并选择独立输出目录；源文件不会被覆盖。";
    private Guid? _activeTaskId;

    public BatchCompressionViewModel(IBatchCompressionTaskCoordinator coordinator, IDialogService dialogs)
    {
        _coordinator = coordinator;
        _dialogs = dialogs;
        AddFilesCommand = new RelayCommand(_ => AddFiles());
        ChooseDestinationCommand = new RelayCommand(_ => ChooseDestination());
        StartCommand = new AsyncRelayCommand(_ => StartAsync(), _ => CanStart);
        CancelCommand = new AsyncRelayCommand(_ => CancelAsync(), _ => _activeTaskId.HasValue);
    }

    public ObservableCollection<BatchCompressionItemViewModel> Items { get; } = [];
    public ICommand AddFilesCommand { get; }
    public ICommand ChooseDestinationCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand CancelCommand { get; }
    public string DestinationDirectory
    {
        get => _destinationDirectory;
        set
        {
            if (SetProperty(ref _destinationDirectory, value ?? string.Empty)) RaiseCommands();
        }
    }
    public int JpegQuality
    {
        get => _jpegQuality;
        set
        {
            if (SetProperty(ref _jpegQuality, Math.Clamp(value, BatchCompressionDefaults.MinimumJpegQuality,
                    BatchCompressionDefaults.MaximumJpegQuality))) RaiseCommands();
        }
    }
    public int LongestEdge
    {
        get => _longestEdge;
        set
        {
            if (SetProperty(ref _longestEdge, Math.Clamp(value, BatchCompressionDefaults.MinimumLongestEdge,
                    BatchCompressionDefaults.MaximumLongestEdge))) RaiseCommands();
        }
    }
    public bool PreserveMetadata { get => _preserveMetadata; set => SetProperty(ref _preserveMetadata, value); }
    public bool PreserveIccProfile { get => _preserveIccProfile; set => SetProperty(ref _preserveIccProfile, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value)) RaiseCommands();
        }
    }
    public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool CanStart => !IsBusy && Items.Count > 0 && Directory.Exists(DestinationDirectory);

    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(IsSupportedFile).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var fullPath = Path.GetFullPath(path);
            if (Items.All(item => !string.Equals(item.SourcePath, fullPath, StringComparison.OrdinalIgnoreCase)))
                Items.Add(new BatchCompressionItemViewModel(fullPath));
        }
        RaiseCommands();
    }

    private void AddFiles() => AddFiles(_dialogs.ChooseFiles("选择要压缩的照片",
        "照片|*.jpg;*.jpeg;*.png;*.tif;*.tiff|所有文件|*.*", true));

    private void ChooseDestination()
    {
        var folder = _dialogs.ChooseFolder("选择独立输出目录", DestinationDirectory);
        if (folder is not null) DestinationDirectory = folder;
    }

    private async Task StartAsync()
    {
        if (!CanStart) return;
        IsBusy = true;
        Progress = 0;
        try
        {
            var request = new BatchCompressionRequest(Items.Select(item => item.SourcePath).ToArray(),
                DestinationDirectory, new BatchCompressionOptions(JpegQuality, LongestEdge, PreserveMetadata, PreserveIccProfile));
            _activeTaskId = await _coordinator.StartAsync(request).ConfigureAwait(true);
            var taskId = _activeTaskId.Value;
            foreach (var item in Items) item.StatusText = "已提交到任务中心";
            StatusText = "压缩任务已提交到任务中心；源文件保持不变。";
            await _coordinator.WaitForCompletionAsync(taskId).ConfigureAwait(true);
            var terminal = await _coordinator.GetTaskStateAsync(taskId).ConfigureAwait(true);
            if (terminal is not null)
                StatusText = $"{terminal.State} · {taskId:N}" + (string.IsNullOrWhiteSpace(terminal.LastErrorMessage) ? string.Empty : $" · {terminal.LastErrorMessage}");
            RaiseCommands();
        }
        catch (Exception)
        {
            StatusText = "压缩任务提交失败；请检查输出目录。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CancelAsync()
    {
        if (_activeTaskId is not Guid taskId) return;
        try
        {
            await _coordinator.CancelAsync(taskId).ConfigureAwait(true);
            await _coordinator.WaitForCompletionAsync(taskId).ConfigureAwait(true);
            StatusText = "已请求安全取消；已完成输出会保留，临时文件会清理。";
        }
        catch (KeyNotFoundException)
        {
            StatusText = "任务已经结束，请在任务中心查看结果。";
        }
        finally
        {
            _activeTaskId = null;
            RaiseCommands();
        }
    }

    private static bool IsSupportedFile(string path) =>
        File.Exists(path) && BatchCompressionDefaults.SupportedExtensions.Contains(Path.GetExtension(path));

    private void RaiseCommands()
    {
        (StartCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CancelCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }
}
