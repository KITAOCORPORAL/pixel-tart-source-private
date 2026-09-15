using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetFullPreviewResolutionTests
{
    [TestMethod]
    public async Task ViewerReplacesBoundedFirstFrameWithOriginalPixels()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pixel-tart-full-preview-{Guid.NewGuid():N}.png");
        try
        {
            WriteImage(path, 1600, 900);
            await RunSta(async () =>
            {
                var provider = new RecordingThumbnailProvider();
                var viewer = new AssetViewerWindow([path], 0, provider)
                {
                    WindowState = WindowState.Normal,
                    Width = 720,
                    Height = 480,
                    ShowActivated = false,
                    ShowInTaskbar = false
                };
                viewer.Show();
                await WaitUntilAsync(() => viewer.IsShowingFullResolution);
                Assert.AreEqual(512, provider.RequestedWidth);
                Assert.AreEqual(1600, viewer.DisplayedPixelWidth);
                Assert.AreEqual(900, viewer.DisplayedPixelHeight);
                StringAssert.Contains(viewer.Title, "1600 × 900");
                viewer.Close();
            });
        }
        finally { try { File.Delete(path); } catch (IOException) { } }
    }

    private static void WriteImage(string path, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index += 4) { pixels[index] = 35; pixels[index + 1] = 115; pixels[index + 2] = 170; pixels[index + 3] = 255; }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline) Assert.Fail("Full-resolution stage did not complete.");
            await Task.Delay(20);
        }
    }

    private static Task RunSta(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            try
            {
                var operation = action();
                _ = operation.ContinueWith(_ => dispatcher.BeginInvokeShutdown(DispatcherPriority.Background), TaskScheduler.Default);
                Dispatcher.Run();
                operation.GetAwaiter().GetResult();
                completion.SetResult();
            }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private sealed class RecordingThumbnailProvider : IAssetThumbnailProvider
    {
        public int RequestedWidth { get; private set; }
        public Task<AssetThumbnailResult> GetAsync(AssetThumbnailRequest request, CancellationToken cancellationToken = default)
        {
            RequestedWidth = request.DecodePixelWidth;
            var bitmap = BitmapSource.Create(64, 36, 96, 96, PixelFormats.Bgra32, null, new byte[64 * 36 * 4], 64 * 4);
            bitmap.Freeze();
            return Task.FromResult(new AssetThumbnailResult(AssetThumbnailState.Available, bitmap));
        }
    }
}
