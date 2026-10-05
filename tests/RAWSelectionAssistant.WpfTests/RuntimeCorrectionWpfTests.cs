using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using PixelTart.Modules.AssetLibrary;
using static RAWSelectionAssistant.WpfTests.RuntimeUserFindingsBatchATests;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class RuntimeCorrectionWpfTests
{
    [TestMethod]
    public Task AnalysisInvalidatesOnTargetChangeAndMappingDoesNotEditParameters() => RunSta(async()=>
    {
        using var workspace=new ReferenceColorWorkspaceViewModel(new TestDialogs());
        var red=Image(255,0,0); var blue=Image(0,0,255);
        await workspace.Editor.SetSourceAsync(null,red);
        await workspace.RefreshPreviewAnalysisAsync();
        Assert.AreEqual(64u,workspace.PreviewHistogram!.R[255]);
        var cached=workspace.PreviewHistogram;
        await workspace.RefreshPreviewAnalysisAsync(); Assert.AreSame(cached,workspace.PreviewHistogram);
        var stack=workspace.Editor.AdjustmentStack;
        workspace.HighlightImageSample(new(255,0,0)); Assert.HasCount(64,workspace.HighlightedPixels);
        Assert.AreSame(stack,workspace.Editor.AdjustmentStack);
        workspace.SelectionTolerance=.02;Assert.HasCount(64,workspace.HighlightedPixels,"Changing tolerance recomputes the current exact-color inspection instead of losing it.");
        workspace.HighlightCloudSelection(0);Assert.HasCount(64,workspace.HighlightedPixels);
        workspace.ClearColorSpaceHighlight();Assert.IsEmpty(workspace.HighlightedPixels);
        await workspace.Editor.SetSourceAsync(null,blue);
        await workspace.RefreshPreviewAnalysisAsync();
        Assert.AreEqual(64u,workspace.PreviewHistogram!.B[255]);Assert.AreEqual(0u,workspace.PreviewHistogram.R[255]);
        Assert.IsTrue(workspace.AnalysisIsOriginal);
        var pending=workspace.RefreshPreviewAnalysisAsync();
        await workspace.Editor.SetSourceAsync(null,null);await pending;await workspace.AnalysisWork;
        Assert.IsNull(workspace.ColorSpaceModel);Assert.IsNull(workspace.PreviewHistogram);
    });

    [TestMethod]
    public Task InspectionCanAnalyzeResultWithoutChangingViewOrAdjustmentState() => RunSta(async()=>
    {
        using var workspace=new ReferenceColorWorkspaceViewModel(new TestDialogs());
        await workspace.Editor.SetSourceAsync(null,Image(255,0,0));
        var mode=workspace.Editor.ViewMode;var stack=workspace.Editor.AdjustmentStack;
        await workspace.HighlightDisplayedImageSampleAsync(Image(0,255,0),new(0,255,0));
        Assert.IsFalse(workspace.AnalysisIsOriginal);Assert.AreEqual(64u,workspace.PreviewHistogram!.G[255]);
        Assert.HasCount(64,workspace.HighlightedPixels);Assert.AreEqual(mode,workspace.Editor.ViewMode);Assert.AreSame(stack,workspace.Editor.AdjustmentStack);
    });

    [TestMethod]
    public Task RightHistogramsAndToolsRemainBoundedAcrossViewportSizes() => RunSta(async()=>
    {
        EnsureTestApplication();
        using var workspace=new ReferenceColorWorkspaceViewModel(new TestDialogs());
        var view=new ReferenceColorWorkspaceView { DataContext=workspace };
        ((ListBox)view.FindName("AuxModes")).SelectedIndex=0;
        foreach(var scale in new[]{1d,1.25,1.5,2d})
        foreach(var size in new[]{new Size(1180,720),new Size(1600,920),new Size(1920,1080)})
        {
            var logical=new Size(size.Width/scale,size.Height/scale);view.Width=logical.Width;view.Height=logical.Height;
            view.Measure(logical);view.Arrange(new Rect(logical));view.UpdateLayout();
            var left=(FrameworkElement)view.FindName("EditingRail");var right=(FrameworkElement)view.FindName("ContextRail");
            var picker=(FrameworkElement)view.FindName("SourcePickerAnchor");
            Assert.IsTrue(((FrameworkElement)view.FindName("ReferencePage")).IsAncestorOf(picker));
            Assert.HasCount(2,Descendants((FrameworkElement)view.FindName("CentralAnalysis")).OfType<StudioHistogramView>().ToArray());
            Assert.IsTrue(IsDescendant((FrameworkElement)view.FindName("CentralAnalysis"),left));
            Assert.AreEqual(0,Grid.GetColumn(right));
            Assert.IsLessThanOrEqualTo(logical.Width+.01,((FrameworkElement)view.FindName("WorkspaceGrid")).ActualWidth);
        }
        await Task.CompletedTask;
    });

    [TestMethod]
    public Task CompactInspectionNeverCoversTargetAndCollapseReclaimsSpace() => RunSta(async () =>
    {
        EnsureTestApplication();
        using var workspace = new ReferenceColorWorkspaceViewModel(new TestDialogs());
        await workspace.Editor.SetSourceAsync(null, Image(255, 0, 0));
        var view = new ReferenceColorWorkspaceView { DataContext = workspace };
        foreach (var width in new[] { 1000d, 1332d, 1652d })
        {
            void Arrange() { view.Width = width; view.Height = 720; view.Measure(new Size(width, 720)); view.Arrange(new Rect(0, 0, width, 720)); view.UpdateLayout(); }
            Arrange(); workspace.Editor.ContextRailOpen = true; Arrange();
            var target = (FrameworkElement)view.FindName("PreviewCanvas");
            var right = (FrameworkElement)view.FindName("ContextRail");
            var targetBounds = target.TransformToAncestor(view).TransformBounds(new Rect(target.RenderSize));
            var rightBounds = right.TransformToAncestor(view).TransformBounds(new Rect(right.RenderSize));
            Assert.IsTrue(rightBounds.Right <= targetBounds.Left, $"Inspection covers target at {width}: {targetBounds} / {rightBounds}");
            Assert.IsTrue(rightBounds.Right <= width + .01);
            workspace.Editor.ContextRailOpen = false; Arrange();
            var collapsedBounds = target.TransformToAncestor(view).TransformBounds(new Rect(target.RenderSize));
            Assert.IsTrue(collapsedBounds.Width > targetBounds.Width, "A collapsed context rail must return its width to the photograph.");
            Assert.IsGreaterThanOrEqualTo(targetBounds.Height, collapsedBounds.Height);
        }
    });

    [TestMethod]
    public Task SidebarRenameRefreshesTreeInspectorAndSameSelectedTagQuery() => RunSta(async()=>
    {
        var root=await Fixture();await using var vm=ViewModel(root);await vm.InitializeAsync();
        var folder=vm.OrganizationFolders.Single(x=>x.Name=="旅途");
        await vm.RenameOrganizationAsync(folder,"旅途 English & 长名称");
        Assert.IsTrue(vm.OrganizationFolders.Any(x=>x.FolderId==folder.FolderId&&x.Name=="旅途 English & 长名称"));
        vm.SyncSelection([vm.AssetCards[0].Asset]);
        for(var i=0;!vm.CanEditInspectorRelations&&i<100;i++)await Task.Delay(20);
        vm.InspectorTagSearch="重命名测试";vm.CreateInspectorTagCommand.Execute(null);await vm.CreateInspectorTagCommand.ExecutionTask;
        var tag=vm.OrganizationTagGroups.SelectMany(x=>x.Children).Single(x=>x.Name=="重命名测试");
        tag.SelectCommand.Execute(null);
        await vm.RenameOrganizationAsync(tag,"标签 & English 100%");
        Assert.AreEqual(tag.Tag.TagId,vm.SelectedTag?.TagId);
        Assert.AreEqual("标签 & English 100%",vm.SelectedTag?.Name);
        Assert.IsTrue(vm.InspectorTagChips.Any(x=>x.Name=="标签 & English 100%"));
    });
    [TestMethod]
    public Task CanvasMenusExecuteClipboardAndShareZoomStateAtAllViewportSizes() => RunSta(async()=>
    {
        EnsureTestApplication();
        var editor=new RAWSelectionAssistant.Core.Services.FreeCanvas.CanvasEditor(new());
        var store=new RAWSelectionAssistant.Core.Services.FreeCanvas.CanvasDocumentStore(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"canvas-menu-test",Guid.NewGuid().ToString("N")));
        var view=new PixelTart.Modules.AssetLibrary.FreeCanvas.FreeCanvasView(editor,new WpfAssetThumbnailProvider(),store);
        foreach(var scale in new[]{1d,1.25,1.5,2d})
        foreach(var size in new[]{new Size(1180,720),new Size(1600,920),new Size(1920,1080)})
        {
            var logical=new Size(size.Width/scale,size.Height/scale);view.Width=logical.Width;view.Height=logical.Height;
            view.Measure(logical);view.Arrange(new Rect(logical));view.UpdateLayout();
            foreach(var button in Descendants(view.HeaderPanel).OfType<Button>())
            {
                var bounds=button.TransformToAncestor(view).TransformBounds(new Rect(button.RenderSize));
                Assert.IsTrue(bounds.Left>=0&&bounds.Right<=logical.Width+.1,$"Clipped {button.Content} at {logical}");
            }
        }
        var edit=Descendants(view.HeaderPanel).OfType<Button>().Single(b=>Equals(b.Content,"编辑"));
        edit.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        // Discover the real popup created by the click, not source-text assertions.
        var menu=System.Windows.PresentationSource.CurrentSources.OfType<System.Windows.Interop.HwndSource>().Where(source=>source.Dispatcher.CheckAccess())
            .SelectMany(source=>source.RootVisual is DependencyObject root?Descendants(root):[]).OfType<ContextMenu>().FirstOrDefault();
        // A disconnected view may not host a popup HWND: use a real host for behavior below.
        var window=new Window { Content=view,Width=1180,Height=720,ShowInTaskbar=false };window.Show();
        edit.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        await Task.Delay(30);
        menu=System.Windows.PresentationSource.CurrentSources.OfType<System.Windows.Interop.HwndSource>().Where(source=>source.Dispatcher.CheckAccess())
            .SelectMany(source=>source.RootVisual is DependencyObject root?Descendants(root):[]).OfType<ContextMenu>().LastOrDefault(x => x.IsOpen && ReferenceEquals(x.PlacementTarget, edit));
        Assert.IsNotNull(menu);Assert.IsFalse(menu.Items.OfType<MenuItem>().Single(x=>Equals(x.Header,"粘贴")).IsEnabled);menu.IsOpen=false;
        editor.AddText(0,0,"测试文字");editor.Copy();
        edit.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));await Task.Delay(30);
        menu=System.Windows.PresentationSource.CurrentSources.OfType<System.Windows.Interop.HwndSource>().Where(source=>source.Dispatcher.CheckAccess())
            .SelectMany(source=>source.RootVisual is DependencyObject root?Descendants(root):[]).OfType<ContextMenu>().Last(x => x.IsOpen && ReferenceEquals(x.PlacementTarget, edit));
        var paste=menu.Items.OfType<MenuItem>().Single(x=>Equals(x.Header,"粘贴"));Assert.IsTrue(paste.IsEnabled);
        paste.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Assert.HasCount(2,editor.Document.Objects);menu.IsOpen=false;
        view.Surface.SetZoom(1.5);
        Assert.HasCount(1,Descendants(view.HeaderPanel).OfType<Button>().Where(x=>x.Content?.ToString()?.Contains("150%") == true).ToArray());
        await view.FlushAsync();window.Close();
    });
    [TestMethod]
    public Task EscapeClearsTransientInspectionBeforeShellNavigation() => RunSta(async () =>
    {
        EnsureTestApplication();
        using var workspace = new ReferenceColorWorkspaceViewModel(new TestDialogs());
        var view = new ReferenceColorWorkspaceView { DataContext = workspace };
        await workspace.Editor.SetSourceAsync(null, Image(255, 0, 0));
        await workspace.RefreshPreviewAnalysisAsync();
        workspace.HighlightImageSample(new(255, 0, 0));
        var source = workspace.Editor.SourceImage;
        var stack = workspace.Editor.AdjustmentStack;
        Assert.IsTrue(view.TryClearTransientInspection());
        Assert.IsEmpty(workspace.HighlightedPixels);
        Assert.AreSame(source, workspace.Editor.SourceImage);
        Assert.AreSame(stack, workspace.Editor.AdjustmentStack);
        Assert.IsFalse(view.TryClearTransientInspection());
    });

    [TestMethod]
    public Task CloudInspectionControlsShareTransientSettings() => RunSta(async () =>
    {
        EnsureTestApplication();
        using var workspace = new ReferenceColorWorkspaceViewModel(new TestDialogs());
        var controls = new CloudInspectionControls { DataContext = workspace, IsExpanded = true };
        var window = new Window { Content = controls, Width = 500, Height = 400, ShowInTaskbar = false, ShowActivated = false, Left = -32000 };
        window.Show();
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.DataBind);
        var sliders = Descendants(controls).OfType<Slider>().ToArray();
        Assert.HasCount(3, sliders);
        sliders.Single(x => System.Windows.Automation.AutomationProperties.GetName(x) == "点大小").Value = 4;
        sliders.Single(x => System.Windows.Automation.AutomationProperties.GetName(x) == "不透明度").Value = .4;
        sliders.Single(x => System.Windows.Automation.AutomationProperties.GetName(x) == "颜色容差").Value = .03;
        Assert.AreEqual(4d, workspace.CloudPointSize);
        Assert.AreEqual(.4, workspace.CloudPointOpacity);
        Assert.AreEqual(.03, workspace.SelectionTolerance);
        Assert.IsEmpty(workspace.Editor.AdjustmentNodes);
        window.Close();
    });

    // Keep the class dispatcher alive: shutting down an STA that owns Application
    [TestMethod]
    public Task ClosingOwnerWithSavedCanvasDefersCloseAndCoalescesRequests() => RunSta(async () =>
    {
        EnsureTestApplication();
        var root = await Fixture();
        await using var page = new AssetLibraryPage(System.IO.Path.Combine(root, "assets.db"), new RAWSelectionAssistant.Core.Services.Tasks.TaskOperationBridge(), []);
        await page.InitializeForSessionAsync();
        var owner = new Window { Width = 1180, Height = 720, ShowInTaskbar = false, ShowActivated = false, Left = -32000 };
        // Exercise the actual closing handler and saved canvas against a real HWND.
        // Keep the page disconnected: the suite's process-wide Application belongs to
        // another STA, and page Unloaded has a separate shell/Application dependency.
        var handler = (System.ComponentModel.CancelEventHandler)Delegate.CreateDelegate(
            typeof(System.ComponentModel.CancelEventHandler), page,
            typeof(AssetLibraryPage).GetMethod("CanvasOwnerClosing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!);
        owner.Closing += handler;
        var closed = false;
        Exception? unhandled = null;
        System.Windows.Threading.DispatcherUnhandledExceptionEventHandler capture = (_, e) => { unhandled = e.Exception; e.Handled = true; };
        owner.Dispatcher.UnhandledException += capture;
        owner.Closed += (_, _) => closed = true;
        try
        {
            owner.Show();
            await page.ShowCanvasAsync(new RAWSelectionAssistant.Core.Services.FreeCanvas.CanvasDocument());
            Assert.IsTrue(await page.ActiveCanvas!.FlushAsync());
            owner.Close(); owner.Close();
            Assert.IsFalse(closed, "Closing must leave the initial WPF Closing stack before retrying.");
            for (var i = 0; !closed && unhandled is null && i < 100; i++) await Task.Delay(20);
            Assert.IsNull(unhandled, unhandled?.ToString());
            Assert.IsTrue(closed, "A saved canvas must allow the owner to close without prompting or crashing.");
        }
        finally { owner.Dispatcher.UnhandledException -= capture; owner.Closing -= handler; }
    });

    // permanently ends the process-wide WPF lifecycle and poisons later tests.
    private static readonly Lazy<System.Windows.Threading.Dispatcher> UiDispatcher = new(() =>
    {
        var ready = new TaskCompletionSource<System.Windows.Threading.Dispatcher>();
        var thread = new Thread(() =>
        {
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
            ready.SetResult(dispatcher);
            System.Windows.Threading.Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return ready.Task.GetAwaiter().GetResult();
    });
    internal static void EnsureTestApplication()
    {
        if (Application.Current is not null) return;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var root = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !System.IO.File.Exists(System.IO.Path.Combine(root.FullName, "RAWSelectionAssistant.sln"))) root = root.Parent;
        var document = System.Xml.Linq.XDocument.Load(System.IO.Path.Combine(root!.FullName, "src/RAWSelectionAssistant/App.xaml"));
        foreach (var source in document.Descendants().Attributes("Source"))
            app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/KitaoPhotoSelector;component/" + source.Value, UriKind.Relative)));
    }

    internal static Task RunSta(Func<Task> action) => UiDispatcher.Value.InvokeAsync(action).Task.Unwrap();

    private static BitmapSource Image(byte r,byte g,byte b)
    {var pixels=Enumerable.Range(0,64).SelectMany(_=>new[]{r,g,b}).ToArray();var image=BitmapSource.Create(8,8,96,96,PixelFormats.Rgb24,null,pixels,24);image.Freeze();return image;}
    private static bool IsDescendant(DependencyObject element,DependencyObject parent)
    {for(var p=element;p is not null;p=VisualTreeHelper.GetParent(p))if(ReferenceEquals(p,parent))return true;return false;}
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}}
    private sealed class TestDialogs : RAWSelectionAssistant.Services.IDialogService
    {
        public IReadOnlyList<string> ChooseFiles(string title, string filter, bool multiselect) => [];
        public string? ChooseFolder(string title, string? initialDirectory = null) => null;
        public string? ChooseSaveFile(string title, string filter, string defaultExtension, string? suggestedFileName = null) => null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds) => currentToolIds;
        public void ShowInfo(string message) { } public void ShowError(string message) { }
        public bool Confirm(string message, string title) => false;
        public RAWSelectionAssistant.Services.HelpAction ShowHelp() => RAWSelectionAssistant.Services.HelpAction.None;
        public void ShowFeedback() { }
        public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> candidates) => null;
        public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem item, bool showAdvancedDetails) => false;
        public void RevealFile(string path) { }
    }

}
