using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed record BookingDocumentTypeOption(BookingDocumentType Value, string Label) { public override string ToString() => Label; }
public sealed record BookingDocumentLinkModeOption(BookingDocumentLinkMode Value, string Label) { public override string ToString() => Label; }

public sealed class BookingDocumentsViewModel : ObservableObject
{
    public const string SupportedFileFilter = "支持的资料|*.pdf;*.doc;*.docx;*.ppt;*.pptx;*.xls;*.xlsx;*.txt;*.md;*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.psd;*.ai;*.zip;*.rar|所有文件|*.*";
    private readonly IBookingDocumentWorkflowService _workflow;
    private readonly IDialogService _dialogs;
    private readonly Dictionary<Guid, List<PendingDocumentActionViewModel>> _pendingByBooking = [];
    private Guid _bookingId;
    private Guid? _projectId;
    private bool _isArchived;
    private bool _isBusy;
    private bool _isDraftOnly;
    private readonly List<DraftDocumentOperation> _draftOperations = [];
    private string _statusText = "尚未关联本地资料";
    private string? _sessionDestinationPreference;
    private BookingDocumentTypeOption _selectedDocumentType;
    private BookingDocumentLinkModeOption _selectedLinkMode;
    private BookingDocumentItemViewModel? _selectedItem;
    private DocumentPreviewKind _previewKind;
    private ImageSource? _previewImage;
    private string _previewText = string.Empty;
    private string _previewTitle = "选择资料以预览";
    private string _previewMessage = "图片、PDF、TXT 和 Markdown 可在本地安全预览；Office 与未知格式显示文件卡片。";
    private double _previewZoom = 1d;
    private int _previewPageIndex;
    private int _previewPageCount;
    private string _previewSearchText = string.Empty;
    private string _previewSearchResult = string.Empty;
    private bool _previewTextWrap = true;

