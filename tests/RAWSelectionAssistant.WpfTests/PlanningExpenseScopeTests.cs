using System.IO;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class PlanningExpenseScopeTests
{
    private static string Root=>Environment.GetEnvironmentVariable("KITAO_BUSINESS_FLOW_ROOT")??throw new InvalidOperationException("Explicit business root required");
    private static FinanceTransaction Row(Guid project,string note)=>new(){Id=Guid.NewGuid(),ProjectId=project,Kind=FinanceTransactionKind.Expense,AmountMinor=1234,CurrencyCode="CNY",Note=note};
    private static async Task<(PlanningCenterViewModel ViewModel,Guid First,Guid Second)> Create()
    {
        EnsureTestApplication();var root=Path.Combine(Root,"expense-scope-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var database=new PixelTartDatabase(Path.Combine(root,"data.db"));Assert.IsTrue((await new DatabaseMigrator(database,new DatabaseBackupService(database,Path.Combine(root,"backups"))).MigrateAsync()).Success);
        var projects=new SqliteProjectRepository(database);var first=Guid.NewGuid();var second=Guid.NewGuid();await projects.UpsertAsync(new PhotoProjectRecord{Id=first,Name="SCOPE-A"});await projects.UpsertAsync(new PhotoProjectRecord{Id=second,Name="SCOPE-B"});var repository=new SqliteShootBookingRepository(database);var bookings=new ShootBookingService(repository,new BookingConflictDetector(repository));
        var viewModel=new PlanningCenterViewModel(projects,bookings,new PlanningFoundationWpfTests.Dialogs(),new ProjectShotStore(Path.Combine(root,"shots")),new PlanningProjectStore(Path.Combine(root,"planning")),new ProjectVisualReferenceStore(Path.Combine(root,"visuals")),new ReferenceLookStore(Path.Combine(root,"looks")),new CanvasDocumentStore(Path.Combine(root,"canvas")));await viewModel.LoadAsync(first);viewModel.DocumentTitle="A";Assert.IsTrue(await viewModel.FlushAsync());return(viewModel,first,second);
    }
    private static async Task Record(string name,object value){Directory.CreateDirectory(Root);await File.WriteAllTextAsync(Path.Combine(Root,name+".json"),JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));}
    [TestMethod]
    public Task AToBToALateReadCannotReplaceTheNewerARead()=>RunSta(async()=>
    {
        var fixture=await Create();var viewModel=fixture.ViewModel;var pending=new TaskCompletionSource<IReadOnlyList<FinanceTransaction>>(TaskCreationOptions.RunContinuationsAsynchronously);var calls=0;viewModel.ReadPlanningExpenses=_=>++calls==1?pending.Task:Task.FromResult<IReadOnlyList<FinanceTransaction>>([Row(fixture.First,"CURRENT_A")]);
        var old=viewModel.RefreshPlanningExpensesAsync();await viewModel.LoadAsync(fixture.Second);await viewModel.LoadAsync(fixture.First);await viewModel.RefreshPlanningExpensesAsync();pending.SetResult([Row(fixture.First,"STALE_A_BEFORE_SWITCH")]);await old;
        await Record("ABA_EXPENSE_READ",new{project=viewModel.ProjectId,notes=viewModel.PlanningExpenses.Select(value=>value.Note),status=viewModel.ResourceStatus});Assert.AreEqual("CURRENT_A",viewModel.PlanningExpenses.Single().Note,"An old A read must not publish merely because the UI returned to A");
    });
    [TestMethod]
    public Task LateFailureFromADoesNotReplaceBReadStatus()=>RunSta(async()=>
    {
        var fixture=await Create();var viewModel=fixture.ViewModel;var pending=new TaskCompletionSource<IReadOnlyList<FinanceTransaction>>(TaskCreationOptions.RunContinuationsAsynchronously);viewModel.ReadPlanningExpenses=project=>project==fixture.First?pending.Task:Task.FromResult<IReadOnlyList<FinanceTransaction>>([Row(fixture.Second,"CURRENT_B")]);
        var old=viewModel.RefreshPlanningExpensesAsync();await viewModel.LoadAsync(fixture.Second);await viewModel.RefreshPlanningExpensesAsync();var expected=viewModel.ResourceStatus;pending.SetException(new IOException("SYNTHETIC_OLD_A_FAILURE"));await old;
        await Record("LATE_EXPENSE_FAILURE",new{project=viewModel.ProjectId,notes=viewModel.PlanningExpenses.Select(value=>value.Note),expected,status=viewModel.ResourceStatus});Assert.AreEqual(expected,viewModel.ResourceStatus,"A stale A error must not become B's current ledger-read state");Assert.AreEqual(fixture.Second,viewModel.PlanningExpenses.Single().ProjectId);
    });
    [TestMethod]
    public Task EmptyDisconnectedAndPermissionFailureRemainExplicitNotSuccessfulZeroOrCache()=>RunSta(async()=>
    {
        var fixture=await Create();var viewModel=fixture.ViewModel;viewModel.ReadPlanningExpenses=_=>Task.FromResult<IReadOnlyList<FinanceTransaction>>([]);await viewModel.RefreshPlanningExpensesAsync();await Record("EMPTY_EXPENSE_STATE",new{count=viewModel.PlanningExpenses.Count,status=viewModel.ResourceStatus});StringAssert.Contains(viewModel.ResourceStatus,"暂无");
        var emptyPath=Path.Combine(Root,"empty-expenses-"+Guid.NewGuid().ToString("N")+".docx");Assert.IsNotNull(await viewModel.ExportPlanningSummaryDocxAsync(emptyPath));using(var document=WordprocessingDocument.Open(emptyPath,false))StringAssert.Contains(document.MainDocumentPart!.Document.Body!.InnerText,"暂无实际支出");
        viewModel.ReadPlanningExpenses=_=>Task.FromResult<IReadOnlyList<FinanceTransaction>>([Row(fixture.First,"PRIOR_RESULT")]);await viewModel.RefreshPlanningExpensesAsync();viewModel.ReadPlanningExpenses=_=>Task.FromException<IReadOnlyList<FinanceTransaction>>(new UnauthorizedAccessException("SYNTHETIC_PERMISSION_FAILURE"));await viewModel.RefreshPlanningExpensesAsync();Assert.IsEmpty(viewModel.PlanningExpenses);StringAssert.Contains(viewModel.ResourceStatus,"失败");Assert.IsFalse(viewModel.ResourceStatus.Contains("读取成功"));
        var permissionStatus=viewModel.ResourceStatus;var failedPath=Path.Combine(Root,"failed-expenses-"+Guid.NewGuid().ToString("N")+".docx");Assert.IsNotNull(await viewModel.ExportPlanningSummaryDocxAsync(failedPath));using(var document=WordprocessingDocument.Open(failedPath,false)){var text=document.MainDocumentPart!.Document.Body!.InnerText;StringAssert.Contains(text,"本次账本读取失败；未使用旧缓存");Assert.IsFalse(text.Contains("PRIOR_RESULT"));}
        viewModel.ReadPlanningExpenses=null;await viewModel.RefreshPlanningExpensesAsync();Assert.IsEmpty(viewModel.PlanningExpenses);StringAssert.Contains(viewModel.ResourceStatus,"未连接");await Record("FAILED_AND_DISCONNECTED_EXPENSE_STATE",new{count=viewModel.PlanningExpenses.Count,permissionFailureStatus=permissionStatus,status=viewModel.ResourceStatus,summaryFailureExplicit=true,scope="Controlled read-delegate failures only; no real-user ledger permissions changed"});
    });
}
