using System.Windows;
using System.Windows.Input;

namespace RAWSelectionAssistant.Views;

public enum ThemedMessageKind { Information, Success, Warning, Error, Question }

public partial class ThemedMessageDialog : Window
{
    private readonly bool _confirmation;

    private ThemedMessageDialog(string title, string message, ThemedMessageKind kind, bool confirmation)
    {
        InitializeComponent();
        _confirmation = confirmation;
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        GlyphTextBlock.Text = kind switch
        {
            ThemedMessageKind.Success => "✓",
            ThemedMessageKind.Warning => "!",
            ThemedMessageKind.Error => "×",
            ThemedMessageKind.Question => "?",
            _ => "i"
        };
        GlyphTextBlock.SetResourceReference(ForegroundProperty, kind switch
        {
            ThemedMessageKind.Success => "SuccessBrush",
            ThemedMessageKind.Warning => "WarningBrush",
            ThemedMessageKind.Error => "DangerBrush",
            _ => "AccentBrush"
        });
        NoButton.Visibility = confirmation ? Visibility.Visible : Visibility.Collapsed;
        YesButton.Content = confirmation ? "确认" : "确定";
        Loaded += (_, _) => YesButton.Focus();
    }

    public static bool Show(Window? owner, string title, string message, ThemedMessageKind kind, bool confirmation = false)
    {
        var dialog = new ThemedMessageDialog(title, message, kind, confirmation);
        if (owner is { IsLoaded: true }) dialog.Owner = owner;
        return dialog.ShowDialog() == true;
    }

    private void YesButton_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void NoButton_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    private void CloseButton_Click(object sender, RoutedEventArgs e) { DialogResult = _confirmation ? false : true; Close(); }
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { DialogResult = _confirmation ? false : true; Close(); e.Handled = true; }
        else if (e.Key == Key.Enter) { DialogResult = true; Close(); e.Handled = true; }
    }
}
