using System.Text.RegularExpressions;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public static partial class MediaExtensionPolicy
{
    public static readonly string[] DefaultJpegExtensions = [".JPG", ".JPEG"];
    public static readonly string[] DefaultRawExtensions =
    [
        ".ARW", ".CR2", ".CR3", ".NEF", ".NRW", ".RAF", ".DNG", ".RW2",
        ".ORF", ".ORI", ".PEF", ".3FR", ".FFF", ".IIQ", ".SRW", ".RWL"
    ];

    [GeneratedRegex(@"^[.][A-Z0-9]{1,10}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidExtensionRegex();

    public static string NormalizeExtension(string extension)
    {
        var value = extension.Trim().ToUpperInvariant();
        return value.Length == 0 ? string.Empty : value.StartsWith('.') ? value : $".{value}";
    }

    public static FileCategory Classify(string extension, IEnumerable<string> customExtensions)
    {
        var value = NormalizeExtension(extension);
        if (DefaultJpegExtensions.Contains(value, StringComparer.OrdinalIgnoreCase)) return FileCategory.Jpeg;
        if (DefaultRawExtensions.Contains(value, StringComparer.OrdinalIgnoreCase)) return FileCategory.Raw;
        if (value == ".XMP") return FileCategory.Sidecar;
        if (value is ".TIF" or ".TIFF" or ".PSD" or ".PNG") return FileCategory.ProcessedImage;
        return customExtensions.Select(NormalizeExtension).Contains(value, StringComparer.OrdinalIgnoreCase)
            ? FileCategory.Custom
            : FileCategory.Custom;
    }

    public static ExtensionParseResult ParseCustomExtensions(string? text)
    {
        var values = (text ?? string.Empty)
            .Split([' ', '\t', '\r', '\n', ',', '，', ';', '；', '|', '、'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeExtension)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var invalid = values.FirstOrDefault(value => !ValidExtensionRegex().IsMatch(value));
        return invalid is null
            ? new ExtensionParseResult(true, values, string.Empty)
            : new ExtensionParseResult(false, [], $"扩展名“{invalid}”包含非法字符。只允许字母和数字。");
    }
}

public sealed record ExtensionParseResult(bool IsValid, IReadOnlyList<string> Extensions, string ErrorMessage);
