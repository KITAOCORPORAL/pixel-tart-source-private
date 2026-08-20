#if ASSET_LIBRARY_P1_STATE_ACCEPTANCE
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.ViewModels;
using AssetLibraryPageResult = RAWSelectionAssistant.Core.Models.AssetLibraryPage;

namespace RAWSelectionAssistant.Services;

internal sealed class AssetLibraryP1AcceptanceStateController : IAssetLibraryLoadStateController
{
    internal const string OptInEnvironmentVariable = "PIXEL_TART_ASSET_LIBRARY_P1_STATE_ACCEPTANCE";
    internal const string StartRouteEnvironmentVariable = "PIXEL_TART_ASSET_LIBRARY_P1_START_ROUTE";
    internal const string HeadEnvironmentVariable = "PIXEL_TART_ASSET_LIBRARY_P1_HEAD";
    internal const string FirstEmptyScenario = "first-empty/v1";
    internal const string LoadingErrorRetryScenario = "loading-error-retry-empty/v1";
    internal const string AssetLibraryStartRoute = "asset-library";
    internal const string ExpectedProcessName = "PixelTart_ModularHarness_V1_DevPreview";
    private const string StateProtocol = "pixel-tart-asset-library-p1-state/v1";
    private const string RouteProtocol = "pixel-tart-asset-library-p1-route/v1";
    private const string QueryFailureInjectionId = "asset-library-p1-initial-query-io-once/v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _isolatedRoot;
    private readonly string _databasePath;
    private readonly string _sourceHead;
    private readonly string? _scenario;
    private readonly string? _releaseFile;
    private readonly string? _snapshotFile;
    private readonly string? _currentStateFile;
    private readonly string? _controllerEventFile;
    private readonly string _manifestFile;
    private readonly string? _routeSessionsFile;
    private readonly int _routeSessionIndex;
    private readonly DateTimeOffset _startedAt;
    private readonly ILogService? _logService;
    private readonly object _writeGate = new();
    private string? _repositorySource;
    private string? _repositoryImplementation;
    private int? _repositorySchemaVersion;
    private int? _repositoryAssetCount;
    private string? _repositoryProofStage;
    private DateTimeOffset? _repositoryProofRecordedAt;
    private string? _exceptionType;
    private string? _injectionId;
    private int? _failureAttempt;
    private DateTimeOffset? _failureRecordedAt;
    private string? _startRouteSource;
    private string? _startRoute;
    private string? _startRouteCurrentPage;
    private string? _startRouteHead;
    private DateTimeOffset? _startRouteRecordedAt;
    private int _queryFailureInjected;
    private int _startRouteApplied;
    private long _sequence;

    private AssetLibraryP1AcceptanceStateController(
        string isolatedRoot,
        string? scenario,
        string sourceHead,
        ILogService? logService)
    {
        _logService = logService;
        _scenario = string.IsNullOrEmpty(scenario) ? null : scenario;
        _sourceHead = sourceHead;
        _isolatedRoot = NormalizeRoot(isolatedRoot);
        _databasePath = Path.GetFullPath(Path.Combine(_isolatedRoot, "Data", "asset-library-v16.db"));
        _startedAt = DateTimeOffset.UtcNow;

        if (HasStateScenario)
        {
            EnsureFreshAcceptanceRoot(_isolatedRoot, "The P1 state acceptance scenario requires a fresh acceptance root.");
            EnsureDatabaseArtifactsAbsent(_databasePath, "The P1 state acceptance scenario requires a fresh Asset Library database.");
            var evidenceDirectory = Path.Combine(_isolatedRoot, "InputDiagnostics", "AssetLibraryP1StateAcceptance");
            Directory.CreateDirectory(evidenceDirectory);
            _releaseFile = Path.Combine(evidenceDirectory, "release-loading.gate");
            _snapshotFile = Path.Combine(evidenceDirectory, "view-model-snapshots.jsonl");
            _currentStateFile = Path.Combine(evidenceDirectory, "current-view-model-state.json");
            _controllerEventFile = Path.Combine(evidenceDirectory, "controller-events.jsonl");
            _manifestFile = Path.Combine(evidenceDirectory, "scenario-manifest.json");
            _routeSessionsFile = null;
            _routeSessionIndex = 0;
            if (new[] { _releaseFile, _snapshotFile, _currentStateFile, _controllerEventFile, _manifestFile }.Any(File.Exists))
                throw new InvalidOperationException("The P1 state acceptance scenario requires a fresh isolated evidence directory.");
            WriteStateManifestUnsafe();
        }
        else
        {
            _releaseFile = null;
            _snapshotFile = null;
            _currentStateFile = null;
            _controllerEventFile = null;
            var routeEvidence = InitializeRouteEvidence(_isolatedRoot, _databasePath, _sourceHead);
            _manifestFile = routeEvidence.CurrentSessionFile;
            _routeSessionsFile = routeEvidence.SessionsFile;
            _routeSessionIndex = routeEvidence.SessionIndex;
            WriteRouteSessionManifestUnsafe("validated");
        }
    }

