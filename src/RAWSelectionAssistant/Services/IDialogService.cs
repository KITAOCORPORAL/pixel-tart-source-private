using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Services;

public interface IDialogService
{
    string? ChooseFolder(string title, string? initialDirectory = null);
    IReadOnlyList<string> ChooseFiles(string title, string filter, bool multiselect = true);
    string? ChooseSaveFile(string title, string filter, string defaultExtension, string? suggestedFileName = null);
    IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds);
    void ShowInfo(string message);
    void ShowError(string message);
    bool Confirm(string message, string title);
    HelpAction ShowHelp();
    void ShowFeedback();
    RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates);
    bool ShowMediaDetails(MediaSelectionItem item, bool showAdvancedDetails);
    void RevealFile(string path);
}

public enum HelpAction
{
    None,
    ReplayTutorial,
    ResetTutorialData,
    DeleteTutorialData
}

public interface IClipboardService
{
    string GetText();
}
