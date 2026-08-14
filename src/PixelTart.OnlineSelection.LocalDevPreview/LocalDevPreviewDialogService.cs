using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Services;

namespace PixelTart.OnlineSelection.LocalDevPreview;

internal sealed class LocalDevPreviewDialogService : IDialogService
{
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
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            Multiselect = multiselect,
            CheckFileExists = true
        };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileNames : [];
    }

    public string? ChooseSaveFile(string title, string filter, string defaultExtension, string? suggestedFileName = null)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            DefaultExt = defaultExtension,
            AddExtension = true,
            OverwritePrompt = false,
            FileName = suggestedFileName ?? string.Empty
        };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
    }

    public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds) => null;

    public void ShowInfo(string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, "Pixel Tart Online Selection LocalDev", MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, "Pixel Tart Online Selection LocalDev", MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string message, string title) =>
        MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public HelpAction ShowHelp() => HelpAction.None;

    public void ShowFeedback() => ShowInfo("LocalDev Preview 不发送反馈或任何生产数据。");

    public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates) => candidates.FirstOrDefault();

    public bool ShowMediaDetails(MediaSelectionItem item, bool showAdvancedDetails) => false;

    public void RevealFile(string path)
    {
        if (!File.Exists(path))
        {
            ShowError("文件不存在或当前不可访问。");
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }
}
