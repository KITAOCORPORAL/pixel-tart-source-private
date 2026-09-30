using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.Projects;

public readonly record struct ColorSpaceCamera(double Yaw, double Pitch, double Distance, double PanX, double PanY)
{
    public static ColorSpaceCamera Default => new(ColorSpaceCameraState.DefaultYaw, ColorSpaceCameraState.DefaultPitch, ColorSpaceCameraState.DefaultDistance, 0, 0);
    public ColorSpaceCamera Rotate(double yawDelta, double pitchDelta) => !double.IsFinite(yawDelta) || !double.IsFinite(pitchDelta)
        ? this : this with { Yaw = WrapDegrees(Yaw + yawDelta), Pitch = Math.Clamp(Pitch + pitchDelta, -89, 89) };
    public ColorSpaceCamera Pan(double x, double y) => !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(PanX + x) || !double.IsFinite(PanY + y)
        ? this : this with { PanX = PanX + x, PanY = PanY + y };
    public ColorSpaceCamera Zoom(double factor) => !double.IsFinite(factor) || factor <= 0 ? this
        : this with { Distance = Math.Clamp(Distance / factor, ColorSpaceProjection.MinimumDistance, ColorSpaceProjection.MaximumDistance) };
    public ColorSpaceCamera Reset() => Default;
    private static double WrapDegrees(double value) { value %= 360; return value <= -180 ? value + 360 : value > 180 ? value - 360 : value; }
}
public sealed record ColorSpaceRendererState(ColorSpaceVisualizationModel Model, ColorSpaceCamera Camera, ColorCloudMode Mode, bool ShowMigrationVectors, bool IsFit)
{
    public IReadOnlyList<ColorSpaceCloud> VisibleClouds => (Model with { Mode = Mode }).VisibleClouds;
    public ColorSpaceRendererState WithMode(ColorCloudMode mode) => this with { Model = Model with { Mode = mode }, Mode = mode, IsFit = false };
    public ColorSpaceRendererState Fit(double viewportWidth, double viewportHeight) =>
        ColorSpaceProjection.ValidViewport(viewportWidth, viewportHeight)
            ? this with { Camera = ColorSpaceProjection.Fit(Model with { Mode = Mode }, Camera, viewportWidth, viewportHeight), IsFit = true }
            : this;
}
public static class ColorSpaceRendererContract
{
    public static ColorSpaceRendererState Create(ColorSpaceVisualizationModel model) => new(model, ColorSpaceCamera.Default, model.Mode, true, false);
}

public readonly record struct ColorSpaceProjectedPoint(int PointIndex, double X, double Y, double Depth, VisualRgb24 Color);

/// <summary>Deterministic, bounded projection for a single batched renderer surface.</summary>
public static class ColorSpaceProjection
{
    public const double FitPadding = .10;
    public const double MinimumDistance = .5;
    public const double MaximumDistance = 10;
    private const double ProjectionExtent = .42;
    private const double DistanceNumerator = 1.8;
    public static bool ValidViewport(double width, double height) =>
        double.IsFinite(width) && double.IsFinite(height) && width > 0 && height > 0;

    // Models hold OKLab (L roughly 0..1, a/b roughly -0.4..0.4), not display coordinates.
    // This is the same normalization and orientation path used by Project and Fit.
    private static ColorSpaceCoordinate ViewCoordinate(OklabColor lab, ColorSpaceCamera camera)
    {
        var p = ColorSpaceCoordinate.FromLab(lab);
        var yaw = camera.Yaw * Math.PI / 180; var pitch = camera.Pitch * Math.PI / 180;
        var x = p.X * Math.Cos(yaw) - p.Z * Math.Sin(yaw);
        var z = p.X * Math.Sin(yaw) + p.Z * Math.Cos(yaw);
        return new(x, p.Y * Math.Cos(pitch) - z * Math.Sin(pitch),
            p.Y * Math.Sin(pitch) + z * Math.Cos(pitch));
    }
    private static bool IsFinite(OklabColor lab) => double.IsFinite(lab.L) && double.IsFinite(lab.A) && double.IsFinite(lab.B);
    private static ColorSpaceCamera Sanitize(ColorSpaceCamera camera) => new(
        double.IsFinite(camera.Yaw) ? camera.Yaw % 360 : ColorSpaceCamera.Default.Yaw,
        double.IsFinite(camera.Pitch) ? Math.Clamp(camera.Pitch, -89, 89) : ColorSpaceCamera.Default.Pitch,
        double.IsFinite(camera.Distance) ? Math.Clamp(camera.Distance, MinimumDistance, MaximumDistance) : ColorSpaceCamera.Default.Distance,
        double.IsFinite(camera.PanX) ? camera.PanX : 0, double.IsFinite(camera.PanY) ? camera.PanY : 0);

