using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;

if (args.Length != 2) throw new ArgumentException("app-data root and source document required");
var appDataRoot = Path.GetFullPath(args[0]);
var sourceDocument = Path.GetFullPath(args[1]);
Directory.CreateDirectory(Path.Combine(appDataRoot, "Data"));
var database = new PixelTartDatabase(Path.Combine(appDataRoot, "Data", "pixel-tart.db"));
var migration = await new DatabaseMigrator(database, new DatabaseBackupService(database, Path.Combine(appDataRoot, "Backups")), [new InitialSchemaMigration(), new CalendarSchemaMigration()]).MigrateAsync();
if (!migration.Success || migration.CurrentVersion != 2) throw new InvalidOperationException("Schema 2 seed migration failed.");

var project = new PhotoProjectRecord { Name = "2.2.0 隔离升级项目", Status = PhotoProjectStatus.Ready, UpdatedAt = DateTimeOffset.UtcNow };
await new SqliteProjectRepository(database).UpsertAsync(project);
await new SqliteQuickToolsRepository(database).SaveAsync(["Workflow", "PhotoOrganize", "BatchCompress"]);
var bookingRepository = new SqliteShootBookingRepository(database);
var bookings = new ShootBookingService(bookingRepository, new BookingConflictDetector(bookingRepository));
var start = DateTimeOffset.UtcNow.AddDays(2);
var bookingResult = await bookings.SaveAsync(new ShootBookingDraft { ProjectId = project.Id, Title = "2.2.0 隔离升级排期", ClientDisplayName = "隔离客户", StartAt = start, EndAt = start.AddHours(2), TimeZoneId = TimeZoneInfo.Utc.Id, ShootingType = "Portrait" });
var booking = bookingResult.Booking ?? throw new InvalidOperationException("Schema 2 booking seed failed.");
var file = new FileInfo(sourceDocument);
var document = new BookingDocumentRecord { BookingId = booking.Id, ProjectId = project.Id, DocumentType = BookingDocumentType.PhotographyPlan, DisplayName = "隔离升级文档", FilePath = sourceDocument, NormalizedPath = Path.GetFullPath(sourceDocument).ToUpperInvariant(), FileExtension = file.Extension.ToLowerInvariant(), FileSize = file.Length, LastKnownModifiedAtUtc = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero), LinkMode = BookingDocumentLinkMode.Reference, AddedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow };
await new SqliteBookingDocumentRepository(database).AddAsync(document);
Console.WriteLine(JsonSerializer.Serialize(new { Passed = true, SchemaVersion = migration.CurrentVersion, ProjectId = project.Id, BookingId = booking.Id, DocumentId = document.Id }));
