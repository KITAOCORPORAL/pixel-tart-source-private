using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;

#pragma warning disable MSTEST0037

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version220CalendarSchemaTests
{
    [TestMethod]
    public async Task DefaultMigration_UpgradesToSchemaVersionTwo()
    {
        using var setup = await SetupAsync();
        await using var connection = await setup.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(Version) FROM SchemaInfo;";
        Assert.AreEqual(2L, (long)(await command.ExecuteScalarAsync())!);
    }

    [TestMethod]
    public async Task SchemaTwo_AddsExactlyFourBookingTables()
    {
        using var setup = await SetupAsync();
        var expected = new[] { "BookingDocuments", "BookingReminders", "ShootBookings", "ShootRequirementItems" };
        await using var connection = await setup.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('ShootBookings','ShootRequirementItems','BookingDocuments','BookingReminders','ProjectRelationships') ORDER BY name;";
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) names.Add(reader.GetString(0));
        CollectionAssert.AreEqual(expected, names.ToArray());
    }

    [TestMethod]
    public async Task SchemaTwo_DoesNotCreateProjectRelationships()
    {
        using var setup = await SetupAsync();
        await using var connection = await setup.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ProjectRelationships';";
        Assert.AreEqual(0L, (long)(await command.ExecuteScalarAsync())!);
    }

    [TestMethod]
    public void ProjectRelationshipInterface_HasNoConcreteImplementation()
    {
        Assert.IsFalse(typeof(IProjectRelationshipService).Assembly.GetTypes().Any(type => type.IsClass && !type.IsAbstract && typeof(IProjectRelationshipService).IsAssignableFrom(type)));
    }

    [TestMethod]
    public async Task SchemaOneToTwo_CreatesMigrationBackup()
    {
        using var temp = new TempDirectory();
        var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        var backupRoot = temp.Combine("backups");
        Assert.IsTrue((await new DatabaseMigrator(database, new DatabaseBackupService(database, backupRoot), [new InitialSchemaMigration()]).MigrateAsync()).Success);
        var result = await new DatabaseMigrator(database, new DatabaseBackupService(database, backupRoot)).MigrateAsync();
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.PreviousVersion);
        Assert.AreEqual(2, result.CurrentVersion);
        Assert.IsNotNull(result.BackupPath);
        Assert.IsTrue(File.Exists(result.BackupPath));
    }

    [TestMethod]
    public async Task FailedSchemaTwoMigration_RollsBackEveryCreatedObject()
    {
        using var temp = new TempDirectory();
        var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        var backup = new DatabaseBackupService(database, temp.Combine("backups"));
        Assert.IsTrue((await new DatabaseMigrator(database, backup, [new InitialSchemaMigration()]).MigrateAsync()).Success);
        var result = await new DatabaseMigrator(database, backup, [new InitialSchemaMigration(), new FailingCalendarMigration()]).MigrateAsync();
        Assert.IsFalse(result.Success);
        var reopened = new PixelTartDatabase(database.DatabasePath);
        await using var connection = await reopened.OpenConnectionAsync();
        await using var table = connection.CreateCommand();
        table.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ShootBookings';";
        Assert.AreEqual(0L, (long)(await table.ExecuteScalarAsync())!);
        await using var version = connection.CreateCommand();
        version.CommandText = "SELECT MAX(Version) FROM SchemaInfo;";
        Assert.AreEqual(1L, (long)(await version.ExecuteScalarAsync())!);
    }

    [TestMethod]
    public async Task ChildForeignKeys_RestrictParentDeletion()
    {
        using var setup = await SetupAsync();
        var repository = new SqliteShootBookingRepository(setup.Database);
        var booking = Booking();
        await repository.SaveAsync(booking, [new ShootRequirementItem { BookingId = booking.Id, ItemText = "准备电池" }]);
        await using var connection = await setup.Database.OpenConnectionAsync(write: true);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ShootBookings WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", booking.Id.ToString("D"));
        await Assert.ThrowsExactlyAsync<SqliteException>(() => command.ExecuteNonQueryAsync());
        Assert.IsNotNull(await repository.GetAsync(booking.Id));
    }

    [TestMethod]
    public async Task DatabaseAllowsPaidAmountAboveTotal()
    {
        using var setup = await SetupAsync();
        var repository = new SqliteShootBookingRepository(setup.Database);
        var booking = Booking() with { TotalAmountMinor = 10_000, PaidAmountMinor = 12_000 };
        await repository.SaveAsync(booking, []);
        Assert.AreEqual(12_000, (await repository.GetAsync(booking.Id))!.PaidAmountMinor);
    }

    [TestMethod]
    public async Task DatabaseRejectsNegativeMoneyFields()
    {
        using var setup = await SetupAsync();
        var repository = new SqliteShootBookingRepository(setup.Database);
        await Assert.ThrowsExactlyAsync<SqliteException>(() => repository.SaveAsync(Booking() with { PaidAmountMinor = -1 }, []));
    }

    [TestMethod]
    public async Task CalendarTablesContainNoBlobColumns()
    {
        using var setup = await SetupAsync();
        await using var connection = await setup.Database.OpenConnectionAsync();
        foreach (var tableName in new[] { "ShootBookings", "ShootRequirementItems", "BookingDocuments", "BookingReminders" })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName});";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) Assert.AreNotEqual("BLOB", reader.GetString(2).ToUpperInvariant());
        }
    }

    [TestMethod]
    public void BookingRepositoryContract_HasNoPermanentDeleteMethod()
    {
        var methods = typeof(IShootBookingRepository).GetMethods().Select(method => method.Name).ToArray();
        Assert.IsFalse(methods.Any(name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase) || name.Contains("Purge", StringComparison.OrdinalIgnoreCase)));
    }

    private static ShootBooking Booking() => new()
    {
        Title = "阶段A测试", ClientDisplayName = "客户", StartAtUtc = DateTimeOffset.UtcNow.AddHours(1), EndAtUtc = DateTimeOffset.UtcNow.AddHours(2), ShootingType = "Portrait"
    };

    private static async Task<Setup> SetupAsync()
    {
        var temp = new TempDirectory();
        var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        var result = await new DatabaseMigrator(database, new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync();
        Assert.IsTrue(result.Success, result.ErrorMessage);
        return new(temp, database);
    }

    private sealed class FailingCalendarMigration : IMigration
    {
        public int Version => 2;
        public string Name => "FailingCalendar";
        public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "CREATE TABLE ShootBookings(Id TEXT PRIMARY KEY);";
            await command.ExecuteNonQueryAsync(cancellationToken);
            throw new InvalidOperationException("forced rollback");
        }
    }

    private sealed class Setup(TempDirectory temp, PixelTartDatabase database) : IDisposable
    {
        public PixelTartDatabase Database { get; } = database;
        public void Dispose() { SqliteConnection.ClearAllPools(); temp.Dispose(); }
    }
}
