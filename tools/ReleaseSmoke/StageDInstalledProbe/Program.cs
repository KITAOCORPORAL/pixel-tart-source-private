using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FileOperations;
using RAWSelectionAssistant.Core.Services.Tasks;
using RAWSelectionAssistant.Core.Services.Tethering;

if (args.Length is < 1 or > 2) throw new ArgumentException("isolated stage-D root and optional UI database required");
var root = Path.GetFullPath(args[0]);
Directory.CreateDirectory(root);
var database = new PixelTartDatabase(Path.Combine(root, "data", "pixel-tart.db"));
var migration = await new DatabaseMigrator(database, new DatabaseBackupService(database, Path.Combine(root, "backups"))).MigrateAsync();
if (!migration.Success) throw new InvalidOperationException(migration.ErrorMessage);

var audit = new AuditLogService(database);
var notificationCenter = new NotificationCenter(database, TimeSpan.Zero);
var bookingRepository = new SqliteShootBookingRepository(database);
var bookings = new ShootBookingService(bookingRepository, new BookingConflictDetector(bookingRepository), audit);
var documents = new SqliteBookingDocumentRepository(database);
var reminders = new SqliteReminderRepository(database);
var reminderService = new BookingReminderService(reminders, bookings, audit);
var now = DateTimeOffset.UtcNow;
var bookingResult = await bookings.SaveAsync(new ShootBookingDraft
{
    Title = "隔离安装验收排期",
    ClientDisplayName = "验收客户",
    StartAt = now.AddHours(2),
    EndAt = now.AddHours(4),
    TimeZoneId = TimeZoneInfo.Utc.Id,
    ShootingType = "Portrait",
    Location = "验收地点",
    Requirements = [new() { ItemText = "电池", IsCompleted = true }, new() { ItemText = "灯架" }]
});
var booking = bookingResult.Booking ?? throw new InvalidOperationException("booking save failed");

var defaultReminder = await reminderService.SaveAsync(new ReminderDefinition(Guid.NewGuid(), null, string.Empty, string.Empty,
    new(ReminderTriggerKind.RelativeToBookingStart, null, TimeSpan.FromHours(1)), BookingId: booking.Id));
var reminderDefaultOff = !defaultReminder.IsEnabled && defaultReminder.Status == ReminderStatus.Disabled;
var reminderEnabled = await reminderService.SetEnabledAsync(defaultReminder.Id, true);
var dueReminder = new ReminderDefinition(Guid.NewGuid(), null, string.Empty, string.Empty,
    new(ReminderTriggerKind.AbsoluteTime, now.AddMinutes(-1), null), ReminderStatus.Scheduled, booking.Id, true);
await reminders.SaveAsync(dueReminder);
var scheduler = new BookingReminderScheduler(reminders, bookings, new BookingReminderNotificationService(notificationCenter, TimeZoneInfo.Utc), audit);
await scheduler.ProcessMissedAsync();
var reminderTriggeredOnce = (await notificationCenter.GetHistoryAsync()).Count(message => message.DeduplicationKey == $"booking-reminder:{dueReminder.Id:D}") == 1;
await scheduler.ProcessMissedAsync();
reminderTriggeredOnce &= (await notificationCenter.GetHistoryAsync()).Count(message => message.DeduplicationKey == $"booking-reminder:{dueReminder.Id:D}") == 1;
await scheduler.DisposeAsync();

var taskRepository = new SqliteTaskRepository(database);
var verification = new FileVerificationService();
var undoRepository = new SqliteUndoJournalRepository(database);
var planner = new FileOperationPlanner(new FileConflictResolver());
var executor = new FileOperationExecutor(new FileOperationValidator(), verification, undoRepository, database);
var undo = new UndoJournalService(undoRepository, verification);
var bridge = new TaskOperationBridge();
var engine = new TaskEngine(taskRepository, new ConservativeTaskScheduler(), [bridge], audit, notificationCenter, TimeSpan.Zero);
bridge.Attach(engine);
var projects = new SqliteProjectRepository(database);

var referenceSource = Path.Combine(root, "documents", "reference.txt");
Directory.CreateDirectory(Path.GetDirectoryName(referenceSource)!);
await File.WriteAllTextAsync(referenceSource, "reference");
var workflow = new BookingDocumentWorkflowService(documents, bookings, projects, planner, executor, verification, undo, bridge, audit, database);
var reference = await workflow.AddReferencesAsync(new(booking.Id, null, BookingDocumentType.Other, [referenceSource]));
var linked = reference.Successful == 1;
var referenceDocument = reference.Items.Single().Document!;
File.Delete(referenceSource);
var missing = (await workflow.VerifyAsync(referenceDocument.Id))?.State == BookingDocumentFileState.Missing;
var relocatedPath = Path.Combine(root, "documents", "relocated.txt");
await File.WriteAllTextAsync(relocatedPath, "reference");
var relocated = await workflow.RelocateAsync(referenceDocument.Id, relocatedPath);
var removed = await workflow.RemoveAssociationAsync(referenceDocument.Id);
var removeKeptFile = removed && File.Exists(relocatedPath);

