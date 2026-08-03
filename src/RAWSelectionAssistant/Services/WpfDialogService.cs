using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.Services;

public sealed class WpfDialogService : IDialogService
{
    private readonly IFeedbackService _feedbackService;

    public WpfDialogService(IFeedbackService feedbackService) => _feedbackService = feedbackService;

    public string? ChooseFolder(string title, string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false,
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : null
        };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FolderName : null;
    }

    public IReadOnlyList<string> ChooseFiles(string title, string filter, bool multiselect = true)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter, Multiselect = multiselect, CheckFileExists = true };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileNames : [];
    }

    public string? ChooseSaveFile(string title, string filter, string defaultExtension, string? suggestedFileName = null)
    {
        var dialog = new SaveFileDialog { Title = title, Filter = filter, DefaultExt = defaultExtension, AddExtension = true, OverwritePrompt = false, FileName = suggestedFileName ?? string.Empty };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
    }

    public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds)
    {
        var dialog = new QuickToolsManagerWindow(currentToolIds) { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true ? dialog.ResultToolIds : null;
    }

    public void ShowInfo(string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, Branding.ProductName, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, Branding.ProductName, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string message, string title) =>
        MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public HelpAction ShowHelp()
    {
        var dialog = new HelpWindow { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
        return dialog.SelectedAction;
    }

    public void ShowFeedback()
    {
        var dialog = new FeedbackDialog(_feedbackService) { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates)
    {
        var dialog = new CandidateSelectionWindow(candidates)
        {
            Owner = Application.Current.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.SelectedCandidate : null;
    }

    public bool ShowMediaDetails(MediaSelectionItem item, bool showAdvancedDetails)
    {
        var dialog = new MediaDetailsWindow(item, showAdvancedDetails)
        {
            Owner = Application.Current.MainWindow
        };
        dialog.ShowDialog();
        return dialog.SelectionChanged;
    }

    public void RevealFile(string path)
    {
        if (!File.Exists(path))
        {
            ShowError("文件不存在或存储设备当前不可用。");
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }
}

public sealed class WpfClipboardService : IClipboardService
{
    public string GetText()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText(TextDataFormat.UnicodeText) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
