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
        _body.Children.Add(new TextBlock { Text = "OKLab · L 明度 / a 绿–红 / b 蓝–黄", TextWrapping = TextWrapping.Wrap });
        _body.Children.Add(new TextBlock { Text = "离中性轴越远色度越高；容差为 OKLab 球半径。", TextWrapping = TextWrapping.Wrap, FontSize = 11 });
        var density = new ComboBox { Margin = new Thickness(0, 5, 0, 4) };
        density.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("SamplingTiers"));
        density.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, new Binding("SamplingTier") { Mode = BindingMode.TwoWay });
        density.ToolTip = "采样密度：Preview 1024 / Standard 4096 / Dense 16384";
        System.Windows.Automation.AutomationProperties.SetName(density, "点云采样密度"); _body.Children.Add(density);
        AddSlider("点大小", "CloudPointSize", 1, 6, "0.0");
        AddSlider("不透明度", "CloudPointOpacity", .15, 1, "P0");
        AddSlider("颜色容差", "SelectionTolerance", .01, .15, "0.00");
    }
    private void AddSlider(string label, string property, double min, double max, string format)
    {
        var row = new DockPanel(); var title = new TextBlock { Text = label, Width = 68, VerticalAlignment = VerticalAlignment.Center };
        StudioTextExtension.Bind(title,TextBlock.TextProperty,property switch{"CloudPointSize"=>"PointSize","CloudPointOpacity"=>"PointOpacity",_=>"SelectionTolerance"});
        row.Children.Add(title); var value = new TextBlock { Width = 42, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        value.SetBinding(TextBlock.TextProperty, new Binding(property) { StringFormat = "{0:" + format + "}" }); DockPanel.SetDock(value, Dock.Right); row.Children.Add(value);
        var slider = new Slider { Minimum = min, Maximum = max, Margin = new Thickness(4) };
        slider.SetBinding(Slider.ValueProperty, new Binding(property) { Mode = BindingMode.TwoWay });
        System.Windows.Automation.AutomationProperties.SetName(slider, label); row.Children.Add(slider); _body.Children.Add(row);
    }
}
