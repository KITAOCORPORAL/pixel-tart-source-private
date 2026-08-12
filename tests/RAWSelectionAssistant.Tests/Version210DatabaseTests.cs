using System.Text.Json;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;

#pragma warning disable MSTEST0037

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version210DatabaseTests
{
    [TestMethod]
    [DataRow("SchemaInfo")]
    [DataRow("Projects")]
    [DataRow("ProjectSources")]
    [DataRow("SelectionInputs")]
    [DataRow("MediaFiles")]
    [DataRow("MatchDecisions")]
    [DataRow("Tasks")]
    [DataRow("TaskSteps")]
    [DataRow("OperationItems")]
    [DataRow("UndoJournals")]
    [DataRow("QuickTools")]
    [DataRow("AuditLogs")]
    [DataRow("Notifications")]
    public async Task InitialMigration_CreatesRequiredTable(string table)
    {
        using var temp = new TempDirectory();
        var (database, migrator) = Create(temp);
        Assert.IsTrue((await migrator.MigrateAsync()).Success);
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name=$name;"; command.Parameters.AddWithValue("$name", table);
        Assert.AreEqual(1L, (long)(await command.ExecuteScalarAsync())!);
    }

    [TestMethod] public async Task InitialMigration_CreatesDatabaseAtRequestedPath() { using var temp = new TempDirectory(); var (db, migrator) = Create(temp); var result = await migrator.MigrateAsync(); Assert.IsTrue(result.Success); Assert.IsTrue(File.Exists(db.DatabasePath)); }
    [TestMethod] public async Task InitialMigration_RecordsSchemaVersionOne() { using var temp = new TempDirectory(); var db=new PixelTartDatabase(temp.Combine("data","pixel-tart.db"));var migrator=new DatabaseMigrator(db,new DatabaseBackupService(db,temp.Combine("backups")),[new InitialSchemaMigration()]); await migrator.MigrateAsync(); await using var connection = await db.OpenConnectionAsync(); await using var command=connection.CreateCommand();command.CommandText="SELECT MAX(Version) FROM SchemaInfo;";Assert.AreEqual(1L,(long)(await command.ExecuteScalarAsync())!); }
    [TestMethod] public async Task RepeatedMigration_DoesNotRunTwice() { using var temp = new TempDirectory(); var (_, migrator) = Create(temp); await migrator.MigrateAsync(); var second=await migrator.MigrateAsync(); Assert.IsTrue(second.Success); Assert.AreEqual(0,second.AppliedMigrations.Count);Assert.AreEqual(5,second.CurrentVersion); }

    [TestMethod]
    public async Task MigrationBeforeUpgrade_CreatesBackup()
    {
        using var temp = new TempDirectory(); var db=new PixelTartDatabase(temp.Combine("data","pixel-tart.db")); var initial=new DatabaseMigrator(db,new DatabaseBackupService(db,temp.Combine("backups")),[new InitialSchemaMigration()]); await initial.MigrateAsync();
        var backup=new DatabaseBackupService(db,temp.Combine("backups")); var migrator=new DatabaseMigrator(db,backup,[new InitialSchemaMigration(),new AddProbeMigration()]);
        var result=await migrator.MigrateAsync();Assert.IsTrue(result.Success);Assert.IsNotNull(result.BackupPath);Assert.IsTrue(File.Exists(result.BackupPath));Assert.AreEqual(2,result.CurrentVersion);
    }

    [TestMethod]
    public async Task FailedMigration_RollsBackAndKeepsOriginal()
    {
        using var temp=new TempDirectory();var db=new PixelTartDatabase(temp.Combine("data","pixel-tart.db"));var initial=new DatabaseMigrator(db,new DatabaseBackupService(db,temp.Combine("backups")),[new InitialSchemaMigration()]);await initial.MigrateAsync();var migrator=new DatabaseMigrator(db,new DatabaseBackupService(db,temp.Combine("backups")),[new InitialSchemaMigration(),new FailingMigration()]);var result=await migrator.MigrateAsync();Assert.IsFalse(result.Success);Assert.IsTrue(File.Exists(db.DatabasePath));
        var check=new PixelTartDatabase(db.DatabasePath);await using var connection=await check.OpenConnectionAsync();await using var command=connection.CreateCommand();command.CommandText="SELECT count(*) FROM sqlite_master WHERE type='table' AND name='ShouldRollback';";Assert.AreEqual(0L,(long)(await command.ExecuteScalarAsync())!);
    }

    [TestMethod]
    public async Task CorruptedDatabase_IsNotOverwritten()
    {
        using var temp=new TempDirectory();var path=temp.CreateFile("pixel-tart.db",[1,2,3,4,5,6,7]);var original=File.ReadAllBytes(path);var db=new PixelTartDatabase(path);var result=await new DatabaseMigrator(db,new DatabaseBackupService(db,temp.Combine("backups"))).MigrateAsync();Assert.IsFalse(result.Success);Assert.IsTrue(result.IsReadOnlyRecovery);CollectionAssert.AreEqual(original,File.ReadAllBytes(path));
    }

    [TestMethod]
    public async Task HigherSchema_BlocksWrites()
    {
        using var temp=new TempDirectory();var path=temp.Combine("future.db");await using(var connection=new SqliteConnection($"Data Source={path}")){await connection.OpenAsync();await using var command=connection.CreateCommand();command.CommandText="CREATE TABLE SchemaInfo(Version INTEGER PRIMARY KEY,AppliedAt TEXT,ApplicationVersion TEXT,MigrationName TEXT);INSERT INTO SchemaInfo VALUES(99,'x','9.9.9','future');";await command.ExecuteNonQueryAsync();}
        var db=new PixelTartDatabase(path);var result=await new DatabaseMigrator(db,new DatabaseBackupService(db,temp.Combine("backups"))).MigrateAsync();Assert.IsFalse(result.Success);Assert.IsTrue(result.IsReadOnlyRecovery);await Assert.ThrowsExactlyAsync<InvalidOperationException>(()=>db.OpenConnectionAsync(write:true));
    }

    [TestMethod]
    public async Task JsonProjects_AreImportedAndOriginalIsRetained()
    {
        using var temp=new TempDirectory();var root=temp.Combine("root");Directory.CreateDirectory(Path.Combine(root,"Projects"));var project=new PhotoProjectRecord{Name="旧项目",SourceDirectories=[temp.Combine("photos")],SelectionInputs=["DSC00123.JPG"]};var file=Path.Combine(root,"Projects","projects.json");await File.WriteAllTextAsync(file,JsonSerializer.Serialize(new[]{project}));var(db,migrator)=Create(temp);await migrator.MigrateAsync();var report=await new JsonDataMigrationService(db,root,temp.Combine("migration-backups")).MigrateAsync();Assert.IsTrue(report.Success);Assert.IsTrue(File.Exists(file));Assert.AreEqual(1,(await new SqliteProjectRepository(db).ListAsync()).Count);
    }

    [TestMethod]
    public async Task JsonMigration_IsIdempotent()
    {
        using var temp=new TempDirectory();var root=temp.Combine("root");Directory.CreateDirectory(Path.Combine(root,"Projects"));await File.WriteAllTextAsync(Path.Combine(root,"Projects","projects.json"),"[]");var(db,migrator)=Create(temp);await migrator.MigrateAsync();var service=new JsonDataMigrationService(db,root,temp.Combine("backups"));await service.MigrateAsync();var second=await service.MigrateAsync();Assert.IsTrue(second.AlreadyCompleted);
    }

    [TestMethod]
    public async Task CorruptJson_IsReportedWithoutDeletingFile()
    {
        using var temp=new TempDirectory();var root=temp.Combine("root");Directory.CreateDirectory(Path.Combine(root,"Projects"));var file=Path.Combine(root,"Projects","projects.json");await File.WriteAllTextAsync(file,"{bad-json");var(db,migrator)=Create(temp);await migrator.MigrateAsync();var report=await new JsonDataMigrationService(db,root,temp.Combine("backups")).MigrateAsync();Assert.IsFalse(report.Success);Assert.IsTrue(File.Exists(file));Assert.IsTrue(report.Items.Any(x=>!x.Success));
    }

    [TestMethod]
    public async Task QuickToolsJson_IsImportedInOrder()
    {
        using var temp=new TempDirectory();var root=temp.Combine("root");Directory.CreateDirectory(root);var settings=new AppSettings{PinnedQuickTools=["Collage","PhotoOrganize"]};settings.QuickToolLayout.OrderedToolIds=["Collage","PhotoOrganize"];await File.WriteAllTextAsync(Path.Combine(root,"settings.json"),JsonSerializer.Serialize(settings));var(db,migrator)=Create(temp);await migrator.MigrateAsync();await new JsonDataMigrationService(db,root,temp.Combine("backups")).MigrateAsync();CollectionAssert.AreEqual(new[]{"Collage","PhotoOrganize"},(await new SqliteQuickToolsRepository(db).LoadAsync()).ToArray());
    }

    [TestMethod]
    public async Task MediaIndexRepository_HandlesTenThousandRecords()
    {
        using var temp=new TempDirectory();var(db,migrator)=Create(temp);await migrator.MigrateAsync();var repository=new SqliteMediaIndexRepository(db);var media=Enumerable.Range(0,10000).Select(i=>new MediaFileRecord{FullPath=temp.Combine("photos",$"{i:D5}.jpg"),FileName=$"{i:D5}.jpg",Extension=".JPG",Size=i,LastWriteTimeUtc=DateTime.UtcNow,Category=FileCategory.Jpeg}).ToArray();await repository.ReplaceAsync(media);Assert.AreEqual(10000,(await repository.LoadAsync()).Count);
    }

    private static (PixelTartDatabase Database, DatabaseMigrator Migrator) Create(TempDirectory temp) { var db=new PixelTartDatabase(temp.Combine("data","pixel-tart.db"));return(db,new DatabaseMigrator(db,new DatabaseBackupService(db,temp.Combine("backups")))); }
    private sealed class AddProbeMigration:IMigration { public int Version=>2;public string Name=>"Probe";public async Task ApplyAsync(SqliteConnection c,SqliteTransaction t,CancellationToken token){await using var cmd=c.CreateCommand();cmd.Transaction=t;cmd.CommandText="CREATE TABLE Probe(Id INTEGER);";await cmd.ExecuteNonQueryAsync(token);} }
    private sealed class FailingMigration:IMigration { public int Version=>2;public string Name=>"Fail";public async Task ApplyAsync(SqliteConnection c,SqliteTransaction t,CancellationToken token){await using var cmd=c.CreateCommand();cmd.Transaction=t;cmd.CommandText="CREATE TABLE ShouldRollback(Id INTEGER);";await cmd.ExecuteNonQueryAsync(token);throw new InvalidOperationException("boom");} }
}
