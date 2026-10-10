namespace RAWSelectionAssistant.Core.Services.Color;

public readonly record struct ColorVector3(double X, double Y, double Z);

public readonly record struct ColorMatrix3x3(
    double M11, double M12, double M13,
    double M21, double M22, double M23,
    double M31, double M32, double M33)
{
    public ColorVector3 Transform(ColorVector3 value) => new(
        M11 * value.X + M12 * value.Y + M13 * value.Z,
        M21 * value.X + M22 * value.Y + M23 * value.Z,
        M31 * value.X + M32 * value.Y + M33 * value.Z);

    public ColorMatrix3x3 Multiply(ColorMatrix3x3 right) => new(
        M11 * right.M11 + M12 * right.M21 + M13 * right.M31,
        M11 * right.M12 + M12 * right.M22 + M13 * right.M32,
        M11 * right.M13 + M12 * right.M23 + M13 * right.M33,
        M21 * right.M11 + M22 * right.M21 + M23 * right.M31,
        M21 * right.M12 + M22 * right.M22 + M23 * right.M32,
        M21 * right.M13 + M22 * right.M23 + M23 * right.M33,
        M31 * right.M11 + M32 * right.M21 + M33 * right.M31,
        M31 * right.M12 + M32 * right.M22 + M33 * right.M32,
        M31 * right.M13 + M32 * right.M23 + M33 * right.M33);

    public static ColorMatrix3x3 Diagonal(ColorVector3 value) => new(
        value.X, 0, 0,
        0, value.Y, 0,
        0, 0, value.Z);
}

public sealed record LinearRgbSpaceDefinition(
    WorkingColorSpace Id,
    string DisplayName,
    double WhiteX,
    double WhiteY,
    ColorMatrix3x3 ToXyz,
    ColorMatrix3x3 FromXyz,
    string Primaries,
    string TransferFunction = "Linear");

/// <summary>
/// Public-standard linear RGB transforms used to evaluate a future working
/// space. This class is not wired into historical ProcessingVersion 1/2 renders.
/// Values outside 0..1 are preserved; output clipping belongs to a later output
/// transform, not this conversion.
/// </summary>
public static class LinearRgbColorScience
{
    private static readonly ColorMatrix3x3 Bradford = new(
        .8951, .2664, -.1614,
        -.7502, 1.7135, .0367,
        .0389, -.0685, 1.0296);
    private static readonly ColorMatrix3x3 BradfordInverse = new(
        .9869929, -.1470543, .1599627,
        .4323053, .5183603, .0492912,
        -.0085287, .0400428, .9684867);

    private static readonly IReadOnlyDictionary<WorkingColorSpace, LinearRgbSpaceDefinition> Definitions =
        new Dictionary<WorkingColorSpace, LinearRgbSpaceDefinition>
        {
            [WorkingColorSpace.LinearSRgb] = new(
                WorkingColorSpace.LinearSRgb, "Linear sRGB", .3127, .3290,
                new(.4124564, .3575761, .1804375, .2126729, .7151522, .0721750, .0193339, .1191920, .9503041),
                new(3.2404542, -1.5371385, -.4985314, -.9692660, 1.8760108, .0415560, .0556434, -.2040259, 1.0572252),
                "IEC 61966-2-1 / D65"),
            [WorkingColorSpace.ProPhotoRgb] = new(
                WorkingColorSpace.ProPhotoRgb, "Linear ProPhoto RGB", .3457, .3585,
                new(.7976749, .1351917, .0313534, .2880402, .7118741, .0000857, 0, 0, .8252100),
                new(1.3459433, -.2556075, -.0511118, -.5445989, 1.5081673, .0205351, 0, 0, 1.2118128),
                "ROMM RGB / D50"),
            [WorkingColorSpace.AcesCg] = new(
                WorkingColorSpace.AcesCg, "ACEScg", .32168, .33767,
                new(.6624541811, .1340042065, .1561876870, .2722287168, .6740817658, .0536895174, -.0055746495, .0040607335, 1.0103391003),
                new(1.6410233797, -.3248032942, -.2364246952, -.6636628587, 1.6153315917, .0167563477, .0117218943, -.0082844420, .9883948585),
                "ACES AP1 / D60")
        };

    public static IReadOnlyCollection<LinearRgbSpaceDefinition> SupportedSpaces => Definitions.Values.ToArray();

    public static LinearRgbSpaceDefinition Get(WorkingColorSpace space) => Definitions.TryGetValue(space, out var definition)
        ? definition
        : throw new NotSupportedException($"工作空间 {space} 尚无已验证的线性 RGB 变换。");

    public static ColorVector3 Convert(ColorVector3 linearRgb, WorkingColorSpace source, WorkingColorSpace destination)
    {
        ValidateFinite(linearRgb);
        if (source == destination) return linearRgb;
        var from = Get(source); var to = Get(destination);
        var xyz = from.ToXyz.Transform(linearRgb);
        if (Math.Abs(from.WhiteX - to.WhiteX) > 1e-12 || Math.Abs(from.WhiteY - to.WhiteY) > 1e-12)
            xyz = ChromaticAdaptation(from.WhiteX, from.WhiteY, to.WhiteX, to.WhiteY).Transform(xyz);
        return to.FromXyz.Transform(xyz);
    }

    public static ColorMatrix3x3 ChromaticAdaptation(double sourceX, double sourceY, double destinationX, double destinationY)
    {
        var sourceWhite = XyToXyz(sourceX, sourceY); var destinationWhite = XyToXyz(destinationX, destinationY);
        var sourceCone = Bradford.Transform(sourceWhite); var destinationCone = Bradford.Transform(destinationWhite);
        if (Math.Abs(sourceCone.X) < 1e-12 || Math.Abs(sourceCone.Y) < 1e-12 || Math.Abs(sourceCone.Z) < 1e-12)
            throw new ArgumentException("源白点无法进行 Bradford 色适应。");
        var scale = new ColorVector3(destinationCone.X / sourceCone.X, destinationCone.Y / sourceCone.Y, destinationCone.Z / sourceCone.Z);
        return BradfordInverse.Multiply(ColorMatrix3x3.Diagonal(scale)).Multiply(Bradford);
    }

    private static ColorVector3 XyToXyz(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || x <= 0 || y <= 0 || x + y >= 1)
            throw new ArgumentOutOfRangeException(nameof(x), "白点 xy 坐标无效。");
        return new(x / y, 1, (1 - x - y) / y);
    }

    private static void ValidateFinite(ColorVector3 value)
    {
        if (!double.IsFinite(value.X) || !double.IsFinite(value.Y) || !double.IsFinite(value.Z))
            throw new ArgumentException("颜色分量必须是有限数值。", nameof(value));
    }
}
