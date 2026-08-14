using System.Windows;
using System.Windows.Controls;
using PixelTart.Kernel;

namespace RAWSelectionAssistant.Views;

public sealed class ModuleWorkspaceHost : ContentControl
{
    public static readonly DependencyProperty ModuleRegistryProperty = DependencyProperty.Register(
        nameof(ModuleRegistry), typeof(IModuleRegistry), typeof(ModuleWorkspaceHost), new PropertyMetadata(null, OnRouteChanged));

    public static readonly DependencyProperty RouteProperty = DependencyProperty.Register(
        nameof(Route), typeof(string), typeof(ModuleWorkspaceHost), new PropertyMetadata(string.Empty, OnRouteChanged));

    public IModuleRegistry? ModuleRegistry
    {
        get => (IModuleRegistry?)GetValue(ModuleRegistryProperty);
        set => SetValue(ModuleRegistryProperty, value);
    }

    public string Route
    {
        get => (string)GetValue(RouteProperty);
        set => SetValue(RouteProperty, value);
    }

    private static void OnRouteChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((ModuleWorkspaceHost)dependencyObject).RefreshContent();
    }

    private void RefreshContent()
    {
        Content = ModuleRegistry?.Routes.TryGet(Route, out var descriptor) == true
            ? descriptor.ViewFactory()
            : null;
    }
}