    private static double DisplayScale(ColorSpaceCamera camera, double width, double height) =>
        Math.Min(width, height) * ProjectionExtent * DistanceNumerator / camera.Distance;

    /// <summary>Convert a DIP drag to the same isotropic view-space units used by Project. Screen Y points down.</summary>
    public static ColorSpaceCamera PanByDisplayDelta(ColorSpaceCamera camera, double deltaX, double deltaY, double width, double height)
    {
        if (!ValidViewport(width, height) || !double.IsFinite(deltaX) || !double.IsFinite(deltaY)) return camera;
        camera = Sanitize(camera);
        var scale = DisplayScale(camera, width, height);
        return scale > 0 && double.IsFinite(scale) ? camera.Pan(deltaX / scale, -deltaY / scale) : camera;
    }

    public static ColorSpaceCamera Fit(ColorSpaceVisualizationModel model, ColorSpaceCamera camera, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(model);
        camera = Sanitize(camera);
        if (!ValidViewport(width, height)) return camera;
        var points = model.VisibleClouds.SelectMany(c => c.Points).Where(p => IsFinite(p.Lab))
            .Select(p => ViewCoordinate(p.Lab, camera)).ToArray();
        if (points.Length == 0) return camera with { PanX = 0, PanY = 0 };
        var minX = points.Min(p => p.X); var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y); var maxY = points.Max(p => p.Y);
        // Isotropic orthographic projection: one view-space unit uses the same DIP scale on both axes.
        // k = min(width,height)*ProjectionExtent*DistanceNumerator/distance.
        // Enforce spanX*k <= width*(1-2*padding), likewise Y; center by view-space pan.
        var shortestSide = Math.Min(width, height);
        var requiredDistance = DistanceNumerator * ProjectionExtent / (1 - 2 * FitPadding) * Math.Max(
            (maxX - minX) * (shortestSide / width),
            (maxY - minY) * (shortestSide / height));
        // FromLab clamps all axes to [-1,1]; rotation spans are bounded by 2*sqrt(3).
        // Thus requiredDistance <= 1.8*.42*2*sqrt(3)/.8 < 3.274, below MaximumDistance.
        // The minimum-distance clamp can only add padding, never exclude a point.
        return camera with { PanX = -(minX + maxX) / 2, PanY = -(minY + maxY) / 2,
            Distance = requiredDistance < 1e-12 ? ColorSpaceCamera.Default.Distance : Math.Clamp(requiredDistance, MinimumDistance, MaximumDistance) };
    }

    public static IReadOnlyList<ColorSpaceProjectedPoint> Project(ColorSpaceCloud cloud, ColorSpaceCamera camera, double width, double height, double pointScale = 1)
    {
        ArgumentNullException.ThrowIfNull(cloud);
        if (!ValidViewport(width, height)) return [];
        pointScale = Math.Clamp(double.IsFinite(pointScale) ? pointScale : 1, .25, 4);
        camera = Sanitize(camera);
        var scale = DisplayScale(camera, width, height) * pointScale;
        var result = new List<ColorSpaceProjectedPoint>(cloud.Points.Count);
        for (var index = 0; index < cloud.Points.Count; index++)
        {
            if (!IsFinite(cloud.Points[index].Lab)) continue;
            var p = ViewCoordinate(cloud.Points[index].Lab, camera);
            result.Add(new(index, width / 2 + (p.X + camera.PanX) * scale,
                height / 2 - (p.Y + camera.PanY) * scale, p.Z, cloud.Points[index].PreviewRgb));
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
