using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

/// <summary>Filter swatches change the existing filmstrip filter, never target metadata.</summary>
public sealed class StudioColorLabelFilters : StackPanel
{
    private ReferenceColorWorkspaceViewModel? _workspace;
    private readonly List<(string Id, Border Border)> _swatches = [];
    public StudioColorLabelFilters()
    {
        Orientation = Orientation.Horizontal;
        foreach (var id in new[] { "All", "None", "红", "橙", "黄", "绿", "蓝", "紫" })
        {
            var label = id == "All" ? "所有色标" : id == "None" ? "无色标" : id + "色标";
            var color = id is "All" or "None" ? Brushes.Transparent : new ReferenceTargetItem("filter") { ColorLabel = id }.ColorLabelBrush;
            var swatch = new Border { Width = 15, Height = 15, CornerRadius = new CornerRadius(3), Background = color, BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray };
            if (id == "All") swatch.Child = new TextBlock { Text = "全", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var button = new Button { Content = swatch, Width = 24, Height = 27, Padding = new Thickness(2), ToolTip = label };
            button.SetResourceReference(StyleProperty, "PixelTart.Button.Ghost");
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            button.Click += (_, _) => { if (_workspace is not null) _workspace.ColorLabelFilter = id; };
            Children.Add(button); _swatches.Add((id, swatch));
        }
        Loaded += (_, _) => Attach(); DataContextChanged += (_, _) => Attach();
        Unloaded += (_, _) => { if (_workspace is not null) _workspace.PropertyChanged -= Changed; _workspace = null; };
    }
    private void Attach()
    {
        if (_workspace is not null) _workspace.PropertyChanged -= Changed;
        _workspace = DataContext as ReferenceColorWorkspaceViewModel;
        if (_workspace is not null) _workspace.PropertyChanged += Changed;
        Refresh();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(ReferenceColorWorkspaceViewModel.ColorLabelFilter)) Refresh(); }
    private void Refresh()
    {
        foreach (var item in _swatches)
        {
            var selected = item.Id == (_workspace?.ColorLabelFilter ?? "All");
            item.Border.BorderThickness = new Thickness(selected ? 2 : 1);
            item.Border.BorderBrush = selected ? Brushes.White : Brushes.Gray;
            item.Border.Opacity = selected ? 1 : .7;
        }
    }
}
