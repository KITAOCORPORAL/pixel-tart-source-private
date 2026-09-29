using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RAWSelectionAssistant.WpfTests;

internal sealed record UiVisualDiffMetrics(
    int Width,
    int Height,
    long ChangedPixelCount,
    double ChangedPixelRatio,
    double MeanAbsoluteDifference,
    byte MaxChannelDifference,
    Rect? ChangedBoundingBox,
    string Status);

internal static class UiVisualDiffEngine
{
    internal const byte PerChannelTolerance = 3;
    internal const double ChangedPixelRatioThreshold = 0.0025;

    internal static UiVisualDiffMetrics Compare(string baselinePath, string receivedPath, string diffPath)
    {
        var baseline = Load(baselinePath); var received = Load(receivedPath);
        if (baseline.PixelWidth != received.PixelWidth || baseline.PixelHeight != received.PixelHeight)
        {
            return new(baseline.PixelWidth, baseline.PixelHeight, 0, 1, 0, 0, null, "VISUAL_SIZE_MISMATCH");
        }
        var width = baseline.PixelWidth; var height = baseline.PixelHeight;
        var pixelsA = new byte[width * height * 4]; var pixelsB = new byte[pixelsA.Length];
        baseline.CopyPixels(pixelsA, width * 4, 0); received.CopyPixels(pixelsB, width * 4, 0);
        var diff = new byte[pixelsA.Length]; long changed = 0; double sum = 0; byte max = 0;
        var minX = width; var minY = height; var maxX = -1; var maxY = -1;
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var offset = (y * width + x) * 4;
            var delta = Math.Max(Math.Abs(pixelsA[offset] - pixelsB[offset]), Math.Max(Math.Abs(pixelsA[offset + 1] - pixelsB[offset + 1]), Math.Abs(pixelsA[offset + 2] - pixelsB[offset + 2])));
            sum += delta;
            var different = delta > PerChannelTolerance;
            if (different) { changed++; minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
            max = Math.Max(max, (byte)delta);
            var intensity = different ? (byte)255 : (byte)(pixelsA[offset] * .22);
            diff[offset] = intensity; diff[offset + 1] = different ? (byte)32 : (byte)(pixelsA[offset + 1] * .12); diff[offset + 2] = different ? (byte)32 : (byte)(pixelsA[offset + 2] * .12); diff[offset + 3] = 255;
        }
        var ratio = changed / (double)(width * height); Rect? box = maxX < 0 ? null : new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        Save(diffPath, diff, width, height);
        return new(width, height, changed, ratio, sum / (width * height), max, box, ratio <= ChangedPixelRatioThreshold ? "PASS" : "FAIL");
    }

    private static BitmapSource Load(string path)
    {
        using var stream = File.OpenRead(path);
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
    }
    private static void Save(string path, byte[] pixels, int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); encoder.Save(stream);
    }
}
