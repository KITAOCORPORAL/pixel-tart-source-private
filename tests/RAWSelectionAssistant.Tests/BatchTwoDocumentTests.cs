using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class BatchTwoDocumentTests
{
    [TestMethod]
    public async Task LegacyDocumentGetsStablePackageAndPreservesUnknownFields()
    {
        using var temp = new TempDirectory(); var id = Guid.NewGuid(); var store = new PlanningProjectStore(temp.Path);
        var doc = JsonSerializer.Deserialize<PlanningDocument>("{\"Body\":\"旧正文\",\"FutureField\":{\"value\":17}}")!;
        await store.UpdateDocumentAsync(id, doc, new());
        var first = (await store.LoadAsync(id)).Document!;
        Assert.IsNotNull(first.Package); Assert.AreEqual(id, first.Package.ProjectId);
        await store.UpdateDocumentAsync(id, first with { Body = "新正文" }, new());
        var reopened = (await new PlanningProjectStore(temp.Path).LoadAsync(id)).Document!;
        Assert.AreEqual(first.Package.DocumentId, reopened.Package!.DocumentId);
        Assert.AreEqual("新正文", reopened.Package.Body);
        Assert.AreEqual(17, reopened.Extensions!["FutureField"].GetProperty("value").GetInt32());
    }
    [TestMethod]
    public void CanvasExplicitUpdateKeepsPreviousObjectSnapshot()
    {
        var project = Guid.NewGuid(); var canvas = new CanvasDocument { ProjectId = project, Objects = [new() { Text = "初版" }] };
        var package = new PlanningDocumentPackage { ProjectId = project }.CaptureCanvas(canvas);
        package = package.CaptureCanvas(canvas with { Objects = [canvas.Objects[0] with { Text = "新版" }] });
        var restored = JsonSerializer.Deserialize<PlanningDocumentPackage>(JsonSerializer.Serialize(package))!;
        Assert.HasCount(2, restored.CanvasRevisions);
        Assert.AreEqual("初版", restored.CanvasRevisions[0].EditableSnapshot.Objects[0].Text);
        Assert.AreEqual("新版", restored.CanvasRevisions[1].EditableSnapshot.Objects[0].Text);
    }
    [TestMethod]
    public async Task StudioVersionOneOptionalIdentityRoundTrips()
    {
        using var temp = new TempDirectory(); var id = Guid.NewGuid(); var library = Guid.NewGuid(); var hash = new string('a',64);
        var target = new ColorStudioSessionTarget(id, temp.Combine("source.jpg"), Guid.NewGuid(), true, null, null, null, LibraryId: library, SourceHash: hash);
        await ColorStudioSessionStore.SaveAsync(temp.Combine("edit.ptstudio.json"), new([target],id));
        var restored = await ColorStudioSessionStore.LoadAsync(temp.Combine("edit.ptstudio.json"));
        Assert.AreEqual(library, restored.Targets[0].LibraryId); Assert.AreEqual(hash, restored.Targets[0].SourceHash); Assert.AreEqual(1, restored.Version);
    }
    [TestMethod]
    public async Task ExplicitReferenceOutcomeDoesNotFollowShotAndIsIdempotent()
    {
        using var temp=new TempDirectory(); var project=Guid.NewGuid(); var reference=Guid.NewGuid(); var asset=Guid.NewGuid();
        var store=new PlanningProjectStore(temp.Path);
        var relation=new PlanningReferenceOutcome(reference,Guid.NewGuid(),asset,DateTimeOffset.UtcNow);
        await store.LinkReferenceOutcomeAsync(project,relation); await store.LinkReferenceOutcomeAsync(project,relation);
        var reopened=await new PlanningProjectStore(temp.Path).LoadAsync(project);
        Assert.HasCount(1,reopened.ReferenceOutcomes); Assert.AreEqual(asset,reopened.ReferenceOutcomes[0].AssetId);
    }}
