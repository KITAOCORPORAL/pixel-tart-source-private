using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP2AutomatedEvidenceContractTests
{
    private static readonly string[] Scenarios =
    [
        "fixture-integrity/v1", "organization-browser/v1", "smart-tag-query/v1",
        "four-views-query-sort/v1", "selection-large/v1", "metadata-drag-command/v1",
        "inspector-states/v1", "resilience-states/v1", "restart-persistence/v1",
        "layout-dpi-performance/v1"
    ];

    private static readonly string[] NegativeFixtures =
    [
        "missing-screenshot", "mutated-hash", "wrong-scenario-order", "fixture-count-mismatch",
        "fixture-content-hash-mismatch", "fixture-display-name-not-chinese", "fixture-missing-count-mismatch",
        "fixture-visual-outcome-mismatch", "fixture-schema-marker-mismatch",
        "fixture-path-escape", "folder-cycle-accepted", "duplicate-automation-id", "smart-result-mismatch",
        "query-plan-divergence", "stale-cancelled-query", "view-state-lost", "virtualization-realizes-all",
        "sort-unstable", "selection-truncated", "invalid-drop-accepted", "undo-mismatch",
        "prohibited-command-present", "inspector-mode-mismatch", "restart-identity-reused", "dpi-overflow",
        "performance-threshold-exceeded", "ui-block-exceeded", "user-source-write", "eagle-write",
        "residual-process", "database-not-v6", "cross-run-splice", "runner-session-splice",
        "process-session-splice", "binary-hash-mismatch", "input-tree-mutated"
    ];

    [TestMethod]
    public void ContractIsIndependentP2FailClosedContract()
    {
        using var document = JsonDocument.Parse(Read("tools/AssetLibraryP2AutomatedAcceptance/automated-acceptance-contract.json"));
        var root = document.RootElement;
        Assert.AreEqual("pixel-tart-asset-library-p2-automated-acceptance-contract/v1", root.GetProperty("schema").GetString());
        Assert.AreEqual("automated", root.GetProperty("validation_mode").GetString());
        Assert.AreEqual("waived", root.GetProperty("owner_manual_ux_smoke").GetString());
        Assert.IsFalse(root.GetProperty("manual_evidence_claimed").GetBoolean());
        Assert.AreEqual(11, root.GetProperty("required_runner_session_count").GetInt32());
        CollectionAssert.AreEqual(Scenarios, root.GetProperty("required_scenario_order").EnumerateArray().Select(item => item.GetString()).ToArray());
    }

    [TestMethod]
    public void FixtureAndDpiAndThresholdsAreFixed()
    {
        using var document = JsonDocument.Parse(Read("tools/AssetLibraryP2AutomatedAcceptance/automated-acceptance-contract.json"));
        var root = document.RootElement;
        var fixture = root.GetProperty("fixture");
        Assert.AreEqual(512, fixture.GetProperty("total_count").GetInt32());
        Assert.AreEqual(500, fixture.GetProperty("active_count").GetInt32());
        Assert.AreEqual(12, fixture.GetProperty("archived_count").GetInt32());
        Assert.AreEqual(6, fixture.GetProperty("schema_version").GetInt32());
        Assert.AreEqual(512, fixture.GetProperty("display_name_count").GetInt32());
        Assert.AreEqual("zh-CN", fixture.GetProperty("display_name_language").GetString());
        Assert.AreEqual(512, fixture.GetProperty("content_hash_count").GetInt32());
        Assert.AreEqual("sha256", fixture.GetProperty("content_hash_algorithm").GetString());
        Assert.IsTrue(fixture.GetProperty("content_hash_deterministic").GetBoolean());
        Assert.AreEqual(32, fixture.GetProperty("missing_count").GetInt32());
        var visual = fixture.GetProperty("visual_feature_counts");
        Assert.AreEqual("visual-analysis-v2", visual.GetProperty("analysis_version").GetString());
        Assert.AreEqual(128, visual.GetProperty("valid").GetInt32());
        Assert.AreEqual(64, visual.GetProperty("failed").GetInt32());
        Assert.AreEqual(320, visual.GetProperty("not_analyzed").GetInt32());
        Assert.AreEqual(192, visual.GetProperty("feature_rows").GetInt32());
        var dpi = root.GetProperty("required_dpi_matrix").EnumerateArray()
            .Select(item => (item.GetProperty("width").GetInt32(), item.GetProperty("height").GetInt32(), item.GetProperty("scale_percent").GetInt32())).ToArray();
        CollectionAssert.AreEqual(new[] { (1366, 768, 100), (1920, 1080, 125), (1920, 1080, 150), (2560, 1440, 175) }, dpi);
        var limits = root.GetProperty("performance_thresholds_ms");
        CollectionAssert.AreEqual(new[] { 1500, 250, 350, 250, 750, 100 }, limits.EnumerateObject().Select(item => item.Value.GetInt32()).ToArray());
    }

    [TestMethod]
    public void EveryNegativeFixtureHasAnExplicitValidatorGuard()
    {
        using var document = JsonDocument.Parse(Read("tools/AssetLibraryP2AutomatedAcceptance/automated-acceptance-contract.json"));
        CollectionAssert.AreEqual(NegativeFixtures, document.RootElement.GetProperty("required_negative_fixtures").EnumerateArray().Select(item => item.GetString()).ToArray());
        var validator = Read("tools/AssetLibraryP2AutomatedAcceptance/Test-P2AssetLibraryAutomatedEvidence.ps1");
        foreach (var fixture in NegativeFixtures)
            StringAssert.Contains(validator, $"'{fixture}'=");
        StringAssert.Contains(validator, "negative fixture list has no exact validator guard map");
    }

    [TestMethod]
    public void RunnerAndValidatorPowerShellParseWithoutErrors()
    {
        foreach (var relative in new[]
                 {
                     "tools/AssetLibraryP2AutomatedAcceptance/Invoke-P2AssetLibraryAutomatedAcceptance.ps1",
                     "tools/AssetLibraryP2AutomatedAcceptance/Test-P2AssetLibraryAutomatedEvidence.ps1"
                 })
        {
            var escapedPath = Path(relative).Replace("'", "''", StringComparison.Ordinal);
            var script = $"$e=$null;[Management.Automation.Language.Parser]::ParseFile('{escapedPath}',[ref]$null,[ref]$e)|Out-Null;if(@($e).Count){{$e|% Message;exit 1}}";
            using var process = Process.Start(new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", script }
            })!;
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode, $"{relative}: {process.StandardOutput.ReadToEnd()} {process.StandardError.ReadToEnd()}");
        }
    }

    [TestMethod]
    public void RunnerExposesFourModesAndOneRestartOnly()
    {
        var runner = Read("tools/AssetLibraryP2AutomatedAcceptance/Invoke-P2AssetLibraryAutomatedAcceptance.ps1");
        StringAssert.Contains(runner, "[ValidateSet('Run', 'DryRun', 'ValidateExistingRun', 'RecoveryTest')]");
        StringAssert.Contains(runner, "$restartScenarios = @('restart-persistence/v1')");
        StringAssert.Contains(runner, "New-P2SyntheticFixture");
        StringAssert.Contains(runner, "Test-P2AssetLibraryAutomatedEvidence.ps1");
    }

    [TestMethod]
    public void FixtureGeneratorCreatesDeterministic512MetadataContract()
    {
        var runner = Read("tools/AssetLibraryP2AutomatedAcceptance/Invoke-P2AssetLibraryAutomatedAcceptance.ps1");
        ContainsAll(runner, "range(512)", "index >= 500", "asset-library-v16.db", "(512, 500, 12)",
            "AssetFolders", "AssetFolderMemberships", "AssetTags", "AssetTagMemberships", "SmartFolders", "SmartFolderRules",
            "uuid.uuid5", "user_source_read_count = 0", "user_source_write_count = 0");
    }

    [TestMethod]
    public void ValidatorIsReadOnlyAndChecksInputTreeFingerprint()
    {
        var validator = Read("tools/AssetLibraryP2AutomatedAcceptance/Test-P2AssetLibraryAutomatedEvidence.ps1");
        ContainsAll(validator, "$fingerprintBefore = Tree-Fingerprint $root", "$fingerprintAfter = Tree-Fingerprint $root",
            "input tree fingerprint", "Get-FileHash", "runtime_database_count_after", "display settings unchanged");
        foreach (var forbidden in new[] { "Set-Content", "Add-Content", "Out-File", "Remove-Item", "Move-Item", "Copy-Item", "WriteAllText", "WriteAllBytes" })
            Assert.IsFalse(validator.Contains(forbidden, StringComparison.OrdinalIgnoreCase), $"validator contains mutator {forbidden}");
    }

    [TestMethod]
    public void P2PackageDoesNotDriveDesktopOrEagle()
    {
        var package = string.Join("\n", Directory.GetFiles(Path("tools/AssetLibraryP2AutomatedAcceptance"), "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        var driver = Read("src/PixelTart.Modules.AssetLibrary/AssetLibraryP2AutomatedAcceptanceDriver.cs");
        foreach (var forbidden in new[] { "SendInput(", "mouse_event(", "keybd_event(", "SetForegroundWindow(", "ChangeDisplaySettings", "AutomationElement", "InvokePattern", "Eagle.exe" })
        {
            Assert.IsFalse(package.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(driver.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void ContainsAll(string text, params string[] values)
    {
        foreach (var value in values) StringAssert.Contains(text, value);
    }

    private static string Read(string relative) => File.ReadAllText(Path(relative));
    private static string Path(string relative) => System.IO.Path.Combine(RepositoryRoot(), relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
    private static string RepositoryRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null && !File.Exists(System.IO.Path.Combine(cursor.FullName, "RAWSelectionAssistant.sln"))) cursor = cursor.Parent;
        return cursor?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
