namespace RAWSelectionAssistant.Core.Services.Color;

public enum ColorProfileOperation
{
    Assign,
    Convert
}

public sealed record ColorProfileDefinition(string Id, string DisplayName, string[] CommonFileNames)
{
    public ColorProfileDefinition Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(DisplayName)) throw new ArgumentException("颜色配置身份不能为空。");
        if (CommonFileNames is null || CommonFileNames.Length == 0) throw new ArgumentException("颜色配置必须声明可识别文件名。", nameof(CommonFileNames));
        return this;
    }
}

public sealed record ColorProfileResolution(ColorProfileDefinition Definition, string? Path, bool IsAvailable)
{
    public bool CanConvert => IsAvailable && !string.IsNullOrWhiteSpace(Path);
}

/// <summary>
/// Central registry for output profiles. A profile tag is never treated as a conversion:
/// callers must choose <see cref="ColorProfileOperation.Assign"/> or Convert explicitly.
/// </summary>
public sealed class ColorProfileRegistry
{
    private static readonly IReadOnlyList<ColorProfileDefinition> Definitions =
    [
        new("sRGB", "sRGB IEC61966-2.1", ["sRGB Color Space Profile.icm", "sRGB.icm"]),
        new("AdobeRGB", "Adobe RGB (1998)", ["AdobeRGB1998.icc", "Adobe RGB (1998).icm"]),
        new("DisplayP3", "Display P3", ["Display P3.icc", "Display P3.icm"]),
        new("ProPhotoRGB", "ProPhoto RGB", ["ProPhoto.icc", "ProPhoto RGB.icc"])
    ];

    public IReadOnlyList<ColorProfileDefinition> List() => Definitions;

    public ColorProfileDefinition Get(string id) => Definitions.FirstOrDefault(item =>
        string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(item.DisplayName, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"未知 ICC 配置：{id}");

    public ColorProfileResolution Resolve(string id, IEnumerable<string>? roots = null)
    {
        var definition = Get(id);
        var searchRoots = (roots ?? DefaultRoots()).Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var path = searchRoots.SelectMany(root => definition.CommonFileNames.Select(name => Path.Combine(root, name)))
            .FirstOrDefault(File.Exists);
        return new(definition, path, path is not null);
    }

    public static string NormalizeId(string id) => id switch
    {
        "sRGB IEC61966-2.1" or "sRGB" => "sRGB",
        "Adobe RGB (1998)" or "AdobeRGB" => "AdobeRGB",
        "Display P3" or "DisplayP3" => "DisplayP3",
        "ProPhoto RGB" or "ProPhotoRGB" => "ProPhotoRGB",
        _ => throw new KeyNotFoundException($"未知 ICC 配置：{id}")
    };

    private static IEnumerable<string> DefaultRoots()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "spool", "drivers", "color");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Adobe", "Color", "Profiles");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Common Files", "Adobe", "Color", "Profiles", "Recommended");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Common Files", "Adobe", "Color", "Profiles", "Recommended");
    }
}
