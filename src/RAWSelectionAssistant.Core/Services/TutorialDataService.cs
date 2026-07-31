using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Services;

public sealed class TutorialDataService
{
    private const string DemoJpegBase64 = "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////2wBDAf//////////////////////////////////////////////////////////////////////////////////////wAARCAAMABADASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAf/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIQAxAAAAFQAP/EABQQAQAAAAAAAAAAAAAAAAAAABD/2gAIAQEAAQUCf//EABQRAQAAAAAAAAAAAAAAAAAAABD/2gAIAQMBAT8Bf//EABQRAQAAAAAAAAAAAAAAAAAAABD/2gAIAQIBAT8Bf//EABQQAQAAAAAAAAAAAAAAAAAAABD/2gAIAQEABj8Cf//EABQQAQAAAAAAAAAAAAAAAAAAABD/2gAIAQEAAT8hf//aAAwDAQACAAMAAAAQAP/EABQRAQAAAAAAAAAAAAAAAAAAABD/2gAIAQMBAT8Qf//EABQRAQAAAAAAAAAAAAAAAAAAABD/2gAIAQIBAT8Qf//EABQQAQAAAAAAAAAAAAAAAAAAABD/2gAIAQEAAT8Qf//Z";
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

    public async Task<TutorialSandboxPaths> EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        var paths = Paths;
        Directory.CreateDirectory(paths.JpegSource);
        Directory.CreateDirectory(paths.RawSource);
        Directory.CreateDirectory(paths.CustomerSelection);
        Directory.CreateDirectory(paths.Output);
        var jpeg = Convert.FromBase64String(DemoJpegBase64);
        foreach (var number in new[] { "01234", "01235", "01236" })
        {
            var jpgPath = Path.Combine(paths.JpegSource, $"DSC{number}.JPG");
            if (!File.Exists(jpgPath) || new FileInfo(jpgPath).Length == 0)
            {
                await File.WriteAllBytesAsync(jpgPath, jpeg, cancellationToken).ConfigureAwait(false);
            }
            var rawPath = Path.Combine(paths.RawSource, $"DSC{number}.ARW");
            if (!File.Exists(rawPath) || new FileInfo(rawPath).Length == 0)
            {
                await File.WriteAllTextAsync(rawPath, $"KitaoPhotoSelector tutorial RAW placeholder DSC{number}", cancellationToken).ConfigureAwait(false);
            }
        }
        File.Copy(Path.Combine(paths.JpegSource, "DSC01234.JPG"), paths.CustomerJpeg, true);
        await File.WriteAllTextAsync(paths.SelectionText, "1235、DSC01236.JPG", cancellationToken).ConfigureAwait(false);
        return paths;
    }

    public async Task<TutorialSandboxPaths> ResetAsync(CancellationToken cancellationToken = default)
    {
        Delete(_tutorialRoot);
        return await EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Delete(string path)
    {
        if (!IsTutorialRoot(path))
        {
            throw new InvalidOperationException("教程数据路径验证失败，已停止删除。");
        }
        if (Directory.Exists(_tutorialRoot)) Directory.Delete(_tutorialRoot, true);
    }

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
