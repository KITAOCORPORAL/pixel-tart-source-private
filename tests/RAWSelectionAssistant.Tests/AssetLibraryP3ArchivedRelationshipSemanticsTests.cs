using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryP3ArchivedRelationshipSemanticsTests
{
    [TestMethod]
    public async Task UntaggedAndUncategorizedConsiderOnlyActiveRelationships()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var folder = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "活动文件夹"));
        var group = await setup.Repository.SaveTagGroupAsync(new(Guid.NewGuid(), "活动标签组"));
        var tag = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "活动标签", group.TagGroupId));
        await setup.Repository.AddToFolderAsync([setup.A], folder.FolderId);
        await setup.Repository.AddTagsAsync([setup.A], [tag.TagId]);

        await AssertBooleanRuleAsync(setup, AssetQueryField.IsUncategorized, [setup.B, setup.C]);
        await AssertBooleanRuleAsync(setup, AssetQueryField.IsUntagged, [setup.B, setup.C]);

        await setup.Repository.SetFolderArchivedAsync(folder.FolderId, true);
        await setup.Repository.SetTagGroupArchivedAsync(group.TagGroupId, true);
        await AssertBooleanRuleAsync(setup, AssetQueryField.IsUncategorized, [setup.A, setup.B, setup.C]);
        await AssertBooleanRuleAsync(setup, AssetQueryField.IsUntagged, [setup.A, setup.B, setup.C]);

        var legacyUncategorized = await setup.Repository.QueryAsync(new(UncategorizedOnly: true, PageSize: 20));
        var legacyUntagged = await setup.Repository.QueryAsync(new(UntaggedOnly: true, PageSize: 20));
        CollectionAssert.AreEquivalent(new[] { setup.A, setup.B, setup.C }, legacyUncategorized.Items.Select(item => item.AssetId).ToArray());
        CollectionAssert.AreEquivalent(new[] { setup.A, setup.B, setup.C }, legacyUntagged.Items.Select(item => item.AssetId).ToArray());

        var database = new AssetLibraryDatabase(setup.DatabasePath);
        var visual = new SqliteVisualAssetQueryService(database, new SqliteAssetVisualAnalysisCache(database));
        var visualUncategorized = await visual.QueryAsync(new(new(UncategorizedOnly: true), new(), PageSize: 20));
        var visualUntagged = await visual.QueryAsync(new(new(UntaggedOnly: true), new(), PageSize: 20));
        CollectionAssert.AreEquivalent(new[] { setup.A, setup.B, setup.C }, visualUncategorized.Items.Select(item => item.Asset.AssetId).ToArray());
        CollectionAssert.AreEquivalent(new[] { setup.A, setup.B, setup.C }, visualUntagged.Items.Select(item => item.Asset.AssetId).ToArray());

        await setup.Repository.SetFolderArchivedAsync(folder.FolderId, false);
        await setup.Repository.SetTagGroupArchivedAsync(group.TagGroupId, false);
        await AssertBooleanRuleAsync(setup, AssetQueryField.IsUncategorized, [setup.B, setup.C]);
        await AssertBooleanRuleAsync(setup, AssetQueryField.IsUntagged, [setup.B, setup.C]);
    }

    [TestMethod]
    public async Task VisualQueryUsesCanonicalP3DocumentCandidateScopeInsteadOfDroppingIt()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var database = new AssetLibraryDatabase(setup.DatabasePath);
        var visual = new SqliteVisualAssetQueryService(database, new SqliteAssetVisualAnalysisCache(database));
        var scope = new AssetLibraryQuery(PageSize: 20)
        {
            Document = new AssetQueryDocument
            {
                Scope = AssetQueryScope.AllAssets,
                RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
                [
                    AssetQueryNode.Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, ["3"]),
                    AssetQueryNode.Rule(AssetQueryField.IsMissing, AssetQueryOperator.IsFalse)
                ])
            }
        };

        var page = await visual.QueryAsync(new(scope, new(), PageSize: 20));

        CollectionAssert.AreEqual(new[] { setup.B }, page.Items.Select(item => item.Asset.AssetId).ToArray());
        Assert.AreEqual(1, page.TotalCount);
    }

    private static async Task AssertBooleanRuleAsync(
        AssetLibraryP3TestSetup setup,
        AssetQueryField field,
        IReadOnlyCollection<Guid> expected)
    {
        var page = await setup.Repository.QueryAsync(new AssetLibraryQuery(PageSize: 20)
        {
            Document = new AssetQueryDocument
            {
                Scope = AssetQueryScope.AllAssets,
                RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
                [
                    AssetQueryNode.Rule(field, AssetQueryOperator.IsTrue)
                ])
            }
        });
        Assert.IsTrue(string.IsNullOrWhiteSpace(page.RegexError), page.RegexError);
        CollectionAssert.AreEquivalent(expected.ToArray(), page.Items.Select(item => item.AssetId).ToArray());
        Assert.AreEqual(expected.Count, page.TotalCount);
    }
}
