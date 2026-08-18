using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FileOperations;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version210RecoveryCoordinatorTests
{
    [TestMethod]
    public async Task InterruptedCopy_ContinuesOnlyPendingItems()
    {
        using var setup=await SetupAsync();var source=setup.Temp.CreateFile("source/a.jpg",[1,2,3]);var task=await setup.CreateTaskAsync(TaskLifecycleState.Interrupted);await setup.InsertItemAsync(task,source,setup.Temp.Combine("out","a.jpg"),FileOperationType.Copy,"Pending");Assert.IsTrue(await setup.Recovery.ContinueAsync(task));Assert.IsTrue(File.Exists(setup.Temp.Combine("out","a.jpg")));Assert.AreEqual(TaskLifecycleState.Completed,(await setup.Repository.GetAsync(task))!.State);
    }

    [TestMethod]
    public async Task InterruptedMove_RequiresExplicitHighRiskConfirmation()
    {
        using var setup=await SetupAsync();var source=setup.Temp.CreateFile("source/a.raw",[1,2,3]);var task=await setup.CreateTaskAsync(TaskLifecycleState.Interrupted);await setup.InsertItemAsync(task,source,setup.Temp.Combine("out","a.raw"),FileOperationType.Move,"Pending");Assert.IsFalse(await setup.Recovery.ContinueAsync(task,false));Assert.IsTrue(File.Exists(source));Assert.AreEqual(TaskLifecycleState.NeedsAttention,(await setup.Repository.GetAsync(task))!.State);
    }

    [TestMethod]
    public async Task AbandonInterruptedTask_PreservesFilesAndCancelsRecord()
    {
        using var setup=await SetupAsync();var source=setup.Temp.CreateFile("source/a.jpg",[1]);var task=await setup.CreateTaskAsync(TaskLifecycleState.Interrupted);await setup.Recovery.AbandonAsync(task);Assert.IsTrue(File.Exists(source));Assert.AreEqual(TaskLifecycleState.Cancelled,(await setup.Repository.GetAsync(task))!.State);
    }

    [TestMethod]
    public async Task RollbackSafeOutputs_UsesUndoJournalPreconditions()
    {
        using var setup=await SetupAsync();var source=setup.Temp.CreateFile("source/a.jpg",[1,2]);var task=Guid.NewGuid();var plan=await setup.Planner.CreateAsync(task,null,FileOperationType.Copy,setup.Temp.Combine("source"),setup.Temp.Combine("out"),[source]);var execution=await setup.Executor.ExecuteAsync(plan);var output=execution.Items.Single().DestinationPath!;var summary=await setup.Recovery.RollbackSafeOutputsAsync(task);Assert.AreEqual(1,summary.Succeeded);Assert.IsFalse(File.Exists(output));Assert.IsTrue(File.Exists(source));
    }

    private static async Task<Setup> SetupAsync(){var temp=new TempDirectory();var db=new PixelTartDatabase(temp.Combine("db.sqlite"));await new DatabaseMigrator(db,new DatabaseBackupService(db,temp.Combine("backups"))).MigrateAsync();var repository=new SqliteTaskRepository(db);var verification=new FileVerificationService();var journals=new SqliteUndoJournalRepository(db);var executor=new FileOperationExecutor(new FileOperationValidator(),verification,journals,db);var undo=new UndoJournalService(journals,verification);var recovery=new RecoveryCoordinator(db,repository,executor,undo,new AuditLogService(db));return new(temp,db,repository,new FileOperationPlanner(new FileConflictResolver()),executor,recovery);}

    private sealed class Setup:IDisposable
    {
        public Setup(TempDirectory temp,PixelTartDatabase database,SqliteTaskRepository repository,FileOperationPlanner planner,FileOperationExecutor executor,RecoveryCoordinator recovery){Temp=temp;Database=database;Repository=repository;Planner=planner;Executor=executor;Recovery=recovery;}
        public TempDirectory Temp{get;}public PixelTartDatabase Database{get;}public SqliteTaskRepository Repository{get;}public FileOperationPlanner Planner{get;}public FileOperationExecutor Executor{get;}public RecoveryCoordinator Recovery{get;}
        public async Task<Guid> CreateTaskAsync(TaskLifecycleState state){var id=Guid.NewGuid();await Repository.SaveAsync(new TaskRuntimeState{Definition=new TaskDefinition(id,null,"FileOperation","recovery","",Guid.NewGuid(),DateTimeOffset.UtcNow),State=state});return id;}
        public async Task InsertItemAsync(Guid task,string source,string destination,FileOperationType operation,string state){var info=new FileInfo(source);await using var connection=await Database.OpenConnectionAsync(write:true);await using var command=connection.CreateCommand();command.CommandText="INSERT INTO OperationItems(Id,TaskId,Sequence,SourcePath,DestinationPath,OperationType,ConflictPolicy,ExpectedSourceSize,ExpectedSourceModifiedAt,State) VALUES($id,$task,0,$source,$destination,$operation,'AutoNumber',$size,$modified,$state);";command.Parameters.AddWithValue("$id",Guid.NewGuid().ToString("D"));command.Parameters.AddWithValue("$task",task.ToString("D"));command.Parameters.AddWithValue("$source",source);command.Parameters.AddWithValue("$destination",destination);command.Parameters.AddWithValue("$operation",operation.ToString());command.Parameters.AddWithValue("$size",info.Length);command.Parameters.AddWithValue("$modified",new DateTimeOffset(info.LastWriteTimeUtc).ToString("O"));command.Parameters.AddWithValue("$state",state);await command.ExecuteNonQueryAsync();}
        public void Dispose(){SqliteTestIsolation.ClearPool(Database);Temp.Dispose();}
    }
}

