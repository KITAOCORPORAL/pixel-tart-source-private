using RAWSelectionAssistant.Core.Services.Presets;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class PresetBrowserCatalogTests
{
    [TestMethod]
    public void QuerySupportsSearchProviderFavoritesAndRecent()
    {
        var catalog = new PresetBrowserCatalog();
        catalog.Upsert(new("a", "暖光", PresetProviderKind.AdobeXmp, "人像"));
        catalog.Upsert(new("b", "冷调", PresetProviderKind.User, "风景"));
        catalog.SetFavorite("a", true); catalog.MarkUsed("a");
        Assert.AreEqual("a", catalog.Query(new(FavoritesOnly: true)).Single().Id);
        Assert.AreEqual("a", catalog.Query(new(Search: "暖")).Single().Id);
        Assert.AreEqual("a", catalog.Query(new(RecentOnly: true)).Single().Id);
    }

    [TestMethod]
    public async Task PreviewCoordinatorCancelsStaleRequest()
    {
        var coordinator = new PresetPreviewCoordinator();
        var first = coordinator.PreviewLatestAsync(async token => { await Task.Delay(100, token); return "first"; });
        await Task.Delay(10);
        var second = coordinator.PreviewLatestAsync(async token => { await Task.Delay(1, token); return "second"; });
        Assert.AreEqual("second", await second); Assert.IsNull(await first);
    }
}
