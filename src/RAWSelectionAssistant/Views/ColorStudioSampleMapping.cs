using System.Windows;
using System.Windows.Media.Imaging;

namespace RAWSelectionAssistant.Views;

/// <summary>Maps a click to the bitmap actually visible in each comparison viewport.</summary>
public static class ColorStudioSampleMapping
{
    public static (BitmapSource Image, int X, int Y)? Map(
        Point point, Size canvas, string mode, double split,
        BitmapSource? original, BitmapSource? matched)
    {
        var sideBySide = mode == "并排对比";
        var onRight = sideBySide ? point.X >= canvas.Width / 2 : mode == "左右对比" && point.X >= canvas.Width * split;
        var image = mode switch
        {
            "原片" => original,
            "仿色结果" => matched,
            "左右对比" or "并排对比" => onRight ? matched : original,
            _ => null
        };
        if (image is null || canvas.Width <= 0 || canvas.Height <= 0) return null;
        var viewport = sideBySide
            ? new Rect(onRight ? canvas.Width / 2 + .5 : 0, 0, canvas.Width / 2 - .5, canvas.Height)
            : new Rect(new Point(), canvas);
        if (viewport.Width <= 0 || !viewport.Contains(point)) return null;
        var scale = Math.Min(viewport.Width / image.PixelWidth, viewport.Height / image.PixelHeight);
        var width = image.PixelWidth * scale; var height = image.PixelHeight * scale;
        var left = viewport.Left + (viewport.Width - width) / 2;
        var top = viewport.Top + (viewport.Height - height) / 2;
        if (point.X < left || point.X >= left + width || point.Y < top || point.Y >= top + height) return null;
        var x = (int)((point.X - left) / scale); var y = (int)((point.Y - top) / scale);
        return (image, x, y);
    }
}
