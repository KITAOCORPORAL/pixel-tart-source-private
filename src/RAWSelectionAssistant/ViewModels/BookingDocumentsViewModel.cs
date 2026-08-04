using System.Collections.ObjectModel;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed record BookingDocumentTypeOption(BookingDocumentType Value, string Label);

public sealed class BookingDocumentsViewModel : ObservableObject
{
    public const string SupportedFileFilter = "支持的资料|*.pdf;*.doc;*.docx;*.ppt;*.pptx;*.xls;*.xlsx;*.txt;*.jpg;*.jpeg;*.png|所有文件|*.*";
    private readonly IBookingDocumentWorkflowService _workflow;
    private readonly IDialogService _dialogs;
    private Guid _bookingId;
    private Guid? _projectId;
    private bool _isArchived;
    private bool _isBusy;
    private string _statusText = "尚未关联本地资料";
    private string? _sessionDestinationPreference;
    private BookingDocumentTypeOption _selectedDocumentType;

    public BookingDocumentsViewModel(IBookingDocumentWorkflowService workflow, IDialogService dialogs)
    {
        _workflow = workflow;
        _dialogs = dialogs;
        DocumentTypes =
        [
            new(BookingDocumentType.PhotographyPlan, "摄影策划"), new(BookingDocumentType.ShootAgreement, "拍摄协议"),
            new(BookingDocumentType.ModelRelease, "模特授权书"), new(BookingDocumentType.Quotation, "报价单"),
            new(BookingDocumentType.VenueMaterial, "场地资料"), new(BookingDocumentType.WardrobeReference, "服装参考"),
            new(BookingDocumentType.LightingDiagram, "灯光图"), new(BookingDocumentType.Other, "其他")
        ];
        _selectedDocumentType = DocumentTypes[0];
        AddReferenceCommand = new AsyncRelayCommand(_ => ChooseAndAddReferencesAsync(), _ => CanModify);
        CopyAndAssociateCommand = new AsyncRelayCommand(_ => ChooseAndCopyAsync(), _ => CanModify);
        CheckAllCommand = new AsyncRelayCommand(_ => CheckAllAsync(), _ => !IsBusy);
        OpenCommand = new AsyncRelayCommand(parameter => parameter is BookingDocumentItemViewModel item ? OpenAsync(item, reveal: false) : Task.CompletedTask);
        RevealCommand = new AsyncRelayCommand(parameter => parameter is BookingDocumentItemViewModel item ? OpenAsync(item, reveal: true) : Task.CompletedTask);
        CheckCommand = new AsyncRelayCommand(parameter => parameter is BookingDocumentItemViewModel item ? CheckAsync(item) : Task.CompletedTask);
        RelocateCommand = new AsyncRelayCommand(parameter => parameter is BookingDocumentItemViewModel item ? RelocateAsync(item) : Task.CompletedTask, _ => CanModify);
        RemoveAssociationCommand = new AsyncRelayCommand(parameter => parameter is BookingDocumentItemViewModel item ? RemoveAsync(item) : Task.CompletedTask, _ => CanModify);
        TogglePathCommand = new RelayCommand(parameter => { if (parameter is BookingDocumentItemViewModel item) item.IsPathExpanded = !item.IsPathExpanded; });
        RetryAssociationCommand = new AsyncRelayCommand(parameter => parameter is PendingDocumentActionViewModel item ? RetryAsync(item) : Task.CompletedTask);
        UndoCopyCommand = new AsyncRelayCommand(parameter => parameter is PendingDocumentActionViewModel item ? UndoAsync(item) : Task.CompletedTask);
        OpenOutputDirectoryCommand = new RelayCommand(parameter => { if (parameter is PendingDocumentActionViewModel item) RevealDirectoryRequested?.Invoke(this, item.Pending.DestinationPath); });
        AbandonAssociationCommand = new AsyncRelayCommand(parameter => parameter is PendingDocumentActionViewModel item ? AbandonAsync(item) : Task.CompletedTask);
    }

