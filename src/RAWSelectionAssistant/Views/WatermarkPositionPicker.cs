using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Views;

public sealed class WatermarkPositionPicker : UniformGrid
{
    private readonly Dictionary<WatermarkPosition, Button> _buttons = [];
    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(nameof(Position), typeof(WatermarkPosition),
        typeof(WatermarkPositionPicker), new FrameworkPropertyMetadata(WatermarkPosition.BottomRight,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (target, _) => ((WatermarkPositionPicker)target).Refresh()));
    public WatermarkPosition Position { get => (WatermarkPosition)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public WatermarkPositionPicker()
    {
        Rows = Columns = 3; Width = 132; Height = 108; HorizontalAlignment = HorizontalAlignment.Left;
        AutomationProperties.SetName(this, "水印位置九宫格");
        string[] names = ["左上", "上中", "右上", "左中", "居中", "右中", "左下", "下中", "右下"];
        WatermarkPosition[] positions = [WatermarkPosition.TopLeft, WatermarkPosition.TopCenter, WatermarkPosition.TopRight,
            WatermarkPosition.MiddleLeft, WatermarkPosition.Center, WatermarkPosition.MiddleRight,
            WatermarkPosition.BottomLeft, WatermarkPosition.BottomCenter, WatermarkPosition.BottomRight];
        for (var index = 0; index < positions.Length; index++)
        {
            var position = positions[index];
            var button = new Button { Content = "○", ToolTip = names[index], Margin = new(2), Padding = new(0), MinWidth = 0, FontSize = 18 };
            button.SetResourceReference(StyleProperty, "GhostButton");
            AutomationProperties.SetName(button, names[index]);
            button.Click += (_, _) => SetCurrentValue(PositionProperty, position);
            _buttons.Add(position, button); Children.Add(button);
        }
        Refresh();
    }
    private void Refresh()
    {
        foreach (var (position, button) in _buttons)
        {
            button.Content = Position == position ? "●" : "○";
            button.SetResourceReference(Control.ForegroundProperty, Position == position ? "AccentBrush" : "TextPrimaryBrush");
            AutomationProperties.SetHelpText(button, Position == position ? "当前位置" : "点击放置水印");
        }
    }
}
