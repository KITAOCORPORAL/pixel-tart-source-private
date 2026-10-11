using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Opt-in laboratory candidate only. Never selected by a stored v1/v2 stack or product renderer.</summary>
public static class ColorStudioLinearCandidate
{
    public const int CandidateProcessingVersion = 3;
    public static ColorVector3 Transform(ColorVector3 linearInput, WorkingColorSpace inputSpace,
        double exposureStops = 0, ColorVector3? whiteBalanceGains = null)
    {
        if (!double.IsFinite(exposureStops) || exposureStops < -10 || exposureStops > 10) throw new ArgumentOutOfRangeException(nameof(exposureStops));
        var gains = whiteBalanceGains ?? new ColorVector3(1,1,1);
        if (!double.IsFinite(gains.X) || !double.IsFinite(gains.Y) || !double.IsFinite(gains.Z) || gains.X <= 0 || gains.Y <= 0 || gains.Z <= 0)
            throw new ArgumentOutOfRangeException(nameof(whiteBalanceGains));
        var balanced = new ColorVector3(linearInput.X*gains.X,linearInput.Y*gains.Y,linearInput.Z*gains.Z);
        var work = LinearRgbColorScience.Convert(balanced,inputSpace,WorkingColorSpace.AcesCg);
        var multiplier=Math.Pow(2,exposureStops);
        return new(work.X*multiplier,work.Y*multiplier,work.Z*multiplier);
    }
}
