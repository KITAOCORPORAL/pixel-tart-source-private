using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class PlanningBatch3ServiceTests
{
    [TestMethod]
    public async Task ProductionStoresKeepCrossSectionIdsCancellationAndMovedRoot()
    {
        var parent=Environment.GetEnvironmentVariable("KITAO_PLANNING3_OUTPUT")??Path.GetTempPath();
        var root=Path.Combine(parent,"service-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var project=Guid.NewGuid();var shotId=Guid.NewGuid();var referenceId=Guid.NewGuid();var now=DateTimeOffset.UtcNow;
        var original=Path.Combine(root,"original-reference.txt");await File.WriteAllTextAsync(original,"SYNTHETIC ORIGINAL - NEVER MODIFY");var originalHash=Hash(original);
        var shots=new ProjectShotStore(Path.Combine(root,"shots"));var store=new PlanningProjectStore(Path.Combine(root,"planning"));
        var reference=new ProjectShotReference(referenceId,ShotReferenceKind.General,ExternalReference:original,Title:"合成引用");
        var shot=new ProjectShot(shotId,project,0,"合成镜头",ProjectShotStatus.NotStarted,null,null,[reference],now,now){Action="转身",Scene="合成摄影棚"};
        await shots.SaveAsync(shot);
        PlanningResource[] resources=[new PlanningLight{Name="主灯",ShotIds=[shotId]},new PlanningProp{Name="白纱",ShotIds=[shotId]},new PlanningGearItem{Name="手动相机",Model="Synthetic",ShotIds=[shotId]},new PlanningCrewMember{Name="合成助理",ShotIds=[shotId]},new PlanningVenue{Name="合成场地",TimeZoneId="Asia/Shanghai"},new PlanningScheduleSlot{Name="开场镜头",Date=new(2026,10,21),TimeZoneId="Asia/Shanghai",ShotIds=[shotId]},new PlanningBudgetLine{Name="道具预算",Planned=500,Currency="CNY",ShotIds=[shotId]}];
        var document=new PlanningDocument{Title="批次3服务闭环",Body="合成正文",CustomBody="补充执行事项",Location="合成棚",People="合成团队",References=[new(reference,IsHero:true,IsMoodboard:true)],Resources=new(){Items=resources},Attachments=[new(Guid.NewGuid(),original,"合成附件")]}.EnsureIdentity(project);
        await store.UpdateDocumentAsync(project,document,new("人像方案",["柔光"],"合成客户","开场","不得丢失","审阅"));var documentId=(await store.LoadAsync(project)).Document!.Package!.DocumentId;
        var booking=Guid.NewGuid();await store.LinkBookingAsync(project,booking);
        var edited=resources.Select(r=>r with{Name=r.Name+" · 已编辑"}).ToArray();await store.UpdateDocumentAsync(project,document with{Resources=new(){Items=edited},Body="合成正文已编辑"},new());
        var clone=edited[1] with{Id=Guid.NewGuid(),Name="白纱副本"};var catalog=new PlanningResources{Items=edited}.Upsert(clone);await store.UpdateDocumentAsync(project,document with{Resources=catalog},new());
        var archived=catalog.Upsert(clone with{Archived=true});await store.UpdateDocumentAsync(project,document with{Resources=archived},new());
        var path=Path.Combine(root,"planning",$"{project:N}.planning.json");var before=await File.ReadAllBytesAsync(path);
        using(var c=new CancellationTokenSource()){c.Cancel();await Assert.ThrowsAsync<OperationCanceledException>(()=>store.UpdateDocumentAsync(project,document,new(),c.Token));}CollectionAssert.AreEqual(before,await File.ReadAllBytesAsync(path));
        await Assert.ThrowsAsync<InvalidDataException>(()=>store.UpdateDocumentAsync(project,document with{Resources=new(){Items=[new PlanningScheduleSlot{Name="跨日错误",StartLocal="23:00",EndLocal="01:00"}]}},new()));CollectionAssert.AreEqual(before,await File.ReadAllBytesAsync(path));
        // Physical relocation of only this owned synthetic storage; no source asset moves.
        var moved=Path.Combine(root,"relocated");Directory.CreateDirectory(moved);Directory.Move(Path.Combine(root,"planning"),Path.Combine(moved,"planning"));Directory.Move(Path.Combine(root,"shots"),Path.Combine(moved,"shots"));
        var restored=await new PlanningProjectStore(Path.Combine(moved,"planning")).LoadAsync(project);var restoredShots=await new ProjectShotStore(Path.Combine(moved,"shots")).LoadAsync(project);
        Assert.AreEqual(documentId,restored.Document!.Package!.DocumentId);Assert.AreEqual(booking,restored.BookingId);Assert.HasCount(8,restored.Document.Resources.Items);Assert.IsTrue(restored.Document.Resources.Items.Single(r=>r.Id==clone.Id).Archived);
        foreach(var r in restored.Document.Resources.Items.Where(r=>r.ShotIds.Length>0))Assert.AreEqual(shotId,r.ShotIds.Single());Assert.AreEqual(referenceId,restoredShots.Shots.Single().References.Single().ReferenceId);Assert.AreEqual(originalHash,Hash(original));
        await File.WriteAllTextAsync(Path.Combine(root,"RESULT.json"),JsonSerializer.Serialize(new{project,documentId,shotId,referenceId,bookingId=booking,resources=restored.Document.Resources.Items.Select(r=>new{r.Id,r.Name,r.ShotIds,r.Archived}),created=true,edited=true,linked=true,cancelSafe=true,invalidScheduleRejected=true,movedRootReopen=true,sourceOriginalHash=originalHash,sourceUnchanged=true,scope="Actual production JSON stores; not WPF, physical mouse, DOCX or full app DB migration"},new JsonSerializerOptions{WriteIndented=true}));
    }
    static string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
}