    public event EventHandler<string>? OpenFileRequested;
    public event EventHandler<string>? RevealFileRequested;
    public event EventHandler<string>? RevealDirectoryRequested;
    public IReadOnlyList<BookingDocumentTypeOption> DocumentTypes { get; }
    public ObservableCollection<BookingDocumentItemViewModel> Items { get; } = [];
    public ObservableCollection<PendingDocumentActionViewModel> PendingActions { get; } = [];
    public ICommand AddReferenceCommand { get; }
    public ICommand CopyAndAssociateCommand { get; }
    public ICommand CheckAllCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand RevealCommand { get; }
    public ICommand CheckCommand { get; }
    public ICommand RelocateCommand { get; }
    public ICommand RemoveAssociationCommand { get; }
    public ICommand TogglePathCommand { get; }
    public ICommand RetryAssociationCommand { get; }
    public ICommand UndoCopyCommand { get; }
    public ICommand OpenOutputDirectoryCommand { get; }
    public ICommand AbandonAssociationCommand { get; }
    public bool IsBusy { get => _isBusy; private set { if (!SetProperty(ref _isBusy, value)) return; OnPropertyChanged(nameof(CanModify)); RefreshCommands(); } }
    public bool IsArchived { get => _isArchived; private set { if (!SetProperty(ref _isArchived, value)) return; OnPropertyChanged(nameof(CanModify)); RefreshCommands(); } }
    public bool CanModify => !IsArchived && !IsBusy;
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public BookingDocumentTypeOption SelectedDocumentType { get => _selectedDocumentType; set => SetProperty(ref _selectedDocumentType, value); }

    public async Task LoadAsync(Guid bookingId, Guid? projectId, bool isArchived, CancellationToken cancellationToken = default)
    {
        _bookingId = bookingId;
        _projectId = projectId;
        IsArchived = isArchived;
        _sessionDestinationPreference = null;
        await RefreshAndVerifyAsync(cancellationToken).ConfigureAwait(true);
    }

    public async Task HandleDroppedFilesAsync(IReadOnlyList<string> paths, BookingDocumentLinkMode mode)
    {
        if (!CanModify || paths.Count == 0) return;
        if (paths.Any(Directory.Exists))
        {
            _dialogs.ShowInfo("当前版本只支持添加单个或多个文件，不会扫描文件夹。");
            return;
        }
        if (mode == BookingDocumentLinkMode.Reference) await AddReferencesAsync(paths).ConfigureAwait(true);
        else await CopyAsync(paths).ConfigureAwait(true);
    }

    private async Task ChooseAndAddReferencesAsync()
    {
        var paths = _dialogs.ChooseFiles("添加本地摄影资料（仅关联原位置）", SupportedFileFilter, multiselect: true);
        if (paths.Count > 0) await AddReferencesAsync(paths).ConfigureAwait(true);
    }

    private async Task ChooseAndCopyAsync()
    {
        var paths = _dialogs.ChooseFiles("选择要复制到项目资料目录的文件", SupportedFileFilter, multiselect: true);
        if (paths.Count > 0) await CopyAsync(paths).ConfigureAwait(true);
    }

    private async Task AddReferencesAsync(IReadOnlyList<string> paths)
    {
        IsBusy = true;
        try
        {
            var result = await _workflow.AddReferencesAsync(new(_bookingId, _projectId, SelectedDocumentType.Value, paths)).ConfigureAwait(true);
            ApplyBatchResult(result);
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex) { _dialogs.ShowError(PrivacySafeMessage(ex)); }
        finally { IsBusy = false; }
    }

