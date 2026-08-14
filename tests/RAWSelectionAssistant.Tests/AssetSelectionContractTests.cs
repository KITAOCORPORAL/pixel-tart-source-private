using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetSelection;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetSelectionContractTests
{
    [TestMethod]
    public async Task SnapshotNeverExposesSourcePathAndProxyIsReadOnlyNotUploadReady()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-AssetSelection", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "测试 图像.jpg"); await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);
            await using var repository = new SqliteAssetLibraryRepository(Path.Combine(root, "library.db")); await repository.InitializeAsync(); await repository.ImportAsync([new(path, ComputeContentHash: true)]);
            var source = new AssetLibrarySelectionSource(repository);
            var page = await source.QueryAssetsAsync(new(PageSize: 500));
            Assert.AreEqual(AssetSelectionContract.ContractVersion, source.ContractVersion);
            Assert.AreEqual(200, new AssetSelectionQuery(PageSize: 500).EffectivePageSize);
            var snapshot = page.Items.Single();
            Assert.IsFalse(typeof(AssetSelectionSnapshot).GetProperties().Any(x => x.Name.Contains("SourcePath", StringComparison.Ordinal)));
            Assert.AreEqual("测试 图像", snapshot.OriginalStem);
            await using var proxy = await source.GetProxySourceAsync(snapshot.AssetId);
            Assert.IsNotNull(proxy); Assert.IsFalse(proxy.IsUploadReady); Assert.AreEqual(AssetProxySourceKind.RasterOriginal, proxy.SourceKind);
            Assert.AreEqual("测试 图像.jpg", proxy.SuggestedFileName); Assert.AreEqual(snapshot.FileSize, proxy.Length);
            var stream = await proxy.OpenReadAsync(CancellationToken.None);
            Assert.IsFalse(stream.CanWrite);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
