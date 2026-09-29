using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

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

public readonly record struct ColorSpaceProjectedPoint(int PointIndex, double X, double Y, double Depth, VisualRgb24 Color);

/// <summary>Deterministic, bounded projection for a single batched renderer surface.</summary>
public static class ColorSpaceProjection
{
    public static IReadOnlyList<ColorSpaceProjectedPoint> Project(ColorSpaceCloud cloud, ColorSpaceCamera camera, double width, double height, double pointScale = 1)
    {
        ArgumentNullException.ThrowIfNull(cloud);
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return [];
        pointScale = Math.Clamp(double.IsFinite(pointScale) ? pointScale : 1, .25, 4);
        var yaw = camera.Yaw * Math.PI / 180; var pitch = camera.Pitch * Math.PI / 180;
        var cosYaw = Math.Cos(yaw); var sinYaw = Math.Sin(yaw); var cosPitch = Math.Cos(pitch); var sinPitch = Math.Sin(pitch);
        var result = new List<ColorSpaceProjectedPoint>(cloud.Points.Count);
        for (var index = 0; index < cloud.Points.Count; index++)
        {
            var coordinate = ColorSpaceCoordinate.FromLab(cloud.Points[index].Lab);
            var x = coordinate.X; var y = coordinate.Y; var z = coordinate.Z;
            var yawX = x * cosYaw - z * sinYaw; var yawZ = x * sinYaw + z * cosYaw;
            var screenY = y * cosPitch - yawZ * sinPitch; var depth = y * sinPitch + yawZ * cosPitch;
            var scale = Math.Clamp(1.8 / Math.Max(.5, camera.Distance), .25, 4) * pointScale;
            result.Add(new(index, width / 2 + (yawX + camera.PanX) * width * .42 * scale,
                height / 2 - (screenY + camera.PanY) * height * .42 * scale, depth, cloud.Points[index].PreviewRgb));
        }
        return result;
    }

    public static int HitTest(IReadOnlyList<ColorSpaceProjectedPoint> points, double x, double y, double tolerance = 12)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || tolerance <= 0) return -1;
        var radius = tolerance * tolerance; var best = -1; var bestDepth = double.NegativeInfinity; var bestDistance = double.PositiveInfinity;
        foreach (var point in points)
        {
            var dx = point.X - x; var dy = point.Y - y; var distance = dx * dx + dy * dy;
            if (distance > radius || distance > bestDistance) continue;
            if (distance < bestDistance || point.Depth > bestDepth) { best = point.PointIndex; bestDistance = distance; bestDepth = point.Depth; }
        }
        return best;
    }
}
