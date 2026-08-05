using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Tethering;
using System.IO.Compression;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version230WatchFolderCoreTests
{
    [TestMethod]
    [DataRow("photo.jpg", TetherMediaKind.PreviewImage)]
    [DataRow("photo.jpeg", TetherMediaKind.PreviewImage)]
    [DataRow("photo.png", TetherMediaKind.PreviewImage)]
    [DataRow("photo.tif", TetherMediaKind.PreviewImage)]
    [DataRow("photo.tiff", TetherMediaKind.PreviewImage)]
    [DataRow("photo.arw", TetherMediaKind.Raw)]
    [DataRow("photo.cr2", TetherMediaKind.Raw)]
    [DataRow("photo.cr3", TetherMediaKind.Raw)]
    [DataRow("photo.dng", TetherMediaKind.Raw)]
    [DataRow("photo.nef", TetherMediaKind.Raw)]
    [DataRow("photo.nrw", TetherMediaKind.Raw)]
    [DataRow("photo.orf", TetherMediaKind.Raw)]
    [DataRow("photo.pef", TetherMediaKind.Raw)]
    [DataRow("photo.raf", TetherMediaKind.Raw)]
    [DataRow("photo.rw2", TetherMediaKind.Raw)]
    [DataRow("photo.srw", TetherMediaKind.Raw)]
    public void MediaKind_RecognizesConservativePreviewAndRawExtensions(string fileName, TetherMediaKind expected) =>
        Assert.AreEqual(expected, WatchFolderPathPolicy.MediaKind(fileName));

    [TestMethod]
    [DataRow("capture.tmp")]
    [DataRow("capture.part")]
    [DataRow("capture.crdownload")]
    [DataRow(".hidden.jpg")]
    [DataRow("~writing.jpg")]
    [DataRow("capture.exe")]
    public void CandidateFilter_RejectsTemporaryHiddenAndUnsupportedNames(string fileName)
    {
        using var temp = new TempDirectory();
        Assert.IsFalse(WatchFolderPathPolicy.IsCandidate(temp.Path, temp.Combine(fileName)));
    }

    [TestMethod]
    public void CandidateFilter_IsStrictlyTopLevel()
    {
        using var temp = new TempDirectory();
        var child = temp.CreateFile(Path.Combine("child", "photo.jpg"), [1]);
        Assert.IsFalse(WatchFolderPathPolicy.IsTopLevelFile(temp.Path, child));
        Assert.IsFalse(WatchFolderPathPolicy.IsCandidate(temp.Path, child));
    }

    [TestMethod]
    public void FileSystemWatcher_ExplicitlyDisablesSubdirectories()
    {
        using var temp = new TempDirectory();
        var watcher = new FileSystemWatcherEventSource(temp.Path);
        Assert.IsFalse(watcher.IncludeSubdirectories);
        watcher.Start(); watcher.Stop(); watcher.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [TestMethod]
    public async Task StabilityProbe_RequiresMultipleUnchangedSamplesAndReadableHeader()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("stable.jpg", Enumerable.Range(0, 128).Select(value => (byte)value).ToArray());
        var probe = new FileStabilityProbe(new(TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(8), TimeSpan.FromSeconds(1), 3, 16));
        var result = await probe.WaitForStableAsync(path);
        Assert.AreEqual(TetherStabilityState.Stable, result.State);
        Assert.AreEqual(128L, result.Length);
    }

    [TestMethod]
    public async Task StabilityProbe_SlowWriteMustSettleBeforeStable()
    {
        using var temp = new TempDirectory(); var path = temp.CreateFile("slow.nef", [1, 2]); var delayCalls = 0;
        async Task AdvanceWriter(TimeSpan _, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (Interlocked.Increment(ref delayCalls) == 1)
            {
                await using var writer = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous);
                await writer.WriteAsync(new byte[] { 3, 4, 5 }, token); await writer.FlushAsync(token);
            }
            await Task.Yield();
        }
        var probe = new FileStabilityProbe(new(TimeSpan.FromMilliseconds(1), TimeSpan.Zero, TimeSpan.FromSeconds(1), 3, 2), AdvanceWriter);
        var result = await probe.WaitForStableAsync(path);
        Assert.AreEqual(TetherStabilityState.Stable, result.State);
        Assert.AreEqual(5L, result.Length);
        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public async Task StabilityProbe_TimesOutWhenWriterKeepsExclusiveHandle()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("locked.jpg", [1, 2, 3]);
        await using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
        var probe = new FileStabilityProbe(new(TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(60), 2, 2));
        var result = await probe.WaitForStableAsync(path);
        Assert.AreEqual(TetherStabilityState.TimedOut, result.State);
        Assert.AreEqual(ErrorCodeCatalog.FileLocked, result.ErrorCode);
        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public async Task StabilityProbe_IsCancellableWithoutFixedSleep()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("candidate.jpg", [1]);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => new FileStabilityProbe().WaitForStableAsync(path, cancellation.Token));
    }

    [TestMethod]
    public async Task Pairing_JpgThenRaw_CreatesSymmetricRelationship()
    {
        using var setup = await SetupAsync();
        var (jpg, raw) = await AddPairAsync(setup, jpgFirst: true);
        await new TetherPairingService(setup.Assets).PairAsync(raw);
        var loadedJpg = await setup.Assets.GetAsync(jpg.Id); var loadedRaw = await setup.Assets.GetAsync(raw.Id);
        Assert.AreEqual(raw.Id, loadedJpg!.PairedAssetId);
        Assert.AreEqual(jpg.Id, loadedRaw!.PairedAssetId);
    }

    [TestMethod]
    public async Task Pairing_RawThenJpg_CreatesSymmetricRelationship()
    {
        using var setup = await SetupAsync();
        var (jpg, raw) = await AddPairAsync(setup, jpgFirst: false);
        await new TetherPairingService(setup.Assets).PairAsync(jpg);
        Assert.AreEqual(raw.Id, (await setup.Assets.GetAsync(jpg.Id))!.PairedAssetId);
        Assert.AreEqual(jpg.Id, (await setup.Assets.GetAsync(raw.Id))!.PairedAssetId);
    }

    [TestMethod]
    public async Task Pairing_AmbiguousRawCandidates_RequiresAttention()
    {
        using var setup = await SetupAsync();
        var jpg = await setup.AddStableAsync("IMG_0001.jpg", TetherMediaKind.PreviewImage, DateTimeOffset.UtcNow);
        await setup.AddStableAsync("IMG_0001.nef", TetherMediaKind.Raw, DateTimeOffset.UtcNow);
        await setup.AddStableAsync("IMG_0001.cr3", TetherMediaKind.Raw, DateTimeOffset.UtcNow);
        await new TetherPairingService(setup.Assets).PairAsync(jpg);
        var loaded = await setup.Assets.GetAsync(jpg.Id);
        Assert.AreEqual(TetherProcessingState.NeedsAttention, loaded!.ProcessingState);
        Assert.AreEqual(ErrorCodeCatalog.DuplicateConflict, loaded.LastErrorCode);
    }

    [TestMethod]
    public async Task Pairing_OutsideTimeWindow_DoesNotPair()
    {
        using var setup = await SetupAsync();
        var jpg = await setup.AddStableAsync("IMG_0002.jpg", TetherMediaKind.PreviewImage, DateTimeOffset.UtcNow.AddHours(-1));
        var raw = await setup.AddStableAsync("IMG_0002.nef", TetherMediaKind.Raw, DateTimeOffset.UtcNow);
        await new TetherPairingService(setup.Assets, TimeSpan.FromMinutes(5)).PairAsync(raw);
        Assert.IsNull((await setup.Assets.GetAsync(jpg.Id))!.PairedAssetId);
        Assert.IsNull((await setup.Assets.GetAsync(raw.Id))!.PairedAssetId);
    }

    [TestMethod]
    public async Task NoneRawPreviewDecoder_ReturnsSafePlaceholderContract()
    {
        var decoder = new NoneRawPreviewDecoder();
        var result = await decoder.DecodeAsync("unread.raw", "unused.jpg", 2048);
        Assert.AreEqual("None", decoder.Name);
        Assert.IsFalse(result.Success);
        Assert.IsNull(result.PreviewPath);
    }

    [TestMethod]
    public void WatchFolderCapabilities_DoNotClaimStageCFeatures()
    {
        var capabilities = new DefaultCameraCapabilityService().GetCapabilities(CameraProviderType.WatchFolder);
        Assert.IsTrue(capabilities.FileTransfer);
        Assert.IsFalse(capabilities.LiveView);
        Assert.IsFalse(capabilities.RemoteShutter);
        Assert.IsFalse(capabilities.CameraSettings);
    }

    [TestMethod]
    public async Task DefaultDiscovery_UsesNoneAndWatchFolderOnly()
    {
        var providers = await new DefaultCameraDiscoveryService().DiscoverAsync();
        CollectionAssert.AreEqual(new[] { CameraProviderType.None, CameraProviderType.WatchFolder }, providers.Select(item => item.ProviderType).ToArray());
    }

    [TestMethod]
    public void StartRequest_DefaultsAllImportAndCopyOptionsOff()
    {
        var request = new WatchFolderStartRequest(Path.GetFullPath("watch"));
        Assert.IsFalse(request.ImportExisting); Assert.IsFalse(request.CopyToProject); Assert.IsFalse(request.CopyToBackup); Assert.IsFalse(request.VerifySha256);
    }

    [TestMethod]
    [DataRow(@"C:\Clients\Alice\IMG_001.JPG")]
    [DataRow("FileName=IMG_001.JPG")]
    [DataRow("DisplayName=客户合同")]
    [DataRow("OptionalHash=0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    public void AuditSanitizer_RemovesPrivateFileIdentity(string message)
    {
        var sanitized = AuditLogService.Sanitize(message);
        Assert.DoesNotContain("IMG_001", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("客户合同", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("0123456789abcdef", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\Clients", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void TetherImplementation_HasNoSleepRecursionLocalhostOrVendorSdk()
    {
        var text = Text("src/RAWSelectionAssistant.Core/Services/Tethering/WatchFolderCameraAdapter.cs") + Text("src/RAWSelectionAssistant.Core/Services/Tethering/WatchFolderServices.cs");
        Assert.DoesNotContain("Thread.Sleep", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AllDirectories", text, StringComparison.Ordinal);
        Assert.DoesNotContain("localhost", text, StringComparison.OrdinalIgnoreCase);
        foreach (var vendor in new[] { "Canon", "Nikon", "Sony", "Edsdk", "MAID", "CameraRemoteSDK" }) Assert.DoesNotContain(vendor, text, StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(text, "SearchOption.TopDirectoryOnly");
        StringAssert.Contains(text, "BoundedChannelOptions");
        StringAssert.Contains(text, "QueueCapacity = 256");
        StringAssert.Contains(text, "FullMode = BoundedChannelFullMode.Wait");
    }

    [TestMethod]
    public void ReleaseUsesNoneProviderNoFakeCameraAndOrdinaryPagesKeepOptionalTetherDependency()
    {
        var app = Text("src/RAWSelectionAssistant/App.xaml.cs"); var settings = Text("src/RAWSelectionAssistant/appsettings.license.json"); var main = Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        StringAssert.Contains(settings, "\"Provider\": \"None\"");
        StringAssert.Contains(app, "allowMockProvider: false");
        StringAssert.Contains(main, "TetherCaptureViewModel? tetherPage = null");
        Assert.DoesNotContain("FakeCamera", app + main, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MockCamera", app + main, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void WatchFolderAddsNoServiceTrayBrowserUploadOrLocalhostRuntime()
    {
        var text = Text("src/RAWSelectionAssistant.Core/Services/Tethering/WatchFolderCameraAdapter.cs") + Text("src/RAWSelectionAssistant/App.xaml.cs");
        foreach (var forbidden in new[] { "ServiceBase", "BackgroundService", "NotifyIcon", "Process.Start", "HttpListener", "localhost", "UploadAsync" }) Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task DiagnosticsPackage_DoesNotIncludeProxyCacheFiles()
    {
        using var temp = new TempDirectory(); var logs = temp.Combine("logs"); var proxies = temp.Combine("cache", "TetherProxies"); Directory.CreateDirectory(logs); Directory.CreateDirectory(proxies);
        await File.WriteAllTextAsync(Path.Combine(logs, "app.log"), "safe"); await File.WriteAllBytesAsync(Path.Combine(proxies, "opaque.jpg"), [1, 2, 3]);
        var zip = temp.Combine("diagnostics.zip"); await new LogMaintenanceService(logs).ExportDiagnosticsAsync(zip);
        using var archive = ZipFile.OpenRead(zip); Assert.IsFalse(archive.Entries.Any(entry => entry.FullName.Contains("opaque.jpg", StringComparison.OrdinalIgnoreCase) || entry.FullName.Contains("TetherProxies", StringComparison.OrdinalIgnoreCase)));
    }

    private static async Task<(TetherAssetRecord Jpg, TetherAssetRecord Raw)> AddPairAsync(Setup setup, bool jpgFirst)
    {
        var time = DateTimeOffset.UtcNow;
        if (jpgFirst)
        {
            var jpg = await setup.AddStableAsync("IMG_1000.jpg", TetherMediaKind.PreviewImage, time);
            var raw = await setup.AddStableAsync("IMG_1000.nef", TetherMediaKind.Raw, time.AddSeconds(1));
            return (jpg, raw);
        }
        else
        {
            var raw = await setup.AddStableAsync("IMG_1000.nef", TetherMediaKind.Raw, time);
            var jpg = await setup.AddStableAsync("IMG_1000.jpg", TetherMediaKind.PreviewImage, time.AddSeconds(1));
            return (jpg, raw);
        }
    }

    private static async Task<Setup> SetupAsync()
    {
        var temp = new TempDirectory(); var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        Assert.IsTrue((await new DatabaseMigrator(database, new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync()).Success);
        var sessions = new SqliteTetherSessionRepository(database); var assets = new SqliteTetherAssetRepository(database);
        var watch = temp.Combine("watch"); Directory.CreateDirectory(watch); var now = DateTimeOffset.UtcNow;
        var session = new TetherSessionRecord(Guid.NewGuid(), null, CameraProviderType.WatchFolder, watch, WatchFolderPathPolicy.NormalizeDirectory(watch), TetherSessionState.Running, now, now, true, false, null, false, null, now);
        await sessions.AddAsync(session); return new(temp, database, session, assets);
    }

    private static string Text(string relative)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return File.ReadAllText(Path.Combine(directory.FullName, relative.Replace('/', Path.DirectorySeparatorChar)));
        throw new DirectoryNotFoundException();
    }

    private sealed class Setup(TempDirectory temp, PixelTartDatabase database, TetherSessionRecord session, SqliteTetherAssetRepository assets) : IDisposable
    {
        public TempDirectory Temp { get; } = temp; public PixelTartDatabase Database { get; } = database; public TetherSessionRecord Session { get; } = session; public SqliteTetherAssetRepository Assets { get; } = assets;
        public async Task<TetherAssetRecord> AddStableAsync(string fileName, TetherMediaKind kind, DateTimeOffset modified)
        {
            var path = Temp.CreateFile(Path.Combine("watch", fileName), [1, 2, 3]); File.SetLastWriteTimeUtc(path, modified.UtcDateTime); var info = new FileInfo(path);
            var asset = new TetherAssetRecord(Guid.NewGuid(), Session.Id, null, path, WatchFolderPathPolicy.NormalizePath(path), info.Name, info.Extension.ToLowerInvariant(), kind,
                info.Length, modified, modified, TetherStabilityState.Stable, TetherProcessingState.Ready, kind == TetherMediaKind.Raw ? TetherPreviewState.Placeholder : TetherPreviewState.None, modified, modified);
            return await Assets.UpsertDiscoveredAsync(asset);
        }
        public void Dispose() { SqliteTestIsolation.ClearPool(Database); Temp.Dispose(); }
    }
}
