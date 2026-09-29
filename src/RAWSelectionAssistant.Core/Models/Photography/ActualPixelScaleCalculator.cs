namespace RAWSelectionAssistant.Core.Models.Photography;

public readonly record struct ActualPixelScale(double SourceDpiX, double SourceDpiY, double DevicePixelsPerDipX, double DevicePixelsPerDipY)
{
    public double ScaleX => DevicePixelsPerDipX * SourceDpiX / 96d;
    public double ScaleY => DevicePixelsPerDipY * SourceDpiY / 96d;
}

public static class ActualPixelScaleCalculator
{
    /// <summary>
    /// Converts a WPF zoom ratio into the scale needed for one source pixel to
    /// occupy one physical display pixel. WPF layout uses 96-DPI device
    /// independent pixels, while BitmapSource DPI describes source pixels per
    /// inch; both factors are therefore part of the contract.
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
