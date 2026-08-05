using System.Windows;
using System.Windows.Input;

namespace RAWSelectionAssistant.Views;

public partial class ClientMonitorWindow : Window
{
    public ClientMonitorWindow() { InitializeComponent(); PreviewKeyDown += OnPreviewKeyDown; }
    private void OnPreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key is Key.Escape or Key.F11) { Close(); e.Handled = true; } }
}
