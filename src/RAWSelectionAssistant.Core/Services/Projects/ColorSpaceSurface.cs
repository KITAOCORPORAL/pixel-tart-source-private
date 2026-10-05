using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Transient display settings. These are never an image adjustment node.</summary>
public sealed record ColorSpaceViewSettings
{
    public int SurfaceMode { get; init; } // 0 sphere + cloud, 1 affine OKLab cloud, 2 sphere only
    public double SurfaceOpacity { get; init; } = .85;
    public int Background { get; init; }
    public bool ShowGrid { get; init; } = true;
    public bool ShowAxes { get; init; } = true;
    public bool ShowGamut { get; init; }
    public double ChromaMin { get; init; }
    public double ChromaMax { get; init; } = 1;
    public bool SliceEnabled { get; init; }
    public double SliceCenter { get; init; } = .5;
    public double SliceThickness { get; init; } = .25;
    public double RotationX { get; init; }
    public double RotationY { get; init; }
    public double RotationZ { get; init; }
    public bool IsSpherical => SurfaceMode != 1;
    public ColorSpaceViewSettings Normalize() => this with
    {
        SurfaceMode = Math.Clamp(SurfaceMode, 0, 2), Background = Math.Clamp(Background, 0, 2),
        SurfaceOpacity = Clamp(SurfaceOpacity, 0, 1, .85),
        ChromaMin = Clamp(ChromaMin, 0, 1, 0), ChromaMax = Math.Max(Clamp(ChromaMin, 0, 1, 0), Clamp(ChromaMax, 0, 1, 1)),
        SliceCenter = Clamp(SliceCenter, 0, 1, .5), SliceThickness = Clamp(SliceThickness, .01, 1, .25),
        RotationX = Clamp(RotationX, -180, 180, 0), RotationY = Clamp(RotationY, -180, 180, 0), RotationZ = Clamp(RotationZ, -180, 180, 0)
    };
    public bool Includes(OklabColor color)
    {
        if (!ColorSpaceSurface.IsFinite(color)) return false;
        var chroma = ColorSpaceSurface.RelativeChroma(color);
        return chroma >= ChromaMin - 1e-8 && chroma <= ChromaMax + 1e-8 && (!SliceEnabled || Math.Abs(color.L - SliceCenter) <= SliceThickness / 2);
    }
    private static double Clamp(double value, double min, double max, double fallback) => Math.Clamp(double.IsFinite(value) ? value : fallback, min, max);
}

