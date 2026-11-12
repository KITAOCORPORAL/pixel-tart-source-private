using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;
using PixelTart.Modules.AssetLibrary.FreeCanvas;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class CompanyCanvasWorkflowTests
{
    private static string Root => Environment.GetEnvironmentVariable("KITAO_COMPANY_WORKFLOW_ROOT") ?? throw new InvalidOperationException("Explicit isolated root required");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private sealed record Receipt(int ProcessId, Guid ProjectId, Guid CanvasId, string LibraryPath, Guid DocumentId, Guid RevisionId, string PreviewHash, string ObjectsJson);
    private static Receipt ReadReceipt()=>JsonSerializer.Deserialize<Receipt>(File.ReadAllText(Path.Combine(Root,"WORKFLOW.json")))!;
    private static string Hash(string png)=>Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(png)));
    private static string FileHash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static IEnumerable<Target> Find<Target>(DependencyObject root) where Target:DependencyObject
    {
        if(root is Target match)yield return match;
        for(var index=0;index<VisualTreeHelper.GetChildrenCount(root);index++)
            foreach(var child in Find<Target>(VisualTreeHelper.GetChild(root,index)))yield return child;
    }
    private static async Task<FreeCanvasView> OpenCanvas(MainWindow window,MainViewModel main,string library,CanvasDocument document)
    {
        main.NavigateCommand.Execute("AssetLibrary");await Task.Delay(150);
        var host=Find<AssetLibraryWorkspaceHost>((DependencyObject)window.Content).Single();
        await host.InitializeForProductHarnessAsync();
        var previous=host.CurrentPage;
        var reopening=host.CurrentDescriptor?.ContainerPath.Equals(library,StringComparison.OrdinalIgnoreCase)==true;
        await host.SwitchToContainerAsync(library);
        if(reopening)Assert.AreSame(previous,host.CurrentPage,"Reopening current library must not replace its live canvas");
        var page=await host.InitializeForProductHarnessAsync();Assert.IsNotNull(page);await page.ShowCanvasAsync(document);await Task.Delay(100);
        return Find<FreeCanvasView>((DependencyObject)window.Content).First(view=>view.IsVisible);
    }
    private static async Task<PlanningCenterView> OpenPlanning(MainWindow window,MainViewModel main,Guid project)
    {
        main.NavigateCommand.Execute("Planning");
        var deadline=DateTime.UtcNow.AddSeconds(20);
        while(main.PlanningPage!.IsPlanningLoading&&DateTime.UtcNow<deadline)await Task.Delay(20);
        await main.PlanningPage!.LoadAsync(project);main.PlanningPage.ContentPage="情绪板";await Task.Delay(100);
        var view=Find<PlanningCenterView>((DependencyObject)window.Content).Single(item=>item.IsVisible);view.Render();return view;
    }
    private static async Task SaveButton(FreeCanvasView view)
    {
        Find<Button>(view).Single(button=>Equals(button.Content,"保存")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert.IsTrue(await view.FlushAsync());
    }
    [TestMethod]
    public void BusyLibraryReportsInlineAndPreservesCurrentCanvas()=>RunHost(async(window,main)=>
    {
        var receipt=ReadReceipt();var store=new CanvasDocumentStore(Path.Combine(AppDataPaths.DataDirectory,"FreeCanvas"));
        var document=(await store.LoadAsync(receipt.CanvasId))!;var canvas=await OpenCanvas(window,main,receipt.LibraryPath,document);
        var host=Find<AssetLibraryWorkspaceHost>((DependencyObject)window.Content).Single();var page=host.CurrentPage;
        var busy=await new AssetLibraryContainerService().CreateAsync(Path.Combine(Root,"busy-"+Guid.NewGuid().ToString("N")+".ptlibrary"),"占用测试");
        await using(var lease=await new AssetLibraryContainerService().AcquireWriteLeaseAsync(busy))
        {
            await host.SwitchToContainerAsync(busy.ContainerPath);Assert.AreSame(page,host.CurrentPage);Assert.AreSame(canvas,page!.ActiveCanvas);
            Assert.IsTrue(Find<TextBlock>(host).Any(text=>text.Text.Contains("另一个光镜实例")&&text.Text.Contains("仍保留当前库")));
        }
        await host.SwitchToContainerAsync(receipt.LibraryPath);Assert.AreSame(page,host.CurrentPage);Assert.IsTrue(await canvas.FlushAsync());
    });
    private static async Task CaptureButton(PlanningCenterView view,PlanningCenterViewModel planning,int count)
    {
        Find<Button>(view).Single(button=>(button.Content as string)?.StartsWith("插入/更新画布版本")==true).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var deadline=DateTime.UtcNow.AddSeconds(30);
        while((planning.IsCanvasCaptureRunning||planning.DocumentCanvasRevisions.Count!=count)&&DateTime.UtcNow<deadline)await Task.Delay(20);
        Assert.AreEqual(count,planning.DocumentCanvasRevisions.Count,planning.SaveStatus);
    }
    private static (string Jpeg,string Png) CreateSyntheticInputs()
    {
        var directory=Path.Combine(Root,"synthetic-inputs");Directory.CreateDirectory(directory);var width=6000;var height=4000;var rgb=new byte[width*height*3];
        for(var row=0;row<height;row++)for(var column=0;column<width;column++)
        {
            var offset=(row*width+column)*3;rgb[offset]=(byte)(column*255/(width-1));rgb[offset+1]=(byte)(row*255/(height-1));rgb[offset+2]=(byte)((column/59+row/71)%2*190+30);
        }
        var jpegPath=Path.Combine(directory,"synthetic-24mp.jpg");var jpeg=new JpegBitmapEncoder{QualityLevel=90};
        jpeg.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width,height,96,96,PixelFormats.Rgb24,null,rgb,width*3)));using(var stream=File.Create(jpegPath))jpeg.Save(stream);
        var alpha=new byte[64*48*4];for(var pixel=0;pixel<64*48;pixel++){alpha[pixel*4]=50;alpha[pixel*4+1]=190;alpha[pixel*4+2]=245;alpha[pixel*4+3]=(byte)(pixel%64*4);}
        var pngPath=Path.Combine(directory,"synthetic-alpha.png");var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(64,48,96,96,PixelFormats.Bgra32,null,alpha,64*4)));using(var stream=File.Create(pngPath))png.Save(stream);
        return(jpegPath,pngPath);
    }
    private static void Capture(MainWindow window,string name)
    {
        var target=(FrameworkElement)window.Content;target.UpdateLayout();var bitmap=new RenderTargetBitmap((int)target.ActualWidth,(int)target.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(target);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(Root,name));encoder.Save(stream);
    }
    private static void RunHost(Func<MainWindow,MainViewModel,Task> action)
    {
        Exception? failure=null;var complete=false;var thread=new Thread(()=>
        {
            try
            {
                var application=new App();application.InitializeComponent();var busy=false;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(120)};
                timer.Tick+=async(_,_)=>
                {
                    if(busy||application.MainWindow is not MainWindow{IsLoaded:true,DataContext:MainViewModel main} window)return;busy=true;
                    try{main.ForceExitTutorial();await action(window,main);complete=true;}catch(Exception error){failure=error;}finally{timer.Stop();application.Shutdown();}
                };
                timer.Start();application.Run();
            }
            catch(Exception error){failure=error;}
        }){IsBackground=true};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(3)),"Complete company App host timed out");
        if(failure is not null)throw new AssertFailedException(failure.ToString(),failure);Assert.IsTrue(complete);
    }
    [TestMethod]
    public void LinkedBookingContinuePlanningClosesSavedOverlayAndKeepsProjectIdentity() => RunHost(async (window, main) =>
    {
        Directory.CreateDirectory(Root);
        var project = Guid.NewGuid();
        var database = new PixelTartDatabase(AppDataPaths.DatabaseFile);
        await new SqliteProjectRepository(database).UpsertAsync(new PhotoProjectRecord { Id=project, Name="Linked booking overlay regression" });
        main.NavigateCommand.Execute("WorkCalendar");
        var calendar = main.WorkCalendarPage;
        await calendar.InitializeAsync();
        var requested = new TaskCompletionSource<ShootBookingEditorViewModel>();
        calendar.EditorRequested += (_, args) => requested.TrySetResult(args.Editor);
        calendar.NewBookingCommand.Execute(null);
        var editor = await requested.Task.WaitAsync(TimeSpan.FromSeconds(10));
        editor.Title = "Synthetic linked booking";
        editor.SelectedProject = editor.ProjectOptions.Single(option => option.Id == project);
        var bookingId = editor.StableBookingId;
        var overlay = (FrameworkElement)window.FindName("BookingEditorOverlay");
        Assert.AreEqual(Visibility.Visible, overlay.Visibility);
        await ((RAWSelectionAssistant.Utilities.AsyncRelayCommand)editor.ContinuePlanningCommand).ExecuteAsync(null);
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while ((editor.IsBusy || !main.IsPlanningPage || main.PlanningPage?.ProjectId != project) && DateTime.UtcNow < deadline) await Task.Delay(30);
        Assert.IsTrue(editor.WasSaved, editor.ValidationText);
        Assert.IsTrue(main.IsPlanningPage);
        Assert.AreEqual(project, main.PlanningPage!.ProjectId);
        Assert.AreEqual(Visibility.Collapsed, overlay.Visibility, "A saved quick editor must not cover the linked planning workspace.");
        var booking = await new RAWSelectionAssistant.Core.Services.Database.SqliteShootBookingRepository(database).GetAsync(bookingId);
        Assert.IsNotNull(booking);
        Assert.AreEqual(project, booking.ProjectId);
        Assert.AreEqual(bookingId, booking.Id);
        await main.OpenPlanningAsync(project, bookingId);
        Assert.AreEqual(Visibility.Collapsed, overlay.Visibility, "Same-page planning navigation must remain unobstructed.");
        await File.WriteAllTextAsync(Path.Combine(Root,"LINKED_BOOKING_OVERLAY.json"), JsonSerializer.Serialize(new { processId=Environment.ProcessId, projectId=project, bookingId, revision=booking.Revision, overlayCollapsed=true, samePageNavigation=true }, JsonOptions));
    });

    [TestMethod]
    public void CompleteAppCreateMixedCanvasSaveAndInsertFixedSnapshot() => RunHost(async (window, main) =>
    {
        Directory.CreateDirectory(Root);
        Assert.IsFalse(File.Exists(Path.Combine(Root,"WORKFLOW.json")),"Use a fresh fixture root");
        var project = Guid.NewGuid();
        await new SqliteProjectRepository(new PixelTartDatabase(AppDataPaths.DatabaseFile)).UpsertAsync(new PhotoProjectRecord { Id=project, Name="公司闭环 · 可分发合成输入" });
        var planning = main.PlanningPage!;
        await OpenPlanning(window, main, project);
        planning.DocumentTitle="公司策划—自由画布—固定版本";
        Assert.IsTrue(await planning.FlushAsync(),planning.SaveStatus);
        var library=await new AssetLibraryContainerService().CreateAsync(Path.Combine(Root,"workflow.ptlibrary"),"公司合成闭环库");
        var canvasRoot=Path.Combine(AppDataPaths.DataDirectory,"FreeCanvas");
        var document=new CanvasDocument { SchemaVersion=2,ProjectId=project,StorageRoot=canvasRoot,PixelWidth=1280,PixelHeight=800,Name="公司混合画布" };
        var view=await OpenCanvas(window,main,library.ContainerPath,document);
        var inputs=CreateSyntheticInputs();
        var photo=await CanvasPixelRenderer.ImportAsync(inputs.Jpeg,view.Editor.Document,canvasRoot);
        var alpha=await CanvasPixelRenderer.ImportAsync(inputs.Png,view.Editor.Document,canvasRoot);
        view.Editor.Add([photo with { X=30,Y=70,Width=800,Height=560,Name="合成24MP JPEG" }]);
        var photoId=view.Editor.Selected.Single().ObjectId;
        view.Editor.Crop(new(.1,.15,.75,.7));view.Editor.Rotate(17);view.Editor.Flip(true);view.Editor.Flip(false);view.Editor.SetOpacity(.72);
        view.Editor.Add([alpha with { X=650,Y=220,Width=360,Height=300,Name="合成透明 PNG" }]);
        view.Editor.StepLayer(false);view.Editor.StepLayer(true);view.Editor.SetVisible(false);view.Editor.SetVisible(true);
        view.Editor.AddText(60,5,"第一版 · 策划固定快照");
        view.Editor.AddBrushLayer();view.Editor.AddBrushStroke(new([new(.1,.8),new(.7,.9)],"#7AC345",28,.55,.3));
        view.Editor.AddPalette(880,20,new([new("#FA7020",25,.9,.5,1),new("#347AC0",210,.7,.5,1),new("#7AC345",95,.7,.5,1)],[]));
        view.Editor.Add([new CanvasObject { Name="隐藏占位层",SourcePath=Path.Combine(Root,"intentionally-offline.png"),Hidden=true }]);
        view.Editor.Select(photoId);await SaveButton(view);document=view.Editor.Document;
        Assert.IsTrue(document.Objects.Any(item=>item.Hidden));
        await view.Surface.LoadPreviewsAsync();
        CanvasPixelRenderer.RenderPhotoPreview(document,out var metrics);
        Assert.AreEqual(1,metrics.NativeJpegDecodes);Assert.AreEqual(0,metrics.RetainedCacheBytes);Assert.IsTrue(metrics.PeakCacheBytes<=32L*1024*1024);
        var planningView=await OpenPlanning(window,main,project);await CaptureButton(planningView,planning,1);
        var revision=planning.DocumentCanvasRevisions.Single();
        Assert.AreEqual(document.CanvasId,revision.CanvasId);
        Assert.AreEqual(JsonSerializer.Serialize(document.Objects),JsonSerializer.Serialize(revision.EditableSnapshot.Objects));
        Assert.AreEqual(await RAWSelectionAssistant.Services.PlanningCanvasSnapshot.RenderAsync(document),revision.PreviewPngBase64);
        var receipt=new Receipt(Environment.ProcessId,project,document.CanvasId,library.ContainerPath,planning.PlanningDocumentId!.Value,revision.RevisionId,Hash(revision.PreviewPngBase64!),JsonSerializer.Serialize(document.Objects));
        await File.WriteAllTextAsync(Path.Combine(Root,"WORKFLOW.json"),JsonSerializer.Serialize(receipt,JsonOptions));
        await File.WriteAllTextAsync(Path.Combine(Root,"SYNTHETIC_INPUTS.json"),JsonSerializer.Serialize(new { origin="Deterministic generated fixtures, NOT real photographs or household private sources",jpeg=new { path=inputs.Jpeg,width=6000,height=4000,sha256=FileHash(inputs.Jpeg) },png=new { path=inputs.Png,alpha=true,sha256=FileHash(inputs.Png) },metrics },JsonOptions));
        Capture(window,"OFFSCREEN-company-first.png");
    });
    [TestMethod]
    public void IndependentCompleteAppReopenEditAndManuallyUpdateSnapshot() => RunHost(async (window, main) =>
    {
        var receipt=ReadReceipt();Assert.AreNotEqual(receipt.ProcessId,Environment.ProcessId);
        await OpenPlanning(window,main,receipt.ProjectId);var planning=main.PlanningPage!;
        var old=planning.DocumentCanvasRevisions.Single();
        Assert.AreEqual(receipt.DocumentId,planning.PlanningDocumentId!.Value);
        Assert.AreEqual(receipt.RevisionId,old.RevisionId);Assert.AreEqual(receipt.PreviewHash,Hash(old.PreviewPngBase64!));
        var store=new CanvasDocumentStore(Path.Combine(AppDataPaths.DataDirectory,"FreeCanvas"));
        var document=(await store.LoadAsync(receipt.CanvasId))!;
        Assert.AreEqual(receipt.ObjectsJson,JsonSerializer.Serialize(document.Objects));
        var view=await OpenCanvas(window,main,receipt.LibraryPath,document);
        view.Editor.Select(document.Objects.Single(item=>item.IsText).ObjectId);
        view.Editor.EditText("第二版 · 手动更新后保留第一版",42,"#E78345");
        view.Editor.Select(document.Objects.First(item=>item.IsImage&&!item.Hidden).ObjectId);view.Editor.Rotate(19);
        await SaveButton(view);document=view.Editor.Document;
        var planningView=await OpenPlanning(window,main,receipt.ProjectId);
        Assert.AreEqual(1,planning.DocumentCanvasRevisions.Count);Assert.AreEqual(receipt.PreviewHash,Hash(planning.DocumentCanvasRevisions[0].PreviewPngBase64!));
        await CaptureButton(planningView,planning,2);
        Assert.AreEqual(receipt.RevisionId,planning.DocumentCanvasRevisions[0].RevisionId);Assert.AreEqual(receipt.PreviewHash,Hash(planning.DocumentCanvasRevisions[0].PreviewPngBase64!));
        var updated=planning.DocumentCanvasRevisions[1];Assert.AreNotEqual(receipt.RevisionId,updated.RevisionId);Assert.AreEqual(receipt.CanvasId,updated.CanvasId);
        Assert.AreNotEqual(receipt.PreviewHash,Hash(updated.PreviewPngBase64!));
        Assert.AreEqual(JsonSerializer.Serialize(document.Objects),JsonSerializer.Serialize(updated.EditableSnapshot.Objects));
        Assert.IsTrue(await planning.FlushAsync(),planning.SaveStatus);
        await File.WriteAllTextAsync(Path.Combine(Root,"UPDATED.json"),JsonSerializer.Serialize(new { processId=Environment.ProcessId,revisionId=updated.RevisionId,previewHash=Hash(updated.PreviewPngBase64!),objectsJson=JsonSerializer.Serialize(document.Objects) },JsonOptions));
        Capture(window,"OFFSCREEN-company-two-versions.png");
    });
    [TestMethod]
    public void ThirdCompleteAppReopenKeepsBothSnapshotIdentitiesAndLayerStates() => RunHost(async (window, main) =>
    {
        var receipt=ReadReceipt();using var updated=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root,"UPDATED.json")));var expected=updated.RootElement;
        Assert.AreNotEqual(receipt.ProcessId,Environment.ProcessId);Assert.AreNotEqual(expected.GetProperty("processId").GetInt32(),Environment.ProcessId);
        await OpenPlanning(window,main,receipt.ProjectId);var planning=main.PlanningPage!;
        Assert.AreEqual(receipt.DocumentId,planning.PlanningDocumentId!.Value);Assert.AreEqual(2,planning.DocumentCanvasRevisions.Count);
        Assert.AreEqual(receipt.RevisionId,planning.DocumentCanvasRevisions[0].RevisionId);Assert.AreEqual(receipt.PreviewHash,Hash(planning.DocumentCanvasRevisions[0].PreviewPngBase64!));
        Assert.AreEqual(expected.GetProperty("revisionId").GetGuid(),planning.DocumentCanvasRevisions[1].RevisionId);Assert.AreEqual(expected.GetProperty("previewHash").GetString(),Hash(planning.DocumentCanvasRevisions[1].PreviewPngBase64!));
        var store=new CanvasDocumentStore(Path.Combine(AppDataPaths.DataDirectory,"FreeCanvas"));var document=(await store.LoadAsync(receipt.CanvasId))!;
        Assert.AreEqual(expected.GetProperty("objectsJson").GetString(),JsonSerializer.Serialize(document.Objects));
        await OpenCanvas(window,main,receipt.LibraryPath,document);
        await File.WriteAllTextAsync(Path.Combine(Root,"THIRD_REOPEN.json"),JsonSerializer.Serialize(new { processId=Environment.ProcessId,exactLayerStates=true,retainedSnapshots=2 },JsonOptions));
    });
}
