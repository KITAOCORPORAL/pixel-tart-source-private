using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Presets;
using RAWSelectionAssistant.Core.Services.Publishing;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class PublishingTiffAndXmpBatchTests
{
    [TestMethod]
    public void TiffPublishingDestinationUsesTifExtension()
    {
        var source = Path.Combine(Path.GetTempPath(), "photo.jpg"); var destination = Path.Combine(Path.GetTempPath(), "out");
        var path = PublishingExportService.ResolveDestination(source, destination, new(OutputFormat: PublishingOutputFormat.Tiff));
        StringAssert.EndsWith(path, ".tif");
    }

    [TestMethod]
    public async Task XmpImportProcessesFilesAndReportsProgress()
    {
        var root = Path.Combine(Path.GetTempPath(), "pixel-tart-xmp-batch-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var files = Enumerable.Range(1, 3).Select(index => { var path = Path.Combine(root, $"{index}.xmp"); File.WriteAllText(path, $"<root Exposure2012=\"{index}\" />"); return path; }).ToArray();
            var progress = new List<AdobeXmpImportProgress>(); var imported = await new AdobeXmpImportService(new AdobeXmpPresetStore(Path.Combine(root, "store"))).ImportAsync(files, new SynchronousProgress<AdobeXmpImportProgress>(progress.Add));
            Assert.HasCount(3, imported); Assert.HasCount(3, progress); Assert.AreEqual(3, progress[^1].Completed);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class SynchronousProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
