using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class PlanningProjectCopyTests
{
    static PlanningDocumentReference Ref(string path,Guid? id=null)=>new(new(id??Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:Path.GetFullPath(path),Title:Path.GetFileName(path)));
    static PlanningAttachment FileLink(string path)=>new(Guid.NewGuid(),Path.GetFullPath(path),Path.GetFileName(path));
    static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    [TestMethod] public async Task CopiesKeepIdentityNamesHashesAndRebindAfterEntireSyntheticDirectoryMoves()
    {
        using var t=new TempDirectory();var dir=t.Combine("complete-project");var store=new PlanningProjectStore(Path.Combine(dir,"planning"));var project=Guid.NewGuid();
        var a=t.CreateFile("sources/a/same.txt",[1,2,3]);var b=t.CreateFile("sources/b/same.txt",[4,5,6]);var link=Ref(a);var attachment=FileLink(b);
        await store.ImportProjectCopiesAsync(project,[link],[attachment]);var doc=(await store.LoadAsync(project)).Document!;
        Assert.AreEqual(link.Source,doc.References.Single().Source);Assert.AreEqual(attachment.Id,doc.Attachments.Single().Id);Assert.AreEqual(attachment.Path,doc.Attachments.Single().Path);
        foreach(var copy in new[]{doc.References.Single().ProjectCopy!,doc.Attachments.Single().ProjectCopy!}){Assert.IsFalse(Path.IsPathRooted(copy.RelativePath));Assert.AreEqual(copy.Sha256,Hash(PlanningProjectCopyStorage.Resolve(copy)));Assert.AreEqual(Hash(copy.OriginalPath),copy.Sha256);}
        var json=await File.ReadAllTextAsync(Path.Combine(store.StorageRoot,project.ToString("N")+".planning.json"));Assert.IsFalse(json.Contains("StorageRoot"));
        var moved=t.Combine("relocated-project");Directory.Move(dir,moved);File.Delete(a);File.Delete(b);
        var reopened=(await new PlanningProjectStore(Path.Combine(moved,"planning")).LoadAsync(project)).Document!;
        CollectionAssert.AreEqual(new byte[]{1,2,3},PlanningProjectCopyStorage.ReadVerified(reopened.References.Single().ProjectCopy!));
        CollectionAssert.AreEqual(new byte[]{4,5,6},PlanningProjectCopyStorage.ReadVerified(reopened.Attachments.Single().ProjectCopy!));
    }
    [TestMethod] public async Task DuplicateImportsAreIdempotentSameNamesDoNotCollideAndChangedSourceDoesNotReplaceSnapshot()
    {
        using var t=new TempDirectory();var store=new PlanningProjectStore(t.Combine("planning"));var id=Guid.NewGuid();var a=t.CreateFile("a/same.txt",[1,2]);var b=t.CreateFile("b/same.txt",[3,4]);
        await store.ImportProjectCopiesAsync(id,[Ref(a)],[FileLink(a),FileLink(b)]);var first=(await store.LoadAsync(id)).Document!;
        await store.ImportProjectCopiesAsync(id,[Ref(a)],[FileLink(a),FileLink(b)]);var after=(await store.LoadAsync(id)).Document!;
        Assert.HasCount(1,after.References);Assert.HasCount(2,after.Attachments);Assert.AreEqual(first.References[0].Source.ReferenceId,after.References[0].Source.ReferenceId);
        Assert.AreNotEqual(after.Attachments[0].ProjectCopy!.RelativePath,after.Attachments[1].ProjectCopy!.RelativePath);
        var metadata=t.Combine("planning/"+id.ToString("N")+".planning.json");var before=File.ReadAllBytes(metadata);await File.WriteAllBytesAsync(a,[9,9]);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>store.ImportProjectCopiesAsync(id,[Ref(a)],[]));CollectionAssert.AreEqual(before,File.ReadAllBytes(metadata));
        CollectionAssert.AreEqual(new byte[]{1,2},PlanningProjectCopyStorage.ReadVerified((await store.LoadAsync(id)).Document!.References[0].ProjectCopy!));
        Assert.IsFalse(Directory.GetFiles(store.StorageRoot,"*",SearchOption.AllDirectories).Any(p=>p.EndsWith(".part")));
    }
    [TestMethod] public async Task BatchMissingLockedOversizedAndCancelledSourcesLeaveNoReferencesOrHalfFiles()
    {
        using var t=new TempDirectory();var store=new PlanningProjectStore(t.Combine("planning"));var id=Guid.NewGuid();var good=t.CreateFile("good.txt",[1]);var missing=t.Combine("missing.txt");
        await Assert.ThrowsExactlyAsync<FileNotFoundException>(()=>store.ImportProjectCopiesAsync(id,[],[FileLink(good),FileLink(missing)]));Assert.IsNull((await store.LoadAsync(id)).Document);
        var locked=t.CreateFile("locked.txt",[1]);using(var hold=new FileStream(locked,FileMode.Open,FileAccess.ReadWrite,FileShare.None))await Assert.ThrowsExactlyAsync<IOException>(()=>store.ImportProjectCopiesAsync(id,[],[FileLink(locked)]));
        var large=t.Combine("large.bin");using(var f=File.Create(large))f.SetLength(PlanningProjectCopyStorage.MaximumBytes+1);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>store.ImportProjectCopiesAsync(id,[],[FileLink(large)]));
        var empty=t.CreateFile("empty.bin",[]);await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>store.ImportProjectCopiesAsync(id,[],[FileLink(empty)]));
        var chunk=t.CreateFile("cancel.bin",new byte[1024*1024]);using var cancel=new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(()=>store.ImportProjectCopiesAsync(id,[],[FileLink(chunk)],cancel.Token,new CancelProgress(cancel)));
        Assert.IsNull((await store.LoadAsync(id)).Document);Assert.IsFalse(Directory.GetFiles(store.StorageRoot,"*",SearchOption.AllDirectories).Any(p=>p.EndsWith(".part")||p.EndsWith(".bin")||p.EndsWith(".txt")));
        Assert.AreEqual(1024L*1024,new FileInfo(chunk).Length);
    }
    sealed class CancelProgress(CancellationTokenSource cancel):IProgress<long>{public void Report(long value)=>cancel.Cancel();}
    [TestMethod] public async Task SaveFailureRollsBackCopiesAndPreservesOldPlanningBytes()
    {
        using var t=new TempDirectory();var store=new PlanningProjectStore(t.Combine("planning"));var id=Guid.NewGuid();await store.SaveAsync(new(id){Document=new PlanningDocument{Title="旧策划",Body="旧正文"}});
        var path=Path.Combine(store.StorageRoot,id.ToString("N")+".planning.json");var before=File.ReadAllBytes(path);var source=t.CreateFile("source.txt",[1,2]);
        using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            try{await store.ImportProjectCopiesAsync(id,[],[FileLink(source)]);Assert.Fail("Locked planning file must not be replaced.");}
            catch(Exception error) when(error is IOException or UnauthorizedAccessException){}
        }
        CollectionAssert.AreEqual(before,File.ReadAllBytes(path));Assert.AreEqual("旧正文",(await store.LoadAsync(id)).Document!.Body);
        Assert.IsFalse(Directory.GetFiles(store.StorageRoot,"*.txt",SearchOption.AllDirectories).Any());
    }
    [TestMethod] public async Task MissingOrTamperedOwnedCopyNeverFallsBackToExistingOriginalAndLegacyRemainsExternal()
    {
        using var t=new TempDirectory();var store=new PlanningProjectStore(t.Combine("planning"));var id=Guid.NewGuid();var source=t.CreateFile("source.png",[1,2,3]);
        await store.ImportProjectCopiesAsync(id,[Ref(source)],[]);var doc=(await store.LoadAsync(id)).Document!;var copy=doc.References[0].ProjectCopy!;var owned=PlanningProjectCopyStorage.Resolve(copy);File.WriteAllBytes(owned,[9,9,9]);
        StringAssert.Contains(PlanningProjectCopyStorage.Status(copy),"不可用");await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>Task.Run(()=>PlanningProjectCopyStorage.ReadVerified(copy)));
        File.Delete(owned);StringAssert.Contains(PlanningProjectCopyStorage.Status(copy),"不可用");Assert.IsTrue(File.Exists(source));
        var legacy=Guid.NewGuid();await store.SaveAsync(new(legacy){Document=new PlanningDocument{References=[Ref(source)],Attachments=[FileLink(source)]}});
        var loaded=(await store.LoadAsync(legacy)).Document!;Assert.IsNull(loaded.References[0].ProjectCopy);Assert.IsNull(loaded.Attachments[0].ProjectCopy);
    }
    [TestMethod] public async Task SourceAndDestinationReparsePointsAreRejectedWithoutFollowingThem()
    {
        using var t=new TempDirectory();var source=t.CreateFile("real/file.txt",[1]);var alias=t.Combine("alias");
        try{Directory.CreateSymbolicLink(alias,t.Combine("real"));}catch(UnauthorizedAccessException){Assert.Inconclusive("Synthetic symlink creation unavailable; no security change.");}
        try
        {
            var store=new PlanningProjectStore(t.Combine("planning"));await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>store.ImportProjectCopiesAsync(Guid.NewGuid(),[],[FileLink(Path.Combine(alias,"file.txt"))]));
            Directory.CreateDirectory(store.StorageRoot);Directory.CreateSymbolicLink(Path.Combine(store.StorageRoot,"project-copies"),t.Combine("real"));
            try{await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>store.ImportProjectCopiesAsync(Guid.NewGuid(),[],[FileLink(source)]));}
            finally{Directory.Delete(Path.Combine(store.StorageRoot,"project-copies"));}
            CollectionAssert.AreEqual(new byte[]{1},File.ReadAllBytes(source));
        }finally{Directory.Delete(alias);}
    }
    [TestMethod] public async Task ParallelDuplicateImportsShareOneIdentityAndCannotOverwriteAnotherSource()
    {
        using var t=new TempDirectory();var store=new PlanningProjectStore(t.Combine("planning"));var id=Guid.NewGuid();var a=t.CreateFile("a.txt",[1,2,3]);var b=t.CreateFile("b.txt",[4,5,6]);
        await Task.WhenAll(Enumerable.Range(0,6).Select(_=>store.ImportProjectCopiesAsync(id,[Ref(a)],[FileLink(a)])));
        var doc=(await store.LoadAsync(id)).Document!;Assert.HasCount(1,doc.References);Assert.HasCount(1,doc.Attachments);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>store.ImportProjectCopiesAsync(id,[Ref(b,doc.References[0].Source.ReferenceId)],[]));
        CollectionAssert.AreEqual(new byte[]{1,2,3},PlanningProjectCopyStorage.ReadVerified((await store.LoadAsync(id)).Document!.References[0].ProjectCopy!));
        Assert.AreEqual(2,Directory.GetFiles(Path.Combine(store.StorageRoot,"project-copies"),"*.txt",SearchOption.AllDirectories).Length);
    }
    [TestMethod] public async Task ForgedRelativeIdentityAndCrossDocumentReceiptFailClosed()
    {
        using var t=new TempDirectory();var store=new PlanningProjectStore(t.Combine("planning"));var id=Guid.NewGuid();var source=t.CreateFile("source.txt",[1]);await store.ImportProjectCopiesAsync(id,[],[FileLink(source)]);var doc=(await store.LoadAsync(id)).Document!;var a=doc.Attachments[0];
        Assert.ThrowsExactly<InvalidDataException>(()=>PlanningProjectCopyStorage.Bind(doc with{Attachments=[a with{ProjectCopy=a.ProjectCopy! with{RelativePath="../source.txt"}}]},store.StorageRoot));
        Assert.ThrowsExactly<InvalidDataException>(()=>PlanningProjectCopyStorage.Bind(doc with{Attachments=[a with{ProjectCopy=a.ProjectCopy! with{DocumentId=Guid.NewGuid(),RelativePath="wrong"}}]},store.StorageRoot));
        var json=JsonSerializer.Serialize(doc);Assert.IsFalse(json.Contains("StorageRoot"));
    }
}
