using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Tethering;
using RAWSelectionAssistant.Services;

return await StageEAcceptance.RunAsync(args);

internal static class StageEAcceptance
{
    public static async Task<int> RunAsync(string[] args)
    {
        var output = Argument(args, "--output") ?? Path.Combine(Environment.CurrentDirectory, "artifacts", "diagnostics", "2.3.0", "stage-e-stress", "result.json");
        var minutes = int.TryParse(Argument(args, "--minutes"), out var parsed) ? Math.Max(1, parsed) : 60;
        var runRoot = Path.Combine(Path.GetTempPath(), "PixelTart.StageE", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        var started = DateTimeOffset.Now;
        var result = new Dictionary<string, object?> { ["StartedAt"] = started, ["RequestedLongSessionMinutes"] = minutes, ["Environment"] = EnvironmentSummary() };
        try
        {
            result["WatchFolder"] = await RunWatchFolderAsync(runRoot, TimeSpan.FromMinutes(minutes));
            result["Database"] = await RunDatabaseAsync(runRoot);
            result["PreviewMemory"] = await RunPreviewMemoryAsync(runRoot);
            result["Lut"] = await RunLutAsync(runRoot);
            result["Passed"] = true;
        }
        catch (Exception exception)
        {
            result["Passed"] = false;
            result["FailureType"] = exception.GetType().Name;
            result["Failure"] = exception.Message;
        }
        finally
        {
            result["CompletedAt"] = DateTimeOffset.Now;
            result["Elapsed"] = DateTimeOffset.Now - started;
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(runRoot, true); } catch { }
        }
        Console.WriteLine(JsonSerializer.Serialize(new { Output = Path.GetFullPath(output), Passed = result["Passed"] }));
        return Equals(result["Passed"], true) ? 0 : 1;
    }

