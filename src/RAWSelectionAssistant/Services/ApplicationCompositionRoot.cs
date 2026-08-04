using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FileOperations;
using RAWSelectionAssistant.Core.Services.Tasks;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Services;

public sealed class ApplicationCompositionRoot
{
    private ApplicationCompositionRoot(
        PixelTartDatabase database,
        MigrationResult migration,
        JsonMigrationReport? jsonMigration,
        TaskOperationBridge operationBridge,
        TaskEngine taskEngine,
        TaskCenterViewModel taskCenter,
        NotificationCenter notificationCenter,
        IAuditLogService auditLog,
        IFileOperationPlanner fileOperationPlanner,
        IFileOperationExecutor fileOperationExecutor,
        IFileVerificationService fileVerificationService,
        IUndoJournalService undoJournalService)
    {
        Database = database;
        Migration = migration;
        JsonMigration = jsonMigration;
        OperationBridge = operationBridge;
        TaskEngine = taskEngine;
        TaskCenter = taskCenter;
        NotificationCenter = notificationCenter;
        AuditLog = auditLog;
        FileOperationPlanner = fileOperationPlanner;
        FileOperationExecutor = fileOperationExecutor;
        FileVerificationService = fileVerificationService;
        UndoJournalService = undoJournalService;
        ProjectRepository = new SqliteProjectRepository(database);
        MediaIndexRepository = new SqliteMediaIndexRepository(database);
        QuickToolsRepository = new SqliteQuickToolsRepository(database);
        MatchDecisionRepository = new SqliteMatchDecisionRepository(database);
        ShootBookingRepository = new SqliteShootBookingRepository(database);
        BookingConflictDetector = new BookingConflictDetector(ShootBookingRepository);
        ShootBookingService = new ShootBookingService(ShootBookingRepository, BookingConflictDetector, auditLog);
        BookingDocumentRepository = new SqliteBookingDocumentRepository(database);
        BookingDocumentService = new BookingDocumentService(BookingDocumentRepository);
        BookingDocumentWorkflowService = new BookingDocumentWorkflowService(BookingDocumentRepository, ShootBookingService, ProjectRepository,
            FileOperationPlanner, FileOperationExecutor, FileVerificationService, UndoJournalService, OperationBridge, AuditLog);
        ReminderRepository = new SqliteReminderRepository(database);
    }

    public PixelTartDatabase Database { get; }
    public MigrationResult Migration { get; }
    public JsonMigrationReport? JsonMigration { get; }
    public TaskOperationBridge OperationBridge { get; }
    public TaskEngine TaskEngine { get; }
    public TaskCenterViewModel TaskCenter { get; }
    public NotificationCenter NotificationCenter { get; }
    public IAuditLogService AuditLog { get; }
    public IFileOperationPlanner FileOperationPlanner { get; }
    public IFileOperationExecutor FileOperationExecutor { get; }
    public IFileVerificationService FileVerificationService { get; }
    public IUndoJournalService UndoJournalService { get; }
    public IProjectRepository ProjectRepository { get; }
    public IMediaIndexRepository MediaIndexRepository { get; }
    public IQuickToolsRepository QuickToolsRepository { get; }
    public IMatchDecisionRepository MatchDecisionRepository { get; }
    public IShootBookingRepository ShootBookingRepository { get; }
    public IBookingConflictDetector BookingConflictDetector { get; }
    public IShootBookingService ShootBookingService { get; }
    public IBookingDocumentRepository BookingDocumentRepository { get; }
    public IBookingDocumentService BookingDocumentService { get; }
    public IBookingDocumentWorkflowService BookingDocumentWorkflowService { get; }
    public IReminderRepository ReminderRepository { get; }

    public static async Task<ApplicationCompositionRoot> CreateAsync(CancellationToken cancellationToken = default)
    {
        var database = new PixelTartDatabase();
        var backup = new DatabaseBackupService(database, RAWSelectionAssistant.Core.Utilities.AppDataPaths.MigrationBackupDirectory);
        var migrator = new DatabaseMigrator(database, backup);
        var migration = await migrator.MigrateAsync(cancellationToken);
        if (!migration.Success)
            throw new InvalidOperationException($"{migration.ErrorCode}: {migration.ErrorMessage} 数据库原文件未被覆盖，备份路径：{migration.BackupPath ?? "无"}");

        var jsonMigration = await new JsonDataMigrationService(database).MigrateAsync(cancellationToken);
        var audit = new AuditLogService(database);
        var notifications = new NotificationCenter(database);
        var repository = new SqliteTaskRepository(database);
        var verification = new FileVerificationService();
        var undoRepository = new SqliteUndoJournalRepository(database);
        var planner = new FileOperationPlanner(new FileConflictResolver());
        var executor = new FileOperationExecutor(new FileOperationValidator(), verification, undoRepository, database);
        var undo = new UndoJournalService(undoRepository, verification);
        var bridge = new TaskOperationBridge();
        var engine = new TaskEngine(repository, new ConservativeTaskScheduler(), [bridge], audit, notifications);
        bridge.Attach(engine);
        await new TaskRecoveryService(repository, audit).RecoverInterruptedAsync(cancellationToken);
        var recovery = new RecoveryCoordinator(database, repository, executor, undo, audit);
        var taskCenter = new TaskCenterViewModel(engine, recovery);
        await taskCenter.InitializeAsync(cancellationToken);
        return new(database, migration, jsonMigration, bridge, engine, taskCenter, notifications, audit, planner, executor, verification, undo);
    }
}
