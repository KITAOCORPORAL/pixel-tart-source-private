namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Deterministic, UI-framework-neutral camera state for the bounded OKLab view.</summary>
public sealed class ColorSpaceCameraState
{
    public const double DefaultYaw = -35;
    public const double DefaultPitch = 18;
    public const double DefaultDistance = 2.4;
    public double Yaw { get; private set; } = DefaultYaw;
    public double Pitch { get; private set; } = DefaultPitch;
    public double Distance { get; private set; } = DefaultDistance;

    public void Reset()
    {
        Yaw = DefaultYaw; Pitch = DefaultPitch; Distance = DefaultDistance;
    }

    public void Orbit(double yawDelta, double pitchDelta)
    {
        if (!double.IsFinite(yawDelta) || !double.IsFinite(pitchDelta)) return;
        Yaw = WrapDegrees(Yaw + yawDelta);
        Pitch = Math.Clamp(Pitch + pitchDelta, -89, 89);
    }

    public void Zoom(double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0) return;
        Distance = Math.Clamp(Distance / factor, .5, 8);
    }

    private static double WrapDegrees(double value)
    {
        value %= 360;
        return value <= -180 ? value + 360 : value > 180 ? value - 360 : value;
    }
}

public readonly record struct ColorSpaceCoordinate(double X, double Y, double Z)
{
    public static ColorSpaceCoordinate FromLab(OklabColor lab) => new(
        Math.Clamp(lab.A / .4, -1, 1),
        Math.Clamp((lab.L - .5) * 2, -1, 1),
        Math.Clamp(lab.B / .4, -1, 1));
}