var copySource = Path.Combine(root, "documents", "copy-source.pdf");
await File.WriteAllBytesAsync(copySource, [1, 2, 3, 4, 5]);
var copyRoot = Path.Combine(root, "managed");
var failingWorkflow = new BookingDocumentWorkflowService(new FailingDocumentRepository(documents), bookings, projects, planner, executor, verification, undo, bridge, audit, database);
var copy = await failingWorkflow.CopyAndAssociateAsync(new(booking.Id, null, BookingDocumentType.PhotographyPlan, [copySource], copyRoot, true));
var pending = copy.Items.Single().PendingAssociation ?? throw new InvalidOperationException("pending association was not created");
var copyKeptSource = File.Exists(copySource) && File.Exists(pending.DestinationPath);

var restartedDatabase = new PixelTartDatabase(database.DatabasePath);
var restartedBookingRepository = new SqliteShootBookingRepository(restartedDatabase);
var restartedBookings = new ShootBookingService(restartedBookingRepository, new BookingConflictDetector(restartedBookingRepository));
var restartedDocuments = new SqliteBookingDocumentRepository(restartedDatabase);
var restartedVerification = new FileVerificationService();
var restartedJournals = new SqliteUndoJournalRepository(restartedDatabase);
var restartedWorkflow = new BookingDocumentWorkflowService(restartedDocuments, restartedBookings, new SqliteProjectRepository(restartedDatabase),
    new FileOperationPlanner(new FileConflictResolver()), new FileOperationExecutor(new FileOperationValidator(), restartedVerification, restartedJournals, restartedDatabase),
    restartedVerification, new UndoJournalService(restartedJournals, restartedVerification), new TaskOperationBridge(), new AuditLogService(restartedDatabase), restartedDatabase);
var recovered = await restartedWorkflow.ListPendingAssociationsAsync(booking.Id);
var recoveryVisible = recovered.Count == 1 && recovered[0].TaskId == pending.TaskId;
var retry = await restartedWorkflow.RetryAssociationAsync(recovered.Single());
var recoveryRetried = retry.Succeeded && (await restartedWorkflow.ListPendingAssociationsAsync(booking.Id)).Count == 0;

var workbench = await new WorkbenchScheduleService(bookings, documents, reminders, projectRepository: projects).LoadAsync();
var workbenchVisible = workbench.Today.Any(item => item.BookingId == booking.Id && item.RequirementCompleted == 1 && item.RequirementTotal == 2);

var weatherState = new WeatherFeatureState();
weatherState.Apply(new WeatherSettings());
var weatherProvider = new ProbeWeatherProvider(now);
var weatherService = new WeatherForecastService(weatherProvider, new ProbeGeocoder(), new JsonWeatherCacheStore(Path.Combine(root, "weather-cache")), weatherState, notificationCenter, audit);
var weatherDisabled = (await weatherService.GetBookingWeatherAsync(booking.Id, booking.StartAtUtc, booking.EndAtUtc)).Availability == WeatherAvailability.Disabled && weatherProvider.Calls == 0;
weatherState.SetEnabled(true);
weatherService.ConfirmLocation(booking.Id, new("probe", "验收城市", "验收地区", "中国", 30, 120, "UTC", "Probe"));
var weather = await weatherService.GetBookingWeatherAsync(booking.Id, booking.StartAtUtc, booking.EndAtUtc, true);
var weatherAvailable = weather.RepresentativeHour is not null && weather.Day is not null && weatherProvider.Calls == 1;

var archived = await bookings.ArchiveAsync(booking.Id);
var archivedReminder = await reminders.GetAsync(defaultReminder.Id);
var restored = await bookings.RestoreAsync(booking.Id);
var restoredReminder = await reminders.GetAsync(defaultReminder.Id);
var archiveRestoreSafe = archived && restored && archivedReminder is { IsEnabled: false, Status: ReminderStatus.Disabled } && restoredReminder is { IsEnabled: false, Status: ReminderStatus.Disabled };

