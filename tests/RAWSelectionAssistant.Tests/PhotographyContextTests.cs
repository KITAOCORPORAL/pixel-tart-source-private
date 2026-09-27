using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class PhotographyContextTests
{
    [TestMethod]
    public void ActiveAssetDoesNotChangeSelectedSet()
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var state = new PhotographyContextSnapshot(first, new HashSet<Guid> { first, second }, 4);
        var next = PhotographyContextReducer.Activate(state, second, "second.raw", 2);
        Assert.AreEqual(second, next.ActiveAssetId);
        CollectionAssert.AreEquivalent(new[] { first, second }, next.SelectedAssetIds.ToArray());
        Assert.AreEqual(2, next.ActiveRating);
    }

    [TestMethod]
    public void PresetPreviewDoesNotBecomeAppliedUntilCommit()
    {
        var state = new PhotographyContextSnapshot(null, new HashSet<Guid>(), null);
        var preview = PhotographyContextReducer.BeginPresetPreview(state, "warm", .4);
        Assert.AreEqual("warm", preview.PreviewPresetId); Assert.IsNull(preview.AppliedPresetId); Assert.IsTrue(preview.HasPendingPreview);
        var committed = PhotographyContextReducer.CommitPreset(preview, "warm", .4);
        Assert.IsNull(committed.PreviewPresetId); Assert.AreEqual("warm", committed.AppliedPresetId); Assert.IsFalse(committed.HasPendingPreview);
    }

    [TestMethod]
    public void BatchTargetsDefaultToSelectionButCanBeExplicit()
    {
        var selected = Guid.NewGuid(); var explicitTarget = Guid.NewGuid();
        var state = new PhotographyContextSnapshot(null, new HashSet<Guid> { selected }, null);
        Assert.AreEqual(selected, state.EffectiveBatchTargetIds.Single());
        var next = state with { BatchTargetIds = new HashSet<Guid> { explicitTarget } };
        Assert.AreEqual(explicitTarget, next.EffectiveBatchTargetIds.Single());
    }
}
