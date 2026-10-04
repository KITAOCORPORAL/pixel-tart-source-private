using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class RuntimeCorrectionCoreTests
{
    [TestMethod]
    public async Task OrganizationRenameKeepsMembershipAndLegacyQueriesAcrossRestart()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var repo = setup.Repository;
        var group = await repo.SaveTagGroupAsync(new(Guid.NewGuid(), "组"));
        var tag = await repo.SaveTagAsync(new(Guid.NewGuid(), "标签", group.TagGroupId));
        var folder = await repo.SaveFolderAsync(new(Guid.NewGuid(), null, "文件夹"));
        await repo.AddTagsAsync([setup.A, setup.B], [tag.TagId]);
        await repo.AddToFoldersAsync([setup.A], [folder.FolderId]);
        var smart = await repo.SaveSmartFolderQueryDocumentAsync(new(Guid.NewGuid(), "引用"), new AssetQueryDocument {
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All, [AssetQueryNode.Rule(AssetQueryField.Tag, AssetQueryOperator.AnyOf, ["name:标签"]), AssetQueryNode.Rule(AssetQueryField.Folder, AssetQueryOperator.AnyOf, ["name:文件夹"])]) });
        const string name = "长中文名称 English & # / 100% 📷";
        await repo.RenameFolderAsync(folder.FolderId, name);
        await repo.RenameTagAsync(tag.TagId, name);
        await repo.SaveTagGroupAsync(group with { Name = name });
        await setup.RestartAsync(); repo = setup.Repository;
        Assert.AreEqual(name, (await repo.ListFoldersAsync()).Single(x=>x.FolderId==folder.FolderId).Name);
        Assert.AreEqual(name, (await repo.ListTagsAsync()).Single(x=>x.TagId==tag.TagId).Name);
        Assert.AreEqual(name, (await repo.ListTagGroupsAsync()).Single(x=>x.TagGroupId==group.TagGroupId).Name);
        Assert.AreEqual(2, (await repo.ListTagMembershipsAsync()).Count(x=>x.TagId==tag.TagId));
        var query = await repo.GetSmartFolderQueryDocumentAsync(smart.SmartFolderId);
        Assert.IsNotNull(query);
        var page = await repo.QueryAsync(new AssetLibraryQuery { Document = query.Document });
        CollectionAssert.AreEqual(new[]{setup.A}, page.Items.Select(x=>x.AssetId).ToArray());
    }

    [TestMethod]
    public async Task RenameRejectsEmptyAndDuplicateNamesWithinCorrectScope()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateAsync(); var repo=setup.Repository;
        var first=await repo.SaveFolderAsync(new(Guid.NewGuid(),null,"First"));
        var second=await repo.SaveFolderAsync(new(Guid.NewGuid(),null,"Second"));
        await Assert.ThrowsAsync<ArgumentException>(()=>repo.RenameFolderAsync(first.FolderId," "));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.RenameFolderAsync(first.FolderId,"SECOND"));
        var child=await repo.SaveFolderAsync(new(Guid.NewGuid(),second.FolderId,"Child"));
        await repo.RenameFolderAsync(child.FolderId,"First");
        var group=await repo.SaveTagGroupAsync(new(Guid.NewGuid(),"Group"));
        var other=await repo.SaveTagGroupAsync(new(Guid.NewGuid(),"Other"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.SaveTagGroupAsync(other with {Name="group"}));
        var tag=await repo.SaveTagAsync(new(Guid.NewGuid(),"Tag",group.TagGroupId));
        var tag2=await repo.SaveTagAsync(new(Guid.NewGuid(),"Other",group.TagGroupId));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.RenameTagAsync(tag2.TagId,"TAG"));
    }

    [TestMethod]
    public void LiveHistogramUsesSameColorMathAsFullAnalysisAndCancellation()
    {
        var bytes=Enumerable.Range(0,256*3).Select(i=>(byte)((i*31)%256)).ToArray();
        var pixels=new VisualPixelBuffer(256,1,bytes);
        var live=VisualAnalysisEngine.AnalyzeHistogram(pixels);
        var full=VisualAnalysisEngine.Analyze(new(Guid.NewGuid(),"histogram-fixture",pixels));
        CollectionAssert.AreEqual(full.HistogramR.ToArray(),live.R);
        CollectionAssert.AreEqual(full.HistogramG.ToArray(),live.G);
        CollectionAssert.AreEqual(full.HistogramB.ToArray(),live.B);
        CollectionAssert.AreEqual(full.HistogramLuma.ToArray(),live.Luma);
        Assert.AreEqual(1d,live.Zones.Sum,1e-10);
        CollectionAssert.AreEqual(full.ZoneDistribution.Ratios.ToArray(),live.Zones.Ratios.ToArray());
        Assert.Throws<OperationCanceledException>(()=>VisualAnalysisEngine.AnalyzeHistogram(pixels,new CancellationToken(true)));
    }

    [TestMethod]
    public void ImageAndCloudMembershipUsesBoundedOklabTolerance()
    {
        var pixels=new VisualPixelBuffer(4,1,new byte[]{255,0,0,250,2,2,0,255,0,0,0,255});
        var cloud=ColorSpaceProxyBuilder.Build(pixels,new ColorSpaceProxySettings(16));
        var index=ColorSpaceLinking.FindNearest(cloud,OklabColorSpace.FromSrgb(new(255,0,0)));
        CollectionAssert.AreEqual(new[]{0,1},ColorSpaceLinking.ToPixelMembership(cloud,pixels,index,.06).ToArray());
        Assert.IsEmpty(ColorSpaceLinking.ToPixelMembership(cloud,pixels,index,double.NaN));
    }
}
