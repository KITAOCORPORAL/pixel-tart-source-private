using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PixelTart.Modules.AssetLibrary;

public partial class AssetQueryComposerView : UserControl
{
    public AssetQueryComposerView() => InitializeComponent();

    private void ColorPlane_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement plane) return;
        plane.CaptureMouse();
        UpdateColorPlane(plane, e.GetPosition(plane));
        e.Handled = true;
    }

    private void ColorPlane_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement plane || !plane.IsMouseCaptured) return;
        UpdateColorPlane(plane, e.GetPosition(plane));
    }

    private void ColorPlane_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement plane) return;
        UpdateColorPlane(plane, e.GetPosition(plane));
        plane.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void UpdateColorPlane(FrameworkElement plane, Point position)
    {
        if (DataContext is not AssetLibraryViewModel viewModel || plane.ActualWidth <= 0 || plane.ActualHeight <= 0) return;
        viewModel.ColorSaturation = Math.Clamp(position.X / plane.ActualWidth, 0, 1);
        viewModel.ColorBrightness = 1 - Math.Clamp(position.Y / plane.ActualHeight, 0, 1);
        Canvas.SetLeft(ColorPlaneCursor, Math.Clamp(position.X - 6, -6, plane.ActualWidth - 6));
        Canvas.SetTop(ColorPlaneCursor, Math.Clamp(position.Y - 6, -6, plane.ActualHeight - 6));
    }
}
