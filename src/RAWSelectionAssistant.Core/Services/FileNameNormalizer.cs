using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed partial class FileNameNormalizer
{
    [GeneratedRegex(@"\s*\(\d+\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NumberedCopySuffixRegex();

    [GeneratedRegex(@"(?:[-_]?COPY|[-_]?副本|副本)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NamedCopySuffixRegex();

    [GeneratedRegex(@"(\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingNumberRegex();

    public NormalizedFileName Normalize(string? input)
    {
        var original = input ?? string.Empty;
        var value = original.Trim().Normalize(NormalizationForm.FormKC);
        value = SafeGetFileName(value);

        var extension = Path.GetExtension(value);
        if (!string.IsNullOrWhiteSpace(extension) && extension.Length <= 10)
        {
            value = Path.GetFileNameWithoutExtension(value);
        }

        value = value.Trim();
        var previous = string.Empty;
        while (!string.Equals(previous, value, StringComparison.Ordinal))
        {
            previous = value;
            value = NumberedCopySuffixRegex().Replace(value, string.Empty).TrimEnd();
            value = NamedCopySuffixRegex().Replace(value, string.Empty).TrimEnd();
        }

        var comparison = value.ToUpper(CultureInfo.InvariantCulture).Normalize(NormalizationForm.FormKC);
        var numberMatch = TrailingNumberRegex().Match(comparison);
        var numericId = numberMatch.Success ? NormalizeNumericId(numberMatch.Groups[1].Value) : string.Empty;
        return new NormalizedFileName(original, comparison, comparison, numericId);
    }

    public static string NormalizeNumericId(string value)
    {
        var digits = value.Trim().TrimStart('0');
        return digits.Length == 0 && value.Any(char.IsDigit) ? "0" : digits;
    }

    private static string SafeGetFileName(string value)
    {
        try
        {
            return Path.GetFileName(value);
        }
        catch
        {
            return value;
        }
    }
}
