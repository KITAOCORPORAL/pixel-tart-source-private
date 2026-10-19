using System.IO;using System.Text.Json;using System.Security.Cryptography;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;using RAWSelectionAssistant.ViewModels;using RAWSelectionAssistant.Views;using RAWSelectionAssistant.Core.Services.Projects;using Kitao.InstantFilm.Research;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class LightPrintR3Tests
{
 [TestMethod]public Task ClicksPreviewWithoutHistoryThenDevelopLocksAndReshootKeepsVariant()=>RunSta(async()=>
 {
  EnsureTestApplication();var root=Root();try
  {
   using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(Image(root,"first.png",30));var page=new PhotoGradePageView{DataContext=vm};page.Measure(new Size(1366,768));page.Arrange(new Rect(0,0,1366,768));page.UpdateLayout();var target=vm.ActiveTarget!;var count=target.EditVersions.Count;
   var before=Pixels(vm.Editor.MatchedImage??vm.Editor.SourceImage!);vm.SelectedPhotographerPaperId="blue-duochrome600";await vm.LightPrintPreviewWork;Assert.AreNotEqual(before,Pixels(vm.Editor.MatchedImage!),vm.LightPrintStatus);Assert.IsNull(target.RawDefinition);Assert.AreEqual(count,target.EditVersions.Count);
   var square=Pixels(vm.Editor.MatchedImage!);vm.SelectedLightPrintFrameId="generic.white.circle";await vm.LightPrintPreviewWork;Assert.AreNotEqual(square,Pixels(vm.Editor.MatchedImage!));
   vm.SelectedPhotographerPaperId="itype-bw";vm.SelectedPhotographerPaperId="sx70-color";vm.SelectedPhotographerPaperId="itype-color";await vm.LightPrintPreviewWork;Assert.AreEqual("itype-color",vm.SelectedInstantProfile.Id);vm.InstantCameraAnimation=false;
   await vm.DevelopLightPrintCommand.ExecuteAsync(null);Assert.IsNotNull(target.RawDefinition?.LightPrint,vm.StatusText);Assert.IsTrue(vm.IsLightPrintLocked);Assert.AreEqual(Math.Max(1,count)+1,target.EditVersions.Count);var old=target.ActiveEditVersionId;var frozen=JsonSerializer.Serialize(target.RawDefinition);var oldPixels=Pixels(vm.Editor.MatchedImage!);
   vm.SelectedPhotographerPaperId="itype-bw";vm.SelectedLightPrintFrameId="generic.cool.square";Assert.AreEqual("itype-color",vm.SelectedInstantProfile.Id);Assert.AreEqual("generic.white.circle",vm.SelectedLightPrintFrameId);Assert.AreEqual(frozen,JsonSerializer.Serialize(target.RawDefinition));
   vm.ReshootLightPrintCommand.Execute(null);vm.SelectedPhotographerPaperId="itype-bw";await vm.LightPrintPreviewWork;await vm.DevelopLightPrintCommand.ExecuteAsync(null);Assert.AreNotEqual(old,target.ActiveEditVersionId);Assert.AreEqual(Math.Max(1,count)+2,target.EditVersions.Count);Assert.AreEqual(frozen,JsonSerializer.Serialize(target.EditVersions.Single(v=>v.Id==old).RawDefinition));
   await vm.SwitchEditVersionAsync(old!.Value);Assert.AreEqual(oldPixels,Pixels(vm.Editor.MatchedImage!));await vm.PhotoGradeUndoCommand.ExecuteAsync(null);await vm.PhotoGradeRedoCommand.ExecuteAsync(null);
   var path=Path.Combine(root,"session.ptstudio.json");await vm.SaveSessionAsync(path);using var restored=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await restored.LoadSessionAsync(path);Assert.AreEqual(JsonSerializer.Serialize(target.RawDefinition),JsonSerializer.Serialize(restored.ActiveTarget!.RawDefinition));Assert.AreEqual(Pixels(vm.Editor.MatchedImage!),Pixels(restored.Editor.MatchedImage!));
   page.DataContext=null;
  }finally{Directory.Delete(root,true);}
 });
 [TestMethod]public Task WindowGradeChangesOnlyWindowAndThreeExportsKeepDefinition()=>RunSta(async()=>
 {
  EnsureTestApplication();var root=Root();try
  {
   using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(Image(root,"first.png",42));vm.EnableLightPrintWorkspace();vm.InstantCameraAnimation=false;vm.SelectedPhotographerPaperId="itype-color";await vm.LightPrintPreviewWork;await vm.DevelopLightPrintCommand.ExecuteAsync(null);var target=vm.ActiveTarget!;var version=target.ActiveEditVersionId;var basis=target.RawDefinition!;using var original=ProfessionalRawPipeline.RenderPreview(target.LinearRawMaster!,basis,1600);
   vm.Editor.AddAdjustmentNodeCommand.Execute("BasicTone");vm.Editor.SetToolParameter(ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.BasicTone).Single(p=>p.Key=="exposure"),.8);await vm.ApplyWindowGradeCommand.ExecuteAsync(null);Assert.AreEqual(version,target.ActiveEditVersionId);Assert.IsNotNull(target.RawDefinition!.LightPrint!.WindowAdjustments);using var changed=ProfessionalRawPipeline.RenderPreview(target.LinearRawMaster!,target.RawDefinition,1600);Assert.AreEqual(original.Rgb32.Span[0],changed.Rgb32.Span[0]);Assert.AreEqual(original.Rgb32.Span[^1],changed.Rgb32.Span[^1]);Assert.AreNotEqual(Hash(original.Rgb32.ToArray()),Hash(changed.Rgb32.ToArray()));
   await vm.PhotoGradeUndoCommand.ExecuteAsync(null);Assert.AreEqual(JsonSerializer.Serialize(basis),JsonSerializer.Serialize(target.RawDefinition));await vm.PhotoGradeRedoCommand.ExecuteAsync(null);Assert.IsNotNull(target.RawDefinition!.LightPrint!.WindowAdjustments);
   foreach(var format in new[]{StudioExportFormat.Png,StudioExportFormat.Tiff,StudioExportFormat.Jpeg}){vm.QuickExportFormat=format;await vm.ExportSelectedCommand.ExecuteAsync(null);Assert.AreEqual(ReferenceExportStatus.Succeeded,target.ExportStatus,target.Error);Assert.IsNotNull(target.LastProfessionalOutput);}
  }finally{Directory.Delete(root,true);}
 });
 [TestMethod]public Task CancelAndSwitchNeverPublishObsoleteDraft()=>RunSta(async()=>
 {
  EnsureTestApplication();var root=Root();try
  {
   using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(Image(root,"a.png",20));vm.EnableLightPrintWorkspace();var before=Pixels(vm.Editor.MatchedImage??vm.Editor.SourceImage!);vm.SelectedPhotographerPaperId="blue-duochrome600";vm.CancelLightPrintPreviewCommand.Execute(null);await vm.LightPrintPreviewWork;Assert.AreEqual(before,Pixels(vm.Editor.MatchedImage??vm.Editor.SourceImage!));
   vm.SelectedPhotographerPaperId="itype-bw";await vm.LoadTargetAsync(Image(root,"b.png",130));await vm.LightPrintPreviewWork;Assert.IsNull(vm.ActiveTarget!.RawDefinition);Assert.IsFalse(vm.LightPrintStatus.Contains("未完成"));Assert.AreEqual("b.png",vm.ActiveTarget.FileName);
  }finally{Directory.Delete(root,true);}
 });
 private static string Root(){var p=Path.Combine(Path.GetTempPath(),"LightPrintR3-"+Guid.NewGuid());Directory.CreateDirectory(p);return p;}
 private static string Image(string r,string n,int shift){var p=Path.Combine(r,n);StudioQuickExport.Encode(BitmapSource.Create(160,120,96,96,PixelFormats.Rgb24,null,Enumerable.Range(0,160*120*3).Select(x=>(byte)((x+shift)%220+15)).ToArray(),480),p,p,StudioExportFormat.Png);return p;}
 private static string Hash(float[] values)=>Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(values.AsSpan())));
 private static string Pixels(BitmapSource b){var x=new FormatConvertedBitmap(b,PixelFormats.Rgb48,null,0);var a=new byte[x.PixelWidth*x.PixelHeight*6];x.CopyPixels(a,x.PixelWidth*6,0);return Convert.ToHexString(SHA256.HashData(a));}
 private sealed class Dialogs(string root):IDialogService
 {public string? ChooseFolder(string title,string? initialDirectory=null)=>root;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string m){}public void ShowError(string m){}public bool Confirm(string m,string t)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool a)=>false;public void RevealFile(string p){}}
}
