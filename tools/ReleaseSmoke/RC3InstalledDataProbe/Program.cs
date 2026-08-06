using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Business;
using RAWSelectionAssistant.Core.Services.Database;

if (args.Length != 4) throw new ArgumentException("mode, database, app-data root and source file required");
var mode = args[0];
var databasePath = Path.GetFullPath(args[1]);
var appDataRoot = Path.GetFullPath(args[2]);
var sourceFile = Path.GetFullPath(args[3]);
var database = new PixelTartDatabase(databasePath);
await using var connection = await database.OpenConnectionAsync();
await using var versionCommand = connection.CreateCommand();
versionCommand.CommandText = "SELECT MAX(Version) FROM SchemaInfo;";
var schemaVersion = Convert.ToInt32(await versionCommand.ExecuteScalarAsync());
await using var integrityCommand = connection.CreateCommand();
integrityCommand.CommandText = "PRAGMA integrity_check;";
var integrity = Convert.ToString(await integrityCommand.ExecuteScalarAsync()) ?? string.Empty;
async Task<long> Scalar(string sql)
{
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    return Convert.ToInt64(await command.ExecuteScalarAsync());
}
var newTableCount = await Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('BookingContacts','BookingStaffMembers','FinanceCategories','FinanceTransactions');");
var categoryCount = await Scalar("SELECT COUNT(*) FROM FinanceCategories;");
var projects = await new SqliteProjectRepository(database).ListAsync();
var project = projects.FirstOrDefault();
if (project is null)
{
    project = new PhotoProjectRecord { Name = "RC3 隔离全新安装项目", Status = PhotoProjectStatus.Ready, UpdatedAt = DateTimeOffset.UtcNow };
    await new SqliteProjectRepository(database).UpsertAsync(project);
}
var bookingRepository = new SqliteShootBookingRepository(database);
var bookings = await bookingRepository.SearchAllUnarchivedAsync(new ShootBookingSearchRequest(PageSize: 50));
var bookingSummary = bookings.Items.FirstOrDefault();
Guid bookingId;
if (bookingSummary is null)
{
    var start = DateTimeOffset.UtcNow.AddDays(1);
    var saved = await new ShootBookingService(bookingRepository, new BookingConflictDetector(bookingRepository)).SaveAsync(new ShootBookingDraft
    {
        ProjectId = project.Id,
        Title = "RC3 隔离全新安装排期",
        ClientDisplayName = "隔离联系人",
        StartAt = start,
        EndAt = start.AddHours(1),
        TimeZoneId = TimeZoneInfo.Utc.Id,
        ShootingType = "Portrait"
    });
    var booking = saved.Booking ?? throw new InvalidOperationException("Fresh booking creation failed.");
    bookingId = booking.Id;
    var file = new FileInfo(sourceFile);
    await new SqliteBookingDocumentRepository(database).AddAsync(new BookingDocumentRecord
    {
        BookingId = bookingId,
        ProjectId = project.Id,
        DocumentType = BookingDocumentType.PhotographyPlan,
        DisplayName = "隔离资料",
        FilePath = sourceFile,
        NormalizedPath = sourceFile.ToUpperInvariant(),
        FileExtension = file.Extension.ToLowerInvariant(),
        FileSize = file.Length,
        LastKnownModifiedAtUtc = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
        LinkMode = BookingDocumentLinkMode.Reference,
        AddedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow
    });
}
else bookingId = bookingSummary.Id;

var people = new BookingPeopleService(new SqliteBookingPeopleRepository(database));
await people.SaveAsync(bookingId,
    [new BookingContact { BookingId=bookingId, DisplayName="隔离主要联系人", IsPrimary=true }],
    [new BookingStaffMember { BookingId=bookingId, DisplayName="隔离摄影师", Role=BookingStaffRole.Photographer }]);
var contacts = await people.ListContactsAsync(bookingId);
var staff = await people.ListStaffAsync(bookingId);
var finance = new FinanceService(new SqliteFinanceRepository(database));
var categories = await finance.ListCategoriesAsync();
var incomeCategory = categories.First(item => item.Kind == FinanceTransactionKind.Income);
var expenseCategory = categories.First(item => item.Kind == FinanceTransactionKind.Expense);
await finance.SaveAsync(new FinanceTransaction { Kind=FinanceTransactionKind.Income,CategoryId=incomeCategory.Id,AmountMinor=120000,PaymentStatus=FinancePaymentStatus.Received,BookingId=bookingId,ProjectId=project.Id });
await finance.SaveAsync(new FinanceTransaction { Kind=FinanceTransactionKind.Expense,CategoryId=expenseCategory.Id,AmountMinor=30000,PaymentStatus=FinancePaymentStatus.Paid,BookingId=bookingId,ProjectId=project.Id });
var financeItems = await finance.QueryAsync(new FinanceQuery(ProjectId: project.Id));
var summary = await finance.SummarizeAsync(new FinanceQuery(ProjectId: project.Id));
var documentCount = await Scalar("SELECT COUNT(*) FROM BookingDocuments;");
var reminderCount = await Scalar("SELECT COUNT(*) FROM BookingReminders;");
var tetherCount = await Scalar("SELECT COUNT(*) FROM TetherSessions;");
var settings = File.Exists(Path.Combine(appDataRoot, "settings.json"));
var backups = Directory.Exists(Path.Combine(appDataRoot, "Backups")) ? Directory.GetFiles(Path.Combine(appDataRoot, "Backups"), "*", SearchOption.AllDirectories).Length : 0;
var upgradeDataRetained = mode != "upgrade" || (documentCount > 0 && reminderCount > 0 && tetherCount > 0 && backups > 0);
var passed = schemaVersion == 4 && string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase) && newTableCount == 4 && categoryCount >= 22 && contacts.Count == 1 && staff.Count == 1 && financeItems.Count >= 2 && summary.NetCashFlowMinor == 90000 && settings && File.Exists(sourceFile) && documentCount > 0 && upgradeDataRetained;
Console.WriteLine(JsonSerializer.Serialize(new
{
    Passed=passed,
    Mode=mode,
    SchemaVersion=schemaVersion,
    IntegrityCheck=integrity,
    NewBusinessTableCount=newTableCount,
    FinanceCategoryCount=categoryCount,
    ProjectCount=projects.Count + (projects.Count == 0 ? 1 : 0),
    BookingId=bookingId,
    ContactCount=contacts.Count,
    StaffCount=staff.Count,
    FinanceTransactionCount=financeItems.Count,
    NetCashFlowMinor=summary.NetCashFlowMinor,
    BookingDocumentCount=documentCount,
    ReminderCount=reminderCount,
    TetherSessionCount=tetherCount,
    SettingsRetained=settings,
    MigrationBackupCount=backups,
    SourceSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(sourceFile)))
}));
if (!passed) Environment.ExitCode = 1;
