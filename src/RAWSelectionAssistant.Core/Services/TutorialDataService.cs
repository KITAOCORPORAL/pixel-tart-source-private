using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Services;

public sealed class TutorialDataService
{
    private readonly string _applicationRoot;
    private readonly string _tutorialRoot;

    public TutorialDataService(string? applicationRoot = null)
    {
        _applicationRoot = Path.GetFullPath(applicationRoot ?? AppDataPaths.Root).TrimEnd(Path.DirectorySeparatorChar);
        _tutorialRoot = Path.Combine(_applicationRoot, "Tutorial");
    }

    public TutorialSandboxPaths Paths => new(
        _tutorialRoot,
        Path.Combine(_tutorialRoot, "Source"),
        Path.Combine(_tutorialRoot, "Source", "JPG"),
        Path.Combine(_tutorialRoot, "Source", "RAW"),
        Path.Combine(_tutorialRoot, "CustomerSelection"),
        Path.Combine(_tutorialRoot, "Output"),
        Path.Combine(_tutorialRoot, "CustomerSelection", "DSC01234.JPG"),
        Path.Combine(_tutorialRoot, "CustomerSelection", "选片编号.txt"));

    // Retired compatibility methods must not generate, overwrite or delete any files.
    public Task<TutorialSandboxPaths> EnsureCreatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(Paths);
    public Task<TutorialSandboxPaths> ResetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Paths);
    public void Delete(string path) { }

    public bool IsWithinTutorial(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var rootPrefix = Path.GetFullPath(_tutorialRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || IsTutorialRoot(fullPath);
    }

    public void EnsureWithinTutorial(string path)
    {
        if (!IsWithinTutorial(path)) throw new InvalidOperationException("教程模式只允许访问教程演示目录。");
    }

    private bool IsTutorialRoot(string path) => string.Equals(
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar),
        Path.GetFullPath(_tutorialRoot).TrimEnd(Path.DirectorySeparatorChar),
        StringComparison.OrdinalIgnoreCase) && IsUnderApplicationRoot(_tutorialRoot);

    private bool IsUnderApplicationRoot(string path)
    {
        var prefix = _applicationRoot + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
