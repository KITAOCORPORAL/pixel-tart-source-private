using System.IO;using System.Text.Json;using System.Diagnostics;using System.Security.Cryptography;using System.Windows;using System.Windows.Media;using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;using RAWSelectionAssistant.ViewModels;using RAWSelectionAssistant.Core.Services.Projects;using RAWSelectionAssistant.Core.Services.Color.PhotoWideV1;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class LinearSrgbAndImmediateTests
{
 [TestMethod]public Task ImmediateAndSkippedDemoKeepSamePixelsRecipeSeedAndOneHistoryStep()=>RunSta(async()=>
 {
  EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"ImmediateTest-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   var path=Make(root);using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(path);vm.InstantEngineVersion="V3";vm.InstantCameraAnimation=false;vm.InstantV3Seed=999;
   var watch=Stopwatch.StartNew();await vm.GenerateInstantFilmCommand.ExecuteAsync(null);var instantMs=watch.Elapsed.TotalMilliseconds;Assert.IsNotNull(vm.ActiveTarget!.RawDefinition!.InstantFilm!.V3,vm.StatusText);Assert.IsTrue(vm.LastInstantApplyTimings!.DemoMs<100);Assert.IsTrue(instantMs<3000,"No four-second synthetic wait allowed");
   var pixels=Pixels(vm.Editor.MatchedImage!);var recipe=JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition);var count=vm.ActiveTarget.EditVersions.Count;
   vm.InstantDevelopmentDemo=true;using(var capture=Kitao.InstantFilm.Research.CandidateProcessingEvidence.Capture(p=>{if(p=="DevelopmentAnimation")vm.SkipInstantDemoCommand.Execute(null);}))await vm.GenerateInstantFilmCommand.ExecuteAsync(null);
   Assert.AreEqual(recipe,JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition));Assert.AreEqual(pixels,Pixels(vm.Editor.MatchedImage!));Assert.AreEqual(count+1,vm.ActiveTarget.EditVersions.Count);Assert.AreEqual(999UL,vm.InstantV3Seed);
   var output=Environment.GetEnvironmentVariable("KITAO_TEST_EVIDENCE_ROOT");if(!string.IsNullOrWhiteSpace(output)){Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"IMMEDIATE.json"),JsonSerializer.Serialize(new{instantMs,skip=vm.LastInstantApplyTimings},new JsonSerializerOptions{WriteIndented=true}));}
  }
  finally{Directory.Delete(root,true);}
 });
 [TestMethod]public Task LinearExposureUsesActualProductionRenderHistoryReopenAndThreeOutputs()=>RunSta(async()=>
 {
  EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"LinearChainTest-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   var path=Make(root);using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(path);var before=Pixels(vm.Editor.MatchedImage??vm.Editor.SourceImage!);vm.SrgbExposureEv=.7;await vm.ApplySrgbExposureCommand.ExecuteAsync(null);
   Assert.AreEqual(.7,vm.ActiveTarget!.RawDefinition!.SrgbExposure!.ExposureEv,vm.StatusText);Assert.AreEqual(2,vm.ActiveTarget.RawDefinition.RenderedInputVersion);var after=Pixels(vm.Editor.MatchedImage!);Assert.AreNotEqual(before,after);var serialized=JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition);
   var master=vm.ActiveTarget.LinearRawMaster!;using var actual=ProfessionalRawPipeline.Render(master,vm.ActiveTarget.RawDefinition);var expected=new RenderedSrgbExposure(ExposureEv:.7);
   for(int i=0;i<actual.Rgb32.Length;i+=3){var p=master.Image.Rgb32.Span;var e=expected.Apply(new(p[i],p[i+1],p[i+2]));Assert.AreEqual((float)e.X,actual.Rgb32.Span[i]);Assert.AreEqual((float)e.Y,actual.Rgb32.Span[i+1]);Assert.AreEqual((float)e.Z,actual.Rgb32.Span[i+2]);}
   await vm.PhotoGradeUndoCommand.ExecuteAsync(null);Assert.IsNull(vm.ActiveTarget.RawDefinition);Assert.AreEqual(before,Pixels(vm.Editor.MatchedImage??vm.Editor.SourceImage!));await vm.PhotoGradeRedoCommand.ExecuteAsync(null);Assert.AreEqual(after,Pixels(vm.Editor.MatchedImage!));
   var session=Path.Combine(root,"chain.ptstudio.json");await vm.SaveSessionAsync(session);using var restored=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await restored.LoadSessionAsync(session);Assert.AreEqual(serialized,JsonSerializer.Serialize(restored.ActiveTarget!.RawDefinition));Assert.AreEqual(after,Pixels(restored.Editor.MatchedImage!));Assert.AreEqual(.7,restored.SrgbExposureEv);
   foreach(var format in new[]{StudioExportFormat.Tiff,StudioExportFormat.Png,StudioExportFormat.Jpeg}){var output=Path.Combine(root,"chain"+StudioQuickExport.Extension(path,format));await ProfessionalRawExport.WriteAsync(restored.ActiveTarget.LinearRawMaster!,restored.ActiveTarget.RawDefinition!,output,format);var result=ProfessionalRawExport.Inspect(output,restored.ActiveTarget.LinearRawMaster!,restored.ActiveTarget.RawDefinition!,format);if(format!=StudioExportFormat.Jpeg)Assert.AreEqual(0,result.MaximumError);else{Assert.IsTrue(result.MeanAbsoluteError<=5);Assert.IsTrue(result.RootMeanSquareError<=15);}}
   Assert.Throws<InvalidDataException>(()=>new ProfessionalRawDefinition(){SrgbExposure=new()}.Validate());
  }
  finally{Directory.Delete(root,true);}
 });
 private static string Make(string root){var path=Path.Combine(root,"input.png");StudioQuickExport.Encode(BitmapSource.Create(61,43,96,96,PixelFormats.Rgb24,null,Enumerable.Range(0,61*43*3).Select(x=>(byte)(x%239)).ToArray(),61*3),path,path,StudioExportFormat.Png);return path;}
 private static string Pixels(BitmapSource image){var rgb=new FormatConvertedBitmap(image,PixelFormats.Rgb48,null,0);var bytes=new byte[rgb.PixelWidth*rgb.PixelHeight*6];rgb.CopyPixels(bytes,rgb.PixelWidth*6,0);return Convert.ToHexString(SHA256.HashData(bytes));}
 private sealed class Dialogs(string root):IDialogService
 {public string? ChooseFolder(string title,string? initialDirectory=null)=>root;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string m){}public void ShowError(string m){}public bool Confirm(string m,string t)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool a)=>false;public void RevealFile(string p){}}
}
