using System.IO;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class BookingContinuePlanningAndResponsiveToolsTests
{
    [TestMethod]
    public async Task QuickCreate_ContinuePlanningAwaitsReplacementAndKeepsOneBookingId()
    {
        using var setup = await BookingSetup.CreateAsync();
        var editor = new ShootBookingEditorViewModel(setup.Service, setup.Projects, suggestedStart: new DateTime(2026, 9, 8, 9, 0, 0));
        await editor.InitializeAsync();
        editor.Title = "继续策划测试";
        editor.ClientDisplayName = "客户代号";
        editor.Location = "影棚A";

        var originalId = editor.StableBookingId;
        var replacementInitialized = new TaskCompletionSource<ShootBookingEditorViewModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = false;
        editor.CloseRequested += (_, _) => closed = true;
        editor.ContinuePlanningRequested += async saved =>
        {
            var replacement = new ShootBookingEditorViewModel(setup.Service, setup.Projects, saved.Id);
            await replacement.InitializeAsync();
            replacementInitialized.SetResult(replacement);
        };

        await ExecuteAndWaitAsync(editor, editor.ContinuePlanningCommand);
        var fullPlanningEditor = await replacementInitialized.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(originalId, fullPlanningEditor.StableBookingId);
        Assert.IsTrue(fullPlanningEditor.IsEditMode);
        Assert.AreEqual("继续策划测试", fullPlanningEditor.Title);
        Assert.IsFalse(closed, "继续完整策划应由新编辑器原子替换当前编辑器，而不是先关闭后后台重开。");
        Assert.AreEqual(1L, await setup.CountBookingsAsync(originalId));
    }

    [TestMethod]
    public async Task ContinuePlanning_DoesNotCompleteUntilReplacementEditorIsReady()
    {
        var service = new SuccessfulBookingService();
        var editor = new ShootBookingEditorViewModel(service, new EmptyProjectRepository(), suggestedStart: new DateTime(2026, 9, 8, 9, 0, 0))
        {
            Title = "等待完整策划"
        };
        await editor.InitializeAsync();
        var allowReplacement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementReady = false;
        editor.ContinuePlanningRequested += async _ =>
        {
            await allowReplacement.Task;
            replacementReady = true;
        };

        var completion = ExecuteAndWaitAsync(editor, editor.ContinuePlanningCommand);
        await WaitUntilAsync(() => editor.IsBusy);
        Assert.IsFalse(completion.IsCompleted);
        allowReplacement.SetResult();
        await completion;

        Assert.IsTrue(replacementReady);
    }

    [TestMethod]
    public async Task WorkCalendar_QuickCreateContinuesAsFullPlanningForSameBooking()
    {
        using var setup = await BookingSetup.CreateAsync();
        using var calendar = new WorkCalendarViewModel(
            setup.Service,
            setup.Projects,
            availabilityStore: new InMemoryAvailabilityStore());
        await calendar.InitializeAsync();
        var requests = new List<BookingEditorRequestEventArgs>();
        var fullPlanningReady = new TaskCompletionSource<BookingEditorRequestEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        calendar.EditorRequested += (_, request) =>
        {
            requests.Add(request);
            if (request.Presentation == BookingEditorPresentation.FullPlanning)
                fullPlanningReady.TrySetResult(request);
        };

        calendar.NewBookingCommand.Execute(null);
        await WaitUntilAsync(() => requests.Count == 1);
        var quickCreate = requests[0];
        Assert.AreEqual(BookingEditorPresentation.QuickCreate, quickCreate.Presentation);
        quickCreate.Editor.Title = "日历继续策划";

        await ExecuteAndWaitAsync(quickCreate.Editor, quickCreate.Editor.ContinuePlanningCommand);
        var fullPlanning = await fullPlanningReady.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(BookingEditorPresentation.FullPlanning, fullPlanning.Presentation);
        Assert.AreEqual(quickCreate.Editor.StableBookingId, fullPlanning.Editor.StableBookingId);
        Assert.AreEqual("日历继续策划", fullPlanning.Editor.Title);
        Assert.AreEqual(1L, await setup.CountBookingsAsync(fullPlanning.Editor.StableBookingId));
    }

    [TestMethod]
    public void Workbench_At1280KeepsAllFourQuickToolsVisible()
    {
        var windowCode = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        var viewModelCode = Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        var markup = Read("src/RAWSelectionAssistant/MainWindow.xaml");

        Assert.IsFalse(windowCode.Contains("ActualWidth <= 1280", StringComparison.Ordinal));
        StringAssert.Contains(windowCode, "ActualWidth < 1180");
        StringAssert.Contains(markup, "MinWidth=\"1180\"");
        StringAssert.Contains(viewModelCode, "_quickToolsCompact ? PinnedToolboxItems.Take(2).ToList() : PinnedToolboxItems");
        StringAssert.Contains(markup, "Grid.ColumnSpan=\"4\" ItemsSource=\"{Binding DisplayedPinnedToolboxItems}\"");
    }

    [TestMethod]
    public void BookingContinuePlanning_HasNoAsyncVoidEventSubscription()
    {
        var editorCode = Read("src/RAWSelectionAssistant/ViewModels/BookingEditorViewModels.cs");
        var calendarCode = Read("src/RAWSelectionAssistant/ViewModels/CalendarViewModels.cs");

        StringAssert.Contains(editorCode, "event Func<ShootBooking, Task>? ContinuePlanningRequested");
        StringAssert.Contains(editorCode, "await ContinuePlanningAsync(result.Booking)");
        StringAssert.Contains(editorCode, "event Func<ShootBooking, Task>? SavedAsync");
        Assert.IsFalse(calendarCode.Contains("ContinuePlanningRequested += async", StringComparison.Ordinal));
        Assert.IsFalse(calendarCode.Contains("editor.Saved += async", StringComparison.Ordinal));
    }

    private static async Task ExecuteAndWaitAsync(ShootBookingEditorViewModel editor, System.Windows.Input.ICommand command)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sawBusy = false;
        System.ComponentModel.PropertyChangedEventHandler? handler = null;
        handler = (_, args) =>
        {
            if (args.PropertyName != nameof(ShootBookingEditorViewModel.IsBusy)) return;
            if (editor.IsBusy) sawBusy = true;
            else if (sawBusy) completion.TrySetResult();
        };
        editor.PropertyChanged += handler;
        try
        {
            command.Execute(null);
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { editor.PropertyChanged -= handler; }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeout) Assert.Fail("Timed out waiting for editor state.");
            await Task.Yield();
        }
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("RAWSelectionAssistant.sln was not found.");
    }

    private sealed class EmptyProjectRepository : IProjectRepository
    {
        public Task UpsertAsync(PhotoProjectRecord project, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PhotoProjectRecord>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PhotoProjectRecord>>([]);
    }

    private sealed class InMemoryAvailabilityStore : RAWSelectionAssistant.Services.ICalendarAvailabilityStore
    {
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public bool IsClosed(DateTime date) => false;
        public Task SetClosedAsync(DateTime date, bool isClosed, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SuccessfulBookingService : IShootBookingService
    {
        public Task<BookingSaveResult> SaveAsync(ShootBookingDraft draft, BookingConflictResolution conflictResolution = BookingConflictResolution.None, CancellationToken cancellationToken = default)
        {
            var booking = new ShootBooking
            {
                Id = draft.Id!.Value,
                Title = draft.Title,
                ClientDisplayName = draft.ClientDisplayName,
                StartAtUtc = draft.StartAt.ToUniversalTime(),
                EndAtUtc = draft.EndAt.ToUniversalTime(),
                TimeZoneId = draft.TimeZoneId,
                Status = draft.Status,
                ShootingType = draft.ShootingType
            };
            return Task.FromResult(new BookingSaveResult(BookingSaveStatus.Saved, booking, BookingMoneyCalculator.Calculate(null, null, null), [], []));
        }

        public Task<ShootBooking?> GetAsync(Guid id, bool includeArchived = false, CancellationToken cancellationToken = default) => Task.FromResult<ShootBooking?>(null);
        public Task<IReadOnlyList<ShootRequirementItem>> GetRequirementsAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShootRequirementItem>>([]);
        public Task<IReadOnlyList<ShootBookingSummary>> QueryCurrentViewAsync(ShootBookingQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShootBookingSummary>>([]);
        public Task<ShootBookingPage> SearchAllUnarchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ShootBookingPage([], null));
        public Task<ShootBookingPage> SearchArchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ShootBookingPage([], null));
        public Task<bool> CompleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> RestoreAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class BookingSetup(string directory, PixelTartDatabase database, ShootBookingService service, SqliteProjectRepository projects) : IDisposable
    {
        public ShootBookingService Service { get; } = service;
        public SqliteProjectRepository Projects { get; } = projects;

        public static async Task<BookingSetup> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), "PixelTart.ContinuePlanning", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var database = new PixelTartDatabase(Path.Combine(directory, "continue-planning.db"));
            var migration = await new DatabaseMigrator(database, new DatabaseBackupService(database, Path.Combine(directory, "backups"))).MigrateAsync();
            Assert.IsTrue(migration.Success, migration.ErrorMessage);
            var repository = new SqliteShootBookingRepository(database);
            return new(directory, database, new ShootBookingService(repository, new BookingConflictDetector(repository)), new SqliteProjectRepository(database));
        }

        public async Task<long> CountBookingsAsync(Guid id)
        {
            await using var connection = await database.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM ShootBookings WHERE Id=$id;";
            command.Parameters.AddWithValue("$id", id.ToString("D"));
            return (long)(await command.ExecuteScalarAsync())!;
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(directory, true); } catch { }
        }
    }
}