/// <summary>
/// A reversible display-only warp of OKLab into a sphere: y=2L-1,
/// radial distance=(C/Cmax(L,h))*sqrt(1-y²). It does not alter sample Lab or distances.
/// Cmax is the display sRGB gamut boundary, not HSL saturation.
/// </summary>
public static class ColorSpaceSurface
{
    public static bool IsFinite(OklabColor color) => double.IsFinite(color.L) && double.IsFinite(color.A) && double.IsFinite(color.B);
    public static double MaximumChroma(double lightness, double hue)
    {
        if (!double.IsFinite(lightness) || !double.IsFinite(hue) || lightness <= 0 || lightness >= 1) return 0;
        var ca = Math.Cos(hue); var cb = Math.Sin(hue); var low = 0d; var high = .5;
        for (var i = 0; i < 20; i++)
        {
            var mid = (low + high) / 2; var rgb = OklabColorSpace.ToLinear(new(lightness, mid * ca, mid * cb));
            if (rgb.R is >= 0 and <= 1 && rgb.G is >= 0 and <= 1 && rgb.B is >= 0 and <= 1) low = mid; else high = mid;
        }
        return low;
    }
    public static double RelativeChroma(OklabColor lab)
    {
        if (!IsFinite(lab)) return 0;
        if (lab.Chroma < 1e-7) return 0;
        var relative = lab.Chroma / Math.Max(1e-8, MaximumChroma(Math.Clamp(lab.L, 0, 1), Math.Atan2(lab.B, lab.A)));
        return relative > 1 && relative < 1.0001 ? 1 : relative;
    }
    public static ColorSpaceCoordinate ToSphere(OklabColor lab)
    {
        if (!IsFinite(lab)) return new(0, 0, 0);
        var y = 2 * Math.Clamp(lab.L, 0, 1) - 1;
        if (lab.Chroma < 1e-7 || Math.Abs(y) >= 1) return new(0, y, 0);
        var hue = Math.Atan2(lab.B, lab.A);
        // Preserve out-of-gamut radial position; clipping is a display decision, never a Lab edit.
        var radius = RelativeChroma(lab) * Math.Sqrt(Math.Max(0, 1 - y * y));
        return new(radius * Math.Cos(hue), y, radius * Math.Sin(hue));
    }
    public static OklabColor FromSphere(ColorSpaceCoordinate point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z)) return new(.5, 0, 0);
        var l = Math.Clamp((point.Y + 1) / 2, 0, 1);
        var latitudeRadius = Math.Sqrt(Math.Max(0, 1 - point.Y * point.Y));
        if (latitudeRadius < 1e-9) return new(l, 0, 0);
        var hue = Math.Atan2(point.Z, point.X);
        var c = Math.Sqrt(point.X * point.X + point.Z * point.Z) / latitudeRadius * MaximumChroma(l, hue);
        return new(l, c * Math.Cos(hue), c * Math.Sin(hue));
    }
    public static ColorSpaceCoordinate Rotate(ColorSpaceCoordinate p, ColorSpaceCamera camera, ColorSpaceViewSettings settings, bool inverse = false)
    {
        var yaw = (camera.Yaw + settings.RotationY) * Math.PI / 180;
        var pitch = (camera.Pitch + settings.RotationX) * Math.PI / 180;
        var roll = settings.RotationZ * Math.PI / 180;
        static ColorSpaceCoordinate Y(ColorSpaceCoordinate q, double a) => new(q.X * Math.Cos(a) - q.Z * Math.Sin(a), q.Y, q.X * Math.Sin(a) + q.Z * Math.Cos(a));
        static ColorSpaceCoordinate X(ColorSpaceCoordinate q, double a) => new(q.X, q.Y * Math.Cos(a) - q.Z * Math.Sin(a), q.Y * Math.Sin(a) + q.Z * Math.Cos(a));
        static ColorSpaceCoordinate Z(ColorSpaceCoordinate q, double a) => new(q.X * Math.Cos(a) - q.Y * Math.Sin(a), q.X * Math.Sin(a) + q.Y * Math.Cos(a), q.Z);
        return inverse ? Y(X(Z(p, -roll), -pitch), -yaw) : Z(X(Y(p, yaw), pitch), roll);
    }
    public static double Scale(ColorSpaceCamera camera, double width, double height) => Math.Min(width, height) * .42 * 1.8 / Math.Clamp(camera.Distance, .5, 10);
    public static ColorSpaceProjectedPoint Project(OklabColor lab, VisualRgb24 color, int index, ColorSpaceCamera camera, ColorSpaceViewSettings settings, double width, double height)
    {
        var p = Rotate(settings.IsSpherical ? ToSphere(lab) : new(lab.A / .4, (lab.L - .5) * 2, lab.B / .4), camera, settings);
        var scale = Scale(camera, width, height);
        return new(index, width / 2 + (p.X + camera.PanX) * scale, height / 2 - (p.Y + camera.PanY) * scale, p.Z, color);
    }
    public static OklabColor? PickSurface(double x, double y, double width, double height, ColorSpaceCamera camera, ColorSpaceViewSettings settings)
    {
        if (!ColorSpaceProjection.ValidViewport(width, height) || !settings.IsSpherical) return null;
        var scale = Scale(camera, width, height);
        var px = (x - width / 2) / scale - camera.PanX; var py = -(y - height / 2) / scale - camera.PanY;
        var depth2 = 1 - px * px - py * py;
        if (depth2 < 0) return null;
        var lab = FromSphere(Rotate(new(px, py, Math.Sqrt(depth2)), camera, settings, inverse: true));
        return settings.Includes(lab) ? lab : null;
    }
    public static double SelectionWeight(OklabColor color, OklabColor center, double radius, double softness)
    {
        if (!IsFinite(color) || !IsFinite(center) || !double.IsFinite(radius) || radius <= 0) return 0;
        softness = Math.Clamp(double.IsFinite(softness) ? softness : 0, 0, 1);
        var d = Math.Sqrt(Math.Pow(color.L - center.L, 2) + Math.Pow(color.A - center.A, 2) + Math.Pow(color.B - center.B, 2));
        if (d > radius) return 0;
        var inner = radius * (1 - softness);
        if (d <= inner || softness <= 1e-9) return 1;
        var t = (d - inner) / (radius - inner); return 1 - t * t * (3 - 2 * t);
    }
    public static byte[] SelectionMask(VisualPixelBuffer source, OklabColor center, double radius, double softness, CancellationToken token = default)
        => SelectionMask(source, BuildColorIndex(source, token), center, radius, softness, token);

    public static OklabColor[] BuildColorIndex(VisualPixelBuffer source, CancellationToken token = default)
    {
        var colors = new OklabColor[source.PixelCount]; var rgb = source.Rgb24.Span;
        for(var p=0;p<colors.Length;p++)
        {
            if((p&4095)==0)token.ThrowIfCancellationRequested();
            colors[p]=OklabColorSpace.FromSrgb(new VisualRgb24(rgb[p*3],rgb[p*3+1],rgb[p*3+2]));
        }
        return colors;
    }
    public static byte[] SelectionMask(VisualPixelBuffer source, IReadOnlyList<OklabColor> colors, OklabColor center, double radius, double softness, CancellationToken token = default)
    {
        if(colors.Count!=source.PixelCount)throw new ArgumentException("Color index dimensions must match analysis snapshot.",nameof(colors));
        var mask = new byte[source.PixelCount]; var alpha = source.Alpha.Span;
        for (var p = 0; p < mask.Length; p++)
        {
            if ((p & 4095) == 0) token.ThrowIfCancellationRequested();
            if (!alpha.IsEmpty && alpha[p] == 0) continue;
            mask[p] = (byte)Math.Clamp(Math.Round(SelectionWeight(colors[p], center, radius, softness) * (alpha.IsEmpty ? 255 : alpha[p])), 0, 255);
        }
        return mask;
    }
}
