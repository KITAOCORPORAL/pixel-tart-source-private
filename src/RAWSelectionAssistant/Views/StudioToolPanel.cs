using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Markup;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

/// <summary>Editors of the Core parameter contract. Values always live in the editor's adjustment stack.</summary>
public sealed class StudioToolPanel : StackPanel
{
    private const string CompactTemplate = """
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Expander">
         <StackPanel><Grid MinHeight="25"><Grid.ColumnDefinitions><ColumnDefinition Width="20"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
          <ToggleButton IsChecked="{Binding IsExpanded,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}" Background="Transparent" BorderThickness="0" FocusVisualStyle="{DynamicResource PixelTart.Focus}">
           <ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border x:Name="Surface" Background="Transparent"><Path x:Name="Arrow" Data="M0,0 L4,4 0,8" Stroke="{DynamicResource TextSecondaryBrush}" StrokeThickness="1.5" Width="8" Height="8" VerticalAlignment="Center" HorizontalAlignment="Center" RenderTransformOrigin=".5,.5"/></Border><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Arrow" Property="RenderTransform"><Setter.Value><RotateTransform Angle="90"/></Setter.Value></Setter></Trigger><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Surface" Property="Background" Value="{DynamicResource SurfaceHoverBrush}"/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template>
          </ToggleButton><ContentPresenter Grid.Column="1" ContentSource="Header" VerticalAlignment="Center"/>
         </Grid><ContentPresenter x:Name="Body" ContentSource="Content" Visibility="Collapsed"/></StackPanel>
         <ControlTemplate.Triggers><Trigger Property="IsExpanded" Value="True"><Setter TargetName="Body" Property="Visibility" Value="Visible"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value=".45"/></Trigger></ControlTemplate.Triggers>
        </ControlTemplate>
        """;
    public static readonly DependencyProperty NodeTypesProperty = DependencyProperty.Register(nameof(NodeTypes), typeof(string), typeof(StudioToolPanel), new PropertyMetadata("", Rebuild));
    public string NodeTypes { get => (string)GetValue(NodeTypesProperty); set => SetValue(NodeTypesProperty, value); }
    private TetherReferenceModeViewModel? _editor;
    private bool _refreshing;
    private readonly List<(ColorStudioToolParameter Parameter, Slider Slider, TextBox Number)> _fields = [];
    private readonly List<(ColorStudioNodeType Type, CheckBox Toggle)> _toggles = [];
    private readonly Dictionary<string, bool> _expanded = [];
    public StudioToolPanel()
    {
        DataContextChanged += (_, _) => BindEditor();
        Loaded += (_, _) => BindEditor();
        Unloaded += (_, _) => { if (_editor is not null) _editor.PropertyChanged -= Changed; _editor = null; };
    }
    private void BindEditor()
    {
        var editor = (DataContext as ReferenceColorWorkspaceViewModel)?.Editor ?? DataContext as TetherReferenceModeViewModel;
        if (ReferenceEquals(editor, _editor)) return;
        if (_editor is not null) _editor.PropertyChanged -= Changed;
        _editor = editor;
        if (_editor is not null) _editor.PropertyChanged += Changed;
        Build();
    }
    private static void Rebuild(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((StudioToolPanel)d).Build();
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TetherReferenceModeViewModel.AdjustmentStack) or nameof(TetherReferenceModeViewModel.SelectedAdjustmentNode)) Refresh();
    }
    private void Build()
    {
        Children.Clear(); _fields.Clear(); _toggles.Clear();
        foreach (var id in NodeTypes.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Enum.TryParse<ColorStudioNodeType>(id, out var type)) continue;
            var heading = new DockPanel { MinHeight = 25, LastChildFill = true };
            var enabled = new CheckBox { IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
            Text(enabled, ContentControl.ContentProperty, TetherReferenceModeViewModel.ToolName(type));
            enabled.Checked += (_, _) => { if (!_refreshing) _editor?.SetToolEnabled(type, true); };
            enabled.Unchecked += (_, _) => { if (!_refreshing) _editor?.SetToolEnabled(type, false); };
            _toggles.Add((type, enabled));
            var nodeBody = new StackPanel();
            var nodeSection = Section(type.ToString(), heading, nodeBody, true); Children.Add(nodeSection);
            var groups = ColorStudioToolCatalog.GetParameters(type).GroupBy(p => p.Group).ToArray();
            foreach (var group in groups)
            {
                var body = new StackPanel { Margin = new Thickness(2, 1, 2, 3) };
                var reset = new Button { Height = 24, MinHeight = 24, HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(6, 0, 0, 0), Focusable = true };
                Text(reset, ContentControl.ContentProperty, "复位本组");
                reset.SetResourceReference(StyleProperty, "PixelTart.Button.Ghost");
                reset.Click += (_, _) => _editor?.ResetToolGroup(type, group.Key);
                DockPanel.SetDock(reset, Dock.Right);
                if (groups.Length == 1) { heading.Children.Add(reset); nodeBody.Children.Add(body); }
                else
                {
                    var groupHeader = new DockPanel { MinHeight = 25, LastChildFill = true }; groupHeader.Children.Add(reset);
                    var title = new TextBlock { VerticalAlignment = VerticalAlignment.Center }; Text(title, TextBlock.TextProperty, group.Key); groupHeader.Children.Add(title);
                    nodeBody.Children.Add(Section(type + ":" + group.Key, groupHeader, body, type is ColorStudioNodeType.BasicTone));
                }
                foreach (var parameter in group)
                {
                    var row = new DockPanel { Margin = new Thickness(0, 1, 0, 0) };
                    var number = new TextBox { Width = 68, Height = 25, MinHeight = 25, Padding = new Thickness(4, 1, 4, 1), TextAlignment = TextAlignment.Right, Tag = parameter.Key };
                    Text(number, ToolTipProperty, "Enter 保存，Esc 取消", $"{parameter.Minimum}–{parameter.Maximum} {parameter.Unit} · {{0}}");
                    Text(number, System.Windows.Automation.AutomationProperties.NameProperty, parameter.Label);
                    DockPanel.SetDock(number, Dock.Right); row.Children.Add(number);
                    var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
                    Text(label, TextBlock.TextProperty, parameter.Label, "{0}" + (parameter.Unit.Length > 0 ? " · " + parameter.Unit : "")); row.Children.Add(label);
                    body.Children.Add(row);
                    var slider = new Slider { Minimum = parameter.Minimum, Maximum = parameter.Maximum, SmallChange = parameter.Step, LargeChange = parameter.Step * 10, Value = parameter.DefaultValue, Margin = new Thickness(0, 0, 0, 1), IsMoveToPointEnabled = true };
                    slider.SetResourceReference(StyleProperty, "PixelTart.Slider");
                    slider.Tag = parameter.Key; Text(slider, System.Windows.Automation.AutomationProperties.NameProperty, parameter.Label);
                    slider.ValueChanged += (_, _) =>
                    {
                        if (_refreshing) return;
                        try { _editor?.SetToolParameter(parameter, slider.Value); slider.ToolTip = null; }
                        catch (ArgumentException error) { slider.ToolTip = error.Message; Refresh(); }
                    };
                    slider.MouseDoubleClick += (_, e) => { _editor?.SetToolParameter(parameter, parameter.DefaultValue); e.Handled = true; };
                    number.KeyDown += (_, e) =>
                    {
                        if (e.Key == Key.Escape) { Refresh(); Keyboard.ClearFocus(); e.Handled = true; }
                        else if (e.Key == Key.Enter) { CommitNumber(); Keyboard.ClearFocus(); e.Handled = true; }
                    };
                    number.LostKeyboardFocus += (_, _) => CommitNumber();
                    void CommitNumber()
                    {
                        if (_refreshing) return;
                        if (double.TryParse(number.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) && double.IsFinite(value))
                        {
                            try { _editor?.SetToolParameter(parameter, value); number.ClearValue(Border.BorderBrushProperty); Refresh(); }
                            catch (ArgumentException error) { number.BorderBrush = Brushes.OrangeRed; number.ToolTip = error.Message; }
                        }
                        else { number.BorderBrush = Brushes.OrangeRed; number.ToolTip = StudioLocalizationService.Current["请输入有限数值；Esc 恢复当前参数。"]; }
                    }
                    body.Children.Add(slider); _fields.Add((parameter, slider, number));
                }
            }
            heading.Children.Add(enabled);
        }
        Refresh();
    }
    private Expander Section(string id, object header, object content, bool expanded)
    {
        var section = new Expander { Header = header, Content = content, Tag = id, IsExpanded = _expanded.GetValueOrDefault(id, expanded), Margin = new Thickness(0, 2, 0, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        section.SetResourceReference(StyleProperty, "PixelTart.Section");
        section.Template = (ControlTemplate)XamlReader.Parse(CompactTemplate);
        if (header is FrameworkElement headerElement) headerElement.MouseLeftButtonDown += (_, e) =>
        {
            for (var current = e.OriginalSource as DependencyObject; current is not null && !ReferenceEquals(current, headerElement); current = VisualTreeHelper.GetParent(current))
                if (current is ButtonBase) return;
            section.IsExpanded = !section.IsExpanded; e.Handled = true;
        };
        section.Expanded += (_, e) => { if (ReferenceEquals(e.OriginalSource, section)) _expanded[id] = true; };
        section.Collapsed += (_, e) => { if (ReferenceEquals(e.OriginalSource, section)) _expanded[id] = false; };
        return section;
    }
    internal static void Text(FrameworkElement element, DependencyProperty property, string key, string? format = null) =>
        element.SetBinding(property, new Binding("[" + key + "]") { Source = StudioLocalizationService.Current, StringFormat = format });
    private void Refresh()
    {
        _refreshing = true;
        try
        {
            foreach (var field in _fields)
            {
                var value = _editor?.ToolValue(field.Parameter) ?? field.Parameter.DefaultValue;
                field.Slider.Value = value;
                if (!field.Number.IsKeyboardFocusWithin) field.Number.Text = value.ToString("0.###", CultureInfo.CurrentCulture);
            }
            foreach (var (type, toggle) in _toggles) toggle.IsChecked = _editor?.ToolNode(type)?.Enabled != false;
        }
        finally { _refreshing = false; }
    }
}