var tetherWatch = Path.Combine(root, "tether-watch");
Directory.CreateDirectory(tetherWatch);
var tetherJpg = Path.Combine(tetherWatch, "installed-pair.jpg");
var tetherRaw = Path.Combine(tetherWatch, "installed-pair.nef");
await File.WriteAllBytesAsync(tetherJpg, [1, 2, 3, 4]);
await File.WriteAllBytesAsync(tetherRaw, [0x49, 0x49, 0x2A, 0, 1, 2, 3, 4]);
var tetherHashes = new[] { tetherJpg, tetherRaw }.ToDictionary(path => path, path => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))));
var tetherSessions = new SqliteTetherSessionRepository(database);
var tetherAssets = new SqliteTetherAssetRepository(database);
var tetherSource = new ProbeWatchSource(tetherWatch);
var tetherAdapter = new WatchFolderCameraAdapter(tetherSessions, tetherAssets, new ImmediateStabilityProbe(), new TetherPairingService(tetherAssets), new ProbeProxyCache(), new ProbeTransfer(), audit, notificationCenter, _ => tetherSource);
var tetherSession = await tetherAdapter.StartAsync(new(tetherWatch, ImportExisting: true));
await tetherSession.ReconcileAsync();
var installedAssets = await tetherAssets.ListBySessionAsync(tetherSession.Session.Id);
var tetherPairing = installedAssets.Count == 2 && installedAssets.All(item => item.StabilityState == TetherStabilityState.Stable) && installedAssets.All(item => item.PairedAssetId.HasValue);
var tetherAnnotationRepository = new SqliteTetherAnnotationRepository(database);
var installedAnnotation = new TetherAnnotationRecord(Guid.NewGuid(), installedAssets[0].Id, 5, "绿", "隔离安装摄影师备注", now, now, true, "隔离安装客户备注");
await tetherAnnotationRepository.UpsertAsync(installedAnnotation);
var tetherAnnotationPersisted = (await tetherAnnotationRepository.GetByAssetAsync(installedAssets[0].Id)) is { Rating: 5, ClientFavorite: true, ClientNote: "隔离安装客户备注" };
var offlineWatch = tetherWatch + ".offline";
Directory.Move(tetherWatch, offlineWatch);
await tetherSession.ReconcileAsync();
var tetherDisconnected = tetherSession.Session.State == TetherSessionState.NeedsAttention;
Directory.Move(offlineWatch, tetherWatch);
await tetherSession.ReconcileAsync();
var tetherRecovered = tetherSession.Session.State == TetherSessionState.Running && (await tetherAssets.ListBySessionAsync(tetherSession.Session.Id)).Count == 2;
await tetherSession.DisposeAsync();
var tetherSourcesSafe = tetherHashes.All(pair => File.Exists(pair.Key) && pair.Value == Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(pair.Key))));

var lutParser = new CubeLutParser();
await using var oneDimensionalLut = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("LUT_1D_SIZE 2\n0 0 0\n1 1 1\n"));
await using var threeDimensionalLut = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("LUT_3D_SIZE 2\n0 0 0\n1 0 0\n0 1 0\n1 1 0\n0 0 1\n1 0 1\n0 1 1\n1 1 1\n"));
var parsed1D = await lutParser.ParseAsync(oneDimensionalLut);
var parsed3D = await lutParser.ParseAsync(threeDimensionalLut);
var lutInstalledProbe = parsed1D.Success && parsed3D.Success && parsed3D.Definition is not null && new CpuLutProcessor().Apply(parsed3D.Definition, new(.25f, .5f, .75f), .5f) is var transformed && float.IsFinite(transformed.Red);

await using var integrityConnection = await database.OpenConnectionAsync();
await using var integrityCommand = integrityConnection.CreateCommand();
integrityCommand.CommandText = "PRAGMA integrity_check;";
var integrity = string.Equals(Convert.ToString(await integrityCommand.ExecuteScalarAsync()), "ok", StringComparison.OrdinalIgnoreCase);
await using var versionCommand = integrityConnection.CreateCommand();
versionCommand.CommandText = "SELECT MAX(Version) FROM SchemaInfo;";
var schemaVersion = Convert.ToInt32(await versionCommand.ExecuteScalarAsync());

var uiReminderEnabled = true;
if (args.Length == 2)
{
    var uiDatabasePath = Path.GetFullPath(args[1]);
    var uiDatabase = new PixelTartDatabase(uiDatabasePath);
    await using var uiConnection = await uiDatabase.OpenConnectionAsync();
    await using var uiReminderCommand = uiConnection.CreateCommand();
    uiReminderCommand.CommandText = "SELECT COUNT(*) FROM BookingReminders WHERE IsEnabled = 1 AND Status = 'Scheduled';";
    uiReminderEnabled = Convert.ToInt64(await uiReminderCommand.ExecuteScalarAsync()) > 0;
}

