using System.Diagnostics;
using System.Windows;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Services;

public sealed class WpfFeedbackClipboard : IFeedbackClipboard
{
    public void SetText(string text) => Clipboard.SetText(text, TextDataFormat.UnicodeText);
}

public sealed class ShellFeedbackMailLauncher : IFeedbackMailLauncher
{
    public void Open(string mailtoUri)
    {
        using var process = Process.Start(new ProcessStartInfo(mailtoUri) { UseShellExecute = true });
    }
}
