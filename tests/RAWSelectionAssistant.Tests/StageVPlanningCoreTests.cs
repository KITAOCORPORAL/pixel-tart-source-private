using System.Security.Cryptography;
using System.Diagnostics;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class PlanningProjectPersistenceTests
{
    [TestMethod]
    public async Task SummaryLinksAndCurrentShotPersistAcrossStoreReopen()
    {
        using var temp = new PlanningTempDirectory(); var project = Guid.NewGuid(); var shot = Guid.NewGuid(); var store = new PlanningProjectStore(temp.Path);
        await store.UpdateSummaryAsync(project, new("先锋街头肖像", ["强闪", "石材", "强闪"], "保留建筑空间", "入口\n长廊", "注意游客", "画册"));
        await store.SetCurrentShotAsync(project, shot);
        await store.AddVisualLinkAsync(project, new(Guid.NewGuid(), PlanningVisualLinkKind.FreeCanvas, Guid.NewGuid(), "灯光画布", PlanningCanvasRole.Lighting));
        var actual = await new PlanningProjectStore(temp.Path).LoadAsync(project);
        Assert.AreEqual(shot, actual.CurrentShotId); Assert.AreEqual("先锋街头肖像", actual.Summary!.ShootGoal); Assert.HasCount(2, actual.Summary.Keywords!); Assert.HasCount(1, actual.VisualLinks!); Assert.IsGreaterThanOrEqualTo(3, actual.Revision);
    }

    [TestMethod]
    public async Task EmptyStageIvProjectMigratesWithoutCentralDatabaseMutation()
    {
        using var temp = new PlanningTempDirectory(); var project = Guid.NewGuid();
        var state = await new PlanningProjectStore(temp.Path).LoadAsync(project);
        Assert.AreEqual(project, state.ProjectId); Assert.AreEqual(1, state.Version); Assert.AreEqual(0, state.Revision); Assert.HasCount(0, state.VisualLinks!); Assert.HasCount(0, state.CapturedAssets!);
    }
}

[TestClass]
public sealed class ShotCrudAndReorderTests
{
    [TestMethod]
    public async Task StageIvShotJsonLoadsWithStageVDefaultsAndCrudPersists()
    {
        using var temp = new PlanningTempDirectory(); var project = Guid.NewGuid(); var now = DateTimeOffset.UtcNow; var store = new ProjectShotStore(temp.Path);
        var shot = new ProjectShot(Guid.NewGuid(), project, 0, "庙门入口全身", ProjectShotStatus.NotStarted, "低机位", null, [], now, now);
        await store.SaveAsync(shot); var actual = (await new ProjectShotStore(temp.Path).LoadAsync(project)).Shots.Single();
        Assert.AreEqual(0, actual.EstimatedMinutes); Assert.IsNull(actual.Scene); Assert.IsFalse(actual.IsArchived);
        await store.SaveAsync(actual with { EstimatedMinutes = 12, Scene = "东岳庙入口", Status = ProjectShotStatus.InProgress });
        var updated = (await store.LoadAsync(project)).Shots.Single(); Assert.AreEqual(12, updated.EstimatedMinutes); Assert.AreEqual("东岳庙入口", updated.Scene); Assert.AreEqual(ProjectShotStatus.InProgress, updated.Status);
    }

    [TestMethod]
    public async Task ReorderWritesOneAtomicCatalogAndKeepsArchivedShots()
    {
        using var temp = new PlanningTempDirectory(); var project = Guid.NewGuid(); var now = DateTimeOffset.UtcNow; var store = new ProjectShotStore(temp.Path);
        var shots = Enumerable.Range(0, 100).Select(index => new ProjectShot(Guid.NewGuid(), project, index, $"拍摄 {index}", ProjectShotStatus.NotStarted, null, null, [], now, now)).ToArray();
        await store.SaveCatalogAsync(new(project, shots)); await store.ReorderAsync(project, shots.Reverse().Select(shot => shot.ShotId).ToArray());
        var actual = await store.LoadAsync(project); Assert.AreEqual(shots[^1].ShotId, actual.Shots[0].ShotId); Assert.AreEqual(99, actual.Shots[^1].Order);
    }

    [TestMethod]
    public void AllShotReferenceKindsRemainTheSingleSharedContract() =>
        CollectionAssert.AreEquivalent(new[] { "Lighting", "Pose", "Storyboard", "Styling", "General" }, Enum.GetNames<ShotReferenceKind>());
}

[TestClass]
public sealed class PlanningExecutionContextTests
{
    [TestMethod]
    public async Task ContextUsesSharedColorResolverAndCarriesReferencesWithoutBitmaps()
    {
        using var temp = new PlanningTempDirectory(); var project = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var shotLook = Guid.NewGuid(); var reference = new ProjectShotReference(Guid.NewGuid(), ShotReferenceKind.Lighting, Guid.NewGuid(), Guid.NewGuid(), Title: "顶光", IsPinned: true);
        var shot = new ProjectShot(Guid.NewGuid(), project, 0, "石狮旁近景", ProjectShotStatus.InProgress, "保持人物与石狮关系", shotLook, [reference], now, now);
        var shotStore = new ProjectShotStore(temp.Combine("shots")); await shotStore.SaveAsync(shot);
        var planning = new PlanningProjectStore(temp.Combine("planning")); await planning.SetCurrentShotAsync(project, shot.ShotId);
        var visual = new ProjectVisualReferenceStore(temp.Combine("visual")); var lookStore = new ReferenceLookStore(temp.Combine("looks"));
        var context = await new PlanningExecutionContextService(shotStore, planning, visual, lookStore).BuildAsync(project, Guid.NewGuid());
        Assert.AreEqual(shotLook, context.EffectiveReferenceLookId); Assert.AreEqual(ReferenceLookResolver.Resolve(shotLook, null, null), context.EffectiveReferenceLookId);
        Assert.HasCount(1, context.ShotReferences); Assert.HasCount(1, context.PinnedReferenceIds); Assert.AreEqual("保持人物与石狮关系", context.ShootNotes);
    }

