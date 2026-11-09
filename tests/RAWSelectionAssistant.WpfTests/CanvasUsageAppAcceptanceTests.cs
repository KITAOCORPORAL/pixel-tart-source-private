using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelTart.Modules.AssetLibrary;
using PixelTart.Modules.AssetLibrary.FreeCanvas;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;
public sealed partial class CanvasFavoriteAppAcceptanceTests
{
    private sealed record UsageReceipt(int ProcessId,string LibraryPath,string SecondLibraryPath,Guid CanvasId,Guid LibraryId,Guid AssetId,string ContentHash);
    private static UsageReceipt UsageRead()=>JsonSerializer.Deserialize<UsageReceipt>(File.ReadAllText(Path.Combine(Root,"USAGE.json")))!;
    private static async Task NativeButton(MainWindow window,string name)
    {
        var handle=new WindowInteropHelper(window).Handle;
        await Task.Run(()=>{var e=AutomationElement.FromHandle(handle).FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button),new PropertyCondition(AutomationElement.NameProperty,name),new PropertyCondition(AutomationElement.IsOffscreenProperty,false)));Assert.IsNotNull(e,"Native button "+name);((InvokePattern)e.GetCurrentPattern(InvokePattern.Pattern)).Invoke();});
    }
    private static async Task UsageBadge(MainWindow window,FreeCanvasView view,string name,bool expected)
    {
        await Wait(()=>
        {
            var list=Sources(view);var item=list.Items.Cast<CanvasObject>().FirstOrDefault(x=>x.Name==name);if(item is null)return false;
            if(list.ItemContainerGenerator.ContainerFromItem(item) is not ListBoxItem container)return false;
            return Find<Border>(container).Single(x=>AutomationProperties.GetAutomationId(x)=="CanvasMaterialUsedBadge").IsVisible==expected;
        },"Visible used badge="+expected);
        var handle=new WindowInteropHelper(window).Handle;
        var visible=await Task.Run(()=>
        {
            var shelf=AutomationElement.FromHandle(handle).FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"CanvasSources"));Assert.IsNotNull(shelf);
            var item=shelf.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem),new PropertyCondition(AutomationElement.NameProperty,name)));Assert.IsNotNull(item);
            return item.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.NameProperty,"已用"),new PropertyCondition(AutomationElement.IsOffscreenProperty,false))) is not null;
        });
        Assert.AreEqual(expected,visible,"Native UIA used badge");
    }
    private static async Task RemoveSelectedViaMenu(MainWindow window)
    {await NativeButton(window,"编辑");await NativeMenuInvoke(window,"移出画布");}
    private static async Task RenameCanvasViaNativeEntry(MainWindow window,string oldName,string newName)
    {
        var handle=new WindowInteropHelper(window).Handle;
        await Task.Run(()=>
        {
            var root=AutomationElement.FromHandle(handle);AutomationElement? match=null;
            foreach(AutomationElement edit in root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit)))
                if(edit.TryGetCurrentPattern(ValuePattern.Pattern,out var p)&&((ValuePattern)p).Current.Value==oldName){match=edit;break;}
            Assert.IsNotNull(match,"Native canvas name entry");match.SetFocus();((ValuePattern)match.GetCurrentPattern(ValuePattern.Pattern)).SetValue(newName);
        });
        await NativeButton(window,"选择");
    }
    private static async Task<FreeCanvasView> OpenSavedUsage(MainWindow window)
    {
        var previous=Find<FreeCanvasView>((DependencyObject)window.Content).Single(x=>x.IsVisible);
        await NativeInvoke(window,"CanvasMore");await NativeMenuInvoke(window,"打开画布…");await NativeMenuInvoke(window,"已用角标验收");
        await Wait(()=>Find<FreeCanvasView>((DependencyObject)window.Content).Any(x=>x.IsVisible&&!ReferenceEquals(x,previous)&&x.Editor.Document.Name=="已用角标验收"),"Native saved canvas reopen");
        return Find<FreeCanvasView>((DependencyObject)window.Content).Single(x=>x.IsVisible);
    }
    [TestMethod]
    public void UsedPhase1AddRemoveMultiplicityUndoRedoAndSave()=>Run(async(window,main)=>
    {
        Directory.CreateDirectory(Root);
        var library=await new AssetLibraryContainerService().CreateAsync(Path.Combine(Root,"used-a.ptlibrary"),"已用验收库A");
        var second=await new AssetLibraryContainerService().CreateAsync(Path.Combine(Root,"used-b.ptlibrary"),"已用验收库B");
        await Open(window,main,second.ContainerPath); // Fixture-only seed of the second recent library; switches under test use its real menu.
        var (page,view)=await Open(window,main,library.ContainerPath);
        var pixels=new byte[64*48*4];for(var i=0;i<64*48;i++){pixels[i*4]=70;pixels[i*4+1]=190;pixels[i*4+2]=230;pixels[i*4+3]=255;}
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(64,48,96,96,PixelFormats.Bgra32,null,pixels,64*4)));
        var input=Path.Combine(Root,"synthetic-used.png");using(var f=File.Create(input))png.Save(f);
        await page.ViewModel.ImportDroppedFilesAsync([input]);view.ShowSources("素材库");await Wait(()=>Sources(view).Items.Count==1,"Imported material shelf");await NativeSelectSource(window,"synthetic-used.png");
        var source=(CanvasObject)Sources(view).SelectedItem;await UsageBadge(window,view,source.Name,false);
        await NativeInvoke(window,"CanvasToggleFavorite");await Wait(()=>Find<Button>(view).Any(b=>Equals(b.Content,"取消收藏")),"Favorite saved");await NativeSelectSource(window,source.Name);
        await NativeInvoke(window,"CanvasAddSources");await Wait(()=>view.Editor.Document.Objects.Count==1,"Native first add");await UsageBadge(window,view,source.Name,true);
        await RemoveSelectedViaMenu(window);await Wait(()=>view.Editor.Document.Objects.Count==0,"Remove last instance");await UsageBadge(window,view,source.Name,false);
        await NativeButton(window,"撤销");await Wait(()=>view.Editor.Document.Objects.Count==1,"Undo last removal");await UsageBadge(window,view,source.Name,true);
        await NativeButton(window,"重做");await Wait(()=>view.Editor.Document.Objects.Count==0,"Redo last removal");await UsageBadge(window,view,source.Name,false);
        await NativeButton(window,"撤销");await Wait(()=>view.Editor.Document.Objects.Count==1,"Restore first instance");
        await NativeSelectSource(window,source.Name);await NativeInvoke(window,"CanvasAddSources");await Wait(()=>view.Editor.Document.Objects.Count==2,"Second instance");await UsageBadge(window,view,source.Name,true);
        await RemoveSelectedViaMenu(window);await Wait(()=>view.Editor.Document.Objects.Count==1,"Only remove second instance");await UsageBadge(window,view,source.Name,true);
        await NativeButton(window,"撤销");await Wait(()=>view.Editor.Document.Objects.Count==2,"Undo duplicate removal");await UsageBadge(window,view,source.Name,true);
        await NativeButton(window,"重做");await Wait(()=>view.Editor.Document.Objects.Count==1,"Redo duplicate removal");await UsageBadge(window,view,source.Name,true);
        await RenameCanvasViaNativeEntry(window,view.Editor.Document.Name,"已用角标验收");await Wait(()=>view.Editor.Document.Name=="已用角标验收","Native rename committed");await NativeInvoke(window,"CanvasSave");Assert.IsTrue(await view.FlushAsync());
        await File.WriteAllTextAsync(Path.Combine(Root,"USAGE.json"),JsonSerializer.Serialize(new UsageReceipt(Environment.ProcessId,library.ContainerPath,second.ContainerPath,view.Editor.Document.CanvasId,source.LibraryId,source.AssetId!.Value,source.ContentHash)));
        Capture(window,"OFFSCREEN-used-1.png");
    });
    [TestMethod]
    public void UsedPhase2RestartSwitchCanvasAndUnfavoriteKeepsUsage()=>Run(async(window,main)=>
    {
        var prior=UsageRead();Assert.AreNotEqual(prior.ProcessId,Environment.ProcessId);var(page,view)=await Open(window,main,prior.LibraryPath);view=await OpenSavedUsage(window);
        Assert.AreEqual(prior.CanvasId,view.Editor.Document.CanvasId);await Wait(()=>Sources(view).Items.Count==1,"Restart shelf");await UsageBadge(window,view,"synthetic-used.png",true);
        await NativeInvoke(window,"CanvasMore");await NativeMenuInvoke(window,"新建画布");await Wait(()=>Find<FreeCanvasView>((DependencyObject)window.Content).Any(x=>x.IsVisible&&x.Editor.Document.CanvasId!=prior.CanvasId),"Native blank canvas");
        view=Find<FreeCanvasView>((DependencyObject)window.Content).Single(x=>x.IsVisible);await Wait(()=>Sources(view).Items.Count==1,"Blank canvas shelf");await UsageBadge(window,view,"synthetic-used.png",false);
        view=await OpenSavedUsage(window);await Wait(()=>Sources(view).Items.Count==1,"Original canvas shelf");await UsageBadge(window,view,"synthetic-used.png",true);
        await NativeFavoriteFilter(window);await Wait(()=>Sources(view).Items.Count==1,"Favorites source");await NativeSelectSource(window,"synthetic-used.png");await UsageBadge(window,view,"synthetic-used.png",true);
        await NativeInvoke(window,"CanvasToggleFavorite");await Wait(()=>Sources(view).Items.Count==0,"Unfavorite source removal");Assert.HasCount(1,view.Editor.Document.Objects);
        await NativeUsageSource(window,"素材库");await Wait(()=>Sources(view).Items.Count==1,"Back to library source");await UsageBadge(window,view,"synthetic-used.png",true);
        Assert.AreEqual(prior.LibraryId,view.Editor.Document.Objects[0].LibraryId);Assert.AreEqual(prior.AssetId,view.Editor.Document.Objects[0].AssetId);await NativeInvoke(window,"CanvasSave");Assert.IsTrue(await view.FlushAsync());
        await File.WriteAllTextAsync(Path.Combine(Root,"USAGE_PHASE2.json"),JsonSerializer.Serialize(new{processId=Environment.ProcessId,prior.CanvasId,objects=view.Editor.Document.Objects.Count,favoriteCount=(await page.ViewModel.LoadCanvasFavoritesAsync()).Count}));Capture(window,"OFFSCREEN-used-2.png");
    });
    private static async Task NativeUsageSource(MainWindow window,string name)
    {
        var handle=new WindowInteropHelper(window).Handle;
        await Task.Run(()=>
        {
            var root=AutomationElement.FromHandle(handle);var combo=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"CanvasSourceFilter"));Assert.IsNotNull(combo);
            var expand=(ExpandCollapsePattern)combo.GetCurrentPattern(ExpandCollapsePattern.Pattern);expand.Expand();
            var item=combo.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem),new PropertyCondition(AutomationElement.NameProperty,name)));Assert.IsNotNull(item,"Native source option "+name);
            ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();expand.Collapse();
        });
    }
    private static async Task<AssetLibraryPage> NativeUsageLibrary(MainWindow window,string displayName)
    {
        var host=Find<AssetLibraryWorkspaceHost>((DependencyObject)window.Content).Single();
        await NativeButton(window,"当前库 · "+host.CurrentDescriptor!.DisplayName);
        var handle=new WindowInteropHelper(window).Handle;
        await Task.Run(async()=>
        {
            AutomationElement? item=null;var deadline=DateTime.UtcNow.AddSeconds(10);
            var condition=new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.MenuItem),new PropertyCondition(AutomationElement.NameProperty,displayName),new PropertyCondition(AutomationElement.IsOffscreenProperty,false));
            while(item is null&&DateTime.UtcNow<deadline)
            {
                foreach(AutomationElement owned in AutomationElement.RootElement.FindAll(TreeScope.Children,new PropertyCondition(AutomationElement.ProcessIdProperty,Environment.ProcessId)))
                {item=owned.FindFirst(TreeScope.Descendants,condition);if(item is not null)break;}
                if(item is null)await Task.Delay(50);
            }
            Assert.IsNotNull(item,"Native recent library "+displayName);((ExpandCollapsePattern)item.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
        });
        await NativeMenuInvoke(window,"打开 / 切换");await Wait(()=>host.CurrentDescriptor?.DisplayName==displayName&&host.CurrentPage is not null,"Native library switch");return host.CurrentPage!;
    }
    private static async Task<FreeCanvasView> NativeNewUsageCanvas(MainWindow window)
    {
        await NativeInvoke(window,"AssetLibraryMore");await NativeMenuInvoke(window,"新建自由画布");
        await Wait(()=>Find<FreeCanvasView>((DependencyObject)window.Content).Any(x=>x.IsVisible),"Native asset page new canvas");return Find<FreeCanvasView>((DependencyObject)window.Content).Single(x=>x.IsVisible);
    }
    [TestMethod]
    public void UsedPhase3SwitchLibraryRestoreOriginalAndExport()=>Run(async(window,main)=>
    {
        var prior=UsageRead();var(page,view)=await Open(window,main,prior.LibraryPath);view=await OpenSavedUsage(window);await Wait(()=>Sources(view).Items.Count==1,"Original shelf");await UsageBadge(window,view,"synthetic-used.png",true);
        var originalSource=view.Editor.Document.Objects.Single();var originalHash=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(originalSource.SourcePath)));
        var secondPage=await NativeUsageLibrary(window,"已用验收库B");await secondPage.ViewModel.ImportDroppedFilesAsync([Path.Combine(Root,"synthetic-used.png")]);view=await NativeNewUsageCanvas(window);
        await Wait(()=>Sources(view).Items.Count==1,"Other library shelf");Assert.AreNotEqual(prior.LibraryId,((CanvasObject)Sources(view).Items[0]).LibraryId);await UsageBadge(window,view,"synthetic-used.png",false);
        await NativeSelectSource(window,"synthetic-used.png");await NativeInvoke(window,"CanvasAddSources");await Wait(()=>view.Editor.Document.Objects.Count==1,"Other library add");await UsageBadge(window,view,"synthetic-used.png",true);await NativeInvoke(window,"CanvasSave");Assert.IsTrue(await view.FlushAsync());
        page=await NativeUsageLibrary(window,"已用验收库A");view=await NativeNewUsageCanvas(window);await Wait(()=>Sources(view).Items.Count==1,"Return library shelf");await UsageBadge(window,view,"synthetic-used.png",false);
        view=await OpenSavedUsage(window);await Wait(()=>Sources(view).Items.Count==1,"Restored original shelf");await UsageBadge(window,view,"synthetic-used.png",true);Assert.AreEqual(prior.CanvasId,view.Editor.Document.CanvasId);Assert.HasCount(1,view.Editor.Document.Objects);Assert.HasCount(0,await page.ViewModel.LoadCanvasFavoritesAsync());
        var output=Path.Combine(Root,"used-transparent.png");await NativeExport(window,output);
        BitmapSource bitmap;using(var stream=File.OpenRead(output))bitmap=BitmapFrame.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);var bgra=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);var pixels=new byte[bgra.PixelWidth*bgra.PixelHeight*4];bgra.CopyPixels(pixels,bgra.PixelWidth*4,0);
        long transparent=0,nonTransparent=0;for(var i=3;i<pixels.Length;i+=4){if(pixels[i]==0)transparent++;else nonTransparent++;}Assert.IsGreaterThan(0L,transparent);Assert.IsGreaterThan(0L,nonTransparent);
        Assert.AreEqual(originalHash,Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(originalSource.SourcePath))));
        var documentPath=Path.Combine(page.ViewModel.CanvasDirectory,prior.CanvasId.ToString("N")+".json");Assert.IsFalse((await File.ReadAllTextAsync(documentPath)).Contains("UsedMaterialKeys",StringComparison.Ordinal));
        await File.WriteAllTextAsync(Path.Combine(Root,"USAGE_EXPORT.json"),JsonSerializer.Serialize(new{processId=Environment.ProcessId,prior.CanvasId,width=bitmap.PixelWidth,height=bitmap.PixelHeight,transparent,nonTransparent,output,sourceHash=originalHash,outputSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(output))),entries="Full App native UIA shelf/add/remove/undo/redo/canvas menu/library recent menu/save/export; no physical mouse or DPI claim"}));Capture(window,"OFFSCREEN-used-3.png");
    });}
