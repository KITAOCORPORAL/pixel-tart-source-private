using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class PlanningReadableSummaryTests
{
 [TestMethod]public void FifteenSectionsKeepStoredOrderFieldsReferencesAndDoNotMutateInput()
 {
  using var temp=new TempDirectory();var project=Guid.NewGuid();var shotId=Guid.NewGuid();var reference=Guid.NewGuid();var existing=temp.CreateFile("external.txt",[1,2,3]);var missing=temp.Combine("missing.png");
  var document=new PlanningDocument{Title="合成标题",Body="正文标记",CustomBody="补充标记",References=[new(new(reference,ShotReferenceKind.General,ExternalReference:missing,Title:"缺失参考"),IsMoodboard:true)],Attachments=[new(Guid.NewGuid(),existing,"附件存在"),new(Guid.NewGuid(),missing,"附件缺失")],Resources=new(){Items=[new PlanningProp{Name="道具后",ShotIds=[shotId]},new PlanningProp{Name="道具先",Archived=true},new PlanningLight{Name="主灯",ReferencePath=missing},new PlanningGearItem{Name="设备"},new PlanningCrewMember{Name="人员"},new PlanningVenue{Name="场地"},new PlanningScheduleSlot{Name="时间表"},new PlanningBudgetLine{Name="预算",Planned=123,Currency="EUR"}]}}.EnsureIdentity(project);
  var now=DateTimeOffset.UtcNow;var shots=new[]{new ProjectShot(shotId,project,0,"镜头先",ProjectShotStatus.NotStarted,"旧备注",null,[],now,now){Action="动作标记",CustomValues=new(){["服装"]="LOOK-A"}}};
  var input=new PlanningSummaryInput(project,"合成项目",document,new(ShootGoal:"拍摄目标"),shots,new ShootBooking{ProjectId=project,Title="档期标记",StartAtUtc=now,EndAtUtc=now.AddHours(2)},[new FinanceTransaction{ProjectId=project,Kind=FinanceTransactionKind.Expense,AmountMinor=2345,CurrencyCode="EUR"}],"合成账本读取成功");
  input=input with{Expenses=input.Expenses!.Concat([new FinanceTransaction{ProjectId=Guid.NewGuid(),Kind=FinanceTransactionKind.Expense,Note="FOREIGN-EXPENSE",AttachmentPaths=["FOREIGN-ATTACHMENT"]}]).ToArray()};
  var original=JsonSerializer.Serialize(input);var bytes=File.ReadAllBytes(existing);var result=PlanningReadableSummaryBuilder.Build(input);
  Assert.AreEqual(15,result.Sections.Count);CollectionAssert.AreEqual(PlanningReadableSummaryBuilder.SectionOrder.ToArray(),result.Sections.Select(s=>s.Name).ToArray());
  CollectionAssert.AreEqual(new[]{"道具后","道具先"},result.Sections[6].Entries.Select(e=>e.Title).ToArray());
  var text=JsonSerializer.Serialize(result);foreach(var token in new[]{"正文标记","补充标记","动作标记","LOOK-A","EUR","2345","缺失文件","跨机可能缺失","档期标记"})StringAssert.Contains(System.Text.RegularExpressions.Regex.Unescape(text),token);
  Assert.IsFalse(text.Contains("FOREIGN-EXPENSE"));Assert.IsFalse(text.Contains("FOREIGN-ATTACHMENT"));
  Assert.AreEqual(original,JsonSerializer.Serialize(input));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(existing));Assert.IsFalse(File.Exists(missing));
 }
 [TestMethod]public void SummaryUsesShotOrderAndCurrentSectionReferenceClassification()
 {
  var project=Guid.NewGuid();var now=DateTimeOffset.UtcNow;var light=new ProjectShotReference(Guid.NewGuid(),ShotReferenceKind.Lighting,ExternalReference:Path.GetFullPath("missing-light.png"),Title:"LIGHT-REFERENCE");var styling=new ProjectShotReference(Guid.NewGuid(),ShotReferenceKind.Styling,ExternalReference:Path.GetFullPath("missing-style.png"),Title:"STYLING-REFERENCE");
  var document=new PlanningDocument{References=[new(light,"灯光"),new(styling,"造型")]}.EnsureIdentity(project);
  var late=new ProjectShot(Guid.NewGuid(),project,9,"late",ProjectShotStatus.NotStarted,null,null,[new ProjectShotReference(Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:Path.GetFullPath("shot-only.png"),Title:"SHOT-ONLY")],now,now);var early=late with{ShotId=Guid.NewGuid(),Order=0,Name="early",References=[]};
  var input=new PlanningSummaryInput(project,"test",document,new(),[late,early]);var result=PlanningReadableSummaryBuilder.Build(input);
  CollectionAssert.AreEqual(new[]{"early","late","SHOT-ONLY"},result.Sections[2].Entries.Select(x=>x.Title).ToArray());
  Assert.AreEqual("LIGHT-REFERENCE",result.Sections[5].Entries.Single().Title);Assert.AreEqual("STYLING-REFERENCE",result.Sections[6].Entries.Single().Title);Assert.AreEqual(3,result.Sections[3].Entries.Count);Assert.AreEqual("SHOT-ONLY",result.Sections[3].Entries.Last().Title);
  Assert.AreEqual("late",input.Shots[0].Name,"Export must not reorder saved collection");
 }
 [TestMethod]public void ForeignProjectDocumentOrShotFailsClosed()
 {
  var project=Guid.NewGuid();var document=new PlanningDocument().EnsureIdentity(Guid.NewGuid());Assert.ThrowsExactly<InvalidDataException>(()=>PlanningReadableSummaryBuilder.Build(new(project,"test",document,new(),[])));
  var now=DateTimeOffset.UtcNow;var foreign=new ProjectShot(Guid.NewGuid(),Guid.NewGuid(),0,"foreign",ProjectShotStatus.NotStarted,null,null,[],now,now);
  Assert.ThrowsExactly<InvalidDataException>(()=>PlanningReadableSummaryBuilder.Build(new(project,"test",new PlanningDocument().EnsureIdentity(project),new(),[foreign])));
 }
 [TestMethod]public void ReferenceStatusNeverFetchesUrlsOrInventsPortableFiles()
 {
  Assert.AreEqual("外部URL；未访问、未打包",PlanningReadableSummaryBuilder.ReferenceState("https://example.invalid/private.png"));
  Assert.AreEqual("相对外链；未解析、未打包",PlanningReadableSummaryBuilder.ReferenceState("reference.png"));
  Assert.AreEqual("未提供路径",PlanningReadableSummaryBuilder.ReferenceState(null));
 }
}
