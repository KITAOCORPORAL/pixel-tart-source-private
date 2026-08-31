#if ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.ViewModels;
using AssetLibraryPageResult = RAWSelectionAssistant.Core.Models.AssetLibraryPage;

namespace RAWSelectionAssistant.Services;

internal sealed class AssetLibraryP1AutomatedAcceptanceController : IAssetLibraryLoadStateController
{
    internal const string OptInEnvironmentVariable = "PIXEL_TART_P1_AUTOMATED_ACCEPTANCE";
    internal const string RunRootEnvironmentVariable = "PIXEL_TART_P1_AUTOMATED_RUN_ROOT";
    internal const string PlanPathEnvironmentVariable = "PIXEL_TART_P1_AUTOMATED_PLAN_PATH";
    internal const string SourceHeadEnvironmentVariable = "PIXEL_TART_P1_AUTOMATED_SOURCE_HEAD";
    internal const string FixtureRootEnvironmentVariable = "PIXEL_TART_P1_AUTOMATED_FIXTURE_ROOT";
    internal const string PlanSchema = "pixel-tart-p1-automated-plan/v1";
    internal const string EventSchema = "pixel-tart-p1-automated-event/v1";
    internal const string SummarySchema = "pixel-tart-p1-automated-summary/v1";
    internal const string ExpectedProcessName = "PixelTart_ModularHarness_V1_DevPreview";
    internal const string AssetLibraryRoute = "asset-library";
    internal const string FirstEmptyScenario = "first-empty/v1";
    internal const string RetryScenario = "loading-error-retry-recovered/v1";
    internal const string OrganizationSplitterScenario = "organization-splitter/v1";
    internal const string InspectorSplitterScenario = "inspector-splitter/v1";
    internal const string CollapseScenario = "pane-collapse-expand/v1";
    internal const string ThumbnailScenario = "thumbnail-slider/v1";
    internal const string SelectionScenario = "selection-navigation-restart/v1";
    internal const string NavigationImeScenario = "navigation-ime/v1";
    internal const string LayoutDpiButtonsScenario = "layout-dpi-buttons/v1";
    internal const string HistoricalManualGate = "not_closed_superseded_as_release_blocker";
    internal const string SchemaQuery = "SELECT MAX(Version) FROM AssetLibrarySchemaInfo;";

    internal static readonly string[] FixedScenarioIds =
    [
        FirstEmptyScenario,
        RetryScenario,
        OrganizationSplitterScenario,
        InspectorSplitterScenario,
        CollapseScenario,
        ThumbnailScenario,
        SelectionScenario,
        NavigationImeScenario,
        LayoutDpiButtonsScenario,
    ];

    private const string QueryFailureInjectionId = "asset-library-p1-automated-initial-query-io-once/v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly JsonSerializerOptions LineJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly object _gate = new();
    private readonly string _runRoot;
    private readonly string _applicationRoot;
    private readonly string _planPath;
    private readonly string _eventsPath;
    private readonly string _summaryPath;
    private readonly string _summaryJournalPath;
    private readonly string _phaseSummaryPath;
    private readonly string _databasePath;
    private readonly string _sourceHead;
    private readonly string _runId;
    private readonly string _phase;
    private readonly string _scenarioId;
    private readonly string _scenarioRoot;
    private readonly string? _fixtureRoot;
    private readonly string _processSessionId = Guid.NewGuid().ToString("N");
    private readonly string _executablePath;
    private readonly string _executableSha256;
    private readonly string _modulePath;
    private readonly string _moduleSha256;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private readonly TaskCompletionSource<bool> _initialLoadingRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Dictionary<string, ScenarioState> _scenarios = FixedScenarioIds
        .Select((id, index) => new ScenarioState(id, index + 1))
        .ToDictionary(state => state.Id, StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement> _previousScenarios = new(StringComparer.Ordinal);
    private readonly List<ArtifactState> _artifacts = [];
    private readonly List<JsonElement> _previousArtifacts = [];
    private string _previousEventHash = new('0', 64);
    private string _previousSummaryHash = new('0', 64);
    private long _sequence;
    private int _queryFailureInjected;
    private int _loadingReleased;
    private int _boundToWindow;
    private int _executionCompleted;
    private int _summaryWritten;
    private int? _primaryPid;
    private int? _restartPid;
    private nint _hwnd;
    private string _currentRoute = string.Empty;
    private string _activeScenarioId;
    private string? _failure;

    private AssetLibraryP1AutomatedAcceptanceController(
        string applicationRoot,
        AutomatedPlan plan,
        string runRoot,
        string planPath,
        string sourceHead,
        string? fixtureRoot)
    {
        _applicationRoot = NormalizePath(applicationRoot);
        _runRoot = NormalizePath(runRoot);
        _planPath = NormalizePath(planPath);
        _sourceHead = sourceHead;
        _runId = plan.RunId;
        _phase = plan.Phase;
        _scenarioId = plan.ScenarioIds.Single();
        _scenarioRoot = string.IsNullOrWhiteSpace(plan.ScenarioRoot)
            ? NormalizePath(Path.GetDirectoryName(_applicationRoot)
                ?? throw new InvalidOperationException("The isolated application root has no scenario parent."))
            : NormalizePath(plan.ScenarioRoot);
        _activeScenarioId = _scenarioId;
        _fixtureRoot = fixtureRoot;
        _eventsPath = Path.Combine(_runRoot, "app", "evidence", "events.ndjson");
        _summaryPath = Path.Combine(_runRoot, "app", "evidence", "summary.json");
        _summaryJournalPath = Path.Combine(_runRoot, "app", "evidence", "summary.ndjson");
        _phaseSummaryPath = Path.Combine(
            _runRoot,
            "app",
            "evidence",
            $"summary-{SanitizeFileName(_scenarioId.Replace('/', '-'))}-{_phase}.json");
        _databasePath = Path.Combine(_applicationRoot, "Data", "asset-library-v16.db");
        _executablePath = NormalizePath(Environment.ProcessPath
            ?? throw new InvalidOperationException("The automated acceptance process path is unavailable."));
        _modulePath = NormalizePath(typeof(AssetLibraryPage).Assembly.Location);
        _executableSha256 = HashFile(_executablePath);
        _moduleSha256 = HashFile(_modulePath);

        Directory.CreateDirectory(Path.GetDirectoryName(_eventsPath)!);
        EnsurePathInside(_applicationRoot, _scenarioRoot, "application root");
        EnsurePathInside(_scenarioRoot, _runRoot, "scenario root");
        if (!string.IsNullOrWhiteSpace(_fixtureRoot))
            EnsurePathInside(_fixtureRoot, _runRoot, "fixture root");
        Directory.CreateDirectory(_scenarioRoot);
        LoadAggregateEvidence();
        if (IsRestartPhase) ValidateRestartEvidence();
        else EnsureFreshScenarioRuntime();
    }

    internal bool IsRestartPhase => string.Equals(_phase, "restart", StringComparison.Ordinal);
    internal string Phase => _phase;
    internal string ScenarioId => _scenarioId;
    internal string ScenarioRoot => _scenarioRoot;
    internal string? FixtureRoot => _fixtureRoot;
    internal string RunRoot => _runRoot;
    internal string RunId => _runId;
    internal string SourceHead => _sourceHead;
    internal string ProcessSessionId => _processSessionId;
    internal string ExecutablePath => _executablePath;
    internal string ExecutableSha256 => _executableSha256;
    internal string AssetModulePath => _modulePath;
    internal string AssetModuleSha256 => _moduleSha256;
    internal string Hwnd => FormatHwnd(_hwnd.ToInt64());
    internal string DatabasePath => _databasePath;
    public bool DisablePreviewFixtures => true;

    internal static AssetLibraryP1AutomatedAcceptanceController? TryCreate(string applicationRoot, ILogService? logService)
    {
        var optIn = Environment.GetEnvironmentVariable(OptInEnvironmentVariable);
        var runRoot = Environment.GetEnvironmentVariable(RunRootEnvironmentVariable);
        var planPath = Environment.GetEnvironmentVariable(PlanPathEnvironmentVariable);
        var sourceHead = Environment.GetEnvironmentVariable(SourceHeadEnvironmentVariable);
        var fixtureRootFromEnvironment = Environment.GetEnvironmentVariable(FixtureRootEnvironmentVariable);
        if (optIn is null && runRoot is null && planPath is null && sourceHead is null && fixtureRootFromEnvironment is null)
            return null;

        try
        {
            ValidateRuntime(applicationRoot, optIn, runRoot, planPath, sourceHead);
            var plan = ReadPlan(planPath!);
            if (!string.Equals(plan.SourceHead, sourceHead, StringComparison.Ordinal))
                throw new InvalidOperationException("The automated plan source HEAD does not match the explicit runtime source HEAD.");
            var fixtureRoot = string.IsNullOrWhiteSpace(fixtureRootFromEnvironment) ? plan.FixtureRoot : fixtureRootFromEnvironment;
            if (!string.IsNullOrWhiteSpace(fixtureRoot))
            {
                fixtureRoot = NormalizePath(fixtureRoot);
                EnsurePathInside(fixtureRoot, runRoot!, "fixture root");
                if (!Directory.Exists(fixtureRoot))
                    throw new DirectoryNotFoundException("The automated acceptance fixture root does not exist.");
            }

            logService?.Info("P1 Asset Library automated in-process WPF acceptance enabled in the isolated Dev Preview runtime.");
            return new(applicationRoot, plan, runRoot!, planPath!, sourceHead!, fixtureRoot);
        }
        catch (Exception exception)
        {
            logService?.Error("P1 Asset Library automated acceptance opt-in was rejected.", exception);
            throw;
        }
    }

    internal void ApplyStartRoute(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (!viewModel.NavigateCommand.CanExecute(AssetLibraryRoute))
            throw new InvalidOperationException("The real Asset Library navigation route is unavailable.");
        viewModel.NavigateCommand.Execute(AssetLibraryRoute);
        if (!string.Equals(viewModel.CurrentPage, "AssetLibrary", StringComparison.Ordinal))
            throw new InvalidOperationException("The real Asset Library navigation route did not become current.");
        _currentRoute = viewModel.CurrentPage;
    }

    internal void BindWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (Interlocked.CompareExchange(ref _boundToWindow, 1, 0) != 0)
            throw new InvalidOperationException("The automated acceptance controller may bind only one live window.");
        void AttachHandle()
        {
            _hwnd = new WindowInteropHelper(window).Handle;
            if (_hwnd == 0) throw new InvalidOperationException("The automated acceptance live window has no HWND.");
            RecordEvent(
                _scenarioId,
                "process-session-started",
                "MainWindow",
                before: null,
                after: new { process_session_id = _processSessionId, process_id = Environment.ProcessId, hwnd = _hwnd.ToInt64() },
                persisted: null);
        }

        if (new WindowInteropHelper(window).Handle != 0) AttachHandle();
        else window.SourceInitialized += (_, _) => AttachHandle();
    }

