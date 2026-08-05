using System.IO.Compression;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FileOperations;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version220DocumentWorkflowTests
{
    [TestMethod]
    public async Task Reference_DefaultModeStoresMetadataWithoutCopying()
    {
        using var setup = await SetupAsync();
        var path = setup.Temp.CreateFile("资料/摄影策划.pdf", [1, 2, 3]);
        var result = await setup.Workflow.AddReferencesAsync(new(setup.BookingId, null, BookingDocumentType.PhotographyPlan, [path]));
        var document = result.Items.Single().Document!;
        Assert.AreEqual(BookingDocumentLinkMode.Reference, document.LinkMode);
        Assert.AreEqual(Path.GetFullPath(path), document.FilePath);
        Assert.AreEqual(3L, document.FileSize);
        Assert.IsNull(document.OptionalHash);
        Assert.IsTrue(File.Exists(path));
        Assert.AreEqual(BookingDocumentBatchStatus.Completed, result.Status);
    }

    [TestMethod]
    public async Task Reference_MultipleFilesProducesAggregateResult()
    {
        using var setup = await SetupAsync();
        var paths = new[] { setup.Temp.CreateFile("资料/a.pdf", [1]), setup.Temp.CreateFile("资料/b.docx", [2]), setup.Temp.CreateFile("资料/c.png", [3]) };
        var result = await setup.Workflow.AddReferencesAsync(new(setup.BookingId, null, BookingDocumentType.Other, paths));
        Assert.AreEqual(3, result.Summary.Total);
        Assert.AreEqual(3, result.Successful);
        Assert.HasCount(3, await setup.Documents.ListByBookingAsync(setup.BookingId));
    }

    [TestMethod]
    public async Task Reference_DuplicatePathIsSkippedWithoutSecondRow()
    {
        using var setup = await SetupAsync();
        var path = setup.Temp.CreateFile("资料/重复.pdf", [1]);
        await setup.Workflow.AddReferencesAsync(new(setup.BookingId, null, BookingDocumentType.Other, [path]));
        var second = await setup.Workflow.AddReferencesAsync(new(setup.BookingId, null, BookingDocumentType.Other, [path]));
        Assert.AreEqual(1, second.Skipped);
        Assert.AreEqual(ErrorCodeCatalog.DuplicateConflict, second.Items.Single().ErrorCode);
        Assert.HasCount(1, await setup.Documents.ListByBookingAsync(setup.BookingId));
    }

    [TestMethod]
    public async Task Reference_AllSupportedExtensionsAreAcceptedWithoutReadingBody()
    {
        using var setup = await SetupAsync();
        var extensions = new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".txt", ".jpg", ".jpeg", ".png" };
        var paths = extensions.Select((extension, index) => setup.Temp.CreateFile($"支持/文件 {index}{extension}", [(byte)index])).ToArray();
        var result = await setup.Workflow.AddReferencesAsync(new(setup.BookingId, null, BookingDocumentType.Other, paths));
        Assert.AreEqual(extensions.Length, result.Successful);
        CollectionAssert.AreEquivalent(extensions, result.Items.Select(item => item.Document!.FileExtension).ToArray());
    }

    [TestMethod]
    public async Task Reference_FolderIsRejectedWithoutRecursiveScan()
    {
        using var setup = await SetupAsync();
        var folder = setup.Temp.Combine("资料文件夹"); Directory.CreateDirectory(folder);
        var result = await setup.Workflow.AddReferencesAsync(new(setup.BookingId, null, BookingDocumentType.Other, [folder]));
        Assert.AreEqual(1, result.Failed);
        Assert.IsEmpty(await setup.Documents.ListByBookingAsync(setup.BookingId));
    }

    [TestMethod]
    public async Task ProjectSuggestionUsesPhotographyMaterialCategoryFolder()
    {
        using var setup = await SetupAsync();
        var project = new PhotoProjectRecord { Name = "项目", OutputDirectory = setup.Temp.Combine("项目根目录") };
        await setup.Projects.UpsertAsync(project);
        var destination = await setup.Workflow.GetSuggestedDestinationAsync(project.Id, BookingDocumentType.ShootAgreement);
        Assert.AreEqual(Path.Combine(project.OutputDirectory, "拍摄资料", "拍摄协议"), destination);
        Assert.AreEqual(project.OutputDirectory, (await setup.Projects.ListAsync()).Single(item => item.Id == project.Id).OutputDirectory);
    }

    [TestMethod]
    public async Task Copy_UsesTaskEngineAndCreatesManagedAssociationAfterOutput()
    {
        using var setup = await SetupAsync();
        var source = setup.Temp.CreateFile("来源/报价单.xlsx", [1, 2, 3, 4]);
        var destination = setup.Temp.Combine("项目", "拍摄资料", "报价单");
        var result = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Quotation, [source], destination));
        var document = result.Items.Single(item => item.Document is not null).Document!;
        Assert.IsNotNull(result.TaskId);
        Assert.AreEqual(BookingDocumentLinkMode.ManagedCopy, document.LinkMode);
        Assert.AreEqual(result.TaskId, document.ImportTaskId);
        Assert.IsTrue(File.Exists(document.FilePath));
        Assert.IsTrue(File.Exists(source));
        CollectionAssert.AreEqual(File.ReadAllBytes(source), File.ReadAllBytes(document.FilePath));
        var task = await WaitForTerminalAsync(setup.Tasks, result.TaskId!.Value);
        Assert.AreEqual(TaskLifecycleState.Completed, task.State);
    }

    [TestMethod]
    public async Task Copy_MixedValidAndInvalidFilesCompletesValidItemsAndReportsPartialResult()
    {
        using var setup = await SetupAsync();
        var valid = setup.Temp.CreateFile("来源/有效报价.pdf", [1, 2, 3]);
        var missing = setup.Temp.Combine("来源", "已断开资料.pdf");
        var result = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Quotation, [valid, missing], setup.Temp.Combine("目标")));
        Assert.AreEqual(BookingDocumentBatchStatus.PartiallyCompleted, result.Status);
        Assert.AreEqual(1, result.Successful);
        Assert.AreEqual(1, result.Failed);
        Assert.AreEqual(2, result.Summary.Total);
        Assert.IsTrue(File.Exists(result.Items.Single(item => item.Document is not null).Document!.FilePath));
        Assert.IsTrue(File.Exists(valid));
        Assert.AreEqual(TaskLifecycleState.PartiallyCompleted, (await WaitForTerminalAsync(setup.Tasks, result.TaskId!.Value)).State);
    }

    [TestMethod]
    public async Task Copy_NormalizedDuplicateInputIsCopiedOnlyOnce()
    {
        using var setup = await SetupAsync();
        var source = setup.Temp.CreateFile("来源/同一资料.pdf", [1, 2]);
        var alternateSpelling = Path.Combine(Path.GetDirectoryName(source)!, ".", Path.GetFileName(source));
        var result = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Other, [source, alternateSpelling], setup.Temp.Combine("目标")));
        Assert.AreEqual(1, result.Successful);
        Assert.AreEqual(1, result.Skipped);
        Assert.HasCount(1, await setup.Documents.ListByBookingAsync(setup.BookingId));
        Assert.HasCount(1, Directory.GetFiles(setup.Temp.Combine("目标")));
    }

    [TestMethod]
    public async Task Copy_DefaultConflictPolicyAutoNumbersAndNeverOverwrites()
    {
        using var setup = await SetupAsync();
        var source = setup.Temp.CreateFile("来源/协议.docx", [7, 8, 9]);
        var destination = setup.Temp.Combine("目标"); Directory.CreateDirectory(destination);
        var existing = Path.Combine(destination, "协议.docx"); await File.WriteAllBytesAsync(existing, [99]);
        var result = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.ShootAgreement, [source], destination));
        var copied = result.Items.Single(item => item.Document is not null).Document!.FilePath;
        CollectionAssert.AreEqual(new byte[] { 99 }, File.ReadAllBytes(existing));
        Assert.AreNotEqual(existing, copied);
        StringAssert.Contains(Path.GetFileName(copied), "(1)");
        Assert.IsTrue(File.Exists(source));
    }

    [TestMethod]
    public async Task Copy_StoresSha256AndVerifiedLength()
    {
        using var setup = await SetupAsync();
        var source = setup.Temp.CreateFile("来源/灯光图.png", Enumerable.Range(0, 128).Select(value => (byte)value).ToArray());
        var result = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.LightingDiagram, [source], setup.Temp.Combine("目标"), VerifySha256: true));
        var document = result.Items.Single(item => item.Document is not null).Document!;
        Assert.AreEqual(new FileInfo(source).Length, new FileInfo(document.FilePath).Length);
        Assert.IsNotNull(document.OptionalHash);
        Assert.AreEqual(64, document.OptionalHash.Length);
    }

    [TestMethod]
    public async Task Copy_DatabaseFailureReturnsPendingAssociationAndPartialTask()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("来源/授权书.pdf", [1, 2, 3]);
        var result = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.ModelRelease, [source], setup.Temp.Combine("目标")));
        var pending = result.Items.Single().PendingAssociation;
        Assert.IsNotNull(pending);
        Assert.IsTrue(File.Exists(pending.DestinationPath));
        Assert.IsTrue(File.Exists(source));
        Assert.AreEqual(BookingDocumentBatchStatus.NeedsAttention, result.Status);
        var task = await WaitForTerminalAsync(setup.Tasks, result.TaskId!.Value);
        Assert.AreEqual(TaskLifecycleState.PartiallyCompleted, task.State);
    }

    [TestMethod]
    public async Task PendingAssociation_CanRetryAfterDatabaseRecovers()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("来源/报价单.pdf", [5, 6]);
        var result = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Quotation, [source], setup.Temp.Combine("目标")));
        var normalWorkflow = setup.CreateWorkflow(setup.Documents);
        var retry = await normalWorkflow.RetryAssociationAsync(result.Items.Single().PendingAssociation!);
        Assert.IsTrue(retry.Succeeded);
        Assert.IsNotNull(retry.Document);
        Assert.IsTrue(File.Exists(retry.Document.FilePath));
    }

    [TestMethod]
    public async Task PendingCopy_UndoDeletesOnlyUnchangedTaskOutput()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("来源/策划.pdf", [1, 2, 3]);
        var result = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.PhotographyPlan, [source], setup.Temp.Combine("目标")));
        var pending = result.Items.Single().PendingAssociation!;
        var summary = await setup.Workflow.UndoCopiedFileAsync(pending);
        Assert.AreEqual(1, summary.Succeeded);
        Assert.IsFalse(File.Exists(pending.DestinationPath));
        Assert.IsTrue(File.Exists(source));
    }

    [TestMethod]
    public async Task PendingCopy_UndoRejectsFileModifiedByUser()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("来源/策划.pdf", [1, 2, 3]);
        var result = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.PhotographyPlan, [source], setup.Temp.Combine("目标")));
        var pending = result.Items.Single().PendingAssociation!;
        await using (var append = new FileStream(pending.DestinationPath, FileMode.Append, FileAccess.Write, FileShare.None)) await append.WriteAsync(new byte[] { 9 });
        var summary = await setup.Workflow.UndoCopiedFileAsync(pending);
        Assert.AreEqual(1, summary.WaitingForAttention);
        Assert.IsTrue(File.Exists(pending.DestinationPath));
    }

    [TestMethod]
    public async Task PendingCopy_UndoRejectsAssociatedManagedCopy()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("来源/策划.pdf", [1, 2, 3]);
        var result = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.PhotographyPlan, [source], setup.Temp.Combine("目标")));
        var pending = result.Items.Single().PendingAssociation!;
        var normalWorkflow = setup.CreateWorkflow(setup.Documents);
        Assert.IsTrue((await normalWorkflow.RetryAssociationAsync(pending)).Succeeded);
        var summary = await normalWorkflow.UndoCopiedFileAsync(pending);
        Assert.AreEqual(1, summary.WaitingForAttention);
        Assert.IsTrue(File.Exists(pending.DestinationPath));
    }

    [TestMethod]
    public async Task PendingAssociation_IsRecoveredFromPersistedTaskAfterRestart()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("来源/跨重启策划.pdf", [1, 2, 3, 4]);
        var result = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.PhotographyPlan, [source], setup.Temp.Combine("目标"), true));
        var original = result.Items.Single().PendingAssociation!;

        var restarted = setup.CreateRestartedWorkflow();
        var recovered = await restarted.ListPendingAssociationsAsync(setup.BookingId);

        Assert.HasCount(1, recovered);
        Assert.AreEqual(original.TaskId, recovered[0].TaskId);
        Assert.AreEqual(original.DestinationPath, recovered[0].DestinationPath);
        Assert.AreEqual(original.OutputSize, recovered[0].OutputSize);
        Assert.AreEqual(original.OutputHash, recovered[0].OutputHash);
        Assert.IsTrue(File.Exists(recovered[0].DestinationPath));
    }

    [TestMethod]
    public async Task RestartRecovery_IsIsolatedByBookingId()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("来源/隔离协议.pdf", [8, 9]);
        await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.ShootAgreement, [source], setup.Temp.Combine("目标")));
        var other = await setup.Bookings.SaveAsync(new ShootBookingDraft { Title = "其他排期", ClientDisplayName = "其他客户", StartAt = DateTimeOffset.UtcNow.AddDays(3), EndAt = DateTimeOffset.UtcNow.AddDays(3).AddHours(1), TimeZoneId = TimeZoneInfo.Utc.Id, ShootingType = "Portrait", AllowOverlap = true });

        var restarted = setup.CreateRestartedWorkflow();
        Assert.HasCount(1, await restarted.ListPendingAssociationsAsync(setup.BookingId));
        Assert.IsEmpty(await restarted.ListPendingAssociationsAsync(other.Booking!.Id));
    }

    [TestMethod]
    public async Task RecoveredAssociation_CanRetryAndDoesNotAppearAgain()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("来源/重试报价.pdf", [4, 5, 6]);
        await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Quotation, [source], setup.Temp.Combine("目标"), true));
        var restarted = setup.CreateRestartedWorkflow();
        var recovered = (await restarted.ListPendingAssociationsAsync(setup.BookingId)).Single();

        var retry = await restarted.RetryAssociationAsync(recovered);

        Assert.IsTrue(retry.Succeeded);
        Assert.IsNotNull(retry.Document);
        Assert.IsEmpty(await restarted.ListPendingAssociationsAsync(setup.BookingId));
        Assert.IsTrue(File.Exists(recovered.DestinationPath));
    }

    [TestMethod]
    public async Task RecoveredAssociation_SafeUndoVerifiesHashAndSize()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("来源/撤销授权.pdf", [3, 1, 4, 1, 5]);
        await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.ModelRelease, [source], setup.Temp.Combine("目标"), true));
        var restarted = setup.CreateRestartedWorkflow();
        var recovered = (await restarted.ListPendingAssociationsAsync(setup.BookingId)).Single();

        var undo = await restarted.UndoCopiedFileAsync(recovered);

        Assert.AreEqual(1, undo.Succeeded);
        Assert.IsFalse(File.Exists(recovered.DestinationPath));
        Assert.IsTrue(File.Exists(source));
        Assert.IsEmpty(await restarted.ListPendingAssociationsAsync(setup.BookingId));
    }

    [TestMethod]
    public async Task RecoveredAssociation_RejectsUndoAfterUserModification()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("来源/修改后撤销.pdf", [7, 7, 7]);
        await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Other, [source], setup.Temp.Combine("目标"), true));
        var restarted = setup.CreateRestartedWorkflow();
        var recovered = (await restarted.ListPendingAssociationsAsync(setup.BookingId)).Single();
        await File.AppendAllTextAsync(recovered.DestinationPath, "changed");

        var undo = await restarted.UndoCopiedFileAsync(recovered);

        Assert.AreEqual(1, undo.WaitingForAttention);
        Assert.IsTrue(File.Exists(recovered.DestinationPath));
        Assert.IsTrue(File.Exists(source));
    }

    [TestMethod]
    public async Task RecoveredAssociation_AbandonPersistsAndNeverDeletesFile()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("来源/放弃关联.pdf", [2, 7, 1, 8]);
        var copy = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Other, [source], setup.Temp.Combine("目标"), true));
        var persistedBeforeAbandon = await new SqliteTaskRepository(new PixelTartDatabase(setup.Database.DatabasePath)).GetAsync(copy.TaskId!.Value);
        Assert.AreEqual(TaskLifecycleState.PartiallyCompleted, persistedBeforeAbandon!.State);
        Assert.IsNotNull(persistedBeforeAbandon.CompletedAt);
        var restarted = setup.CreateRestartedWorkflow();
        var recovered = (await restarted.ListPendingAssociationsAsync(setup.BookingId)).Single();

        await restarted.AbandonAssociationAsync(recovered);

        Assert.IsTrue(File.Exists(recovered.DestinationPath));
        Assert.IsTrue(File.Exists(source));
        Assert.IsEmpty(await setup.Documents.ListByBookingAsync(setup.BookingId));
        Assert.IsEmpty(await setup.CreateRestartedWorkflow().ListPendingAssociationsAsync(setup.BookingId));
        var persistedAfterAbandon = await new SqliteTaskRepository(new PixelTartDatabase(setup.Database.DatabasePath)).GetAsync(copy.TaskId.Value);
        Assert.AreEqual(TaskLifecycleState.PartiallyCompleted, persistedAfterAbandon!.State);
        Assert.IsNotNull(persistedAfterAbandon.CompletedAt);
    }

    [TestMethod]
    public async Task CopyReturn_MakesTerminalStateAndRecoveryActionImmediatelyVisibleToNewConnection()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("来源/立即恢复.pdf", [8, 6, 7, 5]);

        var copy = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Other, [source], setup.Temp.Combine("目标"), true));

        var task = await new SqliteTaskRepository(new PixelTartDatabase(setup.Database.DatabasePath)).GetAsync(copy.TaskId!.Value);
        Assert.AreEqual(TaskLifecycleState.PartiallyCompleted, task!.State);
        Assert.IsNotNull(task.CompletedAt);
        Assert.HasCount(1, await setup.CreateRestartedWorkflow().ListPendingAssociationsAsync(setup.BookingId));
    }

    [TestMethod]
    public async Task AbandonAssociation_IsIdempotentAndKeepsBothFiles()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("来源/幂等放弃.pdf", [1, 6, 1, 8]);
        var copy = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Other, [source], setup.Temp.Combine("目标"), true));
        var pending = copy.Items.Single().PendingAssociation!;
        var restarted = setup.CreateRestartedWorkflow();

        await restarted.AbandonAssociationAsync(pending);
        await restarted.AbandonAssociationAsync(pending);

        Assert.IsTrue(File.Exists(source));
        Assert.IsTrue(File.Exists(pending.DestinationPath));
        Assert.IsEmpty(await setup.CreateRestartedWorkflow().ListPendingAssociationsAsync(setup.BookingId));
        var entries = await new SqliteUndoJournalRepository(new PixelTartDatabase(setup.Database.DatabasePath)).ListAsync(pending.TaskId);
        Assert.AreEqual(UndoJournalState.Rejected, entries.Single().State);
    }

    [TestMethod]
    public async Task AbandonAssociation_ConcurrentCallsProduceOnePersistedFinalJournalState()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("来源/并发放弃.pdf", [1, 2, 3, 5, 8]);
        var copy = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Other, [source], setup.Temp.Combine("目标"), true));
        var pending = copy.Items.Single().PendingAssociation!;
        var restarted = setup.CreateRestartedWorkflow();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => restarted.AbandonAssociationAsync(pending)));

        var entries = await new SqliteUndoJournalRepository(new PixelTartDatabase(setup.Database.DatabasePath)).ListAsync(pending.TaskId);
        Assert.HasCount(1, entries);
        Assert.AreEqual(UndoJournalState.Rejected, entries.Single().State);
        Assert.IsTrue(File.Exists(source));
        Assert.IsTrue(File.Exists(pending.DestinationPath));
        Assert.IsEmpty(await setup.CreateRestartedWorkflow().ListPendingAssociationsAsync(setup.BookingId));
    }

    [TestMethod]
    public async Task AbandonAssociation_PersistenceFailureKeepsRecoveryActionAndReturnsFailure()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("来源/写入失败.pdf", [4, 2]);
        var copy = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Other, [source], setup.Temp.Combine("目标"), true));
        var pending = copy.Items.Single().PendingAssociation!;
        var workflow = setup.CreateWorkflow(setup.Documents, undo: new FailingAbandonUndoJournalService(setup.Undo));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => workflow.AbandonAssociationAsync(pending));

        Assert.HasCount(1, await setup.CreateRestartedWorkflow().ListPendingAssociationsAsync(setup.BookingId));
        Assert.IsTrue(File.Exists(source));
        Assert.IsTrue(File.Exists(pending.DestinationPath));
    }

    [TestMethod]
    public async Task AbandonAssociation_UsesRejectOnlyAndNeverInvokesUndoDeletion()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("来源/只拒绝撤销.pdf", [9, 9, 7]);
        var copy = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Other, [source], setup.Temp.Combine("目标"), true));
        var pending = copy.Items.Single().PendingAssociation!;
        var tracking = new TrackingUndoJournalService(setup.Undo);

        await setup.CreateWorkflow(setup.Documents, undo: tracking).AbandonAssociationAsync(pending);

        Assert.AreEqual(1, tracking.AbandonCalls);
        Assert.AreEqual(0, tracking.UndoCalls);
        Assert.AreEqual(0, tracking.UndoFileCalls);
        Assert.IsTrue(File.Exists(source));
        Assert.IsTrue(File.Exists(pending.DestinationPath));
    }

    [TestMethod]
    public async Task AbandonAssociation_AuditContainsNoPathOrFileName()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("客户资料/绝密放弃文件.pdf", [2, 0, 2, 6]);
        var copy = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Other, [source], setup.Temp.Combine("目标"), true));
        var pending = copy.Items.Single().PendingAssociation!;

        await setup.CreateRestartedWorkflow().AbandonAssociationAsync(pending);

        await using var connection = await setup.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT SanitizedMessage FROM AuditLogs WHERE EventType='AssociationAbandoned' ORDER BY Timestamp DESC LIMIT 1;";
        var message = (string)(await command.ExecuteScalarAsync())!;
        Assert.IsFalse(message.Contains(setup.Temp.Path, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(message.Contains("绝密放弃文件.pdf", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CopyReturn_LeavesNoTerminalPersistenceOrCompletionNotificationRunning()
    {
        var repository = new TerminalGateTaskRepository();
        var bridge = new TaskOperationBridge();
        var notifications = new TrackingNotificationCenter();
        var engine = new TaskEngine(repository, new ConservativeTaskScheduler(), [bridge], new RecordingAuditLogService(), notifications, TimeSpan.Zero);
        bridge.Attach(engine);
        var summary = new TaskResultSummary(1, 1, 0, 0, 0, 1, 1, 1);

        var run = bridge.RunAsync("recovery-gate", (_, _) => Task.FromResult(summary));
        await repository.TerminalSaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Yield();
        Assert.IsFalse(run.IsCompleted, "TaskOperationBridge returned before TaskEngine persisted the terminal state.");

        repository.ReleaseTerminalSave();
        await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(0, repository.ActiveSaves);
        Assert.AreEqual(0, notifications.ActivePublishes);
        Assert.AreEqual(1, notifications.PublishedCount);
        Assert.AreEqual(TaskLifecycleState.PartiallyCompleted, repository.LastPersistedState);
        Assert.IsTrue(repository.CompletedAtPersisted);
    }

    [TestMethod]
    public void AbandonAssociationSourceContainsNoDeleteMoveOrUndoExecution()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "RAWSelectionAssistant.Core", "Services", "Bookings", "BookingDocumentWorkflowService.cs"));
        var start = source.IndexOf("public async Task AbandonAssociationAsync", StringComparison.Ordinal);
        var end = source.IndexOf("private static bool TryParseDocumentSnapshot", start, StringComparison.Ordinal);
        var method = source[start..end];
        StringAssert.Contains(method, "AbandonFileAsync");
        Assert.IsFalse(method.Contains("File.Delete", StringComparison.Ordinal));
        Assert.IsFalse(method.Contains("File.Move", StringComparison.Ordinal));
        Assert.IsFalse(method.Contains("UndoFileAsync", StringComparison.Ordinal));
        Assert.IsFalse(method.Contains("UndoAsync", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Verify_MissingOrDisconnectedFileIsMarkedWithoutException()
    {
        using var setup = await SetupAsync();
        var path = setup.Temp.CreateFile("外接盘/场地资料.pdf", [1]);
        var added = await setup.Workflow.AddReferencesAsync(new(setup.BookingId, null, BookingDocumentType.VenueMaterial, [path]));
        File.Delete(path);
        var check = await setup.Workflow.VerifyAsync(added.Items.Single().Document!.Id);
        Assert.AreEqual(BookingDocumentFileState.Missing, check!.State);
        Assert.IsTrue(check.Document.IsMissing);
        Assert.IsNotNull(check.Document.MissingSinceAtUtc);
    }

    [TestMethod]
    public async Task Verify_ModifiedFileIsReportedWithoutReplacingLastKnownMetadata()
    {
        using var setup = await SetupAsync();
        var path = setup.Temp.CreateFile("资料/场地资料.pdf", [1]);
        var document = (await setup.Workflow.AddReferencesAsync(new(setup.BookingId, null, BookingDocumentType.VenueMaterial, [path]))).Items.Single().Document!;
        await using (var append = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None)) await append.WriteAsync(new byte[] { 2, 3 });
        var check = await setup.Workflow.VerifyAsync(document.Id);
        Assert.AreEqual(BookingDocumentFileState.Modified, check!.State);
        Assert.AreEqual(1L, check.Document.FileSize);
        Assert.IsFalse(check.Document.IsMissing);
    }

    [TestMethod]
    public async Task Relocate_WithoutHashUpdatesMetadataAndClearsMissing()
    {
        using var setup = await SetupAsync();
        var old = setup.Temp.CreateFile("旧/服装参考.jpg", [1]);
        var document = (await setup.Workflow.AddReferencesAsync(new(setup.BookingId, null, BookingDocumentType.WardrobeReference, [old]))).Items.Single().Document!;
        File.Delete(old); await setup.Workflow.VerifyAsync(document.Id);
        var replacement = setup.Temp.CreateFile("新位置/服装参考 新.jpg", [1, 2]);
        var result = await setup.Workflow.RelocateAsync(document.Id, replacement);
        Assert.AreEqual(BookingDocumentRelocationStatus.Relocated, result.Status);
        Assert.AreEqual(Path.GetFullPath(replacement), result.Document!.FilePath);
        Assert.IsFalse(result.Document.IsMissing);
    }

    [TestMethod]
    public async Task Relocate_HashMatchAcceptsAndMismatchRequiresConfirmation()
    {
        using var setup = await SetupAsync();
        var original = setup.Temp.CreateFile("旧/灯光图.png", [1, 2, 3]);
        var hash = await setup.Verification.ComputeSha256Async(original);
        var document = Document(setup.BookingId, original, BookingDocumentLinkMode.ManagedCopy, hash);
        await setup.Documents.AddAsync(document);
        var matching = setup.Temp.CreateFile("新/同内容.png", [1, 2, 3]);
        Assert.AreEqual(BookingDocumentRelocationStatus.Relocated, (await setup.Workflow.RelocateAsync(document.Id, matching)).Status);
        var different = setup.Temp.CreateFile("新/不同内容.png", [9, 9, 9]);
        var mismatch = await setup.Workflow.RelocateAsync(document.Id, different);
        Assert.AreEqual(BookingDocumentRelocationStatus.HashMismatch, mismatch.Status);
        Assert.IsTrue(mismatch.RequiresConfirmation);
        Assert.AreEqual(Path.GetFullPath(matching), (await setup.Documents.GetAsync(document.Id))!.FilePath);
        var accepted = await setup.Workflow.RelocateAsync(document.Id, different, acceptHashMismatch: true);
        Assert.AreEqual(BookingDocumentRelocationStatus.Relocated, accepted.Status);
        Assert.AreNotEqual(hash, accepted.Document!.OptionalHash);
    }

    [TestMethod]
    public async Task Relocate_AtomicallyPersistsPathMetadataHashAndVerificationState()
    {
        using var setup = await SetupAsync();
        var original = setup.Temp.CreateFile("旧/原始策划.pdf", [1, 2, 3]);
        var originalHash = await setup.Verification.ComputeSha256Async(original);
        var document = Document(setup.BookingId, original, BookingDocumentLinkMode.ManagedCopy, originalHash) with
        {
            IsMissing = true,
            MissingSinceAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
            LastVerifiedAtUtc = DateTimeOffset.UtcNow.AddHours(-1)
        };
        await setup.Documents.AddAsync(document);

        var replacement = setup.Temp.CreateFile("新位置/客户 策划修订.pdf", [9, 8, 7, 6]);
        var expectedModifiedUtc = DateTime.UtcNow.AddMinutes(-5);
        File.SetLastWriteTimeUtc(replacement, expectedModifiedUtc);
        var expectedHash = await setup.Verification.ComputeSha256Async(replacement);

        var result = await setup.Workflow.RelocateAsync(document.Id, replacement, acceptHashMismatch: true);
        var persisted = await setup.Documents.GetAsync(document.Id);

        Assert.AreEqual(BookingDocumentRelocationStatus.Relocated, result.Status);
        Assert.IsNotNull(persisted);
        Assert.AreEqual(Path.GetFullPath(replacement), persisted.FilePath);
        Assert.AreEqual(Path.GetFullPath(replacement).ToUpperInvariant(), persisted.NormalizedPath);
        Assert.AreEqual(4L, persisted.FileSize);
        Assert.IsNotNull(persisted.LastKnownModifiedAtUtc);
        Assert.IsLessThan(1d, Math.Abs((persisted.LastKnownModifiedAtUtc.Value.UtcDateTime - File.GetLastWriteTimeUtc(replacement)).TotalSeconds));
        Assert.AreEqual(expectedHash, persisted.OptionalHash);
        Assert.IsFalse(persisted.IsMissing);
        Assert.IsNull(persisted.MissingSinceAtUtc);
        Assert.IsNotNull(persisted.LastVerifiedAtUtc);
        Assert.IsGreaterThan(document.LastVerifiedAtUtc!.Value, persisted.LastVerifiedAtUtc.Value);
        Assert.AreEqual(result.Document!.LastVerifiedAtUtc, persisted.LastVerifiedAtUtc);
    }

    [TestMethod]
    public async Task DocumentProjectIdAlwaysComesFromTheBooking()
    {
        using var setup = await SetupAsync();
        var path = setup.Temp.CreateFile("资料/策划.pdf", [1]);
        var unrelatedProjectId = Guid.NewGuid();
        var document = (await setup.Workflow.AddReferencesAsync(new(setup.BookingId, unrelatedProjectId, BookingDocumentType.PhotographyPlan, [path]))).Items.Single().Document!;
        Assert.IsNull(document.ProjectId);
    }

    [TestMethod]
    public async Task Relocate_DuplicateTargetLeavesBothAssociationsUnchanged()
    {
        using var setup = await SetupAsync();
        var firstPath = setup.Temp.CreateFile("资料/第一份.pdf", [1]);
        var secondPath = setup.Temp.CreateFile("资料/第二份.pdf", [2]);
        var added = await setup.Workflow.AddReferencesAsync(new(setup.BookingId, null, BookingDocumentType.Other, [firstPath, secondPath]));
        var first = added.Items.Single(item => item.Document!.FilePath == Path.GetFullPath(firstPath)).Document!;
        var result = await setup.Workflow.RelocateAsync(first.Id, secondPath);
        Assert.AreEqual(BookingDocumentRelocationStatus.Failed, result.Status);
        Assert.AreEqual(Path.GetFullPath(firstPath), (await setup.Documents.GetAsync(first.Id))!.FilePath);
        Assert.HasCount(2, await setup.Documents.ListByBookingAsync(setup.BookingId));
    }

    [TestMethod]
    public async Task RemoveAssociation_NeverDeletesReferenceOrManagedCopy()
    {
        using var setup = await SetupAsync();
        var referencePath = setup.Temp.CreateFile("资料/协议.pdf", [1]);
        var reference = (await setup.Workflow.AddReferencesAsync(new(setup.BookingId, null, BookingDocumentType.ShootAgreement, [referencePath]))).Items.Single().Document!;
        var source = setup.Temp.CreateFile("来源/报价.pdf", [2]);
        var managed = (await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Quotation, [source], setup.Temp.Combine("目标")))).Items.Single(item => item.Document is not null).Document!;
        Assert.IsTrue(await setup.Workflow.RemoveAssociationAsync(reference.Id));
        Assert.IsTrue(await setup.Workflow.RemoveAssociationAsync(managed.Id));
        Assert.IsTrue(File.Exists(referencePath));
        Assert.IsTrue(File.Exists(managed.FilePath));
    }

    [TestMethod]
    public async Task ArchivedBookingPreservesDocumentsAndBlocksRemovalUntilRestored()
    {
        using var setup = await SetupAsync();
        var path = setup.Temp.CreateFile("资料/协议.pdf", [1]);
        var document = (await setup.Workflow.AddReferencesAsync(new(setup.BookingId, null, BookingDocumentType.ShootAgreement, [path]))).Items.Single().Document!;
        Assert.IsTrue(await setup.Bookings.ArchiveAsync(setup.BookingId));
        Assert.IsFalse(await setup.Workflow.RemoveAssociationAsync(document.Id));
        Assert.HasCount(1, await setup.Workflow.ListAsync(setup.BookingId));
        Assert.IsTrue(await setup.Bookings.RestoreAsync(setup.BookingId));
        Assert.HasCount(1, await setup.Workflow.ListAsync(setup.BookingId));
    }

    [TestMethod]
    public async Task ArchivedBookingBlocksRetryingAPendingAssociation()
    {
        using var setup = await SetupAsync(failDocumentAdds: true);
        var source = setup.Temp.CreateFile("来源/待恢复.pdf", [1, 2, 3]);
        var result = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Other, [source], setup.Temp.Combine("目标")));
        var pending = result.Items.Single().PendingAssociation!;
        Assert.IsTrue(await setup.Bookings.ArchiveAsync(setup.BookingId));
        var retry = await setup.CreateWorkflow(setup.Documents).RetryAssociationAsync(pending);
        Assert.IsFalse(retry.Succeeded);
        Assert.IsEmpty(await setup.Documents.ListByBookingAsync(setup.BookingId));
        Assert.IsTrue(File.Exists(pending.DestinationPath));
    }

    [TestMethod]
    public async Task Copy_FileLockedReturnsSafeFailureAndKeepsSource()
    {
        using var setup = await SetupAsync();
        var source = setup.Temp.CreateFile("来源/占用.pdf", [1, 2, 3]);
        await using var locked = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var result = await setup.Workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Other, [source], setup.Temp.Combine("目标")));
        Assert.AreEqual(1, result.Failed);
        Assert.IsTrue(File.Exists(source));
        Assert.IsEmpty(await setup.Documents.ListByBookingAsync(setup.BookingId));
    }

    [TestMethod]
    public async Task Copy_PermissionDeniedReturnsSafeFailureAndKeepsSource()
    {
        using var setup = await SetupAsync();
        var source = setup.Temp.CreateFile("来源/权限测试.pdf", [1, 2, 3]);
        var workflow = setup.CreateWorkflow(setup.Documents, setup.CreateRejectedExecutor(ErrorCodeCatalog.PermissionDenied, requiresAttention: false));
        var result = await workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Other, [source], setup.Temp.Combine("目标")));
        Assert.AreEqual(1, result.Failed);
        Assert.AreEqual(ErrorCodeCatalog.PermissionDenied, result.Items.Single().ErrorCode);
        Assert.IsTrue(File.Exists(source));
        Assert.IsEmpty(await setup.Documents.ListByBookingAsync(setup.BookingId));
        Assert.AreEqual(TaskLifecycleState.Failed, (await WaitForTerminalAsync(setup.Tasks, result.TaskId!.Value)).State);
    }

    [TestMethod]
    public async Task Copy_InsufficientDiskSpaceRequiresAttentionAndKeepsSource()
    {
        using var setup = await SetupAsync();
        var source = setup.Temp.CreateFile("来源/空间测试.pdf", [1, 2, 3]);
        var workflow = setup.CreateWorkflow(setup.Documents, setup.CreateRejectedExecutor(ErrorCodeCatalog.DiskSpaceInsufficient, requiresAttention: true));
        var result = await workflow.CopyAndAssociateAsync(new(setup.BookingId, null, BookingDocumentType.Other, [source], setup.Temp.Combine("目标")));
        Assert.AreEqual(BookingDocumentBatchStatus.NeedsAttention, result.Status);
        Assert.AreEqual(1, result.WaitingForAttention);
        Assert.AreEqual(ErrorCodeCatalog.DiskSpaceInsufficient, result.Items.Single().ErrorCode);
        Assert.IsTrue(File.Exists(source));
        Assert.IsEmpty(await setup.Documents.ListByBookingAsync(setup.BookingId));
        Assert.AreEqual(TaskLifecycleState.NeedsAttention, (await WaitForStateAsync(setup.Tasks, result.TaskId!.Value, TaskLifecycleState.NeedsAttention)).State);
    }

    [TestMethod]
    public async Task PathsWithChineseSpacesAndLongNamesArePreserved()
    {
        using var setup = await SetupAsync();
        var longName = new string('长', 80) + " 文件 名称.pdf";
        var path = setup.Temp.CreateFile(Path.Combine("中文 资料", longName), [1]);
        var document = (await setup.Workflow.AddReferencesAsync(new(setup.BookingId, null, BookingDocumentType.Other, [path]))).Items.Single().Document!;
        Assert.AreEqual(longName, document.DisplayName);
        Assert.AreEqual(Path.GetFullPath(path).ToUpperInvariant(), document.NormalizedPath);
    }

    [TestMethod]
    public async Task AuditAndDiagnosticsHidePathFileNameDisplayNameAndHash()
    {
        using var setup = await SetupAsync();
        var path = setup.Temp.CreateFile("客户甲/秘密策划.pdf", [1]);
        await setup.Workflow.AddReferencesAsync(new(setup.BookingId, null, BookingDocumentType.PhotographyPlan, [path]));
        await using (var connection = await setup.Database.OpenConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT SanitizedMessage FROM AuditLogs WHERE Category='BookingDocument';";
            var value = (string)(await command.ExecuteScalarAsync())!;
            Assert.IsFalse(value.Contains("秘密策划", StringComparison.Ordinal));
            Assert.IsFalse(value.Contains("客户甲", StringComparison.Ordinal));
            StringAssert.Contains(value, $"BookingId={setup.BookingId:D}");
        }
        var hash = new string('A', 64);
        var sanitized = AuditLogService.Sanitize($"FileName=秘密策划.pdf DisplayName=客户协议 OptionalHash={hash} C:\\客户甲\\秘密策划.pdf");
        Assert.IsFalse(sanitized.Contains("秘密策划", StringComparison.Ordinal));
        Assert.IsFalse(sanitized.Contains(hash, StringComparison.Ordinal));
        var logDirectory = setup.Temp.Combine("logs"); Directory.CreateDirectory(logDirectory);
        await File.WriteAllTextAsync(Path.Combine(logDirectory, "app.log"), $"C:\\客户甲\\秘密策划.pdf OptionalHash={hash}");
        var zip = await new LogMaintenanceService(logDirectory).ExportDiagnosticsAsync(setup.Temp.Combine("diagnostics.zip"));
        using var archive = ZipFile.OpenRead(zip);
        using var reader = new StreamReader(archive.Entries.Single(entry => entry.FullName.EndsWith("app.log", StringComparison.Ordinal)).Open());
        var exported = await reader.ReadToEndAsync();
        Assert.IsFalse(exported.Contains("秘密策划", StringComparison.Ordinal));
        Assert.IsFalse(exported.Contains(hash, StringComparison.Ordinal));
    }

    [TestMethod]
    public void WorkflowSourceDoesNotReadDocumentBodyOrCreateSecondCopySystem()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "RAWSelectionAssistant.Core", "Services", "Bookings", "BookingDocumentWorkflowService.cs"));
        StringAssert.Contains(source, "IFileOperationPlanner");
        StringAssert.Contains(source, "IFileOperationExecutor");
        StringAssert.Contains(source, "TaskOperationBridge");
        StringAssert.Contains(source, "await progress.DrainAsync()");
        Assert.IsFalse(source.Contains("_ = context.ReportProgressAsync", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("File.Copy", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("File.Move", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("ReadAllText", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("StreamReader", StringComparison.Ordinal));
    }

    private static BookingDocumentRecord Document(Guid bookingId, string path, BookingDocumentLinkMode mode, string? hash) => new()
    {
        Id = Guid.NewGuid(), BookingId = bookingId, DocumentType = BookingDocumentType.Other, DisplayName = Path.GetFileName(path), FilePath = Path.GetFullPath(path),
        NormalizedPath = Path.GetFullPath(path).ToUpperInvariant(), FileExtension = Path.GetExtension(path), FileSize = new FileInfo(path).Length,
        LastKnownModifiedAtUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero), OptionalHash = hash, LinkMode = mode,
        AddedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow, LastVerifiedAtUtc = DateTimeOffset.UtcNow
    };

    private static async Task<TaskRuntimeState> WaitForTerminalAsync(ITaskRepository repository, Guid taskId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var state = await repository.GetAsync(taskId);
            if (state is not null && TaskStateMachine.IsTerminal(state.State)) return state;
            await Task.Delay(20);
        }
        return (await repository.GetAsync(taskId))!;
    }

    private static async Task<TaskRuntimeState> WaitForStateAsync(ITaskRepository repository, Guid taskId, TaskLifecycleState expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var state = await repository.GetAsync(taskId);
            if (state?.State == expected) return state;
            await Task.Delay(20);
        }
        return (await repository.GetAsync(taskId))!;
    }

    private static async Task<Setup> SetupAsync(bool failDocumentAdds = false)
    {
        var temp = new TempDirectory();
        var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        Assert.IsTrue((await new DatabaseMigrator(database, new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync()).Success);
        var bookingRepository = new SqliteShootBookingRepository(database);
        var bookingService = new ShootBookingService(bookingRepository, new BookingConflictDetector(bookingRepository));
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var booking = await bookingService.SaveAsync(new ShootBookingDraft { Title = "文档测试", ClientDisplayName = "测试客户", StartAt = start, EndAt = start.AddHours(1), TimeZoneId = TimeZoneInfo.Utc.Id, ShootingType = "Portrait" });
        var documents = new SqliteBookingDocumentRepository(database);
        IBookingDocumentRepository workflowDocuments = failDocumentAdds ? new FailingAddDocumentRepository(documents) : documents;
        var projects = new SqliteProjectRepository(database);
        var audit = new AuditLogService(database);
        var notifications = new NotificationCenter(database, TimeSpan.Zero);
        var taskRepository = new SqliteTaskRepository(database);
        var verification = new FileVerificationService();
        var undoRepository = new SqliteUndoJournalRepository(database);
        var planner = new FileOperationPlanner(new FileConflictResolver());
        var executor = new FileOperationExecutor(new FileOperationValidator(), verification, undoRepository, database);
        var undo = new UndoJournalService(undoRepository, verification);
        var bridge = new TaskOperationBridge();
        var engine = new TaskEngine(taskRepository, new ConservativeTaskScheduler(), [bridge], audit, notifications, TimeSpan.Zero);
        bridge.Attach(engine);
        var setup = new Setup(temp, database, booking.Booking!.Id, bookingService, projects, documents, workflowDocuments, verification, planner, executor, undo, bridge, taskRepository, audit);
        setup.Workflow = setup.CreateWorkflow(workflowDocuments);
        return setup;
    }

    private sealed class Setup : IDisposable
    {
        public Setup(TempDirectory temp, PixelTartDatabase database, Guid bookingId, ShootBookingService bookings, SqliteProjectRepository projects,
            SqliteBookingDocumentRepository documents, IBookingDocumentRepository workflowDocuments, FileVerificationService verification,
            FileOperationPlanner planner, FileOperationExecutor executor, UndoJournalService undo, TaskOperationBridge bridge, SqliteTaskRepository tasks, AuditLogService audit)
        {
            Temp = temp; Database = database; BookingId = bookingId; Bookings = bookings; Projects = projects; Documents = documents; WorkflowDocuments = workflowDocuments;
            Verification = verification; Planner = planner; Executor = executor; Undo = undo; Bridge = bridge; Tasks = tasks; Audit = audit;
        }
        public TempDirectory Temp { get; }
        public PixelTartDatabase Database { get; }
        public Guid BookingId { get; }
        public ShootBookingService Bookings { get; }
        public SqliteProjectRepository Projects { get; }
        public SqliteBookingDocumentRepository Documents { get; }
        public IBookingDocumentRepository WorkflowDocuments { get; }
        public FileVerificationService Verification { get; }
        public FileOperationPlanner Planner { get; }
        public FileOperationExecutor Executor { get; }
        public UndoJournalService Undo { get; }
        public TaskOperationBridge Bridge { get; }
        public SqliteTaskRepository Tasks { get; }
        public AuditLogService Audit { get; }
        public BookingDocumentWorkflowService Workflow { get; set; } = null!;
        public BookingDocumentWorkflowService CreateWorkflow(IBookingDocumentRepository repository, IFileOperationExecutor? executor = null, IUndoJournalService? undo = null) =>
            new(repository, Bookings, Projects, Planner, executor ?? Executor, Verification, undo ?? Undo, Bridge, Audit, Database);
        public BookingDocumentWorkflowService CreateRestartedWorkflow()
        {
            var database = new PixelTartDatabase(Database.DatabasePath);
            var bookingRepository = new SqliteShootBookingRepository(database);
            var bookings = new ShootBookingService(bookingRepository, new BookingConflictDetector(bookingRepository));
            var documents = new SqliteBookingDocumentRepository(database);
            var projects = new SqliteProjectRepository(database);
            var verification = new FileVerificationService();
            var journals = new SqliteUndoJournalRepository(database);
            return new BookingDocumentWorkflowService(documents, bookings, projects, new FileOperationPlanner(new FileConflictResolver()),
                new FileOperationExecutor(new FileOperationValidator(), verification, journals, database), verification,
                new UndoJournalService(journals, verification), new TaskOperationBridge(), new AuditLogService(database), database);
        }
        public IFileOperationExecutor CreateRejectedExecutor(string errorCode, bool requiresAttention) =>
            new FileOperationExecutor(new RejectedFileOperationValidator(errorCode, requiresAttention), Verification, new SqliteUndoJournalRepository(Database), Database);
        public void Dispose() { SqliteTestIsolation.ClearPool(Database); Temp.Dispose(); }
    }

    private sealed class RejectedFileOperationValidator(string errorCode, bool requiresAttention) : IFileOperationValidator
    {
        public Task<FileOperationValidationResult> ValidateAsync(FileOperationPlan plan, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FileOperationValidationResult(false,
                [new FileOperationValidationIssue(errorCode, ErrorCodeCatalog.Describe(errorCode), RequiresAttention: requiresAttention)],
                plan.Items.Sum(item => item.ExpectedSourceSize ?? 0), plan.RiskLevel));
    }

    private sealed class FailingAddDocumentRepository(IBookingDocumentRepository inner) : IBookingDocumentRepository
    {
        public Task AddAsync(BookingDocumentRecord document, CancellationToken cancellationToken = default) => throw new SqliteException("forced document write failure", 5);
        public Task<BookingDocumentRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetAsync(id, cancellationToken);
        public Task<BookingDocumentRecord?> GetByNormalizedPathAsync(Guid bookingId, string normalizedPath, CancellationToken cancellationToken = default) => inner.GetByNormalizedPathAsync(bookingId, normalizedPath, cancellationToken);
        public Task<BookingDocumentRecord?> GetByNormalizedPathAsync(string normalizedPath, CancellationToken cancellationToken = default) => inner.GetByNormalizedPathAsync(normalizedPath, cancellationToken);
        public Task<IReadOnlyList<BookingDocumentRecord>> ListByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default) => inner.ListByBookingAsync(bookingId, cancellationToken);
        public Task UpdateLocationAsync(Guid id, string filePath, string normalizedPath, string fileExtension, long? fileSize, DateTimeOffset? modifiedAtUtc, bool isMissing, DateTimeOffset verifiedAtUtc, CancellationToken cancellationToken = default) => inner.UpdateLocationAsync(id, filePath, normalizedPath, fileExtension, fileSize, modifiedAtUtc, isMissing, verifiedAtUtc, cancellationToken);
        public Task UpdateLocationAndHashAsync(Guid id, string filePath, string normalizedPath, string fileExtension, long? fileSize, DateTimeOffset? modifiedAtUtc, string? optionalHash, bool isMissing, DateTimeOffset verifiedAtUtc, CancellationToken cancellationToken = default) => inner.UpdateLocationAndHashAsync(id, filePath, normalizedPath, fileExtension, fileSize, modifiedAtUtc, optionalHash, isMissing, verifiedAtUtc, cancellationToken);
        public Task SetMissingAsync(Guid id, bool isMissing, DateTimeOffset verifiedAtUtc, CancellationToken cancellationToken = default) => inner.SetMissingAsync(id, isMissing, verifiedAtUtc, cancellationToken);
        public Task UpdateHashAsync(Guid id, string? optionalHash, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default) => inner.UpdateHashAsync(id, optionalHash, updatedAtUtc, cancellationToken);
        public Task<bool> RemoveAssociationAsync(Guid id, CancellationToken cancellationToken = default) => inner.RemoveAssociationAsync(id, cancellationToken);
    }

    private sealed class FailingAbandonUndoJournalService(IUndoJournalService inner) : IUndoJournalService
    {
        public Task<TaskResultSummary> UndoAsync(Guid taskId, CancellationToken cancellationToken = default) => inner.UndoAsync(taskId, cancellationToken);
        public Task<TaskResultSummary> UndoFileAsync(Guid taskId, string destinationPath, CancellationToken cancellationToken = default) => inner.UndoFileAsync(taskId, destinationPath, cancellationToken);
        public Task<bool> AbandonFileAsync(Guid taskId, string destinationPath, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("forced abandon persistence failure");
    }

    private sealed class TrackingUndoJournalService(IUndoJournalService inner) : IUndoJournalService
    {
        public int UndoCalls { get; private set; }
        public int UndoFileCalls { get; private set; }
        public int AbandonCalls { get; private set; }
        public Task<TaskResultSummary> UndoAsync(Guid taskId, CancellationToken cancellationToken = default) { UndoCalls++; return inner.UndoAsync(taskId, cancellationToken); }
        public Task<TaskResultSummary> UndoFileAsync(Guid taskId, string destinationPath, CancellationToken cancellationToken = default) { UndoFileCalls++; return inner.UndoFileAsync(taskId, destinationPath, cancellationToken); }
        public Task<bool> AbandonFileAsync(Guid taskId, string destinationPath, CancellationToken cancellationToken = default) { AbandonCalls++; return inner.AbandonFileAsync(taskId, destinationPath, cancellationToken); }
    }

    private sealed class TerminalGateTaskRepository : ITaskRepository
    {
        private readonly TaskCompletionSource<bool> _releaseTerminalSave = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _terminalSaveBlocked;
        private int _activeSaves;
        public TaskCompletionSource<bool> TerminalSaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ActiveSaves => Volatile.Read(ref _activeSaves);
        public TaskLifecycleState LastPersistedState { get; private set; } = TaskLifecycleState.Pending;
        public bool CompletedAtPersisted { get; private set; }

        public async Task SaveAsync(TaskRuntimeState state, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _activeSaves);
            try
            {
                if (TaskStateMachine.IsTerminal(state.State) && Interlocked.Exchange(ref _terminalSaveBlocked, 1) == 0)
                {
                    TerminalSaveStarted.TrySetResult(true);
                    await _releaseTerminalSave.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                LastPersistedState = state.State;
                CompletedAtPersisted = state.CompletedAt is not null;
            }
            finally
            {
                Interlocked.Decrement(ref _activeSaves);
            }
        }

        public void ReleaseTerminalSave() => _releaseTerminalSave.TrySetResult(true);
        public Task<TaskRuntimeState?> GetAsync(Guid taskId, CancellationToken cancellationToken = default) => Task.FromResult<TaskRuntimeState?>(null);
        public Task<IReadOnlyList<TaskRuntimeState>> ListAsync(int limit = 200, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TaskRuntimeState>>([]);
        public Task<IReadOnlyList<TaskRuntimeState>> ListUnfinishedAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TaskRuntimeState>>([]);
        public Task SaveCheckpointAsync(Guid taskId, TaskCheckpoint checkpoint, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingAuditLogService : IAuditLogService
    {
        public Task WriteAsync(string category, string eventType, string severity, string message, Guid? taskId = null, Guid? projectId = null, string? errorCode = null, string? correlationId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TrackingNotificationCenter : INotificationCenter
    {
        private int _activePublishes;
        public event EventHandler<NotificationMessage>? Published;
        public int ActivePublishes => Volatile.Read(ref _activePublishes);
        public int PublishedCount { get; private set; }
        public Task<IReadOnlyList<NotificationMessage>> GetHistoryAsync(int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NotificationMessage>>([]);
        public Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void NotifyPersisted(NotificationMessage message) => Published?.Invoke(this, message);
        public Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _activePublishes);
            try { PublishedCount++; NotifyPersisted(message); return Task.CompletedTask; }
            finally { Interlocked.Decrement(ref _activePublishes); }
        }
    }

    private static string Root() { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName; throw new DirectoryNotFoundException(); }
}