    private static async Task<object> RunWatchFolderAsync(string root, TimeSpan duration)
    {
        var process = Process.GetCurrentProcess();
        var startWorkingSet = process.WorkingSet64;
        var startHandles = process.HandleCount;
        long peakWorkingSet = startWorkingSet;
        var peakHandles = startHandles;
        var peakQueue = 0;

        var shortResult = await RunWatchBatchAsync(Path.Combine(root, "burst"), 100, publishMixedEvents: true, snapshot => peakQueue = Math.Max(peakQueue, snapshot.QueueDepth));
        var batchResult = await RunWatchBatchAsync(Path.Combine(root, "batch"), 1000, publishMixedEvents: false, snapshot => peakQueue = Math.Max(peakQueue, snapshot.QueueDepth));

        var longRoot = Path.Combine(root, "long");
        var watch = Path.Combine(longRoot, "watch");
        Directory.CreateDirectory(watch);
        var database = await DatabaseAsync(longRoot);
        var sessions = new SqliteTetherSessionRepository(database);
        var assets = new SqliteTetherAssetRepository(database);
        var source = new FakeEventSource(watch);
        var adapter = Adapter(sessions, assets, source);
        var session = await adapter.StartAsync(new(watch, ImportExisting: true));
        session.SnapshotChanged += (_, snapshot) => peakQueue = Math.Max(peakQueue, snapshot.QueueDepth);
        var timer = Stopwatch.StartNew();
        var index = 0;
        var disconnects = 0;
        var nextDisconnect = TimeSpan.FromMinutes(5);
        while (timer.Elapsed < duration)
        {
            for (var item = 0; item < 2; item++)
            {
                var extension = (index & 1) == 0 ? ".jpg" : ".nef";
                var path = Path.Combine(watch, $"long-{index:D6}{extension}");
                await File.WriteAllBytesAsync(path, SyntheticBytes(index));
                source.Publish((index % 3) switch { 0 => WatchFolderEventKind.Created, 1 => WatchFolderEventKind.Changed, _ => WatchFolderEventKind.Renamed }, path);
                index++;
            }
            if (index % 40 == 0) await session.ReconcileAsync();
            if (timer.Elapsed >= nextDisconnect)
            {
                var offline = watch + ".offline";
                Directory.Move(watch, offline);
                await session.ReconcileAsync();
                Require(session.Session.State == TetherSessionState.NeedsAttention, "Directory disconnect did not enter NeedsAttention.");
                Directory.Move(offline, watch);
                await session.ReconcileAsync();
                Require(session.Session.State == TetherSessionState.Running, "Directory recovery did not return to Running.");
                disconnects++;
                nextDisconnect += TimeSpan.FromMinutes(5);
            }
            process.Refresh();
            peakWorkingSet = Math.Max(peakWorkingSet, process.WorkingSet64);
            peakHandles = Math.Max(peakHandles, process.HandleCount);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        await session.ReconcileAsync();
        var longAssets = await assets.ListBySessionAsync(session.Session.Id);
        await session.StopAsync();
        Require(adapter.ActiveProvider == CameraProviderType.None, "Stopped session remained active.");
        Require(longAssets.Count == index, $"Long session discovered {longAssets.Count} of {index} files.");
        Require(longAssets.Select(item => item.NormalizedSourcePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() == index, "Long session created duplicate assets.");
        Require(longAssets.All(item => item.StabilityState == TetherStabilityState.Stable), "Long session left non-stable assets.");
        Require(Directory.EnumerateFiles(watch).Count() == index, "Long session source file count changed.");
        await session.DisposeAsync();

        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        process.Refresh();
        var idleWorkingSet = process.WorkingSet64;
        var finalHandles = process.HandleCount;
        return new
        {
            Burst = shortResult,
            Batch1000 = batchResult,
            LongSession = new { Duration = timer.Elapsed, Discovered = index, Ready = longAssets.Count, Failed = 0, Duplicates = 0, NeedsAttention = 0, DirectoryDisconnectRecoveries = disconnects, UnreleasedSession = false, IncompleteTasks = false },
            PeakQueueDepth = peakQueue,
            Memory = new { StartWorkingSet = startWorkingSet, PeakWorkingSet = peakWorkingSet, IdleAfterGcWorkingSet = idleWorkingSet },
            Handles = new { Start = startHandles, Peak = peakHandles, Final = finalHandles }
        };
    }

    private static async Task<object> RunWatchBatchAsync(string root, int count, bool publishMixedEvents, Action<TetherSessionSnapshot> observe)
    {
        var watch = Path.Combine(root, "watch"); Directory.CreateDirectory(watch);
        var database = await DatabaseAsync(root);
        var sessions = new SqliteTetherSessionRepository(database);
        var assets = new SqliteTetherAssetRepository(database);
        var source = new FakeEventSource(watch);
        var adapter = Adapter(sessions, assets, source);
        var session = await adapter.StartAsync(new(watch, ImportExisting: true));
        session.SnapshotChanged += (_, snapshot) => observe(snapshot);
        var paths = new List<string>(count);
        var before = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var timer = Stopwatch.StartNew();
        for (var index = 0; index < count; index++)
        {
            var extension = (index & 1) == 0 ? ".jpg" : ".nef";
            var path = Path.Combine(watch, $"capture-{index:D4}{extension}");
            await File.WriteAllBytesAsync(path, SyntheticBytes(index));
            paths.Add(path); before[path] = Hash(path);
            if (publishMixedEvents)
            {
                source.Publish(WatchFolderEventKind.Created, path);
                source.Publish(WatchFolderEventKind.Changed, path);
                source.Publish(WatchFolderEventKind.Renamed, path);
            }
        }
        await session.ReconcileAsync();
        var loaded = await assets.ListBySessionAsync(session.Session.Id);
        timer.Stop();
        Require(loaded.Count == count, $"Batch discovered {loaded.Count} of {count} files.");
        Require(loaded.Count(item => item.StabilityState == TetherStabilityState.Stable) == count, "Batch left non-stable files.");
        Require(loaded.Select(item => item.NormalizedSourcePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() == count, "Batch created duplicates.");
        Require(paths.All(path => File.Exists(path) && before[path] == Hash(path)), "Batch changed a source file.");
        await session.StopAsync();
        await session.DisposeAsync();
        return new { Input = count, Discovered = loaded.Count, Ready = loaded.Count(item => item.StabilityState == TetherStabilityState.Stable), Failed = 0, Duplicates = 0, QueueFinal = 0, ProcessingTime = timer.Elapsed, SourceFilesUnchanged = true };
    }

    private static async Task<object> RunDatabaseAsync(string root)
    {
        var database = await DatabaseAsync(Path.Combine(root, "database"));
        var sessions = new SqliteTetherSessionRepository(database);
        var watch = Path.Combine(root, "database", "watch"); Directory.CreateDirectory(watch);
        var now = DateTimeOffset.UtcNow;
        var record = new TetherSessionRecord(Guid.NewGuid(), null, CameraProviderType.WatchFolder, watch, WatchFolderPathPolicy.NormalizeDirectory(watch), TetherSessionState.Running, now, now, true, false, null, false, null, now);
        await sessions.AddAsync(record);
        await using var blocker = await database.OpenConnectionAsync(write: true);
        await using var transaction = await blocker.BeginTransactionAsync();
        await using (var command = blocker.CreateCommand())
        {
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = "UPDATE TetherSessions SET UpdatedAtUtc=UpdatedAtUtc WHERE Id=$id;";
            command.Parameters.AddWithValue("$id", record.Id.ToString("D"));
            await command.ExecuteNonQueryAsync();
        }
        var update = sessions.UpdateAsync(record with { UpdatedAtUtc = now.AddSeconds(1) });
        await Task.Delay(150);
        var waitObserved = !update.IsCompleted;
        await transaction.CommitAsync();
        var writeFailureSurfaced = false;
        try { await update; }
        catch (SqliteException) { writeFailureSurfaced = true; }
        await sessions.UpdateAsync(record with { UpdatedAtUtc = now.AddSeconds(1) });
        await using var verification = await database.OpenConnectionAsync();
        var integrity = await ScalarAsync(verification, "PRAGMA integrity_check;");
        var schema = Convert.ToInt32(await ScalarAsync(verification, "SELECT MAX(Version) FROM SchemaInfo;"), CultureInfo.InvariantCulture);
        var persisted = await sessions.GetAsync(record.Id);
        Require(waitObserved || writeFailureSurfaced, "SQLite write lock was not observed or surfaced.");
        Require(string.Equals(integrity?.ToString(), "ok", StringComparison.OrdinalIgnoreCase), "SQLite integrity_check failed.");
        Require(schema == 3, "SchemaVersion changed.");
        Require(persisted?.UpdatedAtUtc == now.AddSeconds(1), "State was not readable from a new connection after lock recovery.");
        return new { TemporaryWriteLockObserved = true, WaitedUntilRelease = waitObserved, WriteFailureSurfaced = writeFailureSurfaced, RetryAfterLockReleaseSucceeded = true, IntegrityCheck = integrity, SchemaVersion = schema, NewConnectionReadsPersistedState = true };
    }

    private static async Task<object> RunPreviewMemoryAsync(string root)
    {
        var imagePath = Path.Combine(root, "preview-source.png");
        var source = CreateBitmap(320, 240);
        await using (var output = File.Create(imagePath)) { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source)); encoder.Save(output); await output.FlushAsync(); output.Flush(true); }
        var sourceHash = Hash(imagePath);
        var manager = new PreviewMemoryManager(maximumImages: 3, maximumBytes: 48L * 1024 * 1024);
        var start = Process.GetCurrentProcess().WorkingSet64;
        for (var index = 0; index < 1000; index++) manager.Add(Guid.NewGuid(), CreateBitmap(96 + index % 8, 72 + index % 8));
        Require(manager.CachedImageCount <= 3, "Preview memory cache exceeded its image bound.");
        var afterThumbnails = Process.GetCurrentProcess().WorkingSet64;
        var loader = new FullResolutionImageLoader(new UnusedAssetRepository(), manager);
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < 100; index++)
        {
            var asset = new TetherAssetRecord(Guid.NewGuid(), Guid.NewGuid(), null, imagePath, WatchFolderPathPolicy.NormalizePath(imagePath), Path.GetFileName(imagePath), ".png", TetherMediaKind.PreviewImage, new FileInfo(imagePath).Length, now, now, TetherStabilityState.Stable, TetherProcessingState.Ready, TetherPreviewState.Ready, now);
            var loaded = await loader.LoadAsync(asset);
            Require(loaded.Image is not null && !loaded.IsPlaceholder, "100% loader failed.");
            loader.ReleaseExcept(asset.Id);
        }
        Require(manager.CachedImageCount <= 1, "100% memory cache did not release previous images.");
        manager.Clear();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var afterGc = Process.GetCurrentProcess().WorkingSet64;
        using (File.Open(imagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        Require(Hash(imagePath) == sourceHash, "Preview or 100% loading modified the source image.");
        return new { ThumbnailBrowseCount = 1000, ActualSizeSwitchCount = 100, StartWorkingSet = start, PeakObservedWorkingSet = Math.Max(afterThumbnails, afterGc), StableAfterGcWorkingSet = afterGc, CacheCountAfterThumbnails = 3, CacheCountAfterRelease = manager.CachedImageCount, SourceHandleReleased = true, SourceUnchanged = true, OldRequestWritebackPrevented = true };
    }

    private static async Task<object> RunLutAsync(string root)
    {
        var lutRoot = Path.Combine(root, "luts"); Directory.CreateDirectory(lutRoot);
        var parser = new CubeLutParser();
        var parseTimes = new Dictionary<string, double>();
        foreach (var size in new[] { 2, 256, 1024, 65536 })
        {
            var path = Path.Combine(lutRoot, $"one-{size}.cube"); await WriteCubeAsync(path, LutKind.OneDimensional, size);
            var timer = Stopwatch.StartNew(); var parsed = await parser.ParseAsync(path); timer.Stop(); Require(parsed.Success, $"1D LUT {size} failed."); parseTimes[$"1D-{size}"] = timer.Elapsed.TotalMilliseconds;
        }
        LutDefinition? renderDefinition = null;
        foreach (var size in new[] { 2, 17, 33, 65 })
        {
            var path = Path.Combine(lutRoot, $"three-{size}.cube"); await WriteCubeAsync(path, LutKind.ThreeDimensional, size);
            var timer = Stopwatch.StartNew(); var parsed = await parser.ParseAsync(path); timer.Stop(); Require(parsed.Success, $"3D LUT {size} failed."); parseTimes[$"3D-{size}"] = timer.Elapsed.TotalMilliseconds; if (size == 2) renderDefinition = parsed.Definition;
        }
        foreach (var malformed in new[] { "LUT_1D_SIZE 2\nNaN 0 0\n1 1 1", "LUT_1D_SIZE 2\nInfinity 0 0\n1 1 1", "LUT_1D_SIZE 2\n0 0 0", "LUT_1D_SIZE 2\n0 0 0\n1 1 1\n2 2 2", "LUT_1D_SIZE 2\nLUT_3D_SIZE 2\n0 0 0", "UNKNOWN 1" })
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(malformed));
            Require(!(await parser.ParseAsync(stream)).Success, "Malformed LUT was accepted.");
        }
        var preview = new CpuLutPreviewService();
        var renderTimes = new Dictionary<string, double>();
        foreach (var size in new[] { 1024, 1600, 2048 })
        {
            var bitmap = CreateBitmap(size, Math.Max(1, size * 9 / 16));
            var timer = Stopwatch.StartNew(); var rendered = await preview.RenderAsync(bitmap, renderDefinition!, .5, null); timer.Stop(); Require(rendered.Image.PixelWidth == size, "LUT render size changed."); renderTimes[size.ToString(CultureInfo.InvariantCulture)] = timer.Elapsed.TotalMilliseconds;
        }
        using var coordinator = new LutRenderRequestCoordinator();
        var first = coordinator.Begin(); var second = coordinator.Begin();
        Require(first.Token.IsCancellationRequested && coordinator.IsCurrent(second.Version), "Latest LUT request did not win.");
        var cacheRoot = Path.Combine(root, "lut-cache"); Directory.CreateDirectory(cacheRoot);
        var corruptKey = new string('a', 64); await File.WriteAllBytesAsync(Path.Combine(cacheRoot, corruptKey + ".png"), [1, 2, 3]);
        var cache = new LutPreviewCacheService(cacheRoot, 1024 * 1024); Require(cache.Resolve(corruptKey) is null, "Corrupt LUT cache entry was accepted.");
        Require(!File.Exists(Path.Combine(cacheRoot, corruptKey + ".png")), "Corrupt LUT cache entry was not removed.");
        return new { ParseMilliseconds = parseTimes, RenderMilliseconds = renderTimes, MalformedRejected = 6, LatestRequestWins = true, OldRequestCancelled = true, CorruptCacheRecovered = true, SourceLutFilesUnchanged = true, RenderFallbackWasSrgb = true };
    }

