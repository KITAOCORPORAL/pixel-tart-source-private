using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class PlanningFoundationWpfTests
{
    [TestMethod]
    public Task EditableRowsCancelAndRoundTripReorderCopyArchiveAndColumns() => RunSta(async () =>
    {
        using var f=await Fixture.Create(); var vm=f.Vm; await vm.LoadAsync(f.Id);
        await vm.NewShotCommand.ExecuteAsync(null);var first=vm.SelectedShot!.ShotId;
        var row=new PlanningShotEditRow(vm.ShotDraft(first),vm.StageShot);row.BeginEdit();row["Name"]="取消的改动";row.CancelEdit();
        Assert.AreEqual("未命名拍摄",row["Name"]);Assert.IsTrue(await vm.FlushAsync());
        row.BeginEdit();row["Name"]="镜头一";row["Action"]="转身";row["Remarks"]="备注";row["ShotSize"]="近景";row["CameraAndLens"]="85mm";row["Props"]="椅子";row["Lighting"]="侧光";row.EndEdit();Assert.IsTrue(await vm.FlushAsync());
        Assert.IsTrue(await vm.AddShotColumnAsync("服装"));var column=vm.ShotColumns.Single();
        row=new(vm.ShotDraft(first),vm.StageShot);row.BeginEdit();row["custom:"+column.Id.ToString("N")]="A01";row.EndEdit();Assert.IsTrue(await vm.FlushAsync());
        vm.SelectedShot=vm.Shots.Single();await vm.DuplicateShotCommand.ExecuteAsync(null);var copy=vm.SelectedShot!.ShotId;
        Assert.AreNotEqual(first,copy);Assert.AreEqual("A01",vm.SelectedShot.CustomValues["custom:"+column.Id.ToString("N")]);
        await vm.MoveShotUpCommand.ExecuteAsync(null);Assert.AreEqual(copy,vm.Shots[0].ShotId);
        f.Dialogs.Accept=false;await vm.ArchiveShotCommand.ExecuteAsync(null);Assert.HasCount(2,vm.Shots);
        f.Dialogs.Accept=true;await vm.ArchiveShotCommand.ExecuteAsync(null);Assert.HasCount(1,vm.Shots);
        Assert.IsTrue(await vm.SetShotColumnArchivedAsync(column.Id,true));Assert.IsEmpty(vm.ShotColumns);
        vm.DocumentCustomBody="原自定义内容";vm.ShootGoal="项目概览目标";Assert.IsTrue(await vm.FlushAsync());var documentId=vm.PlanningDocumentId;
        await vm.LoadAsync(f.Id);Assert.AreEqual(documentId,vm.PlanningDocumentId);Assert.AreEqual("原自定义内容",vm.DocumentCustomBody);
        Assert.IsTrue(await vm.SetShotColumnArchivedAsync(column.Id,false));
        var saved=(await f.Shots.LoadAsync(f.Id)).Shots;Assert.IsTrue(saved.Single(s=>s.ShotId==copy).IsArchived);
        var original=saved.Single(s=>s.ShotId==first);Assert.AreEqual("转身",original.Action);Assert.AreEqual("备注",original.Remarks);Assert.AreEqual("近景",original.ShotSize);
        Assert.AreEqual("A01",original.CustomValues["custom:"+column.Id.ToString("N")]);
        EnsureTestApplication();var view=new PlanningCenterView{DataContext=vm};await vm.SelectContentSafelyAsync("镜头清单");view.Render();
        var table=((StackPanel)view.FindName("DocumentContent")).Children.OfType<DataGrid>().Single();
        Assert.HasCount(13,table.Columns);Assert.HasCount(1,table.Columns.Where(c=>Equals(c.Header,"参考")));
        // Exercise the production grid commit callback, not only the row helper.
        table.Measure(new Size(1100, 400)); table.Arrange(new Rect(0, 0, 1100, 400)); table.UpdateLayout();
        var boundRow = (PlanningShotEditRow)table.Items[0]; boundRow.BeginEdit(); boundRow["Name"] = "表格提交"; boundRow.EndEdit();
        Assert.IsTrue(await vm.FlushAsync()); Assert.AreEqual("表格提交", (await f.Shots.LoadAsync(f.Id)).Shots.Single(s => !s.IsArchived).Name);Assert.AreEqual("可编辑镜头清单",System.Windows.Automation.AutomationProperties.GetName(table));
        view.DataContext=null;
    });
    [TestMethod]
    public Task InvalidDraftAndIoFailureBlockSwitchAndRecoverAcrossNewWorkspace() => RunSta(async () =>
    {
        using var f=await Fixture.Create();await f.Vm.LoadAsync(f.Id);await f.Vm.NewShotCommand.ExecuteAsync(null);
        var id=f.Vm.SelectedShot!.ShotId;var docId=f.Vm.PlanningDocumentId;
        f.Vm.StageShot(f.Vm.ShotDraft(id) with{Name=""});f.Vm.DocumentCustomBody="不能丢的草稿";
        var page=f.Vm.ContentPage;Assert.IsFalse(await f.Vm.SelectContentSafelyAsync("项目概览"));Assert.AreEqual(page,f.Vm.ContentPage);
        Assert.IsNotNull(await f.Planning.LoadDraftAsync(f.Id));
        var recovered=f.NewVm();await recovered.LoadAsync(f.Id);Assert.AreEqual("不能丢的草稿",recovered.DocumentCustomBody);Assert.AreEqual(docId,recovered.PlanningDocumentId);
        recovered.StageShot(recovered.ShotDraft(id) with{Name="修复后的镜头"});Assert.IsTrue(await recovered.FlushAsync());
        // Keep the first VM from a delayed autosave; cancel flush never clears its pending state.
        f.Vm.StageShot(f.Vm.ShotDraft(id) with{Name="修复后的镜头"});Assert.IsTrue(await f.Vm.FlushAsync());
        var file=Path.Combine(f.ShotDirectory,$"{f.Id:N}.shots.json");
        using(var held=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        { recovered.StageShot(recovered.ShotDraft(id) with{Name="IO恢复"});Assert.IsFalse(await recovered.FlushAsync());Assert.IsNotNull(await f.Planning.LoadDraftAsync(f.Id)); }
        Assert.IsTrue(await recovered.FlushAsync());Assert.AreEqual("IO恢复",(await f.Shots.LoadAsync(f.Id)).Shots.Single().Name);
        var planFile = Path.Combine(f.Root, "planning", $"{f.Id:N}.planning.json");
        using (var held = new FileStream(planFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            recovered.StageShot(recovered.ShotDraft(id) with { Name = "跨文件恢复" }); recovered.DocumentCustomBody = "文档写入被阻止";
            Assert.IsFalse(await recovered.FlushAsync());
            Assert.AreEqual("跨文件恢复", (await f.Shots.LoadAsync(f.Id)).Shots.Single().Name);
            Assert.AreEqual("文档写入被阻止", (await f.Planning.LoadDraftAsync(f.Id))!.Document.CustomBody);
        }
        var afterCrash = f.NewVm(); await afterCrash.LoadAsync(f.Id); Assert.AreEqual("文档写入被阻止", afterCrash.DocumentCustomBody);
        Assert.IsTrue(await afterCrash.FlushAsync()); Assert.IsTrue(await recovered.FlushAsync());
        recovered.DocumentBody="取消保存的正文";using var c=new CancellationTokenSource();c.Cancel();Assert.IsFalse(await recovered.FlushAsync(c.Token));
        Assert.AreEqual("取消保存的正文",recovered.DocumentBody);Assert.IsTrue(await recovered.FlushAsync());
    });
    [TestMethod]
    public Task ShellFlushCapturesUncommittedRowAndInvalidTextRemainsRecoverable() => RunSta(async () =>
    {
        using var f = await Fixture.Create(); await f.Vm.LoadAsync(f.Id); await f.Vm.NewShotCommand.ExecuteAsync(null);
        var shot = f.Vm.SelectedShot!; var row = new PlanningShotEditRow(shot, f.Vm.StageShot);
        row.BeginEdit(); row["Name"] = "未离开单元格"; row["EstimatedMinutes"] = "不是数字";
        f.Vm.CapturePendingEditorDraft = row.EndEdit;
        Assert.IsFalse(await f.Vm.FlushAsync());
        var draft = (await f.Planning.LoadDraftAsync(f.Id))!.Shots!.Single();
        Assert.AreEqual("未离开单元格", draft.Name);
        var recovered = new PlanningShotEditRow(draft, f.Vm.StageShot);
        Assert.AreEqual("不是数字", recovered["EstimatedMinutes"]);
        recovered.BeginEdit(); recovered["EstimatedMinutes"] = "12"; recovered.EndEdit();
        f.Vm.CapturePendingEditorDraft = null; Assert.IsTrue(await f.Vm.FlushAsync());
        f.Vm.CapturePendingEditorDraft = () => throw new InvalidOperationException("synthetic cell validation");
        var page = f.Vm.ContentPage; Assert.IsFalse(await f.Vm.SelectContentSafelyAsync("项目概览")); Assert.AreEqual(page, f.Vm.ContentPage);
        f.Vm.CapturePendingEditorDraft = null;
    });
    [TestMethod]
    public Task ResourcesRoundTripUndoArchiveDuplicateAndReadOnlyLedger() => RunSta(async () =>
    {
        using var f = await Fixture.Create(); await f.Vm.LoadAsync(f.Id); await f.Vm.NewShotCommand.ExecuteAsync(null);
        var shot=f.Vm.SelectedShot!.ShotId;
        PlanningResource[] resources=[new PlanningProp(),new PlanningGearItem(),new PlanningCrewMember(),new PlanningVenue(),new PlanningScheduleSlot(),new PlanningBudgetLine(),new PlanningLight{X=.2,Y=.8,DirectionDegrees=45,PowerPercent=30,ReferencePath="synthetic-reference.png"}];
        foreach(var r in resources)Assert.IsTrue(await f.Vm.SaveResourceAsync(r with{ShotIds=[shot]}));
        Assert.IsTrue(await f.Vm.CopyResourceAsync(resources[0].Id));Assert.HasCount(8,f.Vm.ResourceCatalog.Items);
        Assert.IsTrue(await f.Vm.UndoPlanningAsync());Assert.HasCount(7,f.Vm.ResourceCatalog.Items);
        Assert.IsTrue(await f.Vm.ArchiveResourceAsync(resources[0].Id,true));Assert.IsTrue(f.Vm.ResourceCatalog.Items[6].Archived);
        await f.Vm.LoadAsync(f.Id);Assert.HasCount(7,f.Vm.ResourceCatalog.Items);
        var light=f.Vm.ResourceCatalog.Items.OfType<PlanningLight>().Single();Assert.AreEqual(.2,light.X);Assert.AreEqual(45d,light.DirectionDegrees);Assert.AreEqual(shot,light.ShotIds.Single());
        var gear=new PlanningGearItem{SourceCatalog="synthetic-toolbox",SourceId="gear-1",Name="合成设备"};
        Assert.IsTrue(await f.Vm.ImportPlanningGearAsync([gear,gear]));Assert.HasCount(2,f.Vm.ResourceCatalog.Items.OfType<PlanningGearItem>());
        var transaction=new FinanceTransaction{ProjectId=f.Id,Kind=FinanceTransactionKind.Expense,AmountMinor=12345};
        f.Vm.ReadPlanningExpenses=_=>Task.FromResult<IReadOnlyList<FinanceTransaction>>([transaction,transaction,transaction with{Id=Guid.NewGuid(),ProjectId=Guid.NewGuid()}]);
        await f.Vm.RefreshPlanningExpensesAsync();Assert.HasCount(1,f.Vm.PlanningExpenses);Assert.AreEqual(12345L,f.Vm.PlanningExpenses[0].AmountMinor);
        Assert.IsFalse(await f.Vm.SaveResourceAsync(light with{X=double.NaN}));
        var file=Path.Combine(f.Root,"planning",$"{f.Id:N}.planning.json");
        using(var held=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Assert.IsFalse(await f.Vm.SaveResourceAsync(light with{Name="失败草稿"}));
        Assert.IsNotNull(await f.Planning.LoadDraftAsync(f.Id));Assert.IsTrue(await f.Vm.FlushAsync());
    });
    [TestMethod]
    public Task ModesKeepIdentityContentAndSourcesWithSingleUndoAndFailureGate() => RunSta(async()=>
    {
        using var f=await Fixture.Create();await f.Vm.LoadAsync(f.Id);await f.Vm.NewShotCommand.ExecuteAsync(null);var id=f.Vm.SelectedShot!.ShotId;
        f.Vm.DocumentBody="普通正文";f.Vm.DocumentCustomBody="普通文档内容";Assert.IsTrue(await f.Vm.FlushAsync());var doc=f.Vm.PlanningDocumentId;
        Assert.IsTrue(await f.Vm.SwitchPlanningModeAsync(true));var blocks=f.Vm.StructureBlocks.Select(b=>b.Id).ToArray();
        Assert.AreEqual(doc,f.Vm.PlanningDocumentId);Assert.IsTrue(f.Vm.StructureBlocks[0].SourceIds.Contains(id));
        Assert.IsTrue(await f.Vm.ApplyStructureShotAsync(f.Vm.ShotDraft(id) with{Name="同步结构"}));Assert.AreEqual("同步结构",f.Vm.Shots[0].Name);
        Assert.IsTrue(await f.Vm.UndoPlanningAsync());Assert.AreEqual("未命名拍摄",f.Vm.Shots[0].Name);
        Assert.IsTrue(await f.Vm.SwitchPlanningModeAsync(false));Assert.IsTrue(await f.Vm.SwitchPlanningModeAsync(true));CollectionAssert.AreEqual(blocks,f.Vm.StructureBlocks.Select(b=>b.Id).ToArray());
        Assert.IsTrue(await f.Vm.RemoveStructureBlockAsync(blocks[0]));Assert.HasCount(1,f.Vm.Shots);Assert.IsTrue(await f.Vm.UndoPlanningAsync());
        await f.Vm.LoadAsync(f.Id);Assert.IsTrue(f.Vm.IsAdvancedPlanning);Assert.AreEqual(doc,f.Vm.PlanningDocumentId);Assert.AreEqual("普通正文",f.Vm.DocumentBody);Assert.AreEqual("普通文档内容",f.Vm.DocumentCustomBody);
        f.Vm.StageShot(f.Vm.ShotDraft(id) with{Name=""});Assert.IsFalse(await f.Vm.SwitchPlanningModeAsync(false));Assert.IsTrue(f.Vm.IsAdvancedPlanning);
        f.Vm.StageShot(f.Vm.ShotDraft(id) with{Name="修复"});Assert.IsTrue(await f.Vm.FlushAsync());
    });
    [TestMethod]
    public Task DocxConfirmDiffRecoveryUndoAndRepeatedImportAreReal() => RunSta(async()=>
    {
        using var f=await Fixture.Create();await f.Vm.LoadAsync(f.Id);await f.Vm.NewShotCommand.ExecuteAsync(null);f.Vm.DocumentBody="导出正文";Assert.IsTrue(await f.Vm.FlushAsync());
        var path=Path.Combine(f.Root,"proposal.docx");Assert.IsTrue(await f.Vm.ExportPlanningDocxAsync(path));
        using(var package=DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(path,true))
        {
            var table=package.MainDocumentPart!.Document.Body!.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>().Last();
            var cell=table.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>().Skip(1).First().Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>().Skip(1).First();
            cell.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().First().Text="DOCX修改镜头";package.MainDocumentPart.Document.Save();
        }
        var review=f.Vm.PreviewPlanningDocxImport(path);Assert.HasCount(1,review.Differences);var id=review.Differences[0].ShotId;
        Assert.IsFalse(await f.Vm.ApplyPlanningDocxImportAsync(review,[id],false));Assert.AreEqual("未命名拍摄",f.Vm.Shots[0].Name);
        Assert.IsTrue(await f.Vm.ApplyPlanningDocxImportAsync(review,[id],true));Assert.AreEqual("DOCX修改镜头",f.Vm.Shots[0].Name);
        Assert.HasCount(1,Directory.GetFiles(Path.Combine(f.Root,"planning"),"*.docx-recovery-*.json"));
        var again=f.Vm.PreviewPlanningDocxImport(path);Assert.IsEmpty(again.Differences);Assert.HasCount(1,f.Vm.Shots);
        Assert.IsTrue(await f.Vm.UndoPlanningAsync());Assert.AreEqual("未命名拍摄",f.Vm.Shots[0].Name);
        var stale=f.Vm.PreviewPlanningDocxImport(path);f.Vm.DocumentCustomBody="新编辑";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Vm.ApplyPlanningDocxImportAsync(stale,[id],true));Assert.IsTrue(await f.Vm.FlushAsync());
    });
    [TestMethod]
    public Task NestedListDocxUsesRealLevelsAndPreservesHierarchy() => RunSta(async()=>
    {
        var document=new System.Windows.Documents.FlowDocument();var list=new System.Windows.Documents.List{MarkerStyle=TextMarkerStyle.Decimal};
        var item=new System.Windows.Documents.ListItem(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("一级")));
        item.Blocks.Add(new System.Windows.Documents.List(new System.Windows.Documents.ListItem(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("二级")))){MarkerStyle=TextMarkerStyle.Disc});
        list.ListItems.Add(item);list.ListItems.Add(new System.Windows.Documents.ListItem(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("另一个一级"))));document.Blocks.Add(list);
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".docx");
        try
        {
            PlanningDocxAdapter.Export(document,path);
            using(var package=DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(path,false))Assert.IsEmpty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(package).ToArray());
            var imported=PlanningDocxAdapter.Import(path);var restored=imported.Document.Blocks.OfType<System.Windows.Documents.List>().Single();
            Assert.HasCount(2,restored.ListItems);Assert.HasCount(1,restored.ListItems.FirstListItem.Blocks.OfType<System.Windows.Documents.List>());
            Assert.AreEqual(TextMarkerStyle.Decimal,restored.MarkerStyle);
        }finally{File.Delete(path);}await Task.CompletedTask;
    });
    [TestMethod]
    public Task AllFifteenSectionsAndModesConstructAtNarrowWidthsWithoutDesktop() => RunSta(async()=>
    {
        using var f=await Fixture.Create();await f.Vm.LoadAsync(f.Id);EnsureTestApplication();
        var view=new PlanningCenterView{DataContext=f.Vm};
        foreach(var page in PlanningCenterViewModel.ContentPages)
        {
            Assert.IsTrue(await f.Vm.SelectContentSafelyAsync(page));view.Render();
            view.Measure(new Size(860,720));view.Arrange(new Rect(0,0,860,720));view.UpdateLayout();
            Assert.IsTrue(((StackPanel)view.FindName("DocumentContent")).Children.Count>0,page);
        }
        Assert.IsTrue(await f.Vm.SwitchPlanningModeAsync(true));view.Render();
        view.Measure(new Size(1100,720));view.Arrange(new Rect(0,0,1100,720));view.UpdateLayout();
        Assert.IsTrue(await f.Vm.SwitchPlanningModeAsync(false));view.DataContext=null;
    });
    internal sealed class Fixture : IDisposable
    {
        public string Root=Path.Combine(Path.GetTempPath(),"kitao-planning-foundation-"+Guid.NewGuid().ToString("N"));
        public Guid Id=Guid.NewGuid();public string ShotDirectory=>Path.Combine(Root,"shots");
        public ProjectShotStore Shots=null!;public PlanningProjectStore Planning=null!;public PlanningCenterViewModel Vm=null!;
        public SqliteProjectRepository Projects=null!;public ShootBookingService Bookings=null!;public Dialogs Dialogs=new();
        public static async Task<Fixture> Create()
        {
            var f=new Fixture();Directory.CreateDirectory(f.Root);var db=new PixelTartDatabase(Path.Combine(f.Root,"synthetic.db"));
            var migration=await new DatabaseMigrator(db,new DatabaseBackupService(db,Path.Combine(f.Root,"backup"))).MigrateAsync();Assert.IsTrue(migration.Success);
            f.Projects=new(db);await f.Projects.UpsertAsync(new PhotoProjectRecord{Id=f.Id,Name="合成策划"});
            var bookings=new SqliteShootBookingRepository(db);f.Bookings=new(bookings,new BookingConflictDetector(bookings));
            f.Shots=new(f.ShotDirectory);f.Planning=new(Path.Combine(f.Root,"planning"));f.Vm=f.NewVm();return f;
        }
        public PlanningCenterViewModel NewVm()=>new(Projects,Bookings,Dialogs,Shots,Planning,new ProjectVisualReferenceStore(Path.Combine(Root,"visuals")),new ReferenceLookStore(Path.Combine(Root,"looks")),new CanvasDocumentStore(Path.Combine(Root,"canvas")));
        public void Dispose(){SqliteConnection.ClearAllPools();try{Directory.Delete(Root,true);}catch(IOException){}}
    }
    internal sealed class Dialogs:IDialogService
    {
        public bool Accept=true;
        public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect)=>[];
        public string? ChooseFolder(string title,string? initialDirectory=null)=>null;
        public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds)=>currentToolIds;
        public void ShowInfo(string message){}public void ShowError(string message){}
        public bool Confirm(string message,string title)=>Accept;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}
        public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates)=>null;
        public bool ShowMediaDetails(MediaSelectionItem item,bool showAdvancedDetails)=>false;public void RevealFile(string path){}
    }
}
