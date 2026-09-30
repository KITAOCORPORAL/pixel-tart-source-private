using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class HomeMigrationBaselineTests
{
    [TestMethod]
    public async Task WorkflowV5ToV6PreservesDataAndDoesNotMigrateTwice()
    {
        using var temp = new TempDirectory();
        var database = new PixelTartDatabase(temp.Combine("workflow.db"));
        var backup = new DatabaseBackupService(database, temp.Combine("backups"));
        var previous = new DatabaseMigrator(database, backup,
            [new InitialSchemaMigration(), new CalendarSchemaMigration(), new TetherSchemaMigration(), new BusinessSchemaMigration(), new CalendarWorkflowSchemaMigration()]);
        Assert.AreEqual(5, (await previous.MigrateAsync()).CurrentVersion);
        await using (var connection = await database.OpenConnectionAsync(write: true))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Projects(Id,Name,ProjectType,WorkflowState,CreatedAt,UpdatedAt) VALUES('project','home','Photo','Draft','2026-09-30','2026-09-30');
                INSERT INTO ShootBookings(Id,ProjectId,Title,ClientDisplayName,StartAtUtc,EndAtUtc,TimeZoneId,Status,ShootingType,CreatedAtUtc,UpdatedAtUtc)
                VALUES('booking','project','preserve me','client','2026-09-30T10:00:00Z','2026-09-30T11:00:00Z','UTC','Draft','Other','2026-09-30','2026-09-30');
                """;
            await command.ExecuteNonQueryAsync();
        }
        // Asset metadata uses its own database; workflow migration must not touch it.
        await using var assets = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var assetBefore = await assets.Repository.GetAssetAsync(assets.B);
        var annotations = new AssetPresentationMetadataStore(new AssetLibraryDatabase(assets.DatabasePath));
        await annotations.SaveAsync([assets.B], color: "红");
        var tags = await assets.Repository.BatchCreateTagsAsync("keep-tag");
        await assets.Repository.AddTagsAsync([assets.B], tags.Select(tag => tag.TagId));
        var project = Guid.NewGuid();
        await assets.Repository.SaveProjectAssetLinkAsync(new(project, assets.B, "reference", DateTimeOffset.UtcNow));

        var current = new DatabaseMigrator(database, backup);
        var upgraded = await current.MigrateAsync();
        Assert.IsTrue(upgraded.Success, upgraded.ErrorMessage);
        Assert.AreEqual(6, upgraded.CurrentVersion);
        Assert.HasCount(1, upgraded.AppliedMigrations);
        Assert.IsTrue(File.Exists(upgraded.BackupPath));
        Assert.HasCount(0, (await current.MigrateAsync()).AppliedMigrations);
        await using (var connection = await database.OpenConnectionAsync())
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT Title || '|' || ProjectId || '|' || PreBufferMinutes || '|' || Revision FROM ShootBookings WHERE Id='booking';";
            Assert.AreEqual("preserve me|project|0|1", await command.ExecuteScalarAsync());
        }
        await assets.RestartAsync();
        Assert.AreEqual(assetBefore! with { ColorLabel = "红" }, await assets.Repository.GetAssetAsync(assets.B));
        Assert.AreEqual("红", (await annotations.GetAsync(assets.B)).Color);
        Assert.AreEqual(assets.B, (await assets.Repository.QueryAsync(new(TagId: tags.Single().TagId))).Items.Single().AssetId);
        Assert.HasCount(1, await assets.Repository.ListProjectAssetLinksAsync(assetId: assets.B, projectId: project));

        var fresh = new PixelTartDatabase(temp.Combine("fresh.db"));
        Assert.AreEqual(6, (await new DatabaseMigrator(fresh, new DatabaseBackupService(fresh, temp.Combine("fresh-backups"))).MigrateAsync()).CurrentVersion);
    }
}
