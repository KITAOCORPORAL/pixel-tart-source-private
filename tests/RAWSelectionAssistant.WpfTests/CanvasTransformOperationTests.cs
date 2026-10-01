using RAWSelectionAssistant.Core.Services.FreeCanvas;
using System.IO;
namespace RAWSelectionAssistant.WpfTests;

internal static class CanvasFixtures
{
    public static CanvasEditor Create(int count = 1)
    {
        var editor = new CanvasEditor(new());
        editor.Add(Enumerable.Range(0, count).Select(index => new CanvasObject { AssetId = Guid.NewGuid(), X = index * 350, SourceWidth = 1200, SourceHeight = 800, Width = 300, Height = 200 }));
        return editor;
    }
}
[TestClass] public sealed class CanvasCropTests
{
    [TestMethod] public void AspectCropIsNormalizedNonDestructiveAndUndoable()
    {
        var e = CanvasFixtures.Create(); var original = e.Selected[0];
        e.Crop(CanvasEditor.CropForAspect(original, 1));
        Assert.AreEqual(e.Selected[0].Width, e.Selected[0].Height, .001);
        Assert.AreEqual(original.AssetId, e.Selected[0].AssetId);
        e.Undo(); Assert.AreEqual(original.CropRect, e.Document.Objects[0].CropRect);
    }
}
[TestClass] public sealed class CanvasAspectRatioRegressionTests
{
    [TestMethod]
    public async Task ImportedImageKeepsAspectRatioAcrossResizeSaveAndReopen()
    {
        foreach (var (sourceWidth, sourceHeight) in new[] { (3d, 2d), (2d, 3d), (4d, 3d), (1d, 1d), (16d, 9d), (9d, 16d), (1d, 3d), (3d, 1d) })
        {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-CanvasAspect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var editor = new CanvasEditor(new());
            editor.Add([new CanvasObject
            {
                AssetId = Guid.NewGuid(), SourceWidth = sourceWidth, SourceHeight = sourceHeight,
                Width = sourceWidth * 100, Height = sourceHeight * 100, X = 24, Y = 36
            }]);
            var before = editor.Selected.Single();
            var ratio = before.Width / before.Height;
            editor.Move(80, -20);
            editor.Scale(1.37);
            var resized = editor.Selected.Single();
            Assert.AreEqual(ratio, resized.Width / resized.Height, 1e-10, $"ratio changed during resize for {sourceWidth}:{sourceHeight}");
            var store = new CanvasDocumentStore(root);
            await store.SaveAsync(editor.Document);
            var reopened = await store.LoadAsync(editor.Document.CanvasId);
            Assert.IsNotNull(reopened);
            var persisted = reopened!.Objects.Single();
            Assert.AreEqual(ratio, persisted.Width / persisted.Height, 1e-10, $"ratio changed after reopen for {sourceWidth}:{sourceHeight}");
            Assert.AreEqual(resized.X, persisted.X, 1e-10);
            Assert.AreEqual(resized.Y, persisted.Y, 1e-10);
            Assert.AreEqual(sourceWidth, persisted.SourceWidth, 1e-10);
            Assert.AreEqual(sourceHeight, persisted.SourceHeight, 1e-10);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
[TestClass] public sealed class CanvasRotationTests
{
    [TestMethod] public void MultiRotationUsesSharedCenterAndPreservesSpacing()
    {
        var e=CanvasFixtures.Create(2);var first=e.Selected[0];var second=e.Selected[1];e.Rotate(90);
        Assert.AreEqual(e.Selected[0].X,e.Selected[1].X,.001);Assert.AreEqual(second.X-first.X,e.Selected[1].Y-e.Selected[0].Y,.001);
        e.Undo();Assert.AreEqual(first,e.Document.Objects[0]);
    }
    [TestMethod] public void RotationNormalizesAndSnapsAtFifteenDegrees()
    {
        var e = CanvasFixtures.Create(); e.Rotate(-90); Assert.AreEqual(270, e.Selected[0].Rotation);
        e.Rotate(22, true); Assert.AreEqual(285, e.Selected[0].Rotation); e.Rotate(180); Assert.AreEqual(105, e.Selected[0].Rotation);
    }
}
[TestClass] public sealed class CanvasFlipTests
{
    [TestMethod] public void IndependentFlipsPreserveIdentityAndCanUndo()
    {
        var e = CanvasFixtures.Create(); var id = e.Selected[0].AssetId;
        e.Flip(true); e.Flip(false); Assert.IsTrue(e.Selected[0].FlipX && e.Selected[0].FlipY);
        Assert.AreEqual(id, e.Selected[0].AssetId); e.Undo(); Assert.IsFalse(e.Selected[0].FlipY);
    }
}
