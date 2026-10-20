using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Business;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using DocumentFormat.OpenXml.Packaging;
using W=DocumentFormat.OpenXml.Wordprocessing;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;

[TestClass][DoNotParallelize]
public sealed class PlanningBatch3BusinessTests
{
    [TestMethod]public Task FifteenSectionsCreateEditLinkRecoverMoveAndReopenOneSyntheticPlan()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=Fresh("business");var db=new PixelTartDatabase(Path.Combine(root,"synthetic.db"));Assert.IsTrue((await new DatabaseMigrator(db,new DatabaseBackupService(db,Path.Combine(root,"backup"))).MigrateAsync()).Success);
        var projects=new SqliteProjectRepository(db);var repository=new SqliteShootBookingRepository(db);var bookings=new ShootBookingService(repository,new BookingConflictDetector(repository));
        var dialogs=new PlanningFoundationWpfTests.Dialogs();var planRoot=Path.Combine(root,"planning");var shotRoot=Path.Combine(root,"shots");
        PlanningCenterViewModel Vm(string plans,string shots)=>new(projects,bookings,dialogs,new ProjectShotStore(shots),new PlanningProjectStore(plans),new ProjectVisualReferenceStore(Path.Combine(root,"visuals")),new ReferenceLookStore(Path.Combine(root,"looks")),new CanvasDocumentStore(Path.Combine(root,"canvas")));
        var vm=Vm(planRoot,shotRoot);vm.NewPlanningName="批次3 · 合成摄影提案";vm.NewLocation="合成棚A";vm.NewShootDate=new(2026,10,21);await vm.CreatePlanningCommand.ExecuteAsync(null);Assert.IsTrue(vm.HasProject,vm.OverviewStatus);var project=vm.ProjectId!.Value;var document=vm.PlanningDocumentId!.Value;
        vm.DocumentTitle="批次3 · 编辑后的提案";vm.DocumentPeople="合成摄影师与助理";vm.ShootGoal="逆光人像";vm.Keywords="柔光/白纱";vm.DocumentCustomBody="道具清点与收尾事项";Assert.IsTrue(await vm.FlushAsync());
        var view=new PlanningCenterView{DataContext=vm};Layout(view);await vm.SelectContentSafelyAsync("文字");vm.IsDocumentEditing=true;view.Render();Layout(view);
        var rich=Walk<PlanningRichEditor>(view).Single();var box=Walk<RichTextBox>(rich).Single();box.Document=new FlowDocument(new Paragraph(new Run("本轮合成正文：开场走位与灯光说明")));rich.FlushPending();Assert.IsTrue(await vm.FlushAsync());
        await vm.NewShotCommand.ExecuteAsync(null);var shot=vm.SelectedShot!.ShotId;await vm.SelectContentSafelyAsync("镜头清单");view.Render();Layout(view);var table=Walk<DataGrid>(view).Single(t=>AutomationProperties.GetAutomationId(t)=="PlanningEditableShotTable");var row=(PlanningShotEditRow)table.Items[0];row.BeginEdit();row["Name"]="01白纱开场";row["Action"]="由左向右缓行";row["EstimatedMinutes"]="15";row.EndEdit();Assert.IsTrue(await vm.FlushAsync());
        var original=Path.Combine(root,"synthetic-original.png");await File.WriteAllBytesAsync(original,PlanningRichFlowFixture.ReferencePng(0));var hash=Hash(original);var referenceId=Guid.NewGuid();await vm.AddDocumentReferencesAsync([new(referenceId,ShotReferenceKind.General,ExternalReference:original,Title:"合成逆光参考")]);var reference=vm.AllProjectReferences.Single(r=>r.Id==referenceId);await vm.UpdateReferenceAsync(reference,"情绪板");await vm.UpdateReferenceAsync(vm.AllProjectReferences.Single(r=>r.Id==referenceId),"分组","灯光");Assert.IsTrue(vm.AllProjectReferences.Single(r=>r.Id==referenceId).IsMoodboard);vm.StageShot(vm.ShotDraft(shot) with{References=[new(referenceId,ShotReferenceKind.General,ExternalReference:original,Title:"合成逆光参考")]});Assert.IsTrue(await vm.FlushAsync());
        var attachment=Path.Combine(root,"synthetic-call-sheet.txt");await File.WriteAllTextAsync(attachment,"SYNTHETIC ONLY - original attachment");var attachmentHash=Hash(attachment);await vm.AddAttachmentsAsync([attachment]);
        var created=new List<object>();
        foreach(var section in new[]{"灯位图","服化道与道具","设备","人员","场地","当天时间表","预算"})
        {
            await vm.SelectContentSafelyAsync(section);view.Render();Layout(view);var content=(StackPanel)view.FindName("DocumentContent");Walk<Button>(content).Single(b=>Equals(b.Content,"新增")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Layout(view);var modal=(StackPanel)view.FindName("ModalContent");Walk<TextBox>(modal).Single(b=>AutomationProperties.GetName(b)=="名称").Text="合成"+section;
            void Field(string label,string value){var field=Walk<TextBox>(modal).SingleOrDefault(b=>AutomationProperties.GetName(b)==label);if(field is not null&&!field.IsReadOnly)field.Text=value;}
            if(section=="设备")Field("型号","Synthetic Camera A");if(section=="人员"){Field("角色","摄影助理");Field("到场时间","08:30");}
            if(section=="场地"){Field("地址","合成棚A入口");Field("时区ID","Asia/Shanghai");Field("数据来源","Synthetic manual");}
            if(section=="当天时间表"){Field("开始时间","09:00");Field("结束时间","09:15");Field("负责人","合成助理");Field("时区ID","Asia/Shanghai");}
            if(section=="预算")Field("预算金额","500");if(section=="灯位图"){Field("横向位置 0–1","0.25");Field("功率百分比","65");}
            Walk<ListBox>(modal).Single().SelectedItems.Add(vm.Shots.Single(s=>s.ShotId==shot));
            Walk<Button>(modal).Single(b=>Equals(b.Content,"保存")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));await Settled(vm,view);Assert.AreEqual(Visibility.Collapsed,((FrameworkElement)view.FindName("ModalOverlay")).Visibility,section+" "+vm.SaveStatus);
            var resource=vm.ResourceCatalog.Items.Last();Assert.AreEqual(shot,resource.ShotIds.Single());
            var resourceTable=Walk<DataGrid>((StackPanel)view.FindName("DocumentContent")).Single();resourceTable.SelectedItem=resource;Layout(view);Walk<Button>((StackPanel)view.FindName("PlanningInspectorContent")).Single(b=>Equals(b.Content,"编辑当前对象")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Layout(view);modal=(StackPanel)view.FindName("ModalContent");Walk<TextBox>(modal).Single(b=>AutomationProperties.GetName(b)=="名称").Text="已编辑"+section;
            Walk<Button>(modal).Single(b=>Equals(b.Content,"保存")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));await Settled(vm,view);Assert.AreEqual("已编辑"+section,vm.ResourceCatalog.Items.Single(x=>x.Id==resource.Id).Name);created.Add(new{section,resource.Id,shotIds=resource.ShotIds,create="WPF resource modal",edit="WPF inspector modal",save=true});
        }
        var prop=vm.ResourceCatalog.Items.OfType<PlanningProp>().Single();Assert.IsTrue(await vm.CopyResourceAsync(prop.Id));var copy=vm.ResourceCatalog.Items.Single(r=>r.Id!=prop.Id&&r is PlanningProp);Assert.IsTrue(await vm.UndoPlanningAsync());Assert.IsFalse(vm.ResourceCatalog.Items.Any(r=>r.Id==copy.Id));Assert.IsTrue(await vm.RedoPlanningAsync());Assert.IsTrue(await vm.ArchiveResourceAsync(copy.Id,true));Assert.IsTrue(await vm.UndoPlanningAsync());Assert.IsFalse(vm.ResourceCatalog.Items.Single(r=>r.Id==copy.Id).Archived);Assert.IsTrue(await vm.RedoPlanningAsync());
        var booking=await bookings.SaveAsync(new ShootBookingDraft{Title="合成拍摄档期",StartAt=new(2026,10,21,9,0,0,TimeSpan.FromHours(8)),EndAt=new(2026,10,21,12,0,0,TimeSpan.FromHours(8)),TimeZoneId="Asia/Shanghai",Location="合成棚A"});Assert.IsNotNull(booking.Booking);await vm.ShowBookingCommand.ExecuteAsync(null);vm.SelectedBooking=vm.AvailableBookings.Single(b=>b.Id==booking.Booking.Id);await vm.LinkBookingCommand.ExecuteAsync(null);Assert.AreEqual(project,(await bookings.GetAsync(booking.Booking.Id))!.ProjectId);
        var financeRepo=new SqliteFinanceRepository(db);var finance=new FinanceService(financeRepo);var category=(await finance.ListCategoriesAsync()).First(c=>c.Kind==FinanceTransactionKind.Expense);var expense=await finance.SaveAsync(new FinanceTransaction{Kind=FinanceTransactionKind.Expense,CategoryId=category.Id,ProjectId=project,AmountMinor=12345,PaymentStatus=FinancePaymentStatus.Paid,Note="合成道具支出"});var ledgerBefore=JsonSerializer.Serialize(await finance.QueryAsync(new(ProjectId:project)));vm.ReadPlanningExpenses=id=>finance.QueryAsync(new(ProjectId:id));await vm.RefreshPlanningExpensesAsync();Assert.AreEqual(expense.Id,vm.PlanningExpenses.Single().Id);Assert.AreEqual(ledgerBefore,JsonSerializer.Serialize(await finance.QueryAsync(new(ProjectId:project))));
        var invalidSchedule=vm.ResourceCatalog.Items.OfType<PlanningScheduleSlot>().Single();Assert.IsFalse(await vm.SaveResourceAsync(invalidSchedule with{StartLocal="23:00",EndLocal="01:00"}));
        var image=PlanningDocumentImages.Copy(vm.DocumentImageDirectory,document,await File.ReadAllBytesAsync(original),null,"正文","合成正文图","不可修改原图",0,shot);Assert.IsTrue(await vm.InsertSimpleImagesAsync([image]));Assert.IsFalse(Path.IsPathRooted(vm.DocumentImages.Single().CopyPath));
        var planDocx=Path.Combine(root,"same-plan.docx");Assert.IsTrue(await vm.ExportPlanningDocxAsync(planDocx));var planPdf=Path.Combine(root,"same-plan.pdf");PlanningProposalPdf.Export(vm,planPdf,216);
        using(var package=WordprocessingDocument.Open(planDocx,false)){var xml=package.MainDocumentPart!.Document.InnerText;StringAssert.Contains(xml,"01白纱开场");Assert.IsFalse(xml.Contains("已编辑设备"),"Current rich document export does not promise all resource tables");}
        // Save rejection retains actual pending draft, then a new VM recovers and retries.
        var planPath=Path.Combine(planRoot,$"{project:N}.planning.json");var saved=await File.ReadAllBytesAsync(planPath);using(var held=new FileStream(planPath,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){vm.DocumentCustomBody="失败后应恢复的收尾事项";Assert.IsFalse(await vm.FlushAsync());Assert.IsNotNull(await new PlanningProjectStore(planRoot).LoadDraftAsync(project));}
        CollectionAssert.AreEqual(saved,await File.ReadAllBytesAsync(planPath));view.DataContext=null;var recovered=Vm(planRoot,shotRoot);await recovered.LoadAsync(project);Assert.AreEqual("失败后应恢复的收尾事项",recovered.DocumentCustomBody);Assert.IsTrue(await recovered.FlushAsync());
        var sectionResults=new List<object>();foreach(var section in PlanningCenterViewModel.ContentPages){await recovered.SelectContentSafelyAsync(section);var live=new PlanningCenterView{DataContext=recovered};Layout(live);Assert.IsTrue(((StackPanel)live.FindName("DocumentContent")).Children.Count>0);live.DataContext=null;sectionResults.Add(new{section,projectId=project,documentId=document,saveReopen="PASS scoped data",mouse="NOT_RUN"});}
        // Relocate owned planning/shot data, leave original reference/attachment in place.
        var moved=Path.Combine(root,"moved");Directory.CreateDirectory(moved);Directory.Move(planRoot,Path.Combine(moved,"planning"));Directory.Move(shotRoot,Path.Combine(moved,"shots"));var reopened=Vm(Path.Combine(moved,"planning"),Path.Combine(moved,"shots"));await reopened.LoadAsync(project);
        Assert.AreEqual(document,reopened.PlanningDocumentId);Assert.AreEqual(8,reopened.ResourceCatalog.Items.Count);Assert.AreEqual(shot,reopened.Shots.Single().ShotId);Assert.AreEqual(referenceId,reopened.Shots.Single().References.Single().ReferenceId);Assert.IsTrue(reopened.ResourceCatalog.Items.Where(r=>r.ShotIds.Length>0).All(r=>r.ShotIds.Single()==shot));Assert.AreEqual("失败后应恢复的收尾事项",reopened.DocumentCustomBody);Assert.IsTrue(File.Exists(PlanningImageStorage.Resolve(reopened.DocumentImages.Single())));Assert.AreEqual(hash,Hash(original));Assert.AreEqual(attachmentHash,Hash(attachment));
        var finalState=await new PlanningProjectStore(Path.Combine(moved,"planning")).LoadAsync(project);Assert.AreEqual(booking.Booking.Id,finalState.BookingId);Assert.AreEqual(attachment,reopened.Attachments.Single().Path);Assert.AreEqual("批次3 · 编辑后的提案",reopened.DocumentTitle);Assert.AreEqual("逆光人像",reopened.ShootGoal);StringAssert.Contains(reopened.DocumentBody,"本轮合成正文");Assert.IsTrue(reopened.AllProjectReferences.Single(r=>r.Id==referenceId).IsMoodboard);
        Assert.AreEqual("Synthetic Camera A",reopened.ResourceCatalog.Items.OfType<PlanningGearItem>().Single().Model);Assert.AreEqual("摄影助理",reopened.ResourceCatalog.Items.OfType<PlanningCrewMember>().Single().Role);Assert.AreEqual(500m,reopened.ResourceCatalog.Items.OfType<PlanningBudgetLine>().Single().Planned);Assert.AreEqual("09:15",reopened.ResourceCatalog.Items.OfType<PlanningScheduleSlot>().Single().EndLocal);
        await File.WriteAllTextAsync(Path.Combine(root,"RESULT.json"),JsonSerializer.Serialize(new{project,document,shot,referenceId,bookingId=booking.Booking.Id,expenseId=expense.Id,resources=created,sections=sectionResults,crossLinks=true,copyUndoRedoArchive=true,ledgerReadOnly=true,exportDocxSha256=Hash(planDocx),exportPdfSha256=Hash(planPdf),exportScope="Rich body/owned images/shot table only; all resource tables not included",invalidCrossDayScheduleRejected=true,draftRecovered=true,planningRootMoved=true,relativeOwnedImages=true,externalReferencesAndAttachments="Original absolute paths, not made portable",sourceOriginalHash=hash,sourceUnchanged=true,attachmentOriginalHash=attachmentHash,mouse="NOT_RUN",syntheticOnly=true},new JsonSerializerOptions{WriteIndented=true}));SqliteConnection.ClearAllPools();
    });

    [TestMethod]public Task DeclaredRtfDocxPdfScopeKeepsImageTableHeaderAndSourceUnchanged()=>RunSta(async()=>
    {
        EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);await f.Vm.NewShotCommand.ExecuteAsync(null);var root=Fresh("formats");
        var image=PlanningDocumentImages.Copy(f.Vm.DocumentImageDirectory,f.Vm.PlanningDocumentId!.Value,PlanningRichFlowFixture.ReferencePng(1),null,"正文","合成图片","图注",0);Assert.IsTrue(await f.Vm.InsertSimpleImagesAsync([image]));var doc=PlanningDocumentLayout.Load(f.Vm.RichTextPackage!,f.Vm.RichLayout);doc.Blocks.InsertBefore(doc.Blocks.FirstBlock,new Paragraph(new Run("批次3正文段落")));doc.Blocks.Add(PlanningR7TableTests.Make(2,2));doc.Tag=new PlanningHeaderFooter{Header="合成策划页眉",Footer="内部审阅",PageNumbers=true,DifferentFirstPage=true,FirstHeader="合成首页"};
        var layout=PlanningDocumentLayout.Capture(doc,new(),[]);f.Vm.SaveRichText(PlanningDocumentImages.Serialize(doc),new TextRange(doc.ContentStart,doc.ContentEnd).Text,layout);Assert.IsTrue(await f.Vm.FlushAsync());
        var rtf=Path.Combine(root,"proposal.rtf");await File.WriteAllBytesAsync(rtf,Convert.FromBase64String(f.Vm.RichTextPackage!));var originalRtf=Hash(rtf);var restored=new PlanningRichEditor(f.Vm.RichTextPackage!,f.Vm.DocumentBody,(_,_,_)=>{},[],f.Vm.RichLayout);var box=Walk<RichTextBox>(restored).Single();Assert.AreEqual(1,PlanningDocumentImages.Images(box.Document).Count());Assert.HasCount(1,box.Document.Blocks.OfType<Table>().ToArray());Assert.AreEqual("合成策划页眉",((PlanningHeaderFooter)box.Document.Tag).Header);
        var path=Path.Combine(root,"proposal.docx");Assert.IsTrue(await f.Vm.ExportPlanningDocxAsync(path));var inputHash=Hash(path);
        using(var pkg=WordprocessingDocument.Open(path,false)){Assert.IsEmpty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(pkg));Assert.IsGreaterThanOrEqualTo(1,pkg.MainDocumentPart!.ImageParts.Count());Assert.AreEqual(2,pkg.MainDocumentPart.Document.Body!.Elements<W.Table>().Count());Assert.AreEqual(2,pkg.MainDocumentPart.HeaderParts.Count());}
        var review=f.Vm.PreviewPlanningDocxImport(path);Assert.IsTrue(await f.Vm.ApplyPlanningDocxImportAsync(review,[],true));Assert.AreEqual(inputHash,Hash(path));Assert.AreEqual(originalRtf,Hash(rtf));StringAssert.Contains(f.Vm.DocumentBody,"批次3正文段落");Assert.IsGreaterThanOrEqualTo(1,f.Vm.DocumentImages.Count);Assert.AreEqual("合成策划页眉",f.Vm.RichLayout!.HeaderFooter.Header);
        var pdf=Path.Combine(root,"proposal.pdf");PlanningProposalPdf.Export(f.Vm,pdf,216);var text=await File.ReadAllTextAsync(pdf);StringAssert.StartsWith(text,"%PDF");StringAssert.Contains(text,"/Count ");StringAssert.Contains(text,"/Subtype /Image");
        await File.WriteAllTextAsync(Path.Combine(root,"RESULT.json"),JsonSerializer.Serialize(new{project=f.Id,document=f.Vm.PlanningDocumentId,rtfHash=originalRtf,docxHash=inputHash,pdfHash=Hash(pdf),rtf="Actual persisted RTF + companion PlanningRichLayout; no standalone file-import UI claimed",docx="Production ExportPlanningDocxAsync/Preview/Apply, one body table + generated shot table, image, header/footer",pdf="Production image-based PDF, not editable/searchable roundtrip",sourceFilesUnchanged=true,externalWordWps="NOT_RUN",complexSections="PARTIAL unsupported",arbitraryInlineStructure="PARTIAL",mouse="NOT_RUN"},new JsonSerializerOptions{WriteIndented=true}));restored.Retire();
    });
    [TestMethod][DataRow("灯位图","灯光")][DataRow("服化道与道具","造型")]
    public Task CurrentSectionReferenceClassificationAndFilterUseCurrentNames(string section,string expected)=>RunSta(async()=>
    {
        EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);
        var image=Path.Combine(f.Root,"synthetic.png");await File.WriteAllBytesAsync(image,PlanningRichFlowFixture.ReferencePng(2));await f.Vm.SelectContentSafelyAsync(section);
        var own=Guid.NewGuid();await f.Vm.AddDocumentReferencesAsync([new(own,ShotReferenceKind.General,ExternalReference:image,Title:"本分区引用")]);
        Assert.AreEqual(expected,f.Vm.AllProjectReferences.Single(r=>r.Id==own).Category,"Adding a reference from the actual current section must classify it for that section");
        var other=Guid.NewGuid();await f.Vm.AddDocumentReferencesAsync([new(other,ShotReferenceKind.General,ExternalReference:image,Title:"其他分区引用")]);await f.Vm.UpdateReferenceAsync(f.Vm.AllProjectReferences.Single(r=>r.Id==other),"分类",expected=="灯光"?"造型":"灯光");
        CollectionAssert.AreEquivalent(new[]{own},f.Vm.VisibleDocumentReferences.Select(r=>r.Id).ToArray());
        Assert.IsTrue(await f.Vm.FlushAsync());await f.Vm.LoadAsync(f.Id);f.Vm.ContentPage=section;Assert.AreEqual(expected,f.Vm.AllProjectReferences.Single(r=>r.Id==own).Category);
    });
    static string Fresh(string name){var r=Environment.GetEnvironmentVariable("KITAO_PLANNING3_OUTPUT")??Path.GetTempPath();var p=Path.Combine(r,name+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);return p;}
    static string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
    static IEnumerable<T> Walk<T>(DependencyObject root)where T:DependencyObject{if(root is T item)yield return item;for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Walk<T>(System.Windows.Media.VisualTreeHelper.GetChild(root,i)))yield return child;}
    static void Layout(FrameworkElement v){v.Measure(new Size(1600,1000));v.Arrange(new Rect(0,0,1600,1000));v.UpdateLayout();}
    static async Task Settled(PlanningCenterViewModel vm,PlanningCenterView view){for(int i=0;i<20&&((FrameworkElement)view.FindName("ModalOverlay")).Visibility==Visibility.Visible;i++)await Task.Delay(40);await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);Layout(view);Assert.IsTrue(await vm.FlushAsync());}
}
