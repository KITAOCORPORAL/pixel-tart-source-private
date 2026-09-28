using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ColorSpaceDataModelTests
{
    [TestMethod]
    public void BuildsDeterministicOklabCloudAndBoundedSampling()
    {
        var source = Fixture(96, 64);
        var first = ColorSpaceProxyBuilder.Build(source, ColorSpaceSampling.Settings(ColorSpaceSamplingTier.Preview));
        var second = ColorSpaceProxyBuilder.Build(source, ColorSpaceSampling.Settings(ColorSpaceSamplingTier.Preview));
        Assert.IsLessThanOrEqualTo(1024, first.Count);
        Assert.AreEqual(first.SourceFingerprint, second.SourceFingerprint);
        CollectionAssert.AreEqual(first.Points.ToArray(), second.Points.ToArray());
        Assert.IsTrue(first.Points.All(point => point.Lab.L is >= -0.01 and <= 1.01));
    }

    [TestMethod]
    public void EyedropperMapsPixelToCloudAndBackToMembership()
    {
        var source = Fixture(32, 24);
        var cloud = ColorSpaceProxyBuilder.Build(source, new(256));
        var selection = ColorSpaceLinking.FromPixel(cloud, 4, 7, source);
        Assert.AreEqual(4 + 7 * source.Width, selection.SourcePixelIndex);
        Assert.IsGreaterThanOrEqualTo(0, selection.Selection.PointIndex);
        Assert.Contains(selection.SourcePixelIndex, ColorSpaceLinking.ToPixelMembership(cloud, source, selection.Selection.PointIndex));
    }

    [TestMethod]
    public void VisualizationUsesSameV4TransformAndCarriesMigrationProtection()
    {
        var source = Fixture(40, 30); var reference = Fixture(40, 30, invert: true);
        var settings = new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 32, SinkhornIterations: 2);
        var transform = new MatchV4ResolvedTransform(new(.03, .01, -.01), [new(.02, .01, -.01), new(.03, .01, -.01), new(.04, .01, -.01)], settings, "test-transform", 1);
        var model = ColorSpaceVisualizationBuilder.Build(source, reference, transform, ColorSpaceSamplingTier.Preview);
        Assert.AreEqual(transform.TransformHash, model.TransformHash);
        Assert.HasCount(model.Source.Count, model.MigrationVectors);
        Assert.IsTrue(model.MigrationVectors.Any(vector => vector.Distance > 0));
        Assert.IsTrue(model.MigrationVectors.All(vector => Enum.IsDefined(typeof(ColorProtectionState), vector.Protection)));
        Assert.HasCount(3, model.VisibleClouds);
    }

    [TestMethod]
    public void StrengthZeroProducesZeroMigrationVectors()
    {
        var source = Fixture(24, 16); var reference = Fixture(24, 16, invert: true);
        var settings = new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 16, SinkhornIterations: 1);
        var transform = new MatchV4ResolvedTransform(new(.3, .1, -.1), [new(.3, .1, -.1), new(.3, .1, -.1), new(.3, .1, -.1)], settings, "zero", 0);
        var model = ColorSpaceVisualizationBuilder.Build(source, reference, transform, ColorSpaceSamplingTier.Preview);
        Assert.IsTrue(model.MigrationVectors.All(vector => vector.Distance <= .000001));
    }

    private static VisualPixelBuffer Fixture(int width, int height, bool invert = false)
    {
        var values = new byte[width * height * 3];
        for (var i = 0; i < values.Length; i += 3)
        {
            var pixel = i / 3; var r = (byte)(pixel % 256); var g = (byte)((pixel * 3) % 256); var b = (byte)((pixel * 7) % 256);
            values[i] = invert ? (byte)(255 - r) : r; values[i + 1] = invert ? (byte)(255 - g) : g; values[i + 2] = invert ? (byte)(255 - b) : b;
        }
        return new(width, height, values);
    }
}
