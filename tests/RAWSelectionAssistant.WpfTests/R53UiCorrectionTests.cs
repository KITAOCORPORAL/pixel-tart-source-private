using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Views;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using PixelTart.Modules.AssetLibrary;
using PixelTart.Modules.AssetLibrary.FreeCanvas;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class R53UiCorrectionTests
{
    static void Invoke(object view,string name){var method=view.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!;method.Invoke(view,method.GetParameters().Length==0?null:[view,new RoutedEventArgs()]);}
    static void Layout(FrameworkElement view,double w,double h){view.Measure(new Size(w,h));view.Arrange(new Rect(0,0,w,h));view.UpdateLayout();}
    static void Capture(FrameworkElement view,string path,double scale){var bitmap=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth*scale),(int)Math.Ceiling(view.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);png.Save(stream);}
    [TestMethod]public Task DarkroomDipRailRecoveryFilmFullHeightAndIndependentCollapse()=>RunSta(async()=>
    {
        EnsureTestApplication();using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs());var view=new PhotoGradePageView{DataContext=vm};
        foreach(var scale in new[]{1d,1.25,1.5})
        {
            ((TabControl)view.FindName("EditModes")).SelectedIndex=0;var w=1366/scale;var h=768/scale;Layout(view,w,h);view.ApplyR53Layout();view.UpdateLayout();var rail=(Border)view.FindName("R53ParameterRail");var right=(Grid)view.FindName("RightPanel");
            if(right.Visibility==Visibility.Visible)Invoke(view,"TogglePanel");view.UpdateLayout();Assert.AreEqual(Visibility.Visible,rail.Visibility);Assert.AreEqual(40d,((ColumnDefinition)view.FindName("PanelColumn")).Width.Value);
            Invoke(view,"TogglePanel");view.UpdateLayout();Assert.AreEqual(Visibility.Visible,right.Visibility);Assert.AreEqual(304d,right.ActualWidth,1);view.ApplyR53Layout();Assert.AreEqual(Visibility.Visible,right.Visibility,"Resize/layout must not erase explicit drawer choice");
            ((TabControl)view.FindName("EditModes")).SelectedIndex=1;view.UpdateLayout();var footer=(Border)view.FindName("FilmAction");Assert.AreEqual(Visibility.Visible,footer.Visibility);var bottom=footer.TransformToAncestor(right).Transform(new Point(0,footer.ActualHeight));Assert.AreEqual(right.ActualHeight,bottom.Y,1,"Film footer sticks to right panel bottom");Assert.AreEqual(Visibility.Visible,((Expander)view.FindName("FrameSection")).Visibility);Assert.AreEqual(Visibility.Collapsed,((Expander)view.FindName("PaperPickerSection")).Visibility);
            vm.R53Professional=true;view.ApplyR53Layout();if(((RowDefinition)view.FindName("R53NodeRow")).Height.Value!=22)Invoke(view,"R53ToggleNodes");Assert.AreEqual(22d,((RowDefinition)view.FindName("R53NodeRow")).Height.Value);Invoke(view,"R53ToggleFilmstrip");Assert.AreEqual(22d,((RowDefinition)view.FindName("R53FilmRow")).Height.Value);Invoke(view,"R53ToggleNodes");Assert.AreEqual(22d,((RowDefinition)view.FindName("R53FilmRow")).Height.Value);Invoke(view,"R53ToggleFilmstrip");
        }
        view.DataContext=null;await Task.CompletedTask;
    });
    [TestMethod]public Task FourScreensOffscreenEffectiveDipEvidence()=>RunSta(async()=>
    {
        EnsureTestApplication();var output=Environment.GetEnvironmentVariable("KITAO_R53_UI_EVIDENCE");if(string.IsNullOrEmpty(output)){Assert.Inconclusive("Explicit evidence output required");return;}Directory.CreateDirectory(output);
        var path=Path.Combine(output,"ui-synthetic.png");var rgb=new byte[480*320*3];for(int y=0;y<320;y++)for(int x=0;x<480;x++){var o=(y*480+x)*3;rgb[o]=(byte)(x*255/480);rgb[o+1]=(byte)(y*255/320);rgb[o+2]=(byte)(50+80*Math.Sin(x/80d)*Math.Sin(y/60d));}StudioQuickExport.Encode(BitmapSource.Create(480,320,96,96,PixelFormats.Rgb24,null,rgb,480*3),path,path,StudioExportFormat.Png);
        using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs());await vm.LoadTargetAsync(path);vm.Editor.AddAdjustmentNodeCommand.Execute("Curve");await vm.LayerThumbnailWork;await vm.PreviewPhotographerPapersCommand.ExecuteAsync(null);
        var view=new PhotoGradePageView{DataContext=vm};var evidence=new List<object>();
        foreach(var mode in new[]{"simple","pro","instant"})foreach(var pair in new[]{(1920d,1080d,1d),(1366d,768d,1d),(1366d,768d,1.25),(1366d,768d,1.5)})
        {
            vm.R53Professional=mode=="pro";if(mode=="pro")Invoke(view,"R53Professional");((TabControl)view.FindName("EditModes")).SelectedIndex=mode=="instant"?1:0;var w=pair.Item1/pair.Item3;var h=pair.Item2/pair.Item3;Layout(view,w,h);view.ApplyR53Layout();if(mode=="instant"&&((Grid)view.FindName("RightPanel")).Visibility!=Visibility.Visible)Invoke(view,"TogglePanel");if(mode=="instant")await vm.LightPrintPreviewWork;await Task.Delay(150);view.UpdateLayout();Capture(view,Path.Combine(output,$"offscreen-{mode}-{pair.Item1}-{pair.Item3}.png"),pair.Item3);var photo=(FrameworkElement)view.FindName("ViewerArea");evidence.Add(new{mode,physicalWidth=pair.Item1,scale=pair.Item3,clientDip=w,photoWidth=photo.ActualWidth,photoHeight=photo.ActualHeight,right=((Grid)view.FindName("RightPanel")).Visibility.ToString(),scope="OFFSCREEN SYNTHETIC effective-DIP layout, NOT native DPI/desktop acceptance"});
        }
        view.DataContext=null;await File.WriteAllTextAsync(Path.Combine(output,"LAYOUT_RESULTS.json"),JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true}));
    });
    [TestMethod]public void FullApplicationHostedRailAndMenuOffscreenEvidence()
    {
        var output=Environment.GetEnvironmentVariable("KITAO_R53_UI_EVIDENCE");if(string.IsNullOrEmpty(output)){Assert.Inconclusive("Explicit isolated evidence root required");return;}Exception? failure=null;bool complete=false;
        var thread=new Thread(()=>{try{var app=new App();app.InitializeComponent();var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};var running=false;timer.Tick+=async(_,_)=>
        {
            if(running||app.MainWindow is not MainWindow{IsLoaded:true,DataContext:MainViewModel vm} window)return;running=true;
            try
            {
                vm.ForceExitTutorial();vm.NavigateCommand.Execute("ReferenceColor");await vm.ReferenceColorPage.LoadTargetAsync(Path.Combine(output,"ui-synthetic.png"));var session=Path.Combine(output,"hosted-"+Guid.NewGuid().ToString("N")+".ptstudio.json");await vm.ReferenceColorPage.SaveSessionAsync(session);window.WindowState=WindowState.Normal;
                vm.ReferenceColorPage.Editor.AddAdjustmentNodeCommand.Execute("Curve");
                foreach(var width in new[]{1920,1366})
                {
                    window.Width=width;window.Height=width==1366?768:1080;window.UpdateLayout();await Task.Delay(150);var rail=(Border)window.FindName("SidebarContainer");Assert.AreEqual(40d,rail.ActualWidth,1,"One actual 40 DIP global navigation rail");Assert.AreEqual(Visibility.Collapsed,((Menu)window.FindName("TopMenu")).Visibility);
                    var page=Find<PhotoGradePageView>((DependencyObject)window.Content).Single(v=>v.IsVisible);page.ApplyR53Layout();page.UpdateLayout();Assert.AreEqual(Visibility.Collapsed,((Border)page.FindName("AppRail")).Visibility);Assert.AreEqual(0d,((ColumnDefinition)page.FindName("R53AppRailColumn")).Width.Value);
                    foreach(var mode in new[]{"simple","pro","instant"}){vm.ReferenceColorPage.R53Professional=mode=="pro";if(mode=="simple")Invoke(page,"R53PresetTab");if(mode=="pro")Invoke(page,"R53Professional");((TabControl)page.FindName("EditModes")).SelectedIndex=mode=="instant"?1:0;await vm.ReferenceColorPage.LightPrintPreviewWork;if(mode=="instant"){var until=DateTime.UtcNow.AddSeconds(8);do{vm.ReferenceColorPage.QueueFramePhotoPreviews();await vm.ReferenceColorPage.FramePhotoPreviewWork;await Task.Delay(60);}while(vm.ReferenceColorPage.FramePhotoPreviews.Count!=18&&DateTime.UtcNow<until);Assert.AreEqual(18,vm.ReferenceColorPage.FramePhotoPreviews.Count,vm.ReferenceColorPage.LightPrintStatus);}if(width==1366&&mode!="instant"&&((Grid)page.FindName("RightPanel")).Visibility!=Visibility.Visible)Invoke(page,"TogglePanel");await Task.Delay(100);window.UpdateLayout();Capture((FrameworkElement)window.Content,Path.Combine(output,$"OFFSCREEN-hosted-{mode}-{width}.png"),1);}
                }
                await vm.ReferenceColorPage.SaveSessionAsync(session);complete=true;
            }
            catch(Exception error){failure=error;}finally{timer.Stop();app.Shutdown();}
        };timer.Start();app.Run();}catch(Exception error){failure=error;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(3)),"Bounded full-app harness");if(failure is not null)throw failure;Assert.IsTrue(complete);
    }
    static IEnumerable<T> Find<T>(DependencyObject root)where T:DependencyObject{if(root is T value)yield return value;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Find<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    sealed class Provider:IAssetPreviewProvider{public Task<AssetPreviewResult> GetAsync(AssetPreviewRequest request,CancellationToken token=default)=>throw new InvalidOperationException("Synthetic empty canvas does not load external assets");}
    [TestMethod]public Task CanvasPropertiesLayersAndBrushUseSingleHost()=>RunSta(async()=>
    {
        EnsureTestApplication();var dir=Path.Combine(Path.GetTempPath(),"r53-ui-canvas-"+Guid.NewGuid().ToString("N"));var editor=new CanvasEditor(new CanvasDocument{PixelWidth=1080,PixelHeight=1350});editor.AddText(100,100,"光镜 · 自由画布");editor.AddBrushLayer();var view=new FreeCanvasView(editor,new Provider(),new CanvasDocumentStore(dir));
        try{foreach(var scale in new[]{1d,1.25,1.5}){Layout(view,1366/scale,768/scale);view.Surface.Fit();Invoke(view,"BrushOptions");view.UpdateLayout();var field=typeof(FreeCanvasView).GetField("_inspector",BindingFlags.NonPublic|BindingFlags.Instance)!;var inspector=(Grid)field.GetValue(view)!;Assert.AreEqual(Visibility.Visible,inspector.Visibility);Assert.AreEqual(3,inspector.RowDefinitions.Count);Assert.IsTrue(view.Surface.ActualWidth>300);var output=Environment.GetEnvironmentVariable("KITAO_R53_UI_EVIDENCE");if(!string.IsNullOrEmpty(output))Capture(view,Path.Combine(output,$"offscreen-canvas-1366-{scale}.png"),scale);}Assert.AreEqual(0,Application.Current.Windows.OfType<Window>().Count(w=>w.Title is "普通画笔" or "图层 · 上方在前"));await Task.CompletedTask;}finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
    });
    sealed class Dialogs:IDialogService
    {
        public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect)=>[];public string? ChooseFolder(string title,string? initialDirectory=null)=>null;public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds)=>currentToolIds;public void ShowInfo(string message){}public void ShowError(string message){}public bool Confirm(string message,string title)=>false;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> candidates)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem item,bool showAdvancedDetails)=>false;public void RevealFile(string path){}
    }
}
