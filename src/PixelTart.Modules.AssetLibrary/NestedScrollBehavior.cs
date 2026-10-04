using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PixelTart.Modules.AssetLibrary;

/// <summary>A bounded editor owns scrolling even when its non-scrolling rule tree handles wheel input.</summary>
public static class NestedScrollBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(NestedScrollBehavior), new PropertyMetadata(false, Changed));
    public static bool GetIsEnabled(DependencyObject value) => (bool)value.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject value, bool enabled) => value.SetValue(IsEnabledProperty, enabled);
    private static void Changed(DependencyObject value, DependencyPropertyChangedEventArgs e)
    {
        if (value is not ScrollViewer scroll) return;
        if ((bool)e.NewValue) { scroll.PreviewMouseWheel += Wheel; scroll.PreviewKeyDown += Key; }
        else { scroll.PreviewMouseWheel -= Wheel; scroll.PreviewKeyDown -= Key; }
    }
    private static void Wheel(object sender, MouseWheelEventArgs e)
    {
        var scroll = (ScrollViewer)sender;
        if (scroll.ScrollableHeight <= 0) return;
        scroll.ScrollToVerticalOffset(scroll.VerticalOffset - e.Delta / 120d * 48);
        e.Handled = true;
    }
    private static void Key(object sender, KeyEventArgs e)
    {
        var scroll = (ScrollViewer)sender;
        if (e.Key == System.Windows.Input.Key.PageDown) scroll.PageDown();
        else if (e.Key == System.Windows.Input.Key.PageUp) scroll.PageUp();
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == System.Windows.Input.Key.End) scroll.ScrollToEnd();
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == System.Windows.Input.Key.Home) scroll.ScrollToHome();
        else return;
        e.Handled = true;
    }
}
