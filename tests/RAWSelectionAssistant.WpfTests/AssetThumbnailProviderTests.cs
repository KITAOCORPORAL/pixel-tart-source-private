using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetThumbnailProviderTests
{
    [TestMethod]
    public async Task SharedProviderReturnsFrozenCachedThumbnailForSameFileVersion()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-thumbnail-provider", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "pixel.png");
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));

        try
        {
            var provider = new WpfAssetThumbnailProvider();
            var request = new AssetThumbnailRequest(path, 256);

            var first = await provider.GetAsync(request);
            var second = await provider.GetAsync(request);

            Assert.IsTrue(first.IsAvailable);
            Assert.AreSame(first.Bitmap, second.Bitmap);
            Assert.IsTrue(first.Bitmap!.IsFrozen);
            Assert.AreSame(typeof(WpfAssetThumbnailProvider), AsyncThumbnail.Provider.GetType());
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task SharedProviderInvalidatesWhenTheSourceFingerprintChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-thumbnail-provider", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "pixel.png");
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));

        try
        {
            var provider = new WpfAssetThumbnailProvider();
            var request = new AssetThumbnailRequest(path, 256);
            var first = await provider.GetAsync(request);

            File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(2));
            var refreshed = await provider.GetAsync(request);

            Assert.AreNotSame(first.Bitmap, refreshed.Bitmap);
            Assert.IsTrue(refreshed.Bitmap!.IsFrozen);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task SharedProviderReturnsDistinctMissingAndOfflinePlaceholderStates()
    {
        var provider = new WpfAssetThumbnailProvider();

        var missing = await provider.GetAsync(new AssetThumbnailRequest("does-not-exist.png", 256));
        var offline = await provider.GetAsync(new AssetThumbnailRequest(null, 256, AssetThumbnailState.Offline));

        Assert.AreEqual(AssetThumbnailState.Missing, missing.State);
        Assert.IsNull(missing.Bitmap);
        StringAssert.Contains(missing.PlaceholderMessage, "不存在");
        Assert.AreEqual(AssetThumbnailState.Offline, offline.State);
        Assert.IsNull(offline.Bitmap);
        StringAssert.Contains(offline.PlaceholderMessage, "离线");
    }

    [TestMethod]
    public async Task UnifiedPreviewCacheUsesBytesAndKeepsPurposeQualityDistinct()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-preview-provider", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "pixel.png");
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        try
        {
            IAssetPreviewProvider contract = new WpfAssetThumbnailProvider(memoryBudgetBytes: 4L * 1024 * 1024);
            var provider = (WpfAssetThumbnailProvider)contract;
            var gallery = await contract.GetAsync(new(path, AssetPreviewPurpose.GalleryThumbnail, AssetPreviewQuality.Balanced, 320));
            var loupe = await contract.GetAsync(new(path, AssetPreviewPurpose.QuickLoupe, AssetPreviewQuality.High, 1600));
            var viewer = await contract.GetAsync(new(path, AssetPreviewPurpose.ViewerPreview, AssetPreviewQuality.High, 2048));
            var original = await contract.GetAsync(new(path, AssetPreviewPurpose.Original, AssetPreviewQuality.Original));

            Assert.AreEqual(AssetPreviewPurpose.GalleryThumbnail, gallery.Purpose);
            Assert.AreEqual(AssetPreviewPurpose.QuickLoupe, loupe.Purpose);
            Assert.AreEqual(AssetPreviewPurpose.ViewerPreview, viewer.Purpose);
            Assert.AreEqual(AssetPreviewPurpose.Original, original.Purpose);
            Assert.IsTrue(new[] { gallery, loupe, viewer, original }.All(result => result.IsAvailable && result.Bitmap!.IsFrozen));
            Assert.IsLessThanOrEqualTo(provider.MemoryBudgetBytes, provider.CachedBytes);
        }
        finally { try { Directory.Delete(root, recursive: true); } catch { } }
    }

    [TestMethod]
    public async Task DiskPreviewCacheSurvivesProviderRestartAndServesOfflineSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-thumbnail-disk-cache", Guid.NewGuid().ToString("N"));
        var cache = Path.Combine(root, "previews"); Directory.CreateDirectory(root);
        var path = Path.Combine(root, "pixel.png");
        await File.WriteAllBytesAsync(path, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        try
        {
            var request = new AssetThumbnailRequest(path, 256, AssetThumbnailState.Available, Guid.NewGuid(), new string('a', 64), null, cache);
            var first = await new WpfAssetThumbnailProvider(cache).GetAsync(request);
            Assert.IsTrue(first.IsAvailable);
            Assert.IsTrue(Directory.EnumerateFiles(cache, "*.png").Any());
            File.Delete(path);
            var disconnectedPath = Path.Combine(root, "disconnected-volume", "pixel.png");
            var restarted = await new WpfAssetThumbnailProvider(cache).GetAsync(request with { SourcePath = disconnectedPath, KnownState = AssetThumbnailState.Offline });
            Assert.IsTrue(restarted.IsAvailable, "A content-addressed cached preview should remain visible after its source volume and path go offline.");
        }
        finally { try { Directory.Delete(root, recursive: true); } catch { } }
    }

    [TestMethod]
    public Task ScopedProviderKeepsConcurrentLibraryRequestsIsolated() => AssetLibraryP3PerformanceDiagnosticsTests.RunSta(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-thumbnail-scope", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "pixel.png");
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        try
        {
            var providerA = new RecordingThumbnailProvider();
            var providerB = new RecordingThumbnailProvider();
            var scopeA = new Grid();
            var scopeB = new Grid();
            AsyncThumbnail.SetScopedProvider(scopeA, providerA);
            AsyncThumbnail.SetScopedProvider(scopeB, providerB);
            var imageA = new Image();
            var imageB = new Image();
            scopeA.Children.Add(imageA);
            scopeB.Children.Add(imageB);

            AsyncThumbnail.SetSourcePath(imageA, path);
            AsyncThumbnail.SetSourcePath(imageB, path);
            await Task.WhenAll(providerA.Called.Task, providerB.Called.Task).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreEqual(1, providerA.CallCount);
            Assert.AreEqual(1, providerB.CallCount);
            Assert.AreNotSame(providerA, providerB);
            await Task.WhenAll(AsyncThumbnail.CancelAndDrainAsync(scopeA), AsyncThumbnail.CancelAndDrainAsync(scopeB));
        }
        finally { try { Directory.Delete(root, recursive: true); } catch { } }
    });

    [TestMethod]
    public Task CancelAndDrainPreventsOldLibraryWritebackWhileNewLibraryThumbnailCompletes() => AssetLibraryP3PerformanceDiagnosticsTests.RunSta(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-thumbnail-switch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "pixel.png");
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        var providerA = new ControlledThumbnailProvider(waitForRelease: true, blue: 16);
        var providerB = new ControlledThumbnailProvider(waitForRelease: false, blue: 224);
        var scopeA = new Grid(); var imageA = new Image(); scopeA.Children.Add(imageA); AsyncThumbnail.SetScopedProvider(scopeA, providerA);
        var scopeB = new Grid(); var imageB = new Image(); scopeB.Children.Add(imageB); AsyncThumbnail.SetScopedProvider(scopeB, providerB);
        try
        {
            AsyncThumbnail.SetSourcePath(imageA, path);
            await providerA.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var oldLibraryDrain = AsyncThumbnail.CancelAndDrainAsync(scopeA);
            Assert.IsFalse(oldLibraryDrain.IsCompleted, "The old library drain must wait for its in-flight thumbnail provider call.");

            AsyncThumbnail.SetSourcePath(imageB, path);
            await providerB.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitUntilAsync(() => ReferenceEquals(imageB.Source, providerB.Bitmap));
            Assert.IsNull(imageA.Source, "The old library published a thumbnail after its scope was cancelled.");

            providerA.Release.TrySetResult();
            await oldLibraryDrain;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            Assert.IsNull(imageA.Source, "A cancelled old-library request wrote back after the provider completed.");
            Assert.AreSame(providerB.Bitmap, imageB.Source, "The active library did not retain its own provider result.");
            Assert.AreEqual(1, providerA.CallCount);
            Assert.AreEqual(1, providerB.CallCount);
            Assert.AreEqual(0, AsyncThumbnail.PendingRequestCount);
        }
        finally
        {
            providerA.Release.TrySetResult();
            await Task.WhenAll(AsyncThumbnail.CancelAndDrainAsync(scopeA), AsyncThumbnail.CancelAndDrainAsync(scopeB));
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    });

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeout) Assert.Fail("Timed out waiting for the thumbnail publication.");
            await Task.Delay(10);
        }
    }

    private sealed class RecordingThumbnailProvider : IAssetThumbnailProvider
    {
        private int _callCount;
        public int CallCount => Volatile.Read(ref _callCount);
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<AssetThumbnailResult> GetAsync(AssetThumbnailRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            Called.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 0, 255 }, 4);
            bitmap.Freeze();
            return new(AssetThumbnailState.Available, bitmap);
        }
    }

    private sealed class ControlledThumbnailProvider(bool waitForRelease, byte blue) : IAssetThumbnailProvider
    {
        private int _callCount;
        public int CallCount => Volatile.Read(ref _callCount);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public BitmapSource Bitmap { get; } = CreateBitmap(blue);

        public async Task<AssetThumbnailResult> GetAsync(AssetThumbnailRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            Started.TrySetResult();
            if (waitForRelease) await Release.Task;
            return new(AssetThumbnailState.Available, Bitmap);
        }

        private static BitmapSource CreateBitmap(byte blue)
        {
            var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { blue, 0, 0, 255 }, 4);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
