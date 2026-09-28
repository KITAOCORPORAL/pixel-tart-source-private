using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class MatchV4NumericalTests
{
    [TestMethod]
    public void ProtectionBoundaryEpsilonKeepsBoundaryValuesOutOfSkinBranch()
    {
        var e = MatchV4NumericalContract.ProtectionBoundaryEpsilon;
        var transform = new MatchV4ResolvedTransform(new(.01, .01, .01), [new(.01, .01, .01), new(.01, .01, .01), new(.01, .01, .01)], new(), "test");
        var values = new float[] { .28001f, .28001f, .28001f };
        MatchV4PixelApplication.Apply(values, transform);
        Assert.IsTrue(values.All(float.IsFinite));
        Assert.IsGreaterThan(0f, values[0]);
        Assert.IsLessThanOrEqualTo(1f, values[0]);
        Assert.IsGreaterThan(0d, e);
    }

    [TestMethod]
    public void CpuOracleRemainsFiniteForNegativeLmsAndNearZeroChannels()
    {
        var transform = new MatchV4ResolvedTransform(new(.04, .02, -.03), [new(.02, -.01, .01), new(.03, .01, -.02), new(.01, .02, .01)], new(), "negative-lms");
        var values = new float[] { 0f, 1e-7f, .0001f, .02f, .5f, 1f };
        MatchV4PixelApplication.Apply(values, transform);
        Assert.IsTrue(values.All(float.IsFinite));
        Assert.IsTrue(values.All(value => value is >= 0 and <= 1));
    }
}
