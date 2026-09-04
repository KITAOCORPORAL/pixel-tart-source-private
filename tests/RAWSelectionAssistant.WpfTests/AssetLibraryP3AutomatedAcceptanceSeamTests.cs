using System.IO;
using System.Text.Json;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP3AutomatedAcceptanceSeamTests
{
    private static readonly string[] Scenarios =
    [
        "scope-switch/v1", "ime-cancellation/v1", "search-suggestions-history/v1",
        "folder-any-all-not/v1", "tag-any-all-not/v1", "scalar-null-composition/v1",
        "visual-composition/v1", "nested-canonical-query/v1", "invalid-query-fail-closed/v1",
        "smart-folder-lifecycle-preview/v1", "smart-folder-invalid-migration/v1",
        "tag-manager-lifecycle/v1", "bulk-metadata-journal/v1", "four-view-resilience-layout/v1"
    ];

    [TestMethod]
    public void DebugOnlyP3BuildFlagFlowsThroughAppAndModuleAndIsMutuallyExclusive()
    {
        foreach (var relative in new[]
                 {
                     "src/RAWSelectionAssistant/RAWSelectionAssistant.csproj",
                     "src/PixelTart.Modules.AssetLibrary/PixelTart.Modules.AssetLibrary.csproj"
                 })
        {
            var document = XDocument.Load(Path(relative));
            var constants = document.Descendants("DefineConstants")
                .Where(node => node.Value.Contains("ASSET_LIBRARY_P3_AUTOMATED_ACCEPTANCE", StringComparison.Ordinal)).ToArray();
            Assert.HasCount(1, constants, relative);
            var condition = constants[0].Attribute("Condition")?.Value ?? string.Empty;
            ContainsAll(condition, "ModularHarnessDevPreview", "AssetLibraryP3AutomatedAcceptance", "AcceptanceBuild", "Configuration", "Debug");
        }
        var appProject = Read("src/RAWSelectionAssistant/RAWSelectionAssistant.csproj");
        ContainsAll(appProject, "ValidateAssetLibraryP3AutomatedAcceptanceBuild",
            "cannot be combined with a P1 acceptance seam", "cannot be combined with a P2 acceptance seam",
            "IncludeSourceRevisionInInformationalVersion");
    }

    [TestMethod]
    public void AppStartupAndExitOwnTheP3Controller()
    {
        var app = Read("src/RAWSelectionAssistant/App.xaml.cs");
        ContainsAll(app, "#if ASSET_LIBRARY_P3_AUTOMATED_ACCEPTANCE",
            "AssetLibraryP3AutomatedAcceptanceController.TryCreate", "ApplyStartRoute(_mainViewModel)",
            "ConfigureAssetLibraryP3AutomatedAcceptance",
            "PrepareP3AutomatedShutdownAsync", "PrepareForApplicationExitAsync", "FinalizeOnApplicationExit",
            "FinalizeIncompleteApplicationExit", "_assetLibraryP3ShutdownPrepared");
        ContainsAll(app, "AssetLibraryP1AutomatedAcceptanceController.TryCreate", "AssetLibraryP2AutomatedAcceptanceController.TryCreate");
    }

    [TestMethod]
    public void P3ShutdownIsAwaitedBeforeCloseAndOnExitDoesNotSynchronouslyWait()
    {
        var app = Read("src/RAWSelectionAssistant/App.xaml.cs");
        var window = Read("src/RAWSelectionAssistant/MainWindow.AssetLibraryP3AutomatedAcceptance.cs");
        ContainsAll(window,
            "controller.MarkExecutionCompleted();",
            "RecordLifecycle(\"shutdown-requested\"",
            "RecordLifecycle(\"completion-ack-written\"",
            "RecordLifecycle(\"page-dispose-start\"",
            "pendingOperationCount: page.ViewModel.P3PendingOperationCount",
            "await TeardownAssetLibraryP3AutomatedAcceptanceAsync();",
            "RecordLifecycle(\"page-dispose-completed\"",
            "await driver.DisposeAsync();",
            "await application.PrepareP3AutomatedShutdownAsync(controller);",
            "Close();");
        var pageDisposeStart = window.IndexOf("controller.RecordLifecycle(\"page-dispose-start\"", StringComparison.Ordinal);
        var pageTeardownStart = window.IndexOf("await TeardownAssetLibraryP3AutomatedAcceptanceAsync();", pageDisposeStart, StringComparison.Ordinal);
        var pageDisposeCompleted = window.IndexOf("controller.RecordLifecycle(\"page-dispose-completed\"", pageTeardownStart, StringComparison.Ordinal);
        var applicationPreparationStart = window.IndexOf("await application.PrepareP3AutomatedShutdownAsync(controller);", pageDisposeCompleted, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, pageDisposeStart);
        Assert.IsGreaterThan(pageDisposeStart, pageTeardownStart);
        Assert.IsGreaterThan(pageTeardownStart, pageDisposeCompleted);
        Assert.IsGreaterThan(pageDisposeCompleted, applicationPreparationStart);
        StringAssert.Contains(window[pageDisposeStart..pageTeardownStart],
            "pendingOperationCount: page.ViewModel.P3PendingOperationCount");
        StringAssert.Contains(window[pageDisposeCompleted..applicationPreparationStart],
            "pendingOperationCount: page.ViewModel.P3PendingOperationCount");
        Assert.IsFalse(window.Contains("_assetLibraryP3AutomatedDriver?.Dispose();", StringComparison.Ordinal));
        AssertOrdered(window,
        [
            "controller.MarkExecutionCompleted();",
            "await TeardownAssetLibraryP3AutomatedAcceptanceAsync();",
            "await application.PrepareP3AutomatedShutdownAsync(controller);",
            "Close();",
        ]);

        ContainsAll(app,
            "private async Task PrepareP3AutomatedShutdownCoreAsync",
            "await _compositionRoot.BookingReminderScheduler.StopAsync();",
            "await _mainViewModel.SaveSettingsAsync();",
            "await _mainViewModel.TetherPage.DisposeAsync();",
            "await _moduleRegistry.ShutdownAsync();",
            "await _compositionRoot.BookingReminderScheduler.DisposeAsync();",
            "await controller.PrepareForApplicationExitAsync();",
            "_assetLibraryP3ShutdownPreparation is not { IsCompletedSuccessfully: true }");
        var p3ExitStart = app.IndexOf("private void CompleteP3AutomatedExit", StringComparison.Ordinal);
        var p3ExitEnd = app.IndexOf("#endif", p3ExitStart, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, p3ExitStart);
        Assert.IsGreaterThan(p3ExitStart, p3ExitEnd);
        var p3Exit = app[p3ExitStart..p3ExitEnd];
        Assert.IsFalse(p3Exit.Contains("GetAwaiter().GetResult()", StringComparison.Ordinal));
        Assert.IsFalse(p3Exit.Contains(".Result", StringComparison.Ordinal));
        Assert.IsFalse(p3Exit.Contains(".Wait(", StringComparison.Ordinal));
    }

#if ASSET_LIBRARY_P3_AUTOMATED_ACCEPTANCE
    [TestMethod]
    public void P3ExitFailureAlwaysResolvesToANonZeroProcessExitCode()
    {
        Assert.AreEqual(-1, RAWSelectionAssistant.App.ResolveFailedP3ExitCode(0));
        Assert.AreEqual(-7, RAWSelectionAssistant.App.ResolveFailedP3ExitCode(-7));
        Assert.AreEqual(9, RAWSelectionAssistant.App.ResolveFailedP3ExitCode(9));
    }

    [TestMethod]
    public void P3PhaseSummaryHashesTheExactMaterializedJournalRepresentation()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var typedSummary = new
        {
            started_at = new DateTimeOffset(2026, 9, 4, 14, 0, 0, TimeSpan.FromHours(8)),
            status = "completed",
        };
        var typedCanonical = JsonSerializer.Serialize(typedSummary, options);
        StringAssert.Contains(typedCanonical, "+08:00");
        var prematureTypedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(typedCanonical))).ToLowerInvariant();
        var materialized = JsonSerializer.Deserialize<Dictionary<string, object?>>(
            typedCanonical, options)!;
        var canonical = JsonSerializer.Serialize(materialized, options);
        var claimed = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        materialized["record_sha256"] = claimed;
        var summaryElement = JsonSerializer.SerializeToElement(materialized, options);
        var journal = new Dictionary<string, object?>
        {
            ["schema"] = "pixel-tart-p3-automated-summary/v1",
            ["summary"] = summaryElement,
            ["previous_summary_hash"] = new string('0', 64),
        };
        var journalLine = JsonSerializer.Serialize(journal, options);
        var summaryMarker = "\"summary\":";
        var terminalMarker = $",\"record_sha256\":\"{claimed}\"}},\"previous_summary_hash\"";
        var summaryStart = journalLine.IndexOf(summaryMarker, StringComparison.Ordinal) + summaryMarker.Length;
        var terminalStart = journalLine.IndexOf(terminalMarker, summaryStart, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(summaryMarker.Length, summaryStart);
        Assert.IsGreaterThan(summaryStart, terminalStart);
        StringAssert.Contains(journalLine, "\\u002B08:00");
        var recoveredCanonical = journalLine[summaryStart..terminalStart] + "}";
        var recoveredHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(recoveredCanonical))).ToLowerInvariant();
        Assert.AreEqual(claimed, recoveredHash);
        Assert.AreNotEqual(prematureTypedHash, recoveredHash,
            "Hashing the typed DateTimeOffset form would recreate the retained Run's false hash.");

        var controller = Read("src/RAWSelectionAssistant/Services/AssetLibraryP3AutomatedAcceptanceController.cs");
        ContainsAll(controller,
            "var phaseSummaryCanonical = JsonSerializer.Serialize(summaryPayload, LineJsonOptions);",
            "HashBytes(Encoding.UTF8.GetBytes(phaseSummaryCanonical))",
            "var summaryJournalCanonical = JsonSerializer.Serialize(journal, LineJsonOptions);");
        AssertOrdered(controller,
        [
            "JsonSerializer.Deserialize<Dictionary<string, object?>>(",
            "var phaseSummaryCanonical = JsonSerializer.Serialize(summaryPayload, LineJsonOptions);",
            "summaryPayload[\"record_sha256\"] = recordHash;",
            "var summaryElement = JsonSerializer.SerializeToElement(summaryPayload, LineJsonOptions);",
        ]);
    }

    [TestMethod]
    public void P3DurableJournalAppendAllowsRunnerReadsButRejectsConcurrentWriters()
    {
        var root = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "PixelTart-P3SharedJournal", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = System.IO.Path.Combine(root, "lifecycle.ndjson");
        File.WriteAllText(path, string.Empty, new System.Text.UTF8Encoding(false));
        try
        {
            var controllerType = typeof(RAWSelectionAssistant.App).Assembly.GetType(
                "RAWSelectionAssistant.Services.AssetLibraryP3AutomatedAcceptanceController", throwOnError: true)!;
            var append = controllerType.GetMethod(
                "AppendLineDurably",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;

            using (var runnerReader = new FileStream(
                       path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                append.Invoke(null, [path, "first"]);
                runnerReader.Position = 0;
                using var textReader = new StreamReader(
                    runnerReader, new System.Text.UTF8Encoding(false), true, 1024, leaveOpen: true);
                Assert.AreEqual("first" + Environment.NewLine, textReader.ReadToEnd());
            }

            using var productionWriter = new FileStream(
                path, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var concurrentRunnerReader = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Assert.Throws<IOException>(() =>
            {
                using var secondWriter = new FileStream(
                    path, FileMode.Append, FileAccess.Write, FileShare.Read);
            });

            var controller = Read("src/RAWSelectionAssistant/Services/AssetLibraryP3AutomatedAcceptanceController.cs");
            var appendStart = controller.IndexOf("private static void AppendLineDurably", StringComparison.Ordinal);
            var appendEnd = controller.IndexOf("private static string SanitizeFileName", appendStart, StringComparison.Ordinal);
            Assert.IsGreaterThanOrEqualTo(0, appendStart);
            Assert.IsGreaterThan(appendStart, appendEnd);
            var appendSource = controller[appendStart..appendEnd];
            StringAssert.Contains(appendSource, "FileShare.Read");
            Assert.IsFalse(appendSource.Contains("FileShare.None", StringComparison.Ordinal));
            Assert.IsFalse(appendSource.Contains("FileShare.ReadWrite", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
#endif

    [TestMethod]
    public void ImmutablePhaseSummaryPublishesBeforeTerminalLifecycleEvents()
    {
        var controller = Read("src/RAWSelectionAssistant/Services/AssetLibraryP3AutomatedAcceptanceController.cs");
        AssertOrdered(controller,
        [
            "WriteJsonAtomically(_phaseSummaryPath, summaryPayload, overwrite: false);",
            "RecordLifecycle(\"phase-summary-written\"",
            "RecordLifecycle(\"summary-commit-end\"",
        ]);

        var app = Read("src/RAWSelectionAssistant/App.xaml.cs");
        AssertOrdered(app,
        [
            "FinalizeOnApplicationExit(e.ApplicationExitCode);",
            "base.OnExit(e);",
            "RecordLifecycle(\n                \"application-on-exit-completed\"",
        ]);
    }

#if ASSET_LIBRARY_P3_AUTOMATED_ACCEPTANCE
    [TestMethod]
    public void AtomicWriterDoesNotReplaceExistingImmutablePhaseOnPublishFailure()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PixelTart-P3AtomicSummary", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = System.IO.Path.Combine(root, "summary-phase.json");
        File.WriteAllText(path, "original");
        try
        {
            var controllerType = typeof(RAWSelectionAssistant.App).Assembly.GetType(
                "RAWSelectionAssistant.Services.AssetLibraryP3AutomatedAcceptanceController", throwOnError: true)!;
            var method = controllerType.GetMethod(
                "WriteJsonAtomically",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            var thrown = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method.Invoke(null, [path, new { status = "completed" }, false]));
            Assert.IsInstanceOfType<IOException>(thrown.InnerException);
            Assert.AreEqual("original", File.ReadAllText(path));
            Assert.IsEmpty(Directory.GetFiles(root, "*.tmp"));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void ControllerPreservesTheFirstFailureWhenCleanupAlsoFails()
    {
        var controllerType = typeof(RAWSelectionAssistant.App).Assembly.GetType(
            "RAWSelectionAssistant.Services.AssetLibraryP3AutomatedAcceptanceController", throwOnError: true)!;
        var controller = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(controllerType);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        controllerType.GetField("_gate", flags)!.SetValue(controller, new object());
        var setFailure = controllerType.GetMethod("SetFailureOnce", flags)!;
        setFailure.Invoke(controller, ["primary failure"]);
        setFailure.Invoke(controller, ["cleanup failure"]);
        Assert.AreEqual("primary failure", controllerType.GetField("_failure", flags)!.GetValue(controller));
    }
#endif

    [TestMethod]
    public void P3LifecycleTimelineIsRunOwnedHashChainedAndFailureIsFirstWriterWins()
    {
        var controller = Read("src/RAWSelectionAssistant/Services/AssetLibraryP3AutomatedAcceptanceController.cs");
        ContainsAll(controller,
            "pixel-tart-p3-automated-lifecycle/v1",
            "lifecycle-{SanitizeFileName(_scenarioId.Replace('/', '-'))}-{_phase}.ndjson",
            "timestamp_utc", "stopwatch_elapsed_ms", "run_id", "phase", "process_session_id",
            "pid", "hwnd", "source_head", "executable_sha256", "application_sha256",
            "asset_module_sha256", "managed_thread_id", "event", "result", "pending", "exception",
            "previous_record_sha256", "record_sha256",
            "plan-completed", "completion-ack-written", "shutdown-requested",
            "shutdown-dispatch-started", "page-dispose-start", "page-dispose-completed",
            "application-async-dispose-start", "application-async-dispose-completed",
            "window-close-start", "window-close-completed", "application-shutdown-start",
            "application-on-exit-enter", "application-on-exit-completed",
            "summary-commit-start", "phase-summary-written", "summary-commit-end",
            "dispatcher_thread_id", "pending_operation_count", "exception.StackTrace",
            "P3 lifecycle event", "requestedStage <= _lifecycleStage",
            "lock (_gate) _failure ??= failure;");
        Assert.IsFalse(controller.Contains("_failure = $\"{exception.GetType().FullName}", StringComparison.Ordinal));
        Assert.IsFalse(controller.Contains("PrepareForApplicationExitAsync().GetAwaiter().GetResult()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ControllerOwnsExactScenariosFixturesAndSeventeenSessionEvidenceIdentity()
    {
        var controller = Read("src/RAWSelectionAssistant/Services/AssetLibraryP3AutomatedAcceptanceController.cs");
        foreach (var scenario in Scenarios) StringAssert.Contains(controller, $"\"{scenario}\"");
        ContainsAll(controller, "File.Copy(_fixtureDatabasePath, _databasePath, overwrite: false)",
            "BeforeRepositoryInitializationAsync", "SqliteAssetLibraryRepository", "fixture_variant",
            "current-v7", "legacy-v6", "legacyMigration ? 64 : 10128", "legacyMigration ? 60 : 10000",
            "legacyMigration ? 4 : 128", "schemaVersion != 7",
            "process_session_id", "executable_sha256", "application_sha256", "asset_module_sha256",
            "previous_event_hash", "event_hash", "previous_summary_hash", "summary_hash");
        ContainsAll(controller,
            "var processSessionId = RequireString(root, \"process_session_id\");",
            "^[0-9a-f]{32}$",
            "_processSessionId = plan.ProcessSessionId;",
            "string ProcessSessionId,",
            "summaryPayload[\"record_sha256\"] = recordHash;",
            "FileMode.CreateNew", "FileOptions.WriteThrough", "Flush(flushToDisk: true)",
            "File.Replace(temporaryPath, path, null)", "Guid.NewGuid():N");
        Assert.IsFalse(controller.Contains(
            "private readonly string _processSessionId = Guid.NewGuid().ToString(\"N\")",
            StringComparison.Ordinal));
        foreach (var directory in new[]
                 {
                     "bounds", "query-documents", "query-plans", "result-hashes", "histories",
                     "smart-folders", "tags", "memberships", "journals", "commands", "selections",
                     "views", "performance", "databases", "screenshots"
                 }) StringAssert.Contains(controller, $"\"{directory}\"");
    }

    [TestMethod]
    public void MainWindowDispatchesAllFourteenRealWpfScenarios()
    {
        var window = Read("src/RAWSelectionAssistant/MainWindow.AssetLibraryP3AutomatedAcceptance.cs");
        foreach (var symbol in new[]
                 {
                     "ScopeSwitchScenario", "ImeCancellationScenario", "SearchSuggestionsHistoryScenario",
                     "FolderAnyAllNotScenario", "TagAnyAllNotScenario", "ScalarNullCompositionScenario",
                     "VisualCompositionScenario", "NestedCanonicalQueryScenario", "InvalidQueryFailClosedScenario",
                     "SmartFolderLifecyclePreviewScenario", "SmartFolderInvalidMigrationScenario",
                     "TagManagerLifecycleScenario", "BulkMetadataJournalScenario", "FourViewResilienceLayoutScenario"
                 }) StringAssert.Contains(window, symbol);
        ContainsAll(window, "AssetLibraryWorkspace.Content is not AssetLibraryWpfPage", "CaptureFrameAsync",
            "RenderTargetBitmap", "CaptureVisibleBounds", "WriteJsonArtifact", "MarkScenarioCompleted");
    }

    [TestMethod]
    public void QueryEvidenceCoversCanonicalPlansCancellationFailClosedAndMigration()
    {
        var window = Read("src/RAWSelectionAssistant/MainWindow.AssetLibraryP3AutomatedAcceptance.cs");
        ContainsAll(window, "canonical_json", "canonical_sha256", "asset_id_sha256", "parameterized",
            "unparameterized_sql_count", "explain_query_plan", "cancelled_generation_published",
            "suggestions_suppressed_during_composition", "persisted_after_restart", "predicate_variant",
            "rule_count", "fail_closed", "canonical_roundtrip", "preview_isolated",
            "migrated_schema_version", "invalid_reference_fail_closed");
    }

    [TestMethod]
    public void TagBatchJournalFourViewsDpiAndPerformanceAreMeasured()
    {
        var window = Read("src/RAWSelectionAssistant/MainWindow.AssetLibraryP3AutomatedAcceptance.cs");
        ContainsAll(window, "pixel-tart-p3-tag-manager-lifecycle/v2",
            "group_create_command_changed_state", "group_rename_command_changed_state",
            "group_reorder_command_changed_state", "group_order_before_sha256", "group_order_after_sha256",
            "tag_create_command_changed_state", "tag_rename_command_changed_state",
            "tag_reorder_command_changed_state", "tag_move_command_changed_state",
            "tag_archive_command_changed_state", "tag_restore_command_changed_state",
            "archive_restore_preserved_memberships", "merge_source_membership_count_before",
            "merge_target_membership_count_before", "merge_overlap_count_before",
            "merge_source_membership_count_after", "merge_target_membership_count_after",
            "merge_duplicate_membership_count", "merge_source_archived", "merge_memberships_deduplicated",
            "group_cycle_rejected", "group_cycle_proof", "rename_preserved_memberships",
            "batch_size", "committed_count", "atomic",
            "undo_passed", "redo_passed", "chain_valid", "deduplicated",
            "AssetLibraryViewMode.Grid", "AssetLibraryViewMode.Masonry", "Evidence: \"waterfall\"",
            "AssetLibraryViewMode.Justified", "AssetLibraryViewMode.List", "selection_sha256",
            "result_sha256", "first_screen_10000", "search_suggestion", "single_filter_update",
            "nested_8_rule_query", "smart_folder_preview", "scope_switch", "batch_tag_100",
            "batch_tag_500", "ui_block");
        foreach (var value in new[] { "(1366, 768, 100)", "(1920, 1080, 125)", "(1920, 1080, 150)", "(2560, 1440, 200)" })
            StringAssert.Contains(window, value);
        ContainsAll(window, "real_display_settings_changed = false", "has_overflow = false",
            "contrast_passed = readability.Passed", "CapturePublishedQueryAsync", "inspector-selection-states.json",
            "ExecuteMeasuredBatchTagCommandAsync", "content-state-recovery.json");
        Assert.IsFalse(window.Contains("contrast_passed = true", StringComparison.Ordinal));
        Assert.IsFalse(window.Contains("ChangeDisplaySettings", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DriverUsesProductCommandsAndSnapshotsWithoutDesktopAutomationOrReflection()
    {
        var driver = Read("src/PixelTart.Modules.AssetLibrary/AssetLibraryP3AutomatedAcceptanceDriver.cs");
        ContainsAll(driver, "CaptureCanonicalQueryDocument", "CaptureParameterizedQueryPlan",
            "CaptureResultAssetIds", "ResolveAcceptanceDocumentAsync", "ResolveQueryReferencesAsync",
            "P3SmartFolderSortField = document.SortField", "P3SmartFolderSortDirection = document.SortDirection",
            "P3SmartFolderIncludeArchived = document.IncludeArchived",
            "P3QueryScope = scope", "SwitchViewCommand.Execute",
            "SaveP3SmartFolderCommand.Execute", "RetryP3SmartFolderPreviewCommand.Execute",
            "CreateP3TagGroupCommand.Execute", "RenameP3TagGroupCommand.Execute",
            "MoveP3TagGroupCommand.Execute", "CreateP3TagCommand.Execute", "RenameP3TagCommand.Execute",
            "ReorderP3TagCommand.Execute", "MoveP3TagCommand.Execute", "ToggleArchiveP3TagCommand.Execute",
            "PreviewP3TagMergeCommand.Execute", "MergeP3TagCommand.Execute",
            "PreviewP3BatchMetadataCommand.Execute", "ApplyP3BatchMetadataCommand.Execute",
            "P2UndoCommand.Execute", "P2RedoCommand.Execute",
            "P3AcceptancePublishedQueryGeneration == _viewModel.P3AcceptanceQueryGeneration",
            "P3BatchApplyCompletionGeneration > previousCompletionGeneration",
            "P3BatchApplyCompletionOutcome != AssetLibraryP3BatchApplyOutcome.Succeeded",
            "P3BatchApplyCompletionOperationId != completedToken.OperationId",
            "!_viewModel.IsLoading && !_viewModel.HasLoadError",
            "_viewModel.IsOrganizationLoading || _viewModel.HasOrganizationError",
            "did not reach a stable UI state",
            "VirtualizingPanel.GetIsVirtualizing", "CaptureVisibleBounds",
            "CaptureButtonReadabilityMatrix", "AccessibleIdentity", "AnalyzeUndoJournal",
            "ExerciseContentStateRecoveryAsync", "MaximumDispatcherGapMilliseconds", "PreviewIsolated");
        Assert.IsFalse(driver.Contains("GroupCycleRejected: true", StringComparison.Ordinal));
        Assert.IsFalse(driver.Contains("JsonSerializer.SerializeToElement(group)", StringComparison.Ordinal));
        Assert.IsFalse(driver.Contains("EnumerateObject().Any(property", StringComparison.Ordinal));
        foreach (var forbidden in new[]
                 {
                     "SendInput", "SetForegroundWindow", "AutomationElement", "InvokePattern",
                     "BindingFlags", "GetField(", "GetProperty(", "Eagle.exe"
                 }) Assert.IsFalse(driver.Contains(forbidden, StringComparison.Ordinal), forbidden);
    }

    [TestMethod]
    public void TagLifecycleReorderProofUsesAnAdjacentPermutationAndCanonicalOrderHashes()
    {
        var driver = Read("src/PixelTart.Modules.AssetLibrary/AssetLibraryP3AutomatedAcceptanceDriver.cs");
        ContainsAll(driver,
            "var groupOrderBefore = _viewModel.P3ManagedTagGroups",
            "var tagOrderBefore = _viewModel.P3ManagedTags",
            "_viewModel.P3ManagedTagGroups.Select(item => item.TagGroupId)",
            "_viewModel.P3ManagedTags.Select(item => item.TagId)",
            "BuildOneStepReorder(groupOrderBefore, sourceGroup.TagGroupId)",
            "BuildOneStepReorder(tagOrderBefore, source.TagId)",
            "var target = index == 0 ? 1 : index - 1;",
            "(expected[index], expected[target]) = (expected[target], expected[index]);",
            "return new(index == 0 ? \"down\" : \"up\", expected);",
            "Sha256GuidOrder(groupOrderBefore)", "Sha256GuidOrder(groupOrderAfter)",
            "Sha256GuidOrder(tagOrderBefore)", "Sha256GuidOrder(tagOrderAfter)",
            "sourceMembershipsBeforeMerge.Select(item => item.AssetId)",
            ".Intersect(targetMembershipsBeforeMerge.Select(item => item.AssetId)).Count()",
            "targetMembershipsAfterMerge.Count == expectedMergedMembershipCount");
        Assert.IsFalse(driver.Contains(
            "var groupOrderBefore = (await _acceptanceRepository.ListTagGroupsAsync",
            StringComparison.Ordinal));
        Assert.IsFalse(driver.Contains(
            "var tagOrderBefore = (await _acceptanceRepository.ListTagsAsync",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void ContractRunnerAndControllerUseTheSameScenarioOrder()
    {
        using var contract = JsonDocument.Parse(Read("tools/AssetLibraryP3AutomatedAcceptance/automated-acceptance-contract.json"));
        CollectionAssert.AreEqual(Scenarios, contract.RootElement.GetProperty("required_scenario_order")
            .EnumerateArray().Select(item => item.GetString()).ToArray());
        var runner = Read("tools/AssetLibraryP3AutomatedAcceptance/Invoke-P3AssetLibraryAutomatedAcceptance.ps1");
        var controller = Read("src/RAWSelectionAssistant/Services/AssetLibraryP3AutomatedAcceptanceController.cs");
        AssertOrdered(runner, Scenarios.Select(scenario => $"'{scenario}'").ToArray());
        AssertOrdered(controller, Scenarios.Select(scenario => $"\"{scenario}\"").ToArray());
    }

    private static void ContainsAll(string text, params string[] values)
    {
        foreach (var value in values) StringAssert.Contains(text, value);
    }
    private static void AssertOrdered(string text, string[] values)
    {
        var previous = 0;
        foreach (var value in values)
        {
            var current = text.IndexOf(value, previous, StringComparison.Ordinal);
            Assert.IsGreaterThanOrEqualTo(previous, current, $"Missing or out-of-order value: {value}");
            previous = current + value.Length;
        }
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