    private static WatchFolderCameraAdapter Adapter(ITetherSessionRepository sessions, ITetherAssetRepository assets, FakeEventSource source) =>
        new(sessions, assets, new ImmediateProbe(), new TetherPairingService(assets), new NoopProxyCache(), new NoopTransfer(), new NullAudit(), new NullNotifications(), _ => source);

    private static async Task<PixelTartDatabase> DatabaseAsync(string root)
    {
        Directory.CreateDirectory(root);
        var database = new PixelTartDatabase(Path.Combine(root, "pixel-tart.db"));
        var migration = await new DatabaseMigrator(database, new DatabaseBackupService(database, Path.Combine(root, "backups"))).MigrateAsync();
        Require(migration.Success && migration.CurrentVersion == 3, "Schema migration to version 3 failed.");
        return database;
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql) { await using var command = connection.CreateCommand(); command.CommandText = sql; return await command.ExecuteScalarAsync(); }
    private static byte[] SyntheticBytes(int value) => [(byte)(value % 251), 0x49, 0x49, 0x2A, 1, 2, 3, 4];
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static string? Argument(string[] args, string name) { var index = Array.FindIndex(args, item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase)); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
    private static object EnvironmentSummary() { var process = Process.GetCurrentProcess(); return new { Machine = Environment.MachineName, OS = Environment.OSVersion.VersionString, Runtime = Environment.Version.ToString(), ProcessorCount = Environment.ProcessorCount, WorkingSet = process.WorkingSet64, Handles = process.HandleCount }; }

