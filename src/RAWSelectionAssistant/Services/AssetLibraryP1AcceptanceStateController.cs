#if ASSET_LIBRARY_P1_STATE_ACCEPTANCE
using System.Diagnostics;
using System.Text.Json;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using AssetLibraryPageResult = RAWSelectionAssistant.Core.Models.AssetLibraryPage;

namespace RAWSelectionAssistant.Services;

internal sealed class AssetLibraryP1AcceptanceStateController : IAssetLibraryLoadStateController
{
    internal const string OptInEnvironmentVariable = "PIXEL_TART_ASSET_LIBRARY_P1_STATE_ACCEPTANCE";
    internal const string FirstEmptyScenario = "first-empty/v1";
    internal const string LoadingErrorRetryScenario = "loading-error-retry-empty/v1";
    internal const string ExpectedProcessName = "PixelTart_ModularHarness_V1_DevPreview";
    private const string Protocol = "pixel-tart-asset-library-p1-state/v1";
    private const string QueryFailureInjectionId = "asset-library-p1-initial-query-io-once/v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _isolatedRoot;
    private readonly string _databasePath;
    private readonly string _releaseFile;
    private readonly string _snapshotFile;
    private readonly string _currentStateFile;
    private readonly string _controllerEventFile;
    private readonly string _manifestFile;
    private readonly string _scenario;
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
    private int _queryFailureInjected;
    private long _sequence;

    private AssetLibraryP1AcceptanceStateController(string isolatedRoot, string scenario, ILogService? logService)
    {
        _logService = logService;
        _scenario = scenario;
        _isolatedRoot = NormalizeRoot(isolatedRoot);
        _databasePath = Path.GetFullPath(Path.Combine(_isolatedRoot, "Data", "asset-library-v16.db"));
        var databaseArtifacts = new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm" };
        if (databaseArtifacts.Any(File.Exists))
            throw new InvalidOperationException("The P1 state acceptance scenario requires the Asset Library database, WAL, and SHM files to be absent in the isolated root.");

        var evidenceDirectory = Path.Combine(_isolatedRoot, "InputDiagnostics", "AssetLibraryP1StateAcceptance");
        Directory.CreateDirectory(evidenceDirectory);
        _releaseFile = Path.Combine(evidenceDirectory, "release-loading.gate");
        _snapshotFile = Path.Combine(evidenceDirectory, "view-model-snapshots.jsonl");
        _currentStateFile = Path.Combine(evidenceDirectory, "current-view-model-state.json");
        _controllerEventFile = Path.Combine(evidenceDirectory, "controller-events.jsonl");
        _manifestFile = Path.Combine(evidenceDirectory, "scenario-manifest.json");
        if (new[] { _releaseFile, _snapshotFile, _currentStateFile, _controllerEventFile, _manifestFile }.Any(File.Exists))
            throw new InvalidOperationException("The P1 state acceptance scenario requires a fresh isolated evidence directory.");

        _startedAt = DateTimeOffset.UtcNow;
        WriteManifestUnsafe();
    }

    public bool DisablePreviewFixtures => true;

    internal static AssetLibraryP1AcceptanceStateController? TryCreate(string isolatedRoot, ILogService? logService)
    {
        var optIn = Environment.GetEnvironmentVariable(OptInEnvironmentVariable);
        if (optIn is null) return null;
        if (optIn is not FirstEmptyScenario and not LoadingErrorRetryScenario)
            throw new InvalidOperationException($"{OptInEnvironmentVariable} must exactly match the P1 state scenario allowlist.");

        var processName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? string.Empty);
        if (!string.Equals(processName, ExpectedProcessName, StringComparison.Ordinal))
            throw new InvalidOperationException("The P1 state acceptance scenario is restricted to the Modular Harness Dev Preview executable.");

