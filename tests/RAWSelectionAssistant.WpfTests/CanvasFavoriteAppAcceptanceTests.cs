using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;
using PixelTart.Modules.AssetLibrary.FreeCanvas;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.WpfTests;

[TestClass][DoNotParallelize]
public sealed class CanvasFavoriteAppAcceptanceTests
{
    private static string Root => Environment.GetEnvironmentVariable("KITAO_FAVORITES_ACCEPTANCE_ROOT") ?? throw new InvalidOperationException("Isolated root required.");
    private sealed record Receipt(int ProcessId,string LibraryPath,Guid CanvasId,Guid LibraryId,Guid AssetId,string ContentHash);
    private static Receipt Read()=>JsonSerializer.Deserialize<Receipt>(File.ReadAllText(Path.Combine(Root,"FAVORITE.json")))!;
    private static IEnumerable<T> Find<T>(DependencyObject node) where T:DependencyObject
    { if(node is T item)yield return item;for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Find<T>(VisualTreeHelper.GetChild(node,i)))yield return child; }
    private static async Task Wait(Func<bool> condition,string message)
    {var deadline=DateTime.UtcNow.AddSeconds(30);while(!condition()&&DateTime.UtcNow<deadline)await Task.Delay(40);Assert.IsTrue(condition(),message);}
    private static async Task NativeInvoke(MainWindow window,string automationId)
    {
        var handle=new WindowInteropHelper(window).Handle;
        await Task.Run(()=>
        {
            var root=AutomationElement.FromHandle(handle);
            var element=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,automationId));
            Assert.IsNotNull(element,"Native UIA element "+automationId);
            ((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        });
    }
    private static async Task NativeMenuInvoke(MainWindow window,string name)
    {
        var handle=new WindowInteropHelper(window).Handle;
        await Task.Run(async()=>
        {
            var condition=new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.MenuItem),new PropertyCondition(AutomationElement.NameProperty,name),new PropertyCondition(AutomationElement.IsOffscreenProperty,false));
            AutomationElement? item=null;var deadline=DateTime.UtcNow.AddSeconds(10);
            while(item is null&&DateTime.UtcNow<deadline)
            {
                item=AutomationElement.FromHandle(handle).FindFirst(TreeScope.Descendants,condition);
                if(item is null)
                    foreach(AutomationElement owned in AutomationElement.RootElement.FindAll(TreeScope.Children,new PropertyCondition(AutomationElement.ProcessIdProperty,Environment.ProcessId)))
                    {item=owned.FindFirst(TreeScope.Descendants,condition);if(item is not null)break;}
                if(item is null)await Task.Delay(50);
            }
            Assert.IsNotNull(item,"Native menu item "+name);((InvokePattern)item.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        });
    }
    private static async Task NativeSelectSource(MainWindow window,string name)
    {
        var handle=new WindowInteropHelper(window).Handle;
        await Task.Run(()=>
        {
            var shelf=AutomationElement.FromHandle(handle).FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"CanvasSources"));Assert.IsNotNull(shelf);
            var item=shelf.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem),new PropertyCondition(AutomationElement.NameProperty,name)));Assert.IsNotNull(item);
            ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        });
    }
    private static async Task NativeFavoriteFilter(MainWindow window)
    {
        var handle=new WindowInteropHelper(window).Handle;
        await Task.Run(()=>
        {
            var root=AutomationElement.FromHandle(handle);
            var combo=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"CanvasSourceFilter"));Assert.IsNotNull(combo);
            var expand=(ExpandCollapsePattern)combo.GetCurrentPattern(ExpandCollapsePattern.Pattern);expand.Expand();
            var condition=new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem),new PropertyCondition(AutomationElement.NameProperty,"收藏"));
            var item=combo.FindFirst(TreeScope.Descendants,condition)??root.FindFirst(TreeScope.Descendants,condition);Assert.IsNotNull(item,"Native favorite filter item");
            ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();expand.Collapse();
        });
    }
    private static async Task NativeExport(MainWindow window,string output)
    {
        var handle=new WindowInteropHelper(window).Handle;
        var export=Task.Run(()=>
        {
            var main=AutomationElement.FromHandle(handle);
            var button=main.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"CanvasExport"));
            Assert.IsNotNull(button);((InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        });
        var dialog=await Task.Run(async()=>
        {
            AutomationElement? found=null;var deadline=DateTime.UtcNow.AddSeconds(20);
            while(found is null&&DateTime.UtcNow<deadline)
            {
                found=AutomationElement.FromHandle(handle).FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,"另存为"));
                found??=AutomationElement.RootElement.FindFirst(TreeScope.Children,new AndCondition(new PropertyCondition(AutomationElement.ProcessIdProperty,Environment.ProcessId),new PropertyCondition(AutomationElement.NameProperty,"另存为")));
                if(found is null)await Task.Delay(50);
            }
            Assert.IsNotNull(found,"Native save dialog");
            var edit=found.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit),new PropertyCondition(AutomationElement.AutomationIdProperty,"1001")));
            Assert.IsNotNull(edit,"Native file name field");((ValuePattern)edit.GetCurrentPattern(ValuePattern.Pattern)).SetValue(output);
            var save=found.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button),new PropertyCondition(AutomationElement.AutomationIdProperty,"1")));Assert.IsNotNull(save);
            var evidence=new { dialog=found.Current.Name,fileNameId=edit.Current.AutomationId,saveName=save.Current.Name,saveId=save.Current.AutomationId, value=((ValuePattern)edit.GetCurrentPattern(ValuePattern.Pattern)).Current.Value };
            ((InvokePattern)save.GetCurrentPattern(InvokePattern.Pattern)).Invoke();return evidence;
        });
        await File.WriteAllTextAsync(Path.Combine(Root,"NATIVE_EXPORT_DIALOG.json"),JsonSerializer.Serialize(dialog));
        await export;await Wait(()=>File.Exists(output),"Native export file");
    }
    private static async Task<(AssetLibraryPage Page,FreeCanvasView View)> Open(MainWindow window,MainViewModel main,string library,CanvasDocument? document=null)
    {
        main.NavigateCommand.Execute("AssetLibrary");await Task.Delay(150);
        var host=Find<AssetLibraryWorkspaceHost>((DependencyObject)window.Content).Single();await host.InitializeForProductHarnessAsync();await host.SwitchToContainerAsync(library);
        var page=await host.InitializeForProductHarnessAsync();Assert.IsNotNull(page);
        if(document is null)await page.NewCanvasAsync();else await page.ShowCanvasAsync(document);
        var view=Find<FreeCanvasView>((DependencyObject)window.Content).Single(item=>item.IsVisible);return(page,view);
    }
    private static ListBox Sources(FreeCanvasView view)=>Find<ListBox>(view).Single(item=>AutomationProperties.GetAutomationId(item)=="CanvasSources");
    private static ComboBox Filter(FreeCanvasView view)=>Find<ComboBox>(view).Single(item=>AutomationProperties.GetAutomationId(item)=="CanvasSourceFilter");
    private static Button Button(FreeCanvasView view,string text)=>Find<Button>(view).Single(item=>Equals(item.Content,text));
    private static void Capture(MainWindow window,string filename)
    {
        var content=(FrameworkElement)window.Content;content.UpdateLayout();var image=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(content);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(Path.Combine(Root,filename));encoder.Save(stream);
    }
    private static void Run(Func<MainWindow,MainViewModel,Task> action)
    {
        Exception? failure=null;var complete=false;var thread=new Thread(()=>
        {
            try
            {
                var app=new App();app.InitializeComponent();var busy=false;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(120)};
                timer.Tick+=async(_,_)=>
                {
                    if(busy||app.MainWindow is not MainWindow{IsLoaded:true,DataContext:MainViewModel main} window)return;busy=true;
                    try{await action(window,main);complete=true;}catch(Exception error){failure=error;}finally{timer.Stop();app.Shutdown();}
                };timer.Start();app.Run();
            }
            catch(Exception error){failure=error;}
        }){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(3)),"Complete App timed out");
        if(failure is not null)throw new AssertFailedException(failure.ToString(),failure);Assert.IsTrue(complete);
    }
    [TestMethod]
    public void Phase1FavoriteViaVisibleMaterialShelf()=>Run(async(window,main)=>
    {
        Directory.CreateDirectory(Root);
        var library=await new AssetLibraryContainerService().CreateAsync(Path.Combine(Root,"favorites.ptlibrary"),"拼贴收藏验收");
        var (page,view)=await Open(window,main,library.ContainerPath);
        var pixels=new byte[96*64*4];for(var i=0;i<96*64;i++){pixels[i*4]=40;pixels[i*4+1]=150;pixels[i*4+2]=230;pixels[i*4+3]=255;}
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(96,64,96,96,PixelFormats.Bgra32,null,pixels,96*4)));
        var input=Path.Combine(Root,"synthetic-favorite.png");using(var stream=File.Create(input))png.Save(stream);
        await page.ViewModel.ImportDroppedFilesAsync([input]);view.ShowSources("素材库");var list=Sources(view);await Wait(()=>list.Items.Count==1,"Shelf import");await NativeSelectSource(window,"synthetic-favorite.png");
        await NativeInvoke(window,"CanvasToggleFavorite");await Wait(()=>Find<Button>(view).Any(button=>Equals(button.Content,"取消收藏")&&button.IsEnabled),"Favorite persisted");
        var item=(CanvasObject)list.SelectedItem;Assert.HasCount(0,view.Editor.Document.Objects);
        var favorites=await page.ViewModel.LoadCanvasFavoritesAsync();Assert.IsTrue(favorites.Contains(new(item.LibraryId,item.AssetId!.Value)));
        await File.WriteAllTextAsync(Path.Combine(Root,"FAVORITE.json"),JsonSerializer.Serialize(new Receipt(Environment.ProcessId,library.ContainerPath,view.Editor.Document.CanvasId,item.LibraryId,item.AssetId.Value,item.ContentHash)));
        Capture(window,"OFFSCREEN-1-favorite.png");Assert.IsTrue(await view.FlushAsync());
    });
    [TestMethod]
    public void Phase2RestartFilterFavoriteAddAndSave()=>Run(async(window,main)=>
    {
        var prior=Read();Assert.AreNotEqual(prior.ProcessId,Environment.ProcessId);
        var (page,view)=await Open(window,main,prior.LibraryPath);
        await NativeFavoriteFilter(window);var list=Sources(view);await Wait(()=>list.Items.Count==1,"Favorite survives App restart");
        var item=(CanvasObject)list.Items[0];Assert.AreEqual(prior.LibraryId,item.LibraryId);Assert.AreEqual(prior.AssetId,item.AssetId);Assert.AreEqual(prior.ContentHash,item.ContentHash);await NativeSelectSource(window,"synthetic-favorite.png");
        await NativeInvoke(window,"CanvasAddSources");
        await Wait(()=>view.Editor.Document.Objects.Count==1,"Native add completes");Assert.HasCount(1,view.Editor.Document.Objects);Assert.AreEqual(prior.AssetId,view.Editor.Document.Objects[0].AssetId);
        view.Editor.Rename("收藏素材重开验收");await NativeInvoke(window,"CanvasSave");Assert.IsTrue(await view.FlushAsync());
        await File.WriteAllTextAsync(Path.Combine(Root,"SAVED.json"),JsonSerializer.Serialize(new { processId=Environment.ProcessId,canvasId=view.Editor.Document.CanvasId,storage=view.Editor.Document.StorageRoot,objects=view.Editor.Document.Objects }));
        Capture(window,"OFFSCREEN-2-favorite-on-canvas.png");
    });
    [TestMethod]
    public void Phase3ReopenProjectExportAlphaAndUnfavorite()=>Run(async(window,main)=>
    {
        var prior=Read();using var saved=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root,"SAVED.json")));Assert.AreNotEqual(saved.RootElement.GetProperty("processId").GetInt32(),Environment.ProcessId);
        var (page,initial)=await Open(window,main,prior.LibraryPath);
        var store=new CanvasDocumentStore(initial.Editor.Document.StorageRoot??page.ViewModel.CanvasDirectory);
        var document=await store.LoadAsync(saved.RootElement.GetProperty("canvasId").GetGuid());Assert.IsNotNull(document);Assert.HasCount(1,document.Objects);
        Assert.AreEqual(prior.AssetId,document.Objects[0].AssetId);Assert.AreEqual(prior.ContentHash,document.Objects[0].ContentHash);
        await NativeInvoke(window,"CanvasMore");await NativeMenuInvoke(window,"打开画布…");await NativeMenuInvoke(window,"收藏素材重开验收");
        await Wait(()=>Find<FreeCanvasView>((DependencyObject)window.Content).Any(item=>item.IsVisible&&item.Editor.Document.CanvasId==document.CanvasId),"Native saved canvas menu opens exact project");
        var view=Find<FreeCanvasView>((DependencyObject)window.Content).Single(item=>item.IsVisible);Assert.AreEqual(document.CanvasId,view.Editor.Document.CanvasId);Assert.HasCount(1,view.Editor.Document.Objects);await view.Surface.LoadPreviewsAsync();
        var output=Path.Combine(Root,"favorite-transparent.png");await NativeExport(window,output);
        BitmapSource bitmap;using(var stream=File.OpenRead(output))bitmap=BitmapFrame.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
        var bgra=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);var pixels=new byte[bgra.PixelWidth*bgra.PixelHeight*4];bgra.CopyPixels(pixels,bgra.PixelWidth*4,0);
        var transparent=0;var opaque=0;for(var i=3;i<pixels.Length;i+=4){if(pixels[i]==0)transparent++;if(pixels[i]>0)opaque++;}Assert.IsGreaterThan(0,transparent);Assert.IsGreaterThan(0,opaque);
        await NativeFavoriteFilter(window);var list=Sources(view);await Wait(()=>list.Items.Count==1,"Favorite filter");await NativeSelectSource(window,"synthetic-favorite.png");
        await NativeInvoke(window,"CanvasToggleFavorite");await Wait(()=>list.Items.Count==0,"Unfavorite removes filtered item");Assert.HasCount(0,await page.ViewModel.LoadCanvasFavoritesAsync());Assert.HasCount(1,view.Editor.Document.Objects);
        Capture(window,"OFFSCREEN-3-reopened-export.png");
        await File.WriteAllTextAsync(Path.Combine(Root,"EXPORT.json"),JsonSerializer.Serialize(new { processId=Environment.ProcessId,output,width=bitmap.PixelWidth,height=bitmap.PixelHeight,transparent,opaque,sha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(output))),entryEvidence="Full App + native UIA favorite/unfavorite; native UIA selection/filter/favorite/add/save/export + system SaveFileDialog; canvas reopen via visible Canvas/Open saved menus in full App; no physical mouse" }));
    });
}
