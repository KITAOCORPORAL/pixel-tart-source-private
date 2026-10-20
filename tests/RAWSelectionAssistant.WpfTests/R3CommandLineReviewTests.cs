using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using RAWSelectionAssistant.Core.Services.Projects;
using Kitao.InstantFilm.Research;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;

// Command-line only: construct/measure controls, never Show a Window or inject desktop input.
[TestClass][DoNotParallelize]
public sealed class R3CommandLineReviewTests
{
 [TestMethod] public Task DisplayedFrameKeepsFullResolutionAtActualPixels()=>RunSta(()=>
 {
  var view=new ColorStudioImageViewport{Original=Bitmap(600,400),Matched=Bitmap(800,1000),SourcePixelSize=new Size(4000,5000),Mode="仿色结果",PreferDisplayedDimensions=true};
  view.Measure(new Size(800,600));view.Arrange(new Rect(0,0,800,600));view.Configure();
  Assert.AreEqual(new Size(4000,5000),view.State.ImageSize,"100% must remain output pixels, not the 1600px proxy");
  view.State.SetZoom(1);Assert.AreEqual(5000d,view.State.ImageRect(new Rect(0,0,800,600)).Height);
  return Task.CompletedTask;
 });
 [TestMethod] public Task DisplayedDimensionsSetterReconfiguresAlreadyLoadedViewport()=>RunSta(()=>
 {
  var view=new ColorStudioImageViewport{Original=Bitmap(600,400),Matched=Bitmap(600,750),SourcePixelSize=new Size(6000,4000),Mode="仿色结果"};
  view.Measure(new Size(800,600));view.Arrange(new Rect(0,0,800,600));view.Configure();view.PreferDisplayedDimensions=true;
  Assert.AreEqual(.8,view.State.ImageSize.Width/view.State.ImageSize.Height,1e-9,"framed output aspect, without stretching into source ratio");
  Assert.AreEqual(6000d,Math.Max(view.State.ImageSize.Width,view.State.ImageSize.Height));return Task.CompletedTask;
 });
 [TestMethod] public void AllDirectPageBindingPathsExistOnActualModels()
 {
  var doc=XDocument.Load(Path.Combine(TestResources.Root,"src/RAWSelectionAssistant/Views/PhotoGradePageView.xaml"));
  foreach(var value in doc.Descendants().Attributes().Select(a=>a.Value).Where(v=>v.StartsWith("{Binding ")))
  {
   var path=value[9..].Split(',','}')[0].Trim();Type? type=typeof(ReferenceColorWorkspaceViewModel);
   if(path.StartsWith("Editor.")){type=typeof(TetherReferenceModeViewModel);path=path[7..];}
   else if(path.StartsWith("ActiveTarget.")){type=typeof(ReferenceTargetItem);path=path[13..];}
   else if(path.StartsWith("EditVersions.")){type=typeof(ReferenceTargetItem);}
   else if(path is "UpdatedAtUtc" or "CreatedAt" ||path.StartsWith("RawDefinition.")){type=typeof(StudioEditVersion);}
   else if(!path.Contains('.')&&type.GetProperty(path) is null)continue; // item-template fields checked by their typed branches
   if(path.StartsWith("DataContext."))continue;
   if(path.StartsWith("Source=")||path.StartsWith("Tag")||path=="IsExpanded")continue;
   foreach(var part in path.Split('.')){var property=type.GetProperty(part);Assert.IsNotNull(property,$"Unresolvable binding {value} at {type.Name}.{part}");type=property.PropertyType;}
  }
 }
 [TestMethod] public Task NewControlsResolveProductionCommandsWithoutOpeningWindows()=>RunSta(async()=>
 {
  EnsureTestApplication();var folder=Folder();try{
   using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(folder));await vm.LoadTargetAsync(Input(folder));
   var page=new PhotoGradePageView{DataContext=vm};Layout(page);await Dispatcher.Yield(DispatcherPriority.DataBind);
   var tabs=(TabControl)page.FindName("EditModes");
   for(int i=0;i<5;i++){tabs.SelectedIndex=i;Layout(page);await Dispatcher.Yield(DispatcherPriority.DataBind);
    foreach(var button in Children<Button>(page)){var binding=BindingOperations.GetBindingExpression(button,Button.CommandProperty);if(binding is not null){binding.UpdateTarget();Assert.IsNotNull(button.Command,binding.ParentBinding.Path.Path);Assert.AreNotEqual(BindingStatus.PathError,binding.Status);}}
   }
   Assert.IsTrue(Children<Button>(page).Any(b=>ReferenceEquals(b.Command,vm.CreateEditVersionCommand)),"Clone must bind to the real command, not an unguarded click stub");
   page.DataContext=null;
  }finally{Directory.Delete(folder,true);}
 });
 [TestMethod] public Task ExportStopUsesProductionCommandAndFormatBinding()=>RunSta(async()=>
 {
  EnsureTestApplication();var folder=Folder();try{
   using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(folder));await vm.LoadTargetAsync(Input(folder));
   var dialog=new DarkroomExportDialog(vm);var root=(FrameworkElement)dialog.Content;Layout(root);await Dispatcher.Yield(DispatcherPriority.DataBind);
   var stop=Children<Button>(root).Single(b=>Equals(b.Content,"停止"));Assert.AreSame(vm.StopExportCommand,stop.Command);Assert.IsFalse(stop.Command.CanExecute(null));
   var selector=Children<StudioExportFormatSelector>(root).Single();selector.SelectedValue=StudioExportFormat.Tiff;await Dispatcher.Yield(DispatcherPriority.DataBind);Assert.AreEqual(StudioExportFormat.Tiff,vm.QuickExportFormat);
   Assert.IsTrue(Children<TextBlock>(root).Any(t=>BindingOperations.GetBinding(t,TextBlock.TextProperty)?.Path.Path=="ExportStatus"),"Queue progress belongs in export dialog");
   dialog.DataContext=null;
  }finally{Directory.Delete(folder,true);}
 });
 [TestMethod] public Task ClonePreservesFrozenDefinitionAndPortableSession()=>RunSta(async()=>
 {
  EnsureTestApplication();var folder=Folder();try{
   using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(folder),versionRepository:new StudioVersionRepository(Path.Combine(folder,"catalog")));await vm.LoadTargetAsync(Input(folder));vm.EnableLightPrintWorkspace();vm.InstantCameraAnimation=false;
   vm.SelectedLightPrintFrameId="generic.white.circle";vm.SelectedPhotographerPaperId="itype-color";await vm.LightPrintPreviewWork;await vm.DevelopLightPrintCommand.ExecuteAsync(null);
   var original=vm.ActiveTarget!.ActiveEditVersionId;var definition=JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition);var image=Pixels(vm.Editor.MatchedImage!);
   vm.CreateEditVersionCommand.Execute(null);var clone=vm.ActiveTarget.ActiveEditVersionId;Assert.AreNotEqual(original,clone);Assert.AreEqual(definition,JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition));
   vm.EditVersionName="命令行克隆";vm.RenameEditVersionCommand.Execute(null);
   Assert.AreEqual("命令行克隆",vm.ActiveTarget.EditVersions.Single(v=>v.Id==clone).Name);
   await vm.SaveEditVersionsAsync();var catalog=await new StudioVersionRepository(Path.Combine(folder,"catalog")).LoadAsync(vm.ActiveTarget.VersionCatalog!.Identity);
   Assert.AreEqual(clone,catalog.SelectedId);Assert.AreEqual(definition,JsonSerializer.Serialize(catalog.Versions.Single(v=>v.Id==clone).RawDefinition));
   var session=Path.Combine(folder,"clone.ptstudio.json");await vm.SaveSessionAsync(session);
   using var reopened=new ReferenceColorWorkspaceViewModel(new Dialogs(folder));await reopened.LoadSessionAsync(session);reopened.EnableLightPrintWorkspace();
   Assert.AreEqual(clone,reopened.ActiveTarget!.ActiveEditVersionId);Assert.AreEqual(definition,JsonSerializer.Serialize(reopened.ActiveTarget.RawDefinition));Assert.AreEqual(image,Pixels(reopened.Editor.MatchedImage!));
   Assert.IsTrue(reopened.IsLightPrintLocked);await reopened.SwitchEditVersionAsync(original!.Value);Assert.AreEqual(image,Pixels(reopened.Editor.MatchedImage!));
  }finally{Directory.Delete(folder,true);}
 });
 [DataTestMethod][DataRow(StudioExportFormat.Png)][DataRow(StudioExportFormat.Tiff)][DataRow(StudioExportFormat.Jpeg)]
 public Task StopExportCancelsBeforeEncodingAndAllowsRetry(StudioExportFormat format)=>RunSta(async()=>
 {
  EnsureTestApplication();var folder=Folder();try{
   using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(folder));await vm.LoadTargetAsync(Input(folder));vm.EnableLightPrintWorkspace();vm.InstantCameraAnimation=false;
   vm.SelectedPhotographerPaperId="itype-color";await vm.LightPrintPreviewWork;await vm.DevelopLightPrintCommand.ExecuteAsync(null);vm.QuickExportFormat=format;
   var hash=Pixels(vm.Editor.MatchedImage!);var definition=JsonSerializer.Serialize(vm.ActiveTarget!.RawDefinition);var stopped=false;
   using(var capture=CandidateProcessingEvidence.Capture(stage=>{if(stage=="PixelRender"&&!stopped){stopped=true;Assert.IsTrue(vm.StopExportCommand.CanExecute(null));vm.StopExportCommand.Execute(null);}}))await vm.ExportSelectedCommand.ExecuteAsync(null);
   Assert.IsTrue(stopped,"Cancellation checkpoint was reached");Assert.IsFalse(vm.IsExporting);Assert.IsFalse(vm.StopExportCommand.CanExecute(null));
   Assert.AreEqual(ReferenceExportStatus.Cancelled,vm.ActiveTarget.ExportStatus);Assert.AreEqual(0,Directory.GetFiles(folder,".kitao-stage-*",SearchOption.AllDirectories).Length);
   Assert.AreEqual(definition,JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition));Assert.AreEqual(hash,Pixels(vm.Editor.MatchedImage!));
   await vm.ExportSelectedCommand.ExecuteAsync(null);Assert.AreEqual(ReferenceExportStatus.Succeeded,vm.ActiveTarget.ExportStatus,vm.ActiveTarget.Error);Assert.IsTrue(File.Exists(vm.ActiveTarget.OutputPath));
  }finally{Directory.Delete(folder,true);}
 });

 [TestMethod] public Task TwoExplicitAuthorizedPhotosExerciseCurrentProductPipeline()=>RunSta(async()=>
 {
  EnsureTestApplication();var manifest=Environment.GetEnvironmentVariable("KITAO_R3_CLI_AUTHORIZED_MANIFEST")??throw new InvalidOperationException("Explicit authorized manifest required; never scan.");
  var outputRoot=Environment.GetEnvironmentVariable("KITAO_R3_CLI_OUTPUT")??throw new InvalidOperationException("Fresh evidence output required");
  // The configured path is an evidence parent, never a reusable project directory.
  // Preserve prior runs and the product's refusal to overwrite a project this VM never read.
  var output=Path.Combine(Path.GetFullPath(outputRoot),"run-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+"-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(output);var rows=new List<object>();
  foreach(var input in JsonDocument.Parse(await File.ReadAllTextAsync(manifest)).RootElement.EnumerateArray())
  {
   var path=input.GetProperty("path").GetString()!;var expected=input.GetProperty("sha256").GetString()!;
   using(var stream=File.OpenRead(path))Assert.AreEqual(expected,Convert.ToHexString(SHA256.HashData(stream)),true);
   var folder=Path.Combine(output,Path.GetFileNameWithoutExtension(path));Directory.CreateDirectory(folder);
   var derivative=Path.Combine(folder,"bounded-input.png");var source=StudioQuickExport.Load(path,768);StudioQuickExport.Encode(source,derivative,derivative,StudioExportFormat.Png);
   using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(folder));await vm.LoadTargetAsync(derivative);vm.EnableLightPrintWorkspace();vm.InstantCameraAnimation=false;
   vm.SelectedPhotographerPaperId="itype-color";vm.RequestLightPrintPreview();await vm.LightPrintPreviewWork;Assert.IsNotNull(vm.Editor.MatchedImage,vm.LightPrintStatus);var first=Pixels(vm.Editor.MatchedImage!);
   vm.SelectedPhotographerPaperId="blue-duochrome600";await vm.LightPrintPreviewWork;Assert.AreNotEqual(first,Pixels(vm.Editor.MatchedImage!));
   var square=Pixels(vm.Editor.MatchedImage!);vm.SelectedLightPrintFrameId="generic.white.circle";await vm.LightPrintPreviewWork;Assert.AreNotEqual(square,Pixels(vm.Editor.MatchedImage!));
   vm.SelectedPhotographerPaperId="itype-bw";vm.SelectedPhotographerPaperId="sx70-color";vm.SelectedPhotographerPaperId="itype-color";await vm.LightPrintPreviewWork;
   Assert.AreEqual("itype-color",vm.SelectedInstantProfile.Id);var preview=Pixels(vm.Editor.MatchedImage!);await vm.DevelopLightPrintCommand.ExecuteAsync(null);Assert.AreEqual(preview,Pixels(vm.Editor.MatchedImage!));Assert.IsTrue(vm.IsLightPrintLocked);
   var old=vm.ActiveTarget!.ActiveEditVersionId;var frozen=JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition);
   vm.ReshootLightPrintCommand.Execute(null);vm.SelectedPhotographerPaperId="itype-bw";await vm.LightPrintPreviewWork;await vm.DevelopLightPrintCommand.ExecuteAsync(null);var next=vm.ActiveTarget.ActiveEditVersionId;Assert.AreNotEqual(old,next);
   await vm.SwitchEditVersionAsync(old!.Value);Assert.AreEqual(frozen,JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition));Assert.AreEqual(preview,Pixels(vm.Editor.MatchedImage!));
   var session=Path.Combine(folder,"review.ptstudio.json");await vm.SaveSessionAsync(session);using var reopened=new ReferenceColorWorkspaceViewModel(new Dialogs(folder));await reopened.LoadSessionAsync(session);Assert.AreEqual(frozen,JsonSerializer.Serialize(reopened.ActiveTarget!.RawDefinition));Assert.AreEqual(preview,Pixels(reopened.Editor.MatchedImage!));
   var exports=new List<object>();foreach(var format in new[]{StudioExportFormat.Png,StudioExportFormat.Tiff,StudioExportFormat.Jpeg}){reopened.QuickExportFormat=format;await reopened.ExportSelectedCommand.ExecuteAsync(null);Assert.AreEqual(ReferenceExportStatus.Succeeded,reopened.ActiveTarget.ExportStatus,reopened.ActiveTarget.Error);var exported=reopened.ActiveTarget.OutputPath!;using var stream=File.OpenRead(exported);exports.Add(new{format=format.ToString(),path=exported,sha256=Convert.ToHexString(SHA256.HashData(stream)),inspection=reopened.ActiveTarget.LastProfessionalOutput});}
   rows.Add(new{input=path,inputSha256=expected,derivative,scope="768px bounded authorized derivative, not native RAW or desktop input",paperChanges=true,frameChanges=true,latestWins=true,developExact=true,variants=true,reopenExact=true,exports});
  }
  await File.WriteAllTextAsync(Path.Combine(output,"TWO_PHOTO_RESULTS.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
 });

 [TestMethod] public Task UnreadExistingProjectIsRejectedWithoutChangingItsBytes()=>RunSta(async()=>
 {
  EnsureTestApplication();var folder=Folder();try
  {
   using var writer=new ReferenceColorWorkspaceViewModel(new Dialogs(folder));await writer.LoadTargetAsync(Input(folder));
   var project=Path.Combine(folder,"protected.ptstudio.json");await writer.SaveSessionAsync(project);var before=await File.ReadAllBytesAsync(project);
   using var other=new ReferenceColorWorkspaceViewModel(new Dialogs(folder));await other.LoadTargetAsync(Path.Combine(folder,"test.png"));
   var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>other.SaveSessionAsync(project));StringAssert.Contains(error.Message,"未读取");
   CollectionAssert.AreEqual(before,await File.ReadAllBytesAsync(project));
   await other.LoadSessionAsync(project);await other.SaveSessionAsync(project);Assert.IsTrue(File.Exists(project));
  }finally{Directory.Delete(folder,true);}
 });
 [TestMethod] public Task BoundVariantSelectionAndSoloGroupsStayConsistent()=>RunSta(async()=>
 {
  EnsureTestApplication();var folder=Folder();try{
   using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(folder));await vm.LoadTargetAsync(Input(folder));
   var page=new PhotoGradePageView{DataContext=vm};Layout(page);var tabs=(TabControl)page.FindName("EditModes");tabs.SelectedIndex=4;Layout(page);
   vm.CreateEditVersionCommand.Execute(null);var first=vm.ActiveTarget!.ActiveEditVersionId;vm.CreateEditVersionCommand.Execute(null);var second=vm.ActiveTarget.ActiveEditVersionId;
   var list=(ListBox)page.FindName("HistoryVersions");list.SelectedItem=vm.ActiveTarget.EditVersions.Single(v=>v.Id==first);
   for(int i=0;i<200&&(vm.IsLoading||vm.ActiveTarget.ActiveEditVersionId!=first);i++)await Task.Delay(10);
   Assert.AreEqual(first,vm.ActiveTarget.ActiveEditVersionId);Assert.IsFalse(vm.IsLoading);
   list.SelectedItem=vm.ActiveTarget.EditVersions.Single(v=>v.Id==second);
   for(int i=0;i<200&&(vm.IsLoading||vm.ActiveTarget.ActiveEditVersionId!=second);i++)await Task.Delay(10);
   Assert.AreEqual(second,vm.ActiveTarget.ActiveEditVersionId);
   tabs.SelectedIndex=0;Layout(page);typeof(PhotoGradePageView).GetField("solo",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(page,true);
   var sections=((StackPanel)((ScrollViewer)page.FindName("AdjustmentScroll")).Content).Children.OfType<Expander>().ToArray();sections[1].IsExpanded=true;sections[2].IsExpanded=true;Assert.IsFalse(sections[1].IsExpanded);Assert.IsTrue(sections[2].IsExpanded);
   ((ComboBox)page.FindName("GuideChoice")).SelectedIndex=1;Layout(page);page.DataContext=null;
  }finally{Directory.Delete(folder,true);}
 });
 [TestMethod] public Task PanelLayoutRoundTripsWithoutChangingImageDefinitions()=>RunSta(async()=>
 {
  EnsureTestApplication();var folder=Folder();try{
   using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(folder));await vm.LoadTargetAsync(Input(folder));
   var before=JsonSerializer.Serialize(vm.ActiveTarget!.RawDefinition);
   var page=new PhotoGradePageView{DataContext=vm};Layout(page);
   var group=(StackPanel)((ScrollViewer)page.FindName("AdjustmentScroll")).Content;
   var first=group.Children.OfType<Expander>().First();
   Invoke(page,"MoveSection",first,1);Invoke(page,"SetSolo",true);group.Children.OfType<Expander>().Last().IsExpanded=true;
   var panelColumn=(ColumnDefinition)page.FindName("PanelColumn");panelColumn.Width=new GridLength(388);Layout(page);
   ((TabControl)page.FindName("EditModes")).SelectedIndex=4;
   Invoke(page,"SetPanelOpen",false);Invoke(page,"SavePanelLayout");
   var expected=vm.Layout.Darkroom!;Assert.IsFalse(expected.Open);Assert.IsTrue(expected.Solo);Assert.AreEqual(4,expected.Tab);Assert.AreEqual(388d,expected.Width);
   Assert.AreNotEqual((string)first.Tag,expected.SectionOrder![0]);
   var path=Path.Combine(folder,"panel.ptstudio.json");await vm.SaveSessionAsync(path);
   using var reopened=new ReferenceColorWorkspaceViewModel(new Dialogs(folder));await reopened.LoadSessionAsync(path);
   Assert.AreEqual(JsonSerializer.Serialize(expected),JsonSerializer.Serialize(reopened.Layout.Darkroom));
   Assert.AreEqual(before,JsonSerializer.Serialize(reopened.ActiveTarget!.RawDefinition));
   var restored=new PhotoGradePageView{DataContext=reopened};Layout(restored);
   Assert.AreEqual(4,((TabControl)restored.FindName("EditModes")).SelectedIndex);
   Assert.AreEqual(Visibility.Collapsed,((Grid)restored.FindName("RightPanel")).Visibility);
   Invoke(restored,"SetPanelOpen",true);Layout(restored);Assert.AreEqual(388d,((ColumnDefinition)restored.FindName("PanelColumn")).Width.Value);
   var restoredGroup=(StackPanel)((ScrollViewer)restored.FindName("AdjustmentScroll")).Content;
   Assert.AreEqual(expected.SectionOrder[0],(string)restoredGroup.Children.OfType<Expander>().First().Tag);
   Assert.AreEqual(1,restoredGroup.Children.OfType<Expander>().Count(s=>s.IsExpanded));
   page.DataContext=null;restored.DataContext=null;
  }finally{Directory.Delete(folder,true);}
 });
 [TestMethod] public Task ExpandAllExitsSoloAndMenuSurvivesVariantsTab()=>RunSta(()=>
 {
  EnsureTestApplication();using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(Path.GetTempPath()));var page=new PhotoGradePageView{DataContext=vm};Layout(page);
  Invoke(page,"SetSolo",true);Invoke(page,"SetSectionsExpanded",true);
  var sections=((StackPanel)((ScrollViewer)page.FindName("AdjustmentScroll")).Content).Children.OfType<Expander>().ToArray();
  Assert.IsTrue(sections.All(s=>s.IsExpanded));Assert.IsFalse(vm.Layout.Darkroom!.Solo);
  Invoke(page,"SetSectionsExpanded",false);Assert.IsTrue(sections.All(s=>!s.IsExpanded));
  ((TabControl)page.FindName("EditModes")).SelectedIndex=4;Layout(page);
  var options=(Button)page.FindName("PanelOptions");Assert.AreEqual(0,Grid.GetRow(options));Assert.AreSame(page.FindName("RightPanel"),options.Parent);
  Assert.AreEqual(Visibility.Visible,options.Visibility);
  page.DataContext=null;return Task.CompletedTask;
 });
 [TestMethod] public Task OldAndMalformedPanelLayoutsRemainCompatible()=>RunSta(()=>
 {
  EnsureTestApplication();var old=JsonSerializer.Deserialize<ColorStudioLayout>("{}");Assert.IsNull(old!.Darkroom);
  var malformed=(new ColorStudioLayout{Darkroom=new DarkroomPanelLayout(double.NaN,Tab:100,SectionOrder:["", "Exposure", "Exposure"],CollapsedSections:["old-unknown"])}).Normalize();
  Assert.AreEqual(304d,malformed.Darkroom!.Width);Assert.AreEqual(4,malformed.Darkroom.Tab);Assert.AreEqual(1,malformed.Darkroom.SectionOrder!.Length);
  using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(Path.GetTempPath()));var page=new PhotoGradePageView{DataContext=vm};Layout(page);
  vm.Layout=malformed;Layout(page);Assert.AreEqual(4,((TabControl)page.FindName("EditModes")).SelectedIndex);
  vm.Layout=new ColorStudioLayout();Layout(page);Assert.AreEqual(0,((TabControl)page.FindName("EditModes")).SelectedIndex);
  page.DataContext=null;return Task.CompletedTask;
 });
 static void Invoke(PhotoGradePageView page,string name,params object[] args)=>typeof(PhotoGradePageView).GetMethod(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(page,args);
 static string Folder(){var p=Path.Combine(Path.GetTempPath(),"R3Cli-"+Guid.NewGuid());Directory.CreateDirectory(p);return p;}
 static BitmapSource Bitmap(int w,int h){var b=BitmapSource.Create(w,h,96,96,PixelFormats.Rgb24,null,new byte[w*h*3],w*3);b.Freeze();return b;}
 static string Input(string folder){var p=Path.Combine(folder,"test.png");var bytes=Enumerable.Range(0,160*120*3).Select(x=>(byte)(20+x%210)).ToArray();var b=BitmapSource.Create(160,120,96,96,PixelFormats.Rgb24,null,bytes,480);StudioQuickExport.Encode(b,p,p,StudioExportFormat.Png);return p;}
 static string Pixels(BitmapSource b){var rgb=new FormatConvertedBitmap(b,PixelFormats.Rgb48,null,0);var data=new byte[rgb.PixelWidth*rgb.PixelHeight*6];rgb.CopyPixels(data,rgb.PixelWidth*6,0);return Convert.ToHexString(SHA256.HashData(data));}
 static void Layout(FrameworkElement v){v.Measure(new Size(1366,768));v.Arrange(new Rect(0,0,1366,768));v.UpdateLayout();}
 static IEnumerable<T> Children<T>(DependencyObject root)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T t)yield return t;foreach(var c in Children<T>(child))yield return c;}}
 sealed class Dialogs(string folder):IDialogService
 {public string? ChooseFolder(string t,string? initialDirectory=null)=>folder;public IReadOnlyList<string> ChooseFiles(string t,string f,bool multiselect=true)=>[];public string? ChooseSaveFile(string t,string f,string e,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> i)=>null;public void ShowInfo(string m){}public void ShowError(string m){}public bool Confirm(string m,string t)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool a)=>false;public void RevealFile(string p){}}
}