        var explicitRoot = Environment.GetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_ROOT");
        if (string.IsNullOrWhiteSpace(explicitRoot) || !Path.IsPathFullyQualified(explicitRoot))
            throw new InvalidOperationException("The P1 state acceptance scenario requires an explicit absolute PIXEL_TART_ACCEPTANCE_ROOT.");
        if (!string.Equals(NormalizeRoot(isolatedRoot), NormalizeRoot(explicitRoot), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The active application data root does not match PIXEL_TART_ACCEPTANCE_ROOT.");

        logService?.Info("Asset Library P1 deterministic state acceptance scenario enabled in the isolated Dev Preview runtime.");
        return new(NormalizeRoot(isolatedRoot), optIn, logService);
    }

    public async Task BeforeRepositoryInitializationAsync(int attempt, CancellationToken cancellationToken)
    {
        if (_scenario != LoadingErrorRetryScenario || attempt != 1) return;
        WriteControllerEvent("loading-barrier-waiting", attempt);
        while (!File.Exists(_releaseFile))
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        WriteControllerEvent("loading-barrier-released", attempt);
    }

    public async Task<AssetLibraryPageResult> ExecuteInitialQueryAsync(
        int attempt,
        Func<CancellationToken, Task<AssetLibraryPageResult>> realQuery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(realQuery);
        cancellationToken.ThrowIfCancellationRequested();
        if (_scenario == LoadingErrorRetryScenario &&
            attempt == 1 &&
            Interlocked.CompareExchange(ref _queryFailureInjected, 1, 0) == 0)
        {
            var exception = new IOException("Deterministic Asset Library P1 recoverable query failure.");
            exception.Data[AssetLibraryLoadStateExceptionMetadata.InjectionIdDataKey] = QueryFailureInjectionId;
            WriteControllerEvent(
                "recoverable-query-error-injected",
                attempt,
                exception.GetType().FullName,
                QueryFailureInjectionId);
            throw exception;
        }

        WriteControllerEvent("real-repository-query-entered", attempt);
        var page = await realQuery(cancellationToken).ConfigureAwait(false);
        WriteControllerEvent("real-repository-query-completed", attempt, repositoryAssetCount: page.TotalCount);
        return page;
    }

    public void RecordState(AssetLibraryLoadStateSnapshot snapshot)
    {
        if (!string.Equals(Path.GetFullPath(snapshot.DatabasePath), _databasePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The P1 state snapshot database path does not match the fresh database verified by the controller.");

        var envelope = new
        {
            protocol = Protocol,
            sequence = Interlocked.Increment(ref _sequence),
            snapshot
        };
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        lock (_writeGate)
        {
            File.AppendAllText(_snapshotFile, json.ReplaceLineEndings(string.Empty) + Environment.NewLine);
            File.WriteAllText(_currentStateFile, json);
            UpdateManifestProofUnsafe(snapshot);
            WriteManifestUnsafe();
        }
    }

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

    private void WriteManifestUnsafe()
    {
        File.WriteAllText(
            _manifestFile,
            JsonSerializer.Serialize(new
            {
                protocol = Protocol,
                scenario = _scenario,
                processId = Environment.ProcessId,
                processName = Process.GetCurrentProcess().ProcessName,
                isolatedRoot = _isolatedRoot,
                databasePath = _databasePath,
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
                failureRecordedAt = _failureRecordedAt
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
                protocol = Protocol,
                sequence = Interlocked.Increment(ref _sequence),
                stage,
                attempt,
                exceptionType,
                injectionId,
                repositoryAssetCount,
                recordedAt = DateTimeOffset.UtcNow
            }, JsonOptions);
            lock (_writeGate)
                File.AppendAllText(_controllerEventFile, json.ReplaceLineEndings(string.Empty) + Environment.NewLine);
        }
        catch (Exception exception)
        {
            _logService?.Error("Unable to record the Asset Library P1 state controller event.", exception);
            throw;
        }
    }

    private static string NormalizeRoot(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
#endif
