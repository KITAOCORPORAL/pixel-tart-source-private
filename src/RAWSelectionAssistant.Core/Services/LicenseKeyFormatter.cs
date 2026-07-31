using System.Text;

namespace RAWSelectionAssistant.Core.Services;

public static class LicenseKeyFormatter
{
    private const string Prefix = "KQGP";
    private const int SegmentLength = 5;
    private const int SegmentCount = 3;

    public static string Normalize(string? value)
    {
        var characters = new string((value ?? string.Empty)
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
        if (characters.StartsWith(Prefix, StringComparison.Ordinal)) characters = characters[Prefix.Length..];
        characters = characters.Length > SegmentLength * SegmentCount
            ? characters[..(SegmentLength * SegmentCount)]
            : characters;

        var builder = new StringBuilder(Prefix);
        for (var index = 0; index < characters.Length; index += SegmentLength)
        {
            builder.Append('-');
            builder.Append(characters.AsSpan(index, Math.Min(SegmentLength, characters.Length - index)));
        }
        return builder.ToString();
    }

    public static bool IsComplete(string? value)
    {
        var formatted = Normalize(value);
        return formatted.Length == Prefix.Length + SegmentCount * (SegmentLength + 1) &&
               formatted.Split('-').Skip(1).All(segment => segment.Length == SegmentLength);
    }

    public static string Suffix(string? value)
    {
        var normalized = Normalize(value);
        var characters = normalized.Where(char.IsLetterOrDigit).ToArray();
        return characters.Length <= 4 ? new string(characters) : new string(characters[^4..]);
    }

    public static string Mask(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : $"KQGP-*****-*****-*{Suffix(value)}";
}
