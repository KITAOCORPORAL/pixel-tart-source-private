using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ColorProfileRegistryTests
{
    [TestMethod]
    public void RegistryKeepsAssignAndConvertAsDistinctOperations()
    {
        Assert.AreEqual("AdobeRGB", ColorProfileRegistry.NormalizeId("Adobe RGB (1998)"));
        var registry = new ColorProfileRegistry();
        Assert.HasCount(4, registry.List());
        Assert.IsTrue(registry.List().Any(item => item.Id == "DisplayP3"));
    }

    [TestMethod]
    public void ResolveUsesOnlyExplicitRootsAndReportsUnavailableWithoutInventingAProfile()
    {
        using var temp = new TempDirectory();
        var registry = new ColorProfileRegistry();
        var missing = registry.Resolve("ProPhotoRGB", [temp.Path]);
        Assert.IsFalse(missing.IsAvailable);
        Assert.IsNull(missing.Path);
        File.WriteAllBytes(temp.Combine("ProPhoto.icc"), [1, 2, 3]);
        var found = registry.Resolve("ProPhoto RGB", [temp.Path]);
        Assert.IsTrue(found.IsAvailable);
        Assert.AreEqual(temp.Combine("ProPhoto.icc"), found.Path);
    }
}
