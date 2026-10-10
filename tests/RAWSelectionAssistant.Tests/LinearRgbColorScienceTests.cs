using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class LinearRgbColorScienceTests
{
    [TestMethod]
    public void SupportedSpacesDeclareWhitePointPrimariesAndLinearTransfer()
    {
        var spaces = LinearRgbColorScience.SupportedSpaces.ToDictionary(item => item.Id);
        CollectionAssert.AreEquivalent(new[] { WorkingColorSpace.LinearSRgb, WorkingColorSpace.ProPhotoRgb, WorkingColorSpace.AcesCg }, spaces.Keys.ToArray());
        Assert.AreEqual(.3457, spaces[WorkingColorSpace.ProPhotoRgb].WhiteX, 1e-12);
        Assert.AreEqual(.32168, spaces[WorkingColorSpace.AcesCg].WhiteX, 1e-12);
        Assert.IsTrue(spaces.Values.All(item => item.TransferFunction == "Linear" && item.Primaries.Length > 0));
    }

    [TestMethod]
    public void IdentityConversionIsExactAndRejectsNonFiniteInputs()
    {
        var value = new ColorVector3(-.05, .18, 1.12);
        Assert.AreEqual(value, LinearRgbColorScience.Convert(value, WorkingColorSpace.AcesCg, WorkingColorSpace.AcesCg));
        Assert.ThrowsExactly<ArgumentException>(() => LinearRgbColorScience.Convert(new(double.NaN, 0, 0), WorkingColorSpace.AcesCg, WorkingColorSpace.ProPhotoRgb));
        Assert.ThrowsExactly<NotSupportedException>(() => LinearRgbColorScience.Convert(value, WorkingColorSpace.SRgb, WorkingColorSpace.AcesCg));
    }

    [TestMethod]
    public void ProPhotoAndAcesCgRoundTripPreservesWideGamutLinearSamples()
    {
        var samples = new[]
        {
            new ColorVector3(.18, .18, .18),
            new ColorVector3(1, 0, 0),
            new ColorVector3(0, 1, 0),
            new ColorVector3(0, 0, 1),
            new ColorVector3(1.2, -.04, .72)
        };
        foreach (var sample in samples)
        {
            var aces = LinearRgbColorScience.Convert(sample, WorkingColorSpace.ProPhotoRgb, WorkingColorSpace.AcesCg);
            var restored = LinearRgbColorScience.Convert(aces, WorkingColorSpace.AcesCg, WorkingColorSpace.ProPhotoRgb);
            AssertVector(sample, restored, 3e-6);
        }
    }

    [TestMethod]
    public void BradfordAdaptationKeepsNeutralAxisNeutralAcrossD50D60D65()
    {
        foreach (var target in new[] { WorkingColorSpace.AcesCg, WorkingColorSpace.LinearSRgb })
        {
            var value = LinearRgbColorScience.Convert(new(.18, .18, .18), WorkingColorSpace.ProPhotoRgb, target);
            Assert.AreEqual(value.X, value.Y, 2e-4); Assert.AreEqual(value.Y, value.Z, 2e-4);
        }
    }

    [TestMethod]
    public void ConversionDoesNotClipOutOfRangeWorkingValues()
    {
        var converted = LinearRgbColorScience.Convert(new(1.4, -.1, .2), WorkingColorSpace.AcesCg, WorkingColorSpace.ProPhotoRgb);
        Assert.IsTrue(converted.X > 1 || converted.Y < 0 || converted.Z < 0,
            "Working-space conversion must preserve out-of-range energy for a later gamut/output stage.");
    }

    private static void AssertVector(ColorVector3 expected, ColorVector3 actual, double tolerance)
    {
        Assert.AreEqual(expected.X, actual.X, tolerance);
        Assert.AreEqual(expected.Y, actual.Y, tolerance);
        Assert.AreEqual(expected.Z, actual.Z, tolerance);
    }
}
