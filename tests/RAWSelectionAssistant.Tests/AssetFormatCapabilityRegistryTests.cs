using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetFormatCapabilityRegistryTests
{
    [TestMethod]
    public void DefaultRegistryTruthfullyCoversRequiredFormats()
    {
        var registry = AssetFormatCapabilityRegistry.Default;
        foreach (var extension in new[]
                 {
                     ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tif", ".tiff",
                     ".arw", ".cr2", ".cr3", ".nef", ".raf", ".orf", ".rw2", ".dng",
                     ".psd", ".psb", ".svg", ".gif", ".mp4", ".mov", ".avi", ".pdf", ".ttf"
                 })
        {
            Assert.IsTrue(registry.TryGet(extension, out var capability), extension);
            Assert.IsTrue(capability.CanImport, extension);
            Assert.IsTrue(capability.CanIndex, extension);
            Assert.IsFalse(capability.CanView, $"{extension} must not claim a Viewer before one exists.");
            Assert.AreEqual(capability.CanThumbnail, capability.ThumbnailProvider is not null, extension);
            Assert.AreEqual(capability.CanAnalyze, capability.AnalysisProvider is not null, extension);
            Assert.AreEqual(capability.CanView, capability.ViewerProvider is not null, extension);
        }
    }

    [TestMethod]
    public void PickerAndUnknownValidationUseTheSameRegistry()
    {
        var registry = AssetFormatCapabilityRegistry.Default;
        var filter = registry.BuildPickerFilter();
        foreach (var capability in registry.Items.Where(item => item.CanImport))
            StringAssert.Contains(filter, "*" + capability.Extension.ToLowerInvariant());

        var unknown = registry.GetOrUnknown("photo.future");
        Assert.IsFalse(unknown.CanImport);
        Assert.IsFalse(unknown.CanThumbnail);
        Assert.IsFalse(unknown.CanView);
        Assert.IsFalse(unknown.CanAnalyze);
        Assert.IsFalse(string.IsNullOrWhiteSpace(unknown.LimitationReason));
    }
}