    public BookingDocumentsViewModel(IBookingDocumentWorkflowService workflow, IDialogService dialogs)
    {
        _workflow = workflow;
        _dialogs = dialogs;
        Items.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(HasItems)); OnPropertyChanged(nameof(HasNoItems)); };
        DocumentTypes =
        [
            new(BookingDocumentType.PhotographyPlan, "摄影策划"), new(BookingDocumentType.ShootAgreement, "拍摄协议"),
            new(BookingDocumentType.ModelRelease, "模特授权书"), new(BookingDocumentType.Quotation, "报价单"),
            new(BookingDocumentType.VenueMaterial, "场地资料"), new(BookingDocumentType.WardrobeReference, "服装参考"),
            new(BookingDocumentType.LightingDiagram, "灯光图"), new(BookingDocumentType.CameraDiagram, "机位图"),
            new(BookingDocumentType.MoodBoard, "情绪板"), new(BookingDocumentType.StaffFile, "工作人员资料"), new(BookingDocumentType.Other, "其他")
        ];
        _selectedDocumentType = DocumentTypes[0];
        LinkModes = [new(BookingDocumentLinkMode.Reference, "仅关联原位置"), new(BookingDocumentLinkMode.ManagedCopy, "安全复制到项目目录")];
        _selectedLinkMode = LinkModes[0];
        AddDocumentsCommand = new AsyncRelayCommand(_ => ChooseAndAddUsingSelectedModeAsync(), _ => CanModify);
        AddReferenceCommand = new AsyncRelayCommand(_ => ChooseAndAddReferencesAsync(), _ => CanModify);
        CopyAndAssociateCommand = new AsyncRelayCommand(_ => ChooseAndCopyAsync(), _ => CanModify);
        CheckAllCommand = new AsyncRelayCommand(_ => CheckAllAsync(), _ => !IsBusy);
        OpenCommand = new AsyncRelayCommand(parameter => parameter is BookingDocumentItemViewModel item ? ExecuteSafelyAsync(() => OpenAsync(item, reveal: false)) : Task.CompletedTask);
        RevealCommand = new AsyncRelayCommand(parameter => parameter is BookingDocumentItemViewModel item ? ExecuteSafelyAsync(() => OpenAsync(item, reveal: true)) : Task.CompletedTask);
        CheckCommand = new AsyncRelayCommand(parameter => parameter is BookingDocumentItemViewModel item ? ExecuteSafelyAsync(() => CheckAsync(item)) : Task.CompletedTask);
        RelocateCommand = new AsyncRelayCommand(parameter => parameter is BookingDocumentItemViewModel item ? ExecuteSafelyAsync(() => RelocateAsync(item)) : Task.CompletedTask, _ => CanModify);
        RemoveAssociationCommand = new AsyncRelayCommand(parameter => parameter is BookingDocumentItemViewModel item ? ExecuteSafelyAsync(() => RemoveAsync(item)) : Task.CompletedTask, _ => CanModify);
        TogglePathCommand = new RelayCommand(parameter => { if (parameter is BookingDocumentItemViewModel item) item.IsPathExpanded = !item.IsPathExpanded; });
        RetryAssociationCommand = new AsyncRelayCommand(parameter => parameter is PendingDocumentActionViewModel item ? ExecuteSafelyAsync(() => RetryAsync(item)) : Task.CompletedTask);
        UndoCopyCommand = new AsyncRelayCommand(parameter => parameter is PendingDocumentActionViewModel item ? ExecuteSafelyAsync(() => UndoAsync(item)) : Task.CompletedTask);
        OpenOutputDirectoryCommand = new RelayCommand(parameter => { if (parameter is PendingDocumentActionViewModel item) RevealDirectoryRequested?.Invoke(this, item.Pending.DestinationPath); });
        AbandonAssociationCommand = new AsyncRelayCommand(parameter => parameter is PendingDocumentActionViewModel item ? ExecuteSafelyAsync(() => AbandonAsync(item)) : Task.CompletedTask);
        PreviewCommand = new AsyncRelayCommand(parameter => parameter is BookingDocumentItemViewModel item ? PreviewAsync(item) : Task.CompletedTask);
        PreviousPreviewCommand = new AsyncRelayCommand(_ => NavigatePreviewAsync(-1), _ => CanPreviousPreview);
        NextPreviewCommand = new AsyncRelayCommand(_ => NavigatePreviewAsync(1), _ => CanNextPreview);
        ZoomInCommand = new RelayCommand(_ => PreviewZoom = Math.Min(4d, PreviewZoom + 0.25d));
        ZoomOutCommand = new RelayCommand(_ => PreviewZoom = Math.Max(0.25d, PreviewZoom - 0.25d));
        ActualSizeCommand = new RelayCommand(_ => PreviewZoom = 1d);
        FitPreviewCommand = new RelayCommand(_ => PreviewZoom = 1d);
        PreviousPdfPageCommand = new AsyncRelayCommand(_ => ChangePdfPageAsync(-1), _ => IsPdfPreview && PreviewPageIndex > 0);
        NextPdfPageCommand = new AsyncRelayCommand(_ => ChangePdfPageAsync(1), _ => IsPdfPreview && PreviewPageIndex + 1 < PreviewPageCount);
        ToggleTextWrapCommand = new RelayCommand(_ => PreviewTextWrap = !PreviewTextWrap);
    }

    public event EventHandler<string>? OpenFileRequested;
    public event EventHandler<string>? RevealFileRequested;
    public event EventHandler<string>? RevealDirectoryRequested;
    public IReadOnlyList<BookingDocumentTypeOption> DocumentTypes { get; }
    public IReadOnlyList<BookingDocumentLinkModeOption> LinkModes { get; }
    public ObservableCollection<BookingDocumentItemViewModel> Items { get; } = [];
    public ObservableCollection<PendingDocumentActionViewModel> PendingActions { get; } = [];
    public ICommand AddReferenceCommand { get; }
    public ICommand CopyAndAssociateCommand { get; }
    public ICommand AddDocumentsCommand { get; }
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
    public ICommand PreviewCommand { get; }
    public ICommand PreviousPreviewCommand { get; }
    public ICommand NextPreviewCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ActualSizeCommand { get; }
    public ICommand FitPreviewCommand { get; }
    public ICommand PreviousPdfPageCommand { get; }
    public ICommand NextPdfPageCommand { get; }
    public ICommand ToggleTextWrapCommand { get; }
    public bool IsBusy { get => _isBusy; private set { if (!SetProperty(ref _isBusy, value)) return; OnPropertyChanged(nameof(CanModify)); RefreshCommands(); } }
    public bool IsArchived { get => _isArchived; private set { if (!SetProperty(ref _isArchived, value)) return; OnPropertyChanged(nameof(CanModify)); RefreshCommands(); } }
    public bool CanModify => !IsArchived && !IsBusy;
    public bool HasDraftOperations => _draftOperations.Count > 0;
    public bool HasItems => Items.Count > 0;
    public bool HasNoItems => Items.Count == 0;
    public bool HasPreview => SelectedItem is not null;
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public BookingDocumentTypeOption SelectedDocumentType { get => _selectedDocumentType; set => SetProperty(ref _selectedDocumentType, value); }
    public BookingDocumentLinkModeOption SelectedLinkMode { get => _selectedLinkMode; set => SetProperty(ref _selectedLinkMode, value); }
    public BookingDocumentItemViewModel? SelectedItem { get => _selectedItem; set { if (!SetProperty(ref _selectedItem, value)) return; OnPropertyChanged(nameof(HasPreview)); OnPropertyChanged(nameof(CanPreviousPreview)); OnPropertyChanged(nameof(CanNextPreview)); RefreshPreviewCommands(); if (value is not null) _ = PreviewAsync(value); } }
    public DocumentPreviewKind PreviewKind { get => _previewKind; private set { if (!SetProperty(ref _previewKind, value)) return; OnPropertyChanged(nameof(IsImagePreview)); OnPropertyChanged(nameof(IsPdfPreview)); OnPropertyChanged(nameof(IsTextPreview)); OnPropertyChanged(nameof(IsCardPreview)); RefreshPreviewCommands(); } }
    public bool IsImagePreview => PreviewKind is DocumentPreviewKind.Image or DocumentPreviewKind.Pdf;
    public bool IsPdfPreview => PreviewKind == DocumentPreviewKind.Pdf;
    public bool IsTextPreview => PreviewKind == DocumentPreviewKind.Text;
    public bool IsCardPreview => PreviewKind is DocumentPreviewKind.OfficeCard or DocumentPreviewKind.Unsupported or DocumentPreviewKind.None;
    public ImageSource? PreviewImage
    {
        get => _previewImage;
        private set
        {
            if (!SetProperty(ref _previewImage, value)) return;
            OnPropertyChanged(nameof(PreviewDisplayWidth));
            OnPropertyChanged(nameof(PreviewDisplayHeight));
        }
    }
    public string PreviewText { get => _previewText; private set => SetProperty(ref _previewText, value); }
    public string PreviewTitle { get => _previewTitle; private set => SetProperty(ref _previewTitle, value); }
    public string PreviewMessage { get => _previewMessage; private set => SetProperty(ref _previewMessage, value); }
    public double PreviewZoom
    {
        get => _previewZoom;
        private set
        {
            if (!SetProperty(ref _previewZoom, value)) return;
            OnPropertyChanged(nameof(PreviewZoomText));
            OnPropertyChanged(nameof(PreviewDisplayWidth));
            OnPropertyChanged(nameof(PreviewDisplayHeight));
        }
    }
    public string PreviewZoomText => $"{PreviewZoom * 100:0}%";
    public double PreviewDisplayWidth => PreviewImage is BitmapSource bitmap ? bitmap.PixelWidth * PreviewZoom : double.NaN;
    public double PreviewDisplayHeight => PreviewImage is BitmapSource bitmap ? bitmap.PixelHeight * PreviewZoom : double.NaN;
    public int PreviewPageIndex { get => _previewPageIndex; private set { if (!SetProperty(ref _previewPageIndex, value)) return; OnPropertyChanged(nameof(PreviewPageText)); RefreshPreviewCommands(); } }
    public int PreviewPageCount { get => _previewPageCount; private set { if (!SetProperty(ref _previewPageCount, value)) return; OnPropertyChanged(nameof(PreviewPageText)); RefreshPreviewCommands(); } }
    public string PreviewPageText => PreviewPageCount <= 0 ? string.Empty : $"第 {PreviewPageIndex + 1} / {PreviewPageCount} 页";
    public bool CanPreviousPreview => SelectedItem is not null && Items.IndexOf(SelectedItem) > 0;
    public bool CanNextPreview => SelectedItem is not null && Items.IndexOf(SelectedItem) >= 0 && Items.IndexOf(SelectedItem) + 1 < Items.Count;
    public string PreviewSearchText { get => _previewSearchText; set { if (SetProperty(ref _previewSearchText, value ?? string.Empty)) UpdateTextSearch(); } }
    public string PreviewSearchResult { get => _previewSearchResult; private set => SetProperty(ref _previewSearchResult, value); }
    public bool PreviewTextWrap { get => _previewTextWrap; private set { if (!SetProperty(ref _previewTextWrap, value)) return; OnPropertyChanged(nameof(PreviewTextWrapping)); OnPropertyChanged(nameof(PreviewTextWrapLabel)); } }
    public TextWrapping PreviewTextWrapping => PreviewTextWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
    public string PreviewTextWrapLabel => PreviewTextWrap ? "取消换行" : "自动换行";

    public async Task LoadAsync(Guid bookingId, Guid? projectId, bool isArchived, CancellationToken cancellationToken = default)
    {
        StorePendingActions();
        _isDraftOnly = false;
        _draftOperations.Clear();
        _bookingId = bookingId;
        _projectId = projectId;
        IsArchived = isArchived;
        _sessionDestinationPreference = null;
        Items.Clear();
        ClearPreview();
        PendingActions.Clear();
        if (_pendingByBooking.TryGetValue(bookingId, out var pending))
            foreach (var item in pending) PendingActions.Add(item);
        try
        {
            foreach (var recovered in await _workflow.ListPendingAssociationsAsync(bookingId, cancellationToken).ConfigureAwait(true))
                if (!PendingActions.Any(item => item.Pending.TaskId == recovered.TaskId && string.Equals(item.Pending.DestinationPath, recovered.DestinationPath, StringComparison.OrdinalIgnoreCase)))
                    PendingActions.Add(new(recovered, "已从上次未完成的文档任务恢复。"));
            StorePendingActions();
            await RefreshAndVerifyAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = "本地资料暂时无法加载。";
            _dialogs.ShowError(PrivacySafeMessage(ex));
        }
    }

    public void Reset()
    {
        StorePendingActions();
        _isDraftOnly = false;
        _draftOperations.Clear();
        _bookingId = Guid.Empty;
        _projectId = null;
        IsArchived = true;
        Items.Clear();
        ClearPreview();
        PendingActions.Clear();
        StatusText = "尚未关联本地资料";
    }

    public void BeginDraft(Guid bookingId, Guid? projectId)
    {
        StorePendingActions();
        _bookingId = bookingId;
        _projectId = projectId;
        _isDraftOnly = true;
        IsArchived = false;
        _draftOperations.Clear();
        Items.Clear();
        PendingActions.Clear();
        ClearPreview();
        StatusText = "资料已暂存；创建排期成功后才会写入资料关联。";
        OnPropertyChanged(nameof(HasDraftOperations));
        RefreshCommands();
    }

    public async Task CommitDraftAsync(CancellationToken cancellationToken = default)
    {
        if (!_isDraftOnly || _draftOperations.Count == 0) return;
        var operations = _draftOperations.ToArray();
        try
        {
            foreach (var group in operations.Where(item => item.Mode == BookingDocumentLinkMode.Reference).GroupBy(item => item.DocumentType))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await _workflow.AddReferencesAsync(new(_bookingId, _projectId, group.Key, group.Select(item => item.Path).ToArray()), cancellationToken).ConfigureAwait(true);
                ApplyBatchResult(result);
                if (result.Status is BookingDocumentBatchStatus.Failed or BookingDocumentBatchStatus.NeedsAttention or BookingDocumentBatchStatus.PartiallyCompleted)
                    throw new InvalidOperationException("部分资料关联未完成，请在资料面板中重试。", new IOException());
            }
            foreach (var group in operations.Where(item => item.Mode == BookingDocumentLinkMode.ManagedCopy).GroupBy(item => item.DocumentType))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destination = await _workflow.GetSuggestedDestinationAsync(_projectId, group.Key, cancellationToken).ConfigureAwait(true);
                if (string.IsNullOrWhiteSpace(destination))
                {
                    destination = _sessionDestinationPreference;
                    if (string.IsNullOrWhiteSpace(destination)) destination = _dialogs.ChooseFolder("选择本次排期的目标资料目录");
                    if (string.IsNullOrWhiteSpace(destination)) throw new OperationCanceledException("未选择资料目录。", cancellationToken);
                    _sessionDestinationPreference = destination;
                }
                var result = await _workflow.CopyAndAssociateAsync(new(_bookingId, _projectId, group.Key, group.Select(item => item.Path).ToArray(), destination, VerifySha256: true), cancellationToken).ConfigureAwait(true);
                ApplyBatchResult(result);
                if (result.Status is BookingDocumentBatchStatus.Failed or BookingDocumentBatchStatus.NeedsAttention or BookingDocumentBatchStatus.PartiallyCompleted)
                    throw new InvalidOperationException("部分资料复制或关联未完成，请在资料面板中处理。", new IOException());
            }
            _draftOperations.Clear();
            _isDraftOnly = false;
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
            StatusText = Items.Count == 0 ? "尚未关联本地资料" : $"已关联 {Items.Count} 份本地资料";
            OnPropertyChanged(nameof(HasDraftOperations));
        }
        catch
        {
            StatusText = "排期已保存，但部分资料尚未完成关联；请在资料面板中重试。";
            throw;
        }
    }