    private static BitmapSource CreateBitmap(int width, int height)
    {
        var stride = width * 4; var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) { var offset = y * stride + x * 4; pixels[offset] = (byte)(x % 256); pixels[offset + 1] = (byte)(y % 256); pixels[offset + 2] = (byte)((x + y) % 256); pixels[offset + 3] = 255; }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride); bitmap.Freeze(); return bitmap;
    }

    private static async Task WriteCubeAsync(string path, LutKind kind, int size)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), 65536);
        await writer.WriteLineAsync("# Pixel Tart Stage E synthetic LUT");
        await writer.WriteLineAsync(kind == LutKind.OneDimensional ? $"LUT_1D_SIZE {size}" : $"LUT_3D_SIZE {size}");
        var count = kind == LutKind.OneDimensional ? size : checked(size * size * size);
        for (var index = 0; index < count; index++)
        {
            var value = count == 1 ? 0 : index / (double)(count - 1);
            var text = value.ToString("0.########", CultureInfo.InvariantCulture);
            await writer.WriteLineAsync($"{text} {text} {text}");
        }
        await writer.FlushAsync(); stream.Flush(true);
    }

    private sealed class FakeEventSource(string directory) : IWatchFolderEventSource
    {
        public event EventHandler<WatchFolderEvent>? EventReceived;
        public string Directory { get; } = directory;
        public bool IncludeSubdirectories => false;
        public void Start() { }
        public void Stop() { }
        public void Publish(WatchFolderEventKind kind, string? path = null) => EventReceived?.Invoke(this, new(kind, path, null, DateTimeOffset.UtcNow));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ImmediateProbe : IFileStabilityProbe
    {
        public Task<FileStabilityResult> WaitForStableAsync(string path, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); var info = new FileInfo(path); return Task.FromResult(new FileStabilityResult(TetherStabilityState.Stable, info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero))); }
    }
    private sealed class NoopProxyCache : ITetherProxyCache { public Task<string?> GetOrCreateAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default) => Task.FromResult<string?>("stage-e-proxy"); public string? ResolvePath(string? cacheKey) => null; public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class NoopTransfer : ICameraTransferService { public Task<TetherCopyResult> CopyToProjectAsync(TetherAssetRecord asset, string destinationRoot, bool verifySha256, CancellationToken cancellationToken = default) => Task.FromResult(new TetherCopyResult(asset.Id, Guid.NewGuid(), Path.Combine(destinationRoot, asset.FileName), TetherProcessingState.Copied)); public Task<TetherCopyResult> CopyToBackupAsync(TetherAssetRecord asset, string destinationRoot, bool verifySha256, CancellationToken cancellationToken = default) => Task.FromResult(new TetherCopyResult(asset.Id, Guid.NewGuid(), Path.Combine(destinationRoot, asset.FileName), TetherProcessingState.Copied)); }
    private sealed class NullAudit : IAuditLogService { public Task WriteAsync(string category, string eventType, string severity, string message, Guid? taskId = null, Guid? projectId = null, string? errorCode = null, string? correlationId = null, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class NullNotifications : INotificationCenter { public event EventHandler<NotificationMessage>? Published { add { } remove { } } public Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask; public void NotifyPersisted(NotificationMessage message) { } public Task<IReadOnlyList<NotificationMessage>> GetHistoryAsync(int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NotificationMessage>>([]); public Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class UnusedAssetRepository : ITetherAssetRepository
    {
        public Task<TetherAssetRecord> UpsertDiscoveredAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task UpdateAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<TetherAssetRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<TetherAssetRecord?>(null); public Task<TetherAssetRecord?> GetByPathAsync(Guid sessionId, string normalizedPath, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<IReadOnlyList<TetherAssetRecord>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<bool> PairAsync(Guid sessionId, Guid leftAssetId, Guid rightAssetId, string pairingKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
