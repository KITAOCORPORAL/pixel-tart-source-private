#if ASSET_LIBRARY_P3_AUTOMATED_ACCEPTANCE
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Services;
using AssetLibraryWpfPage = PixelTart.Modules.AssetLibrary.AssetLibraryPage;

namespace RAWSelectionAssistant;

public partial class MainWindow
{
    private static readonly (int Width, int Height, int ScalePercent)[] AssetLibraryP3LayoutMatrix =
    [
        (1366, 768, 100),
        (1920, 1080, 125),
        (1920, 1080, 150),
        (2560, 1440, 200),
    ];

    private AssetLibraryP3AutomatedAcceptanceController? _assetLibraryP3AutomatedController;
    private AssetLibraryP3AutomatedAcceptanceDriver? _assetLibraryP3AutomatedDriver;
    private AssetLibraryWpfPage? _assetLibraryP3AutomatedPage;

    internal void ConfigureAssetLibraryP3AutomatedAcceptance(AssetLibraryP3AutomatedAcceptanceController controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        if (_assetLibraryP3AutomatedController is not null)
            throw new InvalidOperationException("The live window may host only one P3 automated acceptance controller.");
        _assetLibraryP3AutomatedController = controller;
        Loaded += AssetLibraryP3AutomatedAcceptance_Loaded;
        Closed += (_, _) => _assetLibraryP3AutomatedDriver?.Dispose();
    }

    private async void AssetLibraryP3AutomatedAcceptance_Loaded(object sender, RoutedEventArgs e)
    {
        var controller = _assetLibraryP3AutomatedController
            ?? throw new InvalidOperationException("The P3 automated acceptance controller is unavailable.");
        try
        {
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
            if (_viewModel is null) throw new InvalidOperationException("The live MainWindow has no MainViewModel.");
            if (AssetLibraryWorkspace.Content is not AssetLibraryWpfPage page)
                throw new InvalidOperationException("AssetLibraryWorkspace.Content is not AssetLibraryWpfPage.");
            _assetLibraryP3AutomatedPage = page;
            var driver = new AssetLibraryP3AutomatedAcceptanceDriver(page, controller.DatabasePath);
            _assetLibraryP3AutomatedDriver = driver;
            controller.Observe(_viewModel, driver);
            await ExecuteAssetLibraryP3AutomatedScenarioAsync(controller, driver);
            controller.MarkExecutionCompleted();
            await TeardownAssetLibraryP3AutomatedAcceptanceAsync();
            Close();
        }
        catch (Exception exception)
        {
            controller.Fail(exception);
            try { await TeardownAssetLibraryP3AutomatedAcceptanceAsync(); }
            catch (Exception teardown) { controller.Fail(new AggregateException(exception, teardown)); }
            Application.Current.Shutdown(-1);
        }
    }

    internal async Task TeardownAssetLibraryP3AutomatedAcceptanceAsync()
    {
        _assetLibraryP3AutomatedDriver?.Dispose();
        _assetLibraryP3AutomatedDriver = null;
        var page = _assetLibraryP3AutomatedPage ?? AssetLibraryWorkspace.Content as AssetLibraryWpfPage;
        if (page is null) return;
        _assetLibraryP3AutomatedPage = page;
        await page.DisposeAsync();
    }

    private async Task ExecuteAssetLibraryP3AutomatedScenarioAsync(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3AutomatedAcceptanceDriver driver)
    {
        controller.SetActiveScenario(controller.ScenarioId);
        var firstScreen = Stopwatch.StartNew();
        await WaitForAssetLibraryStateAsync(driver, state => state.IsReady && !state.HasLoadError,
            "the repository-backed P3 browser");
        firstScreen.Stop();

        switch (controller.ScenarioId)
        {
            case AssetLibraryP3AutomatedAcceptanceController.ScopeSwitchScenario:
                await RunScopeSwitchScenarioAsync(controller, driver);
                break;
            case AssetLibraryP3AutomatedAcceptanceController.ImeCancellationScenario:
                await RunImeCancellationScenarioAsync(controller, driver);
                break;
            case AssetLibraryP3AutomatedAcceptanceController.SearchSuggestionsHistoryScenario:
                await RunSearchSuggestionsHistoryScenarioAsync(controller, driver);
                break;
            case AssetLibraryP3AutomatedAcceptanceController.FolderAnyAllNotScenario:
                await RunCompositionScenarioAsync(controller, driver, FolderCompositionDocuments());
                break;
            case AssetLibraryP3AutomatedAcceptanceController.TagAnyAllNotScenario:
                await RunCompositionScenarioAsync(controller, driver, TagCompositionDocuments());
                break;
            case AssetLibraryP3AutomatedAcceptanceController.ScalarNullCompositionScenario:
                await RunCompositionScenarioAsync(controller, driver, ScalarCompositionDocuments());
                break;
            case AssetLibraryP3AutomatedAcceptanceController.VisualCompositionScenario:
                await RunCompositionScenarioAsync(controller, driver, VisualCompositionDocuments());
                break;
            case AssetLibraryP3AutomatedAcceptanceController.NestedCanonicalQueryScenario:
                await RunNestedCanonicalScenarioAsync(controller, driver);
                break;
            case AssetLibraryP3AutomatedAcceptanceController.InvalidQueryFailClosedScenario:
                await RunInvalidQueryScenarioAsync(controller, driver);
                break;
            case AssetLibraryP3AutomatedAcceptanceController.SmartFolderLifecyclePreviewScenario:
                await RunSmartFolderLifecycleScenarioAsync(controller, driver);
                break;
            case AssetLibraryP3AutomatedAcceptanceController.SmartFolderInvalidMigrationScenario:
                await RunSmartFolderMigrationScenarioAsync(controller, driver);
                break;
            case AssetLibraryP3AutomatedAcceptanceController.TagManagerLifecycleScenario:
                await RunTagManagerLifecycleScenarioAsync(controller, driver);
                break;
            case AssetLibraryP3AutomatedAcceptanceController.BulkMetadataJournalScenario:
                await RunBulkMetadataJournalScenarioAsync(controller, driver);
                break;
            case AssetLibraryP3AutomatedAcceptanceController.FourViewResilienceLayoutScenario:
                await RunFourViewLayoutPerformanceScenarioAsync(controller, driver, firstScreen.Elapsed.TotalMilliseconds);
                break;
            default:
                throw new InvalidOperationException($"Unsupported P3 automated scenario '{controller.ScenarioId}'.");
        }
    }

    private async Task RunScopeSwitchScenarioAsync(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3AutomatedAcceptanceDriver driver)
    {
        var clock = Stopwatch.StartNew();
        await driver.SelectFirstFolderAsync();
        var current = Document(AssetQueryScope.Current,
            Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, "4"));
        await driver.ApplyQueryDocumentAsync(current);
        var currentResult = await driver.CapturePublishedQueryAsync();
        var currentPlan = await driver.CaptureParameterizedQueryPlanAsync();

