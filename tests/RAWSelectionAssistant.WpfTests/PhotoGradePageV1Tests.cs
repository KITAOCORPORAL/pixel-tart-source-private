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
   var view=new PhotoGradePageView{DataContext=vm};view.Measure(new Size(1400,850));view.Arrange(new Rect(0,0,1400,850));view.UpdateLayout();vm.UsePhotoGradePage=false;vm.TogglePhotoGradePageCommand.Execute(null);Assert.IsTrue(vm.UsePhotoGradePage);Assert.IsFalse(vm.UseLegacyGradePage);
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
   var exposure=(Slider)view.FindName("LightPrintExposure");exposure.SetCurrentValue(Slider.ValueProperty,.6);await Dispatcher.Yield(DispatcherPriority.DataBind);Assert.AreEqual(.6,vm.InstantV3Exposure);
   await vm.LightPrintPreviewWork;await ((RAWSelectionAssistant.Utilities.AsyncRelayCommand)((Button)view.FindName("DevelopPrint")).Command).ExecuteAsync(null);Assert.AreEqual(.6,vm.ActiveTarget!.RawDefinition!.InstantFilm!.V3!.Response.ExposureEv,vm.StatusText);
   Assert.IsTrue(((StudioToolPanel)view.FindName("CurrentParameters")).IsEnabled,"Developed window adjustment remains available");Assert.IsFalse(vm.PhotoGradeSyncStepCommand.CanExecute(null));
   var pixels=vm.Editor.MatchedImage;await vm.PhotoGradeUndoCommand.ExecuteAsync(null);Assert.IsNull(vm.ActiveTarget.RawDefinition);await vm.PhotoGradeRedoCommand.ExecuteAsync(null);Assert.AreEqual(.6,exposure.Value);Assert.IsNotNull(pixels);
   view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));var list=(ListBox)view.FindName("OrderedSteps");var before=list.BorderBrush;vm.PhotoMaskColor="Yellow";Assert.AreSame(before,list.BorderBrush);
   view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));Assert.AreEqual(Brushes.Yellow,list.BorderBrush);
   var locked=JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition);vm.InstantAmount=.2;vm.InstantBorder=.12;vm.InstantV3Exposure=1.2;vm.ResetPhotoV3DraftCommand.Execute(null);Assert.AreEqual(.6,exposure.Value);Assert.AreEqual(locked,JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition),"Locked recipe must not silently change");
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
    var slider=(Slider)view.FindName("LightPrintExposure");slider.SetCurrentValue(Slider.ValueProperty,.7);await Dispatcher.Yield(DispatcherPriority.DataBind);
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
 [TestMethod]public Task SelectedStepSyncChangesTargetPixelsAndKeepsOtherParameters()=>RunSta(async()=>
 {
  EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"PhotoSyncPixels-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   string Input(string name){var p=Path.Combine(root,name);StudioQuickExport.Encode(BitmapSource.Create(80,53,96,96,PixelFormats.Rgb24,null,Enumerable.Range(0,80*53*3).Select(x=>(byte)(20+x%200)).ToArray(),80*3),p,p,StudioExportFormat.Png);return p;}
   string Pixels(BitmapSource b){var converted=new FormatConvertedBitmap(b,PixelFormats.Rgb24,null,0);var bytes=new byte[b.PixelWidth*b.PixelHeight*3];converted.CopyPixels(bytes,b.PixelWidth*3,0);return Convert.ToHexString(SHA256.HashData(bytes));}
   using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(Input("a.png"));await vm.LoadTargetAsync(Input("b.png"));var target=vm.ActiveTarget!;
   var view=new PhotoGradePageView{DataContext=vm};view.Measure(new Size(1400,850));view.Arrange(new Rect(0,0,1400,850));view.UpdateLayout();var panel=(StudioToolPanel)view.FindName("BasicColorParameters");
   var saturation=Descendants<Slider>(panel).Single(x=>Equals(x.Tag,"saturation"));saturation.SetCurrentValue(Slider.ValueProperty,.35);await Dispatcher.Yield(DispatcherPriority.DataBind);
   vm.Editor.CopyCurrentLookTo([target]);var original=target.ColorAdjustmentStackSnapshot!.DeepClone();
   var before=Pixels(await vm.Editor.ProcessForExportAsync(target.Path,target.AppliedLookSnapshot,target.FilmSettingsSnapshot,CancellationToken.None,target.ColorAdjustmentStackSnapshot));
   await vm.ActivateTargetCommand.ExecuteAsync(vm.Targets[0]);vm.Editor.AddAdjustmentNodeCommand.Execute("Curve");var selected=vm.Editor.SelectedAdjustmentNode!;Assert.AreEqual(ColorStudioNodeType.Curve,selected.Type);
   // Curve node must have a non-identity shape, not merely exist.
   vm.Editor.SetCurvePoints("rgb",[new(0,0),new(.5,.8),new(1,1)]);await Dispatcher.Yield(DispatcherPriority.DataBind);
   target.IsSelected=true;Assert.IsTrue(vm.PhotoGradeSyncStepCommand.CanExecute(null));vm.PhotoGradeSyncStepCommand.Execute(null);
   var after=Pixels(await vm.Editor.ProcessForExportAsync(target.Path,target.AppliedLookSnapshot,target.FilmSettingsSnapshot,CancellationToken.None,target.ColorAdjustmentStackSnapshot));Assert.AreNotEqual(before,after);
   var existing=original.Nodes.Single(x=>x.Type==ColorStudioNodeType.BasicTone);Assert.AreEqual(JsonSerializer.Serialize(existing),JsonSerializer.Serialize(target.ColorAdjustmentStackSnapshot!.Nodes.Single(x=>x.Id==existing.Id)));
   view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
  }
  finally{Directory.Delete(root,true);}
 });
 [TestMethod]public Task RepeatedPageLifecycleAndDraftDragsDoNotStartObsoleteRenders()=>RunSta(async()=>
 {
  EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"PageLifecycle-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   var path=Path.Combine(root,"photo.jpg");StudioQuickExport.Encode(BitmapSource.Create(96,71,96,96,PixelFormats.Rgb24,null,Enumerable.Range(0,96*71*3).Select(x=>(byte)(x%251)).ToArray(),96*3),path,path,StudioExportFormat.Jpeg);
   var held=StudioGlobalResources.AccountedBytes;var process=Process.GetCurrentProcess();process.Refresh();long privateBefore=process.PrivateMemorySize64;var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));
   var clock=Stopwatch.StartNew();await vm.LoadTargetAsync(path);var firstLoadMs=clock.Elapsed.TotalMilliseconds;clock.Restart();await vm.ActivateTargetCommand.ExecuteAsync(vm.ActiveTarget);var cachedActivationMs=clock.Elapsed.TotalMilliseconds;
   vm.InstantCameraAnimation=false;vm.InstantEngineVersion="V3";await vm.GenerateInstantFilmCommand.ExecuteAsync(null);var recipe=JsonSerializer.Serialize(vm.ActiveTarget!.RawDefinition);var pixels=vm.Editor.MatchedImage;int renders=0;
   var view=new PhotoGradePageView{DataContext=vm};view.Measure(new Size(1400,850));view.Arrange(new Rect(0,0,1400,850));view.UpdateLayout();var panel=(InstantV3Editor)view.FindName("V3Editor");var slider=(Slider)view.FindName("LightPrintExposure");
   double? firstPixelMs=null,firstAnimationMs=null;using(var capture=Kitao.InstantFilm.Research.CandidateProcessingEvidence.Capture(p=>{if(p=="PixelRender"){renders++;firstPixelMs??=clock.Elapsed.TotalMilliseconds;}if(p=="DevelopmentAnimation")firstAnimationMs??=clock.Elapsed.TotalMilliseconds;}))
   {
    clock.Restart();for(int i=0;i<200;i++)slider.SetCurrentValue(Slider.ValueProperty,(i%40)/20d-1);await Dispatcher.Yield(DispatcherPriority.DataBind);var dragMs=clock.Elapsed.TotalMilliseconds;
    Assert.AreEqual(0,renders,"Draft dragging must not create full-resolution work per tick");Assert.AreEqual(recipe,JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition));Assert.AreSame(pixels,vm.Editor.MatchedImage);
    for(int i=0;i<12;i++){view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));vm.PhotoMaskColor=i%2==0?"Yellow":"Cyan";view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));}
    var temporary=MakeUnloadedView(vm);await Dispatcher.Yield(DispatcherPriority.ContextIdle);for(int i=0;i<3&&temporary.IsAlive;i++){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Dispatcher.Yield(DispatcherPriority.ContextIdle);}Assert.IsFalse(temporary.IsAlive,"Unloaded view must not be retained by model/editor events");
    clock.Restart();await vm.GenerateInstantFilmCommand.ExecuteAsync(null);var applyMs=clock.Elapsed.TotalMilliseconds;var screenshot=Environment.GetEnvironmentVariable("KITAO_TEST_EVIDENCE_ROOT");if(!string.IsNullOrWhiteSpace(screenshot)){Directory.CreateDirectory(screenshot);view.UpdateLayout();var rendered=new RenderTargetBitmap(1400,850,96,96,PixelFormats.Pbgra32);rendered.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(rendered));using var stream=File.Create(Path.Combine(screenshot,"v3-shared-page.png"));png.Save(stream);}var beforeClose=StudioGlobalResources.AccountedBytes;view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));view.DataContext=null;vm.Dispose();await Dispatcher.Yield(DispatcherPriority.ContextIdle);process.Refresh();var afterClose=StudioGlobalResources.AccountedBytes;Assert.IsTrue(afterClose<=beforeClose);
    var output=Environment.GetEnvironmentVariable("KITAO_TEST_EVIDENCE_ROOT");if(!string.IsNullOrWhiteSpace(output)){Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"LIFECYCLE_PERFORMANCE.json"),JsonSerializer.Serialize(new{firstLoadMs,cachedActivationMs,draft200ChangesMs=dragMs,applyMs,firstPixelRenderMs=firstPixelMs,pixelsReadyBeforeAnimationMs=firstAnimationMs,presentationAndCommitMs=applyMs-firstAnimationMs,pixelRenderEntries=renders,held,beforeClose,afterClose,privateBefore,privateAfter=process.PrivateMemorySize64,peakWorkingSet=process.PeakWorkingSet64,scope="Actual synthetic page/weak-reference/lease observations; process private includes managed/native; not physical close native memory proof"},new JsonSerializerOptions{WriteIndented=true}));}
   }
  }
  finally{Directory.Delete(root,true);}
 });
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 private static WeakReference MakeUnloadedView(ReferenceColorWorkspaceViewModel vm)
 {var view=new PhotoGradePageView{DataContext=vm};view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));return new WeakReference(view);}
 private static IEnumerable<T> Descendants<T>(DependencyObject d)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(d);i++){var c=VisualTreeHelper.GetChild(d,i);if(c is T t)yield return t;foreach(var v in Descendants<T>(c))yield return v;}}
 private sealed class Dialogs(string root,string? second=null):IDialogService
 {public string? ChooseFolder(string title,string? initialDirectory=null)=>root;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>second is null?[]:[second];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string m){}public void ShowError(string m){}public bool Confirm(string m,string t)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool a)=>false;public void RevealFile(string p){}}
}