    internal void Observe(MainViewModel viewModel, AssetLibraryP1AutomatedAcceptanceDriver driver)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(driver);
        _currentRoute = viewModel.CurrentPage;
        viewModel.PageChanged += (_, args) =>
        {
            var before = args.PreviousPage;
            _currentRoute = args.CurrentPage;
            RecordEvent(
                _activeScenarioId,
                "navigation-route-observed",
                "PrimaryNavigation",
                before,
                args.CurrentPage,
                args.CurrentPage);
        };
        driver.Observed += (_, observation) =>
        {
            RecordEvent(
                _activeScenarioId,
                observation.Kind,
                observation.SourceAutomationId,
                before: null,
                after: new { observation.Name, observation.Value, observation.ObservedAt },
                persisted: null);
        };
        RecordEvent(
            _scenarioId,
            "live-asset-library-observer-attached",
            "AssetLibraryPage",
            before: null,
            after: new { route = viewModel.CurrentPage },
            persisted: null);
    }

    internal void SetActiveScenario(string scenarioId)
    {
        EnsureScenario(scenarioId);
        _activeScenarioId = scenarioId;
    }

    internal void ReleaseInitialLoadingBarrier()
    {
        if (Interlocked.CompareExchange(ref _loadingReleased, 1, 0) == 0)
        {
            RecordEvent(
                _scenarioId,
                "initial-loading-barrier-released",
                "AssetLibraryPage",
                before: new { released = false },
                after: new { released = true },
                persisted: null);
            _initialLoadingRelease.TrySetResult(true);
        }
    }

    internal void RecordAction(
        string scenarioId,
        string eventType,
        string automationId,
        object? before,
        object? after,
        object? persisted = null)
    {
        EnsureScenario(scenarioId);
        RecordEvent(scenarioId, eventType, automationId, before, after, persisted);
    }

    internal void RecordScenarioCheck(string scenarioId, string check, object? value)
    {
        EnsureScenario(scenarioId).Checks[check] = value;
    }

    internal string RequireStringScenarioCheck(string scenarioId, string check)
    {
        if (!EnsureScenario(scenarioId).Checks.TryGetValue(check, out var value))
            throw new InvalidOperationException($"Required persisted check '{check}' is missing for '{scenarioId}'.");
        return value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text,
            JsonElement element when element.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(element.GetString()) => element.GetString()!,
            _ => throw new InvalidOperationException($"Persisted check '{check}' is not a non-empty string."),
        };
    }

    internal double RequireDoubleScenarioCheck(string scenarioId, string check)
    {
        if (!EnsureScenario(scenarioId).Checks.TryGetValue(check, out var value))
            throw new InvalidOperationException($"Required persisted check '{check}' is missing for '{scenarioId}'.");
        return value switch
        {
            double number when double.IsFinite(number) => number,
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var number) && double.IsFinite(number) => number,
            _ => throw new InvalidOperationException($"Persisted check '{check}' is not a finite number."),
        };
    }

    internal void MarkScenarioCompleted(string scenarioId)
    {
        var scenario = EnsureScenario(scenarioId);
        if (IsRestartPhase)
        {
            scenario.RestartPid = Environment.ProcessId;
            scenario.RestartHwnd = _hwnd.ToInt64();
            scenario.RestartProcessSessionId = _processSessionId;
        }
        else
        {
            scenario.Pid = Environment.ProcessId;
            scenario.Hwnd = _hwnd.ToInt64();
            scenario.PrimaryProcessSessionId = _processSessionId;
        }
        scenario.Status = "passed";
        RecordEvent(scenarioId, "scenario-completed", "AssetLibraryPage", null, new { status = "passed" }, null);
    }

    internal void IncrementRetryCommandCount()
    {
        if (!string.Equals(_scenarioId, RetryScenario, StringComparison.Ordinal))
            throw new InvalidOperationException("Retry command evidence may be recorded only by the retry scenario.");
        var scenario = EnsureScenario(_scenarioId);
        scenario.RetryCommandCount++;
    }

    internal void RecordImport(string mechanism, int importedCount, string fixtureRoot)
    {
        if (!string.Equals(_scenarioId, SelectionScenario, StringComparison.Ordinal) || IsRestartPhase)
            throw new InvalidOperationException("Only the selection primary scenario may import the isolated synthetic fixture.");
        var import = new
        {
            mechanism,
            source_kind = "synthetic-run-fixture",
            synthetic = true,
            application_import_route = true,
            user_source = false,
            source_path = fixtureRoot,
            imported_count = importedCount,
            fixture_root = fixtureRoot,
            customer_media = false,
            eagle_library = false,
            recorded_at = DateTimeOffset.UtcNow,
        };
        var scenario = EnsureScenario(_scenarioId);
        scenario.ImportEvents.Add(import);
        scenario.Database.AssetCount = importedCount;
        RecordEvent(_scenarioId, "synthetic-fixture-imported-through-public-application-seam", "AssetLibraryPage", null, import, null);
    }

    internal ArtifactState WriteArtifact(string scenarioId, string kind, string fileName, ReadOnlySpan<byte> content)
    {
        var scenario = EnsureScenario(scenarioId);
        var directoryName = kind.Contains("bounds", StringComparison.OrdinalIgnoreCase) ? "bounds" : "screenshots";
        var safeName = SanitizeFileName(fileName);
        var scenarioToken = SanitizeFileName(scenarioId.Replace('/', '-'));
        var absolutePath = Path.Combine(_runRoot, "app", "evidence", directoryName, scenarioToken, _phase, safeName);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        using (var stream = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            stream.Write(content);
        var artifact = new ArtifactState(
            RelativeToRunRoot(absolutePath),
            HashFile(absolutePath),
            kind,
            scenarioId,
            Environment.ProcessId,
            _hwnd.ToInt64(),
            _scenarioRoot,
            _phase,
            _processSessionId,
            _runId,
            _sourceHead,
            _executableSha256,
            _moduleSha256);
        lock (_gate) _artifacts.Add(artifact);
        if (directoryName == "bounds") scenario.BoundsPaths.Add(artifact.Path);
        else scenario.ScreenshotPaths.Add(artifact.Path);
        RecordEvent(scenarioId, "artifact-captured", "AssetLibraryPage", null, artifact, null);
        return artifact;
    }

    internal ArtifactState WriteJsonArtifact(string scenarioId, string kind, string fileName, object value)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions));
        return WriteArtifact(scenarioId, kind, fileName, bytes);
    }

    internal void MarkExecutionCompleted()
    {
        Interlocked.Exchange(ref _executionCompleted, 1);
        RecordEvent(_activeScenarioId, "automated-plan-execution-completed", "MainWindow", null, new { phase = _phase }, null);
    }

    internal void Fail(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _failure = $"{exception.GetType().FullName}: {exception.Message}";
        ReleaseInitialLoadingBarrier();
        try
        {
            RecordEvent(_activeScenarioId, "automated-plan-execution-failed", "MainWindow", null, new { failure = _failure }, null);
        }
        catch
        {
            // The original failure remains authoritative if the evidence sink itself is unavailable.
        }
    }

    internal void FinalizeOnApplicationExit(int exitCode)
    {
        if (Interlocked.CompareExchange(ref _summaryWritten, 1, 0) != 0) return;
        ReleaseInitialLoadingBarrier();
        var completed = Volatile.Read(ref _executionCompleted) == 1 && _failure is null && exitCode == 0;
        if (!completed && _failure is null)
            _failure = $"Application exited before the automated plan completed (exit code {exitCode}).";

        try
        {
            UpdateFinalDatabaseEvidenceAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            completed = false;
            _failure = $"{exception.GetType().FullName}: {exception.Message}";
            try
            {
                RecordEvent(_activeScenarioId, "database-evidence-finalization-failed", "SqliteAssetLibraryRepository", null, new { failure = _failure }, null);
            }
            catch
            {
                // Preserve the finalization failure when the append-only event sink is also unavailable.
            }
        }
        RecordEvent(
            _activeScenarioId,
            "process-session-ending",
            "MainWindow",
            null,
            new { exit_code = exitCode, plan_completed = completed },
            null);
        WriteSummary(completed ? "completed" : "failed", exitCode);
    }

    public async Task BeforeRepositoryInitializationAsync(int attempt, CancellationToken cancellationToken)
    {
        if (attempt != 1 || IsRestartPhase || _scenarioId is not (FirstEmptyScenario or RetryScenario)) return;
        RecordEvent(_scenarioId, "initial-loading-barrier-entered", "AssetLibraryPage", null, new { attempt }, null);
        await _initialLoadingRelease.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssetLibraryPageResult> ExecuteInitialQueryAsync(
        int attempt,
        Func<CancellationToken, Task<AssetLibraryPageResult>> realQuery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(realQuery);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsRestartPhase &&
            string.Equals(_scenarioId, RetryScenario, StringComparison.Ordinal) &&
            attempt == 1 &&
            Interlocked.CompareExchange(ref _queryFailureInjected, 1, 0) == 0)
        {
            var exception = new IOException("Deterministic P1 automated recoverable initial-query failure.");
            exception.Data[AssetLibraryLoadStateExceptionMetadata.InjectionIdDataKey] = QueryFailureInjectionId;
            RecordEvent(
                _scenarioId,
                "recoverable-query-error-injected",
                "AssetLibraryRepositoryQuery",
                null,
                new { attempt, exception_type = exception.GetType().FullName, injection_id = QueryFailureInjectionId },
                null);
            throw exception;
        }

        RecordEvent(_scenarioId, "real-repository-query-entered", "AssetLibraryRepositoryQuery", null, new { attempt }, null);
        var page = await realQuery(cancellationToken).ConfigureAwait(false);
        RecordEvent(_scenarioId, "real-repository-query-completed", "AssetLibraryRepositoryQuery", null, new { attempt, asset_count = page.TotalCount }, null);
        return page;
    }

    public void RecordState(AssetLibraryLoadStateSnapshot snapshot)
    {
        if (!string.Equals(NormalizePath(snapshot.DatabasePath), NormalizePath(_databasePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The automated load-state snapshot references a database outside the isolated application root.");
        var scenarioId = _scenarioId;
        UpdateDatabaseState(EnsureScenario(scenarioId), snapshot);
        RecordEvent(
            scenarioId,
            "load-state-observed",
            "AssetLibraryPage",
            null,
            snapshot,
            new
            {
                database_path = _databasePath,
                snapshot.RepositoryImplementation,
                snapshot.RepositorySchemaVersion,
                snapshot.RepositoryAssetCount,
            });
    }

    private void RecordEvent(
        string scenarioId,
        string eventType,
        string automationId,
        object? before,
        object? after,
        object? persisted)
    {
        var scenario = EnsureScenario(scenarioId);
        lock (_gate)
        {
            var sequence = ++_sequence;
            var identity = new
            {
                run_id = _runId,
                phase = _phase,
                process_session_id = _processSessionId,
                pid = Environment.ProcessId,
                hwnd = _hwnd.ToInt64(),
                source_head = _sourceHead,
            };
            var envelope = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["schema"] = EventSchema,
                ["validation_mode"] = "automated",
                ["owner_manual_ux_smoke"] = "waived",
                ["manual_evidence_claimed"] = false,
                ["historical_manual_gate"] = HistoricalManualGate,
                ["automated_capture_status"] = "captured",
                ["identity"] = identity,
                ["run_id"] = _runId,
                ["phase"] = _phase,
                ["process_session_id"] = _processSessionId,
                ["pid"] = Environment.ProcessId,
                ["hwnd"] = FormatHwnd(_hwnd.ToInt64()),
                ["hwnd_numeric"] = _hwnd.ToInt64(),
                ["source_head"] = _sourceHead,
                ["head"] = _sourceHead,
                ["scenario_root"] = _scenarioRoot,
                ["executable_path"] = _executablePath,
                ["executable_sha256"] = _executableSha256,
                ["module_path"] = _modulePath,
                ["module_sha256"] = _moduleSha256,
                ["asset_module_path"] = _modulePath,
                ["asset_module_sha256"] = _moduleSha256,
                ["sequence"] = sequence,
                ["event_sequence"] = sequence,
                ["timestamp_utc"] = DateTimeOffset.UtcNow,
                ["scenario_id"] = scenarioId,
                ["event_type"] = eventType,
                ["automation_id"] = automationId,
                ["route"] = _currentRoute,
                ["before"] = before,
                ["after"] = after,
                ["persisted"] = persisted,
                ["before_state"] = before,
                ["after_state"] = after,
                ["persisted_state"] = persisted,
                ["activation_mode"] = "in-process-wpf-route",
                ["direct_mutation"] = false,
                ["previous_event_hash"] = _previousEventHash,
                ["previous_record_sha256"] = _previousEventHash,
            };
            var canonical = JsonSerializer.Serialize(envelope, LineJsonOptions);
            var eventHash = HashBytes(Encoding.UTF8.GetBytes(canonical));
            envelope["event_hash"] = eventHash;
            envelope["record_sha256"] = eventHash;
            var line = JsonSerializer.Serialize(envelope, LineJsonOptions);
            File.AppendAllText(_eventsPath, line + Environment.NewLine, new UTF8Encoding(false));
            _previousEventHash = eventHash;
            if (IsRestartPhase)
            {
                scenario.RestartPid = Environment.ProcessId;
                scenario.RestartHwnd = _hwnd.ToInt64();
                scenario.RestartProcessSessionId = _processSessionId;
            }
            else
            {
                scenario.Pid = Environment.ProcessId;
                scenario.Hwnd = _hwnd.ToInt64();
                scenario.PrimaryProcessSessionId = _processSessionId;
            }
        }
    }

    private void WriteSummary(string status, int exitCode)
    {
        if (string.Equals(_scenarioId, SelectionScenario, StringComparison.Ordinal))
        {
            if (string.Equals(_phase, "primary", StringComparison.Ordinal)) _primaryPid = Environment.ProcessId;
            else _restartPid = Environment.ProcessId;
        }

        var scenarioPayload = FixedScenarioIds.Select(id =>
        {
            var current = _scenarios[id];
            return current.Status == "not-run" && _previousScenarios.TryGetValue(id, out var previous)
                ? (object)previous
                : current.ToContract(
                    id == _scenarioId ? _scenarioRoot : GetScenarioRoot(id));
        }).ToArray();
        var artifactPayload = _previousArtifacts.Cast<object>().Concat(_artifacts.Cast<object>()).ToArray();
        var summary = new
        {
            schema = SummarySchema,
            schema_version = SummarySchema,
            validation_mode = "automated",
            owner_manual_ux_smoke = "waived",
            manual_evidence_claimed = false,
            historical_manual_gate = HistoricalManualGate,
            automated_capture_status = "captured",
            status,
            failure = _failure,
            run_id = _runId,
            head = _sourceHead,
            source_head = _sourceHead,
            phase = _phase,
            started_at = _startedAt,
            completed_at = DateTimeOffset.UtcNow,
            primary_pid = _primaryPid,
            restart_pid = _restartPid,
            process_session_id = _processSessionId,
            executable = new { path = _executablePath, sha256 = _executableSha256 },
            module = new { path = _modulePath, sha256 = _moduleSha256 },
            executable_path = _executablePath,
            executable_sha256 = _executableSha256,
            asset_module_path = _modulePath,
            asset_module_sha256 = _moduleSha256,
            run_root = _runRoot,
            application_root = _applicationRoot,
            plan_path = _planPath,
            scenario_ids = FixedScenarioIds,
            scenarios = scenarioPayload,
            artifacts = artifactPayload,
            event_journal = new
            {
                path = RelativeToRunRoot(_eventsPath),
                event_count = _sequence,
                last_event_hash = _previousEventHash,
                append_only = true,
            },
            safety = new
            {
                desktop_input_injection_count = 0,
                real_display_setting_write_count = 0,
                eagle_write_count = 0,
                user_source_read_count = 0,
                user_source_write_count = 0,
                direct_width_mutation_count = 0,
                direct_settings_mutation_count = 0,
                direct_sqlite_row_edit_count = 0,
                send_input_count = 0,
                mouse_event_count = 0,
                keybd_event_count = 0,
                display_api_count = 0,
                uia_invoke_count = 0,
                private_reflection_count = 0,
                direct_settings_or_width_mutation_count = 0,
                direct_database_mutation_count = 0,
            },
            process_cleanup = new
            {
                exit_code = exitCode,
                shutdown_requested = true,
                application_exit_hook_reached = true,
                residual_process_count = (int?)null,
                residual_process_check_owner = "independent-runner-after-process-exit",
                database_wal_present = File.Exists(_databasePath + "-wal"),
                database_shm_present = File.Exists(_databasePath + "-shm"),
            },
        };
        var summaryElement = JsonSerializer.SerializeToElement(summary, LineJsonOptions);
        var journal = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema"] = SummarySchema,
            ["schema_version"] = SummarySchema,
            ["validation_mode"] = "automated",
            ["owner_manual_ux_smoke"] = "waived",
            ["manual_evidence_claimed"] = false,
            ["historical_manual_gate"] = HistoricalManualGate,
            ["automated_capture_status"] = "captured",
            ["run_id"] = _runId,
            ["source_head"] = _sourceHead,
            ["scenario_id"] = _scenarioId,
            ["scenario_root"] = _scenarioRoot,
            ["phase"] = _phase,
            ["process_session_id"] = _processSessionId,
            ["pid"] = Environment.ProcessId,
            ["hwnd"] = FormatHwnd(_hwnd.ToInt64()),
            ["summary"] = summaryElement,
            ["previous_summary_hash"] = _previousSummaryHash,
            ["previous_record_sha256"] = _previousSummaryHash,
        };
        var summaryCanonical = JsonSerializer.Serialize(journal, LineJsonOptions);
        var summaryHash = HashBytes(Encoding.UTF8.GetBytes(summaryCanonical));
        journal["summary_hash"] = summaryHash;
        journal["record_sha256"] = summaryHash;
        File.AppendAllText(
            _summaryJournalPath,
            JsonSerializer.Serialize(journal, LineJsonOptions) + Environment.NewLine,
            new UTF8Encoding(false));
        _previousSummaryHash = summaryHash;
        WriteJsonAtomically(_phaseSummaryPath, summary, overwrite: false);
        WriteJsonAtomically(_summaryPath, summary, overwrite: true);
    }

    private async Task UpdateFinalDatabaseEvidenceAsync()
    {
        var state = EnsureScenario(_scenarioId);
        state.Database.Path = _databasePath;
        state.Database.ActivePath = RelativeToRunRoot(_databasePath);
        if (!File.Exists(_databasePath))
            throw new InvalidOperationException("The real Asset Library SQLite database is missing during evidence finalization.");

        var scenarioToken = SanitizeFileName(_scenarioId.Replace('/', '-'));
        var snapshotPath = Path.Combine(
            _runRoot,
            "app",
            "evidence",
            "databases",
            $"{scenarioToken}-{_phase}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
        if (File.Exists(snapshotPath))
            throw new InvalidOperationException("The immutable database evidence path already exists.");

        var stagingPath = snapshotPath + $".{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            SqliteConnection.ClearAllPools();
            var sourceBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Cache = SqliteCacheMode.Private,
                Pooling = false,
                DefaultTimeout = 5,
            };
            var targetBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = stagingPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
                Pooling = false,
                DefaultTimeout = 5,
            };

            await using (var source = new SqliteConnection(sourceBuilder.ToString()))
            {
                await source.OpenAsync().ConfigureAwait(false);
                await CheckpointWalAsync(source).ConfigureAwait(false);

                await using var target = new SqliteConnection(targetBuilder.ToString());
                await target.OpenAsync().ConfigureAwait(false);
                source.BackupDatabase(target);
                await ValidateDatabaseEvidenceAsync(target, state).ConfigureAwait(false);
            }

            SqliteConnection.ClearAllPools();
            state.Database.WalPresentAfterClose = File.Exists(_databasePath + "-wal");
            state.Database.ShmPresentAfterClose = File.Exists(_databasePath + "-shm");
            if (state.Database.WalPresentAfterClose || state.Database.ShmPresentAfterClose)
                throw new InvalidOperationException("The real Asset Library SQLite WAL/SHM files remained after explicit teardown and checkpoint.");
            if (File.Exists(stagingPath + "-wal") || File.Exists(stagingPath + "-shm"))
                throw new InvalidOperationException("The SQLite evidence backup retained WAL/SHM sidecars.");

            File.Move(stagingPath, snapshotPath, overwrite: false);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(stagingPath)) File.Delete(stagingPath);
            if (File.Exists(stagingPath + "-wal")) File.Delete(stagingPath + "-wal");
            if (File.Exists(stagingPath + "-shm")) File.Delete(stagingPath + "-shm");
        }

        state.Database.EvidencePath = RelativeToRunRoot(snapshotPath);
        state.Database.EvidenceAbsolutePath = snapshotPath;
        state.Database.Sha256 = HashFile(snapshotPath);
        state.Database.EvidencePaths.Add(state.Database.EvidencePath);
        _artifacts.Add(new(
            state.Database.EvidencePath,
            state.Database.Sha256,
            "database",
            _scenarioId,
            Environment.ProcessId,
            _hwnd.ToInt64(),
            _scenarioRoot,
            _phase,
            _processSessionId,
            _runId,
            _sourceHead,
            _executableSha256,
            _moduleSha256));
    }

    private static async Task CheckpointWalAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false))
            throw new InvalidOperationException("The SQLite WAL checkpoint returned no status row.");
        var busy = reader.GetInt32(0);
        var logFrames = reader.GetInt32(1);
        var checkpointedFrames = reader.GetInt32(2);
        if (busy != 0 || logFrames >= 0 && checkpointedFrames < logFrames)
            throw new InvalidOperationException(
                $"The SQLite WAL checkpoint was incomplete (busy={busy}, log={logFrames}, checkpointed={checkpointedFrames}).");
    }

    private static async Task ValidateDatabaseEvidenceAsync(SqliteConnection connection, ScenarioState state)
    {
        await using (var integrity = connection.CreateCommand())
        {
            integrity.CommandText = "PRAGMA quick_check;";
            var result = Convert.ToString(await integrity.ExecuteScalarAsync().ConfigureAwait(false));
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"The SQLite evidence backup failed quick_check: {result ?? "<null>"}.");
        }

        int schemaVersion;
        await using (var schema = connection.CreateCommand())
        {
            schema.CommandText = SchemaQuery;
            schemaVersion = Convert.ToInt32(await schema.ExecuteScalarAsync().ConfigureAwait(false));
        }
        if (schemaVersion != 6)
            throw new InvalidDataException($"The SQLite evidence backup schema is {schemaVersion}; expected v6.");
        if (state.Database.SchemaVersion is int observedSchema && observedSchema != schemaVersion)
            throw new InvalidDataException("The SQLite evidence schema does not match the repository-observed schema.");

        int assetCount;
        await using (var assets = connection.CreateCommand())
        {
            assets.CommandText = "SELECT COUNT(*) FROM AssetItems;";
            assetCount = Convert.ToInt32(await assets.ExecuteScalarAsync().ConfigureAwait(false));
        }
        if (state.Database.AssetCount is int observedAssets && observedAssets != assetCount)
            throw new InvalidDataException("The SQLite evidence asset count does not match the application-observed asset count.");

        state.Database.SchemaVersion = schemaVersion;
        state.Database.AssetCount = assetCount;
    }

    private static void UpdateDatabaseState(ScenarioState state, AssetLibraryLoadStateSnapshot snapshot)
    {
        state.Database.Path = snapshot.DatabasePath;
        state.Database.RepositoryImplementation = snapshot.RepositoryImplementation;
        state.Database.RealRepository = string.Equals(snapshot.RepositoryImplementation, "SqliteAssetLibraryRepository", StringComparison.Ordinal);
        state.Database.SchemaVersion = snapshot.RepositorySchemaVersion;
        state.Database.SchemaQuery = SchemaQuery;
        state.Database.AssetCount = snapshot.RepositoryAssetCount;
        state.Checks[$"load_state_{snapshot.Attempt}_{snapshot.Stage}"] = new
        {
            snapshot.IsLoading,
            snapshot.IsReady,
            snapshot.HasLoadError,
            snapshot.ExceptionType,
            snapshot.InjectionId,
            snapshot.VisibleAssetCount,
        };
    }

    private void EnsureFreshScenarioRuntime()
    {
        if (new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm" }.Any(File.Exists))
            throw new InvalidOperationException("A primary scenario process requires a fresh real SQLite database.");
        if (_previousScenarios.TryGetValue(_scenarioId, out var previous) &&
            previous.TryGetProperty("status", out var status) &&
            string.Equals(status.GetString(), "passed", StringComparison.Ordinal))
            throw new InvalidOperationException("The aggregate run already contains a completed primary result for this scenario.");

        foreach (var prior in _previousScenarios.Values)
        {
            var id = RequireString(prior, "id");
            if (string.Equals(id, _scenarioId, StringComparison.Ordinal)) continue;
            var priorRoot = RequireString(prior, "scenario_root");
            if (string.Equals(NormalizePath(priorRoot), _scenarioRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Two automated scenarios cannot share an isolated scenario root.");
        }
    }

    private void LoadAggregateEvidence()
    {
        if (File.Exists(_eventsPath) && new FileInfo(_eventsPath).Length > 0)
        {
            var lastLine = File.ReadLines(_eventsPath).LastOrDefault(line => !string.IsNullOrWhiteSpace(line))
                ?? throw new InvalidOperationException("The aggregate application event journal is empty.");
            using var lastEvent = JsonDocument.Parse(lastLine);
            _sequence = RequireInt64(lastEvent.RootElement, "sequence");
            _previousEventHash = RequireString(lastEvent.RootElement, "event_hash");
            if (!Regex.IsMatch(_previousEventHash, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
                throw new InvalidOperationException("The aggregate event journal has an invalid terminal hash.");
        }

        if (File.Exists(_summaryJournalPath) && new FileInfo(_summaryJournalPath).Length > 0)
        {
            var lastSummaryLine = File.ReadLines(_summaryJournalPath).LastOrDefault(line => !string.IsNullOrWhiteSpace(line))
                ?? throw new InvalidOperationException("The append-only summary journal is empty.");
            using var lastSummary = JsonDocument.Parse(lastSummaryLine);
            _previousSummaryHash = RequireString(lastSummary.RootElement, "summary_hash");
            if (!Regex.IsMatch(_previousSummaryHash, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
                throw new InvalidOperationException("The append-only summary journal has an invalid terminal hash.");
        }

        if (!File.Exists(_summaryPath)) return;
        using var summary = JsonDocument.Parse(File.ReadAllText(_summaryPath));
        var root = summary.RootElement;
        if (!string.Equals(RequireString(root, "schema"), SummarySchema, StringComparison.Ordinal) ||
            !string.Equals(RequireString(root, "run_id"), _runId, StringComparison.Ordinal) ||
            !string.Equals(RequireString(root, "head"), _sourceHead, StringComparison.Ordinal))
            throw new InvalidOperationException("The aggregate summary does not match this automated run identity.");
        if (root.TryGetProperty("primary_pid", out var primaryPid) &&
            primaryPid.ValueKind == JsonValueKind.Number &&
            primaryPid.TryGetInt32(out var primary))
            _primaryPid = primary;
        if (root.TryGetProperty("restart_pid", out var restartPid) &&
            restartPid.ValueKind == JsonValueKind.Number &&
            restartPid.TryGetInt32(out var restart))
            _restartPid = restart;
        if (root.TryGetProperty("scenarios", out var scenarios) && scenarios.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in scenarios.EnumerateArray())
            {
                var id = RequireString(item, "id");
                if (item.TryGetProperty("status", out var scenarioStatus) &&
                    !string.Equals(scenarioStatus.GetString(), "not-run", StringComparison.Ordinal))
                    _previousScenarios[id] = item.Clone();
            }
        }
        if (root.TryGetProperty("artifacts", out var artifacts) && artifacts.ValueKind == JsonValueKind.Array)
            _previousArtifacts.AddRange(artifacts.EnumerateArray().Select(item => item.Clone()));
    }

    private void ValidateRestartEvidence()
    {
        if (_scenarioId is not (SelectionScenario or CollapseScenario or ThumbnailScenario))
            throw new InvalidOperationException("Only a declared persistence scenario may have a restart phase.");
        if (!File.Exists(_databasePath))
            throw new InvalidOperationException("The persistence restart phase requires the primary real SQLite database.");
        if (!_previousScenarios.TryGetValue(_scenarioId, out var previous) ||
            !previous.TryGetProperty("status", out var status) ||
            !string.Equals(status.GetString(), "passed", StringComparison.Ordinal))
            throw new InvalidOperationException("The persistence restart phase requires its completed primary scenario evidence.");
        var priorScenarioRoot = RequireString(previous, "scenario_root");
        if (!string.Equals(NormalizePath(priorScenarioRoot), _scenarioRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The persistence restart phase must reuse exactly its primary scenario root.");
        EnsureScenario(_scenarioId).LoadPrimary(previous);
    }

    private static void ValidateRuntime(
        string applicationRoot,
        string? optIn,
        string? runRoot,
        string? planPath,
        string? sourceHead)
    {
        if (!string.Equals(optIn, "1", StringComparison.Ordinal))
            throw new InvalidOperationException($"{OptInEnvironmentVariable} must be exactly '1'.");
        if (string.IsNullOrWhiteSpace(runRoot) || !Path.IsPathFullyQualified(runRoot))
            throw new InvalidOperationException($"{RunRootEnvironmentVariable} must be an explicit absolute path.");
        if (string.IsNullOrWhiteSpace(planPath) || !Path.IsPathFullyQualified(planPath) || !File.Exists(planPath))
            throw new InvalidOperationException($"{PlanPathEnvironmentVariable} must name an existing absolute plan file.");
        if (!IsFullHead(sourceHead))
            throw new InvalidOperationException($"{SourceHeadEnvironmentVariable} must be the exact lowercase 40-character source HEAD.");
        if (string.IsNullOrWhiteSpace(applicationRoot) || !Path.IsPathFullyQualified(applicationRoot))
            throw new InvalidOperationException("The automated application root must be explicit and absolute.");
        var explicitApplicationRoot = Environment.GetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_ROOT");
        if (string.IsNullOrWhiteSpace(explicitApplicationRoot) ||
            !string.Equals(NormalizePath(applicationRoot), NormalizePath(explicitApplicationRoot), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The active application root must exactly match PIXEL_TART_ACCEPTANCE_ROOT.");
        EnsurePathInside(applicationRoot, runRoot, "application root");
        EnsurePathInside(planPath, runRoot, "plan path");
        var processName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? string.Empty);
        if (!string.Equals(processName, ExpectedProcessName, StringComparison.Ordinal))
            throw new InvalidOperationException("Automated P1 acceptance is restricted to the Modular Harness Dev Preview executable.");

        var productVersion = FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).ProductVersion ?? string.Empty;
        var marker = productVersion.LastIndexOf('+');
        var embeddedHead = marker >= 0 ? productVersion[(marker + 1)..] : string.Empty;
        if (!string.Equals(embeddedHead, sourceHead, StringComparison.Ordinal))
            throw new InvalidOperationException("The automated Dev Preview binary source HEAD does not match the runtime plan.");
    }

    private static AutomatedPlan ReadPlan(string planPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(planPath));
        var root = document.RootElement;
        var schema = GetString(root, "schema_version") ?? GetString(root, "schema");
        if (!string.Equals(schema, PlanSchema, StringComparison.Ordinal))
            throw new InvalidOperationException("The automated acceptance plan schema is invalid.");
        var runId = RequireString(root, "run_id");
        if (!Regex.IsMatch(runId, "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("The automated acceptance run id is invalid.");
        var phase = RequireString(root, "phase");
        if (phase is not ("primary" or "restart"))
            throw new InvalidOperationException("The automated acceptance phase must be 'primary' or 'restart'.");
        var sourceHead = GetString(root, "source_head") ?? GetString(root, "head")
            ?? throw new InvalidOperationException("The automated acceptance plan is missing source_head.");
        if (!IsFullHead(sourceHead)) throw new InvalidOperationException("The automated acceptance plan source_head is invalid.");
        if (!root.TryGetProperty("scenario_ids", out var scenarioArray) || scenarioArray.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("The automated acceptance plan is missing scenario_ids.");
        var ids = scenarioArray.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        if (phase == "primary" && (ids.Length != 1 || !FixedScenarioIds.Contains(ids[0], StringComparer.Ordinal)))
            throw new InvalidOperationException("A primary automated process must contain exactly one fixed P1 scenario.");
        if (phase == "restart" &&
            (ids.Length != 1 || ids[0] is not (SelectionScenario or CollapseScenario or ThumbnailScenario)))
            throw new InvalidOperationException("A restart automated process must contain exactly one persistence scenario.");
        return new(
            runId,
            phase,
            sourceHead,
            RequireString(root, "scenario_root"),
            GetString(root, "fixture_root"),
            ids);
    }

    private ScenarioState EnsureScenario(string scenarioId) =>
        _scenarios.TryGetValue(scenarioId, out var state)
            ? state
            : throw new InvalidOperationException($"Unknown automated P1 scenario '{scenarioId}'.");

    private string GetScenarioRoot(string scenarioId) =>
        Path.Combine(_runRoot, "app", "scenarios", SanitizeFileName(scenarioId.Replace('/', '-')));

    private string RelativeToRunRoot(string absolutePath) =>
        Path.GetRelativePath(_runRoot, absolutePath).Replace(Path.DirectorySeparatorChar, '/');

    private static void WriteJsonAtomically(string path, object value, bool overwrite)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + $".{Environment.ProcessId}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, JsonOptions), new UTF8Encoding(false));
        File.Move(temporaryPath, path, overwrite);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var result = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        return result.Length == 0 ? "artifact" : result;
    }

    private static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string HashBytes(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string FormatHwnd(long value) => $"0x{value:x}";

    private static bool IsFullHead(string? value) =>
        value is not null && Regex.IsMatch(value, "^[0-9a-f]{40}$", RegexOptions.CultureInvariant);

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static void EnsurePathInside(string childPath, string parentPath, string description)
    {
        var child = NormalizePath(childPath);
        var parent = NormalizePath(parentPath);
        var prefix = parent + Path.DirectorySeparatorChar;
        if (!child.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The automated acceptance {description} must be inside the run root.");
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string RequireString(JsonElement element, string name) =>
        GetString(element, name) ?? throw new InvalidOperationException($"Required JSON string '{name}' is missing.");

    private static int RequireInt32(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.TryGetInt32(out var value)
            ? value
            : throw new InvalidOperationException($"Required JSON integer '{name}' is missing.");

    private static long RequireInt64(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.TryGetInt64(out var value)
            ? value
            : throw new InvalidOperationException($"Required JSON integer '{name}' is missing.");

    internal sealed record ArtifactState(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("sha256")] string Sha256,
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("scenario_id")] string ScenarioId,
        [property: JsonPropertyName("pid")] int Pid,
        [property: JsonPropertyName("hwnd_numeric")] long HwndNumeric,
        [property: JsonPropertyName("scenario_root")] string ScenarioRoot,
        [property: JsonPropertyName("phase")] string Phase,
        [property: JsonPropertyName("process_session_id")] string ProcessSessionId,
        [property: JsonPropertyName("run_id")] string RunId,
        [property: JsonPropertyName("source_head")] string SourceHead,
        [property: JsonPropertyName("executable_sha256")] string ExecutableSha256,
        [property: JsonPropertyName("asset_module_sha256")] string AssetModuleSha256)
    {
        [JsonPropertyName("validation_mode")]
        public string ValidationMode => "automated";

        [JsonPropertyName("owner_manual_ux_smoke")]
        public string OwnerManualUxSmoke => "waived";

        [JsonPropertyName("manual_evidence_claimed")]
        public bool ManualEvidenceClaimed => false;

        [JsonPropertyName("automated_capture_status")]
        public string AutomatedCaptureStatus => "captured";

        [JsonPropertyName("historical_manual_gate")]
        public string HistoricalManualGate => AssetLibraryP1AutomatedAcceptanceController.HistoricalManualGate;

        [JsonPropertyName("hwnd")]
        public string Hwnd => FormatHwnd(HwndNumeric);
    }

    private sealed record AutomatedPlan(
        string RunId,
        string Phase,
        string SourceHead,
        string ScenarioRoot,
        string? FixtureRoot,
        IReadOnlyList<string> ScenarioIds);

    private sealed class ScenarioState(string id, long sequence)
    {
        internal string Id { get; } = id;
        internal long Sequence { get; } = sequence;
        internal string Status { get; set; } = "not-run";
        internal int Pid { get; set; }
        internal long Hwnd { get; set; }
        internal int RestartPid { get; set; }
        internal long RestartHwnd { get; set; }
        internal string? PrimaryProcessSessionId { get; set; }
        internal string? RestartProcessSessionId { get; set; }
        internal int RetryCommandCount { get; set; }
        internal int FilePickerCount { get; set; }
        internal DatabaseState Database { get; } = new();
        internal List<object> ImportEvents { get; } = [];
        internal Dictionary<string, object?> Checks { get; } = new(StringComparer.Ordinal);
        internal List<string> ScreenshotPaths { get; } = [];
        internal List<string> BoundsPaths { get; } = [];

        internal void LoadPrimary(JsonElement previous)
        {
            Pid = RequireInt32(previous, "pid");
            Hwnd = previous.TryGetProperty("hwnd_numeric", out var hwndNumeric) && hwndNumeric.TryGetInt64(out var numeric)
                ? numeric
                : ParseHwnd(RequireString(previous, "hwnd"));
            PrimaryProcessSessionId = RequireString(previous, "primary_process_session_id");
            RetryCommandCount = previous.TryGetProperty("retry_command_count", out var retry) && retry.TryGetInt32(out var retryCount)
                ? retryCount
                : 0;
            FilePickerCount = previous.TryGetProperty("file_picker_count", out var picker) && picker.TryGetInt32(out var pickerCount)
                ? pickerCount
                : 0;
            if (previous.TryGetProperty("database", out var database)) Database.Load(database);
            if (previous.TryGetProperty("import_events", out var imports) && imports.ValueKind == JsonValueKind.Array)
                ImportEvents.AddRange(imports.EnumerateArray().Select(item => (object)item.Clone()));
            if (previous.TryGetProperty("checks", out var checks) && checks.ValueKind == JsonValueKind.Object)
                foreach (var property in checks.EnumerateObject()) Checks[property.Name] = property.Value.Clone();
            if (previous.TryGetProperty("screenshot_paths", out var screenshots) && screenshots.ValueKind == JsonValueKind.Array)
                ScreenshotPaths.AddRange(screenshots.EnumerateArray().Select(item => item.GetString()).Where(item => item is not null)!);
            if (previous.TryGetProperty("bounds_paths", out var bounds) && bounds.ValueKind == JsonValueKind.Array)
                BoundsPaths.AddRange(bounds.EnumerateArray().Select(item => item.GetString()).Where(item => item is not null)!);
        }

        internal object ToContract(string scenarioRoot) => new
        {
            validation_mode = "automated",
            owner_manual_ux_smoke = "waived",
            manual_evidence_claimed = false,
            automated_capture_status = "captured",
            historical_manual_gate = HistoricalManualGate,
            id = Id,
            sequence = Sequence,
            status = Status,
            pid = Pid,
            hwnd = FormatHwnd(Hwnd),
            hwnd_numeric = Hwnd,
            restart_pid = RestartPid,
            restart_hwnd = FormatHwnd(RestartHwnd),
            restart_hwnd_numeric = RestartHwnd,
            primary_process_session_id = PrimaryProcessSessionId,
            restart_process_session_id = RestartProcessSessionId,
            scenario_root = scenarioRoot,
            retry_command_count = RetryCommandCount,
            file_picker_count = FilePickerCount,
            database = Database.ToContract(),
            import_events = ImportEvents,
            checks = Checks,
            screenshot_paths = ScreenshotPaths,
            bounds_paths = BoundsPaths,
        };
    }

    private sealed class DatabaseState
    {
        internal string? Path { get; set; }
        internal string? ActivePath { get; set; }
        internal string? EvidencePath { get; set; }
        internal string? EvidenceAbsolutePath { get; set; }
        internal List<string> EvidencePaths { get; } = [];
        internal string? Sha256 { get; set; }
        internal string? RepositoryImplementation { get; set; }
        internal bool RealRepository { get; set; }
        internal int? SchemaVersion { get; set; }
        internal string SchemaQuery { get; set; } = AssetLibraryP1AutomatedAcceptanceController.SchemaQuery;
        internal int? AssetCount { get; set; }
        internal bool WalPresentAfterClose { get; set; }
        internal bool ShmPresentAfterClose { get; set; }

        internal void Load(JsonElement database)
        {
            Path = GetString(database, "active_database_absolute_path") ?? GetString(database, "absolute_path");
            ActivePath = GetString(database, "active_database_path");
            EvidencePath = GetString(database, "path");
            EvidenceAbsolutePath = GetString(database, "absolute_path");
            if (database.TryGetProperty("evidence_paths", out var evidencePaths) && evidencePaths.ValueKind == JsonValueKind.Array)
                EvidencePaths.AddRange(evidencePaths.EnumerateArray().Select(item => item.GetString()).Where(item => item is not null)!);
            else if (!string.IsNullOrWhiteSpace(EvidencePath))
                EvidencePaths.Add(EvidencePath);
            Sha256 = GetString(database, "sha256");
            RepositoryImplementation = GetString(database, "repository_implementation");
            RealRepository = database.TryGetProperty("real_repository", out var real) && real.ValueKind == JsonValueKind.True;
            SchemaVersion = database.TryGetProperty("schema_version", out var version) && version.TryGetInt32(out var schemaVersion)
                ? schemaVersion
                : null;
            SchemaQuery = GetString(database, "schema_query") ?? AssetLibraryP1AutomatedAcceptanceController.SchemaQuery;
            AssetCount = database.TryGetProperty("asset_count", out var count) && count.TryGetInt32(out var assetCount)
                ? assetCount
                : null;
            WalPresentAfterClose = database.TryGetProperty("wal_present_after_close", out var wal) && wal.ValueKind == JsonValueKind.True;
            ShmPresentAfterClose = database.TryGetProperty("shm_present_after_close", out var shm) && shm.ValueKind == JsonValueKind.True;
        }

        internal object ToContract() => new
        {
            validation_mode = "automated",
            owner_manual_ux_smoke = "waived",
            manual_evidence_claimed = false,
            automated_capture_status = "captured",
            historical_manual_gate = HistoricalManualGate,
            path = EvidencePath,
            absolute_path = EvidenceAbsolutePath,
            active_database_path = ActivePath,
            active_database_absolute_path = Path,
            evidence_paths = EvidencePaths,
            sha256 = Sha256,
            repository_implementation = RepositoryImplementation,
            real_repository = RealRepository,
            schema_version = SchemaVersion,
            schema_query = SchemaQuery,
            asset_count = AssetCount,
            wal_present_after_close = WalPresentAfterClose,
            shm_present_after_close = ShmPresentAfterClose,
        };
    }

    private static long ParseHwnd(string value) =>
        value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
        long.TryParse(value[2..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidOperationException("The persisted scenario HWND is invalid.");
}
#endif
