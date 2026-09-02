using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryP3MigrationBackupTests
{
    [TestMethod]
    public async Task V6UpgradeCreatesAndValidatesACompleteExternalSqliteBackup()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        await DowngradeAsync(setup.DatabasePath);
        var backupPath = setup.DatabasePath + ".schema-v6-backup.sqlite";

        await setup.RestartAsync();

        Assert.IsTrue(File.Exists(backupPath));
        await AssertValidV6BackupAsync(backupPath);
        Assert.IsEmpty(Directory.GetFiles(Path.GetDirectoryName(backupPath)!, "*.partial-*"));
    }

    [TestMethod]
    public async Task ExistingCorruptBackupIsNeverTrustedOrOverwrittenAndFreshBackupIsCreated()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        await DowngradeAsync(setup.DatabasePath);
        var primary = setup.DatabasePath + ".schema-v6-backup.sqlite";
        var corruptBytes = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(primary, corruptBytes);

        await setup.RestartAsync();

        CollectionAssert.AreEqual(corruptBytes, await File.ReadAllBytesAsync(primary));
        var directory = Path.GetDirectoryName(setup.DatabasePath)!;
        var prefix = Path.GetFileName(setup.DatabasePath) + ".schema-v6-backup-";
        var fresh = Directory.GetFiles(directory, prefix + "*.sqlite");
        Assert.HasCount(1, fresh);
        await AssertValidV6BackupAsync(fresh[0]);
        Assert.IsEmpty(Directory.GetFiles(directory, "*.partial-*"));
    }

    private static async Task DowngradeAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys=OFF;
            DROP TABLE IF EXISTS SmartFolderQueryDocuments;
            DELETE FROM AssetLibrarySchemaInfo WHERE Version > 6;
            INSERT OR IGNORE INTO AssetLibrarySchemaInfo(Version,AppliedAt) VALUES(6,'2026-09-02T00:00:00.0000000+00:00');
            PRAGMA foreign_keys=ON;
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertValidV6BackupAsync(string path)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        await connection.OpenAsync();
        await using (var integrity = connection.CreateCommand())
        {
            integrity.CommandText = "PRAGMA quick_check;";
            Assert.AreEqual("ok", Convert.ToString(await integrity.ExecuteScalarAsync()));
        }
        await using (var version = connection.CreateCommand())
        {
            version.CommandText = "SELECT MAX(Version) FROM AssetLibrarySchemaInfo;";
            Assert.AreEqual(6L, Convert.ToInt64(await version.ExecuteScalarAsync()));
        }
    }
}
