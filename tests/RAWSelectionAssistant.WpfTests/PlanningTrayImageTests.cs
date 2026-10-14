using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Views;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class PlanningTrayImageTests
{
 [TestMethod]
 public Task TrayCopySurvivesClearOfflineReopenAndDocxWithoutJsonImagePayload()=>RunSta(async()=>
 {
  EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await using var tray=new SqliteInspirationTrayService(Path.Combine(f.Root,"tray.db"));await tray.InitializeAsync();
  var path=Path.Combine(f.Root,"source.png");var bytes=PlanningRichFlowFixture.ReferencePng(1);await File.WriteAllBytesAsync(path,bytes);var reference=new AssetLibraryStableReference(Guid.NewGuid(),Guid.NewGuid(),Convert.ToHexString(SHA256.HashData(bytes)));await tray.AddAsync(reference,note:"合成参考");
  PlanningCenterViewModel Vm()=>new(f.Projects,f.Bookings,f.Dialogs,f.Shots,f.Planning,inspiration:tray);
  var vm=Vm();await vm.LoadAsync(f.Id);vm.ResolveTrayImage=r=>Task.FromResult(new AssetResolutionResult(AssetResolutionStatus.Available,r.LibraryId,r.AssetId,path,r.ContentHash));
  var choices=await vm.ListTrayImagesAsync();Assert.HasCount(1,choices);var a=await vm.PrepareTrayImageAsync(choices[0],"封面",null);var b=await vm.PrepareTrayImageAsync(choices[0],"情绪板",null);Assert.AreNotEqual(a.DocumentImageId,b.DocumentImageId);Assert.IsTrue(await vm.InsertSimpleImagesAsync([a,b]));Assert.HasCount(2,vm.DocumentImages);
  var rtf=System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(vm.RichTextPackage!));Assert.IsFalse(rtf.Contains("\\pict"));Assert.IsTrue(rtf.Contains("KITAO-IMAGE"));
  await tray.ClearAsync();File.Delete(path);var reopened=Vm();await reopened.LoadAsync(f.Id);Assert.HasCount(2,reopened.DocumentImages);var doc=PlanningDocumentLayout.Load(reopened.RichTextPackage!,reopened.RichLayout);Assert.HasCount(2,PlanningDocumentImages.Images(doc));
  await reopened.SwitchPlanningModeAsync(true);var view=new PlanningCenterView{DataContext=reopened};view.Render();Assert.HasCount(2,PlanningDocumentImages.Images(((StackPanel)view.FindName("DocumentContent")).Children.OfType<PlanningRichEditor>().Single().Children.OfType<RichTextBox>().Single().Document));
  var export=Path.Combine(f.Root,"offline.docx");Assert.IsTrue(await reopened.ExportPlanningDocxAsync(export));using(var pkg=DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(export,false)){Assert.HasCount(2,pkg.MainDocumentPart!.ImageParts);Assert.IsEmpty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(pkg));}
  var review=reopened.PreviewPlanningDocxImport(export);Assert.IsTrue(await reopened.ApplyPlanningDocxImportAsync(review,[],true));Assert.HasCount(2,reopened.DocumentImages);view.DataContext=null;
 });
 [TestMethod]
 public Task HashFailuresCreateNoCopyAndLockedSaveRetainsRecovery()=>RunSta(async()=>
 {
  using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);var path=Path.Combine(f.Root,"source.png");var bytes=PlanningRichFlowFixture.ReferencePng(0);await File.WriteAllBytesAsync(path,bytes);
  var reference=new AssetLibraryStableReference(Guid.NewGuid(),Guid.NewGuid(),new string('a',64));var choice=new PlanningTrayChoice(new(Guid.NewGuid(),reference,DateTimeOffset.UtcNow,0,"bad",null,InspirationTrayResolutionState.Resolved,null),"tray");f.Vm.ResolveTrayImage=r=>Task.FromResult(new AssetResolutionResult(AssetResolutionStatus.Available,r.LibraryId,r.AssetId,path,r.ContentHash));
  await Assert.ThrowsAsync<InvalidDataException>(()=>f.Vm.PrepareTrayImageAsync(choice,"参考图",null));Assert.IsFalse(Directory.Exists(f.Vm.DocumentImageDirectory));
  foreach(var state in new[]{AssetResolutionStatus.Offline,AssetResolutionStatus.Changed,AssetResolutionStatus.Ambiguous,AssetResolutionStatus.Missing}){f.Vm.ResolveTrayImage=r=>Task.FromResult(new AssetResolutionResult(state,r.LibraryId,r.AssetId));await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Vm.PrepareTrayImageAsync(choice,"参考图",null));}
  var meta=PlanningDocumentImages.Copy(f.Vm.DocumentImageDirectory,f.Vm.PlanningDocumentId!.Value,bytes,null,"参考图","one","caption",0);Assert.IsTrue(await f.Vm.InsertSimpleImagesAsync([meta]));var old=await File.ReadAllBytesAsync(Path.Combine(f.Root,"planning",$"{f.Id:N}.planning.json"));
  using(var held=new FileStream(Path.Combine(f.Root,"planning",$"{f.Id:N}.planning.json"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)){Assert.IsFalse(await f.Vm.EditDocumentImageAsync(meta.DocumentImageId,"new","changed"));}
  CollectionAssert.AreEqual(old,await File.ReadAllBytesAsync(Path.Combine(f.Root,"planning",$"{f.Id:N}.planning.json")));Assert.IsTrue(await f.Vm.FlushAsync());var reopened=f.NewVm();await reopened.LoadAsync(f.Id);Assert.AreEqual("new",reopened.DocumentImages.Single().Title);
 });
 [TestMethod]
 public Task CellPlacementImageUndoRedoKeepsSidecarAndPosition()=>RunSta(async()=>
 {
  using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);var doc=new FlowDocument(PlanningR7TableTests.Make(2,2));using var raw=new MemoryStream();new TextRange(doc.ContentStart,doc.ContentEnd).Save(raw,DataFormats.Rtf);
  var editor=new PlanningRichEditor(Convert.ToBase64String(raw.ToArray()),"",f.Vm.SaveRichText,[],PlanningDocumentLayout.Capture(doc,new(),[]));var box=editor.Children.OfType<RichTextBox>().Single();var cell=box.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[1].Cells[1];box.CaretPosition=cell.ContentStart.GetInsertionPosition(LogicalDirection.Forward)!;
  var meta=PlanningDocumentImages.Copy(f.Vm.DocumentImageDirectory,f.Vm.PlanningDocumentId!.Value,PlanningRichFlowFixture.ReferencePng(0),null,"光标","cell","caption",0);editor.InsertOwnedImages([meta],editor.CaptureImageInsertion());editor.FlushPending();Assert.HasCount(1,f.Vm.DocumentImages);Assert.IsTrue(await f.Vm.FlushAsync());
  System.Windows.Input.ApplicationCommands.Undo.Execute(null,editor);Assert.IsEmpty(f.Vm.DocumentImages);System.Windows.Input.ApplicationCommands.Redo.Execute(null,editor);Assert.HasCount(1,f.Vm.DocumentImages);
  var reopened=PlanningDocumentLayout.Load(f.Vm.RichTextPackage!,f.Vm.RichLayout);var restored=reopened.Blocks.OfType<Table>().Single().RowGroups[0].Rows[1].Cells[1];Assert.IsTrue(PlanningTextSearch.Paragraphs(restored.Blocks).SelectMany(p=>PlanningDocumentImages.Inlines(p.Inlines)).OfType<InlineUIContainer>().Any());await Task.CompletedTask;
 });
}
