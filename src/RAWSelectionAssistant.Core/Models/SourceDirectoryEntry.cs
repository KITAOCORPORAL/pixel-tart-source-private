using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Models;

public sealed class SourceDirectoryEntry : ObservableObject
{
    private string _path = string.Empty;
    private SourceDirectoryType _directoryType = SourceDirectoryType.Mixed;
    private int _priority;

    public string Path
    {
        get => _path;
        set
        {
            if (SetProperty(ref _path, value))
            {
                OnPropertyChanged(nameof(Exists));
                OnPropertyChanged(nameof(DisplayLabel));
            }
        }
    }

    public SourceDirectoryType DirectoryType
    {
        get => _directoryType;
        set
        {
            if (SetProperty(ref _directoryType, value)) OnPropertyChanged(nameof(DisplayLabel));
        }
    }

    public int Priority { get => _priority; set => SetProperty(ref _priority, value); }
    public bool Exists => Directory.Exists(Path);
    public string DisplayLabel => $"{Path}  [{DirectoryType.ToChinese()}]";
}
