using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]public sealed class PlanningCurrentRootImportTests
{
    static string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
    static async Task<(SqliteProjectRepository db,PlanningProjectStore planning,ProjectShotStore shots)> Destination(string root)
    {
        var db=new PixelTartDatabase(Path.Combine(root,"current.db"));var migrated=await new DatabaseMigrator(db,new DatabaseBackupService(db,Path.Combine(root,"backup"))).MigrateAsync();Assert.IsTrue(migrated.Success);return(new(db),new(Path.Combine(root,"planning")),new(Path.Combine(root,"shots")));
    }
    static async Task<(Guid id,string package,string source)> Package(TempDirectory t,string name="同名项目")
    {
        var id=Guid.NewGuid();var source=t.CreateFile(id+".txt",[1,2,3]);var store=new PlanningProjectStore(t.Combine("source",id.ToString("N")));await store.ImportProjectCopiesAsync(id,[],[new(Guid.NewGuid(),source,"合成附件.txt")]);var path=t.Combine(id+".ptplan");await PlanningProjectPackage.ExportAsync(path,new PhotoProjectRecord{Id=id,Name=name},await store.LoadAsync(id),new(id,[]),store.StorageRoot);return(id,path,source);
    }
    [TestMethod]public async Task ImportAddsOrdinaryRepositoryRecordAfterFilesAndKeepsSameNamesSeparate()
    {
        using var t=new TempDirectory();var a=await Package(t);var b=await Package(t);var target=await Destination(t.Combine("current"));await PlanningCurrentRootImport.ImportAsync(a.package,target.planning,target.shots,target.db);await PlanningCurrentRootImport.ImportAsync(b.package,target.planning,target.shots,target.db);
        var projects=await target.db.ListAsync();Assert.HasCount(2,projects);Assert.AreEqual(projects[0].Name,projects[1].Name);foreach(var p in projects){Assert.IsTrue(target.planning.Exists(p.Id));var state=await target.planning.LoadAsync(p.Id);CollectionAssert.AreEqual(new byte[]{1,2,3},PlanningProjectCopyStorage.ReadVerified(state.Document!.Attachments.Single().ProjectCopy!));Assert.IsNull(state.BookingId);}
        var old=Hash(Path.Combine(target.planning.StorageRoot,a.id.ToString("N")+".planning.json"));await Assert.ThrowsAsync<IOException>(()=>PlanningCurrentRootImport.ImportAsync(a.package,target.planning,target.shots,target.db));Assert.AreEqual(old,Hash(Path.Combine(target.planning.StorageRoot,a.id.ToString("N")+".planning.json")));Assert.HasCount(2,await target.db.ListAsync());
    }
    [TestMethod]public async Task CancelAndDatabaseDiskFailureRollbackOnlyNewFilesAndPreserveOldProjects()
    {
        using var t=new TempDirectory();var a=await Package(t);var target=await Destination(t.Combine("current"));var old=Guid.NewGuid();await target.db.UpsertAsync(new(){Id=old,Name="旧项目"});await target.planning.SaveAsync(new(old){Document=new(){Title="旧正文"}});var oldPath=Path.Combine(target.planning.StorageRoot,old.ToString("N")+".planning.json");var before=Hash(oldPath);
        using var cancel=new CancellationTokenSource();await Assert.ThrowsAsync<OperationCanceledException>(()=>PlanningCurrentRootImport.ImportAsync(a.package,target.planning,target.shots,target.db,cancel.Token,new ProgressAction(s=>{if(s=="FileInstalled")cancel.Cancel();})));
        Assert.IsFalse(await target.db.ContainsProjectIdentityAsync(a.id));Assert.IsFalse(target.planning.Exists(a.id));Assert.AreEqual(before,Hash(oldPath));
        await Assert.ThrowsAsync<IOException>(()=>PlanningCurrentRootImport.ImportAsync(a.package,target.planning,target.shots,new FailDatabase(target.db)));
        Assert.IsFalse(await target.db.ContainsProjectIdentityAsync(a.id));Assert.IsFalse(target.planning.Exists(a.id));Assert.AreEqual(before,Hash(oldPath));
        await PlanningCurrentRootImport.ImportAsync(a.package,target.planning,target.shots,target.db);Assert.HasCount(2,await target.db.ListAsync());
    }
    [TestMethod]public async Task OwnedInterruptedStageRecoversAndUnknownStageChangedFilesAndActiveOwnerRemain()
    {
        using var t=new TempDirectory();var a=await Package(t);var target=await Destination(t.Combine("current"));
        // Produce a real prepared/install journal, then simulate interruption of a process after installing files.
        await Assert.ThrowsAsync<IOException>(()=>PlanningCurrentRootImport.ImportAsync(a.package,target.planning,target.shots,new FailDatabase(target.db)));
        var recovery=PlanningCurrentRootImport.RecoveryRoot(target.planning);var stage=Directory.GetDirectories(recovery,".import-*").Single();var marker=Path.Combine(stage,"current-root-owner.json");var j=JsonSerializer.Deserialize<PlanningImportJournal>(File.ReadAllText(marker))!;
        j=j with{Phase="FilesInstalled",ProcessId=int.MaxValue,ProcessStartTicks=0};
        foreach(var f in j.Files){var root=f.Area=="planning"?target.planning.StorageRoot:target.shots.StorageRoot;var path=Path.Combine(root,f.RelativePath);Directory.CreateDirectory(Path.GetDirectoryName(path)!);if(f.RelativePath.EndsWith(".planning.json"))File.WriteAllBytes(path,JsonSerializer.SerializeToUtf8Bytes(new PlanningProjectState(a.id){Document=new PlanningDocument().EnsureIdentity(a.id)}));else File.WriteAllBytes(path,[1]);}
        // Match the receipt to exact synthetic installed bytes; recovery is conservative about anything changed.
        j=j with{Files=j.Files.Select(f=>{var path=Path.Combine(f.Area=="planning"?target.planning.StorageRoot:target.shots.StorageRoot,f.RelativePath);return f with{Bytes=new FileInfo(path).Length,Sha256=Hash(path)};}).ToArray()};File.WriteAllText(marker,JsonSerializer.Serialize(j));var unknown=Path.Combine(recovery,".import-legacy");Directory.CreateDirectory(unknown);var note=Path.Combine(unknown,"keep.txt");File.WriteAllText(note,"untouched");
        var result=await PlanningCurrentRootImport.RecoverAsync(target.planning,target.shots,target.db);Assert.IsTrue(result.Any(r=>r.Status=="已撤回未提交文件"));Assert.IsFalse(target.planning.Exists(a.id));Assert.IsTrue(File.Exists(note));Assert.IsTrue(result.Any(r=>r.Status=="保留未知暂存"));await PlanningCurrentRootImport.ImportAsync(a.package,target.planning,target.shots,target.db);
        var after=await PlanningCurrentRootImport.RecoverAsync(target.planning,target.shots,target.db);Assert.IsTrue(after.Any(r=>r.Status=="保留审计"));Assert.IsTrue(target.planning.Exists(a.id));
    }
    [TestMethod]public async Task ExistingFileWithoutDatabaseRecordIsConflictAndArchivedIdentityCannotBeOverwritten()
    {
        using var t=new TempDirectory();var a=await Package(t);var target=await Destination(t.Combine("current"));await target.planning.SaveAsync(new(a.id){Document=new(){Title="文件原样"}});var old=Hash(Path.Combine(target.planning.StorageRoot,a.id.ToString("N")+".planning.json"));await Assert.ThrowsAsync<IOException>(()=>PlanningCurrentRootImport.ImportAsync(a.package,target.planning,target.shots,target.db));Assert.AreEqual(old,Hash(Path.Combine(target.planning.StorageRoot,a.id.ToString("N")+".planning.json")));Assert.IsFalse(await target.db.ContainsProjectIdentityAsync(a.id));
    }
    sealed class ProgressAction(Action<string> action):IProgress<string>{public void Report(string s)=>action(s);}
    sealed class FailDatabase(SqliteProjectRepository db):IPlanningImportRepository
    {public Task<bool> ContainsProjectIdentityAsync(Guid id,CancellationToken token=default)=>db.ContainsProjectIdentityAsync(id,token);public Task<Guid?> GetPlanningImportReceiptAsync(Guid id,CancellationToken token=default)=>db.GetPlanningImportReceiptAsync(id,token);public Task InsertPlanningImportAsync(PhotoProjectRecord p,Guid tx,CancellationToken token=default)=>throw new IOException("Synthetic disk failure before database insert");}
    [TestMethod]public async Task TamperedV1AndArchivedDatabaseIdentityAreRejectedWithoutChangingCurrentFiles()
    {
        using var t=new TempDirectory();var a=await Package(t);var target=await Destination(t.Combine("current"));var bad=t.Combine("bad.ptplan");File.Copy(a.package,bad);using(var zip=System.IO.Compression.ZipFile.Open(bad,System.IO.Compression.ZipArchiveMode.Update)){zip.GetEntry("shots.json")!.Delete();using var stream=zip.CreateEntry("shots.json").Open();stream.Write([1,2,3]);}
        await Assert.ThrowsAsync<InvalidDataException>(()=>PlanningCurrentRootImport.ImportAsync(bad,target.planning,target.shots,target.db));Assert.HasCount(0,await target.db.ListAsync());Assert.IsFalse(Directory.Exists(target.planning.StorageRoot));
        await target.db.UpsertAsync(new(){Id=a.id,Name="已归档同ID"});var database=new PixelTartDatabase(t.Combine("current/current.db"));await using(var conn=await database.OpenConnectionAsync(write:true)){await using var cmd=conn.CreateCommand();cmd.CommandText="UPDATE Projects SET IsArchived=1 WHERE Id=$id";cmd.Parameters.AddWithValue("$id",a.id.ToString("D"));await cmd.ExecuteNonQueryAsync();}
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();var before=Hash(database.DatabasePath);await Assert.ThrowsAsync<IOException>(()=>PlanningCurrentRootImport.ImportAsync(a.package,target.planning,target.shots,target.db));Assert.IsFalse(target.planning.Exists(a.id));Assert.IsTrue(await target.db.ContainsProjectIdentityAsync(a.id));Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Assert.AreEqual(before,Hash(database.DatabasePath));
    }
    [TestMethod]public async Task PreparedNeverDeletesFinalTargetsAndActiveChangedOrWrongRootReceiptsArePreserved()
    {
        using var t=new TempDirectory();var a=await Package(t);var target=await Destination(t.Combine("current"));var validated=PlanningProjectPackage.Validate(a.package);var root=PlanningCurrentRootImport.RecoveryRoot(target.planning);Directory.CreateDirectory(root);var tx=Guid.NewGuid();var stage=Path.Combine(root,".import-"+tx.ToString("N"));Directory.CreateDirectory(stage);File.WriteAllBytes(Path.Combine(stage,"active.lock"),[]);
        var relative=a.id.ToString("N")+".planning.json";var path=Path.Combine(target.planning.StorageRoot,relative);File.WriteAllBytes(path,[8,9]);var before=Hash(path);var f=new PlanningImportTarget("planning",relative,2,before);var j=new PlanningImportJournal(1,tx,a.id,validated.Manifest.DocumentId,"test",target.planning.StorageRoot,target.shots.StorageRoot,"Prepared",int.MaxValue,0,DateTimeOffset.UtcNow,[f]);var marker=Path.Combine(stage,"current-root-owner.json");File.WriteAllText(marker,JsonSerializer.Serialize(j));
        var result=await PlanningCurrentRootImport.RecoverAsync(target.planning,target.shots,target.db);Assert.AreEqual(before,Hash(path));Assert.IsTrue(result.Any(r=>r.Status=="已撤回准备阶段"));
        j=j with{Phase="Installing",ProcessId=Environment.ProcessId,ProcessStartTicks=System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks};File.WriteAllText(marker,JsonSerializer.Serialize(j));result=await PlanningCurrentRootImport.RecoverAsync(target.planning,target.shots,target.db);Assert.IsTrue(result.Any(r=>r.Detail.Contains("仍活动")));Assert.AreEqual(before,Hash(path));
        j=j with{ProcessId=int.MaxValue,ProcessStartTicks=0};File.WriteAllText(marker,JsonSerializer.Serialize(j));File.WriteAllBytes(path,[1,2]);var changed=Hash(path);result=await PlanningCurrentRootImport.RecoverAsync(target.planning,target.shots,target.db);Assert.IsTrue(result.Any(r=>r.Status=="保留变化文件"));Assert.AreEqual(changed,Hash(path));
        File.WriteAllText(marker,JsonSerializer.Serialize(j with{PlanningRoot=t.Combine("other")}));result=await PlanningCurrentRootImport.RecoverAsync(target.planning,target.shots,target.db);Assert.IsTrue(result.Any(r=>r.Status=="保留"));Assert.AreEqual(changed,Hash(path));
    }

}
