using System.IO;
using System.Reflection;
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
[TestClass][DoNotParallelize]
public class R54ClosureUiTests
{
    static async Task Settle(TetherReferenceModeViewModel editor){var end=DateTime.UtcNow.AddSeconds(20);while(!editor.IsSettled&&DateTime.UtcNow<end)await Task.Delay(20);Assert.IsTrue(editor.IsSettled,editor.StatusText);}
    static void Invoke(object page,string name){var m=page.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!;m.Invoke(page,m.GetParameters().Length==0?null:[page,new RoutedEventArgs()]);}
    static void Layout(FrameworkElement v,int w){v.Measure(new Size(w,w==1366?768:1080));v.Arrange(new Rect(0,0,w,w==1366?768:1080));v.UpdateLayout();}
    [TestMethod]public Task SelectedNodeThumbnailsMasksLabelsToolsAndActualPhotoFrameCards()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=Environment.GetEnvironmentVariable("KITAO_R54_CLOSURE_VISUAL")??Path.Combine(Path.GetTempPath(),"closure-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var data=new byte[160*100*3];for(int y=0;y<100;y++)for(int x=0;x<160;x++){var i=(y*160+x)*3;data[i]=(byte)(x*255/159);data[i+1]=(byte)(y*255/99);data[i+2]=(byte)((x+y)%256);}var b=BitmapSource.Create(160,100,96,96,PixelFormats.Rgb24,null,data,160*3);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(b));var path=Path.Combine(root,"synthetic-owned.png");using(var f=File.Create(path))encoder.Save(f);
        using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs());await vm.LoadTargetAsync(path);await vm.CreateR53VariantAsync(true);vm.Editor.AddAdjustmentNodeCommand.Execute("BasicTone");vm.Editor.AddAdjustmentNodeCommand.Execute("Curve");vm.Editor.AddMaskStroke(new([new(.5,.5)],.3,.2,1,1));vm.Editor.SetGraphLabel(3);Assert.IsTrue(vm.Editor.AddParallelGraphBranch());await Settle(vm.Editor);
        var page=new PhotoGradePageView{DataContext=vm};Layout(page,1366);Invoke(page,"OpenR53Panel");vm.R53Professional=true;page.ApplyR53Layout();await vm.LayerThumbnailWork;await Task.Delay(200);Assert.IsNotNull(vm.LayerThumbnailContext,vm.LayerThumbnailFailure);Assert.IsTrue(vm.LayerThumbnailContext.Outputs.Count>=3,vm.LayerThumbnailFailure);var masked=vm.Editor.AdjustmentNodes.Single(n=>n.LocalMask is not null);Assert.IsTrue(vm.LayerThumbnailContext.Masks.ContainsKey(masked.Id));Assert.AreEqual(3,masked.GraphLabel);
        vm.Editor.SelectedAdjustmentNode=masked;page.ApplyR53Layout();var stack=(StackPanel)((ScrollViewer)page.FindName("AdjustmentScroll")).Content;var sections=stack.Children.OfType<Expander>().Where(e=>e.Visibility==Visibility.Visible&&e.Name!="R53SimpleLayers").ToArray();Assert.AreEqual(1,sections.Length);Assert.AreEqual("AdjustmentScroll.4",sections[0].Tag);
        vm.Editor.SelectedAdjustmentNode=vm.Editor.AdjustmentNodes.First(n=>n.Type==ColorStudioNodeType.BasicTone);page.ApplyR53Layout();sections=stack.Children.OfType<Expander>().Where(e=>e.Visibility==Visibility.Visible&&e.Name!="R53SimpleLayers").ToArray();Assert.AreEqual(3,sections.Length);Assert.IsFalse(sections.Any(e=>e.Tag as string=="AdjustmentScroll.5"));
        vm.QueueFramePhotoPreviews();await vm.FramePhotoPreviewWork;Assert.AreEqual(18,vm.FramePhotoPreviews.Count,vm.LightPrintStatus);var thumb=new LightPrintFrameThumbnail{Frame=OriginalFrameLibrary.Frames[0],PhotoPreviews=vm.FramePhotoPreviews};Assert.IsNotNull(thumb.ActualPhotoPreview);
        vm.Layout=vm.Layout with{R54GraphCollapsed=false,R54GraphHeight=230};page.DataContext=null;page.DataContext=vm;
        foreach(var width in new[]{1366,1920})
        {
            Layout(page,width);Invoke(page,"OpenR53Panel");vm.R53Professional=true;page.ApplyR53Layout();((StudioNodeGraphView)((Grid)page.FindName("R53NodeContent")).Children[0]).Fit();page.UpdateLayout();Capture(page,root,$"pro-{width}");vm.R53Professional=false;page.ApplyR53Layout();Capture(page,root,$"simple-{width}");
        }
        Assert.AreEqual(230,vm.Layout.R54GraphHeight);vm.Editor.ProfessionalGroupAmount=.5;var edges=System.Text.Json.JsonSerializer.Serialize(vm.Editor.AdjustmentStack.Graph!.Edges);vm.R53Professional=true;vm.R53Professional=false;Assert.AreEqual(edges,System.Text.Json.JsonSerializer.Serialize(vm.Editor.AdjustmentStack.Graph.Edges));
        vm.R53Professional=false;((TabControl)page.FindName("EditModes")).SelectedIndex=1;vm.SelectedLightPrintFrameId="orig.01";await vm.LightPrintPreviewWork;vm.QueueFramePhotoPreviews();await vm.FramePhotoPreviewWork;Assert.AreEqual(18,vm.FramePhotoPreviews.Count,vm.LightPrintStatus);
        foreach(var width in new[]{1366,1920}){Layout(page,width);page.ApplyR53Layout();Capture(page,root,$"instant-{width}");}
        vm.InstantCameraAnimation=false;await vm.DevelopLightPrintCommand.ExecuteAsync(null);foreach(var width in new[]{1366,1920}){Layout(page,width);page.ApplyR53Layout();Capture(page,root,$"developed-{width}");}
        page.DataContext=null;
    });
    static void Capture(FrameworkElement v,string root,string name){v.UpdateLayout();var image=new RenderTargetBitmap((int)v.ActualWidth,(int)v.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(v);var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(image));using var f=File.Create(Path.Combine(root,"OFFSCREEN-closure-"+name+".png"));e.Save(f);}
    sealed class Dialogs:IDialogService
    {
        public IReadOnlyList<string> ChooseFiles(string t,string f,bool m)=>[];public string? ChooseFolder(string t,string? initialDirectory=null)=>null;public string? ChooseSaveFile(string t,string f,string e,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>ids;public void ShowInfo(string m){}public void ShowError(string m){}public bool Confirm(string m,string t)=>false;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool b)=>false;public void RevealFile(string p){}
    }
}
