using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version230Rc2CalendarWorkflowStateMachineTests
{
    [TestMethod]
    [DataRow(ShootBookingStatus.Tentative, CalendarWorkflowState.Scheduled)]
    [DataRow(ShootBookingStatus.Confirmed, CalendarWorkflowState.Scheduled)]
    [DataRow(ShootBookingStatus.Preparing, CalendarWorkflowState.Scheduled)]
    [DataRow(ShootBookingStatus.Shooting, CalendarWorkflowState.PostProduction)]
    [DataRow(ShootBookingStatus.Completed, CalendarWorkflowState.PostProduction)]
    [DataRow(ShootBookingStatus.AwaitingSelection, CalendarWorkflowState.PostProduction)]
    [DataRow(ShootBookingStatus.Selected, CalendarWorkflowState.PostProduction)]
    [DataRow(ShootBookingStatus.AwaitingRetouch, CalendarWorkflowState.PostProduction)]
    [DataRow(ShootBookingStatus.AwaitingDelivery, CalendarWorkflowState.PostProduction)]
    [DataRow(ShootBookingStatus.Delivered, CalendarWorkflowState.Delivered)]
    [DataRow(ShootBookingStatus.Cancelled, CalendarWorkflowState.Free)]
    public void BookingStatusMapsToFourCalendarStates(ShootBookingStatus status, CalendarWorkflowState expected) =>
        Assert.AreEqual(expected, CalendarWorkflowStateMapper.FromBookingStatus(status));

    [TestMethod]
    public void MultiBookingAggregationUsesNextActionPriority()
    {
        Assert.AreEqual(CalendarWorkflowState.Scheduled, CalendarDayVisualStateResolver.ResolveWorkflowState([ShootBookingStatus.Completed, ShootBookingStatus.Confirmed]));
        Assert.AreEqual(CalendarWorkflowState.PostProduction, CalendarDayVisualStateResolver.ResolveWorkflowState([ShootBookingStatus.AwaitingDelivery, ShootBookingStatus.Delivered]));
        Assert.AreEqual(CalendarWorkflowState.Delivered, CalendarDayVisualStateResolver.ResolveWorkflowState([ShootBookingStatus.Delivered]));
        Assert.AreEqual(CalendarWorkflowState.Free, CalendarDayVisualStateResolver.ResolveWorkflowState([ShootBookingStatus.Cancelled, ShootBookingStatus.Draft]));
    }

    [TestMethod]
    public void VisualStateKeepsClosedTodayAndSelectionIndependent()
    {
        var date = new DateTime(2026, 8, 12);
        var state = CalendarDayVisualStateResolver.Resolve(date, [], isClosed: true, isToday: true, isSelected: true);

        Assert.AreEqual(CalendarWorkflowState.Free, state.WorkflowState);
        Assert.IsTrue(state.LockVisible);
        Assert.IsTrue(state.TodayRingVisible);
        Assert.IsTrue(state.SelectedBorderVisible);
        Assert.AreEqual(CalendarDayVisualStateResolver.FreeBrush, state.BadgeBrushKey);
    }

    [TestMethod]
    public void ClientSelectingRemainsPostProduction()
    {
        Assert.AreEqual(CalendarWorkflowState.PostProduction,
            CalendarWorkflowStateMapper.FromBookingStatus(CalendarPostProductionStageMapper.ToBookingStatus(CalendarPostProductionStage.ClientSelecting)));
        Assert.AreEqual(CalendarWorkflowState.PostProduction,
            CalendarWorkflowStateMapper.FromBookingStatus(ShootBookingStatus.Selected));
    }

    [TestMethod]
    public async Task MarkShootCompleted_PersistsCompletionInstantAcrossRepositoryReload()
    {
        using var setup = await WorkflowSetup.CreateAsync();
        var id = Guid.NewGuid();
        var saved = await setup.Bookings.SaveAsync(WorkflowSetup.Draft(id));
        Assert.AreEqual(BookingSaveStatus.Saved, saved.Status);

        var result = await setup.Workflow.MarkShootCompletedAsync(id);
        Assert.AreEqual(BookingWorkflowOperationStatus.Succeeded, result.Status);
        Assert.IsNotNull(result.ShotCompletedAtUtc);

        var reloaded = await new SqliteShootBookingRepository(setup.Database).GetAsync(id, includeArchived: true);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual(ShootBookingStatus.Completed, reloaded.Status);
        Assert.IsNotNull(reloaded.ShotCompletedAtUtc);
        Assert.IsTrue((result.ShotCompletedAtUtc.Value - reloaded.ShotCompletedAtUtc.Value).Duration() < TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public async Task WorkflowTransitionsAreIdempotentAndProtectDeliveredUndo()
    {
        using var setup = await WorkflowSetup.CreateAsync();
        var id = Guid.NewGuid();
        await setup.Bookings.SaveAsync(WorkflowSetup.Draft(id));

        Assert.AreEqual(BookingWorkflowOperationStatus.Succeeded, (await setup.Workflow.MarkShootCompletedAsync(id)).Status);
        Assert.AreEqual(BookingWorkflowOperationStatus.AlreadyApplied, (await setup.Workflow.MarkShootCompletedAsync(id)).Status);
        Assert.AreEqual(BookingWorkflowOperationStatus.Succeeded, (await setup.Workflow.MarkDeliveredAsync(id)).Status);
        var undo = await setup.Workflow.UndoShootCompletedAsync(id);
        Assert.AreEqual(BookingWorkflowOperationStatus.Rejected, undo.Status);
        Assert.AreEqual("DeliveredCannotUndoShoot", undo.ErrorCode);
    }

    private sealed class WorkflowSetup(TempDirectory temp, PixelTartDatabase database, ShootBookingService bookings, BookingWorkflowService workflow) : IDisposable
    {
        public TempDirectory Temp { get; } = temp;
        public PixelTartDatabase Database { get; } = database;
        public ShootBookingService Bookings { get; } = bookings;
        public BookingWorkflowService Workflow { get; } = workflow;

        public static async Task<WorkflowSetup> CreateAsync()
        {
            var temp = new TempDirectory();
            var database = new PixelTartDatabase(temp.Combine("data", "workflow.db"));
            var migration = await new DatabaseMigrator(database, new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync();
            Assert.IsTrue(migration.Success, migration.ErrorMessage);
            var repository = new SqliteShootBookingRepository(database);
            var bookings = new ShootBookingService(repository, new BookingConflictDetector(repository));
            return new(temp, database, bookings, new BookingWorkflowService(repository));
        }

        public void Dispose()
        {
            SqliteTestIsolation.ClearPool(Database);
            Temp.Dispose();
        }

        public static ShootBookingDraft Draft(Guid id) => new()
        {
            Id = id, EditorSessionId = Guid.NewGuid(), CreateIfMissing = true, ReplacePeople = true,
            Title = "workflow test", ClientDisplayName = "client",
            StartAt = new DateTimeOffset(2026, 8, 12, 9, 0, 0, TimeSpan.FromHours(8)),
            EndAt = new DateTimeOffset(2026, 8, 12, 11, 0, 0, TimeSpan.FromHours(8)),
            TimeZoneId = "China Standard Time", ShootingType = "Portrait", Status = ShootBookingStatus.Confirmed
        };
    }
}
