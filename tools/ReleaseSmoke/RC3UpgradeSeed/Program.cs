using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;

if (args.Length != 3) throw new ArgumentException("app-data root, source document and watch directory required");
var appDataRoot = Path.GetFullPath(args[0]);
var sourceDocument = Path.GetFullPath(args[1]);
var watchDirectory = Path.GetFullPath(args[2]);
Directory.CreateDirectory(Path.Combine(appDataRoot, "Data"));
Directory.CreateDirectory(watchDirectory);
var database = new PixelTartDatabase(Path.Combine(appDataRoot, "Data", "pixel-tart.db"));
var migration = await new DatabaseMigrator(
    database,
    new DatabaseBackupService(database, Path.Combine(appDataRoot, "Backups")),
    [new InitialSchemaMigration(), new CalendarSchemaMigration(), new TetherSchemaMigration()]).MigrateAsync();
if (!migration.Success || migration.CurrentVersion != 3) throw new InvalidOperationException("Schema 3 RC2 seed migration failed.");

var project = new PhotoProjectRecord { Name = "RC2 隔离升级项目", Status = PhotoProjectStatus.Ready, UpdatedAt = DateTimeOffset.UtcNow };
await new SqliteProjectRepository(database).UpsertAsync(project);
await new SqliteQuickToolsRepository(database).SaveAsync(["Workflow", "PhotoOrganize", "BatchCompress"]);
var bookingRepository = new SqliteShootBookingRepository(database);
var bookingService = new ShootBookingService(bookingRepository, new BookingConflictDetector(bookingRepository));
var start = DateTimeOffset.UtcNow.AddDays(2);
var bookingResult = await bookingService.SaveAsync(new ShootBookingDraft
{
    ProjectId = project.Id,
    Title = "RC2 隔离升级排期",
    ClientDisplayName = "隔离客户",
    StartAt = start,
    EndAt = start.AddHours(2),
    TimeZoneId = TimeZoneInfo.Utc.Id,
    ShootingType = "Portrait"
});
var booking = bookingResult.Booking ?? throw new InvalidOperationException("Schema 3 booking seed failed.");
var file = new FileInfo(sourceDocument);
var document = new BookingDocumentRecord
{
    BookingId = booking.Id,
    ProjectId = project.Id,
    DocumentType = BookingDocumentType.PhotographyPlan,
    DisplayName = "隔离升级文档",
    FilePath = sourceDocument,
    NormalizedPath = Path.GetFullPath(sourceDocument).ToUpperInvariant(),
    FileExtension = file.Extension.ToLowerInvariant(),
    FileSize = file.Length,
    LastKnownModifiedAtUtc = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
    LinkMode = BookingDocumentLinkMode.Reference,
    AddedAtUtc = DateTimeOffset.UtcNow,
    UpdatedAtUtc = DateTimeOffset.UtcNow
};
await new SqliteBookingDocumentRepository(database).AddAsync(document);
var reminder = new ReminderDefinition(
    Guid.NewGuid(), project.Id, "隔离升级提醒", string.Empty,
    new ReminderTrigger(ReminderTriggerKind.RelativeToBookingStart, null, TimeSpan.FromHours(1)),
    ReminderStatus.Scheduled, booking.Id, true);
await new SqliteReminderRepository(database).SaveAsync(reminder);
var now = DateTimeOffset.UtcNow;
var tether = new TetherSessionRecord(
    Guid.NewGuid(), project.Id, CameraProviderType.WatchFolder, watchDirectory, watchDirectory.ToUpperInvariant(),
    TetherSessionState.Stopped, now, now, false, false, null, false, null, now, now, null, now, now);
await new SqliteTetherSessionRepository(database).AddAsync(tether);

Console.WriteLine(JsonSerializer.Serialize(new
{
    Passed = true,
    SchemaVersion = migration.CurrentVersion,
    ProjectId = project.Id,
    BookingId = booking.Id,
    DocumentId = document.Id,
    ReminderId = reminder.Id,
    TetherSessionId = tether.Id
}));
