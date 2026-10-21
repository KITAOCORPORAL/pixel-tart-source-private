using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using W=DocumentFormat.OpenXml.Wordprocessing;
using RAWSelectionAssistant.Views;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class PlanningSummaryExportTests
{
 [TestMethod]public Task AllFifteenSectionsExportOneDocxAndSurviveSaveReopenWithoutMutatingReferences()=>RunSta(async()=>
 {
  EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();var vm=f.Vm;await vm.LoadAsync(f.Id);
  vm.DocumentTitle="合成·15区完整阅读提案";vm.DocumentSubtitle="非便携项目包";vm.DocumentPeople="合成人员";vm.DocumentLocation="合成棚";vm.ShootGoal="目标标记";vm.DocumentCustomBody="文档内容尾标记";
  await vm.NewShotCommand.ExecuteAsync(null);var shot=vm.SelectedShot!.ShotId;vm.StageShot(vm.ShotDraft(shot) with{Name="01开场镜头",Action="转身标记",CameraAndLens="SYNTH85mm",Scene="场景标记",Remarks="镜头备注标记"});Assert.IsTrue(await vm.FlushAsync());
  var external=Path.Combine(f.Root,"external-reference.png");var imageBytes=PlanningRichFlowFixture.ReferencePng(0);await File.WriteAllBytesAsync(external,imageBytes);var missing=Path.Combine(f.Root,"missing-reference.png");
  await vm.AddDocumentReferencesAsync([new(Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:external,Title:"外链存在参考"),new(Guid.NewGuid(),ShotReferenceKind.Lighting,ExternalReference:missing,Title:"外链缺失参考")]);
  var reference=vm.AllProjectReferences.First();await vm.UpdateReferenceAsync(reference,"情绪板");
  var attachment=Path.Combine(f.Root,"attachment.docx");await File.WriteAllTextAsync(attachment,"附件内容只读");await vm.AddAttachmentsAsync([attachment,Path.Combine(f.Root,"missing-attachment.txt")]);
  PlanningResource[] resources=[new PlanningLight{Name="灯位资源标记",ShotIds=[shot],ReferencePath=missing},new PlanningProp{Name="服化道资源标记",ShotIds=[shot]},new PlanningGearItem{Name="设备资源标记",Model="MODEL-A",ShotIds=[shot]},new PlanningCrewMember{Name="人员资源标记",Role="摄影",ShotIds=[shot]},new PlanningVenue{Name="场地资源标记",Address="合成地址",TimeZoneId="UTC",ShotIds=[shot]},new PlanningScheduleSlot{Name="时间表资源标记",TimeZoneId="UTC",ShotIds=[shot]},new PlanningBudgetLine{Name="预算资源标记",Planned=123,Currency="EUR",ShotIds=[shot]}];foreach(var resource in resources)Assert.IsTrue(await vm.SaveResourceAsync(resource));
  var booking=await f.Bookings.SaveAsync(new ShootBookingDraft{Title="关联档期标记",StartAt=DateTimeOffset.UtcNow,EndAt=DateTimeOffset.UtcNow.AddHours(1),TimeZoneId="UTC"});await vm.ShowBookingCommand.ExecuteAsync(null);vm.SelectedBooking=vm.AvailableBookings.Single(b=>b.Id==booking.Booking!.Id);await vm.LinkBookingCommand.ExecuteAsync(null);
  var owned=PlanningDocumentImages.Copy(vm.DocumentImageDirectory,vm.PlanningDocumentId!.Value,imageBytes,null,"光标","正文自有图片","图片图注",0);var rich=new FlowDocument(new Paragraph(new Run("富文本正文标记")));rich.Blocks.Add(new Paragraph(new InlineUIContainer(PlanningDocumentImages.Create(owned))));var table=new Table();var group=new TableRowGroup();table.RowGroups.Add(group);group.Rows.Add(new TableRow{Cells={new TableCell(new Paragraph(new Run("基础表格标记")))}});rich.Blocks.Add(table);
  var layout=PlanningDocumentLayout.Capture(rich,new PlanningPageLayout(),[]);vm.SaveRichText(PlanningDocumentImages.Serialize(rich),new TextRange(rich.ContentStart,rich.ContentEnd).Text,layout);Assert.IsTrue(await vm.FlushAsync());
  var expense=new FinanceTransaction{ProjectId=f.Id,Kind=FinanceTransactionKind.Expense,AmountMinor=4567,CurrencyCode="EUR",Note="实际支出标记"};vm.ReadPlanningExpenses=_=>Task.FromResult<IReadOnlyList<FinanceTransaction>>([expense]);
  var planFile=Path.Combine(f.Root,"planning",$"{f.Id:N}.planning.json");var before=await File.ReadAllBytesAsync(planFile);var sourceBefore=Hash(external);var attachmentBefore=Hash(attachment);var path=Path.Combine(f.Root,"summary.docx");var result=await vm.ExportPlanningSummaryDocxAsync(path);Assert.IsNotNull(result);Assert.AreEqual(15,result.Sections);Assert.AreEqual(0,result.Warnings.Count);
  var text=Read(path);foreach(var token in new[]{"目标标记","转身标记","SYNTH85mm","灯位资源标记","服化道资源标记","设备资源标记","人员资源标记","场地资源标记","时间表资源标记","预算资源标记","关联档期标记","实际支出标记","富文本正文标记","基础表格标记","文档内容尾标记","缺失文件","跨机可能缺失","EUR"})StringAssert.Contains(text,token);
  using(var doc=WordprocessingDocument.Open(path,false)){Assert.IsEmpty(new OpenXmlValidator().Validate(doc).ToArray());Assert.AreEqual(1,doc.MainDocumentPart!.ImageParts.Count());var titles=doc.MainDocumentPart.Document.Body!.Elements<W.Paragraph>().Where(p=>p.ParagraphProperties?.ParagraphStyleId?.Val?.Value=="Heading1").Select(p=>p.InnerText).Where(v=>v.Length>3&&char.IsDigit(v[0])).ToArray();CollectionAssert.AreEqual(PlanningReadableSummaryBuilder.SectionOrder.Select((n,i)=>$"{i+1:00} {n}").ToArray(),titles);}
  CollectionAssert.AreEqual(before,await File.ReadAllBytesAsync(planFile));Assert.AreEqual(sourceBefore,Hash(external));Assert.AreEqual(attachmentBefore,Hash(attachment));
  var reopened=f.NewVm();await reopened.LoadAsync(f.Id);reopened.ReadPlanningExpenses=vm.ReadPlanningExpenses;var afterPath=Path.Combine(f.Root,"reopened.docx");Assert.IsNotNull(await reopened.ExportPlanningSummaryDocxAsync(afterPath));Assert.AreEqual(text,Read(afterPath));
  await Assert.ThrowsAsync<InvalidOperationException>(()=>vm.ExportPlanningSummaryDocxAsync(attachment));
  var evidence=Environment.GetEnvironmentVariable("KITAO_SUMMARY_EVIDENCE");if(!string.IsNullOrWhiteSpace(evidence)){Directory.CreateDirectory(evidence);var imported=PlanningDocxAdapter.Import(path);PlanningProposalPdf.ExportDocument(imported.Document,Path.Combine(evidence,"synthetic-summary-layout-review.pdf"),216);File.Copy(path,Path.Combine(evidence,"synthetic-15-section-summary.docx"));await File.WriteAllTextAsync(Path.Combine(evidence,"summary-content.txt"),text);await File.WriteAllTextAsync(Path.Combine(evidence,"identity.json"),JsonSerializer.Serialize(new{project=f.Id,document=vm.PlanningDocumentId,shot,sections=15,sourceReadOnly=true,saveReopen=true,docxSha256=Hash(path),physicalDesktop="NOT_RUN",scope="Synthetic data; real stores and export components, not WPS"},new JsonSerializerOptions{WriteIndented=true}));}
 });
 [TestMethod]public Task MissingOwnedImageIsDeclaredAtItsPositionAndFailedSaveCannotReplaceDestination()=>RunSta(async()=>
 {
  EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);var bytes=PlanningRichFlowFixture.ReferencePng(1);var meta=PlanningDocumentImages.Copy(f.Vm.DocumentImageDirectory,f.Vm.PlanningDocumentId!.Value,bytes,null,"光标","待缺失图片","图注",0);Assert.IsTrue(await f.Vm.InsertSimpleImagesAsync([meta]));
  var path=Path.Combine(f.Root,"missing.docx");var imagePath=PlanningImageStorage.Resolve(f.Vm.DocumentImages.Single());File.Delete(imagePath);
  var result=await f.Vm.ExportPlanningSummaryDocxAsync(path);Assert.IsNotNull(result);Assert.AreEqual(1,result.Warnings.Count);StringAssert.Contains(Read(path),"文档图片不可用");StringAssert.Contains(Read(path),"待缺失图片");
  var evidence=Environment.GetEnvironmentVariable("KITAO_SUMMARY_EVIDENCE");if(!string.IsNullOrWhiteSpace(evidence)){Directory.CreateDirectory(evidence);File.Copy(path,Path.Combine(evidence,"synthetic-missing-reference-summary.docx"));}
  var old=File.ReadAllBytes(path);f.Vm.CapturePendingEditorDraft=()=>throw new InvalidOperationException("synthetic invalid draft");Assert.IsNull(await f.Vm.ExportPlanningSummaryDocxAsync(path));CollectionAssert.AreEqual(old,File.ReadAllBytes(path));f.Vm.CapturePendingEditorDraft=null;
 });
 [TestMethod]public Task LedgerReadFailureIsExplicitAndUiSummaryActionDoesNotReplaceOldExport()=>RunSta(async()=>
 {
  EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);f.Vm.ReadPlanningExpenses=_=>throw new IOException("synthetic ledger unavailable");var path=Path.Combine(f.Root,"ledger.docx");Assert.IsNotNull(await f.Vm.ExportPlanningSummaryDocxAsync(path));StringAssert.Contains(Read(path),"本次账本读取失败；未使用旧缓存");
  var source=File.ReadAllText(Path.Combine(TestResources.Root,"src/RAWSelectionAssistant/Views/PlanningCenterView.xaml.cs"));StringAssert.Contains(source,"导出15区汇总 DOCX");var old=Path.Combine(f.Root,"legacy.docx");Assert.IsTrue(await f.Vm.ExportPlanningDocxAsync(old));StringAssert.Contains(Read(old),"KitaoShotTableV1:");Assert.IsFalse(Read(path).Contains("KitaoShotTableV1:"));
 });
 [TestMethod]public Task ExportDeclinesAProjectChangeDuringLedgerReadAndKeepsExistingFile()=>RunSta(async()=>
 {
  EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);var path=Path.Combine(f.Root,"existing.docx");await File.WriteAllTextAsync(path,"existing unchanged");
  f.Vm.ReadPlanningExpenses=_=>{f.Vm.DocumentTitle="changed during read";return Task.FromResult<IReadOnlyList<FinanceTransaction>>([]);};
  await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Vm.ExportPlanningSummaryDocxAsync(path));Assert.AreEqual("existing unchanged",await File.ReadAllTextAsync(path));
 });
 [TestMethod]public Task EmptySectionsAndUiSavePathSuccessCancelAndFailureAreExplicit()=>RunSta(async()=>
 {
  EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();var dialogs=new SummaryDialogs();var vm=new PlanningCenterViewModel(f.Projects,f.Bookings,dialogs,f.Shots,f.Planning);await vm.LoadAsync(f.Id);
  var view=new PlanningCenterView{DataContext=vm};view.Measure(new Size(1400,900));view.Arrange(new Rect(0,0,1400,900));view.UpdateLayout();
  var method=typeof(PlanningCenterView).GetMethod("ExportPlanningSummaryFromViewAsync",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
  dialogs.Path=Path.Combine(f.Root,"ui-summary.docx");await (Task)method.Invoke(view,null)!;Assert.IsTrue(File.Exists(dialogs.Path));Assert.AreEqual(1,dialogs.Infos.Count);Assert.IsTrue(dialogs.LastTitle.Contains("15区汇总"));StringAssert.Contains(Read(dialogs.Path),"本区未填写内容。");
  using(var pkg=WordprocessingDocument.Open(dialogs.Path,false))Assert.AreEqual(15,pkg.MainDocumentPart!.Document.Body!.Elements<W.Paragraph>().Count(p=>p.ParagraphProperties?.ParagraphStyleId?.Val?.Value=="Heading1"));
  var original=File.ReadAllBytes(dialogs.Path);dialogs.Path=null;await (Task)method.Invoke(view,null)!;Assert.AreEqual(1,dialogs.Infos.Count);Assert.IsEmpty(dialogs.Errors);
  dialogs.Path=Path.Combine(f.Root,"no-such-directory","failure.docx");await (Task)method.Invoke(view,null)!;Assert.AreEqual(1,dialogs.Errors.Count);Assert.IsFalse(File.Exists(dialogs.Path));CollectionAssert.AreEqual(original,File.ReadAllBytes(Path.Combine(f.Root,"ui-summary.docx")));
  vm.CapturePendingEditorDraft=()=>throw new InvalidOperationException("synthetic invalid editor");await (Task)method.Invoke(view,null)!;Assert.AreEqual(2,dialogs.Errors.Count);vm.CapturePendingEditorDraft=null;view.DataContext=null;
 });
 sealed class SummaryDialogs:IDialogService
 {
  public string? Path;public string LastTitle="";public List<string> Infos=[];public List<string> Errors=[];
  public string? ChooseSaveFile(string title,string filter,string extension,string? suggestedFileName=null){LastTitle=title;return Path;}
  public IReadOnlyList<string> ChooseFiles(string t,string f,bool multiselect=true)=>[];public string? ChooseFolder(string t,string? initialDirectory=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;
  public void ShowInfo(string message)=>Infos.Add(message);public void ShowError(string message)=>Errors.Add(message);public bool Confirm(string m,string t)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool a)=>false;public void RevealFile(string p){}
 }
 static string Read(string path){using var doc=WordprocessingDocument.Open(path,false);return string.Join("\n",doc.MainDocumentPart!.Document.Body!.Descendants<W.Text>().Select(t=>t.Text));}
 static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
