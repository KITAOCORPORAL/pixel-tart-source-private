using System.IO;using System.Text.Json;using System.Security.Cryptography;using System.Windows;using System.Windows.Media;using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;using RAWSelectionAssistant.ViewModels;using RAWSelectionAssistant.Core.Services.Projects;using Kitao.InstantFilm.Research;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class ReviewSafetyTests
{
 [TestMethod]public Task RasterExportFailureCancelExistingAndRetryOnlyOwnArtifacts()=>RunSta(async()=>
 {
  EnsureTestApplication();var root=NewRoot();try
  {
   var a=Image(root,"a.png",71);var b=Image(root,"b.png",120);var output=Path.Combine(root,"out");Directory.CreateDirectory(output);var dialogs=new Dialogs(output);using var vm=new ReferenceColorWorkspaceViewModel(dialogs);await vm.LoadTargetAsync(a);vm.QuickExportFormat=StudioExportFormat.Png;
   var source=Hash(a);await Assert.ThrowsAsync<IOException>(()=>vm.SaveSessionAsync(a));Assert.AreEqual(source,Hash(a));var first=Path.Combine(output,"a_仿色.png");var foreign=first+".tmp";File.WriteAllText(foreign,"foreign temp");File.WriteAllText(first,"existing final");
   await vm.ExportSelectedCommand.ExecuteAsync(null);Assert.AreEqual("existing final",File.ReadAllText(first));Assert.AreEqual("foreign temp",File.ReadAllText(foreign));Assert.AreNotEqual(ReferenceExportStatus.Succeeded,vm.ActiveTarget!.ExportStatus);File.Delete(first);
   using(var capture=CandidateProcessingEvidence.Capture(p=>{if(p=="RasterExportEncoded")throw new IOException("Simulated disk/publish boundary failure");}))await vm.ExportSelectedCommand.ExecuteAsync(null);
   Assert.IsFalse(File.Exists(first));Assert.IsFalse(File.Exists(first+".kitao-export.json"));Assert.IsFalse(Directory.EnumerateFiles(output,".kitao-stage-*").Any());Assert.AreEqual("foreign temp",File.ReadAllText(foreign));Assert.AreEqual(source,Hash(a));
   using(var capture=CandidateProcessingEvidence.Capture(p=>{if(p=="RasterExportEncoded"){vm.StopExportCommand.Execute(null);}}))await vm.ExportSelectedCommand.ExecuteAsync(null);
   Assert.IsFalse(File.Exists(first));Assert.AreEqual(ReferenceExportStatus.Cancelled,vm.ActiveTarget.ExportStatus);Assert.IsFalse(Directory.EnumerateFiles(output,".kitao-stage-*").Any());
   await vm.ExportSelectedCommand.ExecuteAsync(null);Assert.AreEqual(ReferenceExportStatus.Succeeded,vm.ActiveTarget.ExportStatus);var firstHash=Hash(first);var receipt=await ExportRecovery.ReadAsync(first+".kitao-export.json");Assert.AreEqual(firstHash,receipt.OutputHash);
   vm.ActiveTarget.IsSelected=false;await vm.LoadTargetAsync(b);var second=Path.Combine(output,"b_仿色.png");using(var capture=CandidateProcessingEvidence.Capture(p=>{if(p=="RasterExportEncoded")throw new IOException("once");}))await vm.ExportSelectedCommand.ExecuteAsync(null);
   Assert.IsFalse(File.Exists(second));await vm.RetryFailedExportCommand.ExecuteAsync(null);Assert.IsTrue(File.Exists(second));Assert.AreEqual(firstHash,Hash(first));Assert.AreEqual(source,Hash(a));Assert.AreEqual("foreign temp",File.ReadAllText(foreign));
   // A destination appearing after encode is never overwritten or reported successful.
   vm.ActiveTarget.IsSelected=false;var c=Image(root,"c.png",94);await vm.LoadTargetAsync(c);var third=Path.Combine(output,"c_仿色.png");using(var capture=CandidateProcessingEvidence.Capture(p=>{if(p=="RasterExportEncoded")File.WriteAllText(third,"racing owner");}))await vm.ExportSelectedCommand.ExecuteAsync(null);Assert.AreEqual("racing owner",File.ReadAllText(third));Assert.AreNotEqual(ReferenceExportStatus.Succeeded,vm.ActiveTarget!.ExportStatus);Assert.IsFalse(File.Exists(third+".kitao-export.json"));
  }finally{Directory.Delete(root,true);}
 });
 [TestMethod]public Task SessionTwoInstancesAndInvalidActivationKeepOldHistoryAndFile()=>RunSta(async()=>
 {
  EnsureTestApplication();var root=NewRoot();try
  {
   var image=Image(root,"a.png",80);var file=Path.Combine(root,"shared.ptstudio.json");var dialogs=new Dialogs(root){Save=file};using var first=new ReferenceColorWorkspaceViewModel(dialogs);await first.LoadTargetAsync(image);first.Editor.Exposure=.2;await first.SaveSessionAsync(file);
   using var second=new ReferenceColorWorkspaceViewModel(new Dialogs(root){Save=file});await second.LoadSessionAsync(file);first.Editor.Exposure=.4;await first.SaveSessionAsync(file);var authoritative=Hash(file);second.Editor.Exposure=.9;
   using(var third=new ReferenceColorWorkspaceViewModel(new Dialogs(root))){await third.LoadTargetAsync(image);await Assert.ThrowsAsync<InvalidOperationException>(()=>third.SaveSessionAsync(file));Assert.AreEqual(authoritative,Hash(file));}
   await Assert.ThrowsAsync<InvalidOperationException>(()=>second.SaveSessionAsync(file));Assert.AreEqual(authoritative,Hash(file));await second.SaveSessionCommand.ExecuteAsync(null);Assert.AreEqual(authoritative,Hash(file));StringAssert.Contains(second.StatusText,"其他实例");
   var active=second.ActiveTarget;var before=JsonSerializer.Serialize(second.Editor.AdjustmentStack);var undo=second.Editor.UndoAdjustmentCommand.CanExecute(null);var bad=Path.Combine(root,"bad.json");await File.WriteAllTextAsync(bad,"{ broken");await Assert.ThrowsAsync<JsonException>(()=>second.LoadSessionAsync(bad));Assert.AreSame(active,second.ActiveTarget);Assert.AreEqual(before,JsonSerializer.Serialize(second.Editor.AdjustmentStack));Assert.AreEqual(undo,second.Editor.UndoAdjustmentCommand.CanExecute(null));
   var other=Image(root,"other.png",44);using(var creator=new ReferenceColorWorkspaceViewModel(new Dialogs(root))){await creator.LoadTargetAsync(other);await creator.SaveSessionAsync(Path.Combine(root,"other.ptstudio.json"));}
   File.WriteAllText(other,"broken bitmap");await Assert.ThrowsAsync<InvalidDataException>(()=>second.LoadSessionAsync(Path.Combine(root,"other.ptstudio.json")));Assert.AreSame(active,second.ActiveTarget);Assert.AreEqual(before,JsonSerializer.Serialize(second.Editor.AdjustmentStack));
   Image(root,"other.png",44);
   using(var capture=CandidateProcessingEvidence.Capture(p=>{if(p=="SessionCommit")throw new IOException("Commit boundary fault");}))await Assert.ThrowsAsync<IOException>(()=>second.LoadSessionAsync(Path.Combine(root,"other.ptstudio.json")));
   Assert.AreSame(active,second.ActiveTarget);Assert.AreEqual(before,JsonSerializer.Serialize(second.Editor.AdjustmentStack));Assert.AreEqual(undo,second.Editor.UndoAdjustmentCommand.CanExecute(null));
   var state=JsonSerializer.Serialize(second.Editor.AdjustmentStack);second.Editor.UndoAdjustmentCommand.Execute(null);Assert.AreNotEqual(state,JsonSerializer.Serialize(second.Editor.AdjustmentStack));second.Editor.RedoAdjustmentCommand.Execute(null);Assert.AreEqual(state,JsonSerializer.Serialize(second.Editor.AdjustmentStack));
   using(var capture=CandidateProcessingEvidence.Capture(p=>{if(p=="SessionPrepared")File.WriteAllText(other,"late source replacement");}))await Assert.ThrowsAsync<InvalidDataException>(()=>second.LoadSessionAsync(Path.Combine(root,"other.ptstudio.json")));
   Assert.AreSame(active,second.ActiveTarget);Assert.AreEqual(before,JsonSerializer.Serialize(second.Editor.AdjustmentStack));Assert.AreEqual(undo,second.Editor.UndoAdjustmentCommand.CanExecute(null));
   File.Delete(other);await Assert.ThrowsAsync<FileNotFoundException>(()=>second.LoadSessionAsync(Path.Combine(root,"other.ptstudio.json")));Assert.AreSame(active,second.ActiveTarget);
   second.Editor.UndoAdjustmentCommand.Execute(null);second.Editor.RedoAdjustmentCommand.Execute(null);second.Editor.Exposure=.7;await second.SaveSessionAsync(Path.Combine(root,"recover.ptstudio.json"));Assert.IsTrue(File.Exists(Path.Combine(root,"recover.ptstudio.json")));
  }finally{Directory.Delete(root,true);}
 });
 [TestMethod]public void PublishingLeaseReleaseNeedsOwnerLockAndPreservesUnknownOrChangedFiles()
 {
  var root=NewRoot();try
  {
   var lease=new PublishingIntermediateLease(root);var owned=Path.Combine(lease.DirectoryPath,"owned.png");lease.OwnNewFile(owned);File.WriteAllText(owned,"encoded");lease.Complete(owned);var foreign=Path.Combine(lease.DirectoryPath,"foreign.txt");File.WriteAllText(foreign,"keep");lease.Handoff();var manifest=lease.ManifestPath;
   Assert.Throws<IOException>(()=>PublishingIntermediateLease.Release(manifest));lease.Dispose();using(var consumer=RAWSelectionAssistant.Core.Services.Publishing.PublishingIntermediateUse.Acquire([Path.Combine(lease.DirectoryPath,"item", "unused.png")]))Assert.Throws<IOException>(()=>PublishingIntermediateLease.Release(manifest));File.WriteAllText(owned,"changed");Assert.Throws<IOException>(()=>PublishingIntermediateLease.Release(manifest));Assert.IsTrue(File.Exists(owned));File.WriteAllText(owned,"encoded");var removed=PublishingIntermediateLease.Release(manifest);Assert.HasCount(1,removed);Assert.IsFalse(File.Exists(owned));Assert.AreEqual("keep",File.ReadAllText(foreign));Assert.IsTrue(File.Exists(manifest));
  }finally{Directory.Delete(root,true);}
 }
 private static string NewRoot(){var p=Path.Combine(Path.GetTempPath(),"ReviewSafety-"+Guid.NewGuid());Directory.CreateDirectory(p);return p;}
 private static string Image(string root,string name,byte v){var p=Path.Combine(root,name);StudioQuickExport.Encode(BitmapSource.Create(31,23,96,96,PixelFormats.Rgb24,null,Enumerable.Repeat(v,31*23*3).ToArray(),31*3),p,p,StudioExportFormat.Png);return p;}
 private static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
 private sealed class Dialogs(string root):IDialogService
 {public string? Save;public string? ChooseFolder(string title,string? initialDirectory=null)=>root;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>Save;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string m){}public void ShowError(string m){}public bool Confirm(string m,string t)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool a)=>false;public void RevealFile(string p){}}
}
