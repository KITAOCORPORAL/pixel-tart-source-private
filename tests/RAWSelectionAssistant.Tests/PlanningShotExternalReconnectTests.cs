using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]public sealed class PlanningShotExternalReconnectTests
{
    static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    static ProjectShot Shot(Guid project,ProjectShotReference reference,int order=7) => new(Guid.NewGuid(),project,order,"原镜头",ProjectShotStatus.NotStarted,"原备注",null,[reference],DateTimeOffset.UtcNow,DateTimeOffset.UtcNow);
    sealed class Fixture:IDisposable
    {
        public TempDirectory Temp=new();public Guid Id=Guid.NewGuid();public PlanningProjectStore Planning;public ProjectShotStore Shots;public ProjectShot Shot=null!;public PlanningExternalFile Selected=null!;
        public string PlanPath=>Path.Combine(Planning.StorageRoot,Id.ToString("N")+".planning.json");public string ShotPath=>Path.Combine(Shots.StorageRoot,Id.ToString("N")+".shots.json");
        public Fixture(){Planning=new(Temp.Combine("planning"));Shots=new(Temp.Combine("shots"));}
        public static async Task<Fixture> Create(bool shared=false)
        {
            var f=new Fixture();var original=f.Temp.Combine("old/missing.png");var reference=new ProjectShotReference(Guid.NewGuid(),ShotReferenceKind.Pose,ExternalReference:original,Title:"原图",Note:"保持备注");f.Shot=PlanningShotExternalReconnectTests.Shot(f.Id,reference);await f.Shots.SaveAsync(f.Shot);
            await f.Planning.UpdateDocumentAsync(f.Id,new(){Title="原策划",CustomBody="正文保持",References=shared?[new(reference,"姿势",true,true,"原分区")]:[]},new());
            var image=f.Temp.CreateFile("selected.png",new byte[]{137,80,78,71,13,10,26,10,1});f.Selected=await PlanningExternalFile.InspectAsync(reference.ReferenceId,"reference",original,image);return f;
        }
        public void Dispose()=>Temp.Dispose();
    }
    [TestMethod]public async Task ShotOnlyExternalAndOwnedCopyPersistWithoutChangingShotBytesOrDocumentReferences()
    {
        foreach(var owned in new[]{false,true})
        {
            using var f=await Fixture.Create();var before=Hash(f.ShotPath);await f.Planning.ReconnectExternalAsync(f.Id,f.Selected,owned,f.Shots,[f.Shot.ShotId]);var doc=(await new PlanningProjectStore(f.Planning.StorageRoot).LoadAsync(f.Id)).Document!;
            Assert.AreEqual(before,Hash(f.ShotPath));Assert.IsEmpty(doc.References);Assert.AreEqual("原策划",doc.Title);Assert.AreEqual("正文保持",doc.CustomBody);var link=doc.ShotExternalLinks.Single();Assert.AreEqual(f.Selected.ReferenceId,link.ReferenceId);CollectionAssert.AreEqual(new[]{f.Shot.ShotId},link.ShotIds.ToArray());Assert.AreEqual(f.Selected.OriginalPath,link.OriginalPath);
            var projected=PlanningReferenceLinks.ForShot(doc,f.Shot,f.Shot.References.Single());if(owned)Assert.AreEqual(f.Selected.Sha256,Hash(PlanningProjectCopyStorage.Resolve(projected.ProjectCopy!)));else Assert.AreEqual(f.Selected.SelectedPath,PlanningExternalFile.ResolveVerified(projected.ReconnectedFile!,f.Selected.ReferenceId,"reference",f.Selected.OriginalPath));
        }
    }
    [TestMethod]public async Task SharedDocumentAndTwoShotIdsUseOneApprovalWithoutRewritingEitherShot()
    {
        using var f=await Fixture.Create(true);var second=Shot(f.Id,f.Shot.References.Single(),9);await f.Shots.SaveAsync(second);var before=Hash(f.ShotPath);await f.Planning.ReconnectExternalAsync(f.Id,f.Selected,true,f.Shots,[f.Shot.ShotId,second.ShotId]);var doc=(await f.Planning.LoadAsync(f.Id)).Document!;Assert.IsEmpty(doc.ShotExternalLinks);Assert.AreEqual("原分区",doc.References.Single().Group);Assert.IsTrue(doc.References.Single().IsHero);Assert.AreEqual(doc.References.Single().ProjectCopy,PlanningReferenceLinks.ForShot(doc,second,second.References.Single()).ProjectCopy);Assert.AreEqual(before,Hash(f.ShotPath));
        var plan=Hash(f.PlanPath);await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>f.Planning.ReconnectExternalAsync(f.Id,f.Selected,false,f.Shots));Assert.AreEqual(plan,Hash(f.PlanPath));
    }
    [TestMethod]public async Task SameIdDifferentSourceOrChangedShotSetRejectsBeforeWrites()
    {
        using var f=await Fixture.Create(true);var original=f.Shot.References.Single();var conflict=f.Shot with{References=[original with{ExternalReference=f.Temp.Combine("other.png")}]};await f.Shots.SaveAsync(conflict);var before=Hash(f.PlanPath);await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>f.Planning.ReconnectExternalAsync(f.Id,f.Selected,true,f.Shots));Assert.AreEqual(before,Hash(f.PlanPath));
        await f.Shots.SaveAsync(f.Shot);await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>f.Planning.ReconnectExternalAsync(f.Id,f.Selected,true,f.Shots,[]));Assert.AreEqual(before,Hash(f.PlanPath));
    }
    [TestMethod]public async Task RepeatedExternalApprovalChangedCandidateAndCancellationPreserveExistingState()
    {
        using var f=await Fixture.Create();await f.Planning.ReconnectExternalAsync(f.Id,f.Selected,false,f.Shots);await f.Planning.ReconnectExternalAsync(f.Id,f.Selected,false,f.Shots);Assert.HasCount(1,(await f.Planning.LoadAsync(f.Id)).Document!.ShotExternalLinks);var before=Hash(f.PlanPath);var shots=Hash(f.ShotPath);
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();await Assert.ThrowsAsync<OperationCanceledException>(()=>f.Planning.ReconnectExternalAsync(f.Id,f.Selected,true,f.Shots,token:cancel.Token));}
        await File.WriteAllBytesAsync(f.Selected.SelectedPath,new byte[]{137,80,78,71,13,10,26,10,2});await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>f.Planning.ReconnectExternalAsync(f.Id,f.Selected,true,f.Shots));Assert.AreEqual(before,Hash(f.PlanPath));Assert.AreEqual(shots,Hash(f.ShotPath));
    }
    [TestMethod]public async Task SaveFailureRollsBackOnlyNewCopyAndRetainsPriorReceiptAndUnrelatedCopy()
    {
        using var f=await Fixture.Create();await f.Planning.ReconnectExternalAsync(f.Id,f.Selected,false,f.Shots);var original=f.Temp.CreateFile("attachment.txt","existing"u8.ToArray());await f.Planning.ImportProjectCopiesAsync(f.Id,[],[new(Guid.NewGuid(),original,"旧副本")]);var doc=(await f.Planning.LoadAsync(f.Id)).Document!;var old=doc.Attachments.Single().ProjectCopy!;var oldPath=PlanningProjectCopyStorage.Resolve(old);var oldHash=Hash(oldPath);var plan=Hash(f.PlanPath);var shots=Hash(f.ShotPath);var copies=Directory.GetFiles(Path.Combine(f.Planning.StorageRoot,"project-copies"),"*",SearchOption.AllDirectories).Where(p=>!p.Contains(".staging")).ToArray();
        using(var held=new FileStream(f.PlanPath,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            Exception? failure=null;try{await f.Planning.ReconnectExternalAsync(f.Id,f.Selected,true,f.Shots);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){failure=e;}
            Assert.IsNotNull(failure,"Held metadata must prevent commit; platform may report sharing violation or access denied.");
        }
        Assert.AreEqual(plan,Hash(f.PlanPath));Assert.AreEqual(shots,Hash(f.ShotPath));Assert.AreEqual(oldHash,Hash(oldPath));CollectionAssert.AreEquivalent(copies,Directory.GetFiles(Path.Combine(f.Planning.StorageRoot,"project-copies"),"*",SearchOption.AllDirectories).Where(p=>!p.Contains(".staging")).ToArray());
    }
    [TestMethod]public async Task ReparseSelectedFileIsNeverFollowed()
    {
        using var f=await Fixture.Create();var alias=f.Temp.Combine("alias.png");
        try{File.CreateSymbolicLink(alias,f.Selected.SelectedPath);}
        catch(UnauthorizedAccessException){Assert.Inconclusive("Host cannot create synthetic symlink; no policy change.");return;}
        // Windows may report missing privilege as IOException; retain that environment failure.
        await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>PlanningExternalFile.InspectAsync(f.Selected.ReferenceId,"reference",f.Selected.OriginalPath,alias));
    }
    [TestMethod]public async Task PackageRoundtripRetainsShotCopyButDropsMachineExternalApproval()
    {
        foreach(var owned in new[]{false,true})
        {
            using var f=await Fixture.Create();await f.Planning.ReconnectExternalAsync(f.Id,f.Selected,owned,f.Shots);var package=f.Temp.Combine("test.ptplan");var state=await f.Planning.LoadAsync(f.Id);await PlanningProjectPackage.ExportAsync(package,new PhotoProjectRecord{Id=f.Id,Name="镜头合成"},state,await f.Shots.LoadAsync(f.Id),f.Planning.StorageRoot);var imported=await PlanningProjectPackage.ImportAsync(package,f.Temp.Combine("destination"));var doc=(await new PlanningProjectStore(Path.Combine(imported.DataRoot,"planning")).LoadAsync(f.Id)).Document!;
            Assert.IsEmpty(doc.References);if(owned){var receipt=doc.ShotExternalLinks.Single();Assert.IsNull(receipt.ReconnectedFile);Assert.AreEqual(f.Selected.Sha256,Hash(PlanningProjectCopyStorage.Resolve(receipt.ProjectCopy!)));}else Assert.IsEmpty(doc.ShotExternalLinks);
        }
    }
}