#if RC3_REVIEW_ONLY
    public void ApplyReviewState(string state, string demoDirectory)
    {
        _bookingId = Guid.Parse("23030000-0000-0000-0000-000000000020");
        _projectId = Guid.Parse("23030000-0000-0000-0000-000000000021");
        IsArchived = false;
        Items.Clear();
        var now = DateTimeOffset.Now;
        var candidates = new[]
        {
            ("RC3_资料图片.png", BookingDocumentType.PhotographyPlan),
            ("RC3_拍摄策划.pdf", BookingDocumentType.PhotographyPlan),
            ("RC3_现场说明.txt", BookingDocumentType.StaffFile),
            ("RC3_报价参考.docx", BookingDocumentType.Quotation),
            ("RC3_未知格式.xyz", BookingDocumentType.Other)
        };
        foreach (var (name, type) in candidates)
        {
            var path = Path.Combine(demoDirectory, name);
            var info = new FileInfo(path);
            Items.Add(new BookingDocumentItemViewModel(new BookingDocumentRecord
            {
                BookingId = _bookingId, ProjectId = _projectId, DocumentType = type, DisplayName = name,
                FilePath = path, NormalizedPath = path.ToUpperInvariant(), FileExtension = info.Extension,
                FileSize = info.Exists ? info.Length : null, LastKnownModifiedAtUtc = info.Exists ? info.LastWriteTimeUtc : null,
                LinkMode = BookingDocumentLinkMode.Reference, AddedAtUtc = now.AddMinutes(-Items.Count * 7),
                UpdatedAtUtc = now, LastVerifiedAtUtc = now, IsMissing = false
            }));
        }
        _selectedItem = state switch
        {
            "DocumentsPdf" => Items.ElementAtOrDefault(1),
            "DocumentsText" => Items.ElementAtOrDefault(2),
            "DocumentsUnsupported" => Items.ElementAtOrDefault(4),
            _ => Items.FirstOrDefault()
        };
        OnPropertyChanged(nameof(SelectedItem));
        PreviewTitle = _selectedItem?.DisplayName ?? "RC3 合成资料";
        PreviewZoom = 1d;
        PreviewPageIndex = 0;
        PreviewPageCount = 0;
        PreviewSearchText = string.Empty;
        if (state == "DocumentsText")
        {
            PreviewKind = DocumentPreviewKind.Text;
            PreviewText = "RC3 合成拍摄说明\n- 客户资料仅为演示代号\n- 文件默认仅关联原位置\n- 移除关联不会删除电脑文件";
            PreviewMessage = "TXT 本地只读预览 · 已限制读取长度。";
            PreviewImage = null;
        }
        else if (state == "DocumentsUnsupported")
        {
            PreviewKind = DocumentPreviewKind.Unsupported;
            PreviewImage = null;
            PreviewText = string.Empty;
            PreviewMessage = "该格式不提供内容预览；仅显示安全文件卡片和元数据。";
        }
        else
        {
            var imagePath = Path.Combine(demoDirectory, "RC3_资料图片.png");
            var bitmap = new BitmapImage();
            bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(imagePath); bitmap.EndInit(); bitmap.Freeze();
            PreviewImage = bitmap;
            PreviewText = string.Empty;
            PreviewKind = state == "DocumentsPdf" ? DocumentPreviewKind.Pdf : DocumentPreviewKind.Image;
            PreviewPageCount = state == "DocumentsPdf" ? 3 : 0;
            PreviewMessage = state == "DocumentsPdf" ? "PDF 本地预览 · 第 1 / 3 页；文件流已释放。" : "图片预览已载入；文件流已释放。";
        }
        StatusText = $"RC3 隔离验收：已关联 {Items.Count} 份合成资料，未写入任何文件正文。";
    }
