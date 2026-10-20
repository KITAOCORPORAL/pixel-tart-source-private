using System.Windows;
using System.Windows.Input;

namespace RAWSelectionAssistant.Views;

public enum SaveDiscardCancelChoice { Cancel, Discard, Save }

public enum ThemedMessageKind { Information, Success, Warning, Error, Question }

public partial class ThemedMessageDialog : Window
{
    private readonly bool _confirmation;
    private string? _logDirectory;
    private SaveDiscardCancelChoice _saveChoice;

    private ThemedMessageDialog(string title, string message, ThemedMessageKind kind, bool confirmation)
    {
        InitializeComponent();
        _confirmation = confirmation;
        TitleTextBlock.Text = title;
        Title = title;
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

    internal static ThemedMessageDialog CreateSaveDiscardCancel(string title, string message)
    {
        var dialog = new ThemedMessageDialog(title, message, ThemedMessageKind.Question, true);
        dialog.YesButton.Content="保存并切换";
        System.Windows.Automation.AutomationProperties.SetName(dialog.YesButton,"保存并切换");
        dialog._saveChoice=SaveDiscardCancelChoice.Cancel;
        dialog.DiscardButton.Visibility=Visibility.Visible;
        return dialog;
    }

    public static SaveDiscardCancelChoice ShowSaveDiscardCancel(Window? owner, string title, string message)
    {
        var dialog=CreateSaveDiscardCancel(title,message);
        if(owner is { IsLoaded:true })dialog.Owner=owner;
        return dialog.ShowDialog()==true?SaveDiscardCancelChoice.Save:dialog._saveChoice;
    }

    public static void ShowStartupFailure(string summary, string logDirectory)
    {
        var dialog = new ThemedMessageDialog("软件启动失败", summary, ThemedMessageKind.Error, false)
        {
            _logDirectory = logDirectory,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        dialog.CopyErrorButton.Visibility = Visibility.Visible;
        dialog.OpenLogsButton.Visibility = Visibility.Visible;
        dialog.ShowDialog();
    }

    private void CopyError_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(MessageTextBlock.Text); }
        catch (System.Runtime.InteropServices.ExternalException) { CopyErrorButton.Content = "剪贴板忙，请重试"; }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        if (_logDirectory is null) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_logDirectory) { UseShellExecute = true }); }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or System.IO.IOException)
        { MessageTextBlock.Text += "\n日志位置：" + _logDirectory; }
    }

    private void YesButton_Click(object sender, RoutedEventArgs e) { _saveChoice=SaveDiscardCancelChoice.Save; DialogResult = true; Close(); }
    private void DiscardButton_Click(object sender, RoutedEventArgs e) { _saveChoice=SaveDiscardCancelChoice.Discard; DialogResult=false; Close(); }
    private void NoButton_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    private void CloseButton_Click(object sender, RoutedEventArgs e) { DialogResult = _confirmation ? false : true; Close(); }
    private void CloseSurfaceRequested(object sender, RoutedEventArgs e) { DialogResult = _confirmation ? false : true; Close(); }
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { DialogResult = _confirmation ? false : true; Close(); e.Handled = true; }
        else if (e.Key == Key.Enter) { DialogResult = true; Close(); e.Handled = true; }
    }
}
