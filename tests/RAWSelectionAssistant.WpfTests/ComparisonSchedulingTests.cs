using System.IO;using System.Text.Json;using System.Security.Cryptography;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Media.Imaging;
using Kitao.InstantFilm.Research;using RAWSelectionAssistant.Services;using RAWSelectionAssistant.ViewModels;using RAWSelectionAssistant.Views;using RAWSelectionAssistant.Core.Services.Projects;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class ComparisonSchedulingTests
{
 [TestMethod]public Task EffectVisibleBeforeComparisonAndCompletionNeverChangesHistory()=>RunSta(async()=>
 {
  EnsureTestApplication();var root=Root();try
  {
   using var vm=await Create(root);var view=new PhotoGradePageView{DataContext=vm};view.Measure(new Size(1600,1000));view.Arrange(new Rect(0,0,1600,1000));view.UpdateLayout();
   var target=vm.ActiveTarget!;var id=target.ActiveEditVersionId;var count=target.EditVersions.Count;var recipe=JsonSerializer.Serialize(target.RawDefinition);var effect=Pixels(vm.Editor.MatchedImage!);
   Assert.IsNull(vm.Editor.SourceImage);Assert.IsTrue(vm.Editor.ComparisonUnavailable);Assert.IsFalse(vm.IsInstantGenerating);Assert.IsTrue(vm.GenerateInstantFilmCommand.CanExecute(null));
   Assert.AreEqual("仿色结果",vm.Editor.EffectiveViewMode);
   var viewport=(ColorStudioImageViewport)view.FindName("PhotoViewport");view.UpdateLayout();Assert.IsNull(viewport.Original);Assert.IsNotNull(viewport.Matched);
   var rendered=new RenderTargetBitmap(1600,1000,96,96,PixelFormats.Pbgra32);rendered.Render(view);var renderedBytes=new byte[1600*1000*4];rendered.CopyPixels(renderedBytes,1600*4,0);Assert.IsTrue(renderedBytes.Any(x=>x!=0),"Effect-only viewport must draw while comparison absent");Assert.IsTrue(vm.PrepareComparisonCommand.CanExecute(null));
   var combo=(ComboBox)view.FindName("ComparisonMode");combo.SetCurrentValue(ComboBox.SelectedValueProperty,"并排对比");await vm.ComparisonWork;
   Assert.IsTrue(vm.IsComparisonReady,vm.ComparisonStatus);Assert.IsNotNull(vm.Editor.SourceImage);Assert.AreEqual("并排对比",vm.Editor.EffectiveViewMode);
   Assert.AreEqual(effect,Pixels(vm.Editor.MatchedImage!));Assert.AreEqual(id,target.ActiveEditVersionId);Assert.AreEqual(count,target.EditVersions.Count);Assert.AreEqual(recipe,JsonSerializer.Serialize(target.RawDefinition));
   var master=target.LinearRawMaster!;using var expected=ProfessionalRawPipeline.RenderPreview(master,InstantV3ProductPipeline.Original(target.RawDefinition!),1600);
   Assert.AreEqual(Pixels(RawDisplayBitmapAdapter.ToBitmap(expected.ToVisualRgb24())),Pixels(vm.Editor.SourceImage));
   view.DataContext=null;
  }finally{Directory.Delete(root,true);}
 });
 [DataTestMethod][DataRow("switch")][DataRow("apply")][DataRow("undo")][DataRow("reopen")][DataRow("export")][DataRow("close")]
 public Task PendingComparisonYieldsToLatestOperationAndDiscardsStalePixels(string operation)=>RunSta(async()=>
 {
  EnsureTestApplication();var root=Root();try
  {
   using var vm=await Create(root);var target=vm.ActiveTarget!;var oldId=target.ActiveEditVersionId;var count=target.EditVersions.Count;var source=vm.Editor.MatchedImage;var session=Path.Combine(root,"project.ptstudio.json");await vm.SaveSessionAsync(session);
   var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);using var gate=new ManualResetEventSlim(false);int held=0;
   using var capture=CandidateProcessingEvidence.Capture(p=>{if(p=="PixelRender"&&Interlocked.CompareExchange(ref held,1,0)==0){started.TrySetResult();if(!gate.Wait(10000))throw new TimeoutException("comparison test gate");}});
   var compare=vm.RequestComparisonAsync();await started.Task.WaitAsync(TimeSpan.FromSeconds(10));Assert.IsTrue(vm.IsComparisonPreparing);Assert.IsFalse(vm.Editor.SourceImage is not null);Assert.IsTrue(vm.GenerateInstantFilmCommand.CanExecute(null));
   Task action=operation switch
   {
    "switch"=>vm.LoadTargetAsync(Image(root,"second.png",151)),
    "apply"=>Apply(),"undo"=>vm.PhotoGradeUndoCommand.ExecuteAsync(null),
    "reopen"=>vm.LoadSessionAsync(session),"export"=>vm.ExportSelectedCommand.ExecuteAsync(null),
    _=>Close()
   };
   gate.Set();await action;await compare;
   Assert.IsTrue(compare.IsCompleted);Assert.IsFalse(vm.IsComparisonPreparing);
   if(operation=="switch"){Assert.AreNotSame(target,vm.ActiveTarget);Assert.IsNull(vm.ActiveTarget!.RawDefinition);}
   if(operation=="apply"){Assert.AreNotEqual(oldId,target.ActiveEditVersionId);Assert.AreEqual(count+1,target.EditVersions.Count);Assert.IsTrue(vm.Editor.ComparisonUnavailable);Assert.IsNull(vm.Editor.SourceImage);}
   if(operation=="undo"){Assert.IsNull(target.RawDefinition);Assert.IsFalse(vm.Editor.IndependentRawMode);}
   if(operation=="reopen"){Assert.AreNotSame(target,vm.ActiveTarget);Assert.AreEqual(oldId,vm.ActiveTarget!.ActiveEditVersionId);Assert.IsNull(vm.Editor.SourceImage);}
   if(operation=="export"){Assert.AreEqual(ReferenceExportStatus.Succeeded,target.ExportStatus);Assert.AreEqual(oldId,target.ActiveEditVersionId);Assert.AreEqual(count,target.EditVersions.Count);Assert.AreSame(source,vm.Editor.MatchedImage);}
   if(operation=="close"){Assert.IsNull(vm.Editor.MatchedImage);Assert.IsNull(vm.Editor.SourceImage);Assert.IsNull(target.LinearRawMaster);}
   async Task Apply(){vm.InstantInputMode="Original";vm.InstantV3Exposure=.7;await vm.GenerateInstantFilmCommand.ExecuteAsync(null);}
   Task Close(){vm.Dispose();return Task.CompletedTask;}
  }finally{Directory.Delete(root,true);}
 });
 [TestMethod]public Task ComparisonFaultOrChangedSourceKeepsEffectAndCanRetry()=>RunSta(async()=>
 {
  EnsureTestApplication();var root=Root();try
  {
   using var vm=await Create(root);var target=vm.ActiveTarget!;var count=target.EditVersions.Count;var id=target.ActiveEditVersionId;var effect=Pixels(vm.Editor.MatchedImage!);
   using(var capture=CandidateProcessingEvidence.Capture(p=>{if(p=="ComparisonPrepared")throw new IOException("comparison only failure");}))await vm.RequestComparisonAsync();
   Assert.IsTrue(vm.Editor.ComparisonUnavailable);Assert.IsNull(vm.Editor.SourceImage);Assert.AreEqual(effect,Pixels(vm.Editor.MatchedImage!));Assert.AreEqual(count,target.EditVersions.Count);Assert.IsTrue(vm.PrepareComparisonCommand.CanExecute(null));
   await vm.RequestComparisonAsync();Assert.IsTrue(vm.IsComparisonReady);Assert.AreEqual(id,target.ActiveEditVersionId);
   vm.InstantInputMode="Original";vm.InstantV3Exposure=.4;await vm.GenerateInstantFilmCommand.ExecuteAsync(null);
   var bytes=File.ReadAllBytes(target.Path);using(var capture=CandidateProcessingEvidence.Capture(p=>{if(p=="ComparisonPrepared")File.WriteAllText(target.Path,"changed after render");}))await vm.RequestComparisonAsync();
   Assert.IsNull(vm.Editor.SourceImage);Assert.IsTrue(vm.Editor.ComparisonUnavailable);File.WriteAllBytes(target.Path,bytes);
  }finally{Directory.Delete(root,true);}
 });
 private static async Task<ReferenceColorWorkspaceViewModel> Create(string root)
 {var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(Image(root,"input.png",78));vm.UsePhotoGradePage=true;vm.InstantEngineVersion="V3";vm.InstantInputMode="Original";vm.InstantDevelopmentDemo=false;await vm.GenerateInstantFilmCommand.ExecuteAsync(null);return vm;}
 private static string Root(){var p=Path.Combine(Path.GetTempPath(),"CompareSchedule-"+Guid.NewGuid());Directory.CreateDirectory(p);return p;}
 private static string Image(string root,string name,byte value){var p=Path.Combine(root,name);StudioQuickExport.Encode(BitmapSource.Create(61,43,96,96,PixelFormats.Rgb24,null,Enumerable.Repeat(value,61*43*3).ToArray(),61*3),p,p,StudioExportFormat.Png);return p;}
 private static string Pixels(BitmapSource image){var c=new FormatConvertedBitmap(image,PixelFormats.Rgb48,null,0);var b=new byte[c.PixelWidth*c.PixelHeight*6];c.CopyPixels(b,c.PixelWidth*6,0);return Convert.ToHexString(SHA256.HashData(b));}
 private sealed class Dialogs(string root):IDialogService
 {public string? ChooseFolder(string title,string? initialDirectory=null)=>root;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string m){}public void ShowError(string m){}public bool Confirm(string m,string t)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool a)=>false;public void RevealFile(string p){}}
}
