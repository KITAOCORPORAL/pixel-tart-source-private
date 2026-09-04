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
        "batch-partial-commit", "journal-chain-mismatch", "lifecycle-chain-mismatch",
        "missing-completion-handshake", "lifecycle-state-regression",
        "lifecycle-pending-count-missing", "lifecycle-pending-count-string",
        "lifecycle-pending-count-negative", "lifecycle-completed-count-nonzero",
        "lifecycle-duplicate-transition",
        "lifecycle-run-id-mismatch", "lifecycle-session-id-mismatch", "lifecycle-source-head-mismatch",
        "lifecycle-binary-hash-mismatch", "partial-phase-summary", "phase-summary-record-hash-mismatch",
        "forced-cleanup-false-success", "missing-exit-code", "missing-on-exit-completed",
        "lifecycle-timing-regression", "lifecycle-old-root-splice",
        "undo-redo-mismatch", "restart-identity-reused",
        "view-result-divergence", "selection-hash-divergence", "dpi-overflow", "contrast-threshold-failed",
        "accessibility-identity-missing", "performance-threshold-exceeded", "ui-block-exceeded",
        "user-source-write", "safety-counter-null", "eagle-write", "network-upload", "permanent-delete", "residual-process",
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
        Assert.AreEqual("pixel-tart-p3-process-table-snapshot/v1", root.GetProperty("process_table_snapshot_schema").GetString());
        Assert.AreEqual("pixel-tart-p3-runner-process-owner/v1", root.GetProperty("process_owner_schema").GetString());
        Assert.AreEqual("pixel-tart-p3-runner-process-exit-diagnostic/v1", root.GetProperty("process_exit_diagnostic_schema").GetString());
        Assert.AreEqual("pixel-tart-p3-runner-session-result/v1", root.GetProperty("runner_session_result_schema").GetString());
        Assert.AreEqual("shared-deadline-staged-evidence-and-exit", root.GetProperty("process_exit_wait_strategy").GetString());
        Assert.AreEqual(300, root.GetProperty("process_exit_total_timeout_seconds").GetInt32());
        var stageCaps = root.GetProperty("process_exit_stage_timeouts_seconds");
        CollectionAssert.AreEqual(new[] { 235, 20, 10, 10, 5, 10, 10 }, new[]
        {
            stageCaps.GetProperty("completion_handshake").GetInt32(),
            stageCaps.GetProperty("shutdown_preparation").GetInt32(),
            stageCaps.GetProperty("application_on_exit_enter").GetInt32(),
            stageCaps.GetProperty("phase_summary_commit").GetInt32(),
            stageCaps.GetProperty("application_on_exit_completed").GetInt32(),
            stageCaps.GetProperty("process_exit").GetInt32(),
            stageCaps.GetProperty("process_table_convergence").GetInt32()
        });
        Assert.AreEqual(2, root.GetProperty("process_table_required_consecutive_empty_observations").GetInt32());
        Assert.IsTrue(root.GetProperty("forced_cleanup_requires_retained_process_handle").GetBoolean());
        Assert.IsTrue(root.GetProperty("runner_preassigns_process_session_id").GetBoolean());
        Assert.AreEqual("pixel-tart-p3-automated-lifecycle/v1", root.GetProperty("application_lifecycle_schema").GetString());
        CollectionAssert.AreEqual(new[]
        {
            "plan-completed", "completion-ack-written", "shutdown-requested", "shutdown-dispatch-started",
            "page-dispose-start", "page-dispose-completed", "application-async-dispose-start",
            "application-async-dispose-completed", "shutdown-preparation-complete", "window-close-start",
            "application-shutdown-start", "window-close-completed", "application-on-exit-enter",
            "summary-commit-start", "phase-summary-written", "summary-commit-end", "application-on-exit-completed"
        }, Strings(root.GetProperty("required_application_lifecycle_events")));
        CollectionAssert.AreEqual(new[]
        {
            "passed", "completed", "accepted", "started", "started", "completed", "started", "completed", "completed",
            "started", "started", "completed", "prepared", "passed", "passed", "passed", "completed"
        }, Strings(root.GetProperty("required_application_lifecycle_results")));
        CollectionAssert.AreEqual(new[]
        {
            false, false, true, true, true, false, true, false, false,
            true, true, false, false, true, false, false, false
        }, root.GetProperty("required_application_lifecycle_pending").EnumerateArray()
            .Select(item => item.GetBoolean()).ToArray());
        CollectionAssert.AreEqual(new[]
        {
            "zero", "zero", "one", "one", "observed-nonnegative", "zero", "one", "zero", "zero",
            "one", "one", "zero", "zero", "one", "zero", "zero", "zero"
        }, Strings(root.GetProperty("required_application_lifecycle_pending_operation_count_rules")));
        CollectionAssert.AreEqual(new[]
        {
            "Pid", "ProcessName", "StartTimeUtc", "ExecutablePath", "ExecutableSha256", "RunId",
            "ProcessSessionId", "WindowHandle", "OwnedByRun", "HasExited", "ObservationError"
        }, Strings(root.GetProperty("process_identity_fields")));
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
            "P3 automated evidence rejected", "EXPECTED_REJECTION_FRAGMENTS",
            "result[\"lifecycle_sha256\"]", "negative mutation rejection did not reach intended validator guard");
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
    public void ValidatorTimeoutBoundsAreContractBoundAndOutputReadsCannotBypassTheDeadline()
    {
        using var contract = JsonDocument.Parse(Read(
            "tools/AssetLibraryP3AutomatedAcceptance/automated-acceptance-contract.json"));
        var proofTimeout = contract.RootElement.GetProperty("negative_evidence_proof_timeout_seconds").GetInt32();
        var validatorTimeout = contract.RootElement.GetProperty("validator_process_timeout_seconds").GetInt32();
        Assert.AreEqual(3600, proofTimeout);
        Assert.AreEqual(3900, validatorTimeout);
        Assert.IsGreaterThan(0, proofTimeout);
        Assert.IsGreaterThan(proofTimeout, validatorTimeout);

        var validator = Read("tools/AssetLibraryP3AutomatedAcceptance/Test-P3AssetLibraryAutomatedEvidence.ps1");
        var stdoutTask = validator.IndexOf("$stdoutTask = $process.StandardOutput.ReadToEndAsync()", StringComparison.Ordinal);
        var boundedWait = validator.IndexOf("$process.WaitForExit($TimeoutSeconds * 1000)", StringComparison.Ordinal);
        var stdoutResult = validator.IndexOf("$stdout = $stdoutTask.GetAwaiter().GetResult()", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, stdoutTask);
        Assert.IsGreaterThan(stdoutTask, boundedWait);
        Assert.IsGreaterThan(boundedWait, stdoutResult);
        Assert.IsFalse(validator.Contains("$process.StandardOutput.ReadToEnd()\r\n        $stderr", StringComparison.Ordinal));
        Assert.IsFalse(validator.Contains("$process.StandardOutput.ReadToEnd()\n        $stderr", StringComparison.Ordinal));

        var runner = Read("tools/AssetLibraryP3AutomatedAcceptance/Invoke-P3AssetLibraryAutomatedAcceptance.ps1");
        ContainsAll(runner,
            "$validatorTimeoutSeconds = [int]$sealedContract.validator_process_timeout_seconds",
            "-Timeout $validatorTimeoutSeconds",
            "Validator target contract has invalid timeout bounds.");
        Assert.IsFalse(runner.Contains("-Name $Name -LogDirectory $LogDirectory -Timeout 300", StringComparison.Ordinal));
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
    public void RunnerProcessSnapshotsOwnershipConvergenceAndFailurePrecedenceExecuteInWindowsPowerShell51()
    {
        var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pixel-tart-p3-process-snapshot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            var runner = Read("tools/AssetLibraryP3AutomatedAcceptance/Invoke-P3AssetLibraryAutomatedAcceptance.ps1");
            var executionMarker = runner.IndexOf("$script:repo = Get-RepositoryRoot", StringComparison.Ordinal);
            Assert.IsGreaterThan(0, executionMarker);
            var harness = runner[..executionMarker] +
                """
                function Require-Harness([bool]$Condition, [string]$Message) { if (-not $Condition) { throw "HARNESS: $Message" } }
                function New-HarnessRow([int]$ProcessId, [string]$Path = 'C:\fake\PixelTart_ModularHarness_V1_DevPreview.exe') {
                    New-ProcessIdentitySnapshotRow $ProcessId 'PixelTart_ModularHarness_V1_DevPreview' `
                        '2026-09-04T00:00:00.0000000Z' $Path ('a' * 64) '' '' '' $false $false ''
                }
                function Capture-Assert($Native, $Cim) {
                    try {
                        $Observation = New-DevPreviewProcessObservation $Native $Cim
                        Assert-NoDevPreview $Observation
                        [pscustomobject]@{ threw=$false; message=''; fqid='' }
                    }
                    catch { [pscustomobject]@{ threw=$true; message=$_.Exception.Message; fqid=$_.FullyQualifiedErrorId } }
                }
                $emptyNative = New-ProcessTableSnapshot 'Get-Process'
                $emptyCim = New-ProcessTableSnapshot 'CIM'
                $oneNative = New-ProcessTableSnapshot 'Get-Process' ([PixelTartP3ProcessIdentitySnapshotRow[]]@((New-HarnessRow 101)))
                $oneCim = New-ProcessTableSnapshot 'CIM' ([PixelTartP3ProcessIdentitySnapshotRow[]]@((New-HarnessRow 201)))
                $manyNative = New-ProcessTableSnapshot 'Get-Process' ([PixelTartP3ProcessIdentitySnapshotRow[]]@(
                    (New-HarnessRow 111),(New-HarnessRow 112),(New-HarnessRow 113)))
                $manyCim = New-ProcessTableSnapshot 'CIM' ([PixelTartP3ProcessIdentitySnapshotRow[]]@(
                    (New-HarnessRow 211),(New-HarnessRow 212),(New-HarnessRow 213)))
                Require-Harness ($emptyNative.Items.Count -eq 0 -and $emptyCim.Items.Count -eq 0) 'zero snapshot count'
                Require-Harness ($oneNative.Items.Count -eq 1 -and $oneCim.Items.Count -eq 1) 'one snapshot count'
                Require-Harness ($manyNative.Items.Count -eq 3 -and $manyCim.Items.Count -eq 3) 'many snapshot count'
                Require-Harness ($emptyNative.GetType() -eq $oneNative.GetType() -and $oneNative.GetType() -eq $manyNative.GetType()) 'table type drift'
                Require-Harness ($emptyNative.Items.GetType() -eq $oneNative.Items.GetType() -and $oneNative.Items.GetType() -eq $manyNative.Items.GetType()) 'Items type drift'
                Require-Harness ($emptyNative.Items.GetType().GetElementType() -eq [PixelTartP3ProcessIdentitySnapshotRow]) 'Items are not strongly typed'
                $fixedFields = 'Pid','ProcessName','StartTimeUtc','ExecutablePath','ExecutableSha256','RunId','ProcessSessionId','WindowHandle','OwnedByRun','HasExited','ObservationError'
                $actualFields = @([PixelTartP3ProcessIdentitySnapshotRow].GetProperties() | ForEach-Object Name)
                Require-Harness (($actualFields -join '|') -ceq ($fixedFields -join '|')) 'identity fields drift'

                $assert00 = Capture-Assert $emptyNative $emptyCim
                $assert10 = Capture-Assert $oneNative $emptyCim
                $assert01 = Capture-Assert $emptyNative $oneCim
                $sameNative = New-ProcessTableSnapshot 'Get-Process' ([PixelTartP3ProcessIdentitySnapshotRow[]]@((New-HarnessRow 601)))
                $sameCim = New-ProcessTableSnapshot 'CIM' ([PixelTartP3ProcessIdentitySnapshotRow[]]@((New-HarnessRow 601)))
                $dedupe = Capture-Assert $sameNative $sameCim
                $failedCim = New-ProcessTableSnapshot 'CIM' ([PixelTartP3ProcessIdentitySnapshotRow[]]@()) 'query-sentinel'
                $failClosed = Capture-Assert $emptyNative $failedCim
                Require-Harness (-not $assert00.threw) '0/0 must pass'
                Require-Harness ($assert10.threw -and $assert10.message.Contains('101') -and $assert10.fqid -notlike 'PropertyNotFoundStrict*') '1/0 wrong failure'
                Require-Harness ($assert01.threw -and $assert01.message.Contains('201') -and $assert01.fqid -notlike 'PropertyNotFoundStrict*') '0/1 wrong failure'
                Require-Harness ($dedupe.threw -and ([regex]::Matches($dedupe.message,'(?<!\d)601(?!\d)').Count) -eq 1) 'PID was not deduplicated'
                Require-Harness ($failClosed.threw -and $failClosed.message.Contains('failed closed')) 'query failure was not fail closed'

                $owner = [pscustomobject]@{ Pid=701; ExecutablePath='C:\sealed\app.exe'; StartTimeUtc='2026-09-04T01:02:03.0000000Z' }
                $ownerPositive = Test-ProcessOwnerTokenValues $owner 701 'c:\SEALED\app.exe' '2026-09-04T01:02:03.0000000Z'
                $ownerWrongPid = Test-ProcessOwnerTokenValues $owner 702 'C:\sealed\app.exe' '2026-09-04T01:02:03.0000000Z'
                $ownerWrongPath = Test-ProcessOwnerTokenValues $owner 701 'C:\other\app.exe' '2026-09-04T01:02:03.0000000Z'
                $ownerWrongStart = Test-ProcessOwnerTokenValues $owner 701 'C:\sealed\app.exe' '2026-09-04T01:02:10.0000000Z'
                Require-Harness ($ownerPositive -and -not $ownerWrongPid -and -not $ownerWrongPath -and -not $ownerWrongStart) 'owner token comparison failed'

                $script:convergenceIndex = 0
                $convergenceProvider = {
                    param($Token)
                    $script:convergenceIndex++
                    if ($script:convergenceIndex -eq 1) { New-DevPreviewProcessObservation $oneNative $emptyCim }
                    else { New-DevPreviewProcessObservation $emptyNative $emptyCim }
                }
                $convergence = Wait-DevPreviewProcessTableConvergence $null 100 1 2 $convergenceProvider
                Require-Harness ($convergence.converged -and $convergence.samples.Count -eq 3) 'nonempty/empty/empty convergence failed'
                $script:alternatingIndex = 0
                $alternatingProvider = {
                    param($Token)
                    $script:alternatingIndex++
                    if (($script:alternatingIndex % 2) -eq 1) { New-DevPreviewProcessObservation $emptyNative $emptyCim }
                    else { New-DevPreviewProcessObservation $oneNative $emptyCim }
                }
                $alternating = Wait-DevPreviewProcessTableConvergence $null 15 1 2 $alternatingProvider
                Require-Harness (-not $alternating.converged) 'a single empty sample caused premature convergence'

                $cleanupFailures = [Collections.Generic.List[object]]::new()
                $observationFailures = [Collections.Generic.List[object]]::new()
                $primary = [TimeoutException]::new('primary-timeout-sentinel')
                $persistentProvider = { param($Token); New-DevPreviewProcessObservation $oneNative $emptyCim }
                $final = Invoke-FinalDevPreviewCheck $primary $cleanupFailures $observationFailures 5 $persistentProvider
                Require-Harness (-not $final.converged -and $cleanupFailures.Count -eq 1) 'secondary cleanup diagnostic missing'
                Require-Harness ([string]$primary.Data['devpreview_cleanup_failure'] -match '101') 'secondary cleanup was not attached to primary'
                $outer = New-P3FinalFailureException 'C:\retained-run' $primary @($cleanupFailures) @($observationFailures)
                Require-Harness ($outer.InnerException.Message -ceq 'primary-timeout-sentinel') 'primary exception was not retained as InnerException'
                $cleanupOnly = $null
                try { Invoke-FinalDevPreviewCheck $null ([Collections.Generic.List[object]]::new()) ([Collections.Generic.List[object]]::new()) 5 $persistentProvider | Out-Null }
                catch { $cleanupOnly = $_ }
                Require-Harness ($null -ne $cleanupOnly -and $cleanupOnly.Exception.Message -match '101') 'cleanup-only failure was swallowed'

                $atomicPath = Join-Path $PSScriptRoot 'atomic-write-harness.json'
                Write-JsonAtomic $atomicPath ([ordered]@{ generation = 1; value = 'before' })
                Write-JsonAtomic $atomicPath ([ordered]@{ generation = 2; value = 'after' })
                $atomicPayload = Get-Content -LiteralPath $atomicPath -Raw -Encoding UTF8 | ConvertFrom-Json
                $atomicTemps = @(Get-ChildItem -LiteralPath $PSScriptRoot -Force -File | Where-Object { $_.Name -like '.atomic-write-harness.json.*.tmp' })
                $atomicBackups = @(Get-ChildItem -LiteralPath $PSScriptRoot -Force -File | Where-Object { $_.Name -like '.atomic-write-harness.json.*.bak' })
                Require-Harness ([int]$atomicPayload.generation -eq 2 -and $atomicPayload.value -ceq 'after' -and $atomicTemps.Count -eq 0 -and $atomicBackups.Count -eq 0) 'atomic write/replace contract failed'

                [pscustomobject]@{
                    powershell_major_minor = "$($PSVersionTable.PSVersion.Major).$($PSVersionTable.PSVersion.Minor)"
                    zero_count = $emptyNative.Items.Count
                    one_count = $oneNative.Items.Count
                    many_count = $manyNative.Items.Count
                    assert_00_passed = -not $assert00.threw
                    assert_10_expected = $assert10.threw -and $assert10.fqid -notlike 'PropertyNotFoundStrict*'
                    assert_01_expected = $assert01.threw -and $assert01.fqid -notlike 'PropertyNotFoundStrict*'
                    deduplicated = ([regex]::Matches($dedupe.message,'(?<!\d)601(?!\d)').Count) -eq 1
                    query_failure_closed = $failClosed.threw
                    convergence_sample_count = $convergence.samples.Count
                    alternating_rejected = -not $alternating.converged
                    owner_checks_passed = $ownerPositive -and -not $ownerWrongPid -and -not $ownerWrongPath -and -not $ownerWrongStart
                    preserved_primary = $outer.InnerException.Message
                    attached_cleanup = [string]$primary.Data['devpreview_cleanup_failure']
                    atomic_write_replace_passed = [int]$atomicPayload.generation -eq 2 -and $atomicTemps.Count -eq 0 -and $atomicBackups.Count -eq 0
                } | ConvertTo-Json -Compress
                """;
            var harnessPath = System.IO.Path.Combine(temp, "ProcessSnapshotHarness.ps1");
            File.WriteAllText(harnessPath, harness, new System.Text.UTF8Encoding(false));
            var result = Start("powershell.exe",
                ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", harnessPath]);
            Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
            using var payload = JsonDocument.Parse(result.Output);
            var root = payload.RootElement;
            Assert.AreEqual("5.1", root.GetProperty("powershell_major_minor").GetString());
            Assert.AreEqual(0, root.GetProperty("zero_count").GetInt32());
            Assert.AreEqual(1, root.GetProperty("one_count").GetInt32());
            Assert.AreEqual(3, root.GetProperty("many_count").GetInt32());
            Assert.IsTrue(root.GetProperty("assert_00_passed").GetBoolean());
            Assert.IsTrue(root.GetProperty("assert_10_expected").GetBoolean());
            Assert.IsTrue(root.GetProperty("assert_01_expected").GetBoolean());
            Assert.IsTrue(root.GetProperty("deduplicated").GetBoolean());
            Assert.IsTrue(root.GetProperty("query_failure_closed").GetBoolean());
            Assert.AreEqual(3, root.GetProperty("convergence_sample_count").GetInt32());
            Assert.IsTrue(root.GetProperty("alternating_rejected").GetBoolean());
            Assert.IsTrue(root.GetProperty("owner_checks_passed").GetBoolean());
            Assert.AreEqual("primary-timeout-sentinel", root.GetProperty("preserved_primary").GetString());
            StringAssert.Contains(root.GetProperty("attached_cleanup").GetString()!, "101");
            Assert.IsTrue(root.GetProperty("atomic_write_replace_passed").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [TestMethod]
    public void RunnerAcceptsObservedZeroPendingCountAndRejectsMalformedLifecycleCountsInWindowsPowerShell51()
    {
        var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pixel-tart-p3-lifecycle-count-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            var runner = Read("tools/AssetLibraryP3AutomatedAcceptance/Invoke-P3AssetLibraryAutomatedAcceptance.ps1");
            var executionMarker = runner.IndexOf("$script:repo = Get-RepositoryRoot", StringComparison.Ordinal);
            Assert.IsGreaterThan(0, executionMarker);
            var harness = runner[..executionMarker] + """
                function Test-Rejected($Record, [int]$Index, [string]$ExpectedMessage) {
                    try { [void](Assert-P3LifecyclePendingOperationCount $Record $Index); return $false }
                    catch { return $_.Exception.Message.Contains($ExpectedMessage) }
                }
                $observedZero = [pscustomobject]@{ pending_operation_count = 0 }
                $fixedOne = [pscustomobject]@{ pending_operation_count = 1 }
                $observedZeroAccepted = (Assert-P3LifecyclePendingOperationCount $observedZero 4) -eq 0
                $legacyObservedOneAccepted = (Assert-P3LifecyclePendingOperationCount $fixedOne 4) -eq 1
                $fixedOneAccepted = (Assert-P3LifecyclePendingOperationCount $fixedOne 3) -eq 1
                [pscustomobject]@{
                    powershell_major_minor="$($PSVersionTable.PSVersion.Major).$($PSVersionTable.PSVersion.Minor)"
                    observed_zero_accepted=$observedZeroAccepted
                    legacy_observed_one_accepted=$legacyObservedOneAccepted
                    fixed_one_accepted=$fixedOneAccepted
                    missing_rejected=(Test-Rejected ([pscustomobject]@{}) 4 "missing required property 'pending_operation_count'")
                    string_rejected=(Test-Rejected ([pscustomobject]@{ pending_operation_count = '0' }) 4 'must be a JSON integer number')
                    negative_rejected=(Test-Rejected ([pscustomobject]@{ pending_operation_count = -1 }) 4 'observed-nonnegative')
                    completed_nonzero_rejected=(Test-Rejected ([pscustomobject]@{ pending_operation_count = 1 }) 5 "rule 'zero'")
                } | ConvertTo-Json -Compress
                """;
            var harnessPath = System.IO.Path.Combine(temp, "LifecyclePendingCountHarness.ps1");
            File.WriteAllText(harnessPath, harness, new System.Text.UTF8Encoding(false));
            var result = Start("powershell.exe",
                ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", harnessPath]);
            Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
            using var payload = JsonDocument.Parse(result.Output);
            var root = payload.RootElement;
            Assert.AreEqual("5.1", root.GetProperty("powershell_major_minor").GetString());
            foreach (var property in new[]
                     {
                         "observed_zero_accepted", "legacy_observed_one_accepted", "fixed_one_accepted", "missing_rejected", "string_rejected",
                         "negative_rejected", "completed_nonzero_rejected"
                     })
                Assert.IsTrue(root.GetProperty(property).GetBoolean(), property);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [TestMethod]
    public void StagedRunnerEvidenceExitAndCleanupPathsExecuteInWindowsPowerShell51()
    {
        var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pixel-tart-p3-staged-exit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            var runner = Read("tools/AssetLibraryP3AutomatedAcceptance/Invoke-P3AssetLibraryAutomatedAcceptance.ps1");
            var executionMarker = runner.IndexOf("$script:repo = Get-RepositoryRoot", StringComparison.Ordinal);
            Assert.IsGreaterThan(0, executionMarker);
            var escapedTemp = temp.Replace("'", "''", StringComparison.Ordinal);
            var harness = runner[..executionMarker] + $$"""
                function Require-Harness([bool]$Condition, [string]$Message) { if (-not $Condition) { throw "HARNESS: $Message" } }
                $harnessRoot = '{{escapedTemp}}'
                $powershellPath = [IO.Path]::GetFullPath((Get-Process -Id $PID).Path)
                function Start-HarnessProcess([string]$Body) {
                    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($Body))
                    Start-Process -FilePath $powershellPath -ArgumentList @('-NoProfile','-NonInteractive','-EncodedCommand',$encoded) -PassThru -WindowStyle Hidden
                }
                function New-HarnessIdentity([Diagnostics.Process]$Process) {
                    [pscustomobject][ordered]@{
                        schema='pixel-tart-p3-automated-lifecycle/v1'; run_id='p3-auto-harness'; scenario_id='scope-switch/v1'
                        phase='primary'; process_session_id=('a' * 32); source_head=('b' * 40)
                        executable_sha256=('c' * 64); application_sha256=('d' * 64); asset_module_sha256=('e' * 64)
                        pid=[int]$Process.Id
                    }
                }
                function New-HarnessOwner([Diagnostics.Process]$Process) {
                    [pscustomobject][ordered]@{
                        schema='pixel-tart-p3-runner-process-owner/v1'; Pid=[int]$Process.Id; ProcessName=[string]$Process.ProcessName
                        StartTimeUtc=$Process.StartTime.ToUniversalTime().ToString('O'); ExecutablePath=$powershellPath
                        ExecutableSha256=(Get-FileSha256 $powershellPath); RunId='p3-auto-harness'; ProcessSessionId=('a' * 32)
                        WindowHandle=''; OwnedByRun=$true; HasExited=$false; ObservationError=''
                    }
                }
                function Write-HarnessEvidence([string]$Directory, $Identity) {
                    [IO.Directory]::CreateDirectory($Directory) | Out-Null
                    $lifecyclePath = Join-Path $Directory 'lifecycle.ndjson'
                    $summaryPath = Join-Path $Directory 'summary.json'
                    $summaryJournalPath = Join-Path $Directory 'summary.ndjson'
                    $builder = [Text.StringBuilder]::new(); $previous = '0' * 64
                    for ($index = 0; $index -lt $script:p3LifecycleEvents.Count; $index++) {
                        $pendingCountRule = $script:p3LifecyclePendingOperationCountRules[$index]
                        $row = [ordered]@{
                            schema=$Identity.schema; sequence=$index + 1
                            timestamp_utc=[DateTimeOffset]::UtcNow.AddMilliseconds($index).ToString('O')
                            stopwatch_elapsed_ms=[double]$index; event=$script:p3LifecycleEvents[$index]
                            result=$script:p3LifecycleResults[$index]; pending=[bool]$script:p3LifecyclePending[$index]
                            pending_operation_count=$(if ($pendingCountRule -ceq 'one') { 1 } else { 0 }); exception=$null
                            run_id=$Identity.run_id; scenario_id=$Identity.scenario_id; phase=$Identity.phase
                            process_session_id=$Identity.process_session_id; pid=$Identity.pid; hwnd='0x1'
                            source_head=$Identity.source_head; executable_sha256=$Identity.executable_sha256
                            application_sha256=$Identity.application_sha256; asset_module_sha256=$Identity.asset_module_sha256
                            managed_thread_id=1; dispatcher_thread_id=1; previous_record_sha256=$previous
                        }
                        $hash = Get-TextSha256 ($row | ConvertTo-Json -Depth 10 -Compress)
                        $row.record_sha256 = $hash; $previous = $hash
                        [void]$builder.AppendLine(($row | ConvertTo-Json -Depth 10 -Compress))
                    }
                    [IO.File]::WriteAllText($lifecyclePath, $builder.ToString(), [Text.UTF8Encoding]::new($false))
                    $summary = [ordered]@{
                        schema='pixel-tart-p3-automated-summary/v1'; status='completed'; run_id=$Identity.run_id; phase=$Identity.phase
                        process_session_id=$Identity.process_session_id; source_head=$Identity.source_head
                        executable_sha256=$Identity.executable_sha256; application_sha256=$Identity.application_sha256
                        asset_module_sha256=$Identity.asset_module_sha256
                        scenarios=@([ordered]@{ id=$Identity.scenario_id; pid=$Identity.pid; hwnd='0x1' })
                    }
                    $summary.record_sha256 = Get-TextSha256 ($summary | ConvertTo-Json -Depth 10 -Compress)
                    Write-JsonAtomic $summaryPath $summary
                    $journal = [ordered]@{
                        schema='pixel-tart-p3-automated-summary/v1'; run_id=$Identity.run_id; source_head=$Identity.source_head
                        scenario_id=$Identity.scenario_id; phase=$Identity.phase; process_session_id=$Identity.process_session_id
                        pid=$Identity.pid; hwnd='0x1'; executable_sha256=$Identity.executable_sha256
                        application_sha256=$Identity.application_sha256; asset_module_sha256=$Identity.asset_module_sha256
                        summary=$summary; previous_summary_hash=('0' * 64); previous_record_sha256=('0' * 64)
                    }
                    $journalHash = Get-TextSha256 ($journal | ConvertTo-Json -Depth 20 -Compress)
                    $journal.summary_hash = $journalHash; $journal.record_sha256 = $journalHash
                    [IO.File]::WriteAllText($summaryJournalPath, ($journal | ConvertTo-Json -Depth 20 -Compress) + [Environment]::NewLine,
                        [Text.UTF8Encoding]::new($false))
                    [pscustomobject]@{ lifecycle=$lifecyclePath; summary=$summaryPath; journal=$summaryJournalPath }
                }

                $rawNullRejected = $false
                try { New-DevPreviewProcessObservation $null $null | Out-Null } catch { $rawNullRejected = $_.FullyQualifiedErrorId -notlike 'PropertyNotFoundStrict*' }
                $actualPowerShell = Get-Process -Id $PID
                function Get-Process { [CmdletBinding()] param(); return $null }
                function Get-CimInstance { [CmdletBinding()] param($ClassName,$Filter); return $null }
                $rawNullGetProcessSnapshot = Get-ProcessSnapshot
                $rawNullCimSnapshot = Get-CimProcessSnapshot
                $rawNullNormalized = $rawNullGetProcessSnapshot.Items.Count -eq 0 -and
                    $rawNullCimSnapshot.Items.Count -eq 0 -and
                    $rawNullGetProcessSnapshot.Items.GetType().GetElementType() -eq [PixelTartP3ProcessIdentitySnapshotRow] -and
                    $rawNullCimSnapshot.Items.GetType().GetElementType() -eq [PixelTartP3ProcessIdentitySnapshotRow]
                function Get-Process { [CmdletBinding()] param(); [pscustomobject]@{
                    Id=4242; ProcessName=$script:expectedProcessName; Path=$powershellPath
                    StartTime=[DateTime]::UtcNow; MainWindowHandle=[IntPtr]::Zero; HasExited=$false
                } }
                $idSnapshot = Get-ProcessSnapshot
                Remove-Item Function:Get-Process
                function Get-CimInstance { [CmdletBinding()] param($ClassName,$Filter); [pscustomobject]@{
                    ProcessId=4343; Name="$($script:expectedProcessName).exe"; ExecutablePath=$powershellPath
                    CreationDate=[DateTime]::UtcNow
                } }
                $dateTimeCimSnapshot = Get-CimProcessSnapshot
                $cimDateTimeAccepted = $dateTimeCimSnapshot.Items.Count -eq 1 -and
                    $dateTimeCimSnapshot.Items[0].Pid -eq 4343 -and
                    -not [string]::IsNullOrWhiteSpace($dateTimeCimSnapshot.Items[0].StartTimeUtc)
                function Get-CimInstance { [CmdletBinding()] param($ClassName,$Filter); throw 'real-cim-query-sentinel' }
                $cimThrowClosed = $false
                try { Get-CimProcessSnapshot | Out-Null } catch { $cimThrowClosed = $_.Exception.Message.Contains('failed closed') -and $_.Exception.Message.Contains('real-cim-query-sentinel') }
                Remove-Item Function:Get-CimInstance

                $historicalIdentity = [pscustomobject][ordered]@{
                    schema='pixel-tart-p3-automated-lifecycle/v1'; run_id='p3-auto-harness'; scenario_id='scope-switch/v1'
                    phase='primary'; process_session_id=('8' * 32); source_head=('b' * 40)
                    executable_sha256=('c' * 64); application_sha256=('d' * 64); asset_module_sha256=('e' * 64); pid=18001
                }
                $currentIdentity = [pscustomobject][ordered]@{
                    schema='pixel-tart-p3-automated-lifecycle/v1'; run_id='p3-auto-harness'; scenario_id='scope-switch/v1'
                    phase='primary'; process_session_id=('9' * 32); source_head=('b' * 40)
                    executable_sha256=('c' * 64); application_sha256=('d' * 64); asset_module_sha256=('e' * 64); pid=18002
                }
                $historicalEvidence = Write-HarnessEvidence (Join-Path $harnessRoot 'journal-history') $historicalIdentity
                $currentEvidence = Write-HarnessEvidence (Join-Path $harnessRoot 'journal-current') $currentIdentity
                $lifecycleLines = [IO.File]::ReadAllLines($currentEvidence.lifecycle, [Text.UTF8Encoding]::new($false))
                $partialLifecyclePath = Join-Path $harnessRoot 'live-partial-lifecycle.ndjson'
                [IO.File]::WriteAllText($partialLifecyclePath, $lifecycleLines[0] + [Environment]::NewLine,
                    [Text.UTF8Encoding]::new($false))
                $secondLifecycleBytes = [Text.UTF8Encoding]::new($false).GetBytes($lifecycleLines[1])
                $secondLifecycleSplit = [int][Math]::Floor($secondLifecycleBytes.Length / 2)
                $partialWriter = [IO.FileStream]::new($partialLifecyclePath,[IO.FileMode]::Append,[IO.FileAccess]::Write,[IO.FileShare]::Read)
                try {
                    $partialWriter.Write($secondLifecycleBytes,0,$secondLifecycleSplit); $partialWriter.Flush($true)
                    $partialObservation = Read-P3LifecycleObservation $partialLifecyclePath $currentIdentity
                    $partialWriter.Write($secondLifecycleBytes,$secondLifecycleSplit,$secondLifecycleBytes.Length - $secondLifecycleSplit)
                    $newlineBytes = [Text.UTF8Encoding]::new($false).GetBytes([Environment]::NewLine)
                    $partialWriter.Write($newlineBytes,0,$newlineBytes.Length); $partialWriter.Flush($true)
                    $completedPartialObservation = Read-P3LifecycleObservation $partialLifecyclePath $currentIdentity
                } finally { $partialWriter.Dispose() }
                $partialTailObservationSafe = $partialObservation.complete_record_count -eq 1 -and
                    $partialObservation.has_partial_tail -and $partialObservation.events.Count -eq 1 -and
                    $completedPartialObservation.complete_record_count -eq 2 -and
                    -not $completedPartialObservation.has_partial_tail -and $completedPartialObservation.events.Count -eq 2
                $historicalLine = (Get-Content -LiteralPath $historicalEvidence.journal -Raw -Encoding UTF8).TrimEnd("`r","`n")
                $historicalRecord = $historicalLine | ConvertFrom-Json
                $currentRecord = (Get-Content -LiteralPath $currentEvidence.journal -Raw -Encoding UTF8) | ConvertFrom-Json
                $currentRecord.previous_summary_hash = [string]$historicalRecord.summary_hash
                $currentRecord.previous_record_sha256 = [string]$historicalRecord.summary_hash
                $currentRecord.PSObject.Properties.Remove('summary_hash')
                $currentRecord.PSObject.Properties.Remove('record_sha256')
                $currentJournalHash = Get-TextSha256 ($currentRecord | ConvertTo-Json -Depth 20 -Compress)
                $currentRecord | Add-Member -NotePropertyName summary_hash -NotePropertyValue $currentJournalHash
                $currentRecord | Add-Member -NotePropertyName record_sha256 -NotePropertyValue $currentJournalHash
                $sharedJournalPath = Join-Path $harnessRoot 'two-session-summary.ndjson'
                [IO.File]::WriteAllText($sharedJournalPath,
                    $historicalLine + [Environment]::NewLine + ($currentRecord | ConvertTo-Json -Depth 20 -Compress) + [Environment]::NewLine,
                    [Text.UTF8Encoding]::new($false))
                $currentSummary = Get-Content -LiteralPath $currentEvidence.summary -Raw -Encoding UTF8 | ConvertFrom-Json
                $multiSessionBinding = Read-P3SummaryJournalBindingObservation $sharedJournalPath $currentIdentity `
                    $currentSummary ([string]$currentSummary.record_sha256)
                $multiSessionJournalBound = $multiSessionBinding.matched -and $multiSessionBinding.complete_record_count -eq 2

                function Start-HarnessFileLocker(
                    [string]$Path,
                    [string]$Marker,
                    [int]$HoldMilliseconds,
                    [int]$AfterReleaseMilliseconds) {
                    $escapedPath = $Path.Replace("'", "''")
                    $escapedMarker = $Marker.Replace("'", "''")
                    $body = @"
                `$stream = [IO.FileStream]::new('$escapedPath',[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
                try {
                    [IO.File]::WriteAllText('$escapedMarker','locked',[Text.UTF8Encoding]::new(`$false))
                    [Threading.Thread]::Sleep($HoldMilliseconds)
                } finally { `$stream.Dispose() }
                [Threading.Thread]::Sleep($AfterReleaseMilliseconds)
                exit 0
                "@
                    $process = Start-HarnessProcess $body
                    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(5)
                    while (-not [IO.File]::Exists($Marker) -and [DateTimeOffset]::UtcNow -lt $deadline) {
                        if ($process.HasExited) { throw 'file-lock harness exited before acquiring its lock' }
                        [Threading.Thread]::Sleep(10)
                    }
                    Require-Harness ([IO.File]::Exists($Marker)) 'file-lock harness did not acquire its lock'
                    return $process
                }

                $contentionPath = Join-Path $harnessRoot 'transient-contention.ndjson'
                $contentionMarker = Join-Path $harnessRoot 'transient-contention.locked'
                [IO.File]::WriteAllText($contentionPath, 'ready', [Text.UTF8Encoding]::new($false))
                $contentionProcess = Start-HarnessFileLocker $contentionPath $contentionMarker 350 300
                $contentionClock = [Diagnostics.Stopwatch]::StartNew()
                $contentionState = [pscustomobject]@{ observed=$false; exit_code=$null; observed_at_utc='' }
                $contentionTimeline = [Collections.Generic.List[object]]::new()
                $contentionStages = [Collections.Generic.List[object]]::new()
                $contentionObservationFailures = [Collections.Generic.List[object]]::new()
                $contentionDetail = Invoke-P3ObservedStage 'transient-contention' 3 $contentionClock 3000 `
                    $contentionProcess $contentionState $contentionTimeline $contentionStages $contentionObservationFailures {
                        $value = Read-SharedUtf8Text $contentionPath
                        [pscustomobject]@{ satisfied=$value -ceq 'ready'; detail=$value }
                    } $false
                [void]$contentionProcess.WaitForExit(5000); $contentionProcess.WaitForExit(); $contentionProcess.Dispose()
                $contentionStage = $contentionStages[0]
                $transientContentionRecovered = $contentionDetail -ceq 'ready' -and
                    $contentionStage.outcome -ceq 'observed' -and
                    [int]$contentionStage.transient_file_contention_count -gt 0 -and
                    [int]$contentionStage.last_transient_file_contention.win32_error_code -in @(32,33) -and
                    $contentionObservationFailures.Count -eq 0 -and
                    $contentionTimeline.Count -eq 0

                $exitedContentionProcess = Start-HarnessProcess 'exit 0'
                [void]$exitedContentionProcess.WaitForExit(5000); $exitedContentionProcess.WaitForExit()
                $exitedContentionClock = [Diagnostics.Stopwatch]::StartNew()
                $exitedContentionState = [pscustomobject]@{ observed=$false; exit_code=$null; observed_at_utc='' }
                $exitedContentionTimeline = [Collections.Generic.List[object]]::new()
                $exitedContentionStages = [Collections.Generic.List[object]]::new()
                $exitedContentionFailures = [Collections.Generic.List[object]]::new()
                $script:exitedContentionProbeCount = 0
                $exitedContentionDetail = Invoke-P3ObservedStage 'exited-transient-contention' 2 `
                    $exitedContentionClock 2000 $exitedContentionProcess $exitedContentionState `
                    $exitedContentionTimeline $exitedContentionStages $exitedContentionFailures {
                        $script:exitedContentionProbeCount++
                        if ($script:exitedContentionProbeCount -eq 1) {
                            throw [IO.IOException]::new('lock-violation-sentinel', -2147024863)
                        }
                        [pscustomobject]@{ satisfied=$true; detail='recovered-after-exit' }
                    } $true
                $exitedContentionProcess.Dispose()
                $transientContentionPrecedesExitCheck = $exitedContentionDetail -ceq 'recovered-after-exit' -and
                    $script:exitedContentionProbeCount -eq 2 -and
                    [int]$exitedContentionStages[0].transient_file_contention_count -eq 1 -and
                    $exitedContentionFailures.Count -eq 0 -and $exitedContentionTimeline.Count -eq 0

                $persistentPath = Join-Path $harnessRoot 'persistent-contention.ndjson'
                $persistentMarker = Join-Path $harnessRoot 'persistent-contention.locked'
                [IO.File]::WriteAllText($persistentPath, 'ready', [Text.UTF8Encoding]::new($false))
                $persistentProcess = Start-HarnessFileLocker $persistentPath $persistentMarker 5000 0
                $persistentClock = [Diagnostics.Stopwatch]::StartNew()
                $persistentState = [pscustomobject]@{ observed=$false; exit_code=$null; observed_at_utc='' }
                $persistentTimeline = [Collections.Generic.List[object]]::new()
                $persistentStages = [Collections.Generic.List[object]]::new()
                $persistentObservationFailures = [Collections.Generic.List[object]]::new()
                $persistentFailure = $null
                try {
                    [void](Invoke-P3ObservedStage 'persistent-contention' 1 $persistentClock 1000 `
                        $persistentProcess $persistentState $persistentTimeline $persistentStages $persistentObservationFailures {
                            [pscustomobject]@{ satisfied=(Read-SharedUtf8Text $persistentPath) -ceq 'ready'; detail=$null }
                        } $false)
                } catch { $persistentFailure = $_ }
                try { $persistentProcess.Kill(); [void]$persistentProcess.WaitForExit(5000); $persistentProcess.WaitForExit() } catch { }
                $persistentProcess.Dispose()
                $persistentContentionTimedOut = $null -ne $persistentFailure -and
                    $persistentFailure.Exception.Message.Contains('timed out') -and
                    [int]$persistentStages[0].transient_file_contention_count -gt 0 -and
                    [int64]$persistentStages[0].elapsed_milliseconds -le 1250 -and
                    $persistentObservationFailures.Count -eq 0

                $nonSharingProcess = Start-HarnessProcess 'Start-Sleep -Seconds 5; exit 0'
                $nonSharingClock = [Diagnostics.Stopwatch]::StartNew()
                $nonSharingState = [pscustomobject]@{ observed=$false; exit_code=$null; observed_at_utc='' }
                $nonSharingTimeline = [Collections.Generic.List[object]]::new()
                $nonSharingStages = [Collections.Generic.List[object]]::new()
                $nonSharingObservationFailures = [Collections.Generic.List[object]]::new()
                $script:nonSharingProbeCount = 0; $nonSharingFailure = $null
                try {
                    [void](Invoke-P3ObservedStage 'non-sharing-io' 2 $nonSharingClock 2000 `
                        $nonSharingProcess $nonSharingState $nonSharingTimeline $nonSharingStages $nonSharingObservationFailures {
                            $script:nonSharingProbeCount++
                            throw [IO.IOException]::new('access-denied-sentinel', -2147024891)
                        } $false)
                } catch { $nonSharingFailure = $_ }
                try { $nonSharingProcess.Kill(); [void]$nonSharingProcess.WaitForExit(5000); $nonSharingProcess.WaitForExit() } catch { }
                $nonSharingProcess.Dispose()
                $nonSharingIoFailedClosed = $null -ne $nonSharingFailure -and
                    $nonSharingFailure.Exception.Message.Contains('access-denied-sentinel') -and
                    $script:nonSharingProbeCount -eq 1 -and
                    [int]$nonSharingStages[0].transient_file_contention_count -eq 0 -and
                    $nonSharingObservationFailures.Count -eq 1

                $identityProbe = Start-HarnessProcess 'Start-Sleep -Seconds 10; exit 0'
                $actualHash = Get-FileSha256 $powershellPath
                $runCreated = [DateTimeOffset]::UtcNow.AddSeconds(-2).ToString('O')
                $outsideRootRejected = $false; $wrongHashRejected = $false
                try { New-RunnerProcessOwnerToken $identityProbe $powershellPath $actualHash 'p3-auto-harness' ('1' * 32) $harnessRoot $runCreated | Out-Null }
                catch { $outsideRootRejected = $_.Exception.Message.Contains('sealed binary root') }
                try { New-RunnerProcessOwnerToken $identityProbe $powershellPath ('0' * 64) 'p3-auto-harness' ('1' * 32) (Split-Path -Parent $powershellPath) $runCreated | Out-Null }
                catch { $wrongHashRejected = $_.Exception.Message.Contains('differs from the sealed executable') }
                $identityProbe.Kill(); [void]$identityProbe.WaitForExit(5000); $identityProbe.Dispose()

                $exitedProbe = Start-HarnessProcess 'Start-Sleep -Milliseconds 250; exit 0'
                $exitedOwner = New-RunnerProcessOwnerToken $exitedProbe $powershellPath $actualHash 'p3-auto-harness' ('2' * 32) (Split-Path -Parent $powershellPath) $runCreated
                [void]$exitedProbe.WaitForExit(5000); $exitedProbe.WaitForExit()
                $exitedIdentityRejected = -not [bool](Test-RunnerOwnedProcessIdentity $exitedProbe $exitedOwner).valid
                $exitedProbe.Dispose()

                $normal = Start-HarnessProcess 'Start-Sleep -Milliseconds 250; exit 0'
                $normalIdentity = New-HarnessIdentity $normal; $normalOwner = New-HarnessOwner $normal
                $normalEvidence = Write-HarnessEvidence (Join-Path $harnessRoot 'normal') $normalIdentity
                $normalDiagnostic = $null
                [void](Wait-RunnerOwnedProcessExit $normal $normalOwner 5 'primary' 'normal' $normalIdentity.scenario_id `
                    $normalEvidence.lifecycle $normalEvidence.summary $normalEvidence.journal $normalIdentity ([ref]$normalDiagnostic))
                Require-Harness ($normalDiagnostic.outcome -ceq 'process-exited-and-tables-empty' -and $normal.ExitCode -eq 0) 'normal real process path failed'
                $normal.Dispose()

                $nonzero = Start-HarnessProcess 'Start-Sleep -Milliseconds 250; exit 7'
                $nonzeroIdentity = New-HarnessIdentity $nonzero; $nonzeroOwner = New-HarnessOwner $nonzero
                $nonzeroEvidence = Write-HarnessEvidence (Join-Path $harnessRoot 'nonzero') $nonzeroIdentity
                $nonzeroDiagnostic = $null; $nonzeroFailure = $null
                try { [void](Wait-RunnerOwnedProcessExit $nonzero $nonzeroOwner 5 'primary' 'nonzero' $nonzeroIdentity.scenario_id `
                    $nonzeroEvidence.lifecycle $nonzeroEvidence.summary $nonzeroEvidence.journal $nonzeroIdentity ([ref]$nonzeroDiagnostic)) } catch { $nonzeroFailure = $_ }
                Require-Harness ($null -ne $nonzeroFailure -and $nonzeroDiagnostic.outcome -ceq 'failed' -and
                    $nonzeroDiagnostic.primary_failure.stage -ceq 'process-exit-code' -and -not $nonzeroDiagnostic.forced_cleanup.required) 'nonzero real process did not fail before success'
                $nonzero.Dispose()

                $premature = Start-HarnessProcess 'Start-Sleep -Milliseconds 100; exit 0'
                $prematureIdentity = New-HarnessIdentity $premature; $prematureOwner = New-HarnessOwner $premature
                $prematureDiagnostic = $null; $prematureFailure = $null
                try { [void](Wait-RunnerOwnedProcessExit $premature $prematureOwner 2 'primary' 'premature' $prematureIdentity.scenario_id `
                    (Join-Path $harnessRoot 'premature-lifecycle.ndjson') (Join-Path $harnessRoot 'premature-summary.json') `
                    (Join-Path $harnessRoot 'premature-summary.ndjson') $prematureIdentity ([ref]$prematureDiagnostic)) } catch { $prematureFailure = $_ }
                $prematureTimeline = @($prematureDiagnostic.timeline)
                $prematureMarkedFailed = $null -ne $prematureFailure -and
                    @($prematureTimeline | Where-Object { $_.event -ceq 'premature-exit' -and $_.outcome -ceq 'failed' }).Count -eq 1 -and
                    @($prematureTimeline | Where-Object { $_.event -ceq 'normal-exit' }).Count -eq 0
                $premature.Dispose()

                $originalExitStateUpdater = ${function:Update-P3RetainedProcessExitState}
                $originalOwnerIdentityTest = ${function:Test-RunnerOwnedProcessIdentity}
                function Update-P3RetainedProcessExitState {
                    param($Process,$State,$Timeline,[string]$Outcome='normal')
                    if ($Outcome -ceq 'pre-forced-cleanup') {
                        $State.observed = $true; $State.exit_code = 0
                        $State.observed_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
                        Add-ProcessExitTimelineEvent $Timeline 'process-exit-observed' 'pre-forced-cleanup' $null
                    }
                }
                function Test-RunnerOwnedProcessIdentity { param($Process,$OwnerToken); throw 'owner-check-must-be-skipped' }
                $cleanupRace = Start-HarnessProcess 'Start-Sleep -Seconds 5; exit 0'
                $cleanupRaceIdentity = New-HarnessIdentity $cleanupRace; $cleanupRaceOwner = New-HarnessOwner $cleanupRace
                $cleanupRaceDiagnostic = $null; $cleanupRaceFailure = $null
                try { [void](Wait-RunnerOwnedProcessExit $cleanupRace $cleanupRaceOwner 1 'primary' 'cleanup-race' $cleanupRaceIdentity.scenario_id `
                    (Join-Path $harnessRoot 'cleanup-race-lifecycle.ndjson') (Join-Path $harnessRoot 'cleanup-race-summary.json') `
                    (Join-Path $harnessRoot 'cleanup-race-summary.ndjson') $cleanupRaceIdentity ([ref]$cleanupRaceDiagnostic)) }
                catch { $cleanupRaceFailure = $_ }
                $naturalExitSkippedKill = $null -ne $cleanupRaceFailure -and
                    -not $cleanupRaceDiagnostic.forced_cleanup.required -and
                    $cleanupRaceDiagnostic.forced_cleanup.started -and
                    $cleanupRaceDiagnostic.forced_cleanup.process_exit_observed -and
                    -not $cleanupRaceDiagnostic.forced_cleanup.kill_requested_through_retained_handle -and
                    $cleanupRaceDiagnostic.cleanup_failures.Count -eq 0 -and
                    @($cleanupRaceDiagnostic.timeline | Where-Object {
                        $_.event -ceq 'forced-cleanup-skipped' -and $_.outcome -ceq 'process-already-exited'
                    }).Count -eq 1
                try { $cleanupRace.Kill(); [void]$cleanupRace.WaitForExit(5000); $cleanupRace.WaitForExit() } catch { }
                $cleanupRace.Dispose()

                function Update-P3RetainedProcessExitState {
                    param($Process,$State,$Timeline,[string]$Outcome='normal')
                    if ($Outcome -ceq 'post-owner-check') {
                        $State.observed = $true; $State.exit_code = 0
                        $State.observed_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
                        Add-ProcessExitTimelineEvent $Timeline 'process-exit-observed' 'post-owner-check' $null
                    }
                }
                function Test-RunnerOwnedProcessIdentity { param($Process,$OwnerToken); [pscustomobject]@{ valid=$false; retained_handle_has_exited=$true } }
                $ownerRace = Start-HarnessProcess 'Start-Sleep -Seconds 5; exit 0'
                $ownerRaceIdentity = New-HarnessIdentity $ownerRace; $ownerRaceOwner = New-HarnessOwner $ownerRace
                $ownerRaceDiagnostic = $null; $ownerRaceFailure = $null
                try { [void](Wait-RunnerOwnedProcessExit $ownerRace $ownerRaceOwner 1 'primary' 'owner-race' $ownerRaceIdentity.scenario_id `
                    (Join-Path $harnessRoot 'owner-race-lifecycle.ndjson') (Join-Path $harnessRoot 'owner-race-summary.json') `
                    (Join-Path $harnessRoot 'owner-race-summary.ndjson') $ownerRaceIdentity ([ref]$ownerRaceDiagnostic)) }
                catch { $ownerRaceFailure = $_ }
                $ownerCheckExitSkippedKill = $null -ne $ownerRaceFailure -and
                    -not $ownerRaceDiagnostic.forced_cleanup.required -and
                    $ownerRaceDiagnostic.forced_cleanup.process_exit_observed -and
                    -not $ownerRaceDiagnostic.forced_cleanup.kill_requested_through_retained_handle -and
                    $ownerRaceDiagnostic.cleanup_failures.Count -eq 0 -and
                    @($ownerRaceDiagnostic.timeline | Where-Object {
                        $_.event -ceq 'forced-cleanup-skipped' -and $_.outcome -ceq 'process-exited-during-owner-check'
                    }).Count -eq 1
                try { $ownerRace.Kill(); [void]$ownerRace.WaitForExit(5000); $ownerRace.WaitForExit() } catch { }
                $ownerRace.Dispose()
                Set-Item -Path Function:Update-P3RetainedProcessExitState -Value $originalExitStateUpdater
                Set-Item -Path Function:Test-RunnerOwnedProcessIdentity -Value $originalOwnerIdentityTest

                function Test-RunnerOwnedProcessIdentity { param($Process,$OwnerToken); [pscustomobject]@{ valid=$true } }
                $timeout = Start-HarnessProcess 'Start-Sleep -Seconds 30; exit 0'
                $timeoutIdentity = New-HarnessIdentity $timeout; $timeoutOwner = New-HarnessOwner $timeout
                $timeoutDiagnostic = $null; $timeoutFailure = $null
                try { [void](Wait-RunnerOwnedProcessExit $timeout $timeoutOwner 1 'primary' 'timeout' $timeoutIdentity.scenario_id `
                    (Join-Path $harnessRoot 'missing-lifecycle.ndjson') (Join-Path $harnessRoot 'missing-summary.json') `
                    (Join-Path $harnessRoot 'missing-summary.ndjson') $timeoutIdentity ([ref]$timeoutDiagnostic)) }
                catch { $timeoutFailure = $_ }
                $timeout.Refresh()
                Require-Harness ($null -ne $timeoutFailure -and $timeout.HasExited -and $timeoutDiagnostic.forced_cleanup.required -and
                    $timeoutDiagnostic.forced_cleanup.kill_requested_through_retained_handle -and
                    $timeoutDiagnostic.primary_failure.stage -ceq 'completion-handshake') `
                    "timeout retained-handle cleanup failed: exited=$($timeout.HasExited), required=$($timeoutDiagnostic.forced_cleanup.required), kill=$($timeoutDiagnostic.forced_cleanup.kill_requested_through_retained_handle), stage=$($timeoutDiagnostic.primary_failure.stage), cleanup=$($timeoutDiagnostic.cleanup_failures | ConvertTo-Json -Compress), message=$($timeoutFailure.Exception.Message)"
                $timeout.Dispose()

                function Test-RunnerOwnedProcessIdentity { param($Process,$OwnerToken); [pscustomobject]@{ valid=$false } }
                $cleanup = Start-HarnessProcess 'Start-Sleep -Seconds 30; exit 0'
                $cleanupIdentity = New-HarnessIdentity $cleanup; $cleanupOwner = New-HarnessOwner $cleanup
                $cleanupDiagnostic = $null; $cleanupFailure = $null
                try { [void](Wait-RunnerOwnedProcessExit $cleanup $cleanupOwner 1 'primary' 'cleanup-failure' $cleanupIdentity.scenario_id `
                    (Join-Path $harnessRoot 'missing-lifecycle-2.ndjson') (Join-Path $harnessRoot 'missing-summary-2.json') `
                    (Join-Path $harnessRoot 'missing-summary-2.ndjson') $cleanupIdentity ([ref]$cleanupDiagnostic)) }
                catch { $cleanupFailure = $_ }
                $primaryPreserved = $null -ne $cleanupFailure -and $cleanupFailure.Exception.Message.Contains("completion-handshake") -and
                    $cleanupDiagnostic.primary_failure.stage -ceq 'completion-handshake' -and $cleanupDiagnostic.cleanup_failures.Count -eq 1
                try { $cleanup.Kill(); [void]$cleanup.WaitForExit(5000); $cleanup.WaitForExit() } catch { }
                $cleanup.Dispose()

                [pscustomobject]@{
                    powershell_major_minor="$($PSVersionTable.PSVersion.Major).$($PSVersionTable.PSVersion.Minor)"
                    raw_null_rejected=$rawNullRejected; id_to_pid=([int]$idSnapshot.Items[0].Pid -eq 4242)
                    raw_null_normalized=$rawNullNormalized; multi_session_journal_bound=$multiSessionJournalBound
                    partial_tail_observation_safe=$partialTailObservationSafe
                    transient_contention_recovered=$transientContentionRecovered
                    transient_contention_precedes_exit_check=$transientContentionPrecedesExitCheck
                    persistent_contention_timed_out=$persistentContentionTimedOut
                    nonsharing_io_failed_closed=$nonSharingIoFailedClosed
                    cim_datetime_accepted=$cimDateTimeAccepted; cim_throw_failed_closed=$cimThrowClosed; outside_root_rejected=$outsideRootRejected
                    wrong_hash_rejected=$wrongHashRejected; exited_identity_rejected=$exitedIdentityRejected
                    normal_process_passed=($normalDiagnostic.outcome -ceq 'process-exited-and-tables-empty')
                    nonzero_process_rejected=($null -ne $nonzeroFailure); premature_exit_marked_failed=$prematureMarkedFailed
                    natural_exit_skipped_kill=$naturalExitSkippedKill
                    owner_check_exit_skipped_kill=$ownerCheckExitSkippedKill
                    timeout_killed=($null -ne $timeoutFailure)
                    primary_preserved_with_cleanup_failure=$primaryPreserved
                } | ConvertTo-Json -Compress
                """;
            var harnessPath = System.IO.Path.Combine(temp, "StagedRunnerHarness.ps1");
            File.WriteAllText(harnessPath, harness, new System.Text.UTF8Encoding(false));
            var result = Start("powershell.exe",
                ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", harnessPath]);
            Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
            using var payload = JsonDocument.Parse(result.Output);
            var root = payload.RootElement;
            Assert.AreEqual("5.1", root.GetProperty("powershell_major_minor").GetString());
            foreach (var property in new[]
                     {
                          "raw_null_rejected", "raw_null_normalized", "multi_session_journal_bound", "partial_tail_observation_safe", "id_to_pid", "cim_datetime_accepted", "cim_throw_failed_closed", "outside_root_rejected",
                          "transient_contention_recovered", "transient_contention_precedes_exit_check", "persistent_contention_timed_out", "nonsharing_io_failed_closed",
                          "wrong_hash_rejected", "exited_identity_rejected", "normal_process_passed",
                          "nonzero_process_rejected", "premature_exit_marked_failed", "natural_exit_skipped_kill", "owner_check_exit_skipped_kill", "timeout_killed", "primary_preserved_with_cleanup_failure"
                     })
                Assert.IsTrue(root.GetProperty(property).GetBoolean(), property);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [TestMethod]
    public void RunnerAndValidatorSealOwnerScopedProcessExitDiagnostics()
    {
        var runner = Read("tools/AssetLibraryP3AutomatedAcceptance/Invoke-P3AssetLibraryAutomatedAcceptance.ps1");
        ContainsAll(runner,
            "public sealed class PixelTartP3ProcessIdentitySnapshotRow",
            "public sealed class PixelTartP3ProcessTableSnapshot",
            "function Get-RequiredJsonIntegerValue", "function Assert-P3LifecyclePendingOperationCount",
            "function Get-P3TransientFileContention", "transient_file_contention_count",
            "last_transient_file_contention",
            "'observed-nonnegative'", "pending_operation_count violates rule",
            "function New-RunnerProcessOwnerToken", "function Test-RunnerOwnedProcessIdentity",
            "function Wait-RunnerOwnedProcessExit", "function Wait-DevPreviewProcessTableConvergence",
            "function Invoke-FinalDevPreviewCheck", "function New-P3FinalFailureException",
            "$Process.Kill()", "kill_requested_through_retained_handle",
            "$expectedProcessSessionId = [guid]::NewGuid().ToString('N')",
            "process_session_id = $expectedProcessSessionId",
            "did not return its runner-preassigned process_session_id",
            "process-exit-observed", "forced-cleanup-start", "forced-cleanup-skipped", "forced-cleanup-completed",
            "same-run-process-zero", "completion-handshake-observed", "shutdown-preparation-observed",
            "application-on-exit-enter-observed", "application-on-exit-completed-observed", "phase-summary-commit",
            "primary_failure", "cleanup_failures", "observation_failures",
            "pixel-tart-p3-runner-session-result/v1", "result_sha256", "phase_summary_record_sha256",
            "[IO.FileMode]::CreateNew", "[IO.FileOptions]::WriteThrough", "$stream.Flush($true)",
            "[IO.File]::Replace($temporary, $fullPath, $backup)");
        var appPhaseStart = runner.IndexOf("function Invoke-AppPhase", StringComparison.Ordinal);
        var appPhaseEnd = runner.IndexOf("function New-P3SyntheticFixture", appPhaseStart, StringComparison.Ordinal);
        Assert.IsGreaterThan(0, appPhaseStart);
        Assert.IsGreaterThan(appPhaseStart, appPhaseEnd);
        Assert.IsFalse(runner[appPhaseStart..appPhaseEnd].Contains("Stop-Process -Id $process.Id", StringComparison.Ordinal));
        Assert.IsFalse(runner.Contains("$processes.pid", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(runner.Contains("$cimProcesses.pid", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(runner.Contains("@(Get-ProcessSnapshot).Count", StringComparison.Ordinal));
        Assert.IsFalse(runner.Contains("[Math]::Max(1L, $remainingMilliseconds)", StringComparison.Ordinal),
            "The process-table convergence stage must not receive time after the shared deadline is exhausted.");
        ContainsAll(runner,
            "exhausted its shared ${sharedBudgetSeconds}s deadline before process-table convergence",
            "exceeded its shared ${sharedBudgetSeconds}s deadline during process-table convergence");

        var validator = Read("tools/AssetLibraryP3AutomatedAcceptance/Test-P3AssetLibraryAutomatedEvidence.ps1");
        ContainsAll(validator,
            "function Require-PendingOperationCount", "required_application_lifecycle_pending_operation_count_rules",
            "contract process exit diagnostic schema", "embedded process exit diagnostic binding",
            "process owner fields", "process convergence required empty count",
            "'required','started','owner_identity_verified','kill_requested_through_retained_handle'", "process exit timeline",
            "successful manifest primary_failure must be null", "independent result recomputed record hash",
            "immutable phase summary record hash binding to summary journal", "must be a JSON integer number");
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
            "negative_fixture_proof_count", "negative_fixture_proof_sha256 -notmatch",
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
