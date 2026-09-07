#if ASSET_LIBRARY_P3_AUTOMATED_ACCEPTANCE
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP3DatabaseEvidenceBehaviorTests
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    [TestMethod]
    public async Task RealSnapshotWriterContractsPassActualValidatorBlockAndCorruptHistoriesFailClosed()
    {
        var repo = RepositoryRoot();
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P3DatabaseContract", Guid.NewGuid().ToString("N"));
        var fixture = Path.Combine(root, "fixture");
        Directory.CreateDirectory(fixture);
        var source = Path.Combine(fixture, "asset-library-v16.db");
        var python = Environment.GetEnvironmentVariable("PIXEL_TART_TEST_PYTHON") ?? "python.exe";
        var generated = await Start(python, [Path.Combine(repo, "tools/AssetLibraryP3AutomatedAcceptance/New-P3SyntheticFixture.py"), fixture, source, Path.Combine(fixture, "asset-library-v16-legacy-v6.db")]);
        Assert.AreEqual(0, generated.Code, generated.Output + generated.Error);
        var controller = typeof(RAWSelectionAssistant.MainWindow).Assembly.GetType("RAWSelectionAssistant.Services.AssetLibraryP3AutomatedAcceptanceController", throwOnError: true)!;
        var scenarioType = controller.GetNestedType("ScenarioState", BindingFlags.NonPublic)!;
        var snapshotWriter = controller.GetMethod("WriteDatabaseEvidenceSnapshotAsync", Hidden)!;
        var cases = new JsonArray();
        var restarts = new[] { "search-suggestions-history/v1", "smart-folder-lifecycle-preview/v1", "bulk-metadata-journal/v1" };
        foreach (var id in new[] { "scope-switch/v1" }.Concat(restarts))
        {
            var scenarioRoot = Path.Combine(root, "runtime", id.Replace('/', '-'));
            Directory.CreateDirectory(scenarioRoot);
            var active = Path.Combine(scenarioRoot, "active.db");
            File.Copy(source, active);
            object NewState() => Activator.CreateInstance(scenarioType, Hidden, null, [id, 1L], null)!;
            var state = NewState();
            object Database(object value) => scenarioType.GetProperty("Database", Hidden)!.GetValue(value)!;
            var database = Database(state);
            var databaseType = database.GetType();
            void Set(string property, object value) => databaseType.GetProperty(property, Hidden)!.SetValue(database, value);
            Set("Path", active);
            Set("RealRepository", true);
            foreach (var phase in restarts.Contains(id) ? new[] { "primary", "restart" } : new[] { "primary" })
            {
                if (phase == "restart")
                {
                    // Exercise the actual persisted primary -> restart loader.
                    var prior = JsonSerializer.SerializeToElement(databaseType.GetMethod("ToContract", Hidden)!.Invoke(database, null));
                    state = NewState(); database = Database(state);
                    databaseType.GetMethod("Load", Hidden)!.Invoke(database, [prior]);
                }
                var relative = $"app/evidence/databases/{id.Replace('/', '-')}-{phase}.db";
                var absolute = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                await (Task)snapshotWriter.Invoke(null, [active, absolute, state])!;
                var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(absolute))).ToLowerInvariant();
                databaseType.GetMethod("RecordEvidence", Hidden)!.Invoke(database, [relative, absolute, hash]);
                Assert.IsFalse(File.Exists(active + "-wal") || File.Exists(active + "-shm"));
                Assert.IsFalse(File.Exists(absolute + "-wal") || File.Exists(absolute + "-shm"));
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await (Task)snapshotWriter.Invoke(null, [active, absolute, state])!);
            }
            var contract = JsonSerializer.SerializeToNode(databaseType.GetMethod("ToContract", Hidden)!.Invoke(database, null))!;
            JsonObject Case(string name, JsonNode db, bool pass) => new() { ["name"] = name, ["pass"] = pass, ["scenario"] = new JsonObject { ["id"] = id, ["scenario_root"] = scenarioRoot, ["database"] = db } };
            cases.Add(Case(id + ":positive", contract.DeepClone(), true));
            if (!restarts.Contains(id))
            {
                var legacy = contract.DeepClone().AsObject(); legacy.Remove("evidence_paths");
                var restored = NewState();
                databaseType.GetMethod("Load", Hidden)!.Invoke(Database(restored), [JsonSerializer.SerializeToElement(legacy)]);
                var upgraded = JsonSerializer.SerializeToNode(databaseType.GetMethod("ToContract", Hidden)!.Invoke(Database(restored), null))!;
                cases.Add(Case("legacy-primary-history-loader", upgraded, true));
            }
            foreach (var mutation in new[] { "missing", "reversed", "duplicate", "cross-scene", "cross-run", "hash", "escape", "schema", "wal", "shm" })
            {
                if (mutation == "reversed" && !restarts.Contains(id)) continue;
                var changed = contract.DeepClone();
                var history = changed["evidence_paths"]!.AsArray();
                switch (mutation)
                {
                    case "missing": history.RemoveAt(history.Count - 1); break;
                    case "reversed": changed["evidence_paths"] = new JsonArray(history.Reverse().Select(node => node!.DeepClone()).ToArray()); break;
                    case "duplicate": history.Add(history[0]!.DeepClone()); break;
                    case "cross-scene": history[0] = "app/evidence/databases/another-scene-primary.db"; break;
                    case "cross-run": changed["absolute_path"] = Path.Combine(Path.GetDirectoryName(root)!, "another-run", "snapshot.db"); break;
                    case "hash": changed["sha256"] = new string('0', 64); break;
                    case "escape": changed["path"] = "../outside.db"; break;
                    case "schema": changed["schema_version"] = 6; break;
                    case "wal": changed["wal_present_after_close"] = true; break;
                    case "shm": changed["shm_present_after_close"] = true; break;
                }
                cases.Add(Case(id + ":" + mutation, changed, false));
            }
        }
        var casesPath = Path.Combine(root, "cases.json");
        await File.WriteAllTextAsync(casesPath, cases.ToJsonString());
        var validator = await File.ReadAllTextAsync(Path.Combine(repo, "tools/AssetLibraryP3AutomatedAcceptance/Test-P3AssetLibraryAutomatedEvidence.ps1"));
        var definitions = validator[..validator.IndexOf("$root = Full $RunRoot", StringComparison.Ordinal)];
        var start = validator.IndexOf("    $isLegacyMigration = $scenario.id", StringComparison.Ordinal);
        var end = validator.IndexOf("    if (@($scenario.screenshot_paths)", start, StringComparison.Ordinal);
        Assert.IsTrue(start > 0 && end > start);
        var harness = definitions + """
            $root = Full $RunRoot
            $databaseEvidenceRoot = Full (Join-Path $root 'app/evidence/databases')
            $expectedRestarts = @('search-suggestions-history/v1','smart-folder-lifecycle-preview/v1','bulk-metadata-journal/v1')
            $index = 0
            $results = foreach ($case in (Get-Content -LiteralPath (Join-Path $root 'cases.json') -Raw | ConvertFrom-Json)) {
                $scenario = $case.scenario
                $scenarioRoot = Full $scenario.scenario_root
                $accepted = $false
                $errorText = ''
                try {
            """ + validator[start..end] + """
                    $accepted = $true
                } catch { $errorText = $_.Exception.Message }
                if ($accepted -ne [bool]$case.pass) { throw "Contract case failed: $($case.name); $errorText" }
                [pscustomobject]@{ name=$case.name; accepted=$accepted; rejection=$errorText }
            }
            $results | ConvertTo-Json -Depth 10
            """;
        var script = Path.Combine(root, "validator-block-tests.ps1");
        await File.WriteAllTextAsync(script, harness, new UTF8Encoding(true));
        var result = await Start("powershell.exe", ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, "-RunRoot", root]);
        await File.WriteAllTextAsync(Path.Combine(root, "validator-results.json"), result.Output);
        Assert.AreEqual(0, result.Code, root + "\n" + result.Output + result.Error);
        Assert.AreEqual(44, JsonNode.Parse(result.Output)!.AsArray().Count);
    }

    private static string RepositoryRoot()
    {
        for (var cursor = new DirectoryInfo(AppContext.BaseDirectory); cursor is not null; cursor = cursor.Parent)
            if (File.Exists(Path.Combine(cursor.FullName, "RAWSelectionAssistant.sln"))) return cursor.FullName;
        throw new DirectoryNotFoundException();
    }

    private static async Task<(int Code, string Output, string Error)> Start(string executable, string[] arguments)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(3));
        return (process.ExitCode, await output, await error);
    }
}
#endif
