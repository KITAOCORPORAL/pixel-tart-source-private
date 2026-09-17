using RAWSelectionAssistant.Core.Services.FreeCanvas;
namespace RAWSelectionAssistant.WpfTests;
[TestClass] public sealed class CanvasGroupTests
{
    [TestMethod] public void GroupRemainsIndependentAndSelectsAsUnit() { var e = CanvasFixtures.Create(2); e.Group(); var id = e.Selected[0].GroupId; e.Select(e.Selected[0].ObjectId); Assert.HasCount(2, e.Selected); e.Move(10, 20); Assert.HasCount(2, e.Document.Objects); Assert.AreEqual(id, e.Selected[1].GroupId); e.Ungroup(); Assert.IsTrue(e.Selected.All(item => item.GroupId is null)); }
}
[TestClass] public sealed class CanvasZOrderTests
{
    [TestMethod] public void TopAndBottomPreserveOthersRelativeOrder() { var e = CanvasFixtures.Create(3); var id = e.Selected[0].ObjectId; e.Select(id); e.Layer(true); Assert.AreEqual(2, e.Selected[0].ZIndex); e.Layer(false); Assert.AreEqual(0, e.Selected[0].ZIndex); }
}
[TestClass] public sealed class CanvasLockTests
{
    [TestMethod] public void LockedObjectsRemainSelectableButRejectTransforms() { var e = CanvasFixtures.Create(); e.SetLocked(true); var before = e.Selected[0]; e.Move(20,20); e.Scale(2); e.Crop(new(.1,.1,.5,.5)); e.Rotate(90); e.Flip(true); e.Remove(); Assert.AreEqual(before, e.Selected[0]); e.SetLocked(false); e.Move(2,0); Assert.AreEqual(2, e.Selected[0].X); }
}
[TestClass] public sealed class CanvasMultiSelectionTests
{
    [TestMethod] public void MarqueeAndAdditiveSelectionSupportSharedScaling() { var e = CanvasFixtures.Create(3); e.Marquee(new(-1,-1,660,240)); Assert.HasCount(2,e.Selected); e.Scale(2); Assert.AreEqual(700,e.Selected[1].X); e.Select(e.Document.Objects[2].ObjectId,true); Assert.HasCount(3,e.Selected); }
}
[TestClass] public sealed class CanvasUndoRedoTests
{
    [TestMethod] public void GestureIsOneUndoAndArrangeCanUndo() { var e=CanvasFixtures.Create(2); e.BeginGesture(); e.Move(1,0); e.Move(2,0); e.EndGesture(); e.Undo(); Assert.AreEqual(0,e.Document.Objects[0].X); e.Redo(); Assert.AreEqual(3,e.Document.Objects[0].X); var before=e.Document.Objects.ToArray(); e.Arrange("grid"); e.Undo(); CollectionAssert.AreEqual(before,e.Document.Objects.ToArray()); }
}
