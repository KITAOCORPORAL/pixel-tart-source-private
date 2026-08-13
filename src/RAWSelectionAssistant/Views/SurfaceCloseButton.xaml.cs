using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.Views;

/// <summary>
/// Shell-owned escape hatch for modal, drawer, overlay, wizard and full-page surfaces.
/// It intentionally exposes no command or CanExecute dependency from the hosted module.
/// </summary>
public partial class SurfaceCloseButton : UserControl
{
    public static readonly RoutedEvent CloseRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(CloseRequested),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(SurfaceCloseButton));

    public static readonly DependencyProperty ToolTipTextProperty = DependencyProperty.Register(
        nameof(ToolTipText),
        typeof(string),
        typeof(SurfaceCloseButton),
        new PropertyMetadata("关闭并返回"));

    public static readonly DependencyProperty AutomationNameProperty = DependencyProperty.Register(
        nameof(AutomationName),
        typeof(string),
        typeof(SurfaceCloseButton),
        new PropertyMetadata("关闭并返回"));

    public static readonly DependencyProperty AutomationIdProperty = DependencyProperty.Register(
        nameof(AutomationId),
        typeof(string),
        typeof(SurfaceCloseButton),
        new PropertyMetadata(string.Empty));

    public SurfaceCloseButton() => InitializeComponent();

    public event RoutedEventHandler CloseRequested
    {
        add => AddHandler(CloseRequestedEvent, value);
        remove => RemoveHandler(CloseRequestedEvent, value);
    }

    public string ToolTipText
    {
        get => (string)GetValue(ToolTipTextProperty);
        set => SetValue(ToolTipTextProperty, value);
    }

    public string AutomationName
    {
        get => (string)GetValue(AutomationNameProperty);
        set => SetValue(AutomationNameProperty, value);
    }

    public string AutomationId
    {
        get => (string)GetValue(AutomationIdProperty);
        set => SetValue(AutomationIdProperty, value);
    }

    private void CloseButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
#if DEBUG || INPUT_ROUTING_DIAGNOSTICS
        InputRoutingDiagnostics.RecordControlEvent(this, "ClosePreviewMouseDown", e.OriginalSource, e.Source, e.Handled);
#endif
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
#if DEBUG || INPUT_ROUTING_DIAGNOSTICS
        InputRoutingDiagnostics.RecordControlEvent(this, "CloseClick", e.OriginalSource, e.Source, e.Handled);
#endif
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(CloseRequestedEvent, this));
    }
}