#endif

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

    private Task ChooseAndAddUsingSelectedModeAsync() => SelectedLinkMode.Value == BookingDocumentLinkMode.Reference
        ? ChooseAndAddReferencesAsync()
        : ChooseAndCopyAsync();

    private async Task ChooseAndCopyAsync()
    {
        var paths = _dialogs.ChooseFiles("选择要复制到项目资料目录的文件", SupportedFileFilter, multiselect: true);
        if (paths.Count > 0) await CopyAsync(paths).ConfigureAwait(true);
    }

    private async Task AddReferencesAsync(IReadOnlyList<string> paths)
    {
        if (_isDraftOnly)
        {
            Stage(paths, BookingDocumentLinkMode.Reference);
            return;
        }
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
        if (_isDraftOnly)
        {
            Stage(paths, BookingDocumentLinkMode.ManagedCopy);
            return;
        }
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
        catch (Exception ex) { _dialogs.ShowError(PrivacySafeMessage(ex)); }
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

    private async Task PreviewAsync(BookingDocumentItemViewModel item)
    {
        if (!ReferenceEquals(SelectedItem, item))
        {
            SelectedItem = item;
            return;
        }
        PreviewImage = null; PreviewText = string.Empty; PreviewTitle = item.DisplayName; PreviewZoom = 1d; PreviewPageIndex = 0; PreviewPageCount = 0; PreviewSearchText = string.Empty;
        var check = await _workflow.VerifyAsync(item.Id).ConfigureAwait(true);
        if (check is null || check.State == BookingDocumentFileState.Missing)
        {
            PreviewKind = DocumentPreviewKind.Unsupported; PreviewMessage = "文件已丢失或暂时不可访问，可使用“重新定位”修复关联。"; return;
        }
        var path = item.FullPath;
        var extension = Path.GetExtension(path).ToLowerInvariant();
        try
        {
            if (extension is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" or ".gif" or ".tif" or ".tiff")
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, true);
                var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
                PreviewImage = bitmap; PreviewKind = DocumentPreviewKind.Image; PreviewMessage = "图片预览已载入；文件流已释放。"; return;
            }
            if (extension == ".pdf")
            {
                await RenderPdfPageAsync(path, 0).ConfigureAwait(true); return;
            }
            if (extension is ".txt" or ".md")
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, true);
                using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true); var buffer = new char[65536]; var read = await reader.ReadBlockAsync(buffer.AsMemory()).ConfigureAwait(true);
                PreviewText = new string(buffer, 0, read); PreviewKind = DocumentPreviewKind.Text; PreviewMessage = read == buffer.Length ? "只显示前 64K 文本，未修改原文件。" : "文本预览已载入；文件流已释放。"; UpdateTextSearch(); return;
            }
            if (extension is ".doc" or ".docx" or ".ppt" or ".pptx" or ".xls" or ".xlsx")
            {
                PreviewKind = DocumentPreviewKind.OfficeCard; PreviewMessage = "Office 文件不解析正文；可安全打开或在资源管理器中定位。"; return;
            }
            PreviewKind = DocumentPreviewKind.Unsupported; PreviewMessage = "此格式暂不生成内容预览，仅显示安全文件卡片。";
        }
        catch { PreviewKind = DocumentPreviewKind.Unsupported; PreviewMessage = "预览暂时不可用；原文件未被修改。"; PreviewImage = null; PreviewText = string.Empty; }
    }

    private void ClearPreview()
    {
        SelectedItem = null; PreviewKind = DocumentPreviewKind.None; PreviewImage = null; PreviewText = string.Empty; PreviewTitle = "选择资料以预览"; PreviewMessage = "图片、PDF、TXT 和 Markdown 可在本地安全预览；Office 与未知格式显示文件卡片。"; PreviewZoom = 1d; PreviewPageIndex = 0; PreviewPageCount = 0; PreviewSearchText = string.Empty;
    }

    private async Task NavigatePreviewAsync(int offset)
    {
        if (SelectedItem is null) return;
        var target = Items.IndexOf(SelectedItem) + offset;
        if (target >= 0 && target < Items.Count) SelectedItem = Items[target];
        await Task.CompletedTask;
    }

    private async Task ChangePdfPageAsync(int offset)
    {
        if (SelectedItem is null || !IsPdfPreview) return;
        var target = Math.Clamp(PreviewPageIndex + offset, 0, Math.Max(0, PreviewPageCount - 1));
        if (target == PreviewPageIndex) return;
        await RenderPdfPageAsync(SelectedItem.FullPath, target).ConfigureAwait(true);
    }

    private async Task RenderPdfPageAsync(string path, int pageIndex)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        var document = await PdfDocument.LoadFromFileAsync(file);
        if (document.PageCount == 0) throw new InvalidDataException("PDF 没有可预览页面。");
        var safePage = Math.Clamp(pageIndex, 0, (int)document.PageCount - 1);
        using var page = document.GetPage((uint)safePage);
        using var random = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(random);
        random.Seek(0);
        using var input = random.AsStreamForRead();
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = input; bitmap.EndInit(); bitmap.Freeze();
        PreviewImage = bitmap; PreviewPageCount = (int)document.PageCount; PreviewPageIndex = safePage; PreviewKind = DocumentPreviewKind.Pdf; PreviewMessage = $"PDF 本地预览 · {PreviewPageText}；文件流已释放。";
    }

    private void UpdateTextSearch()
    {
        if (!IsTextPreview || string.IsNullOrWhiteSpace(PreviewSearchText)) { PreviewSearchResult = string.Empty; return; }
        var count = 0;
        for (var index = 0; (index = PreviewText.IndexOf(PreviewSearchText, index, StringComparison.CurrentCultureIgnoreCase)) >= 0; index += Math.Max(1, PreviewSearchText.Length)) count++;
        PreviewSearchResult = count == 0 ? "未找到" : $"找到 {count} 处";
    }

    private void RefreshPreviewCommands()
    {
        (PreviousPreviewCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (NextPreviewCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (PreviousPdfPageCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (NextPdfPageCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
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
        StorePendingActions();
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task UndoAsync(PendingDocumentActionViewModel item)
    {
        var summary = await _workflow.UndoCopiedFileAsync(item.Pending).ConfigureAwait(true);
        item.StatusText = summary.Succeeded == 1 ? "本次复制已安全撤销。" : "文件已变化或被其他关联使用，未删除并转为等待确认。";
        if (summary.Succeeded == 1)
        {
            PendingActions.Remove(item);
            StorePendingActions();
        }
    }

    private async Task AbandonAsync(PendingDocumentActionViewModel item)
    {
        await _workflow.AbandonAssociationAsync(item.Pending).ConfigureAwait(true);
        PendingActions.Remove(item);
        StorePendingActions();
        StatusText = "已保留复制文件并放弃创建关联。";
    }

    private void ApplyBatchResult(BookingDocumentBatchResult result)
    {
        foreach (var outcome in result.Items.Where(item => item.PendingAssociation is not null))
            if (!PendingActions.Any(item => item.Pending.TaskId == outcome.PendingAssociation!.TaskId && string.Equals(item.Pending.DestinationPath, outcome.PendingAssociation.DestinationPath, StringComparison.OrdinalIgnoreCase)))
                PendingActions.Add(new(outcome.PendingAssociation!, outcome.Message));
        StorePendingActions();
        StatusText = $"总数 {result.Summary.Total} · 成功 {result.Successful} · 失败 {result.Failed} · 跳过 {result.Skipped} · 等待确认 {result.WaitingForAttention}";
    }

    private async Task ExecuteSafelyAsync(Func<Task> operation)
    {
        try { await operation().ConfigureAwait(true); }
        catch (Exception ex) { _dialogs.ShowError(PrivacySafeMessage(ex)); }
    }

    private void StorePendingActions()
    {
        if (_bookingId == Guid.Empty) return;
        if (PendingActions.Count == 0) _pendingByBooking.Remove(_bookingId);
        else _pendingByBooking[_bookingId] = [.. PendingActions];
    }

    private void RefreshCommands()
    {
        (AddDocumentsCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (AddReferenceCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CopyAndAssociateCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RelocateCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RemoveAssociationCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private void Stage(IReadOnlyList<string> paths, BookingDocumentLinkMode mode)
    {
        var files = paths.Where(path => File.Exists(path)).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var path in files)
            if (!_draftOperations.Any(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase) && item.Mode == mode && item.DocumentType == SelectedDocumentType.Value))
            {
                _draftOperations.Add(new(path, SelectedDocumentType.Value, mode));
                var info = new FileInfo(path);
                var item = new BookingDocumentItemViewModel(new BookingDocumentRecord
                {
                    BookingId = _bookingId,
                    ProjectId = _projectId,
                    DocumentType = SelectedDocumentType.Value,
                    DisplayName = info.Name,
                    FilePath = path,
                    NormalizedPath = path.ToUpperInvariant(),
                    FileExtension = info.Extension,
                    FileSize = info.Exists ? info.Length : null,
                    LastKnownModifiedAtUtc = info.Exists ? info.LastWriteTimeUtc : null,
                    LinkMode = mode,
                    AddedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                }, BookingDocumentFileState.WaitingForConfirmation);
                item.SetStatusMessage("等待排期创建后处理；不会修改原文件。");
                Items.Add(item);
                SelectedItem = item;
            }
        StatusText = files.Length == 0 ? "没有可关联的文件；文件夹不会被扫描。" : $"已暂存 {files.Length} 份资料，创建排期成功后完成关联。";
        OnPropertyChanged(nameof(HasDraftOperations));
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

internal sealed record DraftDocumentOperation(string Path, BookingDocumentType DocumentType, BookingDocumentLinkMode Mode);

public sealed class BookingDocumentItemViewModel : ObservableObject
{
    private BookingDocumentRecord _document;
    private BookingDocumentFileState _state;
    private string _statusMessage = string.Empty;
    private bool _isPathExpanded;
    public BookingDocumentItemViewModel(BookingDocumentRecord document, BookingDocumentFileState initialState = BookingDocumentFileState.Normal)
    {
        _document = document;
        _state = document.IsMissing ? BookingDocumentFileState.Missing : initialState;
    }
    public Guid Id => _document.Id;
    public string DisplayName => _document.DisplayName;
    public string DocumentTypeText => DocumentTypeLabel(_document.DocumentType);
    public string ExtensionText => _document.FileExtension.TrimStart('.').ToUpperInvariant();
    public string StateText => _state switch { BookingDocumentFileState.Normal => "正常", BookingDocumentFileState.Missing => "当前不可访问", BookingDocumentFileState.Modified => "文件被修改", BookingDocumentFileState.WaitingForConfirmation => "等待确认", BookingDocumentFileState.Copying => "复制中", BookingDocumentFileState.PartiallyCompleted => "部分完成", _ => "失败" };
    public string LinkModeText => _document.LinkMode == BookingDocumentLinkMode.Reference ? "仅关联原位置" : "项目资料副本";
    public string AddedText => _document.AddedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string VerifiedText => _document.LastVerifiedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "尚未检查";
    public string FileSizeText => _document.FileSize is null ? "大小未知" : FormatBytes(_document.FileSize.Value);
    public string ModifiedText => _document.LastKnownModifiedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "修改时间未知";
    public string FileMetadataText => $"{ExtensionText} · {FileSizeText} · {ModifiedText} · {StateText}";
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool IsMissing => _state == BookingDocumentFileState.Missing;
    public bool IsModified => _state == BookingDocumentFileState.Modified;
    public bool IsPathExpanded { get => _isPathExpanded; set { if (!SetProperty(ref _isPathExpanded, value)) return; OnPropertyChanged(nameof(PathActionText)); } }
    public string PathActionText => IsPathExpanded ? "隐藏完整路径" : "显示完整路径";
    public string FullPath => _document.FilePath;

    internal void SetStatusMessage(string message) => StatusMessage = message;

    public void Apply(BookingDocumentCheckResult result)
    {
        _document = result.Document;
        _state = result.State;
        StatusMessage = result.Message;
        foreach (var name in new[] { nameof(DisplayName), nameof(DocumentTypeText), nameof(ExtensionText), nameof(StateText), nameof(LinkModeText), nameof(AddedText), nameof(VerifiedText), nameof(FileSizeText), nameof(ModifiedText), nameof(FileMetadataText), nameof(IsMissing), nameof(IsModified), nameof(FullPath) }) OnPropertyChanged(name);
    }

    private static string DocumentTypeLabel(BookingDocumentType type) => type switch
    {
        BookingDocumentType.PhotographyPlan => "摄影策划", BookingDocumentType.ShootAgreement => "拍摄协议", BookingDocumentType.ModelRelease => "模特授权书",
        BookingDocumentType.Quotation => "报价单", BookingDocumentType.VenueMaterial => "场地资料", BookingDocumentType.WardrobeReference => "服装参考",
        BookingDocumentType.LightingDiagram => "灯光图", BookingDocumentType.CameraDiagram => "机位图", BookingDocumentType.MoodBoard => "情绪板",
        BookingDocumentType.StaffFile => "工作人员资料", _ => "其他"
    };

    private static string FormatBytes(long value)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var amount = (double)Math.Max(0, value);
        var index = 0;
        while (amount >= 1024 && index < units.Length - 1) { amount /= 1024; index++; }
        return $"{amount:0.#} {units[index]}";
    }
}

public enum DocumentPreviewKind { None, Image, Pdf, Text, OfficeCard, Unsupported }

public sealed class PendingDocumentActionViewModel(PendingDocumentAssociation pending, string statusText) : ObservableObject
{
    private string _statusText = statusText;
    public PendingDocumentAssociation Pending { get; } = pending;
    public string DisplayText => "文件已复制，但关联记录未保存";
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
}
