using System.Windows;

namespace PixelTart.Modules.AssetLibrary;

public readonly record struct ContextMenuPlacementResult(double Left, double Top, bool OpensLeft);

public static class ContextMenuPlacement
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(ContextMenuPlacement), new PropertyMetadata(false, (d, e) =>
        {
            if (d is System.Windows.Controls.MenuItem item && e.NewValue is true)
                AssetLibraryPage.AttachContextSubmenuPlacement(item);
        }));
    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    public static ContextMenuPlacementResult Calculate(Rect parent, Size submenu, Rect workArea)
    {
        var opensLeft = parent.Right + submenu.Width > workArea.Right && parent.Left - submenu.Width >= workArea.Left;
        var left = Math.Clamp(opensLeft ? parent.Left - submenu.Width : parent.Right,
            workArea.Left, Math.Max(workArea.Left, workArea.Right - submenu.Width));
        var top = Math.Clamp(parent.Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - submenu.Height));
        return new(left, top, opensLeft);
    }
}
