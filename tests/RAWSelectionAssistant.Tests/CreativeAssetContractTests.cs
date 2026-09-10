using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class CreativeAssetContractTests
{
    [TestMethod]
    public void InspirationCollectionReferencesPortableAssetIdentityWithoutAssetItem()
    {
        var reference = StableReference();
        var collection = new InspirationCollection(Guid.NewGuid(), "电影感灯光", [reference], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        Assert.AreSame(reference, collection.Assets.Single());
        Assert.IsFalse(typeof(InspirationCollection).GetProperties().Any(property => property.PropertyType.Name.Contains("AssetItem", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void MoodboardAndPlanningReferencesSupportLibraryAndExternalSources()
    {
        var librarySource = new CreativeAssetReference(CreativeAssetSourceType.AssetLibrary, StableReference());
        var externalSource = new CreativeAssetReference(CreativeAssetSourceType.ClientUpload, externalReference: "client-upload:brief/hero");
        var thumbnail = new AssetThumbnailReference(librarySource, 320);
        var moodboard = new MoodboardItem(Guid.NewGuid(), librarySource, thumbnail, new(12, 24), 1.25, -3, "主视觉");
        var planning = new ProjectPlanningReference(Guid.NewGuid(), externalSource, new(externalSource), "客户资料");

        Assert.AreEqual(320, moodboard.Thumbnail.EffectivePixelWidth);
        Assert.AreEqual(512, (thumbnail with { PreferredPixelWidth = 4096 }).EffectivePixelWidth);
        Assert.AreEqual(CreativeAssetSourceType.ClientUpload, planning.Source.SourceType);
        Assert.IsNull(planning.Source.StableReference);
    }

    [TestMethod]
    public void SourceKindsRejectAmbiguousOrUnresolvableReferences()
    {
        var stable = StableReference();

        Assert.ThrowsExactly<ArgumentException>(() =>
            new CreativeAssetReference(CreativeAssetSourceType.AssetLibrary, stable, "external:duplicate"));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CreativeAssetReference(CreativeAssetSourceType.ClientUpload, stable));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CreativeAssetReference(CreativeAssetSourceType.ExternalImage));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CreativeAssetReference(CreativeAssetSourceType.InspirationReference));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CreativeAssetReference(CreativeAssetSourceType.InspirationReference, stable, "inspiration:item"));
    }

    [TestMethod]
    public void ProjectAssetLinkKeepsCalendarConnectionOutsideAssetItem()
    {
        var projectId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var link = new ProjectAssetReferenceLink(projectId, bookingId, StableReference(), "拍摄参考", DateTimeOffset.UtcNow, "calendar");

        Assert.AreEqual(projectId, link.ProjectId);
        Assert.AreEqual(bookingId, link.BookingId);
        Assert.ThrowsExactly<ArgumentException>(() => new CreativeAssetReference(CreativeAssetSourceType.AssetLibrary));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ProjectAssetReferenceLink(Guid.Empty, bookingId, StableReference(), null, DateTimeOffset.UtcNow));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ProjectAssetReferenceLink(projectId, Guid.Empty, StableReference(), null, DateTimeOffset.UtcNow));
    }

    private static AssetLibraryStableReference StableReference() =>
        new(Guid.NewGuid(), Guid.NewGuid(), new string('a', 64));
}
