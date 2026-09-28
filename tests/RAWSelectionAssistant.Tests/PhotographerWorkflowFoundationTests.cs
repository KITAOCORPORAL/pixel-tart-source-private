using RAWSelectionAssistant.Core.Models.Photography;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Publishing;
using RAWSelectionAssistant.Core.Services.Sync;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class PhotographerWorkflowFoundationTests
{
    [TestMethod]
    public void FaceLockUsesEyesAndDeterministicPrimaryRule()
    {
        var source = new FaceObservation(10, 10, 100, 120, new(40, 55), new(80, 60), .99, "a");
        var target = new FaceObservation(30, 40, 200, 240, new(100, 130), new(180, 140), .98, "b");
        var plan = FaceLockPlanner.Plan(source, target);
        Assert.AreEqual(2, plan.Scale, .0001);
        Assert.AreEqual(20, plan.TranslateX, .0001);
        Assert.AreEqual(20, plan.TranslateY, .0001);
        Assert.AreEqual(FaceLockFallback.None, plan.Fallback);
        Assert.AreSame(source, FaceLockPlanner.SelectPrimary([source]));
    }

    [TestMethod]
    public void FaceLockFallsBackWithoutFaceAndCompareKeepsViewportOnSwap()
    {
        Assert.AreEqual(FaceLockFallback.NoFace, FaceLockPlanner.Plan(null, null).Fallback);
        var state = new TwoUpCompareState(Guid.NewGuid(), Guid.NewGuid(), new CompareViewport(2, 4, -3), true);
        var swapped = state.Swap();
        Assert.AreEqual(state.Viewport, swapped.Viewport);
        Assert.AreEqual(2, swapped.Viewport.Zoom);
    }

    [TestMethod]
    public void ExportRecipesValidateTiff16AndProvideFourBuiltIns()
    {
        Assert.HasCount(4, ExportRecipeStore.BuiltIns);
        Assert.IsTrue(ExportRecipeStore.BuiltIns.All(item => item.Validate() is not null));
        Assert.ThrowsExactly<ArgumentException>(() => new ExportRecipe(Guid.NewGuid(), "bad", ExportRecipeFormat.Tiff, ExportRecipeBitDepth.Eight).Validate());
    }

    [TestMethod]
    public void BufferedBookingConflictDistinguishesHoldAndLocked()
    {
        var first = new ShootBooking { StartAtUtc = DateTimeOffset.Parse("2026-09-28T10:00:00Z"), EndAtUtc = DateTimeOffset.Parse("2026-09-28T11:00:00Z"), Status = ShootBookingStatus.Tentative };
        var second = new ShootBooking { StartAtUtc = DateTimeOffset.Parse("2026-09-28T11:15:00Z"), EndAtUtc = DateTimeOffset.Parse("2026-09-28T12:00:00Z"), Status = ShootBookingStatus.Confirmed };
        var candidate = BookingConflictRules.ToOccupiedInterval(second, new(30, 0), DateTimeOffset.UtcNow);
        var conflicts = BookingConflictRules.Detect(candidate, [BookingConflictRules.ToOccupiedInterval(first, new(0, 0), DateTimeOffset.UtcNow)]);
        Assert.AreEqual(BookingConflictKind.HoldOverlap, conflicts.Single().Kind);
    }

    [TestMethod]
    public void SyncIsIdempotentAndFlagsHighRiskFields()
    {
        var local = new SyncEntityEnvelope("Booking", "stable-1", 1, DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddMinutes(-2), null, "desktop", new Dictionary<string, string?> { ["start"] = "10:00" });
        var incoming = local with { Revision = 2, DeviceId = "mobile", UpdatedAtUtc = DateTimeOffset.UtcNow, Fields = new Dictionary<string, string?> { ["start"] = "11:00" } };
        var result = SyncConflictPolicy.Apply(local, incoming);
        Assert.IsTrue(result.Applied);
        Assert.IsTrue(result.Conflicts.Single().RequiresResolution);
        Assert.IsTrue(SyncConflictPolicy.Apply(incoming, incoming).Duplicate);
    }

    [TestMethod]
    public async Task ExportRecipeStorePersistsCustomRecipeAndDeletesIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "pixel-tart-recipe-test", Guid.NewGuid().ToString("N"));
        var store = new ExportRecipeStore(Path.Combine(root, "recipes.json"));
        var recipe = ExportRecipeStore.BuiltIns[0] with { Id = Guid.NewGuid(), Name = "Test Recipe" };
        try
        {
            await store.SaveAsync(recipe);
            Assert.IsTrue((await store.LoadAsync()).Any(item => item.Id == recipe.Id));
            await store.DeleteAsync(recipe.Id);
            Assert.IsFalse((await store.LoadAsync()).Any(item => item.Id == recipe.Id));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
