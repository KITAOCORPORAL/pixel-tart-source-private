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
        workspace.SelectionTolerance=.02;Assert.IsEmpty(workspace.HighlightedPixels);
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
    public Task LeftEditingControlsAndRightAnalysisRemainBoundedAcrossViewportSizes() => RunSta(async()=>
    {
        if(Application.Current is null){var app=new App();app.InitializeComponent();}
        using var workspace=new ReferenceColorWorkspaceViewModel(new TestDialogs());
        var view=new ReferenceColorWorkspaceView { DataContext=workspace };
        foreach(var scale in new[]{1d,1.25,1.5,2d})
        foreach(var size in new[]{new Size(1180,720),new Size(1600,920),new Size(1920,1080)})
        {
            var logical=new Size(size.Width/scale,size.Height/scale);view.Width=logical.Width;view.Height=logical.Height;
            view.Measure(logical);view.Arrange(new Rect(logical));view.UpdateLayout();
            var left=(FrameworkElement)view.FindName("LeftRail");var right=(FrameworkElement)view.FindName("RightRail");
            var picker=(FrameworkElement)view.FindName("SourcePickerAnchor");
            Assert.IsTrue(IsDescendant(picker,left));
            Assert.IsTrue(Descendants(right).OfType<HistogramDrawing>().Any());
            Assert.IsFalse(Descendants(right).OfType<Slider>().Any(x=>System.Windows.Data.BindingOperations.GetBindingExpression(x,Slider.ValueProperty)?.ParentBinding.Path?.Path=="WeightPercent"));
            Assert.IsLessThanOrEqualTo(logical.Width+.01,((FrameworkElement)view.FindName("WorkspaceGrid")).ActualWidth);
        }
        await Task.CompletedTask;
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
        if(Application.Current is null){var app=new App();app.InitializeComponent();}
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
            .SelectMany(source=>source.RootVisual is DependencyObject root?Descendants(root):[]).OfType<ContextMenu>().LastOrDefault();
        Assert.IsNotNull(menu);Assert.IsFalse(menu.Items.OfType<MenuItem>().Single(x=>Equals(x.Header,"粘贴")).IsEnabled);menu.IsOpen=false;
        editor.AddText(0,0,"测试文字");editor.Copy();
        edit.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));await Task.Delay(30);
        menu=System.Windows.PresentationSource.CurrentSources.OfType<System.Windows.Interop.HwndSource>().Where(source=>source.Dispatcher.CheckAccess())
            .SelectMany(source=>source.RootVisual is DependencyObject root?Descendants(root):[]).OfType<ContextMenu>().Last();
        var paste=menu.Items.OfType<MenuItem>().Single(x=>Equals(x.Header,"粘贴"));Assert.IsTrue(paste.IsEnabled);
        paste.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Assert.HasCount(2,editor.Document.Objects);menu.IsOpen=false;
        view.Surface.SetZoom(1.5);
        Assert.HasCount(1,Descendants(view.HeaderPanel).OfType<Button>().Where(x=>x.Content?.ToString()?.Contains("150%") == true).ToArray());
        await view.FlushAsync();window.Close();
    });
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
