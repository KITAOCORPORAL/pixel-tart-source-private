using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;

// Observation-only audit: records the existing product, not approval of spec completeness.
[TestClass][DoNotParallelize]
public sealed class R54DesignRecheckTests
{
    static void Layout(FrameworkElement view,int width,int height=768)
    {view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();}
    static void Invoke(PhotoGradePageView view,string name)
    {var method=typeof(PhotoGradePageView).GetMethod(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;method.Invoke(view,method.GetParameters().Length==0?null:[view,new RoutedEventArgs()]);}
    static IEnumerable<T> Descendants<T>(DependencyObject root)where T:DependencyObject
    {if(root is T item)yield return item;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Descendants<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    static void Capture(FrameworkElement view,string file,int width=1366,int height=768)
    {var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(file);encoder.Save(stream);}
    [TestMethod]public Task RecordActualSelectedToolsAndLayoutAgainstSuppliedDesign()=>RunSta(async()=>
    {
        var root=Environment.GetEnvironmentVariable("KITAO_R54_DESIGN_RECHECK");if(root is null){Assert.Inconclusive("Explicit design audit root required");return;}
        Directory.CreateDirectory(root);EnsureTestApplication();
        var data=Enumerable.Range(0,96*64*3).Select(i=>(byte)((i*17)%251)).ToArray();var image=BitmapSource.Create(96,64,96,96,PixelFormats.Rgb24,null,data,96*3);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));var source=Path.Combine(root,"audit-synthetic.png");using(var stream=File.Create(source))png.Save(stream);
        using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs());await vm.LoadTargetAsync(source);await vm.CreateR53VariantAsync(true);
        var view=new PhotoGradePageView{DataContext=vm};Layout(view,1366);vm.R53Professional=true;Invoke(view,"OpenR53Panel");view.ApplyR53Layout();
        var initialHeight=((RowDefinition)view.FindName("R53NodeRow")).Height.Value;
        Assert.AreEqual(22,initialHeight);Invoke(view,"R53ToggleNodes");
        var measurements=new List<object>();
        foreach(var type in new[]{ColorStudioNodeType.BasicTone,ColorStudioNodeType.WhiteBalance,ColorStudioNodeType.Levels,ColorStudioNodeType.Curve,ColorStudioNodeType.ColorBalance,ColorStudioNodeType.ColorRange,ColorStudioNodeType.SkinTone,ColorStudioNodeType.Details})
        {
            vm.Editor.AddAdjustmentNodeCommand.Execute(type.ToString());var node=vm.Editor.AdjustmentNodes.Last(n=>n.Type==type);vm.Editor.SelectedAdjustmentNode=node;
            for(int i=0;i<500&&!vm.Editor.IsSettled;i++)await Task.Delay(15);Assert.IsTrue(vm.Editor.IsSettled,vm.Editor.StatusText);await vm.LayerThumbnailWork;
            Layout(view,1366);view.ApplyR53Layout();view.UpdateLayout();
            var graph=((Grid)view.FindName("R53NodeContent")).Children.OfType<StudioNodeGraphView>().Single();graph.Fit();view.UpdateLayout();
            var sections=((StackPanel)((ScrollViewer)view.FindName("AdjustmentScroll")).Content).Children.OfType<Expander>().Where(x=>x.Visibility==Visibility.Visible).ToArray();
            var header=typeof(PhotoGradePageView).GetField("r54NodeHeader",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(view) as StackPanel;
            measurements.Add(new{type=type.ToString(),expectedNodeName=node.Name,actualSelectedType=vm.Editor.SelectedAdjustmentNode?.Type.ToString(),actualSelectedName=vm.Editor.SelectedNodeName,headerText=header?.Children.OfType<TextBlock>().FirstOrDefault()?.Text,selectedId=node.Id,visibleSections=sections.Select(s=>new{tag=s.Tag?.ToString(),header=s.Header?.ToString(),enabled=s.IsEnabled,sliders=Descendants<Slider>(s).Count(),textInputs=Descendants<TextBox>(s).Count(),graphics=Descendants<FrameworkElement>(s).Where(x=>x.GetType().Name.Contains("Wheel")||x.GetType().Name.Contains("Curve")).Select(x=>x.GetType().Name).ToArray()}).ToArray(),catalogParameterCount=ColorStudioToolCatalog.GetParameters(type).Count()});
            Capture(view,Path.Combine(root,"tool-"+type+"-1366-OFFSCREEN.png"));
        }
        var layouts=new List<object>();
        foreach(var width in new[]{1366,1920,1093,911})
        {
            int height=width==1920?1080:width==1093?614:width==911?512:768;Layout(view,width,height);view.ApplyR53Layout();
            var photo=(FrameworkElement)view.FindName("PhotoViewport");var right=(FrameworkElement)view.FindName("RightPanel");var nodeRow=(RowDefinition)view.FindName("R53NodeRow");
            layouts.Add(new{width,height,photoWidth=photo.ActualWidth,photoHeight=photo.ActualHeight,nodeHeight=nodeRow.Height.Value,right=right.Visibility.ToString(),scope="effective DIP; user-expanded state; not physical DPI"});
        }
        var heightStates=new List<object>();
        vm.R53Professional=false;Layout(view,1366);view.ApplyR53Layout();
        void RecordHeight(string state){var viewer=(FrameworkElement)view.FindName("ViewerArea");var photo=(FrameworkElement)view.FindName("PhotoViewport");heightStates.Add(new{state,viewerHeight=viewer.ActualHeight,photoViewportHeight=photo.ActualHeight,nodeHeight=((RowDefinition)view.FindName("R53NodeRow")).Height.Value});}
        RecordHeight("simple");Capture(view,Path.Combine(root,"simple-linear-1366-OFFSCREEN.png"));
        vm.R53Professional=true;view.ApplyR53Layout();view.UpdateLayout();RecordHeight("pro-expanded");Invoke(view,"R53ToggleNodes");view.UpdateLayout();RecordHeight("pro-collapsed");Invoke(view,"R53ToggleNodes");
        vm.Editor.SelectedAdjustmentNode=vm.Editor.AdjustmentNodes.First(n=>n.Type==ColorStudioNodeType.Curve);Assert.IsTrue(vm.Editor.AddParallelGraphBranch());
        vm.R53Professional=false;Layout(view,1366);view.ApplyR53Layout();view.UpdateLayout();var list=(ListBox)view.FindName("R53SimpleLayerList");
        var groupStack=((StackPanel)((Expander)view.FindName("R53SimpleLayers")).Content).Children.OfType<StackPanel>().FirstOrDefault(s=>s.Children.OfType<TextBlock>().Any(t=>t.Text=="专业组 · 锁定路由"));
        Capture(view,Path.Combine(root,"complex-group-1366-OFFSCREEN.png"));
        var complexObservation=new{ordinaryLayerListVisible=list.Visibility==Visibility.Visible,groupActualHeight=groupStack?.ActualHeight,groupExists=groupStack is not null};
        Invoke(view,"R53InstantTab");vm.InstantCameraAnimation=false;vm.SelectedLightPrintFrameId="orig.01";await vm.LightPrintPreviewWork;await vm.DevelopLightPrintCommand.ExecuteAsync(null);Assert.IsTrue(vm.IsLightPrintLocked,vm.LightPrintStatus);
        vm.R53Professional=true;((TabControl)view.FindName("EditModes")).SelectedIndex=0;Layout(view,1366);view.ApplyR53Layout();view.UpdateLayout();
        Capture(view,Path.Combine(root,"developed-professional-1366-OFFSCREEN.png"));
        var developedNodes=vm.Editor.AdjustmentNodes.Select(n=>new{type=n.Type.ToString(),n.Name,n.SystemLocked,n.UserLocked}).ToArray();
        await File.WriteAllTextAsync(Path.Combine(root,"OBSERVATIONS.json"),JsonSerializer.Serialize(new{kind="observation not design acceptance",initial1366NodeHeight=initialHeight,measurements,layouts,heightStates,complexGroup=complexObservation,developedNodes,scope="OFFSCREEN; product unchanged; no browser or native input"},new JsonSerializerOptions{WriteIndented=true}));
        view.DataContext=null;
    });
    sealed class Dialogs:IDialogService
    {
        public IReadOnlyList<string> ChooseFiles(string a,string b,bool c)=>[];public string? ChooseFolder(string a,string? b=null)=>null;public string? ChooseSaveFile(string a,string b,string c,string? d=null)=>null;public bool Confirm(string a,string b)=>false;public void ShowInfo(string a){}public void ShowError(string a){}public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string>x)=>x;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry>x)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem x,bool y)=>false;public void RevealFile(string p){}
    }
}