    private async Task CopyAsync(IReadOnlyList<string> paths)
    {
        IsBusy = true;
        try
        {
            var destination = await _workflow.GetSuggestedDestinationAsync(_projectId, SelectedDocumentType.Value).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(destination))
            {
                destination = _sessionDestinationPreference;
                if (string.IsNullOrWhiteSpace(destination)) destination = _dialogs.ChooseFolder("选择本次排期的目标资料目录");
                if (string.IsNullOrWhiteSpace(destination)) return;
                _sessionDestinationPreference = destination;
            }
            var result = await _workflow.CopyAndAssociateAsync(new(_bookingId, _projectId, SelectedDocumentType.Value, paths, destination, VerifySha256: true)).ConfigureAwait(true);
            ApplyBatchResult(result);
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex) { _dialogs.ShowError(PrivacySafeMessage(ex)); }
        finally { IsBusy = false; }
    }

    private async Task RefreshAndVerifyAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
            foreach (var item in Items.ToArray()) await CheckAsync(item, cancellationToken).ConfigureAwait(true);
            StatusText = Items.Count == 0 ? "尚未关联本地资料" : $"已关联 {Items.Count} 份本地资料";
        }
        finally { IsBusy = false; }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var documents = await _workflow.ListAsync(_bookingId, cancellationToken).ConfigureAwait(true);
        Items.Clear();
        foreach (var document in documents) Items.Add(new(document));
    }

    private async Task CheckAllAsync()
    {
        IsBusy = true;
        try
        {
            foreach (var item in Items.ToArray()) await CheckAsync(item).ConfigureAwait(true);
            StatusText = $"已检查 {Items.Count} 份资料";
        }
        finally { IsBusy = false; }
    }

    private async Task CheckAsync(BookingDocumentItemViewModel item, CancellationToken cancellationToken = default)
    {
        var result = await _workflow.VerifyAsync(item.Id, cancellationToken).ConfigureAwait(true);
        if (result is not null) item.Apply(result);
    }

    private async Task OpenAsync(BookingDocumentItemViewModel item, bool reveal)
    {
        var result = await _workflow.VerifyAsync(item.Id).ConfigureAwait(true);
        if (result is null) return;
        item.Apply(result);
        if (result.State == BookingDocumentFileState.Missing)
        {
            _dialogs.ShowInfo("文件已移动或当前不可访问。可以使用“重新定位”更新关联。" );
            return;
        }
        if (reveal) RevealFileRequested?.Invoke(this, result.Document.FilePath);
        else OpenFileRequested?.Invoke(this, result.Document.FilePath);
    }

    private async Task RelocateAsync(BookingDocumentItemViewModel item)
    {
        var paths = _dialogs.ChooseFiles("重新定位本地资料", SupportedFileFilter, multiselect: false);
        if (paths.Count == 0) return;
        var result = await _workflow.RelocateAsync(item.Id, paths[0], acceptHashMismatch: false).ConfigureAwait(true);
        if (result.RequiresConfirmation)
        {
            if (!_dialogs.Confirm("所选文件与原记录哈希不一致。是否接受新文件并更新关联？", "文件内容不一致")) return;
            result = await _workflow.RelocateAsync(item.Id, paths[0], acceptHashMismatch: true).ConfigureAwait(true);
        }
        if (result.Status == BookingDocumentRelocationStatus.Relocated && result.Document is not null) item.Apply(new(result.Document, BookingDocumentFileState.Normal, result.Message));
        else _dialogs.ShowInfo(result.Message);
    }

    private async Task RemoveAsync(BookingDocumentItemViewModel item)
    {
        if (!_dialogs.Confirm("仅从当前拍摄中移除关联，不会删除电脑中的原文件。", "移除关联")) return;
        if (await _workflow.RemoveAssociationAsync(item.Id).ConfigureAwait(true))
        {
            Items.Remove(item);
            StatusText = $"已关联 {Items.Count} 份本地资料";
        }
    }

    private async Task RetryAsync(PendingDocumentActionViewModel item)
    {
        var result = await _workflow.RetryAssociationAsync(item.Pending).ConfigureAwait(true);
        item.StatusText = result.Message;
        if (!result.Succeeded) return;
        PendingActions.Remove(item);
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task UndoAsync(PendingDocumentActionViewModel item)
    {
        var summary = await _workflow.UndoCopiedFileAsync(item.Pending).ConfigureAwait(true);
        item.StatusText = summary.Succeeded == 1 ? "本次复制已安全撤销。" : "文件已变化或被其他关联使用，未删除并转为等待确认。";
        if (summary.Succeeded == 1) PendingActions.Remove(item);
    }

    private async Task AbandonAsync(PendingDocumentActionViewModel item)
    {
        await _workflow.AbandonAssociationAsync(item.Pending).ConfigureAwait(true);
        PendingActions.Remove(item);
        StatusText = "已保留复制文件并放弃创建关联。";
    }

    private void ApplyBatchResult(BookingDocumentBatchResult result)
    {
        foreach (var outcome in result.Items.Where(item => item.PendingAssociation is not null))
            if (!PendingActions.Any(item => item.Pending.TaskId == outcome.PendingAssociation!.TaskId && string.Equals(item.Pending.DestinationPath, outcome.PendingAssociation.DestinationPath, StringComparison.OrdinalIgnoreCase)))
                PendingActions.Add(new(outcome.PendingAssociation!, outcome.Message));
        StatusText = $"总数 {result.Summary.Total} · 成功 {result.Successful} · 失败 {result.Failed} · 跳过 {result.Skipped} · 等待确认 {result.WaitingForAttention}";
    }

    private void RefreshCommands()
    {
        (AddReferenceCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CopyAndAssociateCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RelocateCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RemoveAssociationCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private static string PrivacySafeMessage(Exception ex) => ex switch
    {
        UnauthorizedAccessException => "目标目录没有写入权限或属于受保护位置。",
        FileNotFoundException or DirectoryNotFoundException => "文件不存在或存储设备当前不可用。",
        NotSupportedException => ex.Message,
        IOException => "文件当前被占用、路径过长、空间不足或目标不可写。",
        InvalidOperationException => ex.Message,
        _ => "文档操作未完成，文件和原始数据保持不变。"
    };
}

public sealed class BookingDocumentItemViewModel : ObservableObject
{
    private BookingDocumentRecord _document;
    private BookingDocumentFileState _state;
    private string _statusMessage = string.Empty;
    private bool _isPathExpanded;
    public BookingDocumentItemViewModel(BookingDocumentRecord document)
    {
        _document = document;
        _state = document.IsMissing ? BookingDocumentFileState.Missing : BookingDocumentFileState.Normal;
    }
    public Guid Id => _document.Id;
    public string DisplayName => _document.DisplayName;
    public string DocumentTypeText => DocumentTypeLabel(_document.DocumentType);
    public string ExtensionText => _document.FileExtension.TrimStart('.').ToUpperInvariant();
    public string StateText => _state switch { BookingDocumentFileState.Normal => "正常", BookingDocumentFileState.Missing => "当前不可访问", BookingDocumentFileState.Modified => "文件被修改", BookingDocumentFileState.WaitingForConfirmation => "等待确认", BookingDocumentFileState.Copying => "复制中", BookingDocumentFileState.PartiallyCompleted => "部分完成", _ => "失败" };
    public string LinkModeText => _document.LinkMode == BookingDocumentLinkMode.Reference ? "仅关联原位置" : "项目资料副本";
    public string AddedText => _document.AddedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string VerifiedText => _document.LastVerifiedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "尚未检查";
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool IsMissing => _state == BookingDocumentFileState.Missing;
    public bool IsModified => _state == BookingDocumentFileState.Modified;
    public bool IsPathExpanded { get => _isPathExpanded; set { if (!SetProperty(ref _isPathExpanded, value)) return; OnPropertyChanged(nameof(PathActionText)); } }
    public string PathActionText => IsPathExpanded ? "隐藏完整路径" : "显示完整路径";
    public string FullPath => _document.FilePath;

    public void Apply(BookingDocumentCheckResult result)
    {
        _document = result.Document;
        _state = result.State;
        StatusMessage = result.Message;
        foreach (var name in new[] { nameof(DisplayName), nameof(DocumentTypeText), nameof(ExtensionText), nameof(StateText), nameof(LinkModeText), nameof(AddedText), nameof(VerifiedText), nameof(IsMissing), nameof(IsModified), nameof(FullPath) }) OnPropertyChanged(name);
    }

    private static string DocumentTypeLabel(BookingDocumentType type) => type switch
    {
        BookingDocumentType.PhotographyPlan => "摄影策划", BookingDocumentType.ShootAgreement => "拍摄协议", BookingDocumentType.ModelRelease => "模特授权书",
        BookingDocumentType.Quotation => "报价单", BookingDocumentType.VenueMaterial => "场地资料", BookingDocumentType.WardrobeReference => "服装参考",
        BookingDocumentType.LightingDiagram => "灯光图", _ => "其他"
    };
}

public sealed class PendingDocumentActionViewModel(PendingDocumentAssociation pending, string statusText) : ObservableObject
{
    private string _statusText = statusText;
    public PendingDocumentAssociation Pending { get; } = pending;
    public string DisplayText => "文件已复制，但关联记录未保存";
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
}
