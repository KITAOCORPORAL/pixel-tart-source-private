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
        Assert.HasCount(12,table.Columns);
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
    private sealed class Fixture : IDisposable
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
    private sealed class Dialogs:IDialogService
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
