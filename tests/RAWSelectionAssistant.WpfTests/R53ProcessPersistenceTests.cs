using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Core.Services.Projects;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class R53ProcessPersistenceTests
{
 [TestMethod]public Task IndependentProcessCreatesOrReopensFrozenMaskAndExports()=>RunSta(async()=>
 {
  var root=Environment.GetEnvironmentVariable("KITAO_R53_PROCESS_ROOT");var mode=Environment.GetEnvironmentVariable("KITAO_R53_PROCESS_MODE");if(string.IsNullOrWhiteSpace(root)||mode is not("create" or "reopen")){Assert.Inconclusive("Independent process chain requires explicit isolated output.");return;}Directory.CreateDirectory(root);EnsureTestApplication();var path=Path.Combine(root,"process.ptstudio.json");var exportRoot=Path.Combine(root,mode+"-export");Directory.CreateDirectory(exportRoot);using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(exportRoot));
  if(mode=="create"){
   var input=Path.Combine(root,"input.png");StudioQuickExport.Encode(BitmapSource.Create(96,72,96,96,PixelFormats.Rgb24,null,Enumerable.Range(0,96*72*3).Select(i=>(byte)(40+i%120)).ToArray(),96*3),input,input,StudioExportFormat.Png);await vm.LoadTargetAsync(input);vm.Editor.SetToolParameter(ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.BasicTone).Single(p=>p.Key=="exposure"),.5);vm.Editor.SelectedLayerOpacity=.6;vm.Editor.AddMaskStroke(new([new(.25,.3),new(.65,.55)],.16,.7,.8,1));vm.R53Professional=true;vm.R53Professional=false;await vm.Editor.ApplyCommand.ExecuteAsync(null);await vm.AnalysisWork;Assert.IsNotNull(vm.Editor.MatchedImage);await vm.SaveSessionAsync(path);await vm.ExportSelectedCommand.ExecuteAsync(null);var image=Directory.GetFiles(exportRoot,"*_仿色.png").Single();File.Copy(image,Path.Combine(root,"expected.png"));File.WriteAllText(Path.Combine(root,"definition.json"),JsonSerializer.Serialize(vm.Editor.AdjustmentStack));File.WriteAllText(Path.Combine(root,"create.json"),JsonSerializer.Serialize(new{pid=Environment.ProcessId,scope="Actual product VM and real PNG; testhost, not pointer acceptance"}));
  }else{
   await vm.LoadSessionAsync(path);await vm.Editor.ApplyCommand.ExecuteAsync(null);await vm.AnalysisWork;Assert.AreEqual(File.ReadAllText(Path.Combine(root,"definition.json")),JsonSerializer.Serialize(vm.Editor.AdjustmentStack));await vm.ExportSelectedCommand.ExecuteAsync(null);var paths=Directory.GetFiles(exportRoot,"*_仿色*.png");Assert.AreEqual(1,paths.Length,vm.ExportStatus);Assert.AreEqual(ReferenceExportStatus.Succeeded,vm.ActiveTarget!.ExportStatus,vm.ActiveTarget.Error);var expected=Pixels(StudioQuickExport.Load(Path.Combine(root,"expected.png")));foreach(var image in paths)CollectionAssert.AreEqual(expected,Pixels(StudioQuickExport.Load(image)));File.WriteAllText(Path.Combine(root,"reopen.json"),JsonSerializer.Serialize(new{pid=Environment.ProcessId,previewEqualsExport=Pixels(vm.Editor.MatchedImage!).SequenceEqual(expected),scope="New independent testhost process; not full EXE native acceptance"}));
  }
 });
 static byte[] Pixels(BitmapSource s){var rgb=new FormatConvertedBitmap(s,PixelFormats.Rgb24,null,0);var bytes=new byte[rgb.PixelWidth*rgb.PixelHeight*3];rgb.CopyPixels(bytes,rgb.PixelWidth*3,0);return bytes;}
 sealed class Dialogs(string root):IDialogService{public IReadOnlyList<string> ChooseFiles(string a,string b,bool c)=>[];public string? ChooseFolder(string a,string? b=null)=>root;public string? ChooseSaveFile(string a,string b,string c,string? d=null)=>null;public bool Confirm(string a,string b)=>true;public void ShowInfo(string a){}public void ShowError(string a){}public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>ids;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> x)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem x,bool y)=>false;public void RevealFile(string p){}}
}
