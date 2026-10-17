using System.IO;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Media.Imaging;using System.Windows.Threading;
using RAWSelectionAssistant.Services;using RAWSelectionAssistant.ViewModels;using RAWSelectionAssistant.Views;using RAWSelectionAssistant.Core.Services.Projects;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class PhotoGradePageV1Tests
{
 [TestMethod]public Task RealPageOpenAdjustHistorySaveSyncAndExportKeepsLegacyPage()=>RunSta(async()=>
 {
  EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"PhotoGradeV1-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   string Image(string name,byte value){var path=Path.Combine(root,name);StudioQuickExport.Encode(BitmapSource.Create(24,16,96,96,PixelFormats.Rgb24,null,Enumerable.Repeat(value,24*16*3).ToArray(),24*3),path,path,StudioExportFormat.Png);return path;}
   var first=Image("a.png",80);var second=Image("b.png",120);using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(first);await vm.LoadTargetAsync(second);await vm.ActivateTargetCommand.ExecuteAsync(vm.Targets.First());
   var view=new PhotoGradePageView{DataContext=vm};view.Measure(new Size(1400,850));view.Arrange(new Rect(0,0,1400,850));view.UpdateLayout();vm.TogglePhotoGradePageCommand.Execute(null);Assert.IsTrue(vm.UsePhotoGradePage);Assert.IsFalse(vm.UseLegacyGradePage);
   var panel=(StudioToolPanel)view.FindName("CurrentParameters");var sliders=Descendants<Slider>(panel).ToArray();var exposure=sliders.Single(x=>Equals(x.Tag,"exposure"));exposure.SetCurrentValue(Slider.ValueProperty,.5);await Dispatcher.Yield(DispatcherPriority.DataBind);Assert.AreEqual(.5,vm.Editor.ToolValue(ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.BasicTone).Single(p=>p.Key=="exposure")));
   Assert.AreSame(vm.PhotoGradeUndoCommand,((Button)view.FindName("PhotoUndo")).Command);await vm.PhotoGradeUndoCommand.ExecuteAsync(null);Assert.IsNull(vm.Editor.ToolNode(ColorStudioNodeType.BasicTone));Assert.AreSame(vm.PhotoGradeRedoCommand,((Button)view.FindName("PhotoRedo")).Command);await vm.PhotoGradeRedoCommand.ExecuteAsync(null);Assert.IsNotNull(vm.Editor.ToolNode(ColorStudioNodeType.BasicTone));
   var node=vm.Editor.AdjustmentStack.Nodes.Single(x=>x.Type==ColorStudioNodeType.BasicTone);vm.Editor.SelectedAdjustmentNode=node;vm.Targets[1].IsSelected=true;vm.PhotoGradeSyncStepCommand.Execute(null);Assert.IsTrue(vm.Targets[1].ColorAdjustmentStackSnapshot!.Nodes.Any(x=>x.Id==node.Id));
   var session=Path.Combine(root,"page.ptstudio.json");await vm.SaveSessionAsync(session);using var restored=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await restored.LoadSessionAsync(session);Assert.AreEqual(2,restored.Targets.Count);await restored.ExportSelectedCommand.ExecuteAsync(null);Assert.IsTrue(restored.Targets.Any(t=>t.ExportStatus==ReferenceExportStatus.Succeeded));
   var evidence=Environment.GetEnvironmentVariable("KITAO_TEST_EVIDENCE_ROOT");if(!string.IsNullOrWhiteSpace(evidence))
   {Directory.CreateDirectory(evidence);var bitmap=new RenderTargetBitmap(1400,850,96,96,PixelFormats.Pbgra32);view.UpdateLayout();bitmap.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(Path.Combine(evidence,"photo-grade-populated.png"));encoder.Save(output);}
   vm.PhotoMaskColor="Yellow";Assert.AreEqual(Brushes.Yellow,((ListBox)view.FindName("OrderedSteps")).BorderBrush);vm.TogglePhotoGradePageCommand.Execute(null);Assert.IsTrue(vm.UseLegacyGradePage);
  }
  finally{Directory.Delete(root,true);}
 });
 private static IEnumerable<T> Descendants<T>(DependencyObject d)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(d);i++){var c=VisualTreeHelper.GetChild(d,i);if(c is T t)yield return t;foreach(var v in Descendants<T>(c))yield return v;}}
 private sealed class Dialogs(string root):IDialogService
 {public string? ChooseFolder(string title,string? initialDirectory=null)=>root;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string m){}public void ShowError(string m){}public bool Confirm(string m,string t)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool a)=>false;public void RevealFile(string p){}}
}
