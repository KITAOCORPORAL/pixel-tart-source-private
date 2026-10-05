using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace RAWSelectionAssistant.Views;

public class StudioSpaceControls : StackPanel
{
    public StudioSpaceControls()
    {
        AddSlider(this, "归一化色度下限", "CloudChromaMin", 0, 1);
        AddSlider(this, "归一化色度上限", "CloudChromaMax", 0, 1);
        AddToggle(this, "启用明度切片", "CloudSliceEnabled");
        AddSlider(this, "切片中心 L", "CloudSliceCenter", 0, 1);
        AddSlider(this, "切片厚度（全宽）", "CloudSliceThickness", .01, 1);
        AddSlider(this, "X 旋转 · 度", "CloudRotationX", -180, 180);
        AddSlider(this, "Y 旋转 · 度", "CloudRotationY", -180, 180);
        AddSlider(this, "Z 旋转 · 度", "CloudRotationZ", -180, 180);
        AddSlider(this, "选区容差 · Δ OKLab", "SelectionTolerance", .01, .15);
        AddSlider(this, "选区柔和度", "SelectionSoftness", 0, 1);
    }
    internal static void AddSlider(Panel parent, string title, string path, double min, double max)
    {
        var row = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        var value = new TextBlock { HorizontalAlignment = HorizontalAlignment.Right };
        value.SetBinding(TextBlock.TextProperty, new Binding(path) { StringFormat = "0.##" });
        DockPanel.SetDock(value, Dock.Right); row.Children.Add(value);
        var label=new TextBlock { Text=title };StudioTextExtension.Bind(label,TextBlock.TextProperty,Key(path));row.Children.Add(label);parent.Children.Add(row);
        var slider = new Slider { Minimum = min, Maximum = max, SmallChange = (max - min) / 100, IsMoveToPointEnabled = true, Margin = new Thickness(0, 3, 0, 3) };
        slider.SetBinding(Slider.ValueProperty, new Binding(path) { Mode = BindingMode.TwoWay });
        slider.SetResourceReference(StyleProperty, "PixelTart.Slider");
        System.Windows.Automation.AutomationProperties.SetName(slider, title); parent.Children.Add(slider);
    }
    internal static void AddToggle(Panel parent, string title, string path)
    {
        var toggle = new CheckBox { Content = title, Margin = new Thickness(0, 8, 0, 2) };
        StudioTextExtension.Bind(toggle,ContentControl.ContentProperty,Key(path));
        toggle.SetBinding(CheckBox.IsCheckedProperty, new Binding(path) { Mode = BindingMode.TwoWay }); parent.Children.Add(toggle);
    }
    private static string Key(string path)=>path switch { "CloudSurfaceOpacity"=>"SurfaceOpacity","CloudShowGrid"=>"ShowGrid","CloudShowAxes"=>"ShowAxes","CloudShowGamut"=>"ShowGamut",_=>path.StartsWith("Cloud",StringComparison.Ordinal)?path[5..]:path };
}

public sealed class StudioSpaceAppearanceControls : StackPanel
{
    public StudioSpaceAppearanceControls()
    {
        var label=new TextBlock { Margin = new Thickness(0, 8, 0, 4) };StudioTextExtension.Bind(label,TextBlock.TextProperty,"Background");Children.Add(label);
        var background = new ComboBox();
        foreach(var key in new[]{"Dark","Gray","Light"}){var item=new ComboBoxItem();StudioTextExtension.Bind(item,ContentControl.ContentProperty,key);background.Items.Add(item);}
        background.SetBinding(ComboBox.SelectedIndexProperty, new Binding("CloudBackground") { Mode = BindingMode.TwoWay }); Children.Add(background);
        StudioSpaceControls.AddSlider(this, "表面透明度", "CloudSurfaceOpacity", 0, 1);
        StudioSpaceControls.AddToggle(this, "显示网格", "CloudShowGrid");
        StudioSpaceControls.AddToggle(this, "显示坐标轴", "CloudShowAxes");
        StudioSpaceControls.AddToggle(this, "显示色域轮廓", "CloudShowGamut");
    }
}
