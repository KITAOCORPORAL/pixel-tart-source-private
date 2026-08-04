using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;

#pragma warning disable MSTEST0037

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version220DocumentReminderTests
{
    [TestMethod]
    public async Task DocumentReference_StoresOnlyPathAndMetadata()
    {
        using var setup = await SetupAsync();
        var path = setup.Temp.CreateFile("docs/策划.pdf", [1, 2, 3, 4]);
        var document = await setup.DocumentService.AddReferenceAsync(setup.BookingId, null, BookingDocumentType.PhotographyPlan, path);
        Assert.AreEqual(Path.GetFullPath(path), document.FilePath);
        Assert.AreEqual(4, document.FileSize);
        Assert.AreEqual(BookingDocumentLinkMode.Reference, document.LinkMode);
        await using var connection = await setup.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT FilePath,FileSize,OptionalHash FROM BookingDocuments WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", document.Id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync();
        Assert.IsTrue(await reader.ReadAsync());
        Assert.AreEqual(Path.GetFullPath(path), reader.GetString(0));
        Assert.AreEqual(4L, reader.GetInt64(1));
        Assert.IsTrue(reader.IsDBNull(2));
    }

    [TestMethod]
    public async Task RemoveDocumentAssociation_DoesNotDeleteComputerFile()
    {
        using var setup = await SetupAsync();
        var path = setup.Temp.CreateFile("docs/协议.docx", [1, 2, 3]);
        var document = await setup.DocumentService.AddReferenceAsync(setup.BookingId, null, BookingDocumentType.ShootAgreement, path);
        Assert.IsTrue(await setup.DocumentService.RemoveAssociationAsync(document.Id));
        Assert.IsTrue(File.Exists(path));
        Assert.IsNull(await setup.DocumentRepository.GetAsync(document.Id));
    }

    [TestMethod]
    public async Task MissingDocument_IsMarkedWithoutThrowing()
    {
        using var setup = await SetupAsync();
        var path = setup.Temp.CreateFile("docs/授权书.pdf", [1]);
        var document = await setup.DocumentService.AddReferenceAsync(setup.BookingId, null, BookingDocumentType.ModelRelease, path);
        File.Delete(path);
        var verified = await setup.DocumentService.VerifyAsync(document.Id);
        Assert.IsNotNull(verified);
        Assert.IsTrue(verified.IsMissing);
        Assert.IsNotNull(verified.MissingSinceAtUtc);
    }

    [TestMethod]
    public async Task MissingDocument_CanBeRelocated()
    {
        using var setup = await SetupAsync();
        var oldPath = setup.Temp.CreateFile("docs/旧报价单.xlsx", [1]);
        var document = await setup.DocumentService.AddReferenceAsync(setup.BookingId, null, BookingDocumentType.Quotation, oldPath);
        File.Delete(oldPath);
        await setup.DocumentService.VerifyAsync(document.Id);
        var newPath = setup.Temp.CreateFile("moved/新报价单.xlsx", [9, 8]);
        var relocated = await setup.DocumentService.RelocateAsync(document.Id, newPath);
        Assert.AreEqual(Path.GetFullPath(newPath), relocated.FilePath);
        Assert.IsFalse(relocated.IsMissing);
        Assert.AreEqual(2, relocated.FileSize);
    }

    [TestMethod]
    public async Task UnsupportedDocumentType_IsRejected()
    {
        using var setup = await SetupAsync();
        var path = setup.Temp.CreateFile("docs/script.exe", [1]);
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => setup.DocumentService.AddReferenceAsync(setup.BookingId, null, BookingDocumentType.Other, path));
    }

    [TestMethod]
    public async Task ArchivingBooking_PreservesDocumentAssociation()
    {
        using var setup = await SetupAsync();
        var path = setup.Temp.CreateFile("docs/灯光图.png", [1, 2]);
        await setup.DocumentService.AddReferenceAsync(setup.BookingId, null, BookingDocumentType.LightingDiagram, path);
        Assert.IsTrue(await setup.BookingService.ArchiveAsync(setup.BookingId));
        Assert.HasCount(1, await setup.DocumentRepository.ListByBookingAsync(setup.BookingId));
        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public async Task ReminderRepository_DefaultsToDisabled()
    {
        using var setup = await SetupAsync();
        var id = Guid.NewGuid();
        await setup.ReminderRepository.SaveAsync(new(id, null, "默认提醒", string.Empty, new(ReminderTriggerKind.AbsoluteTime, DateTimeOffset.UtcNow.AddHours(1), null), BookingId: setup.BookingId));
        var saved = await setup.ReminderRepository.GetAsync(id);
        Assert.IsNotNull(saved);
        Assert.IsFalse(saved.IsEnabled);
        Assert.AreEqual(ReminderStatus.Disabled, saved.Status);
    }

    [TestMethod]
    public async Task RelativeReminder_UsesBookingStartAndLeadTime()
    {
        using var setup = await SetupAsync();
        var id = Guid.NewGuid();
        await setup.ReminderRepository.SaveAsync(new(id, null, "提前一小时", string.Empty, new(ReminderTriggerKind.RelativeToBookingStart, null, TimeSpan.FromHours(1)), ReminderStatus.Scheduled, setup.BookingId, true));
        var due = await setup.ReminderRepository.ListDueAsync(setup.BookingStart.AddHours(-2), setup.BookingStart, 100);
        Assert.IsTrue(due.Any(x => x.Id == id));
    }

    [TestMethod]
    public async Task DisabledReminder_IsNotReturnedAsDue()
    {
        using var setup = await SetupAsync();
        var id = Guid.NewGuid();
        var at = DateTimeOffset.UtcNow.AddMinutes(10);
        await setup.ReminderRepository.SaveAsync(new(id, null, "关闭提醒", string.Empty, new(ReminderTriggerKind.AbsoluteTime, at, null), BookingId: setup.BookingId));
        Assert.IsFalse((await setup.ReminderRepository.ListDueAsync(at.AddMinutes(-1), at.AddMinutes(1))).Any(x => x.Id == id));
    }

    [TestMethod]
    public async Task ReminderForFinishedBooking_IsNotReturnedAsDue()
    {
        using var setup = await SetupAsync();
        var repository = new SqliteShootBookingRepository(setup.Database);
        var service = new ShootBookingService(repository, new BookingConflictDetector(repository));
        var end = DateTimeOffset.UtcNow.AddHours(-1);
        var booking = await service.SaveAsync(new ShootBookingDraft { Title = "已结束", ClientDisplayName = "客户", StartAt = end.AddHours(-1), EndAt = end, TimeZoneId = TimeZoneInfo.Utc.Id, ShootingType = "Portrait" });
        var trigger = DateTimeOffset.UtcNow.AddMinutes(-10);
        var id = Guid.NewGuid();
        await setup.ReminderRepository.SaveAsync(new(id, null, "已结束提醒", string.Empty, new(ReminderTriggerKind.AbsoluteTime, trigger, null), ReminderStatus.Scheduled, booking.Booking!.Id, true));
        Assert.IsFalse((await setup.ReminderRepository.ListDueAsync(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow)).Any(x => x.Id == id));
    }

    [TestMethod]
    public async Task DisableForBooking_CancelsScheduledReminderWithoutDeletingIt()
    {
        using var setup = await SetupAsync();
        var id = Guid.NewGuid();
        await setup.ReminderRepository.SaveAsync(new(id, null, "排期提醒", string.Empty, new(ReminderTriggerKind.AbsoluteTime, DateTimeOffset.UtcNow.AddHours(1), null), ReminderStatus.Scheduled, setup.BookingId, true));
        await setup.ReminderRepository.DisableForBookingAsync(setup.BookingId);
        var saved = await setup.ReminderRepository.GetAsync(id);
        Assert.IsNotNull(saved);
        Assert.IsFalse(saved.IsEnabled);
        Assert.AreEqual(ReminderStatus.Cancelled, saved.Status);
    }

    [TestMethod]
    public void ReminderScheduler_RemainsDisabledAndIsNotStarted()
    {
        Assert.IsFalse(new DisabledLocalReminderScheduler().IsEnabled);
    }

    private static async Task<Setup> SetupAsync()
    {
        var temp = new TempDirectory();
        var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        var migration = await new DatabaseMigrator(database, new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync();
        Assert.IsTrue(migration.Success, migration.ErrorMessage);
        var bookingRepository = new SqliteShootBookingRepository(database);
        var bookingService = new ShootBookingService(bookingRepository, new BookingConflictDetector(bookingRepository));
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var saved = await bookingService.SaveAsync(new ShootBookingDraft { Title = "文档测试", ClientDisplayName = "客户", StartAt = start, EndAt = start.AddHours(2), TimeZoneId = TimeZoneInfo.Utc.Id, ShootingType = "Portrait" });
        var documentRepository = new SqliteBookingDocumentRepository(database);
        return new(temp, database, saved.Booking!.Id, start, bookingService, documentRepository, new BookingDocumentService(documentRepository), new SqliteReminderRepository(database));
    }

    private sealed class Setup : IDisposable
    {
        public Setup(TempDirectory temp, PixelTartDatabase database, Guid bookingId, DateTimeOffset bookingStart, ShootBookingService bookingService, SqliteBookingDocumentRepository documentRepository, BookingDocumentService documentService, SqliteReminderRepository reminderRepository)
        {
            Temp = temp; Database = database; BookingId = bookingId; BookingStart = bookingStart; BookingService = bookingService;
            DocumentRepository = documentRepository; DocumentService = documentService; ReminderRepository = reminderRepository;
        }
        public TempDirectory Temp { get; }
        public PixelTartDatabase Database { get; }
        public Guid BookingId { get; }
        public DateTimeOffset BookingStart { get; }
        public ShootBookingService BookingService { get; }
        public SqliteBookingDocumentRepository DocumentRepository { get; }
        public BookingDocumentService DocumentService { get; }
        public SqliteReminderRepository ReminderRepository { get; }
        public void Dispose() { SqliteTestIsolation.ClearPool(Database); Temp.Dispose(); }
    }
}
