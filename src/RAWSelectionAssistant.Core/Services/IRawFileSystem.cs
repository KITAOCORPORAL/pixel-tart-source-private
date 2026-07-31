namespace RAWSelectionAssistant.Core.Services;

public interface IRawFileSystem
{
    bool DirectoryExists(string path);
    IEnumerable<string> EnumerateDirectories(string path);
    IEnumerable<string> EnumerateFiles(string path);
    FileInfo GetFileInfo(string path);
}

public sealed class SystemRawFileSystem : IRawFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public IEnumerable<string> EnumerateDirectories(string path) => Directory.EnumerateDirectories(path);
    public IEnumerable<string> EnumerateFiles(string path) => Directory.EnumerateFiles(path);
    public FileInfo GetFileInfo(string path) => new(path);
}
