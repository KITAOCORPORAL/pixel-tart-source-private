using System.Windows;
using System.Windows.Controls;

namespace RAWSelectionAssistant.Views;

public partial class SurfaceHeader : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SurfaceHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(SurfaceHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CloseToolTipProperty = DependencyProperty.Register(
        nameof(CloseToolTip), typeof(string), typeof(SurfaceHeader), new PropertyMetadata("关闭并返回"));

    public static readonly DependencyProperty CloseAutomationNameProperty = DependencyProperty.Register(
        nameof(CloseAutomationName), typeof(string), typeof(SurfaceHeader), new PropertyMetadata("关闭并返回"));

    public static readonly DependencyProperty CloseAutomationIdProperty = DependencyProperty.Register(
        nameof(CloseAutomationId), typeof(string), typeof(SurfaceHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ShowCloseButtonProperty = DependencyProperty.Register(
        nameof(ShowCloseButton), typeof(bool), typeof(SurfaceHeader), new PropertyMetadata(true));

    public SurfaceHeader() => InitializeComponent();

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string CloseToolTip
    {
        get => (string)GetValue(CloseToolTipProperty);
        set => SetValue(CloseToolTipProperty, value);
    }

    public string CloseAutomationName
    {
        get => (string)GetValue(CloseAutomationNameProperty);
        set => SetValue(CloseAutomationNameProperty, value);
    }

    public string CloseAutomationId
    {
        get => (string)GetValue(CloseAutomationIdProperty);
        set => SetValue(CloseAutomationIdProperty, value);
    }

    public bool ShowCloseButton
    {
        get => (bool)GetValue(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }
}
