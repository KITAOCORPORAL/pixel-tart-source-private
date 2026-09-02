using System.Text.Json;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryP3MigrationTests
{
    [TestMethod]
    public async Task V6LegacyRulesMigrateToV7WithAuditableBackupAndRemainIdempotent()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var folderId = Guid.NewGuid();
        var rootRule = new SmartFolderRule(
            Guid.NewGuid(), folderId, SmartFolderField.Rating, SmartFolderOperator.GreaterThanOrEqual, "3", SortOrder: 0);
        var groupId = Guid.NewGuid();
        var groupedRule = new SmartFolderRule(
            Guid.NewGuid(), folderId, SmartFolderField.Comment, SmartFolderOperator.Contains, "中文", SortOrder: 1, GroupId: groupId, GroupLogic: SmartFolderLogic.Or);
        await setup.Repository.SaveSmartFolderAsync(
            new(folderId, "v6 规则", SmartFolderLogic.And, "legacy payload"),
            [rootRule, groupedRule]);
        await DowngradeToV6Async(setup.DatabasePath);

        await setup.RestartAsync();

        Assert.AreEqual(7L, await ScalarInt64Async(setup.DatabasePath, "SELECT MAX(Version) FROM AssetLibrarySchemaInfo;"));
        var migrated = await setup.Repository.GetSmartFolderQueryDocumentAsync(folderId);
        Assert.IsNotNull(migrated);
        Assert.AreEqual(AssetQueryDocument.CurrentVersion, migrated.Document.Version);
        Assert.AreEqual(AssetQueryDocumentCodec.ComputeHash(migrated.Document), migrated.QueryHash);
        Assert.IsFalse(string.IsNullOrWhiteSpace(migrated.LegacyRulesBackupJson));
        using (var backup = JsonDocument.Parse(migrated.LegacyRulesBackupJson))
        {
            Assert.AreEqual(2, backup.RootElement.GetArrayLength());
            var backupText = backup.RootElement.GetRawText();
            StringAssert.Contains(backupText, rootRule.RuleId.ToString("D"));
            StringAssert.Contains(backupText, groupedRule.RuleId.ToString("D"));
        }
        var result = await setup.Repository.QueryAsync(new(SmartFolderId: folderId, PageSize: 20));
        Assert.IsTrue(string.IsNullOrWhiteSpace(result.RegexError), result.RegexError);
        CollectionAssert.AreEqual(new[] { setup.B }, result.Items.Select(item => item.AssetId).ToArray());
        Assert.HasCount(2, await setup.Repository.ListSmartFolderRulesAsync(folderId));

        var firstJson = AssetQueryDocumentCodec.SerializeCanonical(migrated.Document);
        var firstHash = migrated.QueryHash;
        var firstBackup = migrated.LegacyRulesBackupJson;
        await setup.RestartAsync();
        await setup.RestartAsync();
        var reopened = await setup.Repository.GetSmartFolderQueryDocumentAsync(folderId);
        Assert.IsNotNull(reopened);
        Assert.AreEqual(firstJson, AssetQueryDocumentCodec.SerializeCanonical(reopened.Document));
        Assert.AreEqual(firstHash, reopened.QueryHash);
        Assert.AreEqual(firstBackup, reopened.LegacyRulesBackupJson);
        Assert.AreEqual(1L, await ScalarInt64Async(
            setup.DatabasePath,
            "SELECT COUNT(*) FROM SmartFolderQueryDocuments WHERE SmartFolderId=$id;",
            ("$id", folderId.ToString("D"))));
    }

    [TestMethod]
    public async Task InvalidV6RuleRollsBackWholeMigrationInsteadOfSilentlyCreatingPartialV7State()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var validId = Guid.NewGuid();
        var corruptId = Guid.NewGuid();
        await setup.Repository.SaveSmartFolderAsync(
            new(validId, "valid"),
            [new(Guid.NewGuid(), validId, SmartFolderField.Rating, SmartFolderOperator.GreaterThanOrEqual, "3")]);
        await setup.Repository.SaveSmartFolderAsync(
            new(corruptId, "corrupt"),
            [new(Guid.NewGuid(), corruptId, SmartFolderField.Comment, SmartFolderOperator.Contains, "value")]);
        await DowngradeToV6Async(setup.DatabasePath);
        await ExecuteAsync(
            setup.DatabasePath,
            "UPDATE SmartFolderRules SET Operator='FutureOperator' WHERE SmartFolderId=$id;",
            ("$id", corruptId.ToString("D")));

        var reopening = new SqliteAssetLibraryRepository(setup.DatabasePath);
        await Assert.ThrowsAsync<InvalidDataException>(() => reopening.InitializeAsync());
        await reopening.DisposeAsync();

        Assert.AreEqual(6L, await ScalarInt64Async(setup.DatabasePath, "SELECT MAX(Version) FROM AssetLibrarySchemaInfo;"));
        var queryTableExists = await ScalarInt64Async(
            setup.DatabasePath,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SmartFolderQueryDocuments';");
        Assert.AreEqual(0L, queryTableExists, "The v7 table DDL must roll back with the rejected conversion.");
        Assert.AreEqual(2L, await ScalarInt64Async(setup.DatabasePath, "SELECT COUNT(*) FROM SmartFolders;"));
        Assert.AreEqual(2L, await ScalarInt64Async(setup.DatabasePath, "SELECT COUNT(*) FROM SmartFolderRules;"));
    }

    [TestMethod]
    public async Task UnknownLegacyRootAndGroupLogicBothFailClosedWithoutLeavingV7Ddl()
    {
        await using (var rootSetup = await AssetLibraryP3TestSetup.CreateCanonicalAsync())
        {
            var folderId = Guid.NewGuid();
            await rootSetup.Repository.SaveSmartFolderAsync(
                new(folderId, "bad root"),
                [new(Guid.NewGuid(), folderId, SmartFolderField.Rating, SmartFolderOperator.Equals, "3")]);
            await DowngradeToV6Async(rootSetup.DatabasePath);
            await ExecuteAsync(rootSetup.DatabasePath, "UPDATE SmartFolders SET Logic='FutureLogic' WHERE SmartFolderId=$id;", ("$id", folderId.ToString("D")));

            var repository = new SqliteAssetLibraryRepository(rootSetup.DatabasePath);
            await Assert.ThrowsAsync<InvalidDataException>(() => repository.InitializeAsync());
            await repository.DisposeAsync();
            Assert.AreEqual(0L, await ScalarInt64Async(rootSetup.DatabasePath, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SmartFolderQueryDocuments';"));
        }

        await using (var groupSetup = await AssetLibraryP3TestSetup.CreateCanonicalAsync())
        {
            var folderId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            await groupSetup.Repository.SaveSmartFolderAsync(
                new(folderId, "bad group"),
                [new(Guid.NewGuid(), folderId, SmartFolderField.Rating, SmartFolderOperator.Equals, "3", GroupId: groupId)]);
            await DowngradeToV6Async(groupSetup.DatabasePath);
            await ExecuteAsync(groupSetup.DatabasePath, "UPDATE SmartFolderRules SET GroupLogic='FutureLogic' WHERE SmartFolderId=$id;", ("$id", folderId.ToString("D")));

            var repository = new SqliteAssetLibraryRepository(groupSetup.DatabasePath);
            await Assert.ThrowsAsync<InvalidDataException>(() => repository.InitializeAsync());
            await repository.DisposeAsync();
            Assert.AreEqual(0L, await ScalarInt64Async(groupSetup.DatabasePath, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SmartFolderQueryDocuments';"));
        }
    }

    [TestMethod]
    public async Task CorruptLegacyBackupFailsClosedWithoutRewritingEvidence()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var folderId = Guid.NewGuid();
        await setup.Repository.SaveSmartFolderAsync(
            new(folderId, "backup corruption"),
            [new(Guid.NewGuid(), folderId, SmartFolderField.Rating, SmartFolderOperator.GreaterThanOrEqual, "1")]);
        await DowngradeToV6Async(setup.DatabasePath);
        await setup.RestartAsync();
        var before = await ReadQueryRowAsync(setup.DatabasePath, folderId);
        await ExecuteAsync(
            setup.DatabasePath,
            "UPDATE SmartFolderQueryDocuments SET LegacyRulesBackupJson='{broken-json' WHERE SmartFolderId=$id;",
            ("$id", folderId.ToString("D")));

        await Assert.ThrowsAsync<InvalidDataException>(() => setup.RestartAsync());

        var after = await ReadQueryRowAsync(setup.DatabasePath, folderId);
        Assert.AreEqual(before.QueryJson, after.QueryJson);
        Assert.AreEqual(before.QueryHash, after.QueryHash);
        Assert.AreEqual("{broken-json", after.BackupJson);
        Assert.AreEqual(7L, await ScalarInt64Async(setup.DatabasePath, "SELECT MAX(Version) FROM AssetLibrarySchemaInfo;"));
    }

    [TestMethod]
    public async Task FutureSchemaAndFutureQueryDocumentVersionsFailClosedWithoutDowngrade()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        await ExecuteAsync(
            setup.DatabasePath,
            "INSERT INTO AssetLibrarySchemaInfo(Version,AppliedAt) VALUES(99,$at);",
            ("$at", DateTimeOffset.UtcNow.ToString("O")));

        await Assert.ThrowsAsync<InvalidDataException>(() => setup.RestartAsync());
        Assert.AreEqual(99L, await ScalarInt64Async(setup.DatabasePath, "SELECT MAX(Version) FROM AssetLibrarySchemaInfo;"));

        await ExecuteAsync(setup.DatabasePath, "DELETE FROM AssetLibrarySchemaInfo WHERE Version=99;");
        var repository = new SqliteAssetLibraryRepository(setup.DatabasePath);
        await repository.InitializeAsync();
        var smartFolder = await repository.SaveSmartFolderQueryDocumentAsync(
            new(Guid.NewGuid(), "future query document"),
            new AssetQueryDocument { RootGroup = AssetQueryNode.Group(AssetQueryLogic.All) });
        await repository.DisposeAsync();
        await ExecuteAsync(
            setup.DatabasePath,
            "UPDATE SmartFolderQueryDocuments SET DocumentVersion=99, QueryJson=replace(QueryJson,'\"version\":1','\"version\":99') WHERE SmartFolderId=$id;",
            ("$id", smartFolder.SmartFolderId.ToString("D")));

        var futureDocumentRepository = new SqliteAssetLibraryRepository(setup.DatabasePath);
        await Assert.ThrowsAsync<InvalidDataException>(() => futureDocumentRepository.InitializeAsync());
        await futureDocumentRepository.DisposeAsync();
        Assert.AreEqual(99L, await ScalarInt64Async(
            setup.DatabasePath,
            "SELECT DocumentVersion FROM SmartFolderQueryDocuments WHERE SmartFolderId=$id;",
            ("$id", smartFolder.SmartFolderId.ToString("D"))));
    }

    [TestMethod]
    public async Task V6MigrationPinsUniqueArchivedNameReferenceButRuntimeStillTreatsItAsInvalid()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var referenced = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "迁移后已归档"));
        var smartFolderId = Guid.NewGuid();
        await setup.Repository.SaveSmartFolderAsync(
            new(smartFolderId, "旧名称引用"),
            [new(Guid.NewGuid(), smartFolderId, SmartFolderField.Folder, SmartFolderOperator.Equals, referenced.Name)]);
        await DowngradeToV6Async(setup.DatabasePath);
        await ExecuteAsync(
            setup.DatabasePath,
            "UPDATE AssetFolders SET IsArchived=1 WHERE FolderId=$id;",
            ("$id", referenced.FolderId.ToString("D")));

        await setup.RestartAsync();

        var migrated = await setup.Repository.GetSmartFolderQueryDocumentAsync(smartFolderId);
        Assert.IsNotNull(migrated);
        CollectionAssert.AreEqual(
            new[] { $"id:{referenced.FolderId:D}" },
            migrated.Document.RootGroup.Children.Single().Values.ToArray());
        var runtime = await setup.Repository.QueryAsync(new(SmartFolderId: smartFolderId, PageSize: 20));
        StringAssert.Contains(runtime.RegexError, "引用不存在或已归档");
        Assert.AreEqual(0, runtime.TotalCount);
    }

    [TestMethod]
    public async Task V6MigrationRejectsAmbiguousNameAcrossActiveAndArchivedEntities()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var referenced = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "迁移歧义"));
        var duplicate = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "迁移歧义备用"));
        var smartFolderId = Guid.NewGuid();
        await setup.Repository.SaveSmartFolderAsync(
            new(smartFolderId, "歧义旧名称引用"),
            [new(Guid.NewGuid(), smartFolderId, SmartFolderField.Folder, SmartFolderOperator.Equals, referenced.Name)]);
        await DowngradeToV6Async(setup.DatabasePath);
        await ExecuteAsync(
            setup.DatabasePath,
            "UPDATE AssetFolders SET Name=$name,IsArchived=1 WHERE FolderId=$id;",
            ("$id", duplicate.FolderId.ToString("D")),
            ("$name", referenced.Name));

        var reopening = new SqliteAssetLibraryRepository(setup.DatabasePath);
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => reopening.InitializeAsync());
        await reopening.DisposeAsync();

        StringAssert.Contains(failure.Message, "不唯一");
        Assert.AreEqual(6L, await ScalarInt64Async(setup.DatabasePath, "SELECT MAX(Version) FROM AssetLibrarySchemaInfo;"));
        Assert.AreEqual(0L, await ScalarInt64Async(
            setup.DatabasePath,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SmartFolderQueryDocuments';"));
    }

    private static async Task DowngradeToV6Async(string databasePath)
    {
        await ExecuteAsync(databasePath, """
            PRAGMA foreign_keys=OFF;
            DROP TABLE IF EXISTS SmartFolderQueryDocuments;
            DELETE FROM AssetLibrarySchemaInfo WHERE Version > 6;
            INSERT OR IGNORE INTO AssetLibrarySchemaInfo(Version,AppliedAt) VALUES(6,'2026-09-02T00:00:00.0000000+00:00');
            PRAGMA foreign_keys=ON;
            """);
    }

    private static async Task ExecuteAsync(string databasePath, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarInt64Async(
        string databasePath,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<(string QueryJson, string QueryHash, string? BackupJson)> ReadQueryRowAsync(string databasePath, Guid smartFolderId)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT QueryJson,QueryHash,LegacyRulesBackupJson FROM SmartFolderQueryDocuments WHERE SmartFolderId=$id;";
        command.Parameters.AddWithValue("$id", smartFolderId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync();
        Assert.IsTrue(await reader.ReadAsync());
        return (reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2));
    }
}
