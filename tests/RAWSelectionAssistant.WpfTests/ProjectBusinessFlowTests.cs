using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DocumentFormat.OpenXml.Packaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Business;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class ProjectBusinessFlowTests
{
    private static string Root=>Environment.GetEnvironmentVariable("KITAO_BUSINESS_FLOW_ROOT")??throw new InvalidOperationException("Explicit business evidence root required");
    private sealed record Identity(Guid ProjectId,Guid DocumentId,Guid BookingId,string Name,string Body,string ExpenseNote);
    private sealed record Receipt(int ProcessId,string DataRoot,Identity First,Identity Second,string LedgerHash);
    private static async Task Record(string name,object value){Directory.CreateDirectory(Root);await File.WriteAllTextAsync(Path.Combine(Root,name+".json"),JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));}
    private static async Task Command(ICommand command,object? parameter=null){Assert.IsTrue(command.CanExecute(parameter),"Production command must be enabled");if(command is AsyncRelayCommand asynchronous)await asynchronous.ExecuteAsync(parameter);else command.Execute(parameter);}
    private static async Task Wait(Func<bool> ready){var deadline=DateTime.UtcNow.AddSeconds(25);while(!ready()&&DateTime.UtcNow<deadline)await Task.Delay(20);Assert.IsTrue(ready(),"Production operation did not settle");}
    private static IEnumerable<Target> Find<Target>(DependencyObject root)where Target:DependencyObject{if(root is Target match)yield return match;for(var index=0;index<VisualTreeHelper.GetChildrenCount(root);index++)foreach(var child in Find<Target>(VisualTreeHelper.GetChild(root,index)))yield return child;}
    private static IFinanceService Ledger(FinanceViewModel finance)=>(IFinanceService)typeof(FinanceViewModel).GetField("_service",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(finance)!;
    private static async Task<string> LedgerHash(FinanceViewModel finance)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes((await Ledger(finance).QueryAsync(new())).OrderBy(value=>value.Id).ToArray())));
    private static string DocxText(string path){using var package=WordprocessingDocument.Open(path,false);return package.MainDocumentPart!.Document.Body!.InnerText;}
    private static async Task OpenPlan(MainViewModel main,Guid project)
    {
        await Wait(()=>!main.PlanningPage!.IsPlanningLoading);var repository=new SqliteProjectRepository(new PixelTartDatabase(AppDataPaths.DatabaseFile));var value=(await repository.ListAsync()).Single(item=>item.Id==project);await Command(main.OpenProjectPlanningCommand,value);await Wait(()=>main.IsPlanningPage&&main.PlanningPage!.ProjectId==project&&!main.PlanningPage.IsPlanningLoading);
    }
    private static async Task<PlanningCenterView> BudgetView(MainWindow window,MainViewModel main)
    {
        Assert.IsTrue(await main.PlanningPage!.SelectContentSafelyAsync("预算"));await Task.Delay(60);var view=Find<PlanningCenterView>((DependencyObject)window.Content).Single(value=>value.IsVisible);view.Render();window.UpdateLayout();return view;
    }
    private static async Task ReadExpenses(MainWindow window,MainViewModel main,int expected)
    {
        var view=await BudgetView(window,main);Find<Button>(view).Single(button=>Equals(button.Content,"读取项目实际支出（只读）")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Wait(()=>main.PlanningPage!.PlanningExpenses.Count==expected&&main.PlanningPage.ResourceStatus.Contains("只读"));view.Render();
    }
    private static async Task<ShootBooking> Booking(MainWindow window,MainViewModel main,string title,DateTime date,Guid? edit=null)
    {
        main.NavigateCommand.Execute("WorkCalendar");await Wait(()=>!main.WorkCalendarPage.IsBusy);ShootBookingEditorViewModel? editor=null;void Capture(object? sender,BookingEditorRequestEventArgs value)=>editor=value.Editor;main.WorkCalendarPage.EditorRequested+=Capture;
        try{await Command(edit is null?main.WorkCalendarPage.NewBookingCommand:main.WorkCalendarPage.EditBookingCommand,edit);await Wait(()=>editor is not null);}finally{main.WorkCalendarPage.EditorRequested-=Capture;}
        var current=editor!;Assert.AreSame(current,((FrameworkElement)window.FindName(edit is null?"QuickBookingEditorHost":"DrawerBookingEditorHost")).DataContext);current.Title=title;current.ClientDisplayName="SYNTHETIC TEAM";current.StartDate=date;current.EndDate=date;current.StartTimeText="10:00";current.EndTimeText="12:00";current.Location=title+"-LOCATION";current.SelectedStatus=current.StatusOptions.Single(value=>value.Value==ShootBookingStatus.Confirmed);current.SelectedTimeZone=current.TimeZoneOptions.First(value=>value.Id is "China Standard Time" or "Asia/Shanghai");
        ShootBooking? saved=null;current.Saved+=(_,value)=>saved=value;await Command(current.SaveCommand);Assert.IsNotNull(saved,current.ValidationText);await Wait(()=>main.WorkCalendarPage.Details.Booking?.Id==saved.Id);return saved;
    }
    private static async Task<Identity> Create(MainWindow window,MainViewModel main,string suffix,DateTime date)
    {
        main.NavigateCommand.Execute("Planning");await Wait(()=>!main.PlanningPage!.IsPlanningLoading);var planning=main.PlanningPage!;planning.NewPlanningName="SYNTHETIC PROJECT "+suffix;await Command(planning.CreatePlanningCommand);Assert.IsTrue(planning.HasProject,planning.OverviewStatus);var project=planning.ProjectId!.Value;var document=planning.PlanningDocumentId!.Value;var body="PLAN_"+suffix+"_ONLY";planning.DocumentBody=body;Assert.IsTrue(await planning.FlushAsync());Assert.AreEqual("未安排日期",planning.BookingDate);Assert.IsTrue(await planning.SaveResourceAsync(new PlanningBudgetLine{Name="BUDGET_"+suffix,Planned=suffix=="A"?500:700,Currency=suffix=="A"?"CNY":"EUR"}));
        var booking=await Booking(window,main,"BOOKING_"+suffix,date);await OpenPlan(main,project);await Command(planning.ShowBookingCommand);planning.SelectedBooking=planning.AvailableBookings.Single(value=>value.Id==booking.Id);await Command(planning.LinkBookingCommand);Assert.AreEqual(booking.Id,(await new PlanningProjectStore(Path.Combine(AppDataPaths.DataDirectory,"ProjectPlanning")).LoadAsync(project)).BookingId);return new(project,document,booking.Id,"SYNTHETIC PROJECT "+suffix,body,"EXPENSE_"+suffix+"_ONLY");
    }
    private static async Task Expense(MainViewModel main,Identity identity,string amount,string currency,string note,bool income=false)
    {
        main.NavigateCommand.Execute("Finance");var finance=main.FinancePage!;await Wait(()=>!finance.IsBusy);await finance.RefreshAsync();await Command(income?finance.NewIncomeCommand:finance.NewExpenseCommand);finance.AmountText=amount;finance.CurrencyCode=currency;finance.Note=note;finance.OccurredOn=new DateTime(2026,11,10);finance.SelectedProjectLink=finance.ProjectLinkOptions.Single(value=>value.Id==identity.ProjectId);finance.SelectedBookingLink=finance.BookingLinkOptions.Single(value=>value.Id==identity.BookingId);Assert.IsNotNull(finance.SelectedCategory);await Command(finance.SaveCommand);Assert.IsFalse(finance.IsEditorOpen,finance.StatusText);Assert.AreEqual(1,(await Ledger(finance).QueryAsync(new(ProjectId:identity.ProjectId))).Count(value=>value.Note==note));
    }
    private static async Task ReturnFromCalendar(MainViewModel main,Identity identity)
    {
        main.NavigateCommand.Execute("WorkCalendar");await main.WorkCalendarPage.OpenBookingDetailsAsync(identity.BookingId);Assert.AreEqual(identity.ProjectId,main.WorkCalendarPage.Details.Booking!.ProjectId);await Command(main.WorkCalendarPage.OpenFullPlanningCommand,identity.BookingId);await Wait(()=>main.IsPlanningPage&&main.PlanningPage!.ProjectId==identity.ProjectId&&!main.PlanningPage.IsPlanningLoading);Assert.AreEqual(identity.DocumentId,main.PlanningPage!.PlanningDocumentId);Assert.AreEqual(identity.Body,main.PlanningPage.DocumentBody);
    }
    private static void RunHost(Func<MainWindow,MainViewModel,Task> action)
    {
        Exception? failure=null;var complete=false;var thread=new Thread(()=>
        {
            try
            {
                var configured=Environment.GetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME_ROOT")??throw new InvalidOperationException("Isolation required");Assert.AreEqual(Path.GetFullPath(configured),AppDataPaths.Root,true);Directory.CreateDirectory(Root);
                if(!File.Exists(AppDataPaths.SettingsFile))new SettingsService(new FileLogService()).SaveAsync(new AppSettings{Weather=new(){Enabled=false,AutoRefreshEnabled=false}}).GetAwaiter().GetResult();
                var application=new App();application.InitializeComponent();var busy=false;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(120)};timer.Tick+=async(_,_)=>
                {
                    if(busy||application.MainWindow is not MainWindow{IsLoaded:true,DataContext:MainViewModel main} window)return;busy=true;
                    try{main.ForceExitTutorial();await action(window,main);complete=true;}catch(Exception error){failure=error;}finally{timer.Stop();application.Shutdown();}
                };timer.Start();application.Run();
            }
            catch(Exception error){failure=error;}
        }){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(3)),"Business host timeout");if(failure is not null)throw new AssertFailedException(failure.ToString(),failure);Assert.IsTrue(complete);
    }
    [TestMethod]
    public void CompleteAppProjectPlanningCalendarExpensesAndSummaryChain()=>RunHost(async(window,main)=>
    {
        Assert.IsNotNull(main.FinancePage);Assert.IsNotNull(main.PlanningPage!.ReadPlanningExpenses);Assert.AreSame(main.FinancePage,main.PlanningPage.ReadPlanningExpenses!.Target);
        var first=await Create(window,main,"A",new DateTime(2026,11,10));var second=await Create(window,main,"B",new DateTime(2026,11,11));
        var rescheduled=await Booking(window,main,"BOOKING_A_RESCHEDULED",new DateTime(2026,11,12),first.BookingId);Assert.AreEqual(first.ProjectId,rescheduled.ProjectId);Assert.AreEqual(first.BookingId,rescheduled.Id);
        await ReturnFromCalendar(main,first);Assert.AreEqual(rescheduled.StartAtUtc.ToLocalTime().ToString("MM月dd日"),main.PlanningPage!.BookingDate);
        await Expense(main,first,"125.50","CNY",first.ExpenseNote);await Expense(main,first,"10.00","USD","EXPENSE_A_USD_ONLY");await Expense(main,second,"83.00","EUR",second.ExpenseNote);await Expense(main,first,"999.00","CNY","INCOME_A_EXCLUDED",true);
        var ledgerBefore=await LedgerHash(main.FinancePage!);await ReturnFromCalendar(main,first);await ReadExpenses(window,main,2);Assert.IsTrue(main.PlanningPage!.PlanningExpenses.All(value=>value.ProjectId==first.ProjectId&&value.Kind==FinanceTransactionKind.Expense));CollectionAssert.AreEquivalent(new[]{"CNY","USD"},main.PlanningPage.PlanningExpenses.Select(value=>value.CurrencyCode).ToArray());
        var firstPath=Path.Combine(Root,"project-A-summary.docx");var result=await main.PlanningPage.ExportPlanningSummaryDocxAsync(firstPath);Assert.IsNotNull(result);var firstText=DocxText(firstPath);StringAssert.Contains(firstText,first.ProjectId.ToString());StringAssert.Contains(firstText,first.BookingId.ToString());StringAssert.Contains(firstText,first.Body);StringAssert.Contains(firstText,"BOOKING_A_RESCHEDULED");StringAssert.Contains(firstText,first.ExpenseNote);StringAssert.Contains(firstText,"EXPENSE_A_USD_ONLY");StringAssert.Contains(firstText,"BUDGET_A");Assert.IsFalse(firstText.Contains(second.ExpenseNote));Assert.IsFalse(firstText.Contains("INCOME_A_EXCLUDED"));
        StringAssert.Contains(firstText,"金额最小单位：12550");StringAssert.Contains(firstText,"金额最小单位：1000");StringAssert.Contains(firstText,"预算金额：500");
        await ReturnFromCalendar(main,second);var stale=main.PlanningPage.PlanningExpenses.Select(value=>new{value.Id,value.ProjectId,value.Note}).ToArray();var staleStatus=main.PlanningPage.ResourceStatus;var budget=await BudgetView(window,main);var shown=string.Join("|",Find<TextBlock>(budget).Select(value=>value.Text));Assert.IsFalse(shown.Contains(first.ExpenseNote),"Visible budget must not show A rows on B");
        await Record("PROJECT_SWITCH_OBSERVATION",new{currentProject=second.ProjectId,retainedExpenses=stale,status=staleStatus,oldExpenseVisible=shown.Contains(first.ExpenseNote)});
        await ReadExpenses(window,main,1);Assert.AreEqual(second.ProjectId,main.PlanningPage.PlanningExpenses.Single().ProjectId);var secondPath=Path.Combine(Root,"project-B-summary.docx");Assert.IsNotNull(await main.PlanningPage.ExportPlanningSummaryDocxAsync(secondPath));var secondText=DocxText(secondPath);StringAssert.Contains(secondText,second.Body);StringAssert.Contains(secondText,second.BookingId.ToString());StringAssert.Contains(secondText,second.ExpenseNote);Assert.IsFalse(secondText.Contains(first.ExpenseNote));
        Assert.AreEqual(ledgerBefore,await LedgerHash(main.FinancePage!));
        await Record("BUSINESS_FLOW",new Receipt(Environment.ProcessId,AppDataPaths.Root,first,second,ledgerBefore));await Record("MAIN_CHAIN_RESULT",new{processId=Environment.ProcessId,projects=2,calendarReturn=true,reschedule=true,expenseRecords=3,incomeExcluded=true,currenciesSeparate=true,budgetSeparate=true,ledgerUnchanged=true,summarySections=result.Sections,scope="Actual App and commands/control handlers, not mouse"});
        Assert.AreEqual(0,stale.Length,"A-to-B load must retire the previous project's expense state before a new read");Assert.IsFalse(staleStatus.Contains("按项目只读读取支出"),"B must not present A's successful-read status as its own");
    });
    [TestMethod]
    public void IndependentAppReopenPreservesProjectBookingAndLedgerIdentities()=>RunHost(async(window,main)=>
    {
        var receipt=JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(Path.Combine(Root,"BUSINESS_FLOW.json")))!;Assert.AreNotEqual(receipt.ProcessId,Environment.ProcessId);Assert.AreEqual(receipt.DataRoot,AppDataPaths.Root,true);Assert.AreEqual(receipt.LedgerHash,await LedgerHash(main.FinancePage!));
        foreach(var identity in new[]{receipt.First,receipt.Second})
        {
            if(identity==receipt.First){await main.WorkCalendarPage.OpenBookingDetailsAsync(identity.BookingId);Assert.AreEqual("BOOKING_A_RESCHEDULED",main.WorkCalendarPage.Details.Booking!.Title);Assert.AreEqual(new DateTime(2026,11,12),main.WorkCalendarPage.Details.Booking.StartAtUtc.UtcDateTime.Date);}
            await ReturnFromCalendar(main,identity);await ReadExpenses(window,main,identity==receipt.First?2:1);Assert.IsTrue(main.PlanningPage!.PlanningExpenses.All(value=>value.ProjectId==identity.ProjectId));var path=Path.Combine(Root,"reopened-"+identity.ProjectId.ToString("N")+".docx");Assert.IsNotNull(await main.PlanningPage.ExportPlanningSummaryDocxAsync(path));var text=DocxText(path);StringAssert.Contains(text,identity.Body);StringAssert.Contains(text,identity.ExpenseNote);StringAssert.Contains(text,identity.BookingId.ToString());
        }
        Assert.AreEqual(receipt.LedgerHash,await LedgerHash(main.FinancePage!));await Record("INDEPENDENT_REOPEN_RESULT",new{processId=Environment.ProcessId,producerProcessId=receipt.ProcessId,twoProjectIdentities=true,bookingLinks=true,ledgerExact=true});
    });
}
