namespace RAWSelectionAssistant.Core.Models.Photography;

public readonly record struct ActualPixelScale(double SourceDpiX, double SourceDpiY, double DevicePixelsPerDipX, double DevicePixelsPerDipY)
{
    // BitmapSource natural width is PixelWidth * 96 / DpiX DIP.  A WPF DIP is
    // rendered as DevicePixelsPerDip physical pixels, so ScaleTransform S
    // produces PixelWidth * 96 / DpiX * DevicePixelsPerDip * S pixels.  At
    // Actual Pixels that value must equal PixelWidth, hence:
    //     S = DpiX / (96 * DevicePixelsPerDip)
    // Print DPI changes natural DIP size, but never changes pixel inspection
    // semantics: the resulting physical pixel ratio remains exactly one.
    public double ScaleX => SourceDpiX / (96d * DevicePixelsPerDipX);
    public double ScaleY => SourceDpiY / (96d * DevicePixelsPerDipY);

    public static double PhysicalPixelsPerSourcePixel(double pixelWidth, double sourceDpi, double devicePixelsPerDip, double zoomScale)
    {
        if (!double.IsFinite(pixelWidth) || pixelWidth <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        var naturalDip = pixelWidth * 96d / sourceDpi;
        var displayDip = naturalDip * devicePixelsPerDip * zoomScale;
        return displayDip / pixelWidth;
    }
}

public readonly record struct ImagePixelGeometry(int PixelWidth, int PixelHeight, double DpiX, double DpiY)
{
    public double NaturalWidthDip => PixelWidth * 96d / DpiX;
    public double NaturalHeightDip => PixelHeight * 96d / DpiY;
    public static ImagePixelGeometry Create(int pixelWidth, int pixelHeight, double dpiX, double dpiY)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        if (!double.IsFinite(dpiX) || dpiX <= 0 || !double.IsFinite(dpiY) || dpiY <= 0) throw new ArgumentOutOfRangeException(nameof(dpiX));
        return new(pixelWidth, pixelHeight, dpiX, dpiY);
    }
}

public static class ActualPixelScaleCalculator
{
    /// <summary>
    /// Converts a WPF zoom ratio into the scale needed for one source pixel to
    /// occupy one physical display pixel. WPF layout uses 96-DPI device
    /// independent pixels, while BitmapSource DPI describes source pixels per
    /// inch; the device scale divides the natural DIP size.
    /// </summary>
    public static double ActualPixelZoom(double sourceDpi, double devicePixelsPerDip)
        => Calculate(sourceDpi, sourceDpi, devicePixelsPerDip, devicePixelsPerDip).ScaleX;

    public static ActualPixelScale Calculate(double sourceDpiX, double sourceDpiY, double devicePixelsPerDipX, double devicePixelsPerDipY)
    {
        if (!double.IsFinite(sourceDpiX) || sourceDpiX <= 0 || !double.IsFinite(sourceDpiY) || sourceDpiY <= 0) throw new ArgumentOutOfRangeException(nameof(sourceDpiX));
        if (!double.IsFinite(devicePixelsPerDipX) || devicePixelsPerDipX <= 0 || !double.IsFinite(devicePixelsPerDipY) || devicePixelsPerDipY <= 0) throw new ArgumentOutOfRangeException(nameof(devicePixelsPerDipX));
        return new(sourceDpiX, sourceDpiY, devicePixelsPerDipX, devicePixelsPerDipY);
    }
}
