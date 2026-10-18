using System.IO;using System.Text.Json;using System.Security.Cryptography;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;using RAWSelectionAssistant.ViewModels;using RAWSelectionAssistant.Views;using RAWSelectionAssistant.Core.Services.Projects;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class PhotographerTrialPageTests
{
 [TestMethod]public Task NamesModesPreviewsAndHistoryUseActualProductState()=>RunSta(async()=>
 {
  EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"PhotographerPage-"+Guid.NewGuid());Directory.CreateDirectory(root);try
  {
   string Image(string name,byte value){var path=Path.Combine(root,name);StudioQuickExport.Encode(BitmapSource.Create(80,60,96,96,PixelFormats.Rgb24,null,Enumerable.Range(0,80*60*3).Select(x=>(byte)((x+value)%239)).ToArray(),240),path,path,StudioExportFormat.Png);return path;}
   using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(Image("a.png",20));var view=new PhotoGradePageView{DataContext=vm};view.Measure(new Size(1400,900));view.Arrange(new Rect(0,0,1400,900));view.UpdateLayout();var tabs=(TabControl)view.FindName("EditModes");tabs.SelectedIndex=2;Assert.AreEqual("V3",vm.InstantEngineVersion);
   var combo=(ComboBox)view.FindName("ComparisonMode");Assert.AreEqual("Label",combo.DisplayMemberPath);CollectionAssert.AreEqual(new[]{"效果图","配方基准","左右对比","并排对比"},vm.PhotographerComparisonModes.Select(x=>x.Label).ToArray());Assert.IsFalse(vm.PhotographerComparisonModes.Any(x=>x.Label=="原片"));
   vm.SelectedPhotographerPaperId="blue-duochrome600";Assert.AreEqual("blue-duochrome600",vm.SelectedInstantProfile.Id);var id=vm.ActiveTarget!.ActiveEditVersionId;var count=vm.ActiveTarget.EditVersions.Count;await vm.PreviewPhotographerPapersCommand.ExecuteAsync(null);
   Assert.IsTrue(vm.PhotographerPapers.All(x=>x.Preview is not null),vm.PaperPreviewStatus);Assert.AreEqual("blue-duochrome600",vm.SelectedInstantProfile.Id);Assert.AreEqual(id,vm.ActiveTarget.ActiveEditVersionId);Assert.AreEqual(count,vm.ActiveTarget.EditVersions.Count);Assert.IsNull(vm.ActiveTarget.RawDefinition);
   Assert.AreNotEqual(Hash(vm.PhotographerPapers.First().Preview!),Hash(vm.PhotographerPapers.Last().Preview!));
   vm.SelectedPhotographerPaperId="itype-color";vm.InstantInputMode="Original";await vm.GenerateInstantFilmCommand.ExecuteAsync(null);var frozen=JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition);var pixels=Hash(vm.Editor.MatchedImage!);Assert.AreEqual("仿色结果",combo.SelectedValue);Assert.AreEqual("配方基准",vm.PhotographerComparisonModes.Single(x=>x.Value=="原片").Label);Assert.IsFalse(vm.PhotoGradeRasterToolsEnabled);StringAssert.Contains(vm.RasterAvailabilityReason,"撤销");
   vm.PhotographerComparisonValue="并排对比";await vm.ComparisonWork;Assert.AreEqual(pixels,Hash(vm.Editor.MatchedImage!));Assert.IsTrue(vm.IsComparisonReady);
   await vm.PhotoGradeUndoCommand.ExecuteAsync(null);Assert.IsNull(vm.ActiveTarget.RawDefinition);Assert.IsTrue(vm.PhotoGradeRasterToolsEnabled);await vm.PhotoGradeRedoCommand.ExecuteAsync(null);Assert.AreEqual(frozen,JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition));Assert.AreEqual(pixels,Hash(vm.Editor.MatchedImage!));
   await vm.LoadTargetAsync(Image("b.png",90));Assert.IsTrue(vm.PhotographerPapers.All(x=>x.Preview is null),"No old-photo thumbnails");Assert.AreEqual("V3",vm.InstantEngineVersion,"Import while Polaroid tab selected must not require diagnostic version selection");view.DataContext=null;
  }finally{Directory.Delete(root,true);}
 });
 [TestMethod]public void SharedResponseNamesDoNotInventPixelDifferences()
 {
  Assert.AreEqual(Kitao.InstantFilm.Research.CandidateRecipe.Create("modern-itype-v3").Response,Kitao.InstantFilm.Research.CandidateRecipe.Create("modern-600-v3").Response);
  var choices=new ReferenceColorWorkspaceViewModel(new Dialogs(Path.GetTempPath()));try{Assert.HasCount(5,choices.PhotographerPapers);StringAssert.Contains(choices.PhotographerPapers.First().Description,"共用");}finally{choices.Dispose();}
 }
 private static string Hash(BitmapSource image){var c=new FormatConvertedBitmap(image,PixelFormats.Rgb48,null,0);var b=new byte[c.PixelWidth*c.PixelHeight*6];c.CopyPixels(b,c.PixelWidth*6,0);return Convert.ToHexString(SHA256.HashData(b));}
 private sealed class Dialogs(string root):IDialogService
 {public string? ChooseFolder(string title,string? initialDirectory=null)=>root;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string m){}public void ShowError(string m){}public bool Confirm(string m,string t)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool a)=>false;public void RevealFile(string p){}}
}
