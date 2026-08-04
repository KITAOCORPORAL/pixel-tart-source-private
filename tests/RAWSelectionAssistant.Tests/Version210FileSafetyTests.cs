using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FileOperations;
using Core = RAWSelectionAssistant.Core;

#pragma warning disable CS9124

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version210FileSafetyTests
{
    [TestMethod]
    public async Task Planner_DefaultsToCopyAndAutoNumber()
    {
        using var temp=new TempDirectory();var source=temp.CreateFile("source/a.jpg",[1,2,3]);Directory.CreateDirectory(temp.Combine("out"));File.WriteAllBytes(temp.Combine("out","a.jpg"),[9]);var plan=await new FileOperationPlanner(new FileConflictResolver()).CreateAsync(Guid.NewGuid(),null,FileOperationType.Copy,temp.Combine("source"),temp.Combine("out"),[source]);Assert.AreEqual(FileConflictPolicy.AutoNumber,plan.ConflictPolicy);Assert.AreEqual("a (1).jpg",Path.GetFileName(plan.Items[0].DestinationPath));
    }

    [TestMethod]
    public async Task Planner_PreservesRelativeFolders()
    {
        using var temp=new TempDirectory();var source=temp.CreateFile("source/sub/a.raw",[1]);var plan=await new FileOperationPlanner(new FileConflictResolver()).CreateAsync(Guid.NewGuid(),null,FileOperationType.Copy,temp.Combine("source"),temp.Combine("out"),[source]);Assert.IsTrue(plan.Items[0].DestinationPath.EndsWith(Path.Combine("sub","a.raw"),StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task Validator_BlocksSameSourceAndDestination()
    {
        using var temp=new TempDirectory();var source=temp.CreateFile("source/a.jpg",[1]);var plan=await new FileOperationPlanner(new FileConflictResolver()).CreateAsync(Guid.NewGuid(),null,FileOperationType.Copy,temp.Combine("source"),temp.Combine("source"),[source]);var result=await new FileOperationValidator().ValidateAsync(plan);Assert.IsTrue(result.Issues.Any(x=>x.ErrorCode==Core.Services.ErrorCodeCatalog.SourceAndDestinationSame));
    }

    [TestMethod]
    public async Task Validator_BlocksDestinationInsideSource()
    {
        using var temp=new TempDirectory();var source=temp.CreateFile("source/a.jpg",[1]);var plan=await new FileOperationPlanner(new FileConflictResolver()).CreateAsync(Guid.NewGuid(),null,FileOperationType.Copy,temp.Combine("source"),temp.Combine("source","out"),[source]);var result=await new FileOperationValidator().ValidateAsync(plan);Assert.IsTrue(result.Issues.Any(x=>x.ErrorCode==Core.Services.ErrorCodeCatalog.DestinationInsideSource));
    }

    [TestMethod]
    public async Task Validator_ReportsMissingSource()
    {
        using var temp=new TempDirectory();var missing=temp.Combine("source","missing.jpg");var plan=await new FileOperationPlanner(new FileConflictResolver()).CreateAsync(Guid.NewGuid(),null,FileOperationType.Copy,temp.Combine("source"),temp.Combine("out"),[missing]);var result=await new FileOperationValidator().ValidateAsync(plan);Assert.IsTrue(result.Issues.Any(x=>x.ErrorCode==Core.Services.ErrorCodeCatalog.SourceNotFound));
    }

    [TestMethod]
    public async Task Validator_ReportsLockedFile()
    {
        using var temp=new TempDirectory();var source=temp.CreateFile("source/a.jpg",new byte[100]);var plan=await new FileOperationPlanner(new FileConflictResolver()).CreateAsync(Guid.NewGuid(),null,FileOperationType.Copy,temp.Combine("source"),temp.Combine("out"),[source]);using var held=new FileStream(source,FileMode.Open,FileAccess.ReadWrite,FileShare.None);var result=await new FileOperationValidator().ValidateAsync(plan);Assert.IsTrue(result.Issues.Any(x=>x.ErrorCode==Core.Services.ErrorCodeCatalog.FileLocked));
    }

    [TestMethod]
    public async Task Executor_CopiesWithCreateNewAndHash()
    {
        using var setup=await SetupAsync();var source=setup.Temp.CreateFile("source/a.jpg",Enumerable.Range(0,255).Select(x=>(byte)x).ToArray());var plan=await setup.Planner.CreateAsync(Guid.NewGuid(),null,FileOperationType.Copy,setup.Temp.Combine("source"),setup.Temp.Combine("out"),[source]);var result=await setup.Executor.ExecuteAsync(plan);Assert.AreEqual(1,result.Summary.Succeeded);var output=result.Items.Single().DestinationPath!;CollectionAssert.AreEqual(File.ReadAllBytes(source),File.ReadAllBytes(output));Assert.IsFalse(string.IsNullOrWhiteSpace(result.Items.Single().Hash));
    }

    [TestMethod]
    public async Task Executor_DoesNotOverwriteExistingFile()
    {
        using var setup=await SetupAsync();var source=setup.Temp.CreateFile("source/a.jpg",[1,2,3]);var existing=setup.Temp.CreateFile("out/a.jpg",[9,9]);var plan=await setup.Planner.CreateAsync(Guid.NewGuid(),null,FileOperationType.Copy,setup.Temp.Combine("source"),setup.Temp.Combine("out"),[source]);await setup.Executor.ExecuteAsync(plan);CollectionAssert.AreEqual(new byte[]{9,9},File.ReadAllBytes(existing));Assert.IsTrue(File.Exists(setup.Temp.Combine("out","a (1).jpg")));
    }

    [TestMethod]
    public async Task Executor_MoveDeletesSourceOnlyAfterVerification()
    {
        using var setup=await SetupAsync();var source=setup.Temp.CreateFile("source/a.raw",new byte[4096]);var plan=await setup.Planner.CreateAsync(Guid.NewGuid(),null,FileOperationType.Move,setup.Temp.Combine("source"),setup.Temp.Combine("out"),[source]);var result=await setup.Executor.ExecuteAsync(plan);Assert.AreEqual(1,result.Summary.Succeeded);Assert.IsFalse(File.Exists(source));Assert.IsTrue(File.Exists(result.Items.Single().DestinationPath));
    }

    [TestMethod]
    public async Task Executor_PartialCompletionPreservesSuccessfulItems()
    {
        using var setup=await SetupAsync();var good=setup.Temp.CreateFile("source/good.jpg",[1,2,3]);var missing=setup.Temp.Combine("source","missing.jpg");var plan=await setup.Planner.CreateAsync(Guid.NewGuid(),null,FileOperationType.Copy,setup.Temp.Combine("source"),setup.Temp.Combine("out"),[good,missing]);var result=await setup.Executor.ExecuteAsync(plan);Assert.AreEqual(1,result.Summary.Succeeded);Assert.AreEqual(1,result.Summary.Failed);Assert.IsTrue(result.Summary.IsPartial);Assert.IsTrue(File.Exists(setup.Temp.Combine("out","good.jpg")));
    }

    [TestMethod]
    public async Task Executor_CancellationDoesNotDeleteSource()
    {
        using var setup=await SetupAsync();var source=setup.Temp.CreateFile("source/a.jpg",new byte[1024*1024]);var plan=await setup.Planner.CreateAsync(Guid.NewGuid(),null,FileOperationType.Copy,setup.Temp.Combine("source"),setup.Temp.Combine("out"),[source]);using var cancel=new CancellationTokenSource();cancel.Cancel();await Assert.ThrowsAsync<OperationCanceledException>(()=>setup.Executor.ExecuteAsync(plan,cancellationToken:cancel.Token));Assert.IsTrue(File.Exists(source));Assert.IsFalse(File.Exists(setup.Temp.Combine("out","a.jpg")));
    }

    [TestMethod]
    public async Task UndoCopy_DeletesOnlyTaskCreatedUnchangedOutput()
    {
        using var setup=await SetupAsync();var source=setup.Temp.CreateFile("source/a.jpg",[1,2,3]);var task=Guid.NewGuid();var plan=await setup.Planner.CreateAsync(task,null,FileOperationType.Copy,setup.Temp.Combine("source"),setup.Temp.Combine("out"),[source]);var result=await setup.Executor.ExecuteAsync(plan);var output=result.Items.Single().DestinationPath!;var summary=await setup.Undo.UndoAsync(task);Assert.AreEqual(1,summary.Succeeded);Assert.IsFalse(File.Exists(output));Assert.IsTrue(File.Exists(source));
    }

    [TestMethod]
    public async Task UndoCopy_RejectsUserModifiedOutput()
    {
        using var setup=await SetupAsync();var source=setup.Temp.CreateFile("source/a.jpg",[1,2,3]);var task=Guid.NewGuid();var plan=await setup.Planner.CreateAsync(task,null,FileOperationType.Copy,setup.Temp.Combine("source"),setup.Temp.Combine("out"),[source]);var result=await setup.Executor.ExecuteAsync(plan);var output=result.Items.Single().DestinationPath!;File.WriteAllBytes(output,[3,2,1]);var summary=await setup.Undo.UndoAsync(task);Assert.AreEqual(1,summary.WaitingForAttention);Assert.IsTrue(File.Exists(output));
    }

    [TestMethod]
    public async Task UndoMove_RestoresOriginalWithoutOverwrite()
    {
        using var setup=await SetupAsync();var source=setup.Temp.CreateFile("source/a.raw",[1,2,3,4]);var task=Guid.NewGuid();var plan=await setup.Planner.CreateAsync(task,null,FileOperationType.Move,setup.Temp.Combine("source"),setup.Temp.Combine("out"),[source]);await setup.Executor.ExecuteAsync(plan);var summary=await setup.Undo.UndoAsync(task);Assert.AreEqual(1,summary.Succeeded);Assert.IsTrue(File.Exists(source));Assert.IsFalse(File.Exists(setup.Temp.Combine("out","a.raw")));
    }

    [TestMethod]
    public async Task UndoMove_RejectsWhenOriginalPathNowOccupied()
    {
        using var setup=await SetupAsync();var source=setup.Temp.CreateFile("source/a.raw",[1,2,3,4]);var task=Guid.NewGuid();var plan=await setup.Planner.CreateAsync(task,null,FileOperationType.Move,setup.Temp.Combine("source"),setup.Temp.Combine("out"),[source]);await setup.Executor.ExecuteAsync(plan);File.WriteAllBytes(source,[9]);var summary=await setup.Undo.UndoAsync(task);Assert.AreEqual(1,summary.WaitingForAttention);CollectionAssert.AreEqual(new byte[]{9},File.ReadAllBytes(source));
    }

    private static async Task<Setup> SetupAsync(){var temp=new TempDirectory();var db=new PixelTartDatabase(temp.Combine("db.sqlite"));await new DatabaseMigrator(db,new DatabaseBackupService(db,temp.Combine("backups"))).MigrateAsync();var verify=new FileVerificationService();var journal=new SqliteUndoJournalRepository(db);var planner=new FileOperationPlanner(new FileConflictResolver());return new(temp,planner,new FileOperationExecutor(new FileOperationValidator(),verify,journal,db),new UndoJournalService(journal,verify));}
    private sealed class Setup(TempDirectory temp,FileOperationPlanner planner,FileOperationExecutor executor,UndoJournalService undo):IDisposable{public TempDirectory Temp{get;}=temp;public FileOperationPlanner Planner{get;}=planner;public FileOperationExecutor Executor{get;}=executor;public UndoJournalService Undo{get;}=undo;public void Dispose()=>temp.Dispose();}
}
