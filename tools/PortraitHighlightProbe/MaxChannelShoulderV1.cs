using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Color.PhotoWideV1;

namespace PortraitHighlightResearch;

// Research-only, explicitly selected. No production recipe, decoder or UI registration.
// Independent rational shoulder. The common RGB scale preserves linear chromaticity.
public sealed record MaxChannelShoulderV1(
    int Version = 1,
    double ExposureEv = 0,
    double Knee = .75,
    string Input = "RenderedSrgbD65",
    string Output = "SrgbD65RationalMaxShoulderV1")
{
    public MaxChannelShoulderV1 Validate()
    {
        if (Version != 1 || Input != "RenderedSrgbD65" || Output != "SrgbD65RationalMaxShoulderV1" ||
            !double.IsFinite(ExposureEv) || ExposureEv is < -5 or > 5 || !double.IsFinite(Knee) || Knee is <= 0 or >= 1)
            throw new InvalidDataException("Unknown highlight research recipe or range");
        return this;
    }

    public ColorVector3 ApplyRendered(ColorVector3 encoded)
    {
        Validate();
        if (!Finite(encoded) || encoded.X is < 0 or > 1 || encoded.Y is < 0 or > 1 || encoded.Z is < 0 or > 1)
            throw new InvalidDataException("Candidate requires rendered sRGB in [0,1]; scene inputs need ApplyLinear");
        var linear = new ColorVector3(PhotoColorMathV1.DecodeSrgb(encoded.X), PhotoColorMathV1.DecodeSrgb(encoded.Y), PhotoColorMathV1.DecodeSrgb(encoded.Z));
        var mapped = ApplyLinear(linear);
        return new(PhotoColorMathV1.EncodeSrgb(mapped.X), PhotoColorMathV1.EncodeSrgb(mapped.Y), PhotoColorMathV1.EncodeSrgb(mapped.Z));
    }

    // Linear sRGB D65 relative display values; >1 supported, sensor reconstruction absent.
    public ColorVector3 ApplyLinear(ColorVector3 linear)
    {
        Validate();
        if (!Finite(linear) || linear.X < 0 || linear.Y < 0 || linear.Z < 0)
            throw new InvalidDataException("Negative/non-finite linear input is outside candidate contract");
        var gain = Math.Pow(2, ExposureEv);
        var r = linear.X * gain; var g = linear.Y * gain; var b = linear.Z * gain;
        if (!double.IsFinite(r) || !double.IsFinite(g) || !double.IsFinite(b)) throw new InvalidDataException("Exposed values overflow");
        var maximum = Math.Max(r, Math.Max(g, b));
        if (maximum <= Knee) return new(r, g, b);
        var distance = maximum - Knee; var headroom = 1 - Knee;
        var mappedMaximum = Knee + headroom * (distance / (distance + headroom));
        var scale = mappedMaximum / maximum;
        return new(r * scale, g * scale, b * scale);
    }

    private static bool Finite(ColorVector3 value) => double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z);
}
