using System.Collections.ObjectModel;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class PlanningCenterViewModel
{
    public IEnumerable<ShotCaptureRelation> CapturedLibraryAssets => (_state?.CapturedAssets ?? []).Where(x => x.LibraryId.HasValue);
    public async Task LinkReferenceOutcomeAsync(Guid referenceId, ShotCaptureRelation capture)
    {
        if (ProjectId is not Guid project || capture.ProjectId != project || capture.LibraryId is not Guid library) return;
        await _planning.LinkReferenceOutcomeAsync(project, new(referenceId, library, capture.AssetId, DateTimeOffset.UtcNow));
        _state = await _planning.LoadAsync(project);
        SaveStatus = "已明确关联参考与成片；未修改素材归属";
    }
    public Func<Guid,Guid,Task>? RelocateAsset { get; set; }
    public PlanningRichLayout? RichLayout => PlanningImageStorage.BindDocument(_document,_planning.StorageRoot).Package?.RichLayout;
    public long EditorGeneration { get; private set; }
    public string? RichTextPackage => _document.Package?.RichTextPackageBase64;
    public void SaveRichText(string data, string plainText,PlanningRichLayout layout)
    {
        if(ProjectId is not Guid project)return;
        if(layout.Images.Any(i=>i.DocumentId!=PlanningDocumentId))throw new InvalidDataException("图片资源不属于当前策划文档。");
        if (_document.Package?.RichTextPackageBase64 == data && _document.Body == plainText
            && System.Text.Json.JsonSerializer.Serialize(_document.Package?.RichLayout) == System.Text.Json.JsonSerializer.Serialize(layout)) return;
        layout=layout with{Images=layout.Images.Select(i=>PlanningImageStorage.PrepareForSave(i,_planning.StorageRoot)).ToArray()};
        UpdateDocument(_document with { Body=plainText, Package=(_document.Package ?? new PlanningDocumentPackage{ProjectId=project}) with {RichTextPackageBase64=data,RichLayout=layout} });
    }
    public IDialogService Dialogs => _dialogs;
    public void OpenLinkedCanvas(RAWSelectionAssistant.Core.Services.FreeCanvas.CanvasDocument canvas) => CanvasRequested?.Invoke(this, canvas);
    private long _canvasCaptureGeneration;
    private CancellationTokenSource? _canvasCaptureCancellation;
    private bool _isCanvasCaptureRunning;
    public bool IsCanvasCaptureRunning{get=>_isCanvasCaptureRunning;private set=>SetProperty(ref _isCanvasCaptureRunning,value);}
    internal Func<RAWSelectionAssistant.Core.Services.FreeCanvas.CanvasDocument,CancellationToken,Task<string>> CanvasSnapshotRenderer=PlanningCanvasSnapshot.RenderAsync;
    internal Func<CancellationToken,Task>? CanvasBeforeCommit;
    public void CancelCanvasCapture(){Interlocked.Increment(ref _canvasCaptureGeneration);_canvasCaptureCancellation?.Cancel();IsCanvasCaptureRunning=false;}
    public async Task CaptureCanvasDocumentAsync(RAWSelectionAssistant.Core.Services.FreeCanvas.CanvasDocument canvas,CancellationToken token=default)
    {
        if(ProjectId is not Guid project||PlanningDocumentId is not Guid documentId||IsPlanningLoading||IsOverview)throw new InvalidOperationException("请先打开当前策划。");
        CancelCanvasCapture();var generation=Interlocked.Increment(ref _canvasCaptureGeneration);using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(token);_canvasCaptureCancellation=cancellation;cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        var store=_planning;var canvases=_canvases;var editorGeneration=EditorGeneration;IsCanvasCaptureRunning=true;SaveStatus="正在生成画布版本…";
        bool Current()=>!cancellation.IsCancellationRequested&&generation==Interlocked.Read(ref _canvasCaptureGeneration)&&ProjectId==project&&PlanningDocumentId==documentId&&EditorGeneration==editorGeneration&&ReferenceEquals(store,_planning)&&!IsOverview;
        void RequireCurrent(){cancellation.Token.ThrowIfCancellationRequested();if(!Current())throw new OperationCanceledException("策划已切换，未发布画布版本。",cancellation.Token);}
        try{
            if((await store.LoadAsync(project,cancellation.Token)).Document is null){RequireCurrent();QueueDocumentSave();}if(!await FlushAsync(cancellation.Token))throw new IOException("策划保存未完成，未插入画布版本。");RequireCurrent();
            var saved=await canvases.LoadAsync(canvas.CanvasId,cancellation.Token)??throw new IOException("画布尚未保存或已移除，旧版本保留。");RequireCurrent();
            if(saved.ProjectId!=project)throw new InvalidDataException("画布不属于当前项目。");
            var frozen=System.Text.Json.JsonSerializer.Deserialize<RAWSelectionAssistant.Core.Services.FreeCanvas.CanvasDocument>(System.Text.Json.JsonSerializer.Serialize(saved))! with{StorageRoot=saved.StorageRoot};
            foreach(var item in frozen.Objects.Where(o=>o.PlanningSource is not null)){
                var owned=item.PlanningSource!;var copy=owned.Copy;
                bool projectCopy=copy.ProjectId==project&&copy.DocumentId==documentId&&Path.GetFullPath(owned.PlanningRoot).Equals(store.StorageRoot,StringComparison.OrdinalIgnoreCase);
                bool canvasCopy=copy.ProjectId==frozen.CanvasId&&copy.DocumentId==frozen.CanvasId&&frozen.StorageRoot is{} root&&Path.GetFullPath(owned.PlanningRoot).Equals(Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase);
                if(!projectCopy&&!canvasCopy)throw new InvalidDataException("画布图片副本不属于当前项目/画布。");
            }
            frozen=await Task.Run(()=>PixelTart.Modules.AssetLibrary.FreeCanvas.CanvasPixelRenderer.FreezeSnapshotSources(frozen,cancellation.Token),cancellation.Token);RequireCurrent();
            SaveStatus="正在生成画布版本…";var preview=await CanvasSnapshotRenderer(frozen,cancellation.Token);RequireCurrent();
            double scale=Math.Min(1,1400d/Math.Max(frozen.PixelWidth,frozen.PixelHeight));
            var revision=new PlanningCanvasRevision(Guid.NewGuid(),frozen.CanvasId,DateTimeOffset.UtcNow,frozen,preview){RenderVersion=PlanningCanvasSnapshot.RenderVersion,SamplingContract=PlanningCanvasSnapshot.SamplingContract,PreviewWidth=Math.Max(1,(int)Math.Round(frozen.PixelWidth*scale)),PreviewHeight=Math.Max(1,(int)Math.Round(frozen.PixelHeight*scale))};
            revision.Validate();
            if(CanvasBeforeCommit is{} wait)await wait(cancellation.Token);RequireCurrent();
            // Capture rich edits before the transaction, serialize all pending writes, then append atomically.
            if(!await FlushAsync(cancellation.Token))throw new IOException("策划保存失败，旧画布版本保留。");RequireCurrent();
            await _saveGate.WaitAsync(cancellation.Token);try{
                IDisposable? leases=null;try{RequireCurrent();await store.AppendCanvasRevisionAsync(project,documentId,revision,cancellation.Token,()=>{RequireCurrent();leases=PixelTart.Modules.AssetLibrary.FreeCanvas.CanvasPixelRenderer.OpenVisibleSourceLeases(frozen,cancellation.Token);RequireCurrent();});}finally{leases?.Dispose();}
                // Commit is final. Only publish into this exact editor; a later switch never receives old data.
                if(!Current())return;
                var package=_document.Package!;_document=_document with{Package=package with{CanvasRevisions=package.CanvasRevisions.Append(revision).ToArray()}};
                if(_pendingDocuments.TryGetValue(project,out var pending))_pendingDocuments[project]=(pending.Document with{Package=pending.Document.Package! with{CanvasRevisions=pending.Document.Package.CanvasRevisions.Append(revision).ToArray()}},pending.Summary);
                SaveStatus="已保存画布新版本；旧版本保留";OnPropertyChanged(nameof(DocumentCanvasRevisions));PresentationChanged();
            }finally{_saveGate.Release();}
        }catch(OperationCanceledException){if(generation==Interlocked.Read(ref _canvasCaptureGeneration)&&ProjectId==project)SaveStatus="画布版本已取消；旧版本保留";throw;}
        catch(Exception error)when(IsRecoverableProjectError(error)||error is InvalidOperationException or NotSupportedException or FormatException){if(generation==Interlocked.Read(ref _canvasCaptureGeneration)&&ProjectId==project)SaveStatus="画布版本未保存；旧版本保留";throw;}
        finally{if(ReferenceEquals(_canvasCaptureCancellation,cancellation)){_canvasCaptureCancellation=null;IsCanvasCaptureRunning=false;}}
    }
    public IReadOnlyList<PlanningCanvasRevision> DocumentCanvasRevisions => _document.Package?.CanvasRevisions ?? [];
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
    public static IReadOnlyList<string> ContentPages { get; } = ["项目概览", "文字", "镜头清单", "参考图", "情绪板", "灯位图", "服化道与道具", "设备", "人员", "场地", "档期", "当天时间表", "预算", "文件", "文档内容"];
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
    public string DocumentCustomBody { get => _document.CustomBody; set => UpdateDocument(_document with { CustomBody = value }); }
    public string DocumentPeople { get => _document.People; set => UpdateDocument(_document with { People = value }); }
    public string ProposalStatus { get => _document.Status; set => UpdateDocument(_document with { Status = value }); }
    public string DocumentLocation { get => _booking?.Location ?? _document.Location; set => UpdateDocument(_document with { Location = value }); }
    public string DocumentDate => (_booking?.StartAtUtc ?? _document.ShootDate)?.ToLocalTime().ToString("yyyy年M月d日") ?? "待排期";
    public string ReferenceCategory { get => _referenceCategory; set { if (SetProperty(ref _referenceCategory, value)) PresentationChanged(); } }
    public PlanningReferenceItem? SelectedDocumentReference { get => _selectedDocumentReference; set => SetProperty(ref _selectedDocumentReference, value); }
    public IEnumerable<PlanningReferenceItem> VisibleDocumentReferences => AllProjectReferences.Where(item =>
        (ContentPage != "情绪板" || item.IsMoodboard) && (ContentPage is not ("灯位图" or "灯光图") || item.Category == "灯光")
        && (ContentPage is not ("服化道与道具" or "服化道") || item.Category == "造型") && (ReferenceCategory == "全部" || item.Category == ReferenceCategory));
    public IEnumerable<PlanningReferenceItem> HeroReferences => AllProjectReferences.Where(item => item.IsHero);
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
    public AsyncRelayCommand SelectContentCommand { get; private set; } = null!;
    public RelayCommand CancelPlanningModalCommand { get; private set; } = null!;

    private void InitializeDocument()
    {
        InitializeProjectPackages();
        FlushPlanningCommand = new AsyncRelayCommand(_ => FlushAsync());
        PreviewDocumentCommand = new AsyncRelayCommand(async _ => { if (await FlushAsync()) IsPreviewMode = !IsPreviewMode; });
        EditDocumentCommand = new RelayCommand(_ => IsDocumentEditing = !IsDocumentEditing);
        SelectContentCommand = new AsyncRelayCommand(async value => { if (value is string page) await SelectContentSafelyAsync(page); });
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
            var paths = _dialogs.ChooseFiles("添加参考图（随后选择副本或外链）", "图片|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp;*.webp", true);
            if (paths.Count == 0 || ChooseProjectCopy() is not bool owned) return;
            await AddDocumentReferencesAsync(paths.Select(path => new ProjectShotReference(Guid.NewGuid(), ShotReferenceKind.General, ExternalReference: Path.GetFullPath(path), Title: Path.GetFileNameWithoutExtension(path))), owned);
        });
        AddAttachmentCommand = new AsyncRelayCommand(async _ =>
        {
            var files = _dialogs.ChooseFiles("添加项目文件（随后选择副本或外链）", "资料|*.pdf;*.docx;*.txt;*.md;*.jpg;*.png", true);
            if (files.Count == 0 || ChooseProjectCopy() is not bool owned) return;
            await AddAttachmentsAsync(files, owned);
        });
        OpenColorSchemeCommand = new RelayCommand(_ => { if (ProjectId is Guid id) ColorSchemeRequested?.Invoke(this, id); });
        OpenCalendarCommand = new RelayCommand(_ => { if (_state?.BookingId is Guid id) CalendarRequested?.Invoke(this, id); });
        CancelPlanningModalCommand = new RelayCommand(_ => { IsCreateOpen = false; IsBookingOpen = false; });
    }

    private void UpdateDocument(PlanningDocument value)
    {
        if (_state is null) return;
        // Explicitly promoting a shot reference into a document section carries its
        // verified receipt with it; never leave two competing owners for the same ID.
        value=value with{ShotExternalLinks=value.ShotExternalLinks.Where(link=>!value.References.Any(r=>r.Source.ReferenceId==link.ReferenceId)).ToArray()};
        var package = value.Package ?? _document.Package ?? value.EnsureIdentity(_state.ProjectId).Package!;
        _document = value with { Package = package with { Body = value.Body,
            Assets = value.References.Where(r => r.Source.LibraryId.HasValue && r.Source.AssetId.HasValue)
                .Select(r => new PlanningAssetReference(r.Source.LibraryId!.Value,r.Source.AssetId!.Value)).Distinct().ToArray(),
            ShotIds = Shots.Where(shot => !shot.IsArchived).Select(shot => shot.ShotId).ToArray() } };
        OnPropertyChanged(nameof(DocumentTitle)); OnPropertyChanged(nameof(ProposalStatus));
        QueueDocumentSave();
    }
    private void QueueDocumentSave()
    {
        if (_state is null) return;
        _document = _document.EnsureIdentity(_state.ProjectId);
        _document = _document with { Package = _document.Package! with {
            ShotIds = _document.Package!.ShotIds.Concat(Shots.Select(s => s.ShotId))
                .Concat(_pendingShots.Values.Where(s => s.ProjectId == _state.ProjectId).Select(s => s.ShotId)).Distinct().ToArray() } };
        _pendingDocuments[_state.ProjectId] = (_document, _state.Summary ?? new());
        ScheduleSave();
    }
    private void CaptureShotSave()
    {
        if (SelectedShot is not { } shot) return;
        _pendingShots[shot.ShotId] = _pendingShots.GetValueOrDefault(shot.ShotId, shot) with { Name = _shotTitle, Scene = _shotScene, Notes = _shotNotes, Action = _shotNotes != shot.Notes ? _shotNotes : _pendingShots.GetValueOrDefault(shot.ShotId, shot).Action, PlanningFieldsVersion = 1, EstimatedMinutes = _shotEstimatedMinutes, UpdatedAt = DateTimeOffset.UtcNow };
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
    public Action? CapturePendingEditorDraft { get; set; }
    public async Task<bool> FlushAsync(CancellationToken token = default)
    {
        // Shell navigation also calls this gate, not necessarily the view's Save handler.
        try { CapturePendingEditorDraft?.Invoke(); }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        { SaveStatus = "保存失败，请完成当前单元格编辑；草稿已保留"; return false; }
        _saveDelay?.Cancel();
        try { await _saveGate.WaitAsync(token); }
        catch (OperationCanceledException) { SaveStatus = "保存已取消；草稿保留"; return false; }
        try
        {
            while (_pendingDocuments.Count > 0 || _pendingShots.Count > 0)
            {
                var documents = _pendingDocuments.ToArray();
                var shots = _pendingShots.ToArray();
                // Recovery journal is durable BEFORE validating user drafts or replacing either file.
                // Keep both pending maps until both writes succeed. Replaying the same IDs is idempotent.
                foreach (var pair in documents)
                    await _planning.WriteDraftAsync(pair.Key, pair.Value.Document, pair.Value.Summary, token,
                        shots.Where(s => s.Value.ProjectId == pair.Key).Select(s => s.Value).ToArray());
                foreach (var pair in shots) pair.Value.Normalize();
                foreach (var pair in documents) pair.Value.Document.EnsureIdentity(pair.Key);
                foreach (var group in shots.GroupBy(p => p.Value.ProjectId))
                    await _shots.SaveBatchAsync(group.Key, group.Select(p => p.Value).ToArray(), token);
                foreach (var pair in documents)
                {
                    var saved=(await _planning.LoadAsync(pair.Key,token)).Document;
                    var reconciled=pair.Value.Document with{ShotExternalLinks=saved?.ShotExternalLinks??pair.Value.Document.ShotExternalLinks};
                    await _planning.UpdateDocumentAsync(pair.Key,reconciled,pair.Value.Summary,token);
                    if(ProjectId==pair.Key)_document=_document with{ShotExternalLinks=reconciled.ShotExternalLinks};
                }
                foreach (var pair in shots)
                {
                    if (!_pendingShots.TryGetValue(pair.Key, out var latest) || latest != pair.Value) continue;
                    _pendingShots.Remove(pair.Key);
                    var index = Shots.ToList().FindIndex(s => s.ShotId == pair.Key);
                    if (pair.Value.IsArchived) { if (index >= 0) Shots.RemoveAt(index); }
                    else if (index >= 0) Shots[index] = pair.Value;
                    else if (ProjectId == pair.Value.ProjectId) Shots.Add(pair.Value);
                    if (_selectedShot?.ShotId == pair.Key) _selectedShot = pair.Value.IsArchived ? null : pair.Value;
                }
                foreach (var pair in documents)
                {
                    if (_pendingDocuments.TryGetValue(pair.Key, out var latest) && latest == pair.Value) _pendingDocuments.Remove(pair.Key);
                    if (!_pendingDocuments.ContainsKey(pair.Key) && !_pendingShots.Values.Any(s => s.ProjectId == pair.Key)) _planning.ClearDraft(pair.Key);
                }
            }
            SaveStatus = $"自动保存于 {DateTime.Now:HH:mm}";
            return true;
        }
        catch (OperationCanceledException) { SaveStatus = "保存已取消；未保存内容和恢复草稿已保留"; return false; }
        catch (Exception error) when (IsRecoverableProjectError(error))
        { new FileLogService().Error("策划保存失败，保留待保存内容与恢复草稿。", error); SaveStatus = PlanningImageStorage.IsImageIntegrityError(error) ? "保存失败："+error.Message+" 未保存内容已保留。" : "保存失败，请重试；未保存内容已保留"; return false; }
        finally { _saveGate.Release(); }
    }
    private async Task LoadDocumentAsync()
    {
        SelectedDocumentReference = null;
        _document = _state?.Document ?? new PlanningDocument { Title = ProjectName };
        if (ProjectId is Guid packageProject) _document = _document.EnsureIdentity(packageProject);
        if (ProjectId is Guid id && await _planning.LoadDraftAsync(id) is { } draft)
        {
            _document = draft.Document.EnsureIdentity(id); _state = _state! with { Summary = draft.Summary };
            _pendingDocuments[id] = (_document, draft.Summary);
            foreach (var shot in draft.Shots ?? [])
            {
                _pendingShots[shot.ShotId] = shot;
                var index = Shots.ToList().FindIndex(existing => existing.ShotId == shot.ShotId);
                if (shot.IsArchived) { if (index >= 0) Shots.RemoveAt(index); }
                else if (index >= 0) Shots[index] = shot; else Shots.Add(shot);
            }
            if (SelectedShot is { } selected && _pendingShots.TryGetValue(selected.ShotId, out var recovered)) SelectedShot = recovered;
            SaveStatus = "已恢复未完成保存的草稿，请按 Ctrl+S 保存";
        }
        var orderedShots = Shots.OrderBy(s => s.Order).ToArray();
        Shots.Clear(); foreach (var shot in orderedShots) Shots.Add(shot);
        RefreshAttachments();
        await RefreshDocumentReferencesAsync();
        OnPropertyChanged(string.Empty); PresentationChanged();
    }
    private void RefreshAttachments() { Attachments.Clear(); foreach (var item in _document.Attachments) Attachments.Add(item); }
    public async Task AddAttachmentsAsync(IEnumerable<string> paths, bool importProjectCopy = false)
    {
        if (importProjectCopy) { await ImportProjectCopiesAsync([], paths.Select(path => new PlanningAttachment(Guid.NewGuid(), Path.GetFullPath(path), Path.GetFileName(path))).ToArray()); return; }
        UpdateDocument(_document with { Attachments = _document.Attachments.Concat(paths.Select(path => new PlanningAttachment(Guid.NewGuid(), Path.GetFullPath(path), Path.GetFileName(path)))).DistinctBy(item => item.Path, StringComparer.OrdinalIgnoreCase).ToArray() });
        await FlushAsync(); RefreshAttachments();
    }
    public void OpenAttachment(PlanningAttachment item)
    {
        var resolved = ResolveAttachmentPath(item);
        if (resolved is null) { _dialogs.ShowInfo("文件暂不可用，未回退外链。"); return; }
        // Only explicitly requested document/image types are opened; other attachments are revealed.
        if (new[] { ".pdf", ".docx", ".txt", ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(resolved), StringComparer.OrdinalIgnoreCase))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(resolved) { UseShellExecute = true });
        else _dialogs.RevealFile(resolved);
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
