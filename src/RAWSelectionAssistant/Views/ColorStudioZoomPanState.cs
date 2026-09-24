using System.Windows;

namespace RAWSelectionAssistant.Views;

/// <summary>One image-space viewport. Zoom is logical DIP per source pixel.</summary>
public sealed class ColorStudioZoomPanState
{
    public double Zoom { get; private set; } = 1;
    public double PanX { get; private set; }
    public double PanY { get; private set; }
    public bool IsFit { get; private set; } = true;
    public Size ImageSize { get; private set; }
    public Size ViewportSize { get; private set; }
    public event EventHandler? Changed;
    public void Configure(Size viewport, Size image)
    {
        if (viewport == ViewportSize && image == ImageSize) return;
        ViewportSize = viewport; ImageSize = image;
        if (IsFit) SetFitScale();
        Clamp(); Changed?.Invoke(this, EventArgs.Empty);
    }
    public void Fit() { IsFit = true; PanX = PanY = 0; SetFitScale(); Changed?.Invoke(this, EventArgs.Empty); }
    private void SetFitScale()
    {
        if (ImageSize.Width > 0 && ImageSize.Height > 0 && ViewportSize.Width > 0 && ViewportSize.Height > 0)
            Zoom = Math.Min(ViewportSize.Width / ImageSize.Width, ViewportSize.Height / ImageSize.Height);
    }
    public void SetZoom(double zoom) => ZoomAt(new Point(ViewportSize.Width / 2, ViewportSize.Height / 2), zoom);
    public void ZoomAbout(Point cursor, double factor) => ZoomAt(cursor, Zoom * factor);
    private void ZoomAt(Point cursor, double value)
    {
        if (!double.IsFinite(value) || value <= 0 || Zoom <= 0) return;
        var next = Math.Clamp(value, .25, 4);
        var cx = ViewportSize.Width / 2; var cy = ViewportSize.Height / 2;
        PanX = cursor.X - cx - (cursor.X - cx - PanX) * next / Zoom;
        PanY = cursor.Y - cy - (cursor.Y - cy - PanY) * next / Zoom;
        Zoom = next; IsFit = false; Clamp(); Changed?.Invoke(this, EventArgs.Empty);
    }
    public void PanBy(Vector delta)
    {
        if (!double.IsFinite(delta.X) || !double.IsFinite(delta.Y)) return;
        PanX += delta.X; PanY += delta.Y; Clamp(); Changed?.Invoke(this, EventArgs.Empty);
    }
    private void Clamp()
    {
        var mx = Math.Max(0, (ImageSize.Width * Zoom - ViewportSize.Width) / 2);
        var my = Math.Max(0, (ImageSize.Height * Zoom - ViewportSize.Height) / 2);
        PanX = Math.Clamp(PanX, -mx, mx); PanY = Math.Clamp(PanY, -my, my);
    }
    public Rect ImageRect(Rect viewport) => new(viewport.Left + (viewport.Width - ImageSize.Width * Zoom) / 2 + PanX,
        viewport.Top + (viewport.Height - ImageSize.Height * Zoom) / 2 + PanY, ImageSize.Width * Zoom, ImageSize.Height * Zoom);
}
