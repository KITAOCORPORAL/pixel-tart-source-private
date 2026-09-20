using System.Collections.ObjectModel;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class PlanningCenterViewModel
{
    public IDialogService Dialogs => _dialogs;
    public void OpenLinkedCanvas(RAWSelectionAssistant.Core.Services.FreeCanvas.CanvasDocument canvas) => CanvasRequested?.Invoke(this, canvas);
    private PlanningDocument _document = new();
    private string _contentPage = "文字";
    private bool _isDocumentEditing, _isPreviewMode, _isShotDrawerOpen, _isCreateOpen, _isBookingOpen;
    private CancellationTokenSource? _saveDelay;
    private long _editRevision;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly Dictionary<Guid, (PlanningDocument Document, PlanningSummary Summary)> _pendingDocuments = [];
    private readonly Dictionary<Guid, ProjectShot> _pendingShots = [];
    private string _referenceCategory = "全部";
    private PlanningReferenceItem? _selectedDocumentReference;
    public static IReadOnlyList<string> ContentPages { get; } = ["文字", "参考图", "情绪板", "镜头清单", "灯光图", "服化道", "文件"];
    public IReadOnlyList<string> ReferenceCategories { get; } = ["全部", "主视觉", "场景", "姿势", "造型", "灯光", "色彩", "其他"];
    public IReadOnlyList<string> ProposalStatuses { get; } = ["进行中", "草稿", "已完成"];
    public IReadOnlyList<string> MoodGroups { get; } = ["整体氛围", "材质", "身体姿势", "灯光", "色彩"];
    public IReadOnlyList<string> StylingGroups { get; } = ["服装", "妆发", "道具"];
    public ObservableCollection<PlanningReferenceItem> AllProjectReferences { get; } = [];
    public ObservableCollection<PlanningAttachment> Attachments { get; } = [];
    public ObservableCollection<ShootBookingSummary> AvailableBookings { get; } = [];
    public ObservableCollection<PhotoProjectRecord> AvailableProjects { get; } = [];
    public ShootBookingSummary? SelectedBooking { get; set; }
    public PhotoProjectRecord? NewLinkedProject { get; set; }
    public DateTime? NewShootDate { get; set; }
    public string NewLocation { get; set; } = "";
    public event EventHandler? DocumentPresentationChanged;
    public event EventHandler<Guid>? ColorSchemeRequested;
    public event EventHandler<(Guid ProjectId, Guid LookId)>? ColorReferenceRequested;
    public event EventHandler<Guid>? CalendarRequested;
    public event EventHandler<RAWSelectionAssistant.Core.Services.FreeCanvas.CanvasDocument>? CanvasRequested;
    public string ContentPage { get => _contentPage; set { if (SetProperty(ref _contentPage, value)) { SelectedDocumentReference = null; IsDocumentEditing = false; IsShotDrawerOpen = false; ReferenceCategory = "全部"; PresentationChanged(); } } }
    public bool IsDocumentEditing { get => _isDocumentEditing; set { if (SetProperty(ref _isDocumentEditing, value)) PresentationChanged(); } }
    public bool IsPreviewMode { get => _isPreviewMode; set { if (SetProperty(ref _isPreviewMode, value)) { IsDocumentEditing = false; IsShotDrawerOpen = false; PresentationChanged(); } } }
    public bool IsShotDrawerOpen { get => _isShotDrawerOpen; set => SetProperty(ref _isShotDrawerOpen, value); }
    public bool IsCreateOpen { get => _isCreateOpen; set => SetProperty(ref _isCreateOpen, value); }
    public bool IsBookingOpen { get => _isBookingOpen; set => SetProperty(ref _isBookingOpen, value); }
    public string DocumentTitle { get => string.IsNullOrWhiteSpace(_document.Title) ? ProjectName : _document.Title; set => UpdateDocument(_document with { Title = value }); }
    public string DocumentSubtitle { get => _document.Subtitle; set => UpdateDocument(_document with { Subtitle = value }); }
    public string DocumentBody { get => _document.Body; set => UpdateDocument(_document with { Body = value }); }
    public string DocumentPeople { get => _document.People; set => UpdateDocument(_document with { People = value }); }
    public string ProposalStatus { get => _document.Status; set => UpdateDocument(_document with { Status = value }); }
    public string DocumentLocation { get => _booking?.Location ?? _document.Location; set => UpdateDocument(_document with { Location = value }); }
    public string DocumentDate => (_booking?.StartAtUtc ?? _document.ShootDate)?.ToLocalTime().ToString("yyyy年M月d日") ?? "待排期";
    public string ReferenceCategory { get => _referenceCategory; set { if (SetProperty(ref _referenceCategory, value)) PresentationChanged(); } }
    public PlanningReferenceItem? SelectedDocumentReference { get => _selectedDocumentReference; set => SetProperty(ref _selectedDocumentReference, value); }
    public IEnumerable<PlanningReferenceItem> VisibleDocumentReferences => AllProjectReferences.Where(item =>
        (ContentPage != "情绪板" || item.IsMoodboard) && (ContentPage != "灯光图" || item.Category == "灯光")
        && (ContentPage != "服化道" || item.Category == "造型") && (ReferenceCategory == "全部" || item.Category == ReferenceCategory));
    public IEnumerable<PlanningReferenceItem> HeroReferences => AllProjectReferences.Where(item => item.IsHero).Take(3);
    public AsyncRelayCommand FlushPlanningCommand { get; private set; } = null!;
    public AsyncRelayCommand PreviewDocumentCommand { get; private set; } = null!;
    public RelayCommand EditDocumentCommand { get; private set; } = null!;
    public AsyncRelayCommand ShowCreatePlanningCommand { get; private set; } = null!;
    public AsyncRelayCommand ShowBookingCommand { get; private set; } = null!;
    public AsyncRelayCommand LinkBookingCommand { get; private set; } = null!;
    public AsyncRelayCommand AddDocumentImagesCommand { get; private set; } = null!;
    public AsyncRelayCommand AddAttachmentCommand { get; private set; } = null!;
    public RelayCommand OpenColorSchemeCommand { get; private set; } = null!;
    public RelayCommand OpenCalendarCommand { get; private set; } = null!;
    public RelayCommand SelectContentCommand { get; private set; } = null!;
    public RelayCommand CancelPlanningModalCommand { get; private set; } = null!;

    private void InitializeDocument()
    {
        FlushPlanningCommand = new AsyncRelayCommand(_ => FlushAsync());
        PreviewDocumentCommand = new AsyncRelayCommand(async _ => { if (await FlushAsync()) IsPreviewMode = !IsPreviewMode; });
        EditDocumentCommand = new RelayCommand(_ => IsDocumentEditing = !IsDocumentEditing);
        SelectContentCommand = new RelayCommand(value => { if (value is string page && ContentPages.Contains(page)) ContentPage = page; });
        ShowCreatePlanningCommand = new AsyncRelayCommand(async _ => { AvailableProjects.Clear(); foreach (var p in await _projects.ListAsync()) AvailableProjects.Add(p); IsCreateOpen = true; });
        ShowBookingCommand = new AsyncRelayCommand(async _ =>
        {
            if (!HasProject) return;
            AvailableBookings.Clear();
            ShootBookingPageCursor? cursor = null;
            do { var page = await _bookings.SearchAllUnarchivedAsync(new(Cursor: cursor, PageSize: 100)); foreach (var booking in page.Items.Where(b => b.ProjectId is null || b.ProjectId == ProjectId)) AvailableBookings.Add(booking); cursor = page.NextCursor; } while (cursor is not null);
            IsBookingOpen = true;
        });
        LinkBookingCommand = new AsyncRelayCommand(_ => LinkSelectedBookingAsync());
        AddDocumentImagesCommand = new AsyncRelayCommand(async _ =>
        {
            var paths = _dialogs.ChooseFiles("关联参考图（不复制原图）", "图片|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp;*.webp", true);
            await AddDocumentReferencesAsync(paths.Select(path => new ProjectShotReference(Guid.NewGuid(), ShotReferenceKind.General, ExternalReference: Path.GetFullPath(path), Title: Path.GetFileNameWithoutExtension(path))));
        });
        AddAttachmentCommand = new AsyncRelayCommand(async _ =>
        {
            var files = _dialogs.ChooseFiles("关联项目文件", "资料|*.pdf;*.docx;*.txt;*.md;*.jpg;*.png", true);
            await AddAttachmentsAsync(files);
        });
        OpenColorSchemeCommand = new RelayCommand(_ => { if (ProjectId is Guid id) ColorSchemeRequested?.Invoke(this, id); });
        OpenCalendarCommand = new RelayCommand(_ => { if (_state?.BookingId is Guid id) CalendarRequested?.Invoke(this, id); });
        CancelPlanningModalCommand = new RelayCommand(_ => { IsCreateOpen = false; IsBookingOpen = false; });
    }

    private void UpdateDocument(PlanningDocument value)
    {
        if (_state is null) return;
        _document = value;
        OnPropertyChanged(nameof(DocumentTitle)); OnPropertyChanged(nameof(ProposalStatus));
        QueueDocumentSave();
    }
    private void QueueDocumentSave()
    {
        if (_state is null) return;
        _pendingDocuments[_state.ProjectId] = (_document, _state.Summary ?? new());
        ScheduleSave();
    }
    private void CaptureShotSave()
    {
        if (SelectedShot is not { } shot) return;
        _pendingShots[shot.ShotId] = _pendingShots.GetValueOrDefault(shot.ShotId, shot) with { Name = _shotTitle, Scene = _shotScene, Notes = _shotNotes, EstimatedMinutes = _shotEstimatedMinutes, UpdatedAt = DateTimeOffset.UtcNow };
        if (_state is not null) _pendingDocuments[_state.ProjectId] = (_document, _state.Summary ?? new());
        ScheduleSave();
    }
    private async void ScheduleSave()
    {
        _editRevision++;
        _saveDelay?.Cancel(); _saveDelay?.Dispose(); _saveDelay = new();
        var token = _saveDelay.Token;
        SaveStatus = "正在保存…";
        try { await Task.Delay(450, token); await FlushAsync(); }
        catch (OperationCanceledException) { }
    }
    public async Task<bool> FlushAsync()
    {
        _saveDelay?.Cancel();
        await _saveGate.WaitAsync();
        try
        {
            while (_pendingDocuments.Count > 0 || _pendingShots.Count > 0)
            {
            var revision = _editRevision;
            var drafts = _pendingDocuments.Keys.Concat(_pendingShots.Values.Select(shot => shot.ProjectId)).Distinct().ToArray();
            foreach (var pair in _pendingDocuments.ToArray())
            {
                await _planning.WriteDraftAsync(pair.Key, pair.Value.Document, pair.Value.Summary, shots: _pendingShots.Values.Where(shot => shot.ProjectId == pair.Key).ToArray());
                await _planning.UpdateDocumentAsync(pair.Key, pair.Value.Document, pair.Value.Summary);
                if (_pendingDocuments.TryGetValue(pair.Key, out var latest) && latest == pair.Value) _pendingDocuments.Remove(pair.Key);
            }
            foreach (var pair in _pendingShots.ToArray())
            {
                if (string.IsNullOrWhiteSpace(pair.Value.Name)) throw new InvalidDataException("镜头名称不能为空。");
                await _shots.SaveAsync(pair.Value);
                var index = Shots.ToList().FindIndex(shot => shot.ShotId == pair.Key);
                if (index >= 0) Shots[index] = pair.Value;
                if (_selectedShot?.ShotId == pair.Key) { _selectedShot = pair.Value; OnPropertyChanged(nameof(SelectedShot)); }
                if (_pendingShots.TryGetValue(pair.Key, out var latest) && latest == pair.Value) _pendingShots.Remove(pair.Key);
            }
            foreach (var id in drafts) if (!_pendingDocuments.ContainsKey(id) && !_pendingShots.Values.Any(shot => shot.ProjectId == id)) _planning.ClearDraft(id);
            if (_editRevision == revision) break;
            }
            SaveStatus = $"自动保存于 {DateTime.Now:HH:mm}";
            return true;
        }
        catch (Exception error) when (IsRecoverableProjectError(error))
        { new FileLogService().Error("策划保存失败，保留待保存内容与恢复草稿。", error); SaveStatus = "保存失败，请重试；未保存内容已保留"; return false; }
        finally { _saveGate.Release(); }
    }
    private async Task LoadDocumentAsync()
    {
        SelectedDocumentReference = null;
        _document = _state?.Document ?? new PlanningDocument { Title = ProjectName };
        if (ProjectId is Guid id && await _planning.LoadDraftAsync(id) is { } draft)
        {
            _document = draft.Document; _state = _state! with { Summary = draft.Summary };
            _pendingDocuments[id] = (draft.Document, draft.Summary);
            foreach (var shot in draft.Shots ?? [])
            {
                _pendingShots[shot.ShotId] = shot;
                var index = Shots.ToList().FindIndex(existing => existing.ShotId == shot.ShotId);
                if (index >= 0) Shots[index] = shot;
            }
            if (SelectedShot is { } selected && _pendingShots.TryGetValue(selected.ShotId, out var recovered)) SelectedShot = recovered;
            SaveStatus = "已恢复未完成保存的草稿，请按 Ctrl+S 保存";
        }
        RefreshAttachments();
        await RefreshDocumentReferencesAsync();
        OnPropertyChanged(string.Empty); PresentationChanged();
    }
    private void RefreshAttachments() { Attachments.Clear(); foreach (var item in _document.Attachments) Attachments.Add(item); }
    public async Task AddAttachmentsAsync(IEnumerable<string> paths)
    {
        UpdateDocument(_document with { Attachments = _document.Attachments.Concat(paths.Select(path => new PlanningAttachment(Guid.NewGuid(), Path.GetFullPath(path), Path.GetFileName(path)))).DistinctBy(item => item.Path, StringComparer.OrdinalIgnoreCase).ToArray() });
        await FlushAsync(); RefreshAttachments();
    }
    public void OpenAttachment(PlanningAttachment item)
    {
        if (!File.Exists(item.Path)) { _dialogs.ShowInfo("源文件暂不可用。"); return; }
        // Only explicitly requested document/image types are opened; other attachments are revealed.
        if (new[] { ".pdf", ".docx", ".txt", ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(item.Path), StringComparer.OrdinalIgnoreCase))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.Path) { UseShellExecute = true });
        else _dialogs.RevealFile(item.Path);
    }
    public async Task RemoveAttachmentAsync(PlanningAttachment item)
    { UpdateDocument(_document with { Attachments = _document.Attachments.Where(file => file.Id != item.Id).ToArray() }); await FlushAsync(); RefreshAttachments(); }
    private void PresentationChanged() { OnPropertyChanged(nameof(VisibleDocumentReferences)); DocumentPresentationChanged?.Invoke(this, EventArgs.Empty); }
    private async Task LinkSelectedBookingAsync()
    {
        if (ProjectId is not Guid projectId || SelectedBooking is null || !await FlushAsync()) return;
        var booking = await _bookings.GetAsync(SelectedBooking.Id);
        if (booking is null || booking.ProjectId is not null && booking.ProjectId != projectId) { SaveStatus = "该档期已关联其他项目。"; return; }
        var result = await _bookings.SaveAsync(new ShootBookingDraft
        {
            Id=booking.Id, ProjectId=projectId, Title=booking.Title, ClientDisplayName=booking.ClientDisplayName, StartAt=booking.StartAtUtc, EndAt=booking.EndAtUtc,
            TimeZoneId=booking.TimeZoneId, IsAllDay=booking.IsAllDay, Status=booking.Status, Location=booking.Location, ShootingType=booking.ShootingType,
            ShootingRequirements=booking.ShootingRequirements, PreparationNotes=booking.PreparationNotes, TotalAmountMinor=booking.TotalAmountMinor,
            DepositAmountMinor=booking.DepositAmountMinor, PaidAmountMinor=booking.PaidAmountMinor, CurrencyCode=booking.CurrencyCode, CurrencyScale=booking.CurrencyScale,
            ContactName=booking.ContactName, ContactPhone=booking.ContactPhone, AllowOverlap=booking.AllowOverlap, Notes=booking.Notes, Requirements=await _bookings.GetRequirementsAsync(booking.Id)
        });
        if (result.Booking is null) { SaveStatus = "档期关联未完成，请在工作日历检查冲突。"; return; }
        await _planning.LinkBookingAsync(projectId, booking.Id);
        _booking = result.Booking; _state = await _planning.LoadAsync(projectId); IsBookingOpen = false;
        OnPropertyChanged(string.Empty); PresentationChanged(); await RefreshPlanningListAsync();
    }
}
