using RAWSelectionAssistant.Core.Services.FreeCanvas;
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
