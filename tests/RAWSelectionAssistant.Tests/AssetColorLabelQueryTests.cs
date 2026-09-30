using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetColorLabelQueryTests
{
    [TestMethod]
    public async Task LabelAndRatingRemainIndependentAcrossQueryAndRestart()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var store = new AssetPresentationMetadataStore(new AssetLibraryDatabase(setup.DatabasePath));
        await store.SaveAsync([setup.A], color: "红");
        await setup.Repository.UpdateAssetMetadataAsync(setup.A, rating: 4);
        var query = new AssetLibraryQuery { Document = new() { RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [AssetQueryNode.Rule(AssetQueryField.ColorLabel, AssetQueryOperator.Equals, ["红"]),
             AssetQueryNode.Rule(AssetQueryField.Rating, AssetQueryOperator.Equals, ["4"])]) } };
        Assert.IsTrue(AssetQueryDocumentCodec.Normalize(query.Document).IsValid);
        Assert.AreEqual(setup.A, (await setup.Repository.QueryAsync(query)).Items.Single().AssetId);
        await setup.RestartAsync();
        var asset = (await setup.Repository.QueryAsync(query)).Items.Single();
        Assert.AreEqual(4, asset.Rating);
        Assert.AreEqual("红", asset.ColorLabel);
        await setup.Repository.UpdateAssetMetadataAsync(setup.A, rating: 0);
        Assert.HasCount(0, (await setup.Repository.QueryAsync(query)).Items);
        Assert.AreEqual("红", (await setup.Repository.GetAssetAsync(setup.A))!.ColorLabel);
        await store.SaveAsync([setup.A], color: "");
        await setup.RestartAsync();
        Assert.AreEqual("", (await setup.Repository.GetAssetAsync(setup.A))!.ColorLabel);
        Assert.AreEqual(0, (await setup.Repository.GetAssetAsync(setup.A))!.Rating);
    }
}
