using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class BatchThreeIdentityTests
{
    [TestMethod]
    public async Task IndependentConnectionsRejectStaleAnnotationAndRelocationRejectsSameNameWrongContent()
    {
        using var temp=new TempDirectory();var library=await new AssetLibraryContainerService().CreateAsync(temp.Combine("test.ptlibrary"),"test");
        var file=temp.Combine("original.jpg");await File.WriteAllBytesAsync(file,[1,2,3]);
        await using var repo=new SqliteAssetLibraryRepository(library.DatabasePath);var id=(await repo.ImportWithIdentitiesAsync([new(file,ComputeContentHash:true)])).Items.Single().AssetId;
        var a=new PhotographerAnnotationStore(new AssetLibraryDatabase(library.DatabasePath));var b=new PhotographerAnnotationStore(new AssetLibraryDatabase(library.DatabasePath));
        var old=await a.ReadAsync(id);Assert.IsTrue((await a.CompareExchangeAsync(old,old with{Rating=4,Note="first"})).Applied);
        Assert.IsFalse((await b.CompareExchangeAsync(old,old with{Rating=2,Note="stale"})).Applied);
        var current=await a.ReadAsync(id);await a.CompareExchangeAsync(current,current with{Rating=old.Rating,Note=old.Note});
        Assert.IsFalse((await b.CompareExchangeAsync(old,old with{Rating=3})).Applied,"ABA must not revive old revision");
        var parallel=await a.ReadAsync(id);var outcomes=await Task.WhenAll(Task.Run(()=>a.CompareExchangeAsync(parallel,parallel with{Rating=1})),Task.Run(()=>b.CompareExchangeAsync(parallel,parallel with{Rating=5})));
        Assert.AreEqual(1,outcomes.Count(x=>x.Applied));
        var asset=await repo.GetAssetAsync(id);var moved=temp.Combine("moved.jpg");File.Move(file,moved);
        var wrong=temp.Combine("original.jpg");await File.WriteAllBytesAsync(wrong,[9,9]);var relocate=new AssetRelocationService(library);
        Assert.AreEqual(AssetResolutionStatus.Changed,(await relocate.RelocateAsync(new(library.LibraryId,id,asset!.ContentHash),wrong)).Status);
        Assert.AreEqual(AssetResolutionStatus.Available,(await relocate.RelocateAsync(new(library.LibraryId,id,asset.ContentHash),moved)).Status);
        Assert.AreEqual(id,(await repo.GetAssetAsync(id))!.AssetId);CollectionAssert.AreEqual(new byte[]{1,2,3},await File.ReadAllBytesAsync(moved));
    }
    [TestMethod]
    public async Task SessionPreservesIndependentVersionsAndReceiptRequiresOutput()
    {
        using var temp=new TempDirectory();var id=Guid.NewGuid();var one=new StudioEditVersion(Guid.NewGuid(),"one",DateTimeOffset.UtcNow,null,null,null,null,null,null,ColorStudioMatchEngine.Stable,MatchV4ExecutionMode.Auto);
        var two=one with{Id=Guid.NewGuid(),Name="two"};var target=new ColorStudioSessionTarget(id,temp.Combine("a.png"),null,true,null,null,null,EditVersions:[one,two],ActiveEditVersionId:two.Id);
        var path=temp.Combine("edit.ptstudio.json");await ColorStudioSessionStore.SaveAsync(path,new([target],id));var restored=await ColorStudioSessionStore.LoadAsync(path);
        Assert.HasCount(2,restored.Targets[0].EditVersions!);Assert.AreEqual(two.Id,restored.Targets[0].ActiveEditVersionId);
        var output=temp.Combine("out.png");var receipt=new StudioExportReceipt(Guid.NewGuid(),null,null,null,two.Id,1,"PNG",output,"",DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<IOException>(()=>StudioExportReceiptStore.WriteAsync(output,receipt));Assert.IsFalse(File.Exists(output+".kitao-export.json"));
        await File.WriteAllBytesAsync(output,[1,2]);await StudioExportReceiptStore.WriteAsync(output,receipt);Assert.IsTrue(File.Exists(output+".kitao-export.json"));
    }
    [TestMethod]
    public void CandidateBoundaryRejectsDoubleRawWhiteBalanceAndKeepsIntermediateRange()
    {
        var contract=CandidateColorBoundary.ExistingLibRawOutput(true);
        Assert.Throws<InvalidOperationException>(()=>CandidateColorBoundary.ApplyRawWhiteBalance(new(.1,.2,.3),contract,new(2,1,1)));
        var working=CandidateColorBoundary.ToWorking(new(-.1,1.3,.2),new(CandidateSignalDomain.SceneLinear,RAWSelectionAssistant.Core.Services.Color.WorkingColorSpace.AcesCg,false,false,"synthetic"));
        Assert.AreEqual(-.1,working.X);Assert.AreEqual(1.3,working.Y);
        var output=CandidateColorBoundary.ToSrgbOutput(working);Assert.IsTrue(output.X>=0&&output.X<=1&&output.Y>=0&&output.Y<=1);
    }    [TestMethod]
    public async Task CrossLibraryMappingPreservesSourceIdentityAndRejectsWrongHash()
    {
        using var temp=new TempDirectory();var service=new AssetLibraryContainerService();
        var a=await service.CreateAsync(temp.Combine("a.ptlibrary"),"a");var b=await service.CreateAsync(temp.Combine("b.ptlibrary"),"b");
        var source=temp.Combine("a.png");var target=temp.Combine("b.png");await File.WriteAllBytesAsync(source,[1,2,3]);File.Copy(source,target);
        await using var ar=new SqliteAssetLibraryRepository(a.DatabasePath);await using var br=new SqliteAssetLibraryRepository(b.DatabasePath);
        var ai=(await ar.ImportWithIdentitiesAsync([new(source,ComputeContentHash:true)])).Items.Single();var bi=(await br.ImportWithIdentitiesAsync([new(target,ComputeContentHash:true)])).Items.Single();
        var asset=await ar.GetAssetAsync(ai.AssetId);File.Delete(source);
        var linked=await CrossLibraryRelocation.LinkAsync(a,new(a.LibraryId,ai.AssetId,asset!.ContentHash),b,bi.AssetId);Assert.AreEqual(AssetResolutionStatus.Available,linked.Status);
        var resolved=await new AssetLibraryIdentityResolver(a,ar).ResolveAsync(new(a.LibraryId,ai.AssetId,asset.ContentHash));Assert.AreEqual(ai.AssetId,resolved.AssetId);Assert.AreEqual(target,resolved.ResolvedPath);
        await File.WriteAllBytesAsync(target,[9]);Assert.AreEqual(AssetResolutionStatus.Changed,(await new AssetLibraryIdentityResolver(a,ar).ResolveAsync(new(a.LibraryId,ai.AssetId,asset.ContentHash))).Status);
    }}
