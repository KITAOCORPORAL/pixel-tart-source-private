using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PixelTart.Modules.AssetLibrary;

public partial class AssetQueryComposerView : UserControl
{
    public AssetQueryComposerView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is AssetLibraryViewModel oldModel) oldModel.PropertyChanged -= ColorChanged;
            if (e.NewValue is AssetLibraryViewModel newModel) newModel.PropertyChanged += ColorChanged;
            UpdateCursor();
        };
        ColorPlane.SizeChanged += (_, _) => UpdateCursor();
    }

    private void ColorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AssetLibraryViewModel.ColorSaturation) or nameof(AssetLibraryViewModel.ColorBrightness)) UpdateCursor();
    }

    private void UpdateCursor()
    {
        if (DataContext is not AssetLibraryViewModel model) return;
        Canvas.SetLeft(ColorPlaneCursor, model.ColorSaturation * ColorPlane.ActualWidth - 6);
        Canvas.SetTop(ColorPlaneCursor, (1 - model.ColorBrightness) * ColorPlane.ActualHeight - 6);
    }

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
