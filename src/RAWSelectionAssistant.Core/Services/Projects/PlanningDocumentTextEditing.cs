namespace RAWSelectionAssistant.Core.Services.Projects;

public enum PlanningTextCommand
{
    Heading1,
    Heading2,
    Bold,
    Italic,
    BulletedList,
    NumberedList,
    Quote,
    Divider
}

public sealed record PlanningTextEditResult(string Text, int SelectionStart, int SelectionLength);

/// <summary>
/// Deterministic text-editing operations for the current Markdown-compatible
/// planning format. Kept outside WPF so persistence and keyboard/UI commands use
/// the same selection math.
/// </summary>
public static class PlanningDocumentTextEditing
{
    public static PlanningTextEditResult Apply(string? text, int selectionStart, int selectionLength, PlanningTextCommand command)
    {
        text ??= string.Empty;
        selectionStart = Math.Clamp(selectionStart, 0, text.Length);
        selectionLength = Math.Clamp(selectionLength, 0, text.Length - selectionStart);
        return command switch
        {
            PlanningTextCommand.Bold => Wrap(text, selectionStart, selectionLength, "**"),
            PlanningTextCommand.Italic => Wrap(text, selectionStart, selectionLength, "*"),
            PlanningTextCommand.Heading1 => PrefixLines(text, selectionStart, selectionLength, _ => "# ", removeHeading: true),
            PlanningTextCommand.Heading2 => PrefixLines(text, selectionStart, selectionLength, _ => "## ", removeHeading: true),
            PlanningTextCommand.BulletedList => PrefixLines(text, selectionStart, selectionLength, _ => "- ", removeList: true),
            PlanningTextCommand.NumberedList => PrefixLines(text, selectionStart, selectionLength, index => $"{index + 1}. ", removeList: true),
            PlanningTextCommand.Quote => PrefixLines(text, selectionStart, selectionLength, _ => "> ", removeQuote: true),
            PlanningTextCommand.Divider => InsertDivider(text, selectionStart, selectionLength),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
    }

    private static PlanningTextEditResult Wrap(string text, int start, int length, string marker)
    {
        var selected = text.Substring(start, length);
        if (length > marker.Length * 2 && selected.StartsWith(marker, StringComparison.Ordinal) && selected.EndsWith(marker, StringComparison.Ordinal))
        {
            var unwrapped = selected[marker.Length..^marker.Length];
            return new(text[..start] + unwrapped + text[(start + length)..], start, unwrapped.Length);
        }
        var replacement = marker + selected + marker;
        var caret = length == 0 ? start + marker.Length : start + marker.Length;
        var selectedLength = length == 0 ? 0 : length;
        return new(text[..start] + replacement + text[(start + length)..], caret, selectedLength);
    }

    private static PlanningTextEditResult PrefixLines(string text, int start, int length, Func<int, string> prefix,
        bool removeHeading = false, bool removeList = false, bool removeQuote = false)
    {
        var lineStart = text.LastIndexOf('\n', Math.Max(0, start - 1)); lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var selectionEnd = start + length;
        var lineEndProbe = selectionEnd > lineStart && selectionEnd <= text.Length && text[selectionEnd - 1] == '\n'
            ? selectionEnd - 1
            : selectionEnd;
        var lineEnd = text.IndexOf('\n', lineEndProbe); if (lineEnd < 0) lineEnd = text.Length;
        var original = text[lineStart..lineEnd];
        var lines = original.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var value = lines[index];
            if (removeHeading) value = value.TrimStart().TrimStart('#').TrimStart();
            if (removeQuote && value.TrimStart().StartsWith('>')) value = value.TrimStart()[1..].TrimStart();
            if (removeList)
            {
                var trimmed = value.TrimStart();
                if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal)) trimmed = trimmed[2..];
                else
                {
                    var dot = trimmed.IndexOf(". ", StringComparison.Ordinal);
                    if (dot > 0 && trimmed[..dot].All(char.IsDigit)) trimmed = trimmed[(dot + 2)..];
                }
                value = trimmed;
            }
            lines[index] = prefix(index) + value;
        }
        var replacement = string.Join('\n', lines);
        return new(text[..lineStart] + replacement + text[lineEnd..], lineStart, replacement.Length);
    }

    private static PlanningTextEditResult InsertDivider(string text, int start, int length)
    {
        var before = start > 0 && text[start - 1] != '\n' ? Environment.NewLine : string.Empty;
        var afterIndex = start + length;
        var after = afterIndex < text.Length && text[afterIndex] != '\n' ? Environment.NewLine : string.Empty;
        var replacement = before + "---" + after;
        return new(text[..start] + replacement + text[afterIndex..], start + replacement.Length, 0);
    }
}
