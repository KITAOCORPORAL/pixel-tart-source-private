namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>
/// V4 hard decisions are made once from the double-precision CPU oracle. The integer key
/// is uploaded with each tile; shader arithmetic never reclassifies an approximate OKLab.
/// This is decision encoding, not quantization of color or of classification inputs.
/// </summary>
public static class ProtectionClassifierContract
{
    public const double Epsilon = 1e-4;
    public const double SkinLMin = .28 + Epsilon, SkinLMax = .9 - Epsilon;
    public const double SkinHueMin = 25 + Epsilon, SkinHueMax = 80 - Epsilon;
    public const double SkinChromaMin = .025 + Epsilon, SkinChromaMax = .22 - Epsilon;
    public const double NeutralChroma = .08, HighlightStart = .82, HighlightWidth = .18;
    public const double ShadowEnd = .2, ShadowChroma = .1, MinimumProtection = .08;
    public const int SkinBit = 4, ZoneMask = 3;

    public static double Hue(OklabColor c)
    {
        var hue = Math.Atan2(c.B, c.A) * 180 / Math.PI;
        return hue < 0 ? hue + 360 : hue;
    }

    public static int Classify(OklabColor c)
    {
        var hue = Hue(c);
        var skin = c.L > SkinLMin && c.L < SkinLMax && hue > SkinHueMin && hue < SkinHueMax
            && c.Chroma > SkinChromaMin && c.Chroma < SkinChromaMax;
        return Math.Clamp((int)(c.L * 3), 0, 2) | (skin ? SkinBit : 0);
    }

    public static int Classify(float r, float g, float b) => Classify(OklabColorSpace.FromSrgb(r, g, b));

    public static int ClassifyFloatEquivalent(OklabColor c) => Classify(new OklabColor((float)c.L, (float)c.A, (float)c.B));
}
