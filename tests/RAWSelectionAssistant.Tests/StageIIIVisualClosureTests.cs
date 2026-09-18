using System.Globalization;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class VisualZoneHoverTests
{
    [TestMethod]
    public void HoverReusesPreparedFramesRestoresOriginalAndPreservesSource()
    {
        var bytes = Enumerable.Range(0, 256).SelectMany(value => Enumerable.Repeat((byte)value, 3)).ToArray();
        var before = bytes.ToArray(); var source = new VisualPixelBuffer(256, 1, bytes);
        var map = VisualAnalysisEngine.CreateZoneMap(source);
        var hover = new VisualZoneHoverPreview(source, map);
        for (var zone = 0; zone < 11; zone++)
        {
            var selected = hover.Select(zone);
            Assert.AreSame(selected, hover.Select(zone));
            for (var pixel = 0; pixel < 256; pixel++)
            {
                if ((int)Math.Round(map.Rgb24.Span[pixel * 3] * 10d / 255) == zone) continue;
                CollectionAssert.AreEqual(before.AsSpan(pixel * 3, 3).ToArray(), selected.Rgb24.Span.Slice(pixel * 3, 3).ToArray());
            }
        }
        Assert.AreSame(source, hover.Select(null)); CollectionAssert.AreEqual(before, bytes);
    }
}
[TestClass]
public sealed class PaletteHslCopyTests
{
    [TestMethod]
    public void ClipboardTextIsLocaleIndependent()
    {
        var previous = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = new("fr-FR"); Assert.AreEqual("HSL(31, 24%, 68%)", PaletteClipboardText.Hsl(new(new(1,2,3), new(), 31, .24, .68, 1, "#010203"))); }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
[TestClass]
public sealed class ProjectPalettePersistenceTests
{
    [TestMethod]
    public async Task PaletteAndToneSurviveReopenAndConcurrentStoreInstances()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-StageIII", Guid.NewGuid().ToString("N"));
        try
        {
            var id = Guid.NewGuid(); var sources = new[] { new ProjectVisualSource(Guid.NewGuid(), Guid.NewGuid(), "fingerprint", "Canvas", Guid.NewGuid()) };
            var palette = new ProjectPaletteReference([new(new(1,2,3), new(), 31, .24, .68, 1, "#010203")], sources, DateTimeOffset.UtcNow, "v4");
            var tone = new ProjectToneTarget(new(Enumerable.Repeat(1d / 11, 11).ToArray()), Enumerable.Repeat(1d / 256, 256).ToArray(), sources, DateTimeOffset.UtcNow, "v4");
            await Task.WhenAll(new ProjectVisualReferenceStore(root).SavePaletteAsync(id, palette), new ProjectVisualReferenceStore(root).SaveToneAsync(id, tone));
            var loaded = await new ProjectVisualReferenceStore(root).LoadAsync(id);
            Assert.AreEqual("#010203", loaded.DefaultPalette!.Colors.Single().Hex);
            Assert.AreEqual(sources[0], loaded.DefaultToneTarget!.Sources.Single());
            Assert.AreEqual(1, loaded.DefaultToneTarget.Zones.Sum, 1e-6);
            Assert.HasCount(1, Directory.GetFiles(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
[TestClass]
public sealed class ProjectToneTargetTests
{
    [TestMethod]
    public async Task CorruptProjectDataIsNeverSilentlyOverwritten()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-StageIII", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var id = Guid.NewGuid(); var path = Path.Combine(root, $"{id:N}.visual.json"); await File.WriteAllTextAsync(path, "broken");
            var tone = new ProjectToneTarget(new(new double[11]), new double[256], [new(Guid.NewGuid(), Guid.NewGuid(), "source")], DateTimeOffset.UtcNow, "v4");
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => new ProjectVisualReferenceStore(root).SaveToneAsync(id, tone));
            Assert.AreEqual("broken", await File.ReadAllTextAsync(path));
        }
        finally { Directory.Delete(root, true); }
    }
}