    internal bool HasStateScenario => _scenario is not null;
    public bool DisablePreviewFixtures => HasStateScenario;

    internal static AssetLibraryP1AcceptanceStateController? TryCreate(string isolatedRoot, ILogService? logService)
    {
        var scenario = Environment.GetEnvironmentVariable(OptInEnvironmentVariable);
        var startRoute = Environment.GetEnvironmentVariable(StartRouteEnvironmentVariable);
        var head = Environment.GetEnvironmentVariable(HeadEnvironmentVariable);
        if (scenario is null && startRoute is null && head is null) return null;

        var processName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? string.Empty);
        var explicitRoot = Environment.GetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_ROOT");
        var buildSourceHead = GetBuildSourceHead();
        ValidateRuntimeOptIn(isolatedRoot, scenario, processName, explicitRoot, startRoute, head, buildSourceHead);

        logService?.Info(HasExactStateScenario(scenario)
            ? "Asset Library P1 deterministic state acceptance scenario enabled in the isolated Dev Preview runtime."
            : "Asset Library P1 synthetic fixture start route enabled in the isolated Dev Preview runtime.");
        return new(NormalizeRoot(isolatedRoot), scenario, buildSourceHead, logService);
    }

    internal void ApplyAcceptanceStartRoute(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (Interlocked.CompareExchange(ref _startRouteApplied, 1, 0) != 0)
            throw new InvalidOperationException("The P1 acceptance start route may only be applied once.");
        if (!viewModel.NavigateCommand.CanExecute(AssetLibraryStartRoute))
            throw new InvalidOperationException("The P1 acceptance start route is not currently executable.");

        viewModel.NavigateCommand.Execute(AssetLibraryStartRoute);
        if (!string.Equals(viewModel.CurrentPage, "AssetLibrary", StringComparison.Ordinal))
            throw new InvalidOperationException("The P1 acceptance start route did not resolve to the Asset Library surface.");

        lock (_writeGate)
        {
            _startRouteSource = StartRouteEnvironmentVariable;
            _startRoute = AssetLibraryStartRoute;
            _startRouteCurrentPage = viewModel.CurrentPage;
            _startRouteHead = _sourceHead;
            _startRouteRecordedAt = DateTimeOffset.UtcNow;
            if (HasStateScenario)
            {
                WriteStateManifestUnsafe();
            }
            else
            {
                WriteRouteSessionManifestUnsafe("applied");
                var line = JsonSerializer.Serialize(new
                {
                    protocol = RouteProtocol,
                    status = "applied",
                    sessionIndex = _routeSessionIndex,
                    processId = Environment.ProcessId,
                    processName = Process.GetCurrentProcess().ProcessName,
                    isolatedRoot = _isolatedRoot,
                    sourceHead = _sourceHead,
                    startRouteSource = _startRouteSource,
                    startRoute = _startRoute,
                    route = _startRoute,
                    currentPage = _startRouteCurrentPage,
                    appliedAt = _startRouteRecordedAt
                }, JsonOptions);
                File.AppendAllText(RequireRouteSessionsFile(), line.ReplaceLineEndings(string.Empty) + Environment.NewLine);
            }
        }
    }

    internal static string GetBuildSourceHead()
    {
        var informationalVersion = typeof(AssetLibraryP1AcceptanceStateController).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var separator = informationalVersion?.LastIndexOf('+') ?? -1;
        var sourceHead = separator >= 0 ? informationalVersion![(separator + 1)..] : null;
        if (!IsLowercaseFullHead(sourceHead))
            throw new InvalidOperationException("The P1 acceptance binary does not contain an exact lowercase 40-character build source HEAD.");
        return sourceHead!;
    }

    internal static void ValidateRuntimeOptIn(
        string isolatedRoot,
        string? scenario,
        string processName,
        string? explicitRoot,
        string? startRoute,
        string? head,
        string expectedHead)
    {
        if (!string.IsNullOrEmpty(scenario) && !HasExactStateScenario(scenario))
            throw new InvalidOperationException($"{OptInEnvironmentVariable} must be empty or exactly match the P1 state scenario allowlist.");
        if (!string.Equals(processName, ExpectedProcessName, StringComparison.Ordinal))
            throw new InvalidOperationException("The P1 acceptance route is restricted to the Modular Harness Dev Preview executable.");
        if (string.IsNullOrWhiteSpace(isolatedRoot) || !Path.IsPathFullyQualified(isolatedRoot))
            throw new InvalidOperationException("The active P1 acceptance root must be explicit and absolute.");
        if (string.IsNullOrWhiteSpace(explicitRoot) || !Path.IsPathFullyQualified(explicitRoot))
            throw new InvalidOperationException("The P1 acceptance route requires an explicit absolute PIXEL_TART_ACCEPTANCE_ROOT.");
        if (!string.Equals(NormalizeRoot(isolatedRoot), NormalizeRoot(explicitRoot), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The active application data root does not match PIXEL_TART_ACCEPTANCE_ROOT.");
        if (!string.Equals(startRoute, AssetLibraryStartRoute, StringComparison.Ordinal))
            throw new InvalidOperationException($"{StartRouteEnvironmentVariable} must exactly match '{AssetLibraryStartRoute}'.");
        if (!IsLowercaseFullHead(expectedHead))
            throw new InvalidOperationException("The P1 acceptance binary build source HEAD is invalid.");
        if (!IsLowercaseFullHead(head))
            throw new InvalidOperationException($"{HeadEnvironmentVariable} must be the exact lowercase 40-character source HEAD.");
        if (!string.Equals(head, expectedHead, StringComparison.Ordinal))
            throw new InvalidOperationException($"{HeadEnvironmentVariable} does not match the source HEAD embedded in this P1 acceptance binary.");
    }

    public async Task BeforeRepositoryInitializationAsync(int attempt, CancellationToken cancellationToken)
    {
        EnsureStateScenarioController();
        if (_scenario != LoadingErrorRetryScenario || attempt != 1) return;
        WriteControllerEvent("loading-barrier-waiting", attempt);
        while (!File.Exists(RequireStateFile(_releaseFile, "release gate")))
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        WriteControllerEvent("loading-barrier-released", attempt);
    }

    public async Task<AssetLibraryPageResult> ExecuteInitialQueryAsync(
        int attempt,
        Func<CancellationToken, Task<AssetLibraryPageResult>> realQuery,
        CancellationToken cancellationToken)
    {
        EnsureStateScenarioController();
        ArgumentNullException.ThrowIfNull(realQuery);
        cancellationToken.ThrowIfCancellationRequested();
        if (_scenario == LoadingErrorRetryScenario &&
            attempt == 1 &&
            Interlocked.CompareExchange(ref _queryFailureInjected, 1, 0) == 0)
        {
            var exception = new IOException("Deterministic Asset Library P1 recoverable query failure.");
            exception.Data[AssetLibraryLoadStateExceptionMetadata.InjectionIdDataKey] = QueryFailureInjectionId;
            WriteControllerEvent("recoverable-query-error-injected", attempt, exception.GetType().FullName, QueryFailureInjectionId);
            throw exception;
        }

        WriteControllerEvent("real-repository-query-entered", attempt);
        var page = await realQuery(cancellationToken).ConfigureAwait(false);
        WriteControllerEvent("real-repository-query-completed", attempt, repositoryAssetCount: page.TotalCount);
        return page;
    }

    public void RecordState(AssetLibraryLoadStateSnapshot snapshot)
    {
        EnsureStateScenarioController();
        if (!string.Equals(Path.GetFullPath(snapshot.DatabasePath), _databasePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The P1 state snapshot database path does not match the fresh database verified by the controller.");

        var envelope = new
        {
            protocol = StateProtocol,
            sequence = Interlocked.Increment(ref _sequence),
            snapshot
        };
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        lock (_writeGate)
        {
            File.AppendAllText(RequireStateFile(_snapshotFile, "snapshot"), json.ReplaceLineEndings(string.Empty) + Environment.NewLine);
            File.WriteAllText(RequireStateFile(_currentStateFile, "current state"), json);
            UpdateManifestProofUnsafe(snapshot);
            WriteStateManifestUnsafe();
        }
    }

    private static bool HasExactStateScenario(string? scenario) =>
        scenario is FirstEmptyScenario or LoadingErrorRetryScenario;

    private static bool IsLowercaseFullHead(string? head) =>
        head is not null && Regex.IsMatch(head, "^[0-9a-f]{40}$", RegexOptions.CultureInvariant);

    private static void EnsureDatabaseArtifactsAbsent(string databasePath, string message)
    {
        if (new[] { databasePath, databasePath + "-wal", databasePath + "-shm" }.Any(File.Exists))
            throw new InvalidOperationException(message);
    }

    private static RouteEvidenceInitialization InitializeRouteEvidence(
        string isolatedRoot,
        string databasePath,
        string sourceHead)
    {
        var evidenceDirectory = Path.Combine(isolatedRoot, "InputDiagnostics", "AssetLibraryP1RouteAcceptance");
        var rootManifestFile = Path.Combine(evidenceDirectory, "route-root-manifest.json");
        var currentSessionFile = Path.Combine(evidenceDirectory, "current-route-session.json");
        var sessionsFile = Path.Combine(evidenceDirectory, "route-sessions.jsonl");
        if (!File.Exists(rootManifestFile))
        {
            EnsureFreshAcceptanceRoot(isolatedRoot, "The first P1 synthetic route session requires a fresh acceptance root.");
            EnsureDatabaseArtifactsAbsent(databasePath, "The first P1 synthetic route session requires a fresh Asset Library database.");
            Directory.CreateDirectory(evidenceDirectory);
            File.WriteAllText(rootManifestFile, JsonSerializer.Serialize(new
            {
                protocol = RouteProtocol,
                isolatedRoot,
                databasePath,
                expectedProcessName = ExpectedProcessName,
                sourceHead,
                requestedStartRoute = AssetLibraryStartRoute,
                freshAcceptanceRootVerified = true,
                freshAssetLibraryDatabaseVerified = true,
                verifiedAt = DateTimeOffset.UtcNow
            }, JsonOptions));
            return new(currentSessionFile, sessionsFile, 1);
        }

        ValidateRouteRootManifest(rootManifestFile, isolatedRoot, databasePath, sourceHead);
        if (!File.Exists(currentSessionFile))
            throw new InvalidOperationException("The P1 synthetic route restart is missing its previous current-route-session manifest.");
        using var current = JsonDocument.Parse(File.ReadAllText(currentSessionFile));
        var root = current.RootElement;
        RequireExactJsonString(root, "protocol", RouteProtocol);
        RequireExactJsonString(root, "status", "applied");
        RequireExactJsonString(root, "processName", ExpectedProcessName);
        RequireExactJsonPath(root, "isolatedRoot", isolatedRoot);
        RequireExactJsonString(root, "sourceHead", sourceHead);
        RequireExactJsonString(root, "startRoute", AssetLibraryStartRoute);
        RequireExactJsonString(root, "route", AssetLibraryStartRoute);
        RequireExactJsonString(root, "currentPage", "AssetLibrary");
        var previousIndex = RequirePositiveJsonInt(root, "sessionIndex");
        return new(currentSessionFile, sessionsFile, checked(previousIndex + 1));
    }

    private static void ValidateRouteRootManifest(
        string manifestFile,
        string isolatedRoot,
        string databasePath,
        string sourceHead)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestFile));
        var root = manifest.RootElement;
        RequireExactJsonString(root, "protocol", RouteProtocol);
        RequireExactJsonPath(root, "isolatedRoot", isolatedRoot);
        RequireExactJsonPath(root, "databasePath", databasePath);
        RequireExactJsonString(root, "expectedProcessName", ExpectedProcessName);
        RequireExactJsonString(root, "sourceHead", sourceHead);
        RequireExactJsonString(root, "requestedStartRoute", AssetLibraryStartRoute);
        if (!root.TryGetProperty("freshAcceptanceRootVerified", out var freshRoot) || freshRoot.ValueKind != JsonValueKind.True ||
            !root.TryGetProperty("freshAssetLibraryDatabaseVerified", out var freshDatabase) || freshDatabase.ValueKind != JsonValueKind.True)
            throw new InvalidOperationException("The P1 synthetic route provenance does not prove a fresh acceptance root.");
    }

    private static void EnsureFreshAcceptanceRoot(string isolatedRoot, string message)
    {
        if (File.Exists(isolatedRoot) ||
            Directory.Exists(isolatedRoot) && Directory.EnumerateFileSystemEntries(isolatedRoot).Any())
            throw new InvalidOperationException(message);
    }

    private static void RequireExactJsonString(JsonElement root, string propertyName, string expected)
    {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            !string.Equals(property.GetString(), expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"The P1 synthetic route manifest has an invalid '{propertyName}'.");
    }

    private static void RequireExactJsonPath(JsonElement root, string propertyName, string expected)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"The P1 synthetic route manifest is missing '{propertyName}'.");
        var actual = property.GetString();
        if (string.IsNullOrWhiteSpace(actual) ||
            !string.Equals(NormalizeRoot(actual), NormalizeRoot(expected), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The P1 synthetic route manifest has an invalid '{propertyName}'.");
    }

    private static int RequirePositiveJsonInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || !property.TryGetInt32(out var value) || value < 1)
            throw new InvalidOperationException($"The P1 synthetic route manifest has an invalid '{propertyName}'.");
        return value;
    }

    private void EnsureStateScenarioController()
    {
        if (!HasStateScenario)
            throw new InvalidOperationException("The synthetic fixture start-route controller cannot inject or record a P1 state scenario.");
    }

    private static string RequireStateFile(string? path, string description) =>
        path ?? throw new InvalidOperationException($"The P1 state {description} file is unavailable in a route-only session.");

    private string RequireRouteSessionsFile() =>
        _routeSessionsFile ?? throw new InvalidOperationException("The P1 route session journal is unavailable in a state scenario.");

    private void UpdateManifestProofUnsafe(AssetLibraryLoadStateSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.RepositorySource)) _repositorySource = snapshot.RepositorySource;
        if (!string.IsNullOrWhiteSpace(snapshot.RepositoryImplementation)) _repositoryImplementation = snapshot.RepositoryImplementation;
        if (snapshot.RepositorySchemaVersion is not null) _repositorySchemaVersion = snapshot.RepositorySchemaVersion;
        if (snapshot.RepositoryAssetCount is not null) _repositoryAssetCount = snapshot.RepositoryAssetCount;
        if (_repositorySource is not null && _repositoryImplementation is not null && _repositorySchemaVersion is not null && _repositoryAssetCount is not null)
        {
            _repositoryProofStage = snapshot.Stage;
            _repositoryProofRecordedAt = snapshot.RecordedAt;
        }
        if (!string.IsNullOrWhiteSpace(snapshot.ExceptionType))
        {
            _exceptionType = snapshot.ExceptionType;
            _injectionId = snapshot.InjectionId;
            _failureAttempt = snapshot.Attempt;
            _failureRecordedAt = snapshot.RecordedAt;
        }
    }

    private void WriteStateManifestUnsafe()
    {
        File.WriteAllText(_manifestFile, JsonSerializer.Serialize(new
        {
            protocol = StateProtocol,
            scenario = _scenario,
            stateScenarioEnabled = true,
            processId = Environment.ProcessId,
            processName = Process.GetCurrentProcess().ProcessName,
            isolatedRoot = _isolatedRoot,
            databasePath = _databasePath,
            sourceHead = _sourceHead,
            freshAcceptanceRootVerified = true,
            freshDatabaseVerified = true,
            releaseFile = _releaseFile,
            snapshotFile = _snapshotFile,
            currentStateFile = _currentStateFile,
            controllerEventFile = _controllerEventFile,
            startedAt = _startedAt,
            repositorySource = _repositorySource,
            repositoryImplementation = _repositoryImplementation,
            repositorySchemaVersion = _repositorySchemaVersion,
            repositoryAssetCount = _repositoryAssetCount,
            repositoryProofStage = _repositoryProofStage,
            repositoryProofRecordedAt = _repositoryProofRecordedAt,
            exceptionType = _exceptionType,
            injectionId = _injectionId,
            failureAttempt = _failureAttempt,
            failureRecordedAt = _failureRecordedAt,
            startRouteSource = _startRouteSource,
            startRoute = _startRoute,
            startRouteCurrentPage = _startRouteCurrentPage,
            startRouteHead = _startRouteHead,
            startRouteRecordedAt = _startRouteRecordedAt
        }, JsonOptions));
    }

    private void WriteRouteSessionManifestUnsafe(string status)
    {
        File.WriteAllText(_manifestFile, JsonSerializer.Serialize(new
        {
            protocol = RouteProtocol,
            status,
            sessionIndex = _routeSessionIndex,
            stateScenario = (string?)null,
            stateScenarioEnabled = false,
            previewFixturesDisabled = false,
            processId = Environment.ProcessId,
            processName = Process.GetCurrentProcess().ProcessName,
            isolatedRoot = _isolatedRoot,
            databasePath = _databasePath,
            sourceHead = _sourceHead,
            requestedStartRoute = AssetLibraryStartRoute,
            freshAcceptanceRootVerified = true,
            freshAssetLibraryDatabaseVerified = true,
            startedAt = _startedAt,
            startRouteSource = _startRouteSource,
            startRoute = _startRoute,
            route = _startRoute,
            currentPage = _startRouteCurrentPage,
            startRouteHead = _startRouteHead,
            appliedAt = _startRouteRecordedAt
        }, JsonOptions));
    }

    private void WriteControllerEvent(
        string stage,
        int attempt,
        string? exceptionType = null,
        string? injectionId = null,
        int? repositoryAssetCount = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(new
            {
                protocol = StateProtocol,
                sequence = Interlocked.Increment(ref _sequence),
                stage,
                attempt,
                exceptionType,
                injectionId,
                repositoryAssetCount,
                recordedAt = DateTimeOffset.UtcNow
            }, JsonOptions);
            lock (_writeGate)
                File.AppendAllText(RequireStateFile(_controllerEventFile, "controller event"), json.ReplaceLineEndings(string.Empty) + Environment.NewLine);
        }
        catch (Exception exception)
        {
            _logService?.Error("Unable to record the Asset Library P1 state controller event.", exception);
            throw;
        }
    }

    private static string NormalizeRoot(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private sealed record RouteEvidenceInitialization(string CurrentSessionFile, string SessionsFile, int SessionIndex);
}
#endif
