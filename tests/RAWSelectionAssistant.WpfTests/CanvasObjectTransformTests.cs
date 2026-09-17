using RAWSelectionAssistant.Core.Services.FreeCanvas;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class CanvasObjectTransformTests
{
    [TestMethod]
    public void AddMoveScaleDuplicateAndRemoveOnlyChangeObjects()
    {
        var editor = new CanvasEditor(new());
        editor.Add([new() { AssetId = Guid.NewGuid(), SourcePath = "reference.jpg", Width = 300, Height = 200 }]);
        editor.Move(-100, 50); editor.Scale(2);
        Assert.AreEqual(-100, editor.Selected[0].X); Assert.AreEqual(600, editor.Selected[0].Width);
        editor.Duplicate(); Assert.HasCount(2, editor.Document.Objects);
        Assert.AreEqual("reference.jpg", editor.Selected[0].SourcePath);
        editor.Remove(); Assert.HasCount(1, editor.Document.Objects);
        editor.Undo(); Assert.HasCount(2, editor.Document.Objects);
    }
    [TestMethod]
    public async Task AtomicDocumentRoundTripPreservesReferences()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PixelTart-CanvasStore", Guid.NewGuid().ToString("N"));
        try
        {
            var editor = new CanvasEditor(new()); editor.AddText(-9000, 12000, "构图");
            var store = new CanvasDocumentStore(path); await store.SaveAsync(editor.Document);
            var loaded = await store.LoadAsync(editor.Document.CanvasId);
            Assert.AreEqual("构图", loaded!.Objects.Single().Text);
        }
        finally { if (System.IO.Directory.Exists(path)) System.IO.Directory.Delete(path, true); }
    }
}
