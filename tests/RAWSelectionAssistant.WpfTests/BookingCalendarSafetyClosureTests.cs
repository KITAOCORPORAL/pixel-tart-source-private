using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class BookingCalendarSafetyClosureTests
{
    [TestMethod]
    public async Task UnsavedEditor_CancelReplacementAndWindowCloseKeepCurrentInput()
    {
        var dialogs = new DialogStub { ConfirmResult = false };
        var current = await CreateEditorAsync(dialogs);
        current.Title = "必须保留的未保存排期";
        var replacement = await CreateEditorAsync(dialogs);
        var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        var activeField = typeof(MainWindow).GetField("_activeBookingEditor", BindingFlags.Instance | BindingFlags.NonPublic)!;
        activeField.SetValue(window, current);
        var closeHandler = (EventHandler)typeof(MainWindow)
            .GetMethod("ActiveBookingEditor_CloseRequested", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(EventHandler), window);
        current.CloseRequested += closeHandler;
        try
        {
            current.CancelCommand.Execute(null);
            Assert.AreSame(current, activeField.GetValue(window));
            Assert.AreEqual("必须保留的未保存排期", current.Title);

            RequestEditor(window, replacement, BookingEditorPresentation.FullPlanning);
            Assert.AreSame(current, activeField.GetValue(window));
            Assert.AreEqual("必须保留的未保存排期", current.Title);

            var closing = new CancelEventArgs();
            Invoke(window, "Window_Closing", null, closing);
            Assert.IsTrue(closing.Cancel);
            Assert.AreSame(current, activeField.GetValue(window));
            Assert.AreEqual(3, dialogs.ConfirmCount);
        }
        finally
        {
            current.CloseRequested -= closeHandler;
        }
    }

    [TestMethod]
    public async Task DraftDocumentOnly_CancelReplacementAndWindowCloseKeepSourceFileSafe()
    {
        using var temporary = new TemporaryDirectory();
        var sourcePath = Path.Combine(temporary.Path, "shoot-plan.txt");
        await File.WriteAllTextAsync(sourcePath, "isolated booking document draft");
        var originalBytes = await File.ReadAllBytesAsync(sourcePath);
        var originalWriteTime = File.GetLastWriteTimeUtc(sourcePath);
        var dialogs = new DialogStub { ConfirmResult = false };
        var workflow = new DocumentWorkflowStub();
        var current = await CreateEditorAsync(dialogs, workflow);
        var replacement = await CreateEditorAsync(dialogs);

        Assert.IsFalse(current.HasUnsavedChanges);
        var documents = current.Documents ?? throw new AssertFailedException("Document draft view model was not created.");
        await documents.HandleDroppedFilesAsync([sourcePath], BookingDocumentLinkMode.Reference);
        Assert.IsTrue(documents.HasDraftOperations);
        Assert.IsTrue(current.HasUnsavedChanges);

        var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        var activeField = typeof(MainWindow).GetField("_activeBookingEditor", BindingFlags.Instance | BindingFlags.NonPublic)!;
        activeField.SetValue(window, current);
        var closeHandler = (EventHandler)typeof(MainWindow)
            .GetMethod("ActiveBookingEditor_CloseRequested", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(EventHandler), window);
        current.CloseRequested += closeHandler;
        try
        {
            current.CancelCommand.Execute(null);
            Assert.AreSame(current, activeField.GetValue(window));
            await AssertSourceUnchangedAsync(sourcePath, originalBytes, originalWriteTime);

            RequestEditor(window, replacement, BookingEditorPresentation.QuickEdit);
            Assert.AreSame(current, activeField.GetValue(window));
            await AssertSourceUnchangedAsync(sourcePath, originalBytes, originalWriteTime);

            var closing = new CancelEventArgs();
            Invoke(window, "Window_Closing", null, closing);
            Assert.IsTrue(closing.Cancel);
            Assert.AreSame(current, activeField.GetValue(window));
            await AssertSourceUnchangedAsync(sourcePath, originalBytes, originalWriteTime);

            Assert.AreEqual(3, dialogs.ConfirmCount);
            Assert.AreEqual(0, workflow.MutationCalls);
        }
        finally
        {
            current.CloseRequested -= closeHandler;
        }
    }

    [TestMethod]
    public async Task NewBookingCommand_RaisesForSelectedDateAndClosedDayChanges()
    {
        var closedDate = new DateTime(2028, 4, 12);
        var openDate = closedDate.AddDays(1);
        var availability = new AvailabilityStoreStub();
        await availability.SetClosedAsync(closedDate, true);
        using var calendar = new WorkCalendarViewModel(new BookingServiceStub(), new ProjectRepositoryStub(), availabilityStore: availability);
        var changes = 0;
        calendar.NewBookingCommand.CanExecuteChanged += (_, _) => changes++;

        calendar.SelectedDate = closedDate;
        Assert.AreEqual(1, changes);
        Assert.IsFalse(calendar.NewBookingCommand.CanExecute(null));

        calendar.SelectedDate = openDate;
        Assert.AreEqual(2, changes);
        Assert.IsTrue(calendar.NewBookingCommand.CanExecute(null));

        await calendar.OpenDayDetailsForDateAsync(openDate);
        var openDay = calendar.Month.Days.Single(day => day.Date == openDate);
        await ExecuteAsync(calendar.Month.CloseDayCommand, openDay);
        Assert.AreEqual(3, changes);
        Assert.IsFalse(calendar.NewBookingCommand.CanExecute(null));

        var closedDay = calendar.Month.Days.Single(day => day.Date == openDate);
        await ExecuteAsync(calendar.Month.OpenDayCommand, closedDay);
        Assert.AreEqual(4, changes);
        Assert.IsTrue(calendar.NewBookingCommand.CanExecute(null));
    }

    private static async Task<ShootBookingEditorViewModel> CreateEditorAsync(IDialogService dialogs, IBookingDocumentWorkflowService? documentWorkflow = null)
    {
        var editor = new ShootBookingEditorViewModel(new BookingServiceStub(), new ProjectRepositoryStub(), documentWorkflow: documentWorkflow, dialogs: dialogs);
        await editor.InitializeAsync();
        return editor;
    }

    private static async Task AssertSourceUnchangedAsync(string path, byte[] expectedBytes, DateTime expectedWriteTime)
    {
        Assert.IsTrue(File.Exists(path));
        CollectionAssert.AreEqual(expectedBytes, await File.ReadAllBytesAsync(path));
        Assert.AreEqual(expectedWriteTime, File.GetLastWriteTimeUtc(path));
    }

    private static void RequestEditor(MainWindow window, ShootBookingEditorViewModel editor, BookingEditorPresentation presentation) =>
        Invoke(window, "ViewModel_EditorRequested", null, new BookingEditorRequestEventArgs(editor, presentation));

    private static object? Invoke(object instance, string methodName, params object?[] arguments) =>
        instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, arguments);

    private static async Task ExecuteAsync(ICommand command, object parameter)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = false;
        void Changed(object? _, EventArgs __)
        {
            if (!started) { started = true; return; }
            completion.TrySetResult();
        }
        command.CanExecuteChanged += Changed;
        try
        {
            command.Execute(parameter);
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            command.CanExecuteChanged -= Changed;
        }
    }

    private sealed class AvailabilityStoreStub : ICalendarAvailabilityStore
    {
        private readonly HashSet<DateTime> _closed = [];
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public bool IsClosed(DateTime date) => _closed.Contains(date.Date);
        public Task SetClosedAsync(DateTime date, bool isClosed, CancellationToken cancellationToken = default)
        {
            if (isClosed) _closed.Add(date.Date);
            else _closed.Remove(date.Date);
            return Task.CompletedTask;
        }
    }

    private sealed class ProjectRepositoryStub : IProjectRepository
    {
        public Task UpsertAsync(PhotoProjectRecord project, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PhotoProjectRecord>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PhotoProjectRecord>>([]);
    }

    private sealed class BookingServiceStub : IShootBookingService
    {
        public Task<BookingSaveResult> SaveAsync(ShootBookingDraft draft, BookingConflictResolution conflictResolution = BookingConflictResolution.None, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ShootBooking?> GetAsync(Guid id, bool includeArchived = false, CancellationToken cancellationToken = default) => Task.FromResult<ShootBooking?>(null);
        public Task<IReadOnlyList<ShootRequirementItem>> GetRequirementsAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShootRequirementItem>>([]);
        public Task<IReadOnlyList<ShootBookingSummary>> QueryCurrentViewAsync(ShootBookingQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShootBookingSummary>>([]);
        public Task<ShootBookingPage> SearchAllUnarchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ShootBookingPage([], null));
        public Task<ShootBookingPage> SearchArchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ShootBookingPage([], null));
        public Task<bool> CompleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> SetStatusAsync(Guid id, ShootBookingStatus status, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> RestoreAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class DocumentWorkflowStub : IBookingDocumentWorkflowService
    {
        public int MutationCalls { get; private set; }
        public Task<IReadOnlyList<BookingDocumentRecord>> ListAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<BookingDocumentRecord>>([]);
        public Task<string?> GetSuggestedDestinationAsync(Guid? projectId, BookingDocumentType documentType, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<BookingDocumentBatchResult> AddReferencesAsync(BookingDocumentAddRequest request, CancellationToken cancellationToken = default) { MutationCalls++; throw new InvalidOperationException("Draft reference must not be committed while closing is rejected."); }
        public Task<BookingDocumentBatchResult> CopyAndAssociateAsync(BookingDocumentCopyRequest request, CancellationToken cancellationToken = default) { MutationCalls++; throw new InvalidOperationException("Draft copy must not run while closing is rejected."); }
        public Task<BookingDocumentCheckResult?> VerifyAsync(Guid documentId, CancellationToken cancellationToken = default) => Task.FromResult<BookingDocumentCheckResult?>(null);
        public Task<BookingDocumentRelocationResult> RelocateAsync(Guid documentId, string newFilePath, bool acceptHashMismatch = false, CancellationToken cancellationToken = default) { MutationCalls++; throw new InvalidOperationException("Relocation is not expected."); }
        public Task<bool> RemoveAssociationAsync(Guid documentId, CancellationToken cancellationToken = default) { MutationCalls++; throw new InvalidOperationException("Removal is not expected."); }
        public Task<BookingDocumentRetryResult> RetryAssociationAsync(PendingDocumentAssociation pending, CancellationToken cancellationToken = default) { MutationCalls++; throw new InvalidOperationException("Retry is not expected."); }
        public Task<IReadOnlyList<PendingDocumentAssociation>> ListPendingAssociationsAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PendingDocumentAssociation>>([]);
        public Task<TaskResultSummary> UndoCopiedFileAsync(PendingDocumentAssociation pending, CancellationToken cancellationToken = default) { MutationCalls++; throw new InvalidOperationException("Undo is not expected."); }
        public Task AbandonAssociationAsync(PendingDocumentAssociation pending, CancellationToken cancellationToken = default) { MutationCalls++; throw new InvalidOperationException("Abandon is not expected."); }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PixelTart.BookingDraftSafety", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }

    private sealed class DialogStub : IDialogService
    {
        public bool ConfirmResult { get; set; }
        public int ConfirmCount { get; private set; }
        public string? ChooseFolder(string title, string? initialDirectory = null) => null;
        public IReadOnlyList<string> ChooseFiles(string title, string filter, bool multiselect = true) => [];
        public string? ChooseSaveFile(string title, string filter, string defaultExtension, string? suggestedFileName = null) => null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds) => null;
        public void ShowInfo(string message) { }
        public void ShowError(string message) { }
        public bool Confirm(string message, string title) { ConfirmCount++; return ConfirmResult; }
        public HelpAction ShowHelp() => HelpAction.None;
        public void ShowFeedback() { }
        public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates) => null;
        public bool ShowMediaDetails(MediaSelectionItem item, bool showAdvancedDetails) => false;
        public void RevealFile(string path) { }
    }
}
