using RAWSelectionAssistant.Core.Services.AssetSelection;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetSelectionContractTests
{
    [TestMethod]
    public async Task FakeAdapterPagesAndReturnsReadOnlyNonUploadReadySource()
    {
        var firstId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var snapshots = new[]
        {
            Snapshot(firstId, "Alpha.JPG", 4),
            Snapshot(secondId, "Beta.JPG", 2)
        };
        var adapter = new FakeAssetSelectionSource(snapshots, new Dictionary<Guid, byte[]> { [firstId] = [0xff, 0xd8, 0xff, 0xd9] });

        var page = await adapter.QueryAssetsAsync(new AssetSelectionQuery(MinimumRating: 3, PageSize: 2000));
        Assert.AreEqual(AssetSelectionContract.ContractVersion, adapter.ContractVersion);
        Assert.AreEqual(200, new AssetSelectionQuery(PageSize: 2000).EffectivePageSize);
        Assert.HasCount(1, page.Items);
        Assert.AreEqual(firstId, page.Items[0].AssetId);
        Assert.IsNull(typeof(AssetSelectionSnapshot).GetProperty("SourcePath"));
        await using var proxy = await adapter.GetProxySourceAsync(firstId);
        Assert.IsNotNull(proxy);
        if (proxy.IsUploadReady) Assert.Fail("Asset-selection proxy sources must never be upload-ready.");
        await using var stream = await proxy.OpenReadAsync();
        Assert.IsFalse(stream.CanWrite);
    }

    [TestMethod]
    public void ContractShapeHasStableFingerprint()
    {
        var methods = typeof(IAssetSelectionSource).GetMethods()
            .Select(method => method.Name)
            .OrderBy(name => name)
            .ToArray();
        CollectionAssert.AreEqual(new[] { "get_ContractVersion", "GetAssetSnapshotAsync", "GetProxySourceAsync", "QueryAssetsAsync" }, methods);
        StringAssert.Matches(AssetSelectionContract.ContractVersion, new System.Text.RegularExpressions.Regex("^pixel-tart-asset-selection/v1$"));
    }

    private static AssetSelectionSnapshot Snapshot(Guid id, string name, int rating) => new(
        id, name, name, Path.GetFileNameWithoutExtension(name), Path.GetExtension(name), "image/jpeg", 12,
        "fingerprint", 10, 10, "Square", null, rating, false);
}
