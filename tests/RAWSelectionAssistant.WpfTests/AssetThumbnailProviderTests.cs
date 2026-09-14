using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
            var restarted = await new WpfAssetThumbnailProvider(cache).GetAsync(request with { KnownState = AssetThumbnailState.Missing });
            Assert.IsTrue(restarted.IsAvailable, "A cached preview should remain visible when the reference source is offline.");
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
}
