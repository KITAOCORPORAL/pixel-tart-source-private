using RAWSelectionAssistant.Core.Services.Photos;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class PhotographyInteractionClosureTests
{
    [TestMethod]
    public void ExternalMetadataDoesNotPretendFileCreationIsImportTime()
    {
        var metadata = PhotoMetadata.Unavailable("sample.jpg");
        Assert.AreEqual("—", metadata.ImportedTime);
    }

    [TestMethod]
    public void FilmSnapshotIsImmutableWhenCopied()
    {
        var original = new PixelTartFilmSettings(Enabled: true, ProfileId: "PT-W01", GrainAmount: 40);
        var copy = original with { };
        Assert.AreEqual(original, copy);
        Assert.AreNotSame(original, copy);
    }

    [TestMethod]
    public void ReferenceLookNormalizesIndependentWeightedSources()
    {
        var look = Fixtures.Look(1, 1).Look;
        var normalized = look.Normalize();
        Assert.AreEqual(1d, normalized.ReferenceSources.Sum(source => source.Weight), 1e-9);
    }
}
