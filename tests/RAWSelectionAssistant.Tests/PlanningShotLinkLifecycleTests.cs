using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]public sealed class PlanningShotLinkLifecycleTests
{
    static string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
    sealed class F:IDisposable
    {
        public TempDirectory T=new();public Guid Id=Guid.NewGuid();public PlanningProjectStore P;public ProjectShotStore S;public ProjectShot A=null!,B=null!;public PlanningExternalFile Selected=null!;
        public string Plan=>Path.Combine(P.StorageRoot,Id.ToString("N")+".planning.json");public string Shot=>Path.Combine(S.StorageRoot,Id.ToString("N")+".shots.json");
        public F(){P=new(T.Combine("planning"));S=new(T.Combine("shots"));}
        public static async Task<F> Create()
        {
            var f=new F();var reference=new ProjectShotReference(Guid.NewGuid(),ShotReferenceKind.Pose,ExternalReference:f.T.Combine("old.png"));var now=DateTimeOffset.UtcNow;f.A=new(Guid.NewGuid(),f.Id,0,"A",ProjectShotStatus.NotStarted,null,null,[reference],now,now);f.B=f.A with{ShotId=Guid.NewGuid(),Order=1,Name="B"};await f.S.SaveCatalogAsync(new(f.Id,[f.A,f.B]));await f.P.UpdateDocumentAsync(f.Id,new(){Title="旧策划正文不改"},new());var file=f.T.CreateFile("selected.png",[137,80,78,71,13,10,26,10,1]);f.Selected=await PlanningExternalFile.InspectAsync(reference.ReferenceId,"reference",reference.ExternalReference!,file);await f.P.ReconnectExternalAsync(f.Id,f.Selected,true,f.S);return f;
        }
        public void Dispose()=>T.Dispose();
    }
    [TestMethod]public async Task EveryShotWriteApiPrunesOnlyInvalidRecipientsKeepsSharedCopyAndPackageRoundtrips()
    {
        foreach(var mode in new[]{"single","batch","catalog","remove"})
        {
            using var f=await F.Create();f.S.BindPlanningLifecycle(f.P);var before=(await f.P.LoadAsync(f.Id)).Document!;var copy=before.ShotExternalLinks.Single().ProjectCopy!;var path=PlanningProjectCopyStorage.Resolve(copy);var hash=Hash(path);
            if(mode=="single")await f.S.SaveAsync(f.A with{References=[]});
            if(mode=="batch")await f.S.SaveBatchAsync(f.Id,[f.A with{References=[f.A.References[0] with{ExternalReference=f.T.Combine("other.png")}]}]);
            if(mode=="catalog")await f.S.SaveCatalogAsync(new(f.Id,[f.B]));
            if(mode=="remove")await f.S.RemoveAsync(f.Id,f.A.ShotId);
            var doc=(await f.P.LoadAsync(f.Id)).Document!;CollectionAssert.AreEqual(new[]{f.B.ShotId},doc.ShotExternalLinks.Single().ShotIds.ToArray());Assert.AreEqual(hash,Hash(path));
            var package=f.T.Combine("changed.ptplan");await PlanningProjectPackage.ExportAsync(package,new(){Id=f.Id,Name="shared"},await f.P.LoadAsync(f.Id),await f.S.LoadAsync(f.Id),f.P.StorageRoot);PlanningProjectPackage.Validate(package);
            await f.S.RemoveAsync(f.Id,f.B.ShotId);Assert.IsEmpty((await f.P.LoadAsync(f.Id)).Document!.ShotExternalLinks);Assert.IsTrue(File.Exists(path));Assert.AreEqual(hash,Hash(path));
        }
    }
    [TestMethod]public async Task LibraryIdentityChangeRevokesOnlyEditedRecipientAndFailedShotReplacementRestoresReceiptBytes()
    {
        using var f=await F.Create();f.S.BindPlanningLifecycle(f.P);var plan=Hash(f.Plan);var shot=Hash(f.Shot);
        using(var held=new FileStream(f.Shot,FileMode.Open,FileAccess.Read,FileShare.Read))
        {Exception? error=null;try{await f.S.SaveAsync(f.A with{References=[]});}catch(Exception e)when(e is IOException or UnauthorizedAccessException){error=e;}Assert.IsNotNull(error);}
        Assert.AreEqual(plan,Hash(f.Plan));Assert.AreEqual(shot,Hash(f.Shot));
        await f.S.SaveAsync(f.A with{References=[f.A.References[0] with{LibraryId=Guid.NewGuid(),AssetId=Guid.NewGuid(),ExternalReference=null}]});Assert.HasCount(1,(await f.P.LoadAsync(f.Id)).Document!.ShotExternalLinks.Single().ShotIds);
    }
    [TestMethod]public async Task LegacyMismatchIsNotSilentlyFixedAndExplicitRemovalKeepsSharedReceiptBackupsAndCopies()
    {
        using var f=await F.Create();var original=(await f.P.LoadAsync(f.Id)).Document!;var copy=PlanningProjectCopyStorage.Resolve(original.ShotExternalLinks.Single().ProjectCopy!);var copyHash=Hash(copy);
        // Unbound external writer simulates a pre-existing legacy mismatch.
        await f.S.SaveAsync(f.A with{References=[]});var doc=(await f.P.LoadAsync(f.Id)).Document!;var issue=PlanningShotLinkLifecycle.Inspect(doc,await f.S.LoadAsync(f.Id)).Single();Assert.AreEqual(f.A.ShotId,issue.ShotId);var before=Hash(f.Plan);
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();await Assert.ThrowsAsync<OperationCanceledException>(()=>f.P.RepairShotLinkAsync(f.Id,f.S,doc.ShotExternalLinks.Single(),issue.ShotId,token:cancel.Token));}Assert.AreEqual(before,Hash(f.Plan));
        using(var held=new FileStream(f.Plan,FileMode.Open,FileAccess.Read,FileShare.Read)){Exception? error=null;try{await f.P.RepairShotLinkAsync(f.Id,f.S,doc.ShotExternalLinks.Single(),issue.ShotId);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){error=e;}Assert.IsNotNull(error);}Assert.AreEqual(before,Hash(f.Plan));
        await f.P.RepairShotLinkAsync(f.Id,f.S,doc.ShotExternalLinks.Single(),issue.ShotId);var fixedDoc=(await f.P.LoadAsync(f.Id)).Document!;Assert.IsEmpty(PlanningShotLinkLifecycle.Inspect(fixedDoc,await f.S.LoadAsync(f.Id)));CollectionAssert.AreEqual(new[]{f.B.ShotId},fixedDoc.ShotExternalLinks.Single().ShotIds.ToArray());Assert.AreEqual(copyHash,Hash(copy));Assert.IsTrue(Directory.GetFiles(f.P.StorageRoot,"*.before-shot-link-repair").Any());
        var package=f.T.Combine("repaired.ptplan");await PlanningProjectPackage.ExportAsync(package,new(){Id=f.Id,Name="repaired"},await f.P.LoadAsync(f.Id),await f.S.LoadAsync(f.Id),f.P.StorageRoot);await PlanningProjectPackage.ImportAsync(package,f.T.Combine("other"));
    }
    [TestMethod]public async Task ExplicitRelocateRequiresCurrentUniqueSourceAndRejectsChangedConfirmationWithoutReplacingOldReceipt()
    {
        using var f=await F.Create();await f.S.RemoveAsync(f.Id,f.B.ShotId);var changed=f.A.References[0] with{ExternalReference=f.T.Combine("new-original.png")};await f.S.SaveAsync(f.A with{References=[changed]});var doc=(await f.P.LoadAsync(f.Id)).Document!;var expected=doc.ShotExternalLinks.Single();var replacement=f.T.CreateFile("new-selected.png",[137,80,78,71,13,10,26,10,2]);var selected=await PlanningExternalFile.InspectAsync(changed.ReferenceId,"reference",changed.ExternalReference!,replacement);var before=Hash(f.Plan);File.WriteAllBytes(replacement,[137,80,78,71,13,10,26,10,3]);await Assert.ThrowsAsync<InvalidDataException>(()=>f.P.RepairShotLinkAsync(f.Id,f.S,expected,f.A.ShotId,selected,true));Assert.AreEqual(before,Hash(f.Plan));
        selected=await PlanningExternalFile.InspectAsync(changed.ReferenceId,"reference",changed.ExternalReference!,replacement);await f.P.RepairShotLinkAsync(f.Id,f.S,expected,f.A.ShotId,selected,true);var repaired=(await f.P.LoadAsync(f.Id)).Document!;Assert.AreEqual(changed.ExternalReference,repaired.ShotExternalLinks.Single().OriginalPath);Assert.AreEqual(selected.Sha256,Hash(PlanningProjectCopyStorage.Resolve(repaired.ShotExternalLinks.Single().ProjectCopy!)));Assert.IsTrue(File.Exists(PlanningProjectCopyStorage.Resolve(expected.ProjectCopy!)));
    }
}
