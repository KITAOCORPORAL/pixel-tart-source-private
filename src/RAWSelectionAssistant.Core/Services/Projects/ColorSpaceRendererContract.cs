namespace RAWSelectionAssistant.Core.Services.Projects;

public readonly record struct ColorSpaceCamera(double Yaw, double Pitch, double Distance, double PanX, double PanY)
{
    public static ColorSpaceCamera Default => new(0, 0, 1, 0, 0);
    public ColorSpaceCamera Rotate(double yawDelta, double pitchDelta) => this with { Yaw = Yaw + yawDelta, Pitch = Math.Clamp(Pitch + pitchDelta, -89, 89) };
    public ColorSpaceCamera Pan(double x, double y) => this with { PanX = PanX + x, PanY = PanY + y };
    public ColorSpaceCamera Zoom(double factor) => this with { Distance = Math.Clamp(Distance / Math.Max(.01, factor), .1, 10) };
    public ColorSpaceCamera Reset() => Default;
}
public sealed record ColorSpaceRendererState(ColorSpaceVisualizationModel Model, ColorSpaceCamera Camera, ColorCloudMode Mode, bool ShowMigrationVectors, bool IsFit)
{
    public ColorSpaceRendererState WithMode(ColorCloudMode mode) => this with { Mode = mode };
    public ColorSpaceRendererState Fit() => this with { Camera = ColorSpaceCamera.Default, IsFit = true };
}
public static class ColorSpaceRendererContract
{
    public static ColorSpaceRendererState Create(ColorSpaceVisualizationModel model) => new(model, ColorSpaceCamera.Default, model.Mode, true, false);
}
