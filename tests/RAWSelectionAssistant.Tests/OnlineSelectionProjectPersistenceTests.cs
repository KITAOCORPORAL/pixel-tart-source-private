using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class OnlineSelectionProjectPersistenceTests
{
    private static async Task<PixelTartDatabase> Create(TempDirectory temp)
    {
        var db = new PixelTartDatabase(temp.Combine("workflow.db"));
        var migration = await new DatabaseMigrator(db, new DatabaseBackupService(db, temp.Combine("backups"))).MigrateAsync();
        Assert.IsTrue(migration.Success, migration.ErrorMessage); Assert.AreEqual(8, migration.CurrentVersion);
        return db;
    }
    [TestMethod]
    public async Task ReverseIdentitySurvivesRepositoryReopenAndOrdinarySave()
    {
        using var temp = new TempDirectory(); var db = await Create(temp); var origin = Guid.NewGuid();
        var project = new PhotoProjectRecord { Name="Synthetic selection", SourceOnlineSelectionProjectId=origin, SelectionInputs=["SYNTHETIC.JPG"] };
        await new SqliteProjectRepository(db).UpsertAsync(project);
        var reopened = (await new SqliteProjectRepository(new PixelTartDatabase(db.DatabasePath)).ListAsync()).Single();
        Assert.AreEqual(origin, reopened.SourceOnlineSelectionProjectId); Assert.AreEqual(project.Id, reopened.Id);
        CollectionAssert.AreEqual(project.SelectionInputs, reopened.SelectionInputs);
        reopened.SourceOnlineSelectionProjectId=null; reopened.Name="Ordinary save";
        await new SqliteProjectRepository(db).UpsertAsync(reopened);
        Assert.AreEqual(origin,(await new SqliteProjectRepository(db).ListAsync()).Single().SourceOnlineSelectionProjectId);
    }
    [TestMethod]
    public async Task DifferentSelectionCannotOverwriteExistingWorkflowIdentity()
    {
        using var temp = new TempDirectory(); var db = await Create(temp); var repo = new SqliteProjectRepository(db);
        var project = new PhotoProjectRecord { Name="Keep", SourceOnlineSelectionProjectId=Guid.NewGuid() }; await repo.UpsertAsync(project);
        var origin=project.SourceOnlineSelectionProjectId; project.SourceOnlineSelectionProjectId=Guid.NewGuid(); project.Name="Wrong";
        await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>repo.UpsertAsync(project));
        var stored=(await repo.ListAsync()).Single(); Assert.AreEqual(origin,stored.SourceOnlineSelectionProjectId); Assert.AreEqual("Keep",stored.Name);
    }
    [TestMethod]
    public async Task OneSelectionCannotCreateASecondWorkflowProject()
    {
        using var temp = new TempDirectory(); var db = await Create(temp); var repo = new SqliteProjectRepository(db); var origin=Guid.NewGuid();
        await repo.UpsertAsync(new PhotoProjectRecord {Name="First",SourceOnlineSelectionProjectId=origin});
        await Assert.ThrowsExactlyAsync<SqliteException>(()=>repo.UpsertAsync(new PhotoProjectRecord {Name="Duplicate",SourceOnlineSelectionProjectId=origin}));
        Assert.HasCount(1,await repo.ListAsync());
    }
}
