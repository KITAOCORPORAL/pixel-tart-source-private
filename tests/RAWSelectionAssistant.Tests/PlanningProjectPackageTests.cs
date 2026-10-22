using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]public sealed class PlanningProjectPackageTests
{
    static string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
    static async Task<(PlanningProjectStore store,Guid id,string package)> Fixture(TempDirectory t,string name="同名合成项目")
    {
        var id=Guid.NewGuid();var store=new PlanningProjectStore(t.Combine(id.ToString("N"),"planning"));var source=t.CreateFile(id+".txt",[1,2,3]);await store.ImportProjectCopiesAsync(id,[],[new(Guid.NewGuid(),source,"合成附件.txt")]);var state=await store.LoadAsync(id);var package=t.Combine(id+".ptplan");await PlanningProjectPackage.ExportAsync(package,new PhotoProjectRecord{Id=id,Name=name},state,new(id,[]),store.StorageRoot);return(store,id,package);
    }
    [TestMethod]public async Task ValidatedPackageImportsIntoFreshRootReopensCopiesAndNeverOverwritesIdOrSameName()
    {
        using var t=new TempDirectory();var f=await Fixture(t);var old=Hash(Path.Combine(f.store.StorageRoot,f.id.ToString("N")+".planning.json"));var parent=t.Combine("imports");var result=await PlanningProjectPackage.ImportAsync(f.package,parent);var reopened=(await new PlanningProjectStore(Path.Combine(result.DataRoot,"planning")).LoadAsync(f.id)).Document!;CollectionAssert.AreEqual(new byte[]{1,2,3},PlanningProjectCopyStorage.ReadVerified(reopened.Attachments.Single().ProjectCopy!));
        await Assert.ThrowsAsync<IOException>(()=>PlanningProjectPackage.ImportAsync(f.package,parent));var other=await Fixture(t);var second=await PlanningProjectPackage.ImportAsync(other.package,parent);Assert.AreEqual(result.Name,second.Name);Assert.AreNotEqual(result.DataRoot,second.DataRoot);Assert.AreEqual(old,Hash(Path.Combine(f.store.StorageRoot,f.id.ToString("N")+".planning.json")));
    }
    static void Mutate(string source,string target,Action<ZipArchive> action){File.Copy(source,target);using var zip=ZipFile.Open(target,ZipArchiveMode.Update);action(zip);}
    [TestMethod]public async Task TamperMissingDuplicateTraversalAndUnsupportedVersionAreRejectedBeforeAnyImportWrites()
    {
        using var t=new TempDirectory();var f=await Fixture(t);var parent=t.Combine("not-created");
        var bad=t.Combine("tampered.ptplan");Mutate(f.package,bad,z=>{var e=z.GetEntry("planning.json")!;e.Delete();using var s=z.CreateEntry("planning.json").Open();s.Write([1,2,3]);});await Assert.ThrowsAsync<InvalidDataException>(()=>PlanningProjectPackage.ImportAsync(bad,parent));Assert.IsFalse(Directory.Exists(parent));
        var missing=t.Combine("missing.ptplan");Mutate(f.package,missing,z=>z.GetEntry("shots.json")!.Delete());Assert.Throws<InvalidDataException>(()=>PlanningProjectPackage.Validate(missing));
        var duplicate=t.Combine("duplicate.ptplan");Mutate(f.package,duplicate,z=>{using var s=z.CreateEntry("shots.json").Open();s.Write([1]);});Assert.Throws<InvalidDataException>(()=>PlanningProjectPackage.Validate(duplicate));
        var traversal=t.Combine("traversal.ptplan");Mutate(f.package,traversal,z=>{using var s=z.CreateEntry("../escape").Open();s.Write([1]);});Assert.Throws<InvalidDataException>(()=>PlanningProjectPackage.Validate(traversal));
        foreach(var version in new[]{0,2}){var old=t.Combine(version+".ptplan");Mutate(f.package,old,z=>{var e=z.GetEntry("manifest.json")!;PlanningPackageManifest m;using(var s=e.Open())m=JsonSerializer.Deserialize<PlanningPackageManifest>(s)!;e.Delete();using var output=z.CreateEntry("manifest.json").Open();JsonSerializer.Serialize(output,m with{Version=version});});await Assert.ThrowsAsync<NotSupportedException>(()=>PlanningProjectPackage.ImportAsync(old,parent));}
        Assert.IsFalse(Directory.Exists(parent));
    }
    [TestMethod]public async Task CancelValidationAndExtractionLeaveNoVisibleProjectAndPreserveSource()
    {
        using var t=new TempDirectory();var f=await Fixture(t);var hash=Hash(f.package);using var cancel=new CancellationTokenSource();await Assert.ThrowsAsync<OperationCanceledException>(()=>PlanningProjectPackage.ImportAsync(f.package,t.Combine("imports"),cancel.Token,new CancelAfter(cancel,1)));Assert.IsFalse(Directory.Exists(t.Combine("imports")));Assert.AreEqual(hash,Hash(f.package));
        using var extraction=new CancellationTokenSource();var callbacks=0;var rows=PlanningProjectPackage.Validate(f.package).Manifest.Files.Count;
        await Assert.ThrowsAsync<OperationCanceledException>(()=>PlanningProjectPackage.ImportAsync(f.package,t.Combine("stage-parent"),extraction.Token,new ActionProgress(_=>{if(++callbacks>rows)extraction.Cancel();})));
        Assert.AreEqual(0,Directory.GetFileSystemEntries(t.Combine("stage-parent")).Length);Assert.AreEqual(hash,Hash(f.package));
    }
    sealed class CancelAfter(CancellationTokenSource c,int count):IProgress<long>{int n;public void Report(long value){if(++n>=count)c.Cancel();}}
    sealed class ActionProgress(Action<long> action):IProgress<long>{public void Report(long value)=>action(value);}
    [TestMethod]public async Task RecoveryOnlyCleansExpiredReceiptOwnedPartAndAuditsOrphanWithoutDeletingValidOrUnknown()
    {
        using var t=new TempDirectory();var f=await Fixture(t);var doc=(await f.store.LoadAsync(f.id)).Document!;var owned=doc.Attachments.Single().ProjectCopy!;var stage=Path.Combine(f.store.StorageRoot,"project-copies",f.id.ToString("N"),doc.Package!.DocumentId.ToString("N"),".staging");Directory.CreateDirectory(stage);
        string Receipt(bool valid,PlanningProjectCopy? copy=null){var name=Guid.NewGuid().ToString("N")+".part";var path=Path.Combine(stage,name);File.WriteAllBytes(path,[7,8]);File.WriteAllText(path+".owner.json",JsonSerializer.Serialize(new PlanningCopyJournal(1,valid?f.id:Guid.NewGuid(),doc.Package.DocumentId,name,int.MaxValue,0,DateTimeOffset.UtcNow.AddHours(-2),copy)));return path;}
        var expired=Receipt(true,owned);var wrong=Receipt(false);var unknown=Path.Combine(stage,"old-unowned.part");File.WriteAllBytes(unknown,[9]);var validPath=PlanningProjectCopyStorage.Resolve(owned);var validHash=Hash(validPath);
        var orphan=owned with{ReferenceId=Guid.NewGuid(),RelativePath=""};orphan=orphan with{RelativePath=PlanningProjectCopyStorage.Relative(orphan)};var orphanPath=PlanningProjectCopyStorage.Resolve(orphan);Directory.CreateDirectory(Path.GetDirectoryName(orphanPath)!);File.Copy(validPath,orphanPath);Receipt(true,orphan);
        var unknownFile=Path.Combine(stage,"user-notes.txt");File.WriteAllText(unknownFile,"do not delete");var other=t.CreateFile("other-project/unknown.part",[1]);
        await f.store.LoadAsync(f.id);Assert.IsFalse(File.Exists(expired));Assert.IsTrue(File.Exists(wrong));Assert.IsTrue(File.Exists(unknown));Assert.IsTrue(File.Exists(unknownFile));Assert.IsTrue(File.Exists(other));Assert.AreEqual(validHash,Hash(validPath));Assert.IsTrue(File.Exists(orphanPath));Assert.IsTrue(f.store.LastRecovery.Any(x=>x.Action=="保留孤立副本"));Assert.IsTrue(f.store.LastRecovery.Any(x=>x.Action=="保留有效副本"));
        Assert.IsTrue(Directory.GetFiles(stage,"*.recovery-audit.json").Length>0);
        await f.store.ImportProjectCopiesAsync(f.id,[],[new(Guid.NewGuid(),owned.OriginalPath,"再导入")]);Assert.HasCount(1,(await f.store.LoadAsync(f.id)).Document!.Attachments);
    }
    [TestMethod]public async Task RecoveryKeepsLiveAndRecentStagesMalformedOwnersAndReparseTargets()
    {
        using var t=new TempDirectory();var f=await Fixture(t);var d=(await f.store.LoadAsync(f.id)).Document!;var stage=Path.Combine(f.store.StorageRoot,"project-copies",f.id.ToString("N"),d.Package!.DocumentId.ToString("N"),".staging");Directory.CreateDirectory(stage);
        string Add(DateTimeOffset date,int pid,long start){var name=Guid.NewGuid().ToString("N")+".part";var path=Path.Combine(stage,name);File.WriteAllBytes(path,[1]);File.WriteAllText(path+".owner.json",JsonSerializer.Serialize(new PlanningCopyJournal(1,f.id,d.Package.DocumentId,name,pid,start,date)));return path;}
        var live=Add(DateTimeOffset.UtcNow.AddHours(-2),Environment.ProcessId,System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks);var recent=Add(DateTimeOffset.UtcNow,int.MaxValue,0);var malformed=Add(DateTimeOffset.UtcNow.AddHours(-2),int.MaxValue,0);File.WriteAllText(malformed+".owner.json","{broken");
        await f.store.LoadAsync(f.id);Assert.IsTrue(File.Exists(live));Assert.IsTrue(File.Exists(recent));Assert.IsTrue(File.Exists(malformed));
        var foreign=t.CreateFile("foreign.txt",[8]);var name=Guid.NewGuid().ToString("N")+".part";var link=Path.Combine(stage,name);
        try{File.CreateSymbolicLink(link,foreign);}catch(UnauthorizedAccessException){Assert.Inconclusive("Host disallows synthetic symlink, no security change.");}
        try{File.WriteAllText(link+".owner.json",JsonSerializer.Serialize(new PlanningCopyJournal(1,f.id,d.Package.DocumentId,name,int.MaxValue,0,DateTimeOffset.UtcNow.AddHours(-2))));await f.store.LoadAsync(f.id);CollectionAssert.AreEqual(new byte[]{8},File.ReadAllBytes(foreign));Assert.IsTrue(File.Exists(link));}finally{File.Delete(link);}
    }

    [TestMethod]public async Task CancelExportAndMissingCopyCannotPublishPartialPackage()
    {
        using var t=new TempDirectory();var f=await Fixture(t);var state=await f.store.LoadAsync(f.id);var target=t.Combine("cancel.ptplan");var oldHash=Hash(f.package);using var cancel=new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(()=>PlanningProjectPackage.ExportAsync(target,new PhotoProjectRecord{Id=f.id,Name="test"},state,new(f.id,[]),f.store.StorageRoot,cancel.Token,new CancelAfter(cancel,1)));
        Assert.IsFalse(File.Exists(target));Assert.IsFalse(Directory.GetFiles(Path.GetDirectoryName(target)!,"*.package-stage").Any());Assert.AreEqual(oldHash,Hash(f.package));
        File.Delete(PlanningProjectCopyStorage.Resolve(state.Document!.Attachments.Single().ProjectCopy!));
        await Assert.ThrowsAsync<FileNotFoundException>(()=>PlanningProjectPackage.ExportAsync(t.Combine("missing-owned.ptplan"),new PhotoProjectRecord{Id=f.id,Name="test"},state,new(f.id,[]),f.store.StorageRoot));Assert.IsFalse(File.Exists(t.Combine("missing-owned.ptplan")));
    }

}
