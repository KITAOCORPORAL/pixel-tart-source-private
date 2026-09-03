using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP3AutomatedEvidenceContractTests
{
    private static readonly string[] Scenarios =
    [
        "scope-switch/v1", "ime-cancellation/v1", "search-suggestions-history/v1",
        "folder-any-all-not/v1", "tag-any-all-not/v1", "scalar-null-composition/v1",
        "visual-composition/v1", "nested-canonical-query/v1", "invalid-query-fail-closed/v1",
        "smart-folder-lifecycle-preview/v1", "smart-folder-invalid-migration/v1",
        "tag-manager-lifecycle/v1", "bulk-metadata-journal/v1", "four-view-resilience-layout/v1"
    ];

    private static readonly string[] Restarts =
    [
        "search-suggestions-history/v1", "smart-folder-lifecycle-preview/v1", "bulk-metadata-journal/v1"
    ];

    private static readonly string[] EvidenceKinds =
    [
        "screenshots", "bounds", "query-documents", "query-plans", "result-hashes", "histories",
        "smart-folders", "tags", "memberships", "journals", "commands", "selections", "views",
        "performance", "databases"
    ];

    private static readonly string[] NegativeFixtures =
    [
        "missing-screenshot", "mutated-hash", "wrong-scenario-order", "wrong-restart-order",
        "fixture-count-mismatch", "fixture-content-hash-mismatch", "fixture-schema-marker-mismatch",
        "fixture-path-escape", "legacy-fixture-missing", "duplicate-automation-id",
        "canonical-query-hash-mismatch", "query-result-hash-mismatch", "query-plan-parameter-mismatch",
        "unparameterized-sql", "scope-result-mismatch", "stale-cancelled-query",
        "search-history-not-persisted", "folder-any-all-not-mismatch", "tag-any-all-not-mismatch",
        "scalar-null-mismatch", "visual-query-mismatch", "nested-query-mismatch", "invalid-query-expanded",
        "smart-folder-roundtrip-mismatch", "smart-folder-invalid-ref-expanded",
        "smart-folder-migration-mismatch", "tag-merge-membership-duplicate", "tag-group-cycle-accepted",
        "batch-partial-commit", "journal-chain-mismatch", "undo-redo-mismatch", "restart-identity-reused",
        "view-result-divergence", "selection-hash-divergence", "dpi-overflow", "contrast-threshold-failed",
        "accessibility-identity-missing", "performance-threshold-exceeded", "ui-block-exceeded",
        "user-source-write", "eagle-write", "network-upload", "permanent-delete", "residual-process",
        "database-not-v7", "cross-run-splice", "runner-session-splice", "process-session-splice",
        "binary-hash-mismatch", "input-tree-mutated"
    ];

    [TestMethod]
    public void ContractFixesFourteenScenariosThreeRestartsAndSeventeenSessions()
    {
        using var document = Contract();
        var root = document.RootElement;
        Assert.AreEqual("pixel-tart-asset-library-p3-automated-acceptance-contract/v1", root.GetProperty("schema").GetString());
        Assert.AreEqual("automated", root.GetProperty("validation_mode").GetString());
        Assert.AreEqual("waived", root.GetProperty("owner_manual_ux_smoke").GetString());
        Assert.IsFalse(root.GetProperty("manual_evidence_claimed").GetBoolean());
        Assert.AreEqual(17, root.GetProperty("required_runner_session_count").GetInt32());
        CollectionAssert.AreEqual(Scenarios, Strings(root.GetProperty("required_scenario_order")));
        CollectionAssert.AreEqual(Restarts, Strings(root.GetProperty("required_restart_scenarios")));
    }

    [TestMethod]
    public void ContractFixesCurrentAndLegacyFixtures()
    {
        using var document = Contract();
        var root = document.RootElement;
        Assert.AreEqual(7, root.GetProperty("repository").GetProperty("schema_version").GetInt32());
        Assert.AreEqual(6, root.GetProperty("repository").GetProperty("legacy_schema_version").GetInt32());
        var fixture = root.GetProperty("fixture");
        CollectionAssert.AreEqual(new[] { 7, 10128, 10000, 128, 10128, 10128, 512 }, new[]
        {
            fixture.GetProperty("schema_version").GetInt32(), fixture.GetProperty("total_count").GetInt32(),
            fixture.GetProperty("active_count").GetInt32(), fixture.GetProperty("archived_count").GetInt32(),
            fixture.GetProperty("display_name_count").GetInt32(), fixture.GetProperty("content_hash_count").GetInt32(),
            fixture.GetProperty("missing_count").GetInt32()
        });
        var visual = fixture.GetProperty("visual_feature_counts");
        CollectionAssert.AreEqual(new[] { 3072, 1024, 6032, 4096 }, new[]
        {
            visual.GetProperty("valid").GetInt32(), visual.GetProperty("failed").GetInt32(),
            visual.GetProperty("not_analyzed").GetInt32(), visual.GetProperty("feature_rows").GetInt32()
        });
        var legacy = fixture.GetProperty("legacy_variant");
        CollectionAssert.AreEqual(new[] { 6, 64, 60, 4 }, new[]
        {
            legacy.GetProperty("schema_version").GetInt32(), legacy.GetProperty("total_count").GetInt32(),
            legacy.GetProperty("active_count").GetInt32(), legacy.GetProperty("archived_count").GetInt32()
        });
    }

    [TestMethod]
    public void EvidenceDpiAndPerformanceContractsAreExact()
    {
        using var document = Contract();
        var root = document.RootElement;
        CollectionAssert.AreEqual(EvidenceKinds, Strings(root.GetProperty("required_evidence_kinds")));
        CollectionAssert.AreEqual(new[] { 100, 125, 150, 200 }, root.GetProperty("required_dpi_matrix")
            .EnumerateArray().Select(item => item.GetProperty("scale_percent").GetInt32()).ToArray());
        var limits = root.GetProperty("performance_thresholds_ms");
        CollectionAssert.AreEqual(
            new[] { "first_screen_10000", "search_suggestion", "single_filter_update", "nested_8_rule_query", "smart_folder_preview", "scope_switch", "batch_tag_100", "batch_tag_500", "ui_block" },
            limits.EnumerateObject().Select(item => item.Name).ToArray());
        CollectionAssert.AreEqual(new[] { 1500, 200, 300, 600, 750, 400, 750, 2000, 100 },
            limits.EnumerateObject().Select(item => item.Value.GetInt32()).ToArray());
    }

    [TestMethod]
    public void TagManagerLifecycleValidatorRequiresEveryPublicCommandTransitionAndMergeInvariant()
    {
        var writer = Read("src/RAWSelectionAssistant/MainWindow.AssetLibraryP3AutomatedAcceptance.cs");
        var validator = Read("tools/AssetLibraryP3AutomatedAcceptance/Test-P3AssetLibraryAutomatedEvidence.ps1");
        var fields = new[]
        {
            "group_create_command_changed_state", "group_rename_command_changed_state",
            "group_reorder_command_changed_state", "group_order_count",
            "group_order_before_sha256", "group_order_after_sha256",
            "tag_create_command_changed_state", "tag_rename_command_changed_state",
            "rename_command_changed_state", "rename_preserved_memberships",
            "tag_reorder_command_changed_state", "tag_order_count",
            "tag_order_before_sha256", "tag_order_after_sha256",
            "tag_move_command_changed_state", "tag_original_group_id", "tag_moved_group_id",
            "tag_archive_command_changed_state", "tag_restore_command_changed_state",
            "archive_restore_preserved_memberships", "merge_source_membership_count_before",
            "merge_target_membership_count_before", "merge_overlap_count_before",
            "merge_source_membership_count_after", "merge_target_membership_count_after",
            "merge_duplicate_membership_count", "merge_source_archived",
            "merge_memberships_deduplicated", "group_cycle_rejected", "group_cycle_proof"
        };
        foreach (var field in fields)
        {
            StringAssert.Contains(writer, field, $"Evidence writer omitted {field}.");
            StringAssert.Contains(validator, $"'{field}'", $"Validator omitted {field}.");
        }
        ContainsAll(writer, "pixel-tart-p3-tag-manager-lifecycle/v2",
            "public-flat-group-order-and-tag-reference-contract", "flat-no-parent-reference");
        ContainsAll(validator, "pixel-tart-p3-tag-manager-lifecycle/v2",
            "public-flat-group-order-and-tag-reference-contract", "flat-no-parent-reference",
            "$mergeOverlapBefore -le 0", "$mergeSourceAfter -ne 0",
            "$mergeTargetAfter -ne ($mergeSourceBefore + $mergeTargetBefore - $mergeOverlapBefore)",
            "$groupOrderBeforeHash -ceq $groupOrderAfterHash", "$tagOrderBeforeHash -ceq $tagOrderAfterHash");
    }

    [TestMethod]
    public void EveryNegativeFixtureHasAnExplicitValidatorGuard()
    {
        using var document = Contract();
        CollectionAssert.AreEqual(NegativeFixtures, Strings(document.RootElement.GetProperty("required_negative_fixtures")));
        var validator = Read("tools/AssetLibraryP3AutomatedAcceptance/Test-P3AssetLibraryAutomatedEvidence.ps1");
        var harness = Read("tools/AssetLibraryP3AutomatedAcceptance/Invoke-P3NegativeEvidenceProofs.py");
        foreach (var fixture in NegativeFixtures) StringAssert.Contains(validator, $"'{fixture}'=");
        StringAssert.Contains(validator, "negative fixture list has no exact validator guard map");
        foreach (var fixture in NegativeFixtures) StringAssert.Contains(harness, $"\"{fixture}\"");
        ContainsAll(validator, "function Invoke-NegativeEvidenceProofs",
            "runner\\acceptance-inputs\\Invoke-P3NegativeEvidenceProofs.py",
            "negative proof workspace must be a sibling outside the sealed run root",
            "negative evidence proof recomputed hash", "Invoke-NegativeEvidenceProofs $root $negativeNames",
            "negative_fixture_proof_count");
        Assert.IsFalse(validator.Contains("Invoke-NegativeFixtureProbe", StringComparison.Ordinal));
        Assert.IsFalse(validator.Contains("isolated in-memory mutation", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RealNegativeHarnessClonesMutatesResealsAndCallsTheNormalValidator()
    {
        var harnessPath = Path("tools/AssetLibraryP3AutomatedAcceptance/Invoke-P3NegativeEvidenceProofs.py");
        var harness = File.ReadAllText(harnessPath);
        var syntax = Start("python.exe", ["-I", "-c",
            "import ast,pathlib,sys; source=pathlib.Path(sys.argv[1]).read_text(encoding='utf-8'); ast.parse(source); compile(source,sys.argv[1],'exec')",
            harnessPath]);
        Assert.AreEqual(0, syntax.ExitCode, syntax.Output + syntax.Error);
        ContainsAll(harness,
            "shutil.copytree(original, mutant)", "rebase(mutant, original)",
            "baseline = run_validator(mutant)", "rebased negative baseline did not validate",
            "passed-negative-baseline", "negative_proofs_skipped", "negative_fixture_proof_count",
            "changed = mutate(mutant, name)", "reseal(self.root)", "result = run_validator(mutant)",
            "negative mutation was accepted", "sqlite3.connect", ".unlink()", ".write_bytes(",
            "Test-P3AssetLibraryAutomatedEvidence.ps1", "-SkipNegativeProofs",
            "P3 automated evidence rejected");
        foreach (var fixture in NegativeFixtures) StringAssert.Contains(harness, $"\"{fixture}\"");
        Assert.IsFalse(harness.Contains("in-memory", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ValidatorRecomputesProductionJournalHashesFromExactUtf8Prefixes()
    {
        var validator = Read("tools/AssetLibraryP3AutomatedAcceptance/Test-P3AssetLibraryAutomatedEvidence.ps1");
        ContainsAll(validator,
            "function Get-JournalCanonicalText", "production terminal hash layout",
            "Sha256Text $journalHash.canonical", "event recomputed record hash",
            "summary journal recomputed record hash", "previous record hash alias",
            "final summary journal embedded summary binding");
        Assert.IsFalse(Regex.IsMatch(validator,
            @"Require-String \$event\.event_hash[^\r\n]+\r?\n\s*Require-Equal \$event\.event_hash \$event\.record_sha256[^\r\n]+\r?\n\s*\[void\]",
            RegexOptions.CultureInvariant), "Event validation regressed to format/link-only checks.");
    }

    [TestMethod]
    public void ScriptsParseInWindowsPowerShellAndAvoidPowerShellSevenOnlyApis()
    {
        foreach (var relative in new[]
                 {
                     "tools/AssetLibraryP3AutomatedAcceptance/Invoke-P3AssetLibraryAutomatedAcceptance.ps1",
                     "tools/AssetLibraryP3AutomatedAcceptance/Test-P3AssetLibraryAutomatedEvidence.ps1"
                 })
        {
            var escapedPath = Path(relative).Replace("'", "''", StringComparison.Ordinal);
            var script = $"$t=$null;$e=$null;[Management.Automation.Language.Parser]::ParseFile('{escapedPath}',[ref]$t,[ref]$e)|Out-Null;if(@($e).Count){{$e|% Message;exit 1}}";
            var result = Start("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", script]);
            Assert.AreEqual(0, result.ExitCode, $"{relative}: {result.Output} {result.Error}");
            var source = Read(relative);
            foreach (var forbidden in new[] { "IsPathFullyQualified", "GetRelativePath", "HashData", "ToHexString", "??" })
                Assert.IsFalse(source.Contains(forbidden, StringComparison.Ordinal), $"{relative} contains {forbidden}");
        }
    }

    [TestMethod]
    public void RunnerImplementsFourModesSealedSiblingValidationAndFourWayHandshake()
    {
        var runner = Read("tools/AssetLibraryP3AutomatedAcceptance/Invoke-P3AssetLibraryAutomatedAcceptance.ps1");
        ContainsAll(runner,
            "[ValidateSet('Run', 'DryRun', 'ValidateExistingRun', 'RecoveryTest')]",
            "feature/asset-library-eagle-parity-p3-query-metadata", "New-P3SyntheticFixture",
            "New-P3SyntheticFixture.py", "Test-P3AssetLibraryAutomatedEvidence.ps1",
            "Validator emitted unexpected stderr", "Validator stdout is not valid JSON",
            "Validator stdout failed the result contract", "pixel-tart-p3-automated-validation-result/v1",
            "negative_fixture_proof_count -ne 50", "negative_fixture_proof_sha256 -notmatch",
            "[bool]$validation.negative_proofs_skipped",
            "Validator log directory must be outside the sealed run root",
            "$validatorLogDirectory = Join-Path (Split-Path -Parent $activeRunRoot)",
            "$fingerprintBefore = Get-RunTreeFingerprint", "$fingerprintAfter = Get-RunTreeFingerprint");
        AssertOrdered(runner, Scenarios.Select(scenario => $"'{scenario}'").ToArray());
        AssertOrdered(runner, Restarts.Select(scenario => $"'{scenario}'").ToArray(), runner.IndexOf("$restartScenarios", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CommittedGeneratorDefinesDeterministicTenThousandAndLegacyFixtures()
    {
        var generator = Read("tools/AssetLibraryP3AutomatedAcceptance/New-P3SyntheticFixture.py");
        ContainsAll(generator, "range(10128)", "index >= 10000", "pixel-tart-p3-source-{index:05d}",
            "SmartFolderQueryDocuments", "AssetLibraryUndoJournal", "range(64)", "legacy-v6",
            "fixture-expectations.json", "3072", "1024", "6032", "4096",
            "sqlite-sourcepath-enumeration/v1", "source_path_tree_sha256");
        var result = Start("python.exe", ["-I", "-c", "import ast,pathlib,sys;ast.parse(pathlib.Path(sys.argv[1]).read_text(encoding='utf-8'))", Path("tools/AssetLibraryP3AutomatedAcceptance/New-P3SyntheticFixture.py")]);
        Assert.AreEqual(0, result.ExitCode, $"Generator Python syntax failed: {result.Output} {result.Error}");
    }

    [TestMethod]
    public void ValidatorIsReadOnlyAndFingerprintsSealedInput()
    {
        var validator = Read("tools/AssetLibraryP3AutomatedAcceptance/Test-P3AssetLibraryAutomatedEvidence.ps1");
        ContainsAll(validator, "$fingerprintBefore = Tree-Fingerprint $root", "$fingerprintAfter = Tree-Fingerprint $root",
            "mode=ro&immutable=1", "PRAGMA query_only=ON", "input tree fingerprint", "17 runner process sessions",
            "parameterized", "unparameterized_sql_count", "canonical_sha256", "asset_id_sha256",
            "Measure-SealedSafetyScan", "safety snapshot before/after hash", "provenance",
            "fixture manifest semantic binding", "fixture generated input tree hash",
            "fixture independent source path tree hash", "pre-cleanup database audit manifest hash",
            "audit/summary evidence hash");
        foreach (var forbidden in new[] { "Set-Content", "Add-Content", "Out-File", "Remove-Item", "Move-Item", "Copy-Item", "WriteAllText", "WriteAllBytes", "Get-FileHash" })
            Assert.IsFalse(validator.Contains(forbidden, StringComparison.OrdinalIgnoreCase), $"validator contains mutator {forbidden}");
    }

    [TestMethod]
    public void RecursiveNegativeProofModeCannotProduceAReleasePassingResult()
    {
        var validator = Read("tools/AssetLibraryP3AutomatedAcceptance/Test-P3AssetLibraryAutomatedEvidence.ps1");
        var runner = Read("tools/AssetLibraryP3AutomatedAcceptance/Invoke-P3AssetLibraryAutomatedAcceptance.ps1");
        var runSet = Read("tools/AssetLibraryP3AutomatedAcceptance/Test-P3AssetLibraryAutomatedRunSet.ps1");
        ContainsAll(validator,
            "recursive negative-proof validation is restricted to a named sibling proof workspace",
            "status = if ($SkipNegativeProofs) { 'passed-negative-baseline' } else { 'passed' }",
            "negative_proofs_skipped = [bool]$SkipNegativeProofs");
        ContainsAll(runner, "[string]$validation.status -cne 'passed'", "[bool]$validation.negative_proofs_skipped");
        ContainsAll(runSet, "[string]$result.status -cne 'passed'", "[bool]$result.negative_proofs_skipped");
    }

    [TestMethod]
    public void RunnerDerivesSafetyCountsFromSealedObservationsInsteadOfLiteralZeroes()
    {
        var runner = Read("tools/AssetLibraryP3AutomatedAcceptance/Invoke-P3AssetLibraryAutomatedAcceptance.ps1");
        ContainsAll(runner,
            "function New-SafetyStaticScanInput", "function Measure-SafetyStaticScan",
            "safety-source-snapshot", "source_snapshot_unchanged",
            "devpreview_get_process_count_before", "devpreview_get_process_count_after",
            "environmentBeforeRows", "environmentAfterRows", "display_before", "display_after",
            "outside_run_root_path_count",
            "source_path_observation = [string]$fixture.source_path_observation",
            "source_path_tree_sha256 = [string]$fixture.source_path_tree_sha256",
            "desktop_input_injection_count = Get-SafetyRuleCount",
            "network_upload_count = Get-SafetyRuleCount",
            "user_source_write_count = [int]$pathConfinement.user_source_write_count");
        var safetyStart = runner.IndexOf("$safety = [ordered]@{", StringComparison.Ordinal);
        var safetyEnd = runner.IndexOf("if ($processCleanup", safetyStart, StringComparison.Ordinal);
        Assert.IsGreaterThan(0, safetyStart);
        Assert.IsGreaterThan(safetyStart, safetyEnd);
        var safetyBlock = runner[safetyStart..safetyEnd];
        Assert.IsFalse(Regex.IsMatch(safetyBlock, @"_count\s*=\s*0(?:\s|$)", RegexOptions.CultureInvariant),
            "The final manifest safety block contains an unmeasured literal zero.");
    }

    [TestMethod]
    public void PackageDoesNotDriveDesktopDisplayEagleOrNetwork()
    {
        var package = string.Join("\n", Directory.GetFiles(Path("tools/AssetLibraryP3AutomatedAcceptance"), "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains("__pycache__", StringComparison.OrdinalIgnoreCase)).Select(File.ReadAllText));
        foreach (var forbiddenCall in new[]
                 {
                     @"\bSendInput\s*\(", @"\bmouse_event\s*\(", @"\bkeybd_event\s*\(",
                     @"\bSetForegroundWindow\s*\(", @"\bChangeDisplaySettings(?:Ex)?\s*\(",
                     @"\bInvoke-WebRequest\b", @"\bInvoke-RestMethod\b"
                 })
            Assert.IsFalse(Regex.IsMatch(package, forbiddenCall, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), forbiddenCall);
        StringAssert.Contains(package, "pixel-tart-p3-safety-measurement/v1");
        StringAssert.Contains(package, "source_snapshot_unchanged");
        StringAssert.Contains(package, "outside_run_root_path_count");
    }

    private static JsonDocument Contract() => JsonDocument.Parse(Read("tools/AssetLibraryP3AutomatedAcceptance/automated-acceptance-contract.json"));
    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString()!).ToArray();
    private static void ContainsAll(string text, params string[] values)
    {
        foreach (var value in values) StringAssert.Contains(text, value);
    }
    private static void AssertOrdered(string text, string[] values, int start = 0)
    {
        var previous = start;
        foreach (var value in values)
        {
            var current = text.IndexOf(value, previous, StringComparison.Ordinal);
            Assert.IsGreaterThanOrEqualTo(previous, current, $"Missing or out-of-order value: {value}");
            previous = current + value.Length;
        }
    }
    private static (int ExitCode, string Output, string Error) Start(string fileName, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = RepositoryRoot(), UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
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
