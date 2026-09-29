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
        var state = new TwoUpCompareState(Guid.NewGuid(), Guid.NewGuid(), new CompareViewport(2, 4, -3), FaceLockEnabled: true);
        var swapped = state.Swap();
        Assert.AreEqual(state.Viewport, swapped.Viewport);
        Assert.AreEqual(2, swapped.Viewport.Zoom);
        Assert.AreEqual(CompareZoomMode.Fit, swapped.Viewport.Mode);
    }

    [TestMethod]
    public void CompareViewportKeepsFitAndActualPixelSemanticsDistinct()
    {
        Assert.AreEqual(CompareZoomMode.Fit, CompareViewport.FitViewport().Mode);
        Assert.AreEqual(CompareZoomMode.ActualPixels, CompareViewport.ActualPixels(1.5).Mode);
        Assert.AreEqual(CompareZoomMode.Custom, CompareViewport.Custom(2).Mode);
    }

    [TestMethod]
    public void CompareStateKeepsIndependentViewportsAndSwapsTheirSemantics()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var state = new TwoUpCompareState(a, b, CompareViewport.Custom(2, .2, -.1), CompareViewport.Custom(1.5, -.3, .4));
        var swapped = state.Swap();
        Assert.AreEqual(b, swapped.PrimaryId);
        Assert.AreEqual(a, swapped.ChallengerId);
        Assert.AreEqual(1.5, swapped.PrimaryViewport.Zoom);
        Assert.AreEqual(2, swapped.EffectiveSecondaryViewport.Zoom);
    }

    [TestMethod]
    public void ActualPixelsUsesSourceDpiAndPhysicalDeviceScale()
    {
        var scale = ActualPixelScaleCalculator.Calculate(144, 144, 1.5, 1.5);
        Assert.AreEqual(1, scale.ScaleX * 1.5 * 96 / 144, 1e-12);
        Assert.AreEqual(1, scale.ScaleY * 1.5 * 96 / 144, 1e-12);
        Assert.AreEqual(1, ActualPixelScaleCalculator.ActualPixelZoom(144, 1.5), 1e-12);
    }

    [TestMethod]
    public void ActualPixelsIgnoresPrintDpiSemanticsAcrossWindowsScalingMatrix()
    {
        foreach (var windowsScale in new[] { 1d, 1.25, 1.5, 1.75, 2d })
        foreach (var dpi in new[] { 72d, 96d, 300d })
        foreach (var pixels in new[] { (6000, 4000), (4000, 6000), (9504, 6336), (11648, 8736) })
        {
            var geometry = ImagePixelGeometry.Create(pixels.Item1, pixels.Item2, dpi, dpi);
            var scale = ActualPixelScaleCalculator.Calculate(dpi, dpi, windowsScale, windowsScale);
            var physicalRatio = geometry.NaturalWidthDip * windowsScale * scale.ScaleX / pixels.Item1;
            Assert.AreEqual(1, physicalRatio, 1e-12, $"{pixels} at {windowsScale:P0}/{dpi} DPI");
        }
    }

    [TestMethod]
    public void CompareViewportUsesNormalizedCenterForDifferentResolutionSync()
    {
        var a = new CompareViewport(2).WithNormalizedCenter(.25, .75);
        var geometry = ImagePixelGeometry.Create(6000, 4000, 300, 300);
        var pan = CompareViewportGeometry.PanForCenter(a, geometry, 1200, 800, 1.5);
        Assert.IsGreaterThan(0, pan.X);
        Assert.IsLessThan(0, pan.Y);
        var dragged = CompareViewportGeometry.Drag(a, 120, -80, geometry, 1.5);
        Assert.IsLessThan(a.NormalizedCenterX, dragged.NormalizedCenterX);
        Assert.IsGreaterThan(a.NormalizedCenterY, dragged.NormalizedCenterY);
        Assert.IsTrue(dragged.NormalizedCenterX is >= 0 and <= 1);
        Assert.IsTrue(dragged.NormalizedCenterY is >= 0 and <= 1);
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

    [TestMethod]
    public async Task ExportRecipeStoreRecoversCorruptJsonAndKeepsBuiltIns()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("recipes.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{not-json");
        var store = new ExportRecipeStore(path);
        var loaded = await store.LoadAsync();
        Assert.HasCount(4, loaded);
        Assert.IsFalse(File.Exists(path));
        Assert.IsTrue(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "recipes.json.corrupt-*").Any());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => store.SaveAsync(ExportRecipeStore.BuiltIns[0]));
    }
}