var passed = migration.CurrentVersion == 3 && schemaVersion == 3 && reminderDefaultOff && reminderEnabled && reminderTriggeredOnce &&
    linked && missing && relocated.Status == BookingDocumentRelocationStatus.Relocated && removeKeptFile && copyKeptSource && recoveryVisible && recoveryRetried &&
    workbenchVisible && weatherDisabled && weatherAvailable && archiveRestoreSafe && integrity && uiReminderEnabled && tetherPairing && tetherAnnotationPersisted && tetherDisconnected && tetherRecovered && tetherSourcesSafe && lutInstalledProbe;
Console.WriteLine(JsonSerializer.Serialize(new
{
    Passed = passed,
    SchemaVersion = schemaVersion,
    ReminderDefaultOff = reminderDefaultOff,
    ReminderEnabled = reminderEnabled,
    ReminderTriggeredOnce = reminderTriggeredOnce,
    UiReminderEnabled = uiReminderEnabled,
    WorkbenchVisible = workbenchVisible,
    DocumentReferenceAdded = linked,
    MissingDetected = missing,
    Relocated = relocated.Status == BookingDocumentRelocationStatus.Relocated,
    RemoveAssociationKeptFile = removeKeptFile,
    CopyKeptSource = copyKeptSource,
    RecoveryVisibleAfterRestart = recoveryVisible,
    RecoveryRetried = recoveryRetried,
    WeatherDefaultOff = weatherDisabled,
    WeatherAvailableWithProbe = weatherAvailable,
    ArchiveRestoreSafe = archiveRestoreSafe,
    TetherPairing = tetherPairing,
    TetherAnnotationPersisted = tetherAnnotationPersisted,
    TetherDirectoryDisconnected = tetherDisconnected,
    TetherDirectoryRecovered = tetherRecovered,
    TetherSourcesSafe = tetherSourcesSafe,
    LutInstalledProbe = lutInstalledProbe,
    IntegrityCheck = integrity ? "ok" : "failed"
}));

sealed class ProbeWatchSource(string directory) : IWatchFolderEventSource
{
    public event EventHandler<WatchFolderEvent>? EventReceived { add { } remove { } }
    public string Directory { get; } = directory;
    public bool IncludeSubdirectories => false;
    public void Start() { }
    public void Stop() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

sealed class ImmediateStabilityProbe : IFileStabilityProbe
{
    public Task<FileStabilityResult> WaitForStableAsync(string path, CancellationToken cancellationToken = default)
    {
        var file = new FileInfo(path);
        return Task.FromResult(new FileStabilityResult(TetherStabilityState.Stable, file.Length, new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero)));
    }
}

sealed class ProbeProxyCache : ITetherProxyCache
{
    public Task<string?> GetOrCreateAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default) => Task.FromResult<string?>("installed-proxy");
    public string? ResolvePath(string? cacheKey) => null;
    public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed class ProbeTransfer : ICameraTransferService
{
    public Task<TetherCopyResult> CopyToProjectAsync(TetherAssetRecord asset, string destinationRoot, bool verifySha256, CancellationToken cancellationToken = default) => Task.FromResult(new TetherCopyResult(asset.Id, Guid.NewGuid(), Path.Combine(destinationRoot, asset.FileName), TetherProcessingState.Copied));
    public Task<TetherCopyResult> CopyToBackupAsync(TetherAssetRecord asset, string destinationRoot, bool verifySha256, CancellationToken cancellationToken = default) => Task.FromResult(new TetherCopyResult(asset.Id, Guid.NewGuid(), Path.Combine(destinationRoot, asset.FileName), TetherProcessingState.Copied));
}

sealed class FailingDocumentRepository(IBookingDocumentRepository inner) : IBookingDocumentRepository
{
    public Task AddAsync(BookingDocumentRecord document, CancellationToken cancellationToken = default) => throw new IOException("forced association failure");
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

sealed class ProbeWeatherProvider(DateTimeOffset now) : IWeatherProvider
{
    public string Name => "Probe";
    public int MaximumForecastDays => 16;
    public int Calls { get; private set; }
    public Task<WeatherProviderForecast> GetForecastAsync(WeatherLocation location, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        Calls++;
        var at = now.AddHours(3);
        return Task.FromResult(new WeatherProviderForecast(location, now,
            [new(at, "1", 23, 24, 20, 0, 10, 15, 60, 25, 10_000)],
            [new(fromDate, "1", 18, 28, 20, at.AddHours(-8), at.AddHours(8))], Name));
    }
}

sealed class ProbeGeocoder : IGeocodingProvider
{
    public string Name => "Probe";
    public Task<IReadOnlyList<WeatherLocationCandidate>> SearchAsync(string query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WeatherLocationCandidate>>([]);
}
