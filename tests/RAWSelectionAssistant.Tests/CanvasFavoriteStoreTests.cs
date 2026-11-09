using System.Text.Json;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class CanvasFavoriteStoreTests
{
    [TestMethod]
    public async Task FavoritesPersistWithoutPathsAndKeepLibraryIdentitySeparate()
    {
        var root=Path.Combine(Path.GetTempPath(),"canvas-favorite-tests",Guid.NewGuid().ToString("N"));
        var id=Guid.NewGuid();var first=new CanvasFavorite(Guid.NewGuid(),id);var second=new CanvasFavorite(Guid.NewGuid(),id);
        var store=new CanvasFavoriteStore(root);await store.SetAsync([first,second,first],true);
        var reopened=await new CanvasFavoriteStore(root).LoadAsync();Assert.HasCount(2,reopened);Assert.IsTrue(reopened.Contains(first));
        await new CanvasFavoriteStore(root).SetAsync([first],false);
        reopened=await store.LoadAsync();Assert.HasCount(1,reopened);Assert.IsTrue(reopened.Contains(second));
        var json=await File.ReadAllTextAsync(Path.Combine(root,"canvas-favorites.json"));Assert.IsFalse(json.Contains("SourcePath"));
        Assert.HasCount(0,await new CanvasDocumentStore(root).ListAsync());
    }
    [TestMethod]
    public async Task InvalidStateAndCompetingWriterCannotOverwriteFavorites()
    {
        var root=Path.Combine(Path.GetTempPath(),"canvas-favorite-tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var path=Path.Combine(root,"canvas-favorites.json");const string invalid="{\"Version\":99,\"Items\":[]}";await File.WriteAllTextAsync(path,invalid);
        var item=new CanvasFavorite(Guid.NewGuid(),Guid.NewGuid());var store=new CanvasFavoriteStore(root);
        await Assert.ThrowsAsync<InvalidDataException>(()=>store.SetAsync([item],true));Assert.AreEqual(invalid,await File.ReadAllTextAsync(path));
        await File.WriteAllTextAsync(path,"{\"Version\":1,\"Items\":[]}");
        using(var held=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            await Assert.ThrowsAsync<IOException>(()=>store.SetAsync([item],true));
        Assert.HasCount(0,await store.LoadAsync());
        await Assert.ThrowsAsync<InvalidDataException>(()=>store.SetAsync([new(Guid.Empty,Guid.NewGuid())],true));
    }
}
