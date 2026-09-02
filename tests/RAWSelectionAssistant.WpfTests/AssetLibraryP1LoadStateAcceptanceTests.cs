using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Text.Json;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Tasks;
using AssetLibraryPageResult = RAWSelectionAssistant.Core.Models.AssetLibraryPage;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP1LoadStateAcceptanceTests
{
    [TestMethod]
    public Task FirstEmptyScenarioUsesOneRealInitializationWithoutPreviewFixtures() => RunSta(() =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P1-FirstEmpty", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "asset-library.db");
        var controller = new PassThroughLoadStateController();
        AssetLibraryViewModel? viewModel = null;
        try
        {
            viewModel = new(
                databasePath,
                new TaskOperationBridge(),
                [new("AssetLibraryModuleDiagnostic", "asset")],
                enablePreviewFeatures: true,
                loadStateController: controller);

            viewModel.InitializeAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1, viewModel.LoadAttempt);
            Assert.IsTrue(viewModel.IsReady);
            Assert.IsFalse(viewModel.IsLoading);
            Assert.IsFalse(viewModel.HasLoadError);
            Assert.IsTrue(viewModel.IsEmptyStateVisible);
            Assert.HasCount(0, viewModel.AssetCards);
            Assert.HasCount(0, viewModel.Folders);
            Assert.HasCount(0, viewModel.ModuleDiagnostics);
            AssertDatabaseIsV7AndEmpty(databasePath);
            AssertRepositoryProof(controller.Snapshots.Single(snapshot => snapshot.Stage == "ready"), expectedAssetCount: 0);
        }
        finally
        {
            viewModel?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    });

    [TestMethod]
    public Task LoadingBarrierThenRecoverableErrorAndRetryUseTheSameViewModelAndRealEmptyV6Repository() => RunSta(() =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P1-StateAcceptance", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "asset-library.db");
        var controller = new DeterministicLoadStateController();
        AssetLibraryViewModel? viewModel = null;
        try
        {
            viewModel = new(
                databasePath,
                new TaskOperationBridge(),
                [new("AssetLibraryModuleDiagnostic", "asset")],
                enablePreviewFeatures: true,
                loadStateController: controller);
            var originalViewModel = viewModel;

            var firstAttempt = viewModel.InitializeAsync();
            Assert.IsTrue(controller.LoadingBarrierEntered.Wait(TimeSpan.FromSeconds(10)), "The initial load did not enter the deterministic barrier.");
            Assert.AreSame(originalViewModel, viewModel);
            Assert.AreEqual(1, viewModel.LoadAttempt);
            Assert.IsTrue(viewModel.IsLoading);
            Assert.IsFalse(viewModel.IsReady);
            Assert.IsFalse(viewModel.HasLoadError);
            Assert.HasCount(0, viewModel.ModuleDiagnostics, "Acceptance state mode must disable preview diagnostics and seed fixtures.");

            controller.ReleaseLoadingBarrier();
            Assert.IsTrue(firstAttempt.Wait(TimeSpan.FromSeconds(15)), "The first load attempt did not leave the released barrier.");
            firstAttempt.GetAwaiter().GetResult();

            Assert.AreSame(originalViewModel, viewModel);
            Assert.AreEqual(1, viewModel.LoadAttempt);
            Assert.IsFalse(viewModel.IsLoading);
            Assert.IsFalse(viewModel.IsReady);
            Assert.IsTrue(viewModel.HasLoadError);
            Assert.IsTrue(viewModel.RetryLoadCommand.CanExecute(null));

            viewModel.RetryLoadCommand.Execute(null);
            Assert.IsTrue(controller.ReadyAfterRetry.Wait(TimeSpan.FromSeconds(15)), "The Retry command did not complete the second real repository attempt.");
            Assert.IsTrue(SpinWait.SpinUntil(() => viewModel.RetryLoadCommand.CanExecute(null), TimeSpan.FromSeconds(5)));

            Assert.AreSame(originalViewModel, viewModel);
            Assert.AreEqual(2, viewModel.LoadAttempt);
            Assert.IsTrue(viewModel.IsReady);
            Assert.IsFalse(viewModel.IsLoading);
            Assert.IsFalse(viewModel.HasLoadError);
            Assert.IsTrue(viewModel.IsEmptyStateVisible);
            Assert.HasCount(0, viewModel.AssetCards);
            Assert.HasCount(0, viewModel.Folders, "Preview folder seeding must stay disabled in the acceptance scenario.");
            CollectionAssert.AreEqual(new[] { 1, 2 }, controller.RepositoryInitializationAttempts.ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2 }, controller.InitialQueryAttempts.ToArray());

            AssertDatabaseIsV7AndEmpty(databasePath);

            var error = controller.Snapshots.Single(snapshot => snapshot.Attempt == 1 && snapshot.Stage == "error-visible");
            Assert.IsTrue(error.HasLoadError);
            Assert.AreEqual(typeof(IOException).FullName, error.ExceptionType);
            Assert.AreEqual(DeterministicLoadStateController.InjectionId, error.InjectionId);
            Assert.IsNull(error.RepositoryAssetCount, "The failed decorated query must not self-report a repository count.");

            var ready = controller.Snapshots.Single(snapshot => snapshot.Attempt == 2 && snapshot.Stage == "ready");
            Assert.IsTrue(ready.IsReady);
            Assert.AreEqual(0, ready.VisibleAssetCount);
            AssertRepositoryProof(ready, expectedAssetCount: 0);
            CollectionAssert.AreEqual(new[] { 2 }, controller.RealQueryAttempts.ToArray(), "Only Retry attempt 2 may invoke the real query delegate.");
        }
        finally
        {
            viewModel?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    });

    [TestMethod]
    public Task RepositoryProofReflectsARealNonEmptyDatabaseInsteadOfAConstant() => RunSta(() =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P1-RepositoryProof", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "asset-library.db");
        var sourcePath = Path.Combine(root, "proof.jpg");
        File.WriteAllBytes(sourcePath, new byte[] { 1 });
        var seedRepository = new SqliteAssetLibraryRepository(databasePath);
        seedRepository.ImportAsync([new AssetImportRequest(sourcePath)]).GetAwaiter().GetResult();
        seedRepository.DisposeAsync().AsTask().GetAwaiter().GetResult();

        var controller = new PassThroughLoadStateController();
        AssetLibraryViewModel? viewModel = null;
        try
        {
            viewModel = new(databasePath, new TaskOperationBridge(), [], loadStateController: controller);
            viewModel.InitializeAsync().GetAwaiter().GetResult();

            AssertRepositoryProof(controller.Snapshots.Single(snapshot => snapshot.Stage == "ready"), expectedAssetCount: 1);
            AssertDatabaseSchemaAndCount(databasePath, expectedAssetCount: 1);
        }
        finally
        {
            viewModel?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    });

    [TestMethod]
    public Task DisposeWhileLoadingBarrierIsHeldCancelsInitializationBeforeRepositoryDisposal() => RunSta(() =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P1-DisposeBarrier", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var controller = new DeterministicLoadStateController();
        AssetLibraryViewModel? viewModel = new(
            Path.Combine(root, "asset-library.db"),
            new TaskOperationBridge(),
            [],
            loadStateController: controller);
        var disposed = false;
        try
        {
            var initialization = viewModel.InitializeAsync();
            Assert.IsTrue(controller.LoadingBarrierEntered.Wait(TimeSpan.FromSeconds(10)));

            var disposal = viewModel.DisposeAsync().AsTask();
            Assert.IsTrue(disposal.Wait(TimeSpan.FromSeconds(10)), "DisposeAsync did not wait for and cancel the barrier-held initialization.");
            disposal.GetAwaiter().GetResult();
            disposed = true;
            Assert.IsTrue(initialization.Wait(TimeSpan.FromSeconds(5)));
            initialization.GetAwaiter().GetResult();

            Assert.IsFalse(viewModel.IsReady, "A canceled initialization must never transition to ready.");
            Assert.IsFalse(viewModel.IsLoading);
            Assert.HasCount(0, controller.InitialQueryAttempts);
            Assert.IsTrue(controller.Snapshots.Any(snapshot => snapshot.Stage == "initialization-canceled"));
        }
        finally
        {
            if (!disposed) viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    });

    [TestMethod]
    public Task ProductionFirstEmptyControllerUsesAnIndependentFreshDatabaseSession() => RunSta(() =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P1-ProductionFirstEmpty", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var controller = CreateProductionController(root, "first-empty/v1");
        if (controller is null)
        {
            Directory.Delete(root, recursive: true);
            return;
        }

        var databasePath = Path.Combine(root, "Data", "asset-library-v16.db");
        AssetLibraryViewModel? viewModel = null;
        try
        {
            AssertFreshManifest(root, databasePath, "first-empty/v1");
            viewModel = new(databasePath, new TaskOperationBridge(), [], loadStateController: controller);
            viewModel.InitializeAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1, viewModel.LoadAttempt);
            Assert.IsTrue(viewModel.IsReady);
            AssertManifestProof(root, expectedAssetCount: 0, expectedFailure: false);
            var events = ReadEvidence(root, "controller-events.jsonl");
            Assert.AreEqual(1, CountOccurrences(events, "\"stage\": \"real-repository-query-entered\""));
            Assert.AreEqual(1, CountOccurrences(events, "\"stage\": \"real-repository-query-completed\""));
            Assert.DoesNotContain("recoverable-query-error-injected", events, StringComparison.Ordinal);
        }
        finally
        {
            viewModel?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    });

    [TestMethod]
    public Task ProductionLoadingErrorRetryControllerInjectsOnceThenAwaitsTheRealQuery() => RunSta(() =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P1-ProductionRetry", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var controller = CreateProductionController(root, "loading-error-retry-empty/v1");
        if (controller is null)
        {
            Directory.Delete(root, recursive: true);
            return;
        }

        var databasePath = Path.Combine(root, "Data", "asset-library-v16.db");
        var releaseFile = EvidencePath(root, "release-loading.gate");
        AssetLibraryViewModel? viewModel = null;
        try
        {
            AssertFreshManifest(root, databasePath, "loading-error-retry-empty/v1");
            viewModel = new(databasePath, new TaskOperationBridge(), [], loadStateController: controller);
            var originalViewModel = viewModel;
            var firstAttempt = viewModel.InitializeAsync();
            Assert.IsTrue(SpinWait.SpinUntil(
                () => FileContains(EvidencePath(root, "controller-events.jsonl"), "loading-barrier-waiting"),
                TimeSpan.FromSeconds(10)));
            Assert.IsFalse(File.Exists(databasePath), "The loading barrier must be observable before repository initialization creates SQLite.");

            File.WriteAllText(releaseFile, "release");
            Assert.IsTrue(firstAttempt.Wait(TimeSpan.FromSeconds(15)));
            firstAttempt.GetAwaiter().GetResult();
            Assert.AreSame(originalViewModel, viewModel);
            Assert.AreEqual(1, viewModel.LoadAttempt);
            Assert.IsTrue(viewModel.HasLoadError);
            Assert.IsFalse(viewModel.IsReady);

            viewModel.RetryLoadCommand.Execute(null);
            Assert.IsTrue(SpinWait.SpinUntil(() => viewModel.IsReady && viewModel.LoadAttempt == 2, TimeSpan.FromSeconds(15)));
            Assert.AreSame(originalViewModel, viewModel);
            AssertManifestProof(root, expectedAssetCount: 0, expectedFailure: true);

            var events = ReadEvidence(root, "controller-events.jsonl");
            Assert.AreEqual(1, CountOccurrences(events, "recoverable-query-error-injected"));
            Assert.AreEqual(1, CountOccurrences(events, "\"stage\": \"real-repository-query-entered\""));
            Assert.AreEqual(1, CountOccurrences(events, "\"stage\": \"real-repository-query-completed\""));
            StringAssert.Contains(events, "\"attempt\": 2");
        }
        finally
        {
            viewModel?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    });

    [TestMethod]
    public void ProductionControllerRejectsAnyExistingAssetDatabaseArtifact()
    {
        if (ProductionControllerType() is null) return;
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var root = Path.Combine(Path.GetTempPath(), "PixelTart-P1-ProductionFreshRoot", Guid.NewGuid().ToString("N"));
            var databasePath = Path.Combine(root, "Data", "asset-library-v16.db");
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            File.WriteAllText(databasePath + suffix, "stale");
            try
            {
                _ = InvokeProductionController(root, "first-empty/v1");
                Assert.Fail($"Existing database artifact '{suffix}' was accepted.");
            }
            catch (TargetInvocationException exception)
            {
                Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch { }
            }
        }
    }

    private static Task RunSta(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static void AssertDatabaseIsV7AndEmpty(string databasePath) => AssertDatabaseSchemaAndCount(databasePath, 0);

    private static void AssertDatabaseSchemaAndCount(string databasePath, int expectedAssetCount)
    {
        var database = new AssetLibraryDatabase(databasePath);
        using var connection = database.OpenConnectionAsync().GetAwaiter().GetResult();
        using var schema = connection.CreateCommand();
        schema.CommandText = "SELECT MAX(Version) FROM AssetLibrarySchemaInfo;";
        Assert.AreEqual(7L, Convert.ToInt64(schema.ExecuteScalar()));
        using var assets = connection.CreateCommand();
        assets.CommandText = "SELECT COUNT(*) FROM AssetItems;";
        Assert.AreEqual(expectedAssetCount, Convert.ToInt32(assets.ExecuteScalar()));
    }

    private static void AssertRepositoryProof(AssetLibraryLoadStateSnapshot snapshot, int expectedAssetCount)
    {
        Assert.AreEqual("real-repository", snapshot.RepositorySource);
        Assert.AreEqual(nameof(SqliteAssetLibraryRepository), snapshot.RepositoryImplementation);
        Assert.AreEqual(7, snapshot.RepositorySchemaVersion);
        Assert.AreEqual(expectedAssetCount, snapshot.RepositoryAssetCount);
    }

    private static IAssetLibraryLoadStateController? CreateProductionController(string root, string scenario)
    {
        if (ProductionControllerType() is null) return null;
        return (IAssetLibraryLoadStateController)InvokeProductionController(root, scenario)!;
    }

    private static object? InvokeProductionController(string root, string scenario)
    {
        var type = ProductionControllerType() ?? throw new InvalidOperationException("The special P1 controller is not compiled into this build.");
        var constructor = type.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(string), typeof(string), typeof(string), typeof(RAWSelectionAssistant.Core.Services.ILogService)],
            modifiers: null) ?? throw new MissingMethodException(type.FullName, ".ctor");
        var sourceHead = (string)(type.GetMethod("GetBuildSourceHead", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(type.FullName, "GetBuildSourceHead")).Invoke(null, null)!;
        return constructor.Invoke([root, scenario, sourceHead, null]);
    }

    private static Type? ProductionControllerType() =>
        typeof(RAWSelectionAssistant.App).Assembly.GetType("RAWSelectionAssistant.Services.AssetLibraryP1AcceptanceStateController");

    private static string? BuildSourceHead()
    {
        var type = ProductionControllerType();
        return type is null
            ? null
            : (string)(type.GetMethod("GetBuildSourceHead", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(type.FullName, "GetBuildSourceHead")).Invoke(null, null)!;
    }

    private static void AssertFreshManifest(string root, string databasePath, string scenario)
    {
        using var document = JsonDocument.Parse(ReadEvidence(root, "scenario-manifest.json"));
        var manifest = document.RootElement;
        Assert.AreEqual(scenario, manifest.GetProperty("scenario").GetString());
        Assert.AreEqual(Path.GetFullPath(databasePath), manifest.GetProperty("databasePath").GetString());
        Assert.AreEqual(BuildSourceHead(), manifest.GetProperty("sourceHead").GetString());
        Assert.IsTrue(manifest.GetProperty("freshDatabaseVerified").GetBoolean());
        Assert.IsFalse(File.Exists(databasePath));
        Assert.IsFalse(File.Exists(databasePath + "-wal"));
        Assert.IsFalse(File.Exists(databasePath + "-shm"));
    }

    private static void AssertManifestProof(string root, int expectedAssetCount, bool expectedFailure)
    {
        using var document = JsonDocument.Parse(ReadEvidence(root, "scenario-manifest.json"));
        var manifest = document.RootElement;
        Assert.AreEqual("real-repository", manifest.GetProperty("repositorySource").GetString());
        Assert.AreEqual(nameof(SqliteAssetLibraryRepository), manifest.GetProperty("repositoryImplementation").GetString());
        Assert.AreEqual(7, manifest.GetProperty("repositorySchemaVersion").GetInt32());
        Assert.AreEqual(expectedAssetCount, manifest.GetProperty("repositoryAssetCount").GetInt32());
        Assert.AreEqual("ready", manifest.GetProperty("repositoryProofStage").GetString());
        if (expectedFailure)
        {
            Assert.AreEqual(typeof(IOException).FullName, manifest.GetProperty("exceptionType").GetString());
            Assert.AreEqual("asset-library-p1-initial-query-io-once/v1", manifest.GetProperty("injectionId").GetString());
            Assert.AreEqual(1, manifest.GetProperty("failureAttempt").GetInt32());
        }
    }

    private static string EvidencePath(string root, string fileName) =>
        Path.Combine(root, "InputDiagnostics", "AssetLibraryP1StateAcceptance", fileName);

    private static string ReadEvidence(string root, string fileName) => File.ReadAllText(EvidencePath(root, fileName));

    private static bool FileContains(string path, string value)
    {
        try { return File.Exists(path) && File.ReadAllText(path).Contains(value, StringComparison.Ordinal); }
        catch (IOException) { return false; }
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var index = 0; (index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length) count++;
        return count;
    }

    private sealed class PassThroughLoadStateController : IAssetLibraryLoadStateController
    {
        public bool DisablePreviewFixtures => true;
        public ConcurrentQueue<AssetLibraryLoadStateSnapshot> Snapshots { get; } = new();
        public Task BeforeRepositoryInitializationAsync(int attempt, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<AssetLibraryPageResult> ExecuteInitialQueryAsync(int attempt, Func<CancellationToken, Task<AssetLibraryPageResult>> realQuery, CancellationToken cancellationToken) => realQuery(cancellationToken);
        public void RecordState(AssetLibraryLoadStateSnapshot snapshot) => Snapshots.Enqueue(snapshot);
    }

    private sealed class DeterministicLoadStateController : IAssetLibraryLoadStateController
    {
        public const string InjectionId = "asset-library-p1-test-query-io-once/v1";
        private readonly TaskCompletionSource _releaseLoading = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _failureInjected;

        public bool DisablePreviewFixtures => true;
        public ManualResetEventSlim LoadingBarrierEntered { get; } = new(false);
        public ManualResetEventSlim ReadyAfterRetry { get; } = new(false);
        public ConcurrentQueue<int> RepositoryInitializationAttempts { get; } = new();
        public ConcurrentQueue<int> InitialQueryAttempts { get; } = new();
        public ConcurrentQueue<int> RealQueryAttempts { get; } = new();
        public ConcurrentQueue<AssetLibraryLoadStateSnapshot> Snapshots { get; } = new();

        public Task BeforeRepositoryInitializationAsync(int attempt, CancellationToken cancellationToken)
        {
            RepositoryInitializationAttempts.Enqueue(attempt);
            if (attempt != 1) return Task.CompletedTask;
            LoadingBarrierEntered.Set();
            return _releaseLoading.Task.WaitAsync(cancellationToken);
        }

        public async Task<AssetLibraryPageResult> ExecuteInitialQueryAsync(
            int attempt,
            Func<CancellationToken, Task<AssetLibraryPageResult>> realQuery,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InitialQueryAttempts.Enqueue(attempt);
            if (attempt == 1 && Interlocked.CompareExchange(ref _failureInjected, 1, 0) == 0)
            {
                var exception = new IOException("Known recoverable query failure for P1 acceptance.");
                exception.Data[AssetLibraryLoadStateExceptionMetadata.InjectionIdDataKey] = InjectionId;
                throw exception;
            }
            RealQueryAttempts.Enqueue(attempt);
            return await realQuery(cancellationToken);
        }

        public void RecordState(AssetLibraryLoadStateSnapshot snapshot)
        {
            Snapshots.Enqueue(snapshot);
            if (snapshot.Attempt == 2 && snapshot.Stage == "ready") ReadyAfterRetry.Set();
        }

        public void ReleaseLoadingBarrier() => _releaseLoading.TrySetResult();
    }
}
