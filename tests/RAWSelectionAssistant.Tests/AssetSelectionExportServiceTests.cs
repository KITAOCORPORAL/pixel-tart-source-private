using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetSelectionExportServiceTests
{
    [TestMethod]
    public async Task FilesAndMetadataExportNeverOverwriteOrMutateReferenceSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-AssetExport", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source.jpg"); await File.WriteAllBytesAsync(source, [1, 2, 3]); var hash = SHA256.HashData(await File.ReadAllBytesAsync(source));
            var asset = new AssetItem(Guid.NewGuid(), source, "source.jpg", ".jpg", "image/jpeg", 3, null, null, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            var output = Path.Combine(root, "out"); Directory.CreateDirectory(output); await File.WriteAllTextAsync(Path.Combine(output, "source.jpg"), "existing");
            var service = new AssetSelectionExportService(); var result = await service.ExportFilesAsync([asset], output, false);
            Assert.AreEqual("source (2).jpg", Path.GetFileName(result.OutputPaths.Single()));
            Assert.AreEqual("existing", await File.ReadAllTextAsync(Path.Combine(output, "source.jpg")));
            var csv = Path.Combine(output, "metadata.csv"); await service.ExportMetadataCsvAsync([asset], csv); StringAssert.Contains(await File.ReadAllTextAsync(csv), asset.AssetId.ToString("D"));
            await Assert.ThrowsAsync<IOException>(() => service.ExportMetadataCsvAsync([asset], csv));
            CollectionAssert.AreEqual(hash, SHA256.HashData(await File.ReadAllBytesAsync(source)));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
