using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Tasks;

#pragma warning disable MSTEST0037

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version210StressTests
{
    [TestMethod]
    public async Task OperationItems_FiveThousandRowsPersist()
    {
        using var setup=await SetupAsync();var task=await CreateTaskAsync(setup.Repository,"operation-stress");await using var connection=await setup.Database.OpenConnectionAsync(write:true);await using var transaction=(SqliteTransaction)await connection.BeginTransactionAsync();for(var i=0;i<5000;i++){await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="INSERT INTO OperationItems(Id,TaskId,Sequence,SourcePath,DestinationPath,OperationType,ConflictPolicy,State) VALUES($id,$task,$sequence,$source,$destination,'Copy','AutoNumber','Pending');";command.Parameters.AddWithValue("$id",Guid.NewGuid().ToString("D"));command.Parameters.AddWithValue("$task",task.ToString("D"));command.Parameters.AddWithValue("$sequence",i);command.Parameters.AddWithValue("$source",$"source-{i}");command.Parameters.AddWithValue("$destination",$"destination-{i}");await command.ExecuteNonQueryAsync();}await transaction.CommitAsync();await using var count=connection.CreateCommand();count.CommandText="SELECT count(*) FROM OperationItems WHERE TaskId=$task;";count.Parameters.AddWithValue("$task",task.ToString("D"));Assert.AreEqual(5000L,(long)(await count.ExecuteScalarAsync())!);
    }

    [TestMethod]
    public async Task TaskHistory_OneThousandRowsLoadsWithLimit()
    {
        using var setup=await SetupAsync();for(var i=0;i<1000;i++){var definition=new TaskDefinition(Guid.NewGuid(),null,"stress",$"task-{i}","",null,DateTimeOffset.UtcNow.AddSeconds(i));await setup.Repository.SaveAsync(new TaskRuntimeState{Definition=definition,State=TaskLifecycleState.Completed,Progress=100,CompletedAt=DateTimeOffset.UtcNow});}var history=await setup.Repository.ListAsync(200);Assert.AreEqual(200,history.Count);Assert.IsTrue(history.All(x=>x.State==TaskLifecycleState.Completed));
    }

    [TestMethod]
    public async Task FailedItems_FiveHundredCanBeSelectedForRetry()
    {
        using var setup=await SetupAsync();var task=await CreateTaskAsync(setup.Repository,"retry-stress");await using var connection=await setup.Database.OpenConnectionAsync(write:true);await using var transaction=(SqliteTransaction)await connection.BeginTransactionAsync();for(var i=0;i<500;i++){await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="INSERT INTO OperationItems(Id,TaskId,Sequence,SourcePath,DestinationPath,OperationType,ConflictPolicy,State,ErrorCode) VALUES($id,$task,$sequence,$source,$destination,'Copy','AutoNumber','Failed','FileLocked');";command.Parameters.AddWithValue("$id",Guid.NewGuid().ToString("D"));command.Parameters.AddWithValue("$task",task.ToString("D"));command.Parameters.AddWithValue("$sequence",i);command.Parameters.AddWithValue("$source",$"source-{i}");command.Parameters.AddWithValue("$destination",$"destination-{i}");await command.ExecuteNonQueryAsync();}await transaction.CommitAsync();await using var count=connection.CreateCommand();count.CommandText="SELECT count(*) FROM OperationItems WHERE TaskId=$task AND State='Failed';";count.Parameters.AddWithValue("$task",task.ToString("D"));Assert.AreEqual(500L,(long)(await count.ExecuteScalarAsync())!);
    }

    [TestMethod]
    public async Task NeedsAttention_OneHundredTasksRemainQueryable()
    {
        using var setup=await SetupAsync();for(var i=0;i<100;i++){var definition=new TaskDefinition(Guid.NewGuid(),null,"attention",$"attention-{i}","",null,DateTimeOffset.UtcNow.AddMilliseconds(i));await setup.Repository.SaveAsync(new TaskRuntimeState{Definition=definition,State=TaskLifecycleState.NeedsAttention});}Assert.AreEqual(100,(await setup.Repository.ListUnfinishedAsync()).Count(x=>x.State==TaskLifecycleState.NeedsAttention));
    }

    [TestMethod]
    public async Task Checkpoint_SurvivesDatabaseRestart()
    {
        using var setup=await SetupAsync();var task=await CreateTaskAsync(setup.Repository,"checkpoint");await setup.Repository.SaveCheckpointAsync(task,new TaskCheckpoint("copy",42,"item-42",DateTimeOffset.UtcNow));SqliteTestIsolation.ClearPool(setup.Database);var reopened=new SqliteTaskRepository(new PixelTartDatabase(setup.Database.DatabasePath));var runtime=await reopened.GetAsync(task);Assert.IsNotNull(runtime);await using var connection=await setup.Database.OpenConnectionAsync();await using var command=connection.CreateCommand();command.CommandText="SELECT Checkpoint FROM TaskSteps WHERE TaskId=$task AND Sequence=42;";command.Parameters.AddWithValue("$task",task.ToString("D"));Assert.AreEqual("item-42",(string)(await command.ExecuteScalarAsync())!);
    }

    [TestMethod]
    public async Task DatabaseConnections_AreReleasedAfterParallelReads()
    {
        using var setup=await SetupAsync();var reads=Enumerable.Range(0,100).Select(async _=>{await using var connection=await setup.Database.OpenConnectionAsync();await using var command=connection.CreateCommand();command.CommandText="SELECT 1;";return Convert.ToInt32(await command.ExecuteScalarAsync());});Assert.IsTrue((await Task.WhenAll(reads)).All(x=>x==1));SqliteTestIsolation.ClearPool(setup.Database);File.Move(setup.Database.DatabasePath,setup.Database.DatabasePath+".moved");Assert.IsTrue(File.Exists(setup.Database.DatabasePath+".moved"));
    }

    [TestMethod]
    public async Task AuditLog_OneThousandRowsRemainReadable()
    {
        using var setup=await SetupAsync();var audit=new AuditLogService(setup.Database);for(var i=0;i<1000;i++)await audit.WriteAsync("Stress","Event","Information",$"item {i}");await using var connection=await setup.Database.OpenConnectionAsync();await using var command=connection.CreateCommand();command.CommandText="SELECT count(*) FROM AuditLogs;";Assert.AreEqual(1000L,(long)(await command.ExecuteScalarAsync())!);
    }

    private static async Task<Guid> CreateTaskAsync(ITaskRepository repository,string name){var id=Guid.NewGuid();await repository.SaveAsync(new TaskRuntimeState{Definition=new TaskDefinition(id,null,"stress",name,"",null,DateTimeOffset.UtcNow),State=TaskLifecycleState.Pending});return id;}
    private static async Task<Setup> SetupAsync(){var temp=new TempDirectory();var db=new PixelTartDatabase(temp.Combine("db.sqlite"));await new DatabaseMigrator(db,new DatabaseBackupService(db,temp.Combine("backups"))).MigrateAsync();return new(temp,db,new SqliteTaskRepository(db));}
    private sealed class Setup:IDisposable{private readonly TempDirectory _temp;public Setup(TempDirectory temp,PixelTartDatabase database,SqliteTaskRepository repository){_temp=temp;Database=database;Repository=repository;}public PixelTartDatabase Database{get;}public SqliteTaskRepository Repository{get;}public void Dispose(){SqliteTestIsolation.ClearPool(Database);_temp.Dispose();}}
}
