using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AtomicTiffWriterTests
{
    [TestMethod]
    public async Task WritesValidatedTiffAndLeavesNoTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pixel-tart-{Guid.NewGuid():N}.tiff");
        try
        {
            var result = await AtomicTiffWriter.WriteRgb48Async(path, new HighBitDepthImageBuffer(1, 1, new ushort[] { 1, 2, 3 }));
            Assert.AreEqual(1, result.Width); Assert.IsTrue(File.Exists(path)); Assert.IsGreaterThan(0, new FileInfo(path).Length);
            Assert.IsEmpty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.*.tmp"));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [TestMethod]
    public async Task CancellationDoesNotPublishFinalFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pixel-tart-{Guid.NewGuid():N}.tiff"); using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => AtomicTiffWriter.WriteRgb48Async(path, new HighBitDepthImageBuffer(1, 1, new ushort[] { 1, 2, 3 }), token: cts.Token));
        Assert.IsFalse(File.Exists(path));
    }
}
