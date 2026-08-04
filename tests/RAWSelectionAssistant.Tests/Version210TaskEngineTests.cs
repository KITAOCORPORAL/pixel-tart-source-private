using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Tasks;

#pragma warning disable MSTEST0037

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version210TaskEngineTests
{
    [TestMethod]
    [DataRow(TaskLifecycleState.Pending,TaskLifecycleState.Preparing)]
    [DataRow(TaskLifecycleState.Preparing,TaskLifecycleState.Scanning)]
    [DataRow(TaskLifecycleState.Preparing,TaskLifecycleState.Validating)]
    [DataRow(TaskLifecycleState.Preparing,TaskLifecycleState.Running)]
    [DataRow(TaskLifecycleState.Scanning,TaskLifecycleState.Validating)]
    [DataRow(TaskLifecycleState.Scanning,TaskLifecycleState.Running)]
    [DataRow(TaskLifecycleState.Validating,TaskLifecycleState.WaitingForConfirmation)]
    [DataRow(TaskLifecycleState.Validating,TaskLifecycleState.Running)]
    [DataRow(TaskLifecycleState.Running,TaskLifecycleState.Pausing)]
    [DataRow(TaskLifecycleState.Pausing,TaskLifecycleState.Paused)]
    [DataRow(TaskLifecycleState.Paused,TaskLifecycleState.Running)]
    [DataRow(TaskLifecycleState.Running,TaskLifecycleState.NeedsAttention)]
    [DataRow(TaskLifecycleState.NeedsAttention,TaskLifecycleState.Running)]
    [DataRow(TaskLifecycleState.Running,TaskLifecycleState.Retrying)]
    [DataRow(TaskLifecycleState.Retrying,TaskLifecycleState.Running)]
    [DataRow(TaskLifecycleState.Running,TaskLifecycleState.Cancelling)]
    [DataRow(TaskLifecycleState.Cancelling,TaskLifecycleState.Cancelled)]
    [DataRow(TaskLifecycleState.Running,TaskLifecycleState.PartiallyCompleted)]
    [DataRow(TaskLifecycleState.Running,TaskLifecycleState.Failed)]
    [DataRow(TaskLifecycleState.Running,TaskLifecycleState.Completed)]
    [DataRow(TaskLifecycleState.Running,TaskLifecycleState.Interrupted)]
    [DataRow(TaskLifecycleState.Failed,TaskLifecycleState.Retrying)]
    [DataRow(TaskLifecycleState.Interrupted,TaskLifecycleState.Retrying)]
    public void LegalStateTransitions_AreAccepted(TaskLifecycleState from,TaskLifecycleState to)=>Assert.IsTrue(TaskStateMachine.CanTransition(from,to));

    [TestMethod]
    [DataRow(TaskLifecycleState.Completed,TaskLifecycleState.Running)]
    [DataRow(TaskLifecycleState.Pending,TaskLifecycleState.Completed)]
    [DataRow(TaskLifecycleState.Paused,TaskLifecycleState.Completed)]
    [DataRow(TaskLifecycleState.Cancelled,TaskLifecycleState.Running)]
    [DataRow(TaskLifecycleState.Failed,TaskLifecycleState.Completed)]
    [DataRow(TaskLifecycleState.Preparing,TaskLifecycleState.Completed)]
    [DataRow(TaskLifecycleState.NeedsAttention,TaskLifecycleState.Completed)]
    [DataRow(TaskLifecycleState.Interrupted,TaskLifecycleState.Completed)]
    public void IllegalStateTransitions_AreRejected(TaskLifecycleState from,TaskLifecycleState to){Assert.IsFalse(TaskStateMachine.CanTransition(from,to));Assert.ThrowsExactly<InvalidOperationException>(()=>TaskStateMachine.EnsureTransition(from,to));}

    [TestMethod]
    [DataRow(1,0,0,0,false)]
    [DataRow(1,1,0,0,true)]
    [DataRow(1,0,1,0,true)]
    [DataRow(1,0,0,1,true)]
    public void PartialSummary_IsCalculated(int succeeded,int failed,int skipped,int attention,bool expected)=>Assert.AreEqual(expected,new TaskResultSummary(5,succeeded,failed,skipped,0,attention,0,0).IsPartial);

    [TestMethod]
    public async Task Repository_RoundTripsTaskRuntime()
    {
        using var setup=await SetupAsync();var definition=Definition("roundtrip");var runtime=new TaskRuntimeState{Definition=definition,State=TaskLifecycleState.Running,Progress=42,CurrentStep="copy",ResultSummary=new(10,4,1,0,0,0,100,80)};await setup.Repository.SaveAsync(runtime);var loaded=await setup.Repository.GetAsync(definition.Id);Assert.IsNotNull(loaded);Assert.AreEqual(42,loaded.Progress);Assert.AreEqual(4,loaded.ResultSummary.Succeeded);
    }

    [TestMethod]
    public async Task Recovery_MarksActiveTasksInterrupted()
    {
        using var setup=await SetupAsync();var runtime=new TaskRuntimeState{Definition=Definition("recovery"),State=TaskLifecycleState.Running};await setup.Repository.SaveAsync(runtime);var recovered=await new TaskRecoveryService(setup.Repository,setup.Audit).RecoverInterruptedAsync();Assert.AreEqual(1,recovered.Count);Assert.AreEqual(TaskLifecycleState.Interrupted,(await setup.Repository.GetAsync(runtime.Definition.Id))!.State);
    }

    [TestMethod]
    public async Task Engine_CompletesAndRetainsHistory()
    {
        using var setup=await SetupAsync(new DelegateTaskHandler("complete",async(ctx,token)=>{await ctx.SafeBoundaryAsync("one",1,cancellationToken:token);return TaskExecutionResult.Completed(new(1,1,0,0,0,0,1,1));}));var definition=Definition("complete");await setup.Engine.EnqueueAsync(definition);var runtime=await WaitForAsync(setup.Repository,definition.Id,TaskLifecycleState.Completed);Assert.AreEqual(1,runtime.ResultSummary.Succeeded);Assert.IsTrue((await setup.Engine.LoadHistoryAsync()).Any(x=>x.Definition.Id==definition.Id));
    }

    [TestMethod]
    public async Task Engine_PauseAndResumeAtSafeBoundary()
    {
        using var setup=await SetupAsync(new DelegateTaskHandler("pause",async(ctx,token)=>{for(var i=0;i<8;i++){await Task.Delay(40,token);await ctx.SafeBoundaryAsync("loop",i,cancellationToken:token);}return TaskExecutionResult.Completed(new(8,8,0,0,0,0,8,8));}));var definition=Definition("pause");await setup.Engine.EnqueueAsync(definition);await WaitUntilAsync(async()=> (await setup.Repository.GetAsync(definition.Id))?.State==TaskLifecycleState.Running);await setup.Engine.PauseAsync(definition.Id);await WaitForAsync(setup.Repository,definition.Id,TaskLifecycleState.Paused);await setup.Engine.ResumeAsync(definition.Id);await WaitForAsync(setup.Repository,definition.Id,TaskLifecycleState.Completed);
    }

    [TestMethod]
    public async Task Engine_CancelStopsAtBoundary()
    {
        using var setup=await SetupAsync(new DelegateTaskHandler("cancel",async(ctx,token)=>{for(var i=0;i<50;i++){await Task.Delay(20,token);await ctx.SafeBoundaryAsync("loop",i,cancellationToken:token);}return TaskExecutionResult.Completed(new(50,50,0,0,0,0,50,50));}));var definition=Definition("cancel");await setup.Engine.EnqueueAsync(definition);await WaitUntilAsync(async()=> (await setup.Repository.GetAsync(definition.Id))?.State==TaskLifecycleState.Running);await setup.Engine.CancelAsync(definition.Id);var runtime=await WaitUntilTerminalAsync(setup.Repository,definition.Id);Assert.IsTrue(runtime.State is TaskLifecycleState.Cancelled or TaskLifecycleState.PartiallyCompleted);
    }

    [TestMethod]
    public async Task Engine_NeedsAttentionWaitsForDecision()
    {
        using var setup=await SetupAsync(new DelegateTaskHandler("attention",async(ctx,token)=>{var request=new TaskAttentionRequest(Guid.NewGuid(),ctx.Definition.Id,TaskAttentionType.DuplicateConflict,"冲突","请选择",1,["skip","continue"],"skip",false,DateTimeOffset.UtcNow);var action=await ctx.RequestAttentionAsync(request,token);return action=="continue"?TaskExecutionResult.Completed(new(1,1,0,0,0,0,1,1)):new(TaskLifecycleState.PartiallyCompleted,new(1,0,0,1,0,0,0,0));}));var definition=Definition("attention");await setup.Engine.EnqueueAsync(definition);await WaitForAsync(setup.Repository,definition.Id,TaskLifecycleState.NeedsAttention);await setup.Engine.ResolveAttentionAsync(definition.Id,"continue");await WaitForAsync(setup.Repository,definition.Id,TaskLifecycleState.Completed);
    }

    [TestMethod]
    public async Task Engine_RetryHonorsMaximumCount()
    {
        using var setup=await SetupAsync(new DelegateTaskHandler("fail",(_,_)=>throw new IOException("fail")));var definition=Definition("fail",1);await setup.Engine.EnqueueAsync(definition);await WaitForAsync(setup.Repository,definition.Id,TaskLifecycleState.Failed);await setup.Engine.RetryAsync(definition.Id);await WaitForAsync(setup.Repository,definition.Id,TaskLifecycleState.Failed);await Assert.ThrowsExactlyAsync<InvalidOperationException>(()=>setup.Engine.RetryAsync(definition.Id));
    }

    [TestMethod]
    public void TaskDefinition_IsImmutableRecord()
    {
        var first=Definition("immutable");var second=first with{DisplayName="changed"};Assert.AreNotSame(first,second);Assert.AreNotEqual(first.DisplayName,second.DisplayName);
    }

    [TestMethod]
    public async Task Scheduler_SerializesSameDestination()
    {
        using var temp=new TempDirectory();var scheduler=new ConservativeTaskScheduler();var first=Definition("one") with{InputSnapshot=$"write-root:{temp.Path}"};var second=Definition("two") with{InputSnapshot=$"write-root:{temp.Path}"};var lease=await scheduler.AcquireAsync(first,CancellationToken.None);var waiting=scheduler.AcquireAsync(second,CancellationToken.None);await Task.Delay(50);Assert.IsFalse(waiting.IsCompleted);lease.Dispose();using var secondLease=await waiting;
    }

    [TestMethod]
    public async Task Scheduler_AllowsDifferentDestinations()
    {
        using var temp=new TempDirectory();var scheduler=new ConservativeTaskScheduler();var first=Definition("one") with{InputSnapshot=$"write-root:{temp.Combine("a")}"};var second=Definition("two") with{InputSnapshot=$"write-root:{temp.Combine("b")}"};using var lease=await scheduler.AcquireAsync(first,CancellationToken.None);var waiting=scheduler.AcquireAsync(second,CancellationToken.None);using var secondLease=await waiting.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public async Task Scheduler_SerializesSameWritableSource()
    {
        using var temp=new TempDirectory();var scheduler=new ConservativeTaskScheduler();var first=Definition("one") with{InputSnapshot=$"write-source:{temp.Path}"};var second=Definition("two") with{InputSnapshot=$"write-source:{temp.Path}"};var lease=await scheduler.AcquireAsync(first,CancellationToken.None);var waiting=scheduler.AcquireAsync(second,CancellationToken.None);await Task.Delay(50);Assert.IsFalse(waiting.IsCompleted);lease.Dispose();using var secondLease=await waiting;
    }

    private static TaskDefinition Definition(string type,int retries=3)=>new(Guid.NewGuid(),null,type,type,"",null,DateTimeOffset.UtcNow,TaskPriority.Normal,retries);
    private static async Task<TestSetup> SetupAsync(params ITaskHandler[] handlers){var temp=new TempDirectory();var db=new PixelTartDatabase(temp.Combine("db.sqlite"));await new DatabaseMigrator(db,new DatabaseBackupService(db,temp.Combine("backups"))).MigrateAsync();var repository=new SqliteTaskRepository(db);var audit=new AuditLogService(db);var notification=new NotificationCenter(db,TimeSpan.Zero);var engine=new TaskEngine(repository,new ConservativeTaskScheduler(),handlers.Length==0?[new DelegateTaskHandler("noop",(_,_)=>Task.FromResult(TaskExecutionResult.Completed(TaskResultSummary.Empty)))]:handlers,audit,notification,TimeSpan.Zero);return new(temp,repository,audit,engine);}
    private static async Task<TaskRuntimeState> WaitForAsync(ITaskRepository repository,Guid id,TaskLifecycleState state){TaskRuntimeState? current=null;for(var i=0;i<200;i++){current=await repository.GetAsync(id);if(current?.State==state)return current;await Task.Delay(20);}Assert.Fail($"Expected {state}, actual {current?.State}");return null!;}
    private static async Task WaitUntilAsync(Func<Task<bool>> condition){for(var i=0;i<200;i++){if(await condition())return;await Task.Delay(20);}Assert.Fail("Condition timed out.");}
    private static async Task<TaskRuntimeState> WaitUntilTerminalAsync(ITaskRepository repository,Guid id){TaskRuntimeState? current=null;for(var i=0;i<200;i++){current=await repository.GetAsync(id);if(current is not null&&TaskStateMachine.IsTerminal(current.State))return current;await Task.Delay(20);}Assert.Fail("Task did not finish.");return null!;}
    private sealed class TestSetup(TempDirectory temp,SqliteTaskRepository repository,AuditLogService audit,TaskEngine engine):IDisposable{public SqliteTaskRepository Repository{get;}=repository;public AuditLogService Audit{get;}=audit;public TaskEngine Engine{get;}=engine;public void Dispose()=>temp.Dispose();}
}