        var all = current with { Scope = AssetQueryScope.AllAssets };
        await driver.ApplyQueryDocumentAsync(all);
        var allResult = await driver.CapturePublishedQueryAsync();
        if (allResult.TotalCount <= currentResult.TotalCount ||
            string.Equals(allResult.AssetIdSha256, currentResult.AssetIdSha256, StringComparison.Ordinal))
            throw new InvalidOperationException("The real Current/AllAssets scope switch did not expand beyond the selected folder.");

        var smartDocument = Document(AssetQueryScope.AllAssets,
            Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, "4"));
        var smart = await driver.SaveSmartFolderAndPreviewAsync("验收范围智能文件夹", smartDocument);
        await driver.SelectSmartFolderAsync(smart.SmartFolderId);
        var smartCurrent = Document(AssetQueryScope.Current,
            Rule(AssetQueryField.FileName, AssetQueryOperator.Contains, "P3_0"));
        await driver.ApplyQueryDocumentAsync(smartCurrent);
        var smartResult = await driver.CapturePublishedQueryAsync();
        var smartAll = smartCurrent with { Scope = AssetQueryScope.AllAssets };
        await driver.ApplyQueryDocumentAsync(smartAll);
        var smartAllResult = await driver.CapturePublishedQueryAsync();
        if (smartAllResult.TotalCount < smartResult.TotalCount)
            throw new InvalidOperationException("AllAssets unexpectedly narrowed the real Smart Folder Current result.");
        clock.Stop();

        WriteQueryDocument(controller, driver.CaptureCanonicalQueryDocument(all), "scope-all-query.json");
        WriteQueryDocument(controller, driver.CaptureCanonicalQueryDocument(current), "scope-current-query.json");
        WriteQueryPlan(controller, currentPlan, "scope-parameterized-plan.json");
        WritePublishedResult(controller, allResult, "scope-all-results.json", "all-library");
        WritePublishedResult(controller, currentResult, "scope-folder-current-results.json", "current-folder");
        WritePublishedResult(controller, smartResult, "scope-smart-folder-current-results.json", "current-smart-folder");
        WritePublishedResult(controller, smartAllResult, "scope-smart-folder-all-results.json", "all-from-smart-folder");
        controller.RecordScenarioCheck(controller.ScenarioId, "smart_folder_id", smart.SmartFolderId.ToString("D"));
        controller.RecordScenarioCheck(controller.ScenarioId, "scope_switch_ms", clock.Elapsed.TotalMilliseconds);
        await CaptureFrameAsync(controller, driver, "scope-switch", 1366, 768, 1);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunImeCancellationScenarioAsync(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3AutomatedAcceptanceDriver driver)
    {
        var ime = await driver.ExerciseImeCancellationAsync("人物素材");
        if (!ime.SuggestionsSuppressedDuringComposition || ime.CancelledGenerationPublished ||
            !ime.QueryCancellationObserved || ime.CancelledQueryGenerationPublished ||
            ime.PublishedQueryGeneration <= ime.CancelledQueryGeneration)
            throw new InvalidOperationException("The real P3 IME and query cancellation generation contract failed.");
        var document = Document(AssetQueryScope.AllAssets,
            Rule(AssetQueryField.FileName, AssetQueryOperator.Contains, "人物素材"));
        await driver.ApplyQueryDocumentAsync(document);
        var canonical = driver.CaptureCanonicalQueryDocument(document);
        var result = await driver.CaptureResultAssetIds(document);
        controller.WriteJsonArtifact(controller.ScenarioId, "query-document", "ime-cancellation-query.json", Evidence(controller, new
        {
            canonical_json = canonical.CanonicalJson,
            canonical_sha256 = canonical.CanonicalSha256,
            cancelled_generation_published = ime.CancelledGenerationPublished,
            suggestions_suppressed_during_composition = ime.SuggestionsSuppressedDuringComposition,
            superseded_generation = ime.SupersededGeneration,
            published_generation = ime.PublishedGeneration,
            query_cancellation_observed = ime.QueryCancellationObserved,
            cancelled_query_generation_published = ime.CancelledQueryGenerationPublished,
            cancelled_query_generation = ime.CancelledQueryGeneration,
            published_query_generation = ime.PublishedQueryGeneration,
        }));
        WriteResult(controller, result, "ime-cancellation-results.json");
        await CaptureFrameAsync(controller, driver, "ime-cancellation", 1366, 768, 1);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunSearchSuggestionsHistoryScenarioAsync(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3AutomatedAcceptanceDriver driver)
    {
        const string search = "P3_000";
        var document = new AssetQueryDocument { Scope = AssetQueryScope.AllAssets, Text = search };
        if (!controller.IsRestartPhase)
        {
            var history = await driver.ExerciseSearchSuggestionsAndHistoryAsync(search);
            if (!history.History.Contains(search, StringComparer.Ordinal))
                throw new InvalidOperationException("The real P3 history did not record the submitted query.");
            if (!history.Deduplicated || !history.SingleEntryRemoved || !history.AllEntriesCleared)
                throw new InvalidOperationException("The real P3 history deduplicate/remove/clear contract failed.");
            controller.RecordScenarioCheck(controller.ScenarioId, "persisted_search", search);
            WriteQueryDocument(controller, driver.CaptureCanonicalQueryDocument(document), "history-query-primary.json");
            WriteResult(controller, await driver.CaptureResultAssetIds(document), "history-results-primary.json");
            controller.WriteJsonArtifact(controller.ScenarioId, "history", "history-lifecycle-primary.json", Evidence(controller, new
            {
                entries = history.History,
                suggestion_count = history.SuggestionCount,
                suggestions_suppressed_during_composition = history.SuggestionsSuppressedDuringComposition,
                deduplicated = history.Deduplicated,
                single_entry_removed = history.SingleEntryRemoved,
                all_entries_cleared = history.AllEntriesCleared,
            }));
        }
        else
        {
            var expected = controller.RequireStringScenarioCheck(controller.ScenarioId, "persisted_search");
            var history = driver.CaptureSearchHistory(expected);
            var persisted = history.History.Contains(expected, StringComparer.Ordinal);
            if (!persisted) throw new InvalidOperationException("The P3 history did not survive the real process restart.");
            controller.WriteJsonArtifact(controller.ScenarioId, "history", "history-restart.json", Evidence(controller, new
            {
                entries = history.History,
                persisted_after_restart = persisted,
                suggestions_suppressed_during_composition = history.SuggestionsSuppressedDuringComposition,
            }));
        }
        await CaptureFrameAsync(controller, driver, controller.IsRestartPhase ? "history-restart" : "history-primary", 1366, 768, 1);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunCompositionScenarioAsync(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3AutomatedAcceptanceDriver driver,
        IReadOnlyList<(string Variant, AssetQueryDocument Document)> variants)
    {
        for (var index = 0; index < variants.Count; index++)
        {
            var entry = variants[index];
            var document = await driver.ResolveAcceptanceDocumentAsync(entry.Document);
            await driver.ApplyQueryDocumentAsync(document);
            var canonical = driver.CaptureCanonicalQueryDocument(document);
            var result = await driver.CaptureResultAssetIds(document);
            WriteQueryDocument(controller, canonical, $"{index:D2}-{entry.Variant}-query.json");
            WriteResult(controller, result, $"{index:D2}-{entry.Variant}-results.json", predicateVariant: entry.Variant);
        }
        await CaptureFrameAsync(controller, driver, "composition", 1366, 768, 1);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunNestedCanonicalScenarioAsync(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3AutomatedAcceptanceDriver driver)
    {
        var nested = NestedEightRuleDocument();
        await driver.ApplyQueryDocumentAsync(nested);
        var canonical = driver.CaptureCanonicalQueryDocument(nested);
        if (canonical.RuleCount != 8) throw new InvalidOperationException("The P3 nested query does not contain exactly eight rules.");
        var result = await driver.CaptureResultAssetIds(nested);
        var reparsed = AssetQueryDocumentCodec.Parse(canonical.CanonicalJson);
        var canonicalRoundtrip = reparsed.IsValid && reparsed.Document is not null &&
                                 string.Equals(
                                     AssetQueryDocumentCodec.SerializeCanonical(reparsed.Document),
                                     canonical.CanonicalJson,
                                     StringComparison.Ordinal);
        if (!canonicalRoundtrip)
            throw new InvalidOperationException("The three-level P3 canonical query did not round-trip through the production codec.");
        controller.WriteJsonArtifact(controller.ScenarioId, "query-document", "nested-eight-rule-query.json", Evidence(controller, new
        {
            canonical_json = canonical.CanonicalJson,
            canonical_sha256 = canonical.CanonicalSha256,
            rule_count = canonical.RuleCount,
            canonical_roundtrip = canonicalRoundtrip,
        }));
        WriteResult(controller, result, "nested-eight-rule-results.json");
        await CaptureFrameAsync(controller, driver, "nested-canonical-query", 1920, 1080, 1.25);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunInvalidQueryScenarioAsync(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3AutomatedAcceptanceDriver driver)
    {
        var invalidReference = Document(AssetQueryScope.AllAssets,
            Rule(AssetQueryField.Folder, AssetQueryOperator.AnyOf,
                "id:00000000-0000-0000-0000-000000000000"));
        var canonical = driver.CaptureCanonicalQueryDocument(invalidReference);
        var result = await driver.CaptureResultAssetIds(invalidReference);
        if (result.ResultCount != 0 || string.IsNullOrWhiteSpace(result.ErrorMessage))
            throw new InvalidOperationException("The real P3 repository did not fail closed for an invalid stable reference.");
        WriteQueryDocument(controller, canonical, "invalid-reference-query.json");
        var failClosedCases = new List<object>
        {
            new { kind = "invalid-reference", result_count = result.ResultCount, error = result.ErrorMessage },
        };
        foreach (var invalid in new (string Kind, AssetQueryDocument Document)[]
                 {
                     ("future-version", Document(AssetQueryScope.AllAssets) with { Version = 99 }),
                     ("unknown-field", Document(AssetQueryScope.AllAssets,
                         Rule((AssetQueryField)999, AssetQueryOperator.Equals, "x"))),
                     ("unknown-operator", Document(AssetQueryScope.AllAssets,
                         Rule(AssetQueryField.FileName, (AssetQueryOperator)999, "x"))),
                     ("invalid-typed-value", Document(AssetQueryScope.AllAssets,
                         Rule(AssetQueryField.Rating, AssetQueryOperator.Equals, "99"))),
                     ("unsupported-field-operator", Document(AssetQueryScope.AllAssets,
                         Rule(AssetQueryField.MediaType, AssetQueryOperator.Regex, "Image"))),
                 })
        {
            var invalidResult = await driver.CaptureResultAssetIds(invalid.Document);
            if (invalidResult.ResultCount != 0 || string.IsNullOrWhiteSpace(invalidResult.ErrorMessage))
                throw new InvalidOperationException($"The real P3 repository did not fail closed for {invalid.Kind}.");
            failClosedCases.Add(new
            {
                kind = invalid.Kind,
                result_count = invalidResult.ResultCount,
                error = invalidResult.ErrorMessage,
            });
        }
        var damagedJson = AssetQueryDocumentCodec.Parse("{\"version\":1,\"rootGroup\":");
        if (damagedJson.IsValid || damagedJson.Errors.Count == 0)
            throw new InvalidOperationException("The real P3 query codec accepted damaged JSON.");
        failClosedCases.Add(new
        {
            kind = "damaged-json",
            result_count = 0,
            error = damagedJson.ErrorMessage,
        });
        controller.WriteJsonArtifact(controller.ScenarioId, "result-hash", "invalid-reference-results.json", Evidence(controller, new
        {
            result_kind = "complete",
            asset_ids = result.AssetIds,
            asset_id_sha256 = result.AssetIdSha256,
            result_count = result.ResultCount,
            total_count = result.ResultCount,
            fail_closed = true,
            validation_error = result.ErrorMessage,
            fail_closed_cases = failClosedCases,
        }));
        await CaptureFrameAsync(controller, driver, "invalid-query-fail-closed", 1366, 768, 1);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunSmartFolderLifecycleScenarioAsync(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3AutomatedAcceptanceDriver driver)
    {
        var document = Document(AssetQueryScope.AllAssets,
            Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, "3"),
            Rule(AssetQueryField.Tag, AssetQueryOperator.AnyOf, "name:精选"));
        if (!controller.IsRestartPhase)
        {
            document = await driver.ResolveAcceptanceDocumentAsync(document);
            var smart = await driver.SaveSmartFolderAndPreviewAsync("验收智能文件夹生命周期", document);
            var canonicalRoundtrip = string.Equals(smart.LoadedCanonicalJson, smart.ExpectedCanonicalJson, StringComparison.Ordinal);
            if (!canonicalRoundtrip || !smart.PreviewIsolated || !smart.CancellationIsolated ||
                !smart.EditorClosedAfterCancellation)
                throw new InvalidOperationException(
                    $"The Smart Folder preview/save/cancel contract did not round-trip (canonical={canonicalRoundtrip}, preview={smart.PreviewIsolated}, cancellation={smart.CancellationIsolated}, closed={smart.EditorClosedAfterCancellation}).");
            controller.RecordScenarioCheck(controller.ScenarioId, "smart_folder_id", smart.SmartFolderId.ToString("D"));
            controller.RecordScenarioCheck(controller.ScenarioId, "smart_folder_hash", smart.PersistedAfterPreviewSha256);
            WriteQueryDocument(controller, driver.CaptureCanonicalQueryDocument(document), "smart-folder-query.json");
            WriteResult(controller, await driver.CaptureResultAssetIds(document), "smart-folder-results.json");
            controller.WriteJsonArtifact(controller.ScenarioId, "smart-folder", "smart-folder-lifecycle.json", Evidence(controller, new
            {
                smart_folder_id = smart.SmartFolderId,
                canonical_roundtrip = canonicalRoundtrip,
                preview_isolated = smart.PreviewIsolated,
                saved_canonical_sha256 = smart.SavedCanonicalSha256,
                loaded_canonical_sha256 = smart.LoadedCanonicalSha256,
                editor_before_preview_sha256 = smart.EditorBeforePreviewSha256,
                editor_after_preview_sha256 = smart.EditorAfterPreviewSha256,
                persisted_before_preview_sha256 = smart.PersistedBeforePreviewSha256,
                persisted_after_preview_sha256 = smart.PersistedAfterPreviewSha256,
                cancellation_isolated = smart.CancellationIsolated,
                editor_closed_after_cancellation = smart.EditorClosedAfterCancellation,
                preview_count = smart.PreviewCount,
                preview_ms = smart.PreviewMilliseconds,
                archive_restore_passed = smart.ArchiveRestorePassed,
            }));
        }
        else
        {
            var id = Guid.Parse(controller.RequireStringScenarioCheck(controller.ScenarioId, "smart_folder_id"));
            var persisted = await driver.CapturePersistedSmartFolderAsync(id);
            var expectedHash = controller.RequireStringScenarioCheck(controller.ScenarioId, "smart_folder_hash");
            if (!string.Equals(persisted.PersistedAfterPreviewSha256, expectedHash, StringComparison.Ordinal) ||
                !persisted.PreviewIsolated || !persisted.CancellationIsolated ||
                !persisted.EditorClosedAfterCancellation)
                throw new InvalidOperationException("The Smart Folder canonical document changed across restart.");
            controller.WriteJsonArtifact(controller.ScenarioId, "smart-folder", "smart-folder-restart-roundtrip.json", Evidence(controller, new
            {
                smart_folder_id = persisted.SmartFolderId,
                saved_canonical_sha256 = expectedHash,
                restart_loaded_canonical_sha256 = persisted.LoadedCanonicalSha256,
                editor_before_preview_sha256 = persisted.EditorBeforePreviewSha256,
                editor_after_preview_sha256 = persisted.EditorAfterPreviewSha256,
                persisted_before_preview_sha256 = persisted.PersistedBeforePreviewSha256,
                persisted_after_preview_sha256 = persisted.PersistedAfterPreviewSha256,
                preview_isolated = persisted.PreviewIsolated,
                cancellation_isolated = persisted.CancellationIsolated,
                editor_closed_after_cancellation = persisted.EditorClosedAfterCancellation,
                exact_restart_roundtrip = string.Equals(persisted.PersistedAfterPreviewSha256, expectedHash, StringComparison.Ordinal),
            }));
        }
        await CaptureFrameAsync(controller, driver, controller.IsRestartPhase ? "smart-folder-restart" : "smart-folder-primary", 1366, 768, 1);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunSmartFolderMigrationScenarioAsync(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3AutomatedAcceptanceDriver driver)
    {
        var migration = await driver.CaptureLegacyMigrationAsync();
        var document = Document(AssetQueryScope.AllAssets,
            Rule(AssetQueryField.FileName, AssetQueryOperator.Contains, "旧规则迁移样本"));
        WriteQueryDocument(controller, driver.CaptureCanonicalQueryDocument(document), "migration-query.json");
        WriteResult(controller, await driver.CaptureResultAssetIds(document), "migration-results.json");
        controller.WriteJsonArtifact(controller.ScenarioId, "smart-folder", "smart-folder-migration.json", Evidence(controller, new
        {
            migrated_schema_version = 7,
            query_document_version = migration.MigratedSchemaVersion,
            migration.QueryHash,
            invalid_reference_fail_closed = migration.InvalidReferenceFailClosed,
        }));
        await CaptureFrameAsync(controller, driver, "smart-folder-migration", 1366, 768, 1);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunTagManagerLifecycleScenarioAsync(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3AutomatedAcceptanceDriver driver)
    {
        var tag = await driver.MergeTagsAsync();
        controller.WriteJsonArtifact(controller.ScenarioId, "tag", "tag-manager-lifecycle.json", Evidence(controller, new
        {
            merge_duplicate_membership_count = tag.MergeDuplicateMembershipCount,
            group_cycle_rejected = tag.GroupCycleRejected,
            group_hierarchy_model = "flat-no-parent-reference",
            rename_preserved_memberships = tag.RenamePreservedMemberships,
            rename_command_changed_state = tag.RenameCommandChangedState,
            merge_changed_count = tag.MergeChangedCount,
        }));
        await CaptureFrameAsync(controller, driver, "tag-manager-lifecycle", 1366, 768, 1);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunBulkMetadataJournalScenarioAsync(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3AutomatedAcceptanceDriver driver)
    {
        if (!controller.IsRestartPhase)
        {
            var batch100 = await driver.ExecuteBatchTagCommand(100);
            var batch500 = await driver.ExecuteBatchTagCommand(500);
            foreach (var batch in new[] { batch100, batch500 })
            {
                controller.WriteJsonArtifact(controller.ScenarioId, "command", $"batch-{batch.BatchSize}.json", Evidence(controller, new
                {
                    batch_size = batch.BatchSize,
                    committed_count = batch.CommittedCount,
                    preview_count = batch.PreviewCount,
                    atomic = batch.CommittedCount == batch.BatchSize,
                    undo_passed = batch.UndoPassed,
                    redo_passed = batch.RedoPassed,
                    elapsed_ms = batch.Elapsed.TotalMilliseconds,
                    batch.OperationId,
                }));
            }
            controller.RecordScenarioCheck(controller.ScenarioId, "batch_100_operation", batch100.OperationId);
            controller.RecordScenarioCheck(controller.ScenarioId, "batch_500_operation", batch500.OperationId);
            var journal = await driver.CaptureUndoJournalAsync();
            var consistency = driver.AnalyzeUndoJournal(journal, [batch100.OperationId, batch500.OperationId]);
            if (!consistency.IsValid)
                throw new InvalidOperationException("The real P3 undo journal failed structural consistency validation.");
            controller.WriteJsonArtifact(controller.ScenarioId, "journal", "undo-journal-chain.json", Evidence(controller, new
            {
                chain_valid = consistency.IsValid,
                consistency,
                entries = journal,
            }));
            controller.WriteJsonArtifact(controller.ScenarioId, "membership", "batch-memberships.json", Evidence(controller, new
            {
                deduplicated = batch100.MembershipsDeduplicated && batch500.MembershipsDeduplicated,
                batch_sizes = new[] { 100, 500 },
            }));
        }
        else
        {
            var journal = await driver.CaptureUndoJournalAsync();
            var expected = new[]
            {
                controller.RequireStringScenarioCheck(controller.ScenarioId, "batch_100_operation"),
                controller.RequireStringScenarioCheck(controller.ScenarioId, "batch_500_operation"),
            };
            var actual = journal.Select(item => item.Token.OperationId.ToString("D")).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!expected.All(actual.Contains))
                throw new InvalidOperationException("The real P3 undo journal did not persist both batch operations across restart.");
            var consistency = driver.AnalyzeUndoJournal(journal, expected);
            if (!consistency.IsValid)
                throw new InvalidOperationException("The persisted P3 undo journal failed structural consistency validation.");
            var restartRoundTrip = await driver.VerifyPersistedBatchUndoRedoAsync(expected);
            controller.WriteJsonArtifact(controller.ScenarioId, "journal", "undo-redo-restart-roundtrip.json", Evidence(controller, new
            {
                persisted_operation_ids = expected,
                before_membership_count = restartRoundTrip.BeforeMembershipCount,
                undone_membership_count = restartRoundTrip.UndoneMembershipCount,
                redone_membership_count = restartRoundTrip.RedoneMembershipCount,
                before_membership_sha256 = restartRoundTrip.BeforeMembershipSha256,
                undone_membership_sha256 = restartRoundTrip.UndoneMembershipSha256,
                redone_membership_sha256 = restartRoundTrip.RedoneMembershipSha256,
                exact_restart_roundtrip = restartRoundTrip.BeforeMembershipSha256 == restartRoundTrip.RedoneMembershipSha256,
                journal_consistency = consistency,
            }));
        }
        await CaptureFrameAsync(controller, driver, controller.IsRestartPhase ? "bulk-journal-restart" : "bulk-journal-primary", 1920, 1080, 1.25);
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task RunFourViewLayoutPerformanceScenarioAsync(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3AutomatedAcceptanceDriver driver,
        double firstScreenMs)
    {
        var all = Document(AssetQueryScope.AllAssets);
        await driver.ApplyQueryDocumentAsync(all);
        var baselinePublished = await driver.CapturePublishedQueryAsync();

        await driver.ClearSelectionAsync();
        var emptySelectionInspector = driver.CaptureBrowserSnapshot();
        await driver.SelectFirstAssetsAsync(1);
        var singleSelectionInspector = driver.CaptureBrowserSnapshot();
        var selected = await driver.SelectFirstAssetsAsync(10);
        var multipleSelectionInspector = driver.CaptureBrowserSnapshot();
        if (emptySelectionInspector.SelectionCount != 0 || emptySelectionInspector.InspectorMode != "query" ||
            singleSelectionInspector.SelectionCount != 1 || singleSelectionInspector.InspectorMode != "single" ||
            multipleSelectionInspector.SelectionCount != 10 || multipleSelectionInspector.InspectorMode != "multiple" ||
            string.IsNullOrWhiteSpace(singleSelectionInspector.SingleFolderSummary) ||
            string.IsNullOrWhiteSpace(singleSelectionInspector.SingleTagSummary) ||
            string.IsNullOrWhiteSpace(multipleSelectionInspector.MultipleFolderSummary) ||
            string.IsNullOrWhiteSpace(multipleSelectionInspector.MultipleTagSummary) ||
            string.IsNullOrWhiteSpace(multipleSelectionInspector.MultipleRatingSummary))
            throw new InvalidOperationException("The real 0/1/multiple selection did not publish the required inspector states and summaries.");
        var selectionHash = Sha256Text(string.Join("\n", selected.OrderBy(value => value, StringComparer.Ordinal)));
        controller.WriteJsonArtifact(controller.ScenarioId, "selection", "inspector-selection-states.json", Evidence(controller, new
        {
            zero = emptySelectionInspector,
            one = singleSelectionInspector,
            multiple = multipleSelectionInspector,
        }));
        var views = new List<object>();
        foreach (var entry in new[]
                 {
                     (Mode: AssetLibraryViewMode.Grid, Evidence: "grid"),
                     (Mode: AssetLibraryViewMode.Masonry, Evidence: "waterfall"),
                     (Mode: AssetLibraryViewMode.Justified, Evidence: "justified"),
                     (Mode: AssetLibraryViewMode.List, Evidence: "list"),
                 })
        {
            await driver.SwitchViewAsync(entry.Mode);
            var published = await driver.CapturePublishedQueryAsync();
            var snapshot = driver.CaptureBrowserSnapshot();
            var currentSelectionHash = Sha256Text(string.Join("\n", snapshot.SelectedAssetIds.OrderBy(value => value, StringComparer.Ordinal)));
            if (!string.Equals(published.AssetIdSha256, baselinePublished.AssetIdSha256, StringComparison.Ordinal) ||
                published.TotalCount != baselinePublished.TotalCount ||
                !string.Equals(currentSelectionHash, selectionHash, StringComparison.Ordinal))
                throw new InvalidOperationException($"The live '{entry.Mode}' view changed the published result or selection identity.");
            views.Add(new
            {
                mode = entry.Evidence,
                result_sha256 = published.AssetIdSha256,
                oracle_result_sha256 = published.OracleAssetIdSha256,
                published_query_generation = published.QueryGeneration,
                published_asset_ids = published.AssetIds,
                selection_sha256 = currentSelectionHash,
                is_virtualizing = snapshot.IsVirtualizing,
                virtualization_mode = snapshot.VirtualizationMode,
                realized_item_count = snapshot.RealizedItemCount,
                query_total_count = published.TotalCount,
            });
        }
        controller.WriteJsonArtifact(controller.ScenarioId, "view", "four-view-result-stability.json", Evidence(controller, new { views }));
        controller.WriteJsonArtifact(controller.ScenarioId, "selection", "four-view-selection-stability.json", Evidence(controller, new
        {
            asset_ids = selected,
            selection_sha256 = selectionHash,
        }));

        var contentStates = await driver.ExerciseContentStateRecoveryAsync();
        if (!contentStates.EmptyStateObserved || !contentStates.ErrorStateObserved ||
            !contentStates.LoadingObservedDuringRetry || !contentStates.RetryRecoveredReadyState ||
            !contentStates.CancelledStateObserved ||
            string.IsNullOrWhiteSpace(contentStates.RetryButtonAccessibleIdentity))
            throw new InvalidOperationException("The real empty/error/loading/retry state round-trip did not complete.");
        controller.WriteJsonArtifact(controller.ScenarioId, "view", "content-state-recovery.json", Evidence(controller, contentStates));

        foreach (var entry in AssetLibraryP3LayoutMatrix)
            await CaptureFrameAsync(controller, driver,
                $"dpi-{entry.Width}x{entry.Height}-{entry.ScalePercent}",
                entry.Width, entry.Height, entry.ScalePercent / 100d);

        var suggestionElapsed = await driver.MeasureRepositorySuggestionsAsync("P3_001");
        var singleElapsed = await driver.MeasureFirstPageQueryAsync(Document(AssetQueryScope.AllAssets,
            Rule(AssetQueryField.Rating, AssetQueryOperator.Equals, "5")));
        var nestedElapsed = await driver.MeasureFirstPageQueryAsync(NestedEightRuleDocument());
        var preview = await driver.SaveSmartFolderAndPreviewAsync("验收性能预览", Document(AssetQueryScope.AllAssets,
            Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, "4")));
        var scopeClock = Stopwatch.StartNew();
        await driver.SwitchScopeAsync(AssetQueryScope.Current);
        await driver.SwitchScopeAsync(AssetQueryScope.AllAssets);
        scopeClock.Stop();
        var measuredBatch100 = await driver.ExecuteMeasuredBatchTagCommandAsync(100);
        var measuredBatch500 = await driver.ExecuteMeasuredBatchTagCommandAsync(500);
        var batch100 = measuredBatch100.Batch;
        var batch500 = measuredBatch500.Batch;
        var maximumDispatcherGap = Math.Max(
            measuredBatch100.MaximumDispatcherGapMilliseconds,
            measuredBatch500.MaximumDispatcherGapMilliseconds);
        var liveButtonAcceptance = await CaptureLiveButtonStateAcceptanceAsync(controller, driver);
        var readability = liveButtonAcceptance.Readability;
        if (!readability.Passed)
            throw new InvalidOperationException($"The measured P3 button readability contract failed: {string.Join("; ", readability.Failures)}");

        controller.WriteJsonArtifact(controller.ScenarioId, "performance", "aggregate-performance.json", Evidence(controller, new
        {
            metrics = new
            {
                first_screen_10000 = firstScreenMs,
                search_suggestion = suggestionElapsed.TotalMilliseconds,
                single_filter_update = singleElapsed.TotalMilliseconds,
                nested_8_rule_query = nestedElapsed.TotalMilliseconds,
                smart_folder_preview = (double)preview.PreviewMilliseconds,
                scope_switch = scopeClock.Elapsed.TotalMilliseconds,
                batch_tag_100 = batch100.Elapsed.TotalMilliseconds,
                batch_tag_500 = batch500.Elapsed.TotalMilliseconds,
                ui_block = maximumDispatcherGap,
            },
            thresholds_ms = new
            {
                first_screen_10000 = 1500,
                search_suggestion = 200,
                single_filter_update = 300,
                nested_8_rule_query = 600,
                smart_folder_preview = 750,
                scope_switch = 400,
                batch_tag_100 = 750,
                batch_tag_500 = 2000,
                ui_block = 100,
            },
            dispatcher_samples = new
            {
                batch_100 = measuredBatch100.DispatcherSampleCount,
                batch_500 = measuredBatch500.DispatcherSampleCount,
                batch_100_average_gap_ms = measuredBatch100.AverageDispatcherGapMilliseconds,
                batch_500_average_gap_ms = measuredBatch500.AverageDispatcherGapMilliseconds,
            },
            real_display_settings_changed = false,
            contrast_passed = readability.Passed,
            contrast_failures = readability.Failures,
            button_state_theme_count = 2,
            live_button_state_record_count = liveButtonAcceptance.Matrix.Count,
        }));
        controller.MarkScenarioCompleted(controller.ScenarioId);
    }

    private async Task<(IReadOnlyList<AssetLibraryP3AutomatedButtonReadabilityState> Matrix,
        P3ButtonReadabilityEvaluation Readability)> CaptureLiveButtonStateAcceptanceAsync(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3AutomatedAcceptanceDriver driver)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var themeIndex = dictionaries
            .Select((dictionary, index) => new { dictionary, index })
            .FirstOrDefault(item => item.dictionary.Source?.OriginalString.Contains(
                "DesignSystem/Theme.", StringComparison.OrdinalIgnoreCase) == true)?.index ?? -1;
        if (themeIndex < 0)
            throw new InvalidOperationException("The live application has no replaceable design-system theme dictionary.");
        var originalTheme = dictionaries[themeIndex];
        var matrix = new List<AssetLibraryP3AutomatedButtonReadabilityState>();
        var screenshotFiles = new List<string>();
        try
        {
            foreach (var (theme, fileName) in new[] { ("dark", "Dark"), ("high-contrast", "HighContrast") })
            {
                dictionaries[themeIndex] = new ResourceDictionary
                {
                    Source = new Uri($"Resources/DesignSystem/Theme.{fileName}.xaml", UriKind.Relative),
                };
                RootGrid.InvalidateVisual();
                await driver.DrainDispatcherAsync();
                matrix.AddRange(driver.CaptureButtonReadabilityMatrix(theme));
                foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled", "error" })
                {
                    driver.ShowButtonStateSurface(theme, state);
                    await driver.DrainDispatcherAsync();
                    var file = $"button-state-{theme}-{state}.png";
                    controller.WriteArtifact(controller.ScenarioId, "screenshot", file, CaptureLiveRootPng());
                    screenshotFiles.Add(file);
                    driver.RemoveButtonStateSurface();
                }
            }
        }
        finally
        {
            driver.RemoveButtonStateSurface();
            dictionaries[themeIndex] = originalTheme;
            RootGrid.InvalidateVisual();
            await driver.DrainDispatcherAsync();
        }
        var readability = EvaluateButtonReadability(matrix, driver.CaptureRealizedButtons());
        controller.WriteJsonArtifact(controller.ScenarioId, "view", "live-button-state-matrix.json", Evidence(controller, new
        {
            schema = "pixel-tart-p3-live-visual-tree-button-state-matrix/v1",
            themes = new[] { "dark", "high-contrast" },
            states = new[] { "normal", "hover", "pressed", "focus", "disabled", "error" },
            live_visual_tree = true,
            real_display_settings_changed = false,
            screenshots = screenshotFiles,
            matrix,
            contrast_passed = readability.Passed,
            contrast_failures = readability.Failures,
        }));
        return (matrix, readability);
    }

    private byte[] CaptureLiveRootPng()
    {
        RootGrid.UpdateLayout();
        var width = Math.Max(1, (int)Math.Ceiling(RootGrid.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(RootGrid.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(RootGrid);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private void WriteQueryDocument(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3CanonicalQuerySnapshot canonical,
        string fileName) =>
        controller.WriteJsonArtifact(controller.ScenarioId, "query-document", fileName, Evidence(controller, new
        {
            canonical_json = canonical.CanonicalJson,
            canonical_sha256 = canonical.CanonicalSha256,
            rule_count = canonical.RuleCount,
        }));

    private void WriteQueryPlan(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3ParameterizedQueryPlan plan,
        string fileName) =>
        controller.WriteJsonArtifact(controller.ScenarioId, "query-plan", fileName, Evidence(controller, new
        {
            parameterized = plan.Parameterized,
            unparameterized_sql_count = plan.UnparameterizedSqlCount,
            parameter_count = plan.ParameterCount,
            explain_query_plan = plan.ExplainQueryPlan,
            canonical_sha256 = plan.CanonicalSha256,
            sql_template = plan.SqlTemplate,
            sql_template_sha256 = plan.SqlTemplateSha256,
            parameter_names = plan.ParameterNames,
            parameter_value_sha256 = plan.ParameterValueSha256,
            explain_rows = plan.ExplainRows,
        }));

    private void WriteResult(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3QueryResultSnapshot result,
        string fileName,
        string? scope = null,
        string? predicateVariant = null) =>
        controller.WriteJsonArtifact(controller.ScenarioId, "result-hash", fileName, Evidence(controller, new
        {
            result_kind = "complete",
            asset_ids = result.AssetIds,
            asset_id_sha256 = result.AssetIdSha256,
            result_count = result.ResultCount,
            total_count = result.ResultCount,
            scope,
            predicate_variant = predicateVariant,
            elapsed_ms = result.Elapsed.TotalMilliseconds,
            error = result.ErrorMessage,
        }));

    private void WritePublishedResult(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3PublishedQuerySnapshot result,
        string fileName,
        string scope) =>
        controller.WriteJsonArtifact(controller.ScenarioId, "result-hash", fileName, Evidence(controller, new
        {
            result_kind = "published-page",
            asset_ids = result.AssetIds,
            asset_id_sha256 = result.AssetIdSha256,
            oracle_asset_id_sha256 = result.OracleAssetIdSha256,
            result_count = result.AssetIds.Count,
            total_count = result.TotalCount,
            published_page_count = result.AssetIds.Count,
            result.QueryGeneration,
            scope,
            folder_id = result.FolderId,
            smart_folder_id = result.SmartFolderId,
            viewmodel_oracle_match = result.AssetIdSha256 == result.OracleAssetIdSha256,
        }));

    private object Evidence(AssetLibraryP3AutomatedAcceptanceController controller, object payload) => new
    {
        schema = "pixel-tart-p3-automated-snapshot/v1",
        validation_mode = "automated",
        run_id = controller.RunId,
        source_head = controller.SourceHead,
        scenario_id = controller.ScenarioId,
        process_session_id = controller.ProcessSessionId,
        pid = Environment.ProcessId,
        hwnd = controller.Hwnd,
        payload,
        captured_at = DateTimeOffset.UtcNow,
    };

    private sealed record P3ButtonReadabilityEvaluation(bool Passed, IReadOnlyList<string> Failures);

    private static P3ButtonReadabilityEvaluation EvaluateButtonReadability(
        IReadOnlyList<AssetLibraryP3AutomatedButtonReadabilityState> matrix,
        IReadOnlyList<AssetLibraryP3AutomatedButtonState> realizedButtons)
    {
        var failures = new List<string>();
        foreach (var requiredState in new[] { "normal", "hover", "focus", "disabled", "error" })
            if (!matrix.Any(state => string.Equals(state.State, requiredState, StringComparison.Ordinal)))
                failures.Add($"missing-state:{requiredState}");

        foreach (var state in matrix)
        {
            if (!state.LiveWpfButtonInstance || !state.TemplateApplied)
                failures.Add($"unrealized-template:{state.ButtonIdentity}/{state.State}");
            if (!string.Equals(state.State, "disabled", StringComparison.Ordinal) &&
                state.TextContrastApplicable && state.TextContrast is < 4.5)
                failures.Add($"text-contrast:{state.ButtonIdentity}/{state.State}/{state.TextContrast:0.00}");
            if (!string.Equals(state.State, "disabled", StringComparison.Ordinal) &&
                state.NonTextContrastApplicable && state.NonTextContrast is < 3)
                failures.Add($"non-text-contrast:{state.ButtonIdentity}/{state.State}/{state.NonTextContrast:0.00}");
            if (string.Equals(state.State, "focus", StringComparison.Ordinal) &&
                (!state.FocusVisible || state.FocusContrast < 3))
                failures.Add($"focus-contrast:{state.ButtonIdentity}/{state.FocusContrast:0.00}");
        }

        foreach (var button in realizedButtons)
            if (string.IsNullOrWhiteSpace(button.AccessibleIdentity) || button.AccessibleIdentitySource == "missing")
                failures.Add($"missing-accessible-identity:{button.Identity}");
        return new(failures.Count == 0, failures);
    }

    private async Task CaptureFrameAsync(
        AssetLibraryP3AutomatedAcceptanceController controller,
        AssetLibraryP3AutomatedAcceptanceDriver driver,
        string label,
        int physicalWidth,
        int physicalHeight,
        double scale)
    {
        await driver.DrainDispatcherAsync();
        var logicalWidth = physicalWidth / scale;
        var logicalHeight = physicalHeight / scale;
        WindowState = WindowState.Normal;
        Width = Math.Max(MinWidth, logicalWidth);
        Height = Math.Max(MinHeight, logicalHeight);
        RootGrid.Measure(new Size(logicalWidth, logicalHeight));
        RootGrid.Arrange(new Rect(0, 0, logicalWidth, logicalHeight));
        RootGrid.UpdateLayout();
        if (RootGrid.ActualWidth <= 0 || RootGrid.ActualHeight <= 0)
            throw new InvalidOperationException("The live WPF RootGrid has no renderable bounds.");

        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(RootGrid.Background ?? Brushes.Black, null, new Rect(0, 0, physicalWidth, physicalHeight));
            drawing.PushTransform(new ScaleTransform(scale, scale));
            drawing.DrawRectangle(new VisualBrush(RootGrid), null, new Rect(0, 0, RootGrid.ActualWidth, RootGrid.ActualHeight));
            drawing.Pop();
        }
        var bitmap = new RenderTargetBitmap(physicalWidth, physicalHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        controller.WriteArtifact(controller.ScenarioId, "screenshot", $"{label}.png", stream.ToArray());

        var state = driver.CaptureState();
        var bounds = driver.CaptureVisibleBounds();
        if (AssetLibraryP3AutomatedAcceptanceDriver.HasLayoutOverflow(bounds, state.PageWidth, state.PageHeight))
        {
            var offenders = bounds
                .Where(item => item.MustFit &&
                    (item.Clipped || item.Overlapped || item.X < -0.01 || item.Y < -0.01 ||
                     item.X + item.Width > state.PageWidth + 0.01 || item.Y + item.Height > state.PageHeight + 0.01))
                .Select(item => $"{item.Identity}[{item.X:0.##},{item.Y:0.##},{item.Width:0.##},{item.Height:0.##};visible={item.VisibleX:0.##},{item.VisibleY:0.##},{item.VisibleWidth:0.##},{item.VisibleHeight:0.##};clipped={item.Clipped};overlapped={item.Overlapped}]")
                .ToArray();
            throw new InvalidOperationException($"The live P3 layout overflowed at {physicalWidth}x{physicalHeight}/{scale:P0} (page={state.PageWidth:0.##}x{state.PageHeight:0.##}; offenders={string.Join(", ", offenders)}).");
        }
        var buttonReadability = driver.CaptureButtonReadabilityMatrix("dark");
        var realizedButtons = driver.CaptureRealizedButtons();
        var readability = EvaluateButtonReadability(buttonReadability, realizedButtons);
        if (!readability.Passed)
            throw new InvalidOperationException($"The measured P3 button readability contract failed: {string.Join("; ", readability.Failures)}");
        controller.WriteJsonArtifact(controller.ScenarioId, "bounds", $"{label}.bounds.json", Evidence(controller, new
        {
            viewport = new
            {
                width = state.PageWidth,
                height = state.PageHeight,
                physical_width = physicalWidth,
                physical_height = physicalHeight,
                scale_percent = (int)Math.Round(scale * 100),
            },
            real_display_settings_changed = false,
            has_overflow = false,
            contrast_passed = readability.Passed,
            contrast_failures = readability.Failures,
            button_readability = buttonReadability,
            realized_buttons = realizedButtons,
            elements = bounds,
        }));
    }

    private static IReadOnlyList<(string Variant, AssetQueryDocument Document)> FolderCompositionDocuments() =>
    [
        ("any", Document(AssetQueryScope.AllAssets, Rule(AssetQueryField.Folder, AssetQueryOperator.AnyOf, "name:人像", "name:时尚"))),
        ("all", Document(AssetQueryScope.AllAssets, Rule(AssetQueryField.Folder, AssetQueryOperator.AllOf, "name:人像", "name:项目"))),
        ("not", Document(AssetQueryScope.AllAssets, Rule(AssetQueryField.Folder, AssetQueryOperator.NoneOf, "name:冷光"))),
    ];

    private static IReadOnlyList<(string Variant, AssetQueryDocument Document)> TagCompositionDocuments() =>
    [
        ("any", Document(AssetQueryScope.AllAssets, Rule(AssetQueryField.Tag, AssetQueryOperator.AnyOf, "name:人像", "name:时尚"))),
        ("all", Document(AssetQueryScope.AllAssets, Rule(AssetQueryField.Tag, AssetQueryOperator.AllOf, "name:人像", "name:精选"))),
        ("not", Document(AssetQueryScope.AllAssets, Rule(AssetQueryField.Tag, AssetQueryOperator.NoneOf, "name:失败"))),
    ];

    private static IReadOnlyList<(string Variant, AssetQueryDocument Document)> ScalarCompositionDocuments() =>
    [
        ("value", Document(AssetQueryScope.AllAssets,
            Rule(AssetQueryField.Rating, AssetQueryOperator.Between, "2", "5"),
            Rule(AssetQueryField.AddedAt, AssetQueryOperator.GreaterThanOrEqual, "2026-09-01T00:00:00Z"),
            Rule(AssetQueryField.CaptureTime, AssetQueryOperator.Known),
            Rule(AssetQueryField.FileSize, AssetQueryOperator.Between, "4096", "500000"),
            Rule(AssetQueryField.Width, AssetQueryOperator.GreaterThanOrEqual, "640"),
            Rule(AssetQueryField.Height, AssetQueryOperator.GreaterThanOrEqual, "480"),
            Rule(AssetQueryField.AspectRatio, AssetQueryOperator.Between, "0.5", "3"),
            Rule(AssetQueryField.Orientation, AssetQueryOperator.AnyOf, "Landscape", "Portrait", "Square"),
            Rule(AssetQueryField.Extension, AssetQueryOperator.AnyOf, ".cr3", ".jpg", ".png"),
            Rule(AssetQueryField.MediaType, AssetQueryOperator.AnyOf, "Image", "Raw"))),
        ("null", Document(AssetQueryScope.AllAssets,
            Rule(AssetQueryField.CaptureTime, AssetQueryOperator.Unknown))),
        ("not-null", Document(AssetQueryScope.AllAssets,
            Rule(AssetQueryField.Width, AssetQueryOperator.Known))),
        ("comment-empty", Document(AssetQueryScope.AllAssets,
            Rule(AssetQueryField.Comment, AssetQueryOperator.IsEmpty))),
        ("missing-and-untagged", Document(AssetQueryScope.AllAssets,
            Rule(AssetQueryField.IsMissing, AssetQueryOperator.IsTrue),
            Rule(AssetQueryField.IsUntagged, AssetQueryOperator.IsTrue))),
    ];

    private static IReadOnlyList<(string Variant, AssetQueryDocument Document)> VisualCompositionDocuments() =>
    [
        ("valid", Document(AssetQueryScope.AllAssets, Rule(AssetQueryField.VisualAnalysisStatus, AssetQueryOperator.Equals, "Valid"))),
        ("failed", Document(AssetQueryScope.AllAssets, Rule(AssetQueryField.VisualAnalysisStatus, AssetQueryOperator.Equals, "Failed"))),
        ("not-analyzed", Document(AssetQueryScope.AllAssets, Rule(AssetQueryField.VisualAnalysisStatus, AssetQueryOperator.Equals, "NotAnalyzed"))),
        ("classifications", Document(AssetQueryScope.AllAssets,
            Rule(AssetQueryField.VisualHarmony, AssetQueryOperator.Equals, "Complementary"),
            Rule(AssetQueryField.VisualToneKey, AssetQueryOperator.Equals, "Low"),
            Rule(AssetQueryField.VisualContrast, AssetQueryOperator.Equals, "Low"),
            Rule(AssetQueryField.VisualSaturation, AssetQueryOperator.Equals, "Low"),
            Rule(AssetQueryField.VisualWarmCool, AssetQueryOperator.Equals, "Cool"))),
        ("histogram-summary", Document(AssetQueryScope.AllAssets,
            Rule(AssetQueryField.VisualDominantHue, AssetQueryOperator.Between, "0", "45"),
            Rule(AssetQueryField.VisualAverageLuma, AssetQueryOperator.Between, "64", "128"),
            Rule(AssetQueryField.VisualAverageSaturation, AssetQueryOperator.Between, "0", "1"),
            Rule(AssetQueryField.VisualLumaSpread, AssetQueryOperator.Between, "0", "1"),
            Rule(AssetQueryField.VisualShadowRatio, AssetQueryOperator.Between, "0", "1"),
            Rule(AssetQueryField.VisualHighlightRatio, AssetQueryOperator.Between, "0", "1"))),
        ("palette-color", Document(AssetQueryScope.AllAssets,
            Rule(AssetQueryField.VisualDominantColor, AssetQueryOperator.Equals, "#000000"))),
    ];

    private static AssetQueryDocument NestedEightRuleDocument() => Document(AssetQueryScope.AllAssets,
        AssetQueryNode.Group(AssetQueryLogic.All,
        [
            Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, "1"),
            Rule(AssetQueryField.Rating, AssetQueryOperator.LessThanOrEqual, "5"),
            AssetQueryNode.Group(AssetQueryLogic.Any,
            [
                Rule(AssetQueryField.Extension, AssetQueryOperator.Equals, ".jpg"),
                Rule(AssetQueryField.Extension, AssetQueryOperator.Equals, ".png"),
                AssetQueryNode.Group(AssetQueryLogic.Any,
                [
                    Rule(AssetQueryField.MediaType, AssetQueryOperator.Equals, "Image"),
                    Rule(AssetQueryField.FileName, AssetQueryOperator.Contains, "人物素材"),
                    Rule(AssetQueryField.IsMissing, AssetQueryOperator.IsFalse),
                    Rule(AssetQueryField.CaptureTime, AssetQueryOperator.Known),
                ])
            ])
        ]));

    private static AssetQueryDocument Document(AssetQueryScope scope, params AssetQueryNode[] nodes) => new()
    {
        Scope = scope,
        RootGroup = AssetQueryNode.Group(AssetQueryLogic.All, nodes),
        SortField = AssetLibrarySortField.FileName,
        SortDirection = AssetLibrarySortDirection.Ascending,
        IncludeArchived = false,
    };

    private static AssetQueryNode Rule(AssetQueryField field, AssetQueryOperator operation, params string[] values) =>
        AssetQueryNode.Rule(field, operation, values);

    private static string Sha256Text(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static async Task WaitForAssetLibraryStateAsync(
        AssetLibraryP3AutomatedAcceptanceDriver driver,
        Func<AssetLibraryP3AutomatedState, bool> condition,
        string description)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await driver.DrainDispatcherAsync();
            if (condition(driver.CaptureState())) return;
            await Task.Delay(40);
        }
        throw new TimeoutException($"Timed out waiting for {description}.");
    }
}
#endif
