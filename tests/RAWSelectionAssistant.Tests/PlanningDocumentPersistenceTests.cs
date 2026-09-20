using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class PlanningDocumentPersistenceTests
{
    [TestMethod]
    public async Task DocumentAutosaveMergesWithoutOverwritingExecutionAndLegacyLinks()
    {
        using var temp = new TempDirectory();
        var id = Guid.NewGuid(); var shot = Guid.NewGuid(); var booking = Guid.NewGuid();
        var store = new PlanningProjectStore(temp.Combine("planning"));
        await store.SaveAsync(new(id, booking, new("旧拍摄目标"), shot,
            [new(Guid.NewGuid(), PlanningVisualLinkKind.FreeCanvas, Guid.NewGuid(), "原画布")]));
        var capture = new ShotCaptureRelation(Guid.NewGuid(), Guid.NewGuid(), shot, id, booking, DateTimeOffset.UtcNow, 1);
        await store.AddCaptureRelationAsync(capture);
        var reference = new ProjectShotReference(Guid.NewGuid(), ShotReferenceKind.General, ExternalReference: temp.Combine("original.jpg"));
        await store.UpdateDocumentAsync(id, new() { Title = "新策划", Body = "正文", References = [new(reference, IsMoodboard: true)] }, new("编辑目标"));
        var restored = await store.LoadAsync(id);
        Assert.AreEqual(booking, restored.BookingId); Assert.AreEqual(shot, restored.CurrentShotId);
        Assert.HasCount(1, restored.CapturedAssets!); Assert.HasCount(1, restored.VisualLinks!);
        Assert.AreEqual("正文", restored.Document!.Body); Assert.AreEqual(1, restored.Version);
    }
    [TestMethod]
    public async Task CrashDraftRoundTripsAndClearsOnlyAfterExplicitCommit()
    {
        using var temp = new TempDirectory();
        var id = Guid.NewGuid(); var store = new PlanningProjectStore(temp.Path);
        var now = DateTimeOffset.UtcNow;
        var shot = new ProjectShot(Guid.NewGuid(), id, 0, "草稿镜头", ProjectShotStatus.NotStarted, "恢复备注", null, [], now, now);
        await store.WriteDraftAsync(id, new() { Body = "恢复中文正文" }, new("恢复目标"), shots: [shot]);
        var draft = await new PlanningProjectStore(temp.Path).LoadDraftAsync(id);
        Assert.AreEqual("恢复中文正文", draft!.Document.Body);
        Assert.AreEqual("恢复备注", draft.Shots!.Single().Notes);
        Assert.IsTrue(File.Exists(store.DraftPath(id)));
        await store.UpdateDocumentAsync(id, draft.Document, draft.Summary);
        store.ClearDraft(id); Assert.IsNull(await store.LoadDraftAsync(id));
    }
    [TestMethod]
    public void LegacyVersionOneJsonDoesNotRequireDocumentAndShotEnumsAreUnchanged()
    {
        var id = Guid.NewGuid();
        var state = JsonSerializer.Deserialize<PlanningProjectState>("{\"ProjectId\":\"" + id + "\",\"Version\":1}")!.Normalize();
        Assert.IsNull(state.Document); Assert.AreEqual(id, state.ProjectId);
        CollectionAssert.AreEqual(new[] { "NotStarted", "InProgress", "Completed", "Skipped" }, Enum.GetNames<ProjectShotStatus>());
    }
}
