using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Database;

if (args.Length != 3) throw new ArgumentException("database, app-data root and source file required");
var databasePath = Path.GetFullPath(args[0]);
var appDataRoot = Path.GetFullPath(args[1]);
var sourceFile = Path.GetFullPath(args[2]);
var database = new PixelTartDatabase(databasePath);
await using var connection = await database.OpenConnectionAsync();
await using var version = connection.CreateCommand();
version.CommandText = "SELECT MAX(Version) FROM SchemaInfo;";
var schemaVersion = Convert.ToInt32(await version.ExecuteScalarAsync());
await using var integrity = connection.CreateCommand();
integrity.CommandText = "PRAGMA integrity_check;";
var integrityResult = Convert.ToString(await integrity.ExecuteScalarAsync()) ?? string.Empty;
var projects = await new SqliteProjectRepository(database).ListAsync();
var quickTools = await new SqliteQuickToolsRepository(database).LoadAsync();
var settings = Path.Combine(appDataRoot, "settings.json");
var oldJson = Path.Combine(appDataRoot, "Projects", "projects.json");
var backups = Directory.Exists(Path.Combine(appDataRoot, "Backups")) ? Directory.GetFiles(Path.Combine(appDataRoot, "Backups"), "*", SearchOption.AllDirectories) : [];
Console.WriteLine(JsonSerializer.Serialize(new
{
    Passed = schemaVersion == 2 && string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase) && projects.Count > 0 && quickTools.Count > 0 && File.Exists(settings) && File.Exists(oldJson) && backups.Length > 0 && File.Exists(sourceFile),
    SchemaVersion = schemaVersion,
    IntegrityCheck = integrityResult,
    ProjectCount = projects.Count,
    QuickTools = quickTools,
    SettingsRetained = File.Exists(settings),
    OldJsonRetained = File.Exists(oldJson),
    MigrationBackupCount = backups.Length,
    SourceSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(sourceFile)))
}));
