using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.OnlineSelection;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class OnlineSelectionProxySafetyClosureTests
{
    [TestMethod]
    [TestCategory("OnlineSelection")]
    public async Task GenerateAsync_PassesPrivateDefaultsFlushesAndPreservesSourceIdentity()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart.Selection.CoreProxyTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source.JPG");
            await File.WriteAllBytesAsync(source, [1, 2, 3, 4, 5]);
            var fixedWriteUtc = new DateTime(2026, 8, 11, 2, 3, 4, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(source, fixedWriteUtc);
            var beforeHash = Hash(source);
            var renderer = new RecordingRenderer();

            var result = await new SelectionProxyJpegService(renderer).GenerateAsync(source, Path.Combine(root, "proxies"));

            Assert.AreEqual(SelectionProxyState.Ready, result.State, result.Message);
            Assert.IsNotNull(renderer.Options);
            Assert.AreEqual(2560, renderer.Options.LongEdge);
            Assert.AreEqual(85, renderer.Options.Quality);
            Assert.IsTrue(renderer.Options.ConvertToSrgb);
            Assert.IsTrue(File.Exists(result.OutputPath));
            CollectionAssert.AreEqual(new byte[] { 9, 8, 7, 6 }, await File.ReadAllBytesAsync(result.OutputPath!));
            Assert.AreEqual(5, new FileInfo(source).Length);
            Assert.AreEqual(fixedWriteUtc, File.GetLastWriteTimeUtc(source));
            Assert.AreEqual(beforeHash, Hash(source));
            Assert.IsFalse(Directory.GetFiles(Path.Combine(root, "proxies"), ".selection-proxy-*.tmp").Any());
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed class RecordingRenderer : ISelectionProxyRenderer
    {
        public string Name => "recording";
        public SelectionProxyOptions? Options { get; private set; }

        public async Task RenderJpegAsync(
            string sourcePath,
            Stream destination,
            SelectionProxyOptions options,
            CancellationToken cancellationToken = default)
        {
            Options = options;
            await destination.WriteAsync(new byte[] { 9, 8, 7, 6 }, cancellationToken);
        }
    }
}
