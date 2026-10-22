using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]public sealed class PlanningExternalReconnectTests
{
    static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    [TestMethod]public async Task ExplicitReconnectKeepsReferencePartitionAndOriginalAndPersistsCopy()
    {
        using var t=new TempDirectory();var store=new PlanningProjectStore(t.Combine("planning"));var project=Guid.NewGuid();var id=Guid.NewGuid();var original=t.Combine("missing/old.png");var selected=t.CreateFile("selected/new.png",new byte[]{137,80,78,71,13,10,26,10,1});
        var reference=new PlanningDocumentReference(new(id,ShotReferenceKind.Lighting,ExternalReference:original),"灯光",true,true,"侧光");await store.UpdateDocumentAsync(project,new(){References=[reference]},new());
        var file=await PlanningExternalFile.InspectAsync(id,"reference",original,selected);await store.ReconnectExternalAsync(project,file,true);
        var saved=(await new PlanningProjectStore(store.StorageRoot).LoadAsync(project)).Document!.References.Single();Assert.AreEqual(reference.Source,saved.Source);Assert.AreEqual(reference.Category,saved.Category);Assert.AreEqual(reference.Group,saved.Group);Assert.AreEqual(reference.IsHero,saved.IsHero);Assert.AreEqual(reference.IsMoodboard,saved.IsMoodboard);Assert.AreEqual(original,saved.ProjectCopy!.OriginalPath);Assert.AreEqual(Hash(selected),Hash(PlanningProjectCopyStorage.Resolve(saved.ProjectCopy)));
        var before=Hash(Path.Combine(store.StorageRoot,project.ToString("N")+".planning.json"));await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>store.ReconnectExternalAsync(project,file,false));Assert.AreEqual(before,Hash(Path.Combine(store.StorageRoot,project.ToString("N")+".planning.json")));
    }
    [TestMethod]public async Task ChangedCandidateCancellationWrongTypeAndIdentityConflictAreNonDestructive()
    {
        using var t=new TempDirectory();var store=new PlanningProjectStore(t.Combine("planning"));var project=Guid.NewGuid();var id=Guid.NewGuid();var original=t.Combine("gone.txt");var selected=t.CreateFile("new.txt","first"u8.ToArray());
        await store.UpdateDocumentAsync(project,new(){Attachments=[new(id,original,"通告单")]},new());var path=Path.Combine(store.StorageRoot,project.ToString("N")+".planning.json");var before=Hash(path);var file=await PlanningExternalFile.InspectAsync(id,"attachment",original,selected);
        await File.WriteAllTextAsync(selected,"second");await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>store.ReconnectExternalAsync(project,file,true));Assert.AreEqual(before,Hash(path));
        using var canceled=new CancellationTokenSource();canceled.Cancel();await Assert.ThrowsAsync<OperationCanceledException>(()=>store.ReconnectExternalAsync(project,file,true,canceled.Token));Assert.AreEqual(before,Hash(path));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>PlanningExternalFile.InspectAsync(id,"reference",original,selected));
        await store.UpdateDocumentAsync(project,new(){Attachments=[new(id,original,"A"),new(id,original,"B")]},new());before=Hash(path);file=await PlanningExternalFile.InspectAsync(id,"attachment",original,selected);await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>store.ReconnectExternalAsync(project,file,false));Assert.AreEqual(before,Hash(path));
    }
    [TestMethod]public async Task ReferenceFormatChangeIsRejectedBeforeCopyPathCanMislabelBytes()
    {
        using var t=new TempDirectory();var selected=t.CreateFile("new.png",new byte[]{137,80,78,71,13,10,26,10,1});
        await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>PlanningExternalFile.InspectAsync(Guid.NewGuid(),"reference",t.Combine("old.jpg"),selected));
    }
    [TestMethod]public async Task ExternalReceiptRejectsLaterChangesInsteadOfFallingBack()
    {
        using var t=new TempDirectory();var selected=t.CreateFile("file.txt","synthetic"u8.ToArray());var id=Guid.NewGuid();var original=t.Combine("old.txt");var file=await PlanningExternalFile.InspectAsync(id,"attachment",original,selected);Assert.AreEqual(selected,PlanningExternalFile.ResolveVerified(file,id,"attachment",original));await File.WriteAllTextAsync(selected,"changed");Assert.ThrowsExactly<InvalidDataException>(()=>PlanningExternalFile.ResolveVerified(file,id,"attachment",original));
    }
}
