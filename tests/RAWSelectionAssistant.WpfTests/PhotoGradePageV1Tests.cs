using System.IO;using System.Security.Cryptography;using System.Text.Json;using System.Diagnostics;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Media.Imaging;using System.Windows.Threading;
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
 [TestMethod]public Task FullV3PanelUsesSharedDraftAndUnloadedViewDoesNotReceiveModelEvents()=>RunSta(async()=>
 {
  EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"PageV3Shared-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   var input=Path.Combine(root,"a.jpg");StudioQuickExport.Encode(BitmapSource.Create(43,31,96,96,PixelFormats.Rgb24,null,Enumerable.Range(0,43*31*3).Select(x=>(byte)(x%240)).ToArray(),43*3),input,input,StudioExportFormat.Jpeg);
   using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(input);vm.InstantCameraAnimation=false;vm.InstantEngineVersion="V3";
   var view=new PhotoGradePageView{DataContext=vm};view.Measure(new Size(1400,850));view.Arrange(new Rect(0,0,1400,850));view.UpdateLayout();var panel=(InstantV3Editor)view.FindName("V3Editor");
   var exposure=(Slider)panel.FindName("InstantV3ExposureControl");exposure.SetCurrentValue(Slider.ValueProperty,.6);await Dispatcher.Yield(DispatcherPriority.DataBind);Assert.AreEqual(.6,vm.InstantV3Exposure);
   await ((RAWSelectionAssistant.Utilities.AsyncRelayCommand)((Button)panel.FindName("ApplyV3")).Command).ExecuteAsync(null);Assert.AreEqual(.6,vm.ActiveTarget!.RawDefinition!.InstantFilm!.V3!.Response.ExposureEv,vm.StatusText);
   Assert.IsFalse(((StudioToolPanel)view.FindName("CurrentParameters")).IsEnabled);Assert.IsFalse(vm.PhotoGradeSyncStepCommand.CanExecute(null));
   var pixels=vm.Editor.MatchedImage;await vm.PhotoGradeUndoCommand.ExecuteAsync(null);Assert.IsNull(vm.ActiveTarget.RawDefinition);await vm.PhotoGradeRedoCommand.ExecuteAsync(null);Assert.AreEqual(.6,exposure.Value);Assert.IsNotNull(pixels);
   view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));var list=(ListBox)view.FindName("OrderedSteps");var before=list.BorderBrush;vm.PhotoMaskColor="Yellow";Assert.AreSame(before,list.BorderBrush);
   view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));Assert.AreEqual(Brushes.Yellow,list.BorderBrush);
   vm.ResetPhotoV3DraftCommand.Execute(null);Assert.AreEqual(0,exposure.Value);Assert.AreEqual(.6,vm.ActiveTarget.RawDefinition!.InstantFilm!.V3!.Response.ExposureEv,"Draft reset must not silently commit");
  }
  finally{Directory.Delete(root,true);}
 });
 [TestMethod]public Task AuthorizedPhotoV3PagePixelHistoryReopenAndThreeFormatParity()=>RunSta(async()=>
 {
  var manifest=Environment.GetEnvironmentVariable("KITAO_PHOTO_PAGE_AUTHORIZED_MANIFEST");
  if(string.IsNullOrWhiteSpace(manifest)){Assert.Inconclusive("Explicit authorized frozen-photo manifest is required; no directory scan.");return;}
  EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"AuthorizedV3Page-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   var inputs=JsonDocument.Parse(await File.ReadAllTextAsync(manifest)).RootElement;var rows=new List<object>();
   foreach(var entry in inputs.EnumerateArray())
   {
    var id=entry.GetProperty("Id").GetString()!;var frozen=entry.GetProperty("FrozenTiff").GetString()!;
    using(var input=File.OpenRead(frozen))Assert.AreEqual(entry.GetProperty("ExpectedFrozenSha256").GetString(),Convert.ToHexString(SHA256.HashData(input)));
    // Explicit representative derivative, not a fresh RAW decode or full-resolution claim.
    var first=Path.Combine(root,id+".png");var second=Path.Combine(root,id+"-second.png");
    var bitmap=StudioQuickExport.Load(frozen,768);StudioQuickExport.Encode(bitmap,first,first,StudioExportFormat.Png);StudioQuickExport.Encode(bitmap,second,second,StudioExportFormat.Png);
    using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root,second));var clock=Stopwatch.StartNew();await vm.LoadTargetAsync(first);var loadMs=clock.Elapsed.TotalMilliseconds;
    vm.UsePhotoGradePage=true;vm.InstantCameraAnimation=false;vm.InstantEngineVersion="V3";
    var view=new PhotoGradePageView{DataContext=vm};view.Measure(new Size(1400,850));view.Arrange(new Rect(0,0,1400,850));view.UpdateLayout();var panel=(InstantV3Editor)view.FindName("V3Editor");
    string Pixels(BitmapSource source){var rgb=new FormatConvertedBitmap(source,PixelFormats.Rgb48,null,0);var bytes=new byte[rgb.PixelWidth*rgb.PixelHeight*6];rgb.CopyPixels(bytes,rgb.PixelWidth*6,0);return Convert.ToHexString(SHA256.HashData(bytes));}
    await vm.GenerateInstantFilmCommand.ExecuteAsync(null);var baseline=Pixels(vm.Editor.MatchedImage!);var prior=JsonSerializer.Serialize(vm.ActiveTarget!.RawDefinition);
    var slider=(Slider)panel.FindName("InstantV3ExposureControl");slider.SetCurrentValue(Slider.ValueProperty,.7);await Dispatcher.Yield(DispatcherPriority.DataBind);
    vm.InstantV3Softness=.45;vm.InstantV3Texture=.02;vm.InstantV3DefectKind="Bands";vm.InstantV3DefectAmount=.15;vm.InstantV3DefectX=.25;vm.InstantV3Circle=true;vm.InstantV3WindowPressed=.4;
    await vm.SelectSecondExposureCommand.ExecuteAsync(null);vm.InstantSecondWeight=.3;vm.InstantSecondZoom=1.2;vm.InstantSecondCropX=.3;clock.Restart();
    await vm.GenerateInstantFilmCommand.ExecuteAsync(null);var renderMs=clock.Elapsed.TotalMilliseconds;var edited=Pixels(vm.Editor.MatchedImage!);Assert.AreNotEqual(baseline,edited,vm.StatusText);var definition=JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition);
    await vm.PhotoGradeUndoCommand.ExecuteAsync(null);Assert.AreEqual(prior,JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition));Assert.AreEqual(baseline,Pixels(vm.Editor.MatchedImage!));Assert.AreEqual(0,slider.Value);
    await vm.PhotoGradeRedoCommand.ExecuteAsync(null);Assert.AreEqual(definition,JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition));Assert.AreEqual(edited,Pixels(vm.Editor.MatchedImage!));Assert.AreEqual(.7,slider.Value);
    vm.UsePhotoGradePage=false;vm.UsePhotoGradePage=true;Assert.AreEqual(.7,slider.Value);Assert.AreEqual(edited,Pixels(vm.Editor.MatchedImage!));
    var target=vm.ActiveTarget;await vm.LoadTargetAsync(second);Assert.AreEqual(0,slider.Value);await vm.ActivateTargetCommand.ExecuteAsync(target);Assert.AreEqual(.7,slider.Value);Assert.AreEqual(edited,Pixels(vm.Editor.MatchedImage!));
    var session=Path.Combine(root,id+".ptstudio.json");await vm.SaveSessionAsync(session);
    using var restored=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await restored.LoadSessionAsync(session);Assert.AreEqual(definition,JsonSerializer.Serialize(restored.ActiveTarget!.RawDefinition));Assert.AreEqual(edited,Pixels(restored.Editor.MatchedImage!));Assert.AreEqual(vm.InstantSecondInput,restored.InstantSecondInput);
    var master=restored.ActiveTarget.LinearRawMaster!;var results=new List<object>();
    foreach(var format in new[]{StudioExportFormat.Tiff,StudioExportFormat.Png,StudioExportFormat.Jpeg})
    {
     var output=Path.Combine(root,id+"-"+format+StudioQuickExport.Extension(first,format));clock.Restart();var receipt=await ProfessionalRawExport.WriteAsync(master,restored.ActiveTarget.RawDefinition!,output,format);var exportMs=clock.Elapsed.TotalMilliseconds;
     var repeat=Path.Combine(root,id+"-repeat-"+format+StudioQuickExport.Extension(first,format));await ProfessionalRawExport.WriteAsync(master,restored.ActiveTarget.RawDefinition!,repeat,format);
     Assert.AreEqual(Pixels(StudioQuickExport.Load(output)),Pixels(StudioQuickExport.Load(repeat)));Assert.AreEqual(1,receipt.Orientation);Assert.IsTrue(receipt.Srgb);
     if(format!=StudioExportFormat.Jpeg)Assert.AreEqual(0,receipt.MaximumError);else{Assert.IsTrue(receipt.MeanAbsoluteError<=5);Assert.IsTrue(receipt.RootMeanSquareError<=15);}
     results.Add(new{format,receipt,exportMs});
    }
    rows.Add(new{id,sourceFrozenHash=entry.GetProperty("ExpectedFrozenSha256").GetString(),derivativeWidth=bitmap.PixelWidth,derivativeHeight=bitmap.PixelHeight,loadMs,renderMs,baseline,edited,results,scope="Authorized resized photograph through actual shared page bindings; not native RAW or full-resolution acceptance"});
    var evidence=Environment.GetEnvironmentVariable("KITAO_TEST_EVIDENCE_ROOT");if(!string.IsNullOrWhiteSpace(evidence)){Directory.CreateDirectory(evidence);await File.WriteAllTextAsync(Path.Combine(evidence,"AUTHORIZED_PAGE.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));}
    view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
   }
  }
  finally{Directory.Delete(root,true);}
 });
 private static IEnumerable<T> Descendants<T>(DependencyObject d)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(d);i++){var c=VisualTreeHelper.GetChild(d,i);if(c is T t)yield return t;foreach(var v in Descendants<T>(c))yield return v;}}
 private sealed class Dialogs(string root,string? second=null):IDialogService
 {public string? ChooseFolder(string title,string? initialDirectory=null)=>root;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>second is null?[]:[second];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string m){}public void ShowError(string m){}public bool Confirm(string m,string t)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool a)=>false;public void RevealFile(string p){}}
}
