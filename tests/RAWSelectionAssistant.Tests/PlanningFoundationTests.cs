using System.Text.Json;
using System.Text.Json.Nodes;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class PlanningFoundationTests
{
    [TestMethod]
    public async Task LegacyReadDoesNotWriteAndIdentityMigrationIsStableAndLossless()
    {
        using var temp = new TempDirectory(); var id = Guid.NewGuid();
        var path = Path.Combine(temp.Path, $"{id:N}.planning.json");
        var raw = new JsonObject { ["ProjectId"] = id.ToString(), ["Version"] = 1, ["FuturePlan"] = new JsonObject { ["n"] = 42 },
            ["Summary"] = new JsonObject { ["ShootGoal"] = "目标", ["FutureSummary"] = "preserve" },
            ["Document"] = new JsonObject { ["Title"] = "旧文档", ["Body"] = "正文\n保留", ["CustomBody"] = "旧自定义\n  保留空格", ["FutureDocument"] = new JsonArray(1,2,3) } };
        await File.WriteAllTextAsync(path, raw.ToJsonString()); var bytes = await File.ReadAllBytesAsync(path);
        var store = new PlanningProjectStore(temp.Path);
        var first = await store.LoadAsync(id); var second = await new PlanningProjectStore(temp.Path).LoadAsync(id);
        Assert.AreEqual(first.Document!.Package!.DocumentId, second.Document!.Package!.DocumentId);
        CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path), "Read-only migration cannot rewrite original files.");
        await store.UpdateDocumentAsync(id, first.Document, first.Summary!);
        var reopened = await store.LoadAsync(id);
        CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path + ".foundation-v0.bak"));
        Assert.AreEqual(first.Document.Package.DocumentId, reopened.Document!.Package!.DocumentId);
        Assert.AreEqual("旧自定义\n  保留空格", reopened.Document.CustomBody);
        Assert.AreEqual("preserve", reopened.Summary!.Extensions!["FutureSummary"].GetString());
        Assert.AreEqual(42, reopened.Extensions!["FuturePlan"].GetProperty("n").GetInt32());
        Assert.AreEqual("[1,2,3]", reopened.Document.Extensions!["FutureDocument"].GetRawText());
        Assert.AreEqual(1, reopened.Version); Assert.AreEqual(1, reopened.Document.FoundationVersion);
    }
    [TestMethod]
    public async Task OldNotesBecomeActionOnceAndBatchRetainsUnknownCatalogAndShotFields()
    {
        using var temp = new TempDirectory(); var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var old = new ProjectShot(Guid.NewGuid(),id,0,"旧镜头",ProjectShotStatus.NotStarted,"  转身\n手势  ",null,[],now,now);
        var node = JsonSerializer.SerializeToNode(new ProjectShotCatalog(id,[old]))!.AsObject(); node["FutureCatalog"] = "kept";
        node["Shots"]![0]!["FutureShot"] = new JsonObject { ["flag"] = true };
        var path = Path.Combine(temp.Path,$"{id:N}.shots.json"); await File.WriteAllTextAsync(path,node.ToJsonString());
        var store = new ProjectShotStore(temp.Path); var catalog = await store.LoadAsync(id); var shot = catalog.Shots[0];
        Assert.AreEqual(old.Notes,shot.Notes); Assert.AreEqual(old.Notes,shot.Action);
        await store.SaveBatchAsync(id,[shot with { Action="", Remarks="单独备注", CustomValues=new(){["custom:x"]="值"} }]);
        var reopened = await store.LoadAsync(id); Assert.AreEqual("",reopened.Shots[0].Action,"Empty action must not remigrate old notes.");
        Assert.AreEqual(old.ShotId,reopened.Shots[0].ShotId); Assert.AreEqual("kept",reopened.Extensions!["FutureCatalog"].GetString());
        Assert.IsTrue(reopened.Shots[0].Extensions!["FutureShot"].GetProperty("flag").GetBoolean());
        Assert.AreEqual(1L,reopened.Revision);
        var before = await File.ReadAllBytesAsync(path);
        await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveBatchAsync(id,[reopened.Shots[0] with { Name="" }]));
        CollectionAssert.AreEqual(before,await File.ReadAllBytesAsync(path));
        using var cancel = new CancellationTokenSource();cancel.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(()=>store.SaveBatchAsync(id,[reopened.Shots[0]],cancel.Token));
        CollectionAssert.AreEqual(before,await File.ReadAllBytesAsync(path));
    }
    [TestMethod]
    public async Task ColumnsArchiveWithoutDroppingValuesAndInvalidSchemaFailsClosed()
    {
        using var temp = new TempDirectory(); var id=Guid.NewGuid();var column=new PlanningShotColumn(Guid.NewGuid(),"服装编号");
        var doc=new PlanningDocument{ShotColumns=[column],CustomBody="content"}.EnsureIdentity(id);
        var store=new PlanningProjectStore(temp.Path);await store.UpdateDocumentAsync(id,doc,new());
        await store.UpdateDocumentAsync(id,doc with{ShotColumns=[column with{Archived=true}]},new());
        var reopened=(await store.LoadAsync(id)).Document!;
        Assert.AreEqual(column.Id,reopened.ShotColumns[0].Id);Assert.IsTrue(reopened.ShotColumns[0].Archived);
        Assert.Throws<InvalidDataException>(()=>(doc with{FoundationVersion=99}).EnsureIdentity(id));
        Assert.Throws<InvalidDataException>(()=>(doc with{ShotColumns=[column,column]}).EnsureIdentity(id));
        var bytes=await File.ReadAllBytesAsync(Path.Combine(temp.Path,$"{id:N}.planning.json"));
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(()=>store.UpdateDocumentAsync(id,doc,new(),cancel.Token));
        CollectionAssert.AreEqual(bytes,await File.ReadAllBytesAsync(Path.Combine(temp.Path,$"{id:N}.planning.json")));
    }
}
