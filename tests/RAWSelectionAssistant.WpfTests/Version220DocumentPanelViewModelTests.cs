using System.IO;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version220DocumentPanelViewModelTests
{
    [TestMethod] public void DefaultsToPhotographyPlanAndReferenceAction()
    {
        var vm = new BookingDocumentsViewModel(new StubWorkflow(), new StubDialogs());
        Assert.AreEqual(BookingDocumentType.PhotographyPlan, vm.SelectedDocumentType.Value);
        Assert.IsNotNull(vm.AddReferenceCommand);
        Assert.IsNotNull(vm.CopyAndAssociateCommand);
    }

    [TestMethod] public async Task OpeningDetailsLoadsAndVerifiesOnlyCurrentBookingDocuments()
    {
        var workflow = new StubWorkflow();
        var document = Document("策划.pdf"); workflow.Documents.Add(document);
        var vm = new BookingDocumentsViewModel(workflow, new StubDialogs());
        await vm.LoadAsync(document.BookingId, null, false);
        Assert.AreEqual(document.BookingId, workflow.LastListedBookingId);
        Assert.AreEqual(1, workflow.VerifyCalls);
        Assert.HasCount(1, vm.Items);
    }

    [TestMethod] public async Task DroppedReferenceDoesNotInvokeCopy()
    {
        var workflow = new StubWorkflow();
        var vm = new BookingDocumentsViewModel(workflow, new StubDialogs());
        var bookingId = Guid.NewGuid(); await vm.LoadAsync(bookingId, null, false);
        await vm.HandleDroppedFilesAsync(["C:\\isolated\\document.pdf"], BookingDocumentLinkMode.Reference);
        Assert.AreEqual(1, workflow.ReferenceCalls);
        Assert.AreEqual(0, workflow.CopyCalls);
    }

    [TestMethod] public async Task DroppedManagedCopyUsesSuggestedProjectDirectory()
    {
        var workflow = new StubWorkflow { SuggestedDestination = "C:\\isolated\\project\\拍摄资料\\其他" };
        var vm = new BookingDocumentsViewModel(workflow, new StubDialogs());
        await vm.LoadAsync(Guid.NewGuid(), Guid.NewGuid(), false);
        await vm.HandleDroppedFilesAsync(["C:\\isolated\\document.pdf"], BookingDocumentLinkMode.ManagedCopy);
        Assert.AreEqual(1, workflow.CopyCalls);
        Assert.AreEqual(workflow.SuggestedDestination, workflow.LastCopyRequest!.DestinationRoot);
    }

    [TestMethod] public async Task ArchivedBookingDisablesAllMutationCommands()
    {
        var vm = new BookingDocumentsViewModel(new StubWorkflow(), new StubDialogs());
        await vm.LoadAsync(Guid.NewGuid(), null, true);
        Assert.IsFalse(vm.CanModify);
        Assert.IsFalse(vm.AddReferenceCommand.CanExecute(null));
        Assert.IsFalse(vm.CopyAndAssociateCommand.CanExecute(null));
        Assert.IsFalse(vm.RemoveAssociationCommand.CanExecute(new BookingDocumentItemViewModel(Document("a.pdf"))));
    }

    [TestMethod] public async Task RemoveUsesFixedWarningAndDoesNotRequestFileDeletion()
    {
        var workflow = new StubWorkflow(); var document = Document("协议.pdf"); workflow.Documents.Add(document);
        var dialogs = new StubDialogs { ConfirmResult = true };
        var vm = new BookingDocumentsViewModel(workflow, dialogs); await vm.LoadAsync(document.BookingId, null, false);
        vm.RemoveAssociationCommand.Execute(vm.Items[0]);
        await WaitUntilAsync(() => workflow.RemoveCalls == 1);
        StringAssert.Contains(dialogs.LastConfirmMessage!, "不会删除电脑中的原文件");
    }

    [TestMethod] public void FullPathIsCollapsedByDefaultAndRequiresExplicitToggle()
    {
        var item = new BookingDocumentItemViewModel(Document("客户协议.pdf"));
        Assert.IsFalse(item.IsPathExpanded);
        Assert.AreEqual("显示完整路径", item.PathActionText);
        item.IsPathExpanded = true;
        Assert.AreEqual("隐藏完整路径", item.PathActionText);
    }

    [TestMethod] public async Task PendingDatabaseFailureShowsAllRecoveryActions()
    {
        var workflow = new StubWorkflow();
        var pending = new PendingDocumentAssociation(Guid.NewGuid(), Guid.NewGuid(), null, BookingDocumentType.Other, "C:\\isolated\\copy.pdf", new string('A', 64), 1);
        workflow.CopyResult = new(pending.TaskId, BookingDocumentBatchStatus.NeedsAttention, new(1, 1, 0, 0, 0, 1, 1, 1),
            [new("source.pdf", pending.DestinationPath, BookingDocumentFileState.PartiallyCompleted, null, pending, ErrorCodeCatalog.DatabaseUnavailable, "文件已复制，但关联记录未保存")]);
        var vm = new BookingDocumentsViewModel(workflow, new StubDialogs()); await vm.LoadAsync(pending.BookingId, null, false);
        await vm.HandleDroppedFilesAsync(["C:\\isolated\\source.pdf"], BookingDocumentLinkMode.ManagedCopy);
        Assert.HasCount(1, vm.PendingActions);
        Assert.IsNotNull(vm.RetryAssociationCommand); Assert.IsNotNull(vm.UndoCopyCommand); Assert.IsNotNull(vm.OpenOutputDirectoryCommand); Assert.IsNotNull(vm.AbandonAssociationCommand);
        StringAssert.Contains(vm.StatusText, "等待确认 1");
    }

    [TestMethod] public async Task PendingRecoveryActionsStayWithTheirOwnBookingWhenDetailsSwitch()
    {
        var firstBooking = Guid.NewGuid();
        var secondBooking = Guid.NewGuid();
        var workflow = new StubWorkflow();
        var pending = new PendingDocumentAssociation(Guid.NewGuid(), firstBooking, null, BookingDocumentType.Other, "C:\\isolated\\copy.pdf", new string('A', 64), 1);
        workflow.CopyResult = new(pending.TaskId, BookingDocumentBatchStatus.NeedsAttention, new(1, 1, 0, 0, 0, 1, 1, 1),
            [new("source.pdf", pending.DestinationPath, BookingDocumentFileState.PartiallyCompleted, null, pending, ErrorCodeCatalog.DatabaseUnavailable, "待恢复")]);
        var vm = new BookingDocumentsViewModel(workflow, new StubDialogs());
        await vm.LoadAsync(firstBooking, null, false);
        await vm.HandleDroppedFilesAsync(["C:\\isolated\\source.pdf"], BookingDocumentLinkMode.ManagedCopy);
        Assert.HasCount(1, vm.PendingActions);
        await vm.LoadAsync(secondBooking, null, false);
        Assert.IsEmpty(vm.PendingActions);
        await vm.LoadAsync(firstBooking, null, false);
        Assert.HasCount(1, vm.PendingActions);
        Assert.AreEqual(firstBooking, vm.PendingActions[0].Pending.BookingId);
    }

    [TestMethod] public void MissingModifiedAndManagedStatesHaveTextLabels()
    {
        var item = new BookingDocumentItemViewModel(Document("资料.pdf"));
        item.Apply(new(Document("资料.pdf") with { IsMissing = true }, BookingDocumentFileState.Missing, "不可访问"));
        Assert.AreEqual("当前不可访问", item.StateText);
        item.Apply(new(Document("资料.pdf"), BookingDocumentFileState.Modified, "已修改"));
        Assert.AreEqual("文件被修改", item.StateText);
    }

    private static BookingDocumentRecord Document(string name) => new()
    {
        Id = Guid.NewGuid(), BookingId = Guid.NewGuid(), DocumentType = BookingDocumentType.Other, DisplayName = name,
        FilePath = "C:\\isolated\\" + name, NormalizedPath = ("C:\\isolated\\" + name).ToUpperInvariant(), FileExtension = Path.GetExtension(name),
        FileSize = 1, LinkMode = BookingDocumentLinkMode.Reference, AddedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(20);
        Assert.IsTrue(condition());
    }

    private sealed class StubWorkflow : IBookingDocumentWorkflowService
    {
        public List<BookingDocumentRecord> Documents { get; } = [];
        public Guid LastListedBookingId { get; private set; }
        public int VerifyCalls { get; private set; }
        public int ReferenceCalls { get; private set; }
        public int CopyCalls { get; private set; }
        public int RemoveCalls { get; private set; }
        public string? SuggestedDestination { get; set; } = "C:\\isolated\\target";
        public BookingDocumentCopyRequest? LastCopyRequest { get; private set; }
        public BookingDocumentBatchResult? CopyResult { get; set; }
        public Task<IReadOnlyList<BookingDocumentRecord>> ListAsync(Guid bookingId, CancellationToken cancellationToken = default) { LastListedBookingId = bookingId; return Task.FromResult<IReadOnlyList<BookingDocumentRecord>>(Documents.Where(item => item.BookingId == bookingId).ToArray()); }
        public Task<string?> GetSuggestedDestinationAsync(Guid? projectId, BookingDocumentType documentType, CancellationToken cancellationToken = default) => Task.FromResult(SuggestedDestination);
        public Task<BookingDocumentBatchResult> AddReferencesAsync(BookingDocumentAddRequest request, CancellationToken cancellationToken = default) { ReferenceCalls++; var items = request.FilePaths.Select(path => new BookingDocumentItemOutcome(path, null, BookingDocumentFileState.Normal, Document(Path.GetFileName(path)) with { BookingId = request.BookingId }, null, null, "ok")).ToArray(); return Task.FromResult(new BookingDocumentBatchResult(null, BookingDocumentBatchStatus.Completed, new(items.Length, items.Length, 0, 0, 0, 0, 0, 0), items)); }
        public Task<BookingDocumentBatchResult> CopyAndAssociateAsync(BookingDocumentCopyRequest request, CancellationToken cancellationToken = default) { CopyCalls++; LastCopyRequest = request; return Task.FromResult(CopyResult ?? new BookingDocumentBatchResult(Guid.NewGuid(), BookingDocumentBatchStatus.Completed, new(1, 1, 0, 0, 0, 0, 1, 1), [])); }
        public Task<BookingDocumentCheckResult?> VerifyAsync(Guid documentId, CancellationToken cancellationToken = default) { VerifyCalls++; var doc = Documents.FirstOrDefault(item => item.Id == documentId); return Task.FromResult<BookingDocumentCheckResult?>(doc is null ? null : new(doc, BookingDocumentFileState.Normal, "正常")); }
        public Task<BookingDocumentRelocationResult> RelocateAsync(Guid documentId, string newFilePath, bool acceptHashMismatch = false, CancellationToken cancellationToken = default) => Task.FromResult(new BookingDocumentRelocationResult(BookingDocumentRelocationStatus.NotFound, null, false, "not found"));
        public Task<bool> RemoveAssociationAsync(Guid documentId, CancellationToken cancellationToken = default) { RemoveCalls++; Documents.RemoveAll(item => item.Id == documentId); return Task.FromResult(true); }
        public Task<BookingDocumentRetryResult> RetryAssociationAsync(PendingDocumentAssociation pending, CancellationToken cancellationToken = default) => Task.FromResult(new BookingDocumentRetryResult(false, null, pending, ErrorCodeCatalog.DatabaseUnavailable, "failed"));
        public Task<IReadOnlyList<PendingDocumentAssociation>> ListPendingAssociationsAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PendingDocumentAssociation>>([]);
        public Task<TaskResultSummary> UndoCopiedFileAsync(PendingDocumentAssociation pending, CancellationToken cancellationToken = default) => Task.FromResult(new TaskResultSummary(1, 0, 0, 0, 0, 1, 0, 0));
        public Task AbandonAssociationAsync(PendingDocumentAssociation pending, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubDialogs : IDialogService
    {
        public bool ConfirmResult { get; set; }
        public string? LastConfirmMessage { get; private set; }
        public string? ChooseFolder(string title, string? initialDirectory = null) => "C:\\isolated\\target";
        public IReadOnlyList<string> ChooseFiles(string title, string filter, bool multiselect = true) => [];
        public string? ChooseSaveFile(string title, string filter, string defaultExtension, string? suggestedFileName = null) => null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds) => currentToolIds;
        public void ShowInfo(string message) { }
        public void ShowError(string message) => Assert.Fail(message);
        public bool Confirm(string message, string title) { LastConfirmMessage = message; return ConfirmResult; }
        public HelpAction ShowHelp() => HelpAction.None;
        public void ShowFeedback() { }
        public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates) => null;
        public bool ShowMediaDetails(MediaSelectionItem item, bool showAdvancedDetails) => false;
        public void RevealFile(string path) { }
    }
}