    [TestMethod]
    public async Task FrozenCaptureContextIsRaceSafeAndRelationsAreIdempotent()
    {
        using var temp = new PlanningTempDirectory(); var project = Guid.NewGuid(); var now = DateTimeOffset.UtcNow; var shotA = Guid.NewGuid(); var shotB = Guid.NewGuid();
        var shotStore = new ProjectShotStore(temp.Combine("shots")); await shotStore.SaveCatalogAsync(new(project, [
            new(shotA,project,0,"A",ProjectShotStatus.InProgress,null,null,[],now,now), new(shotB,project,1,"B",ProjectShotStatus.NotStarted,null,null,[],now,now)]));
        var planning = new PlanningProjectStore(temp.Combine("planning")); await planning.SetCurrentShotAsync(project, shotA);
        var service = new PlanningExecutionContextService(shotStore, planning, new(temp.Combine("visual")), new(temp.Combine("looks")));
        var frozen = await service.BuildAsync(project); await planning.SetCurrentShotAsync(project, shotB);
        var relation = frozen.BindCapture(Guid.NewGuid(), Guid.NewGuid(), now); await planning.AddCaptureRelationAsync(relation); await planning.AddCaptureRelationAsync(relation);
        var state = await planning.LoadAsync(project); Assert.AreEqual(shotA, state.CapturedAssets!.Single().ShotId); Assert.HasCount(1, state.CapturedAssets!);
    }
}

[TestClass]
public sealed class PlanningSourceSafetyTests
{
    [TestMethod]
    public async Task PlanningOperationsNeverModifyOrDeleteReferencedSource()
    {
        using var temp = new PlanningTempDirectory(); var source = temp.Combine("reference.jpg"); await File.WriteAllBytesAsync(source, [1,2,3,4,5,6]); var before = SHA256.HashData(await File.ReadAllBytesAsync(source));
        var project = Guid.NewGuid(); var shotStore = new ProjectShotStore(temp.Combine("shots")); var now = DateTimeOffset.UtcNow;
        var reference = new ProjectShotReference(Guid.NewGuid(), ShotReferenceKind.Pose, ExternalReference: source, Title: "姿势参考");
        var first = new ProjectShot(Guid.NewGuid(), project, 0, "姿势", ProjectShotStatus.NotStarted, null, null, [reference], now, now);
        var second = first with { ShotId = Guid.NewGuid(), Order = 1, Name = "姿势副本", References = first.References.Select(item => item with { ReferenceId = Guid.NewGuid() }).ToArray() };
        await shotStore.SaveCatalogAsync(new(project, [first, second])); await shotStore.ReorderAsync(project, [second.ShotId, first.ShotId]); await shotStore.SaveAsync(first with { References = [] }); await shotStore.RemoveAsync(project, second.ShotId);
        Assert.IsTrue(File.Exists(source)); CollectionAssert.AreEqual(before, SHA256.HashData(await File.ReadAllBytesAsync(source)));
    }
}

[TestClass]
public sealed class PlanningPerformanceSmokeTests
{
    [TestMethod]
    public async Task Planning200ShotsAnd1000ReferencesSmokeTest()
    {
        using var temp=new PlanningTempDirectory();var project=Guid.NewGuid();var now=DateTimeOffset.UtcNow;var store=new ProjectShotStore(temp.Path);
        var watch=Stopwatch.StartNew();var shots=Enumerable.Range(0,200).Select(index=>new ProjectShot(Guid.NewGuid(),project,index,$"拍摄 {index}",ProjectShotStatus.NotStarted,null,null,
            Enumerable.Range(0,5).Select(item=>new ProjectShotReference(Guid.NewGuid(),(ShotReferenceKind)(item%5),ExternalReference:temp.Combine($"{index}-{item}.jpg"))).ToArray(),now,now)).ToArray();
        await store.SaveCatalogAsync(new(project,shots));var loaded=await store.LoadAsync(project);await store.ReorderAsync(project,loaded.Shots.Reverse().Select(item=>item.ShotId).ToArray());watch.Stop();
        Assert.HasCount(200,loaded.Shots);Assert.AreEqual(1000,loaded.Shots.Sum(item=>item.References.Count));Assert.IsLessThan(TimeSpan.FromSeconds(10),watch.Elapsed,$"Smoke elapsed: {watch.Elapsed.TotalMilliseconds:F0} ms");
    }

    [TestMethod]
    public async Task DuplicateCaptureRelationsRemainIdempotentAtScale()
    {
        using var temp=new PlanningTempDirectory();var project=Guid.NewGuid();var shot=Guid.NewGuid();var store=new PlanningProjectStore(temp.Path);var relation=new ShotCaptureRelation(null,Guid.NewGuid(),shot,project,null,DateTimeOffset.UtcNow,4,"Tether");
        for(var index=0;index<100;index++)await store.AddCaptureRelationAsync(relation);var state=await store.LoadAsync(project);Assert.HasCount(1,state.CapturedAssets!);
    }
}

internal sealed class PlanningTempDirectory : IDisposable
{
    public PlanningTempDirectory(){Path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"PixelTart-StageV",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path);}
    public string Path { get; }
    public string Combine(string name)=>System.IO.Path.Combine(Path,name);
    public void Dispose(){if(Directory.Exists(Path))Directory.Delete(Path,true);}
}
