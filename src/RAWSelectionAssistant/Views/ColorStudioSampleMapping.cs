using System.Windows;
using System.Windows.Media.Imaging;

namespace RAWSelectionAssistant.Views;

/// <summary>Display and picking share image rectangles, including linked comparison panes.</summary>
public static class ColorStudioSampleMapping
{
    public static Rect Viewport(Size canvas, bool sideBySide, bool right) => sideBySide
        ? new Rect(right ? (canvas.Width + 1) / 2 : 0, 0, Math.Max(0, (canvas.Width - 1) / 2), canvas.Height)
        : new Rect(new Point(), canvas);
    public static (BitmapSource Image, int X, int Y)? Map(Point point, Size canvas, string mode, double split,
        BitmapSource? original, BitmapSource? matched, ColorStudioZoomPanState? state = null)
    {
        if (canvas.Width <= 0 || canvas.Height <= 0 || point.X < 0 || point.Y < 0 || point.X >= canvas.Width || point.Y >= canvas.Height) return null;
        var side = mode == "并排对比";
        var right = side ? point.X >= canvas.Width / 2 : mode == "左右对比" && point.X >= canvas.Width * split;
        var image = mode switch { "原片" => original, "仿色" or "仿色结果" => matched, "左右对比" or "并排对比" => right ? matched : original, _ => null };
        if (image is null) return null;
        var viewport = Viewport(canvas, side, right);
        if (!viewport.Contains(point)) return null;
        var reference = original ?? image;
        if (state is null) { state = new(); state.Configure(viewport.Size, new Size(reference.PixelWidth, reference.PixelHeight)); }
        var rect = state.ImageRect(viewport);
        if (rect.Width <= 0 || rect.Height <= 0 || point.X < rect.Left || point.X >= rect.Right || point.Y < rect.Top || point.Y >= rect.Bottom) return null;
        return (image, (int)((point.X - rect.Left) / rect.Width * image.PixelWidth), (int)((point.Y - rect.Top) / rect.Height * image.PixelHeight));
    }
}
