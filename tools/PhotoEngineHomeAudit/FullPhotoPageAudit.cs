using System.Buffers.Binary;using System.Diagnostics;using System.IO;using System.Security.Cryptography;using System.Text.Json;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Media.Imaging;using System.Xml.Linq;
using Kitao.InstantFilm.Research;using RAWSelectionAssistant.Services;using RAWSelectionAssistant.ViewModels;using RAWSelectionAssistant.Views;using RAWSelectionAssistant.Core.Services.Color;using RAWSelectionAssistant.Core.Services.Export;using RAWSelectionAssistant.Core.Services.Projects;
internal static class FullPhotoPageAudit
{
 public static void Run(string manifest,string output)
 {
  if(Directory.Exists(output))throw new IOException("Fresh audit directory required");Directory.CreateDirectory(output);Exception? failure=null;
  var thread=new Thread(()=>
  {
   var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var root=Environment.GetEnvironmentVariable("KITAO_TEST_RESOURCE_ROOT")??Directory.GetCurrentDirectory();var doc=XDocument.Load(Path.Combine(root,"src/RAWSelectionAssistant/App.xaml"));foreach(var a in doc.Descendants().Attributes("Source"))app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/KitaoPhotoSelector;component/"+a.Value,UriKind.Relative)));
   app.Dispatcher.InvokeAsync(async()=>{try{await Execute(manifest,output);}catch(Exception e){failure=e;}finally{app.Shutdown();}});app.Run();
  });thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure is not null)throw new InvalidOperationException("Full photo page audit failed",failure);
 }
 private static string Hash(string p){using var f=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(f));}
 private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
 private static string BitmapHash(BitmapSource image){var c=new FormatConvertedBitmap(image,PixelFormats.Rgb48,null,0);var b=new byte[c.PixelWidth*c.PixelHeight*6];c.CopyPixels(b,c.PixelWidth*6,0);return Convert.ToHexString(SHA256.HashData(b));}
 private static string Pixels(HighBitDepthImageBuffer image)=>Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(image.Rgb32.Span)));
 private static async Task Execute(string manifest,string output)
 {
  using var lifecycle=StudioPreviewDiagnostics.Begin();lifecycle.Mark("ColdStart");
  var inputs=JsonSerializer.Deserialize<InstantReviewInput[]>(File.ReadAllText(manifest))!;var first=inputs.Single(x=>x.Id=="canon-tnan8886");var second=inputs.Single(x=>x.Id=="sony-dsc08015");
  foreach(var input in new[]{first,second}){Require(Hash(input.Original)==input.ExpectedOriginalSha256,"Original hash");Require(Hash(input.FrozenTiff)==input.ExpectedFrozenSha256,"Frozen hash");}
  var held=StudioGlobalResources.AccountedBytes;using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(output,second.FrozenTiff));await vm.LoadTargetAsync(first.FrozenTiff);lifecycle.Mark("SourceLoaded");vm.UsePhotoGradePage=true;vm.InstantEngineVersion="V3";vm.InstantInputMode="Original";vm.InstantCameraAnimation=false;vm.InstantDevelopmentDemo=false;
  var view=new PhotoGradePageView{DataContext=vm};view.Measure(new Size(1600,1000));view.Arrange(new Rect(0,0,1600,1000));view.UpdateLayout();var panel=(InstantV3Editor)view.FindName("V3Editor");
  Console.WriteLine("Selecting second");await vm.SelectSecondExposureCommand.ExecuteAsync(null);Require(vm.InstantSecondInput?.Version==3,"Original full second source must use bounded TIFF3");vm.InstantSecondWeight=.37;vm.InstantSecondCropX=.2;vm.InstantSecondCropY=.7;vm.InstantSecondZoom=1.21;
  ((Slider)panel.FindName("InstantV3ExposureControl")).SetCurrentValue(Slider.ValueProperty,.23);vm.InstantV3Softness=.35;vm.InstantV3Texture=.003;vm.InstantV3Circle=true;vm.InstantV3WindowPressed=.3;
  using var applyDiagnostics=StudioPreviewDiagnostics.Begin();applyDiagnostics.Mark("BeforeApply");Console.WriteLine("Initial apply");
  double? firstVisible=null;var watch=Stopwatch.StartNew();
  System.ComponentModel.PropertyChangedEventHandler visible=(_,e)=>{if(e.PropertyName==nameof(TetherReferenceModeViewModel.MatchedImage)&&vm.Editor.MatchedImage is not null)firstVisible??=watch.Elapsed.TotalMilliseconds;};vm.Editor.PropertyChanged+=visible;
  await ((RAWSelectionAssistant.Utilities.AsyncRelayCommand)((Button)panel.FindName("ApplyV3")).Command).ExecuteAsync(null);var applyMs=watch.Elapsed.TotalMilliseconds;vm.Editor.PropertyChanged-=visible;
  applyDiagnostics.Mark("AfterApply");File.WriteAllText(Path.Combine(output,"APPLY_WORK.json"),JsonSerializer.Serialize(applyDiagnostics.Observations));applyDiagnostics.Dispose();
  var target=vm.ActiveTarget!;var definition=target.RawDefinition??throw new InvalidDataException(vm.StatusText);Require(definition.InstantFilm?.V3?.MultipleInput?.Version==3,vm.StatusText);var active=target.ActiveEditVersionId;var frozen=JsonSerializer.Serialize(definition);var initialTimings=vm.LastInstantApplyTimings;lifecycle.Mark("FirstApplied");
  Require(vm.Editor.ComparisonUnavailable&&vm.Editor.SourceImage is null,"No stale comparison before demand");Require(!vm.IsInstantGenerating&&vm.GenerateInstantFilmCommand.CanExecute(null),"Application usable before comparison");
  var effectHash=BitmapHash(vm.Editor.MatchedImage!);var versions=target.EditVersions.Count;
  var compareWatch=Stopwatch.StartNew();await ((RAWSelectionAssistant.Utilities.AsyncRelayCommand)((Button)view.FindName("PrepareComparison")).Command).ExecuteAsync(null);var compareMs=compareWatch.Elapsed.TotalMilliseconds;
  Require(vm.IsComparisonReady&&vm.Editor.SourceImage is not null,"On-demand comparison ready");Require(target.EditVersions.Count==versions&&target.ActiveEditVersionId==active&&BitmapHash(vm.Editor.MatchedImage!)==effectHash,"Comparison never modifies effect/history");
  var comparisonHash=BitmapHash(vm.Editor.SourceImage!);
  using(var original=ProfessionalRawPipeline.RenderPreview(target.LinearRawMaster!,InstantV3ProductPipeline.Original(definition),1600))Require(BitmapHash(RawDisplayBitmapAdapter.ToBitmap(original.ToVisualRgb24()))==comparisonHash,"Original recipe comparison semantic oracle");
  File.WriteAllText(Path.Combine(output,"FIRST_VISIBLE.json"),JsonSerializer.Serialize(new{effectFirstVisibleMs=firstVisible,operationAvailableMs=applyMs,comparisonAfterDemandMs=compareMs,comparisonReadyFromApplyMs=applyMs+compareMs,comparisonHash,effectHash,sourceMasterCache="cold",comparisonCache="cold; demand immediately after apply",physicalForeground="NOT_RUN"}));
  using var preview=ProfessionalRawPipeline.RenderPreview(target.LinearRawMaster!,definition,512);var previewHash=Pixels(preview);var size=ProfessionalRawPipeline.OutputSize(target.LinearRawMaster!,definition);Require(size.Width>4000&&size.Height>4000,"Full dimensions must not shrink");
  using var baseline=ProfessionalRawPipeline.RenderRegion(target.LinearRawMaster!,definition,size.Width/2,size.Height/3,61,47);var regionHash=Pixels(baseline);
  var changes=new List<object>();foreach(var variant in new[]{definition.InstantFilm!.V3! with{MultipleInput=definition.InstantFilm.V3.MultipleInput! with{Weight=.8}},definition.InstantFilm.V3 with{MultipleInput=definition.InstantFilm.V3.MultipleInput! with{CropX=.8}},definition.InstantFilm.V3 with{MultipleInput=definition.InstantFilm.V3.MultipleInput! with{Zoom=1.8}}})
  {using var region=ProfessionalRawPipeline.RenderRegion(target.LinearRawMaster!,definition with{InstantFilm=definition.InstantFilm with{V3=variant}},size.Width/2,size.Height/3,61,47);var changed=Pixels(region);Require(changed!=regionHash,"Second weight/crop/zoom must affect actual same-coordinate region");changes.Add(new{variant.MultipleInput,changed});}
  if(Environment.GetEnvironmentVariable("KITAO_MEASURE_CONTINUOUS")=="1")
  {
   var continuous=new List<object>();
   foreach(var weight in new[]{.52,.37})
   {
    vm.InstantInputMode="Original";vm.InstantSecondWeight=weight;var beforeCount=target.EditVersions.Count;
    using var trace=StudioPreviewDiagnostics.Begin();trace.Mark("BeforeContinuous");var timer=Stopwatch.StartNew();await vm.GenerateInstantFilmCommand.ExecuteAsync(null);trace.Mark("AfterContinuous");
    Require(target.EditVersions.Count==beforeCount+1,"One history record per continuous apply");continuous.Add(new{weight,elapsedMs=timer.Elapsed.TotalMilliseconds,timings=vm.LastInstantApplyTimings,events=trace.Observations});
   }
   using(var same=ProfessionalRawPipeline.RenderPreview(target.LinearRawMaster!,target.RawDefinition!,512))Require(Pixels(same)==previewHash,"Continuous parameter restore pixels");
   active=target.ActiveEditVersionId;definition=target.RawDefinition!;frozen=JsonSerializer.Serialize(definition);
   File.WriteAllText(Path.Combine(output,"CONTINUOUS_WORK.json"),JsonSerializer.Serialize(continuous));lifecycle.Mark("ContinuousCompleted");
  }
  if(Environment.GetEnvironmentVariable("KITAO_INTERACTION_AUDIT")=="1")
  {
   Console.WriteLine("Interaction sequence");var interactions=new List<object>();
   async Task ApplyEdit(string label,Action edit)
   {edit();vm.InstantInputMode="Original";int count=target.EditVersions.Count;var timer=Stopwatch.StartNew();await vm.GenerateInstantFilmCommand.ExecuteAsync(null);Require(target.EditVersions.Count==count+1,"Exactly one interaction history");Require(vm.InstantV3Exposure==target.RawDefinition!.InstantFilm!.V3!.Response.ExposureEv,"Controls follow definition");interactions.Add(new{label,ms=timer.Elapsed.TotalMilliseconds,definition=target.RawDefinition,pixels=BitmapHash(vm.Editor.MatchedImage!),comparisonUnavailable=vm.Editor.ComparisonUnavailable});}
   await ApplyEdit("Exposure",()=>vm.InstantV3Exposure=.41);await ApplyEdit("Weight",()=>vm.InstantSecondWeight=.6);await ApplyEdit("Crop",()=>{vm.InstantSecondCropX=.72;vm.InstantSecondCropY=.22;});await ApplyEdit("Zoom",()=>vm.InstantSecondZoom=1.65);
   async Task DuringComparison(string label,Func<Task> operation)
   {
    var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var task=Task.CompletedTask;
    using(var observed=CandidateProcessingEvidence.Capture(p=>{if(p=="ComparisonStarted")started.TrySetResult();}))task=vm.RequestComparisonAsync();
    await started.Task.WaitAsync(TimeSpan.FromSeconds(10));await operation();await task;Require(!vm.IsComparisonPreparing,"Canceled comparison joined");interactions.Add(new{label,pixels=vm.Editor.MatchedImage is{} bitmap?BitmapHash(bitmap):null,definition=vm.ActiveTarget?.RawDefinition,comparisonUnavailable=vm.Editor.ComparisonUnavailable,activeMappings=BoundedTiffPixelSource.ActiveMappings});
   }
   await DuringComparison("CompareThenSwitch",()=>vm.LoadTargetAsync(second.FrozenTiff));await vm.ActivateTargetCommand.ExecuteAsync(target);
   await DuringComparison("CompareThenUndo",()=>vm.PhotoGradeUndoCommand.ExecuteAsync(null));
   await DuringComparison("CompareThenApply",()=>ApplyEdit("ApplyDuringComparison",()=>vm.InstantV3Exposure=.37));
   await ApplyEdit("RestoreBase",()=>{vm.InstantV3Exposure=.23;vm.InstantSecondWeight=.37;vm.InstantSecondCropX=.2;vm.InstantSecondCropY=.7;vm.InstantSecondZoom=1.21;});
   using(var exact=ProfessionalRawPipeline.RenderPreview(target.LinearRawMaster!,target.RawDefinition!,512))Require(Pixels(exact)==previewHash,"Interaction restore same final pixels");active=target.ActiveEditVersionId;definition=target.RawDefinition!;frozen=JsonSerializer.Serialize(definition);File.WriteAllText(Path.Combine(output,"INTERACTIONS.json"),JsonSerializer.Serialize(interactions));
  }
  Console.WriteLine("Undo/redo");await vm.PhotoGradeUndoCommand.ExecuteAsync(null);Require(target.ActiveEditVersionId!=active,"Undo");await vm.PhotoGradeRedoCommand.ExecuteAsync(null);Require(target.ActiveEditVersionId==active,"Redo");Require(JsonSerializer.Serialize(target.RawDefinition)==frozen,"Recipe redo");
  using(var redo=ProfessionalRawPipeline.RenderPreview(target.LinearRawMaster!,definition,512))Require(Pixels(redo)==previewHash,"Undo/redo preview pixels");
  Console.WriteLine("Switch photos");await vm.LoadTargetAsync(second.FrozenTiff);Require(vm.InstantV3Exposure==0,"No cross-photo draft");await vm.ActivateTargetCommand.ExecuteAsync(target);Require(vm.InstantV3Exposure==.23,"Restored photo draft");
  var session=Path.Combine(output,"full.private.ptstudio.json");await vm.SaveSessionAsync(session);view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));vm.Dispose();await vm.ComparisonWork;lifecycle.Mark("FirstWorkspaceClosed");Require(BoundedTiffPixelSource.ActiveMappings==0,"Mappings released on first close");
  Console.WriteLine("Reopen session");using var restored=new ReferenceColorWorkspaceViewModel(new Dialogs(output,second.FrozenTiff));using(var reopenDiagnostics=StudioPreviewDiagnostics.Begin()){reopenDiagnostics.Mark("BeforeReopen");var reopenWatch=Stopwatch.StartNew();await restored.LoadSessionAsync(session);reopenDiagnostics.Mark("AfterReopen");File.WriteAllText(Path.Combine(output,"REOPEN_WORK.json"),JsonSerializer.Serialize(new{elapsedMs=reopenWatch.Elapsed.TotalMilliseconds,events=reopenDiagnostics.Observations}));}var selected=restored.ActiveTarget!;Require(JsonSerializer.Serialize(selected.RawDefinition)==frozen,"Reopen frozen definition");Require(restored.InstantSecondInput!.Sha256==second.ExpectedFrozenSha256,"Reopen second identity");var master=selected.LinearRawMaster!;lifecycle.Mark("Reopened");
  var formats=new List<object>();foreach(var format in new[]{StudioExportFormat.Tiff,StudioExportFormat.Png,StudioExportFormat.Jpeg})
  {
   lifecycle.Mark("BeforeExport"+format);Console.WriteLine("Export "+format);var folder=Path.Combine(output,format.ToString());Directory.CreateDirectory(folder);Environment.SetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_EXPORT_DIRECTORY",folder);restored.QuickExportFormat=format;foreach(var t in restored.Targets)t.IsSelected=ReferenceEquals(t,selected);watch.Restart();await restored.ExportSelectedCommand.ExecuteAsync(null);Require(selected.ExportStatus==ReferenceExportStatus.Succeeded,selected.Error??restored.StatusText);var path=selected.OutputPath!;Require(File.Exists(path),"Actual page export output");var receipt=ProfessionalRawExport.Inspect(path,master,definition,format);formats.Add(new{format,receipt,exportMs=watch.Elapsed.TotalMilliseconds,path,sha256=Hash(path)});lifecycle.Mark("AfterExport"+format);
  }
  Environment.SetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_EXPORT_DIRECTORY",null);
  var tiff=(string)formats.Select(x=>JsonSerializer.SerializeToElement(x)).First(x=>x.GetProperty("format").GetInt32()==(int)StudioExportFormat.Tiff).GetProperty("path").GetString()!;
  var previewError=ComparePreview(tiff,preview);Require(previewError<=2d/65535,"Actual preview/full linear-area oracle");
  var beforeCancel=selected.ActiveEditVersionId;using(var capture=CandidateProcessingEvidence.Capture(p=>{if(p=="PixelRender")restored.ExitInstantPresentationForLifecycle();}))await restored.GenerateInstantFilmCommand.ExecuteAsync(null);Require(selected.ActiveEditVersionId==beforeCancel&&JsonSerializer.Serialize(selected.RawDefinition)==frozen,"Cancellation rollback");
  preview.Dispose();baseline.Dispose();var timings=initialTimings;var process=Process.GetCurrentProcess();process.Refresh();var beforeDispose=StudioGlobalResources.AccountedBytes;restored.Dispose();await restored.ComparisonWork;Require(BoundedTiffPixelSource.ActiveMappings==0,"No active mappings after close");var afterDispose=StudioGlobalResources.AccountedBytes;lifecycle.Mark("WorkspaceClosed");File.WriteAllText(Path.Combine(output,"LIFECYCLE_WORK.json"),JsonSerializer.Serialize(lifecycle.Observations));
  foreach(var input in new[]{first,second})Require(Hash(input.Original)==input.ExpectedOriginalSha256,"Original unchanged");
  var result=new{first=first.Id,second=second.Id,firstFrozen=first.ExpectedFrozenSha256,secondFrozen=second.ExpectedFrozenSha256,firstDimensions=new{master.Image.Width,master.Image.Height},secondDimensions=new{restored.InstantSecondInput!.Width,restored.InstantSecondInput.Height},outputSize=new{size.Width,size.Height},applyMs,timings,definition,previewHash,previewError,limit=2d/65535,regionChanges=changes,formats,held,beforeDispose,afterDispose,privateBytes=process.PrivateMemorySize64,peakWorkingSet=process.PeakWorkingSet64,scope="Full original frozen photographs through actual new page bindings/commands/history/session/export. Different photographs and dimensions; no native RAW repeat claim"};
  File.WriteAllText(Path.Combine(output,"FULL_PAGE_RESULT.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
 }
 private static double ComparePreview(string tiff,HighBitDepthImageBuffer preview)
 {
  using var stream=File.OpenRead(tiff);var meta=TiffStreamValidator.Validate(stream);var sums=new double[preview.Width*preview.Height*3];double rx=meta.Width/(double)preview.Width,ry=meta.Height/(double)preview.Height;
  TiffStreamValidator.Validate(stream,inspectRow:(y,row)=>{for(int py=(int)(y/ry);py<=Math.Min(preview.Height-1,(int)((y+1)/ry));py++){double wy=Math.Max(0,Math.Min(y+1,(py+1)*ry)-Math.Max(y,py*ry));if(wy==0)continue;for(int x=0;x<meta.Width;x++)for(int px=(int)(x/rx);px<=Math.Min(preview.Width-1,(int)((x+1)/rx));px++){double wx=Math.Max(0,Math.Min(x+1,(px+1)*rx)-Math.Max(x,px*rx));for(int c=0;c<3;c++)sums[(py*preview.Width+px)*3+c]+=CandidateTone.Decode(BinaryPrimitives.ReadUInt16LittleEndian(row.Span[((x*3+c)*2)..])/65535d)*wx*wy;}}});
  double maximum=0;for(int i=0;i<sums.Length;i++)maximum=Math.Max(maximum,Math.Abs(CandidateTone.Encode(sums[i]/(rx*ry))-preview.Rgb32.Span[i]));return maximum;
 }
 private sealed class Dialogs(string output,string second):IDialogService
 {public string? ChooseFolder(string title,string? initialDirectory=null)=>output;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[second];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string m){}public void ShowError(string m){}public bool Confirm(string m,string t)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool a)=>false;public void RevealFile(string p){}}
}
