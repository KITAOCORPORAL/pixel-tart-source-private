using System.IO;
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

            Assert.AreSame(first, second);
            Assert.IsTrue(first.IsFrozen);
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

            Assert.AreNotSame(first, refreshed);
            Assert.IsTrue(refreshed.IsFrozen);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
