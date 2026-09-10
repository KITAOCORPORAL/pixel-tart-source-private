using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class CreativeAssetContractTests
{
    [TestMethod]
    public void InspirationCollectionReferencesPortableAssetIdentityWithoutAssetItem()
    {
        var collectionId = Guid.NewGuid();
        var stable = StableReference();
        var source = new CreativeAssetReference(CreativeAssetSourceType.AssetLibrary, AssetOrigin.ExternalReference, stable);
        var reference = new InspirationReference(stable, new(source), DateTimeOffset.UtcNow, collectionId);
        var collection = new InspirationCollection(collectionId, "电影感灯光", [reference], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        Assert.AreSame(reference, collection.Items.Single());
        Assert.AreEqual(stable.LibraryId, collection.Items.Single().StableReference.LibraryId);
        Assert.IsFalse(typeof(InspirationReference).GetProperties().Any(property => property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(typeof(InspirationCollection).GetProperties().Any(property => property.PropertyType.Name.Contains("AssetItem", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void MoodboardAndPlanningReferencesSupportLibraryAndExternalSources()
    {
        var librarySource = new CreativeAssetReference(CreativeAssetSourceType.AssetLibrary, AssetOrigin.SelfCreated, StableReference());
        var externalSource = new CreativeAssetReference(CreativeAssetSourceType.ClientUpload, AssetOrigin.ClientReference, externalReference: "client-upload:brief/hero");
        var thumbnail = new AssetThumbnailReference(librarySource, 320);
        var groupId = Guid.NewGuid();
        var moodboard = new MoodboardItem(Guid.NewGuid(), librarySource, thumbnail, new(12, 24), 1.25, -3, "主视觉", groupId, true, 4);
        var text = MoodboardItem.CreateText(Guid.NewGuid(), "保持冷调", new(20, 40), order: 5);
        var projectId = Guid.NewGuid();
        var planning = new ProjectPlanningReference(Guid.NewGuid(), projectId, externalSource, new(externalSource), "客户资料");

        Assert.AreEqual(320, moodboard.Thumbnail!.EffectivePixelWidth);
        Assert.AreEqual(MoodboardContentType.AssetReference, moodboard.ContentType);
        Assert.AreEqual(groupId, moodboard.GroupId);
        Assert.IsTrue(moodboard.IsLocked);
        Assert.AreEqual(MoodboardContentType.TextNote, text.ContentType);
        Assert.AreEqual("保持冷调", text.TextNote);
        Assert.AreEqual(512, (thumbnail with { PreferredPixelWidth = 4096 }).EffectivePixelWidth);
        Assert.AreEqual(projectId, planning.ProjectId);
        Assert.AreEqual(CreativeAssetSourceType.ClientUpload, planning.Source.SourceType);
        Assert.IsNull(planning.Source.StableReference);
    }

    [TestMethod]
    public void SourceKindsRejectAmbiguousOrUnresolvableReferences()
    {
        var stable = StableReference();

        Assert.ThrowsExactly<ArgumentException>(() =>
            new CreativeAssetReference(CreativeAssetSourceType.AssetLibrary, AssetOrigin.SelfCreated, stable, "external:duplicate"));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CreativeAssetReference(CreativeAssetSourceType.ClientUpload, AssetOrigin.ClientReference, stable));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CreativeAssetReference(CreativeAssetSourceType.ExternalImage, AssetOrigin.ExternalReference));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CreativeAssetReference(CreativeAssetSourceType.InspirationReference, AssetOrigin.ExternalReference));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CreativeAssetReference(CreativeAssetSourceType.InspirationReference, AssetOrigin.ExternalReference, stable, "inspiration:item"));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CreativeAssetReference(CreativeAssetSourceType.ClientUpload, AssetOrigin.GeneratedReference, externalReference: "client:item"));
    }

    [TestMethod]
    public void ProjectAssetLinkKeepsCalendarConnectionOutsideAssetItem()
    {
        var projectId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var link = new ProjectAssetReferenceLink(projectId, bookingId, StableReference(), "拍摄参考", DateTimeOffset.UtcNow, "calendar");

        Assert.AreEqual(projectId, link.ProjectId);
        Assert.AreEqual(bookingId, link.BookingId);
        Assert.ThrowsExactly<ArgumentException>(() => new CreativeAssetReference(CreativeAssetSourceType.AssetLibrary, AssetOrigin.SelfCreated));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ProjectAssetReferenceLink(Guid.Empty, bookingId, StableReference(), null, DateTimeOffset.UtcNow));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ProjectAssetReferenceLink(projectId, Guid.Empty, StableReference(), null, DateTimeOffset.UtcNow));
    }

    private static AssetLibraryStableReference StableReference() =>
        new(Guid.NewGuid(), Guid.NewGuid(), new string('a', 64));
}
