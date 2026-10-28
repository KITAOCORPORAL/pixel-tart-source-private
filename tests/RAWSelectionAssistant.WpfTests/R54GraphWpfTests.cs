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
public sealed class R54GraphWpfTests
{
    static void Invoke(object view,string name){var m=view.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!;m.Invoke(view,m.GetParameters().Length==0?null:[view,new RoutedEventArgs()]);}
    static void Layout(FrameworkElement view,double w,double h){view.Measure(new Size(w,h));view.Arrange(new Rect(0,0,w,h));view.UpdateLayout();}
    static void Capture(FrameworkElement view,string path){var b=new RenderTargetBitmap((int)view.ActualWidth,(int)view.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(path);png.Save(f);}
    [TestMethod] public Task GraphRouteUsesSharedUndoLockAndOffscreenLayout()=>RunSta(async()=>
    {
        EnsureTestApplication();using var vm=new ReferenceColorWorkspaceViewModel(new NoDialogs());var view=new PhotoGradePageView{DataContext=vm};Layout(view,1366,768);
        var editor=vm.Editor;editor.AddAdjustmentNodeCommand.Execute("BasicTone");editor.AddAdjustmentNodeCommand.Execute("Curve");Assert.IsTrue(editor.AdjustmentNodes.Count>=2);
        var graph=editor.GraphView;var selected=editor.AdjustmentNodes.Last();Assert.IsTrue(editor.CommitGraph(graph.Move(selected.Id,310,150),false));Assert.IsNotNull(editor.AdjustmentStack.Graph);
        editor.UndoAdjustmentCommand.Execute(null);Assert.IsNull(editor.AdjustmentStack.Graph);editor.RedoAdjustmentCommand.Execute(null);Assert.AreEqual(150,editor.GraphView.Positions.Single(p=>p.NodeId==selected.Id).Y);
        editor.SelectedAdjustmentNode=selected;editor.SelectedNodeLocked=true;Assert.IsFalse(editor.CommitGraph(editor.GraphView.Move(selected.Id,10,10),false));editor.SelectedNodeLocked=false;
        vm.R53Professional=true;view.ApplyR53Layout();view.UpdateLayout();Assert.AreEqual(22,((RowDefinition)view.FindName("R53NodeRow")).Height.Value);
        vm.Layout=vm.Layout with{R54GraphCollapsed=false,R54GraphHeight=216};view.DataContext=null;view.DataContext=vm;Layout(view,1366,768);view.ApplyR53Layout();view.UpdateLayout();Assert.AreEqual(216,((RowDefinition)view.FindName("R53NodeRow")).Height.Value);
        var content=(Grid)view.FindName("R53NodeContent");Assert.IsInstanceOfType<StudioNodeGraphView>(content.Children[0]);Assert.AreEqual(3,Grid.GetRow((Border)view.FindName("FilmstripArea")));Assert.AreEqual(2,Grid.GetRow((Border)view.FindName("R53SerialNodes")));
        Assert.IsTrue(editor.AddParallelGraphBranch(),editor.StatusText);Assert.AreEqual(2,editor.AdjustmentStack.Graph!.Version);Assert.IsTrue(editor.AdjustmentNodes.Any(n=>n.Type==ColorStudioNodeType.GraphMix));
        vm.R53Professional=false;view.ApplyR53Layout();Assert.AreEqual(Visibility.Collapsed,((ListBox)view.FindName("R53SimpleLayerList")).Visibility);editor.BeginEditTransaction();editor.ProfessionalGroupAmount=.3;editor.ProfessionalGroupAmount=.4;editor.CommitEditTransaction();Assert.AreEqual(.4,editor.AdjustmentStack.Graph.GroupAmount);editor.UndoAdjustmentCommand.Execute(null);Assert.AreEqual(1,editor.AdjustmentStack.Graph!.GroupAmount);editor.RedoAdjustmentCommand.Execute(null);Assert.AreEqual(.4,editor.AdjustmentStack.Graph!.GroupAmount);vm.R53Professional=true;
        var root=Environment.GetEnvironmentVariable("KITAO_R54_EVIDENCE");if(!string.IsNullOrEmpty(root)){Directory.CreateDirectory(root);foreach(var width in new[]{1366,1920}){Invoke(view,"OpenR53Panel");Layout(view,width,width==1366?768:1080);view.ApplyR53Layout();view.UpdateLayout();((StudioNodeGraphView)((Grid)view.FindName("R53NodeContent")).Children[0]).Fit();view.UpdateLayout();Capture(view,Path.Combine(root,$"OFFSCREEN-pro-{width}.png"));vm.R53Professional=false;view.ApplyR53Layout();view.UpdateLayout();Capture(view,Path.Combine(root,$"OFFSCREEN-simple-{width}.png"));vm.R53Professional=true;}}
        view.DataContext=null;await Task.CompletedTask;
    });
    sealed class NoDialogs:IDialogService
    {
        public IReadOnlyList<string> ChooseFiles(string t,string f,bool m)=>[];public string? ChooseFolder(string t,string? initialDirectory=null)=>null;public string? ChooseSaveFile(string t,string f,string e,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>ids;public void ShowInfo(string m){}public void ShowError(string m){}public bool Confirm(string m,string t)=>false;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool b)=>false;public void RevealFile(string p){}
    }
}
