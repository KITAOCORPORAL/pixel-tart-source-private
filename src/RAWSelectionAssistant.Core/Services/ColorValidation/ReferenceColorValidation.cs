using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.Core.Services.ColorValidation;

/// <summary>Validation-only CIELAB/XYZ boundary. XYZ normalized Y=1; never called by PV4/5/6.</summary>
public static class ReferenceColorValidation
{
    public const string Version = "ReferenceLabXyzBradfordV1";
    public static ColorVector3 ToXyz(ReferencePatch patch, ObservationConditions conditions)
    {
        conditions.Validate(requireKnown: true); Finite(patch.Value);
        if (patch.Encoding == ReferenceEncoding.Xyz100) { if(patch.Value.X is <0 or >200 || patch.Value.Y is <0 or >100 || patch.Value.Z is <0 or >200) throw new InvalidDataException("Invalid XYZ100 reference range."); return Scale(patch.Value, .01); }
        if (patch.Encoding != ReferenceEncoding.Lab) throw new InvalidDataException("Unknown reference encoding.");
        if(patch.Value.X is <0 or >100 || Math.Abs(patch.Value.Y)>160 || Math.Abs(patch.Value.Z)>160) throw new InvalidDataException("Invalid Lab reference range.");
        var f = (patch.Value.X + 16) / 116; var white = White(conditions);
        static double Inverse(double t) => t > 6d / 29 ? t * t * t : 3 * Math.Pow(6d / 29, 2) * (t - 4d / 29);
        return new(white.X * Inverse(f + patch.Value.Y / 500), Inverse(f), white.Z * Inverse(f - patch.Value.Z / 200));
    }
    public static ColorVector3 ToLab(ColorVector3 xyz, ObservationConditions conditions)
    {
        conditions.Validate(requireKnown: true); Finite(xyz); var white = White(conditions);
        static double F(double t) => t > Math.Pow(6d / 29, 3) ? Math.Cbrt(t) : t / (3 * Math.Pow(6d / 29, 2)) + 4d / 29;
        var x = F(xyz.X / white.X); var y = F(xyz.Y); var z = F(xyz.Z / white.Z);
        return new(116 * y - 16, 500 * (x - y), 200 * (y - z));
    }
    public static ColorVector3 ToLinearRgb(ReferencePatch patch, ObservationConditions conditions, WorkingColorSpace space)
    {
        if (conditions.ObserverDegrees != 2) throw new NotSupportedException("RGB comparison requires explicit 2-degree reference; Bradford does not convert observers.");
        var xyz = ToXyz(patch, conditions); var destination = LinearRgbColorScience.Get(space);
        // The existing utility supplies a mathematical adaptation, not a measured camera characterization.
        xyz = LinearRgbColorScience.ChromaticAdaptation(conditions.WhiteX!.Value, conditions.WhiteY!.Value,
            destination.WhiteX, destination.WhiteY).Transform(xyz);
        return destination.FromXyz.Transform(xyz); // No clipping: saturated/out-of-gamut references remain inspectable.
    }
    public static double AngularErrorDegrees(ColorVector3 a, ColorVector3 b)
    {
        Finite(a); Finite(b); var norm = Math.Sqrt(Dot(a, a) * Dot(b, b));
        if (!double.IsFinite(norm) || norm <= 0) throw new InvalidDataException("Zero/overflow illumination vector.");
        return Math.Acos(Math.Clamp(Dot(a, b) / norm, -1, 1)) * 180 / Math.PI;
    }
    private static double Dot(ColorVector3 a, ColorVector3 b) => a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    private static ColorVector3 Scale(ColorVector3 a, double b) => new(a.X*b,a.Y*b,a.Z*b);
    private static ColorVector3 White(ObservationConditions c) => new(c.WhiteX!.Value/c.WhiteY!.Value,1,(1-c.WhiteX.Value-c.WhiteY.Value)/c.WhiteY.Value);
    internal static void Finite(ColorVector3 v) { if (!double.IsFinite(v.X)||!double.IsFinite(v.Y)||!double.IsFinite(v.Z)) throw new InvalidDataException("Nonfinite color."); }
}

public enum RawValidationDomain { SensorCodeValues, CameraLinear, DisplayEncoded }
public sealed record PublicRawSampleContract(string SampleId, DataProvenance Provenance, RawValidationDomain Domain,
    ColorVector3 BlackLevels, double WhiteLevel, bool WhiteBalanceApplied, ushort Orientation, string AlgorithmVersion)
{
    public void Validate()
    {
        Provenance.Validate(); ReferenceColorValidation.Finite(BlackLevels);
        if (string.IsNullOrWhiteSpace(SampleId) || AlgorithmVersion != PublicRawValidation.Version || !Enum.IsDefined(Domain) ||
            Orientation is < 1 or > 8 || !double.IsFinite(WhiteLevel) || WhiteLevel <= 0 ||
            new[]{BlackLevels.X,BlackLevels.Y,BlackLevels.Z}.Any(x=>x<0||x>=WhiteLevel)) throw new InvalidDataException("Invalid RAW validation contract.");
    }
}
// This is a validation sample operator, not a demosaicer or a replacement production decoder.
public static class PublicRawValidation
{
    public const string Version = "PublicSensorBlackWbV1";
    public static ColorVector3 SubtractBlack(ColorVector3 code, PublicRawSampleContract contract)
    {
        contract.Validate(); ReferenceColorValidation.Finite(code);
        if (contract.Domain != RawValidationDomain.SensorCodeValues || contract.WhiteBalanceApplied)
            throw new InvalidOperationException("Black subtraction requires unbalanced sensor code values.");
        if (new[]{code.X,code.Y,code.Z}.Any(x=>x<0||x>=contract.WhiteLevel)) throw new InvalidDataException("Invalid/saturated code value; exclude explicitly.");
        // Negative dark noise is retained, not silently clipped.
        return new((code.X-contract.BlackLevels.X)/(contract.WhiteLevel-contract.BlackLevels.X),
            (code.Y-contract.BlackLevels.Y)/(contract.WhiteLevel-contract.BlackLevels.Y),
            (code.Z-contract.BlackLevels.Z)/(contract.WhiteLevel-contract.BlackLevels.Z));
    }
    public static ColorVector3 Balance(ColorVector3 linear, ColorVector3 illuminant, PublicRawSampleContract contract)
    {
        contract.Validate(); ReferenceColorValidation.Finite(linear); ReferenceColorValidation.Finite(illuminant);
        if (contract.Domain != RawValidationDomain.CameraLinear || contract.WhiteBalanceApplied)
            throw new InvalidOperationException("White balance can run once, on camera-linear values only.");
        if (new[]{illuminant.X,illuminant.Y,illuminant.Z}.Any(x=>x<=0) || Math.Abs(illuminant.X+illuminant.Y+illuminant.Z-1)>1e-6)
            throw new InvalidDataException("Illuminant must be positive normalized RGB chromaticity.");
        return new(linear.X*illuminant.Y/illuminant.X,linear.Y,linear.Z*illuminant.Y/illuminant.Z);
    }
}



