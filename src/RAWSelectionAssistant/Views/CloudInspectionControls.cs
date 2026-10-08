using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace RAWSelectionAssistant.Views;

/// <summary>Both viewports edit the same transient inspection settings, never the adjustment stack.</summary>
public sealed class CloudInspectionControls : Expander
{
    private readonly StackPanel _body = new() { Margin = new Thickness(4) };
    public CloudInspectionControls()
    {
        Header = "点云显示与容差";
        StudioTextExtension.Bind(this,HeaderProperty,"CloudSettings");
        IsExpanded = false;
        Content = _body;
        Margin = new Thickness(0, 4, 0, 4);
        var axes = new TextBlock { TextWrapping = TextWrapping.Wrap }; StudioTextExtension.Bind(axes, TextBlock.TextProperty, "CloudAxesHelp"); _body.Children.Add(axes);
        var help = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 11 }; StudioTextExtension.Bind(help, TextBlock.TextProperty, "CloudToleranceHelp"); _body.Children.Add(help);
        var density = new ComboBox { Margin = new Thickness(0, 5, 0, 4) };
        density.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("SamplingTiers"));
        density.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, new Binding("SamplingTier") { Mode = BindingMode.TwoWay });
        StudioTextExtension.Bind(density, ToolTipProperty, "CloudDensityHelp");
        StudioTextExtension.Bind(density, System.Windows.Automation.AutomationProperties.NameProperty, "CloudDensity"); _body.Children.Add(density);
        AddSlider("点大小", "CloudPointSize", 1, 6, "0.0");
        AddSlider("不透明度", "CloudPointOpacity", .15, 1, "P0");
        AddSlider("颜色容差", "SelectionTolerance", .01, .15, "0.00");
    }
    private void AddSlider(string label, string property, double min, double max, string format)
    {
        var row = new DockPanel(); var title = new TextBlock { Text = label, Width = 88, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        StudioTextExtension.Bind(title,TextBlock.TextProperty,property switch{"CloudPointSize"=>"PointSize","CloudPointOpacity"=>"PointOpacity",_=>"SelectionTolerance"});
        row.Children.Add(title); var value = new TextBlock { Width = 42, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        value.SetBinding(TextBlock.TextProperty, new Binding(property) { StringFormat = "{0:" + format + "}" }); DockPanel.SetDock(value, Dock.Right); row.Children.Add(value);
        var slider = new Slider { Minimum = min, Maximum = max, Margin = new Thickness(4) };
        slider.SetBinding(Slider.ValueProperty, new Binding(property) { Mode = BindingMode.TwoWay });
        StudioTextExtension.Bind(slider, System.Windows.Automation.AutomationProperties.NameProperty, property switch { "CloudPointSize" => "PointSize", "CloudPointOpacity" => "PointOpacity", _ => "CloudTolerance" }); row.Children.Add(slider); _body.Children.Add(row);
    }
}
