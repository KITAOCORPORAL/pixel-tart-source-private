using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;
using PixelTart.Modules.AssetLibrary.FreeCanvas;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Projects;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class EveningFeedbackWpfTests
{
    [TestMethod]
    public Task LoadedThemeCannotMakeMarqueeOpaqueAndFolderInputHasTextRoom()=>RunSta(async()=>
    {
        EnsureTestApplication();
        // Exercise the actual runtime accent assignment; ApplyAll also visits HWNDs owned
        // by unrelated STA fixtures in the full suite and is outside this brush contract.
        typeof(AppearanceService).GetMethod("ApplyAccent",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null,[new RAWSelectionAssistant.Core.Models.AppearanceSettings(),false]);
        var root=await RuntimeUserFindingsBatchATests.Fixture();await using var page=new AssetLibraryPage(Path.Combine(root,"assets.db"),new RAWSelectionAssistant.Core.Services.Tasks.TaskOperationBridge(),[]);await page.InitializeForSessionAsync();
        var marquee=(Border)page.FindName("AssetSelectionMarquee");var fill=(SolidColorBrush)marquee.Background;Assert.IsTrue(fill.Color.A>0&&fill.Color.A<40);Assert.IsTrue(marquee.BorderThickness.Left<=1.5);
        var search=(TextBox)page.FindName("OrganizationFolderSearch");search.Text="长中文名称 English # /";
        search.Measure(new(240,500));search.Arrange(new(0,0,240,search.DesiredSize.Height));Assert.IsTrue(search.ActualHeight-search.Padding.Top-search.Padding.Bottom-4>=22);
    });
    [TestMethod]
    public Task RuleTreeWheelAndKeyboardReachEndWithBoundedViewport()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=await RuntimeUserFindingsBatchATests.Fixture();await using var vm=RuntimeUserFindingsBatchATests.ViewModel(root);await vm.InitializeAsync();vm.OpenFilterPanel();for(var i=0;i<12;i++)vm.P3QueryRoot.AddRuleCommand.Execute(null);
        var scroll=new ScrollViewer {Content=new AssetQueryComposerView {DataContext=vm},VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=300};NestedScrollBehavior.SetIsEnabled(scroll,true);
        var window=new Window {Content=scroll,Width=650,Height=350,ShowInTaskbar=false};
        try {window.Show();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);Assert.IsGreaterThan(0,scroll.ScrollableHeight);var tree=Walk(scroll).OfType<TreeView>().First();
            for(var i=0;i<50;i++){tree.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent});await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);}
            Assert.AreEqual(scroll.ScrollableHeight,scroll.VerticalOffset,.1);scroll.ScrollToHome();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            scroll.PageDown();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);Assert.IsGreaterThan(0,scroll.VerticalOffset);
        }finally{window.Close();}
    });
    [TestMethod]
    public Task FitIncludesRotatedObjectsResizesAndIsIdempotent()=>RunSta(async()=>
    {
        var editor=CanvasFixtures.Create(3);editor.Rotate(43);editor.Move(-800,650);var surface=new FreeCanvasSurface(editor,new WpfAssetThumbnailProvider());
        void Arrange(double w,double h){surface.Measure(new(w,h));surface.Arrange(new(0,0,w,h));surface.UpdateLayout();}
        Arrange(800,450);surface.SetZoom(5);surface.Fit();Check();var zoom=surface.Zoom;var pan=surface.Pan;surface.Fit();Assert.AreEqual(zoom,surface.Zoom);Assert.AreEqual(pan,surface.Pan);Arrange(350,220);Check();
        editor.SelectAll();editor.Remove();surface.Fit();Assert.AreEqual(1d,surface.Zoom);await Task.CompletedTask;
        void Check(){var b=surface.GetFitBounds();var a=surface.WorldToScreen(new(b.X,b.Y));var z=surface.WorldToScreen(new(b.X+b.Width,b.Y+b.Height));Assert.IsTrue(a.X>=0&&a.Y>=0&&z.X<=surface.ActualWidth&&z.Y<=surface.ActualHeight,$"{a} {z}");}
    });
    [TestMethod]
    public Task QuickExportKeepsPngAlphaAndTiff16RealPrecision()=>RunSta(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-QuickExport-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try {var alpha=BitmapSource.Create(2,1,96,96,PixelFormats.Bgra32,null,new byte[]{20,30,40,0,40,50,60,127},8);var png=Path.Combine(root,"result.png");StudioQuickExport.Encode(alpha,"source.png",png);var loaded=StudioQuickExport.Load(png);var pixels=new byte[8];new FormatConvertedBitmap(loaded,PixelFormats.Bgra32,null,0).CopyPixels(pixels,8,0);Assert.AreEqual((byte)0,pixels[3]);Assert.AreEqual((byte)127,pixels[7]);
            ushort[] rgb=[10001,22002,33003,65535,40004,50005];var high=BitmapSource.Create(2,1,96,96,PixelFormats.Rgb48,null,rgb,12);var stack=new ColorAdjustmentStack([new(Guid.NewGuid(),ColorStudioNodeType.Develop,"影调")]);var processed=ColorStudioBitmapRenderer.Render(high,stack,null);var tif=Path.Combine(root,"result.tif");StudioQuickExport.Encode(processed,"source.tiff",tif);var decoded=StudioQuickExport.Load(tif);Assert.IsGreaterThan(32,decoded.Format.BitsPerPixel);var values=new ushort[6];new FormatConvertedBitmap(decoded,PixelFormats.Rgb48,null,0).CopyPixels(values,12,0);CollectionAssert.AreEqual(rgb,values);
            Assert.AreEqual(".jpg",StudioQuickExport.Extension("photo.jpeg"));Assert.AreEqual(".png",StudioQuickExport.Extension("photo.PNG"));Assert.AreEqual(".tif",StudioQuickExport.Extension("photo.TIFF"));
        }finally{Directory.Delete(root,true);}await Task.CompletedTask;
    });
    [TestMethod]
    public Task SmartEditorPinsControlsOutsideScrollableRules() => RunSta(async () =>
    {
        EnsureTestApplication(); var root=await RuntimeUserFindingsBatchATests.Fixture();
        await using var vm=RuntimeUserFindingsBatchATests.ViewModel(root); await vm.InitializeAsync();
        vm.NewP3SmartFolderCommand.Execute(null);
        for(var i=0;i<15;i++) vm.P3SmartFolderRoot.AddRuleCommand.Execute(null);
        var view=new AssetSmartFolderEditorView {DataContext=vm};
        foreach(var height in new[]{280d,360d,500d})
        {
            view.Measure(new(650,height)); view.Arrange(new(0,0,650,height)); view.UpdateLayout();
            var scroll=(ScrollViewer)view.FindName("EditorScroll");
            Assert.IsGreaterThan(0,scroll.ScrollableHeight);
            foreach(var id in new[]{"P3SmartFolderClose","P3SmartFolderSave","P3SmartFolderCancel"})
            {
                var button=Walk(view).OfType<Button>().Single(x=>System.Windows.Automation.AutomationProperties.GetAutomationId(x)==id);
                var rect=button.TransformToAncestor(view).TransformBounds(new Rect(button.RenderSize));
                Assert.IsTrue(rect.Top>=0&&rect.Bottom<=height, $"{id}: {rect} at {height}");
            }
        }
        vm.P3SmartFolderName="未保存"; vm.CancelP3SmartFolderCommand.Execute(null);
        Assert.IsTrue(vm.SmartFolderUnsavedGuardOpen);
    });

    [TestMethod]
    public Task ToneHoverUsesSamePixelsAndEscapeAndTargetSwitchClearIt() => RunSta(async () =>
    {
        EnsureTestApplication(); using var workspace=new ReferenceColorWorkspaceViewModel(new NoDialogs());
        var view=new ReferenceColorWorkspaceView {DataContext=workspace};
        byte[] gray=Enumerable.Range(0,256).SelectMany(i=>new[]{(byte)i,(byte)i,(byte)i}).ToArray();
        var source=BitmapSource.Create(256,1,96,96,PixelFormats.Rgb24,null,gray,768); source.Freeze();
        await workspace.Editor.SetSourceAsync(null,source); await workspace.RefreshPreviewAnalysisAsync();
        var stack=workspace.Editor.AdjustmentStack;
        for(var zone=0;zone<11;zone++)
        {
            workspace.HighlightToneZone(zone);
            CollectionAssert.AreEqual(VisualAnalysisEngine.ToneZoneMembers(new(256,1,gray),zone).ToArray(),workspace.HighlightedPixels.ToArray());
        }
        Assert.AreSame(stack,workspace.Editor.AdjustmentStack);
        Assert.IsTrue(view.TryClearTransientInspection()); Assert.IsEmpty(workspace.HighlightedPixels);
        workspace.HighlightToneZone(0); workspace.HighlightToneZone(-1); Assert.IsEmpty(workspace.HighlightedPixels);
        workspace.HighlightToneZone(10); await workspace.Editor.SetSourceAsync(null,null); await workspace.AnalysisWork;
        Assert.IsEmpty(workspace.HighlightedPixels); Assert.IsNull(workspace.PreviewHistogram);
    });

    [TestMethod]
    public Task DevelopResetRestoreAndRealTiffPreviewExportAgree() => RunSta(async () =>
    {
        using var workspace=new ReferenceColorWorkspaceViewModel(new NoDialogs()); var editor=workspace.Editor;
        editor.Shadows=35; editor.Highlights=-25; editor.Structure=15; editor.Exposure=.5;
        var saved=editor.AdjustmentStack.DeepClone(); editor.ResetDevelopGroup("明度");
        Assert.AreEqual(0d,editor.Exposure); Assert.AreEqual(0d,editor.Shadows); Assert.AreEqual(15d,editor.Structure);
        editor.ApplyTargetSnapshot(null,null,saved,false); Assert.AreEqual(.5,editor.Exposure); Assert.AreEqual(35d,editor.Shadows);
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-DevelopExport-"+Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            var values=Enumerable.Range(0,64*64*3).Select(i=>(ushort)(1000+i*41%64000)).ToArray();
            var image=BitmapSource.Create(64,64,96,96,PixelFormats.Rgb48,null,values,64*6); image.Freeze();
            var path=Path.Combine(root,"source.tif"); StudioQuickExport.Encode(image,path,path);
            var input=StudioQuickExport.Load(path);
            var preview=await editor.PreviewColorStudioAsync(input,saved,null);
            var export=await editor.ProcessForExportAsync(path,null,null,CancellationToken.None,saved);
            var a=new byte[64*64*8];var b=new byte[a.Length];preview.CopyPixels(a,64*8,0);export.CopyPixels(b,64*8,0);
            CollectionAssert.AreEqual(a,b); Assert.IsGreaterThan(32,export.Format.BitsPerPixel);
        }
        finally{Directory.Delete(root,true);}
    });
    [TestMethod]
    public Task LoadedTargetRemainsVisibleInShortStudioAndAnalysisCanReclaimSpace() => RunSta(async () =>
    {
        EnsureTestApplication(); using var workspace = new ReferenceColorWorkspaceViewModel(new NoDialogs());
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-ShortStudio-"+Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            var path=Path.Combine(root,"source.png");
            StudioQuickExport.Encode(BitmapSource.Create(120,80,96,96,PixelFormats.Rgb24,null,new byte[120*80*3],360),path,path);
            await workspace.LoadTargetAsync(path);
            var view = new ReferenceColorWorkspaceView { DataContext=workspace };
            foreach(var size in new[]{new Size(1008,632),new Size(740,520),new Size(600,440)})
            {
                void Arrange(){view.Width=size.Width;view.Height=size.Height;view.Measure(size);view.Arrange(new Rect(size));view.UpdateLayout();}
                Arrange();
                var canvas=(FrameworkElement)view.FindName("PreviewCanvas");
                var analysis=(Expander)view.FindName("CentralAnalysis");
                Assert.IsTrue(canvas.ActualHeight>=100,$"Target height {canvas.ActualHeight} at {size}; workspace={((FrameworkElement)view.FindName("WorkspaceGrid")).ActualHeight}, header={((FrameworkElement)view.FindName("SourceHeader")).ActualHeight}, film={((FrameworkElement)view.FindName("FilmstripPanel")).ActualHeight}, analysis={analysis.ActualHeight}/{analysis.IsExpanded}, filmopen={((Expander)view.FindName("FilmstripExpander")).IsExpanded}");
                var collapsed=canvas.ActualHeight;
                analysis.IsExpanded=true;Arrange();
                Assert.IsTrue(canvas.ActualHeight>=70,$"Expanded analysis starves photograph at {size}: {canvas.ActualHeight}");
                analysis.IsExpanded=false;Arrange();Assert.IsTrue(canvas.ActualHeight>=collapsed);
            }
        }
        finally{Directory.Delete(root,true);}
    });

    private sealed class NoDialogs : IDialogService
    {
        public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect)=>[];
        public string? ChooseFolder(string title,string? initialDirectory=null)=>null;
        public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>ids;
        public void ShowInfo(string message){} public void ShowError(string message){}
        public bool Confirm(string message,string title)=>false;
        public HelpAction ShowHelp()=>HelpAction.None; public void ShowFeedback(){}
        public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> candidates)=>null;
        public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem item,bool showAdvancedDetails)=>false;
        public void RevealFile(string path){}
    }
    private static IEnumerable<DependencyObject> Walk(DependencyObject root){yield return root;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var c in Walk(VisualTreeHelper.GetChild(root,i)))yield return c;}
}

