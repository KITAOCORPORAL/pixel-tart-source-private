using System.Text.RegularExpressions;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed partial class InputParserService(ILogService logService)
{
    private static readonly HashSet<string> SupportedInputExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".txt", ".csv" };

    [GeneratedRegex(@"(?<![\p{L}\p{N}_])(?:[\p{L}_][\p{L}\p{N}_-]*\d+|\d+)(?:\(\d+\)|[-_]?COPY|[-_]?副本|副本)?(?:\.[A-Za-z0-9]{1,8})?(?![\p{L}\p{N}_])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputTokenRegex();

    [GeneratedRegex(@"\s+\((\d+)\)(?=(?:\.[A-Za-z0-9]{1,8})?(?:\s|$|[,，、;；|]))", RegexOptions.CultureInvariant)]
    private static partial Regex CopySuffixSpaceRegex();

    public IReadOnlyList<string> ParseText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var protectedText = CopySuffixSpaceRegex().Replace(text, "($1)");
        return InputTokenRegex()
            .Matches(protectedText)
            .Select(match => match.Value.Trim())
            .Where(value => value.Length > 0)
            .ToList();
    }

    public SelectionImportLimitResult ParseTextForProject(
        string? text,
        IEnumerable<MediaSelectionItem> existingItems,
        ProjectEntitlementService entitlementService,
        bool tutorialBypass = false)
    {
        var incoming = ParseText(text).Select(value => new ParsedSelectionInput(value));
        return entitlementService.ApplySelectionLimit(existingItems, incoming, tutorialBypass);
    }

    public async Task<SelectionImportLimitResult> ParseDroppedItemsForProjectAsync(
        IEnumerable<string> paths,
        IEnumerable<MediaSelectionItem> existingItems,
        ProjectEntitlementService entitlementService,
        bool tutorialBypass,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var incoming = await ParseDroppedSelectionInputsAsync(paths, progress, cancellationToken).ConfigureAwait(false);
        return entitlementService.ApplySelectionLimit(existingItems, incoming, tutorialBypass);
    }

    public async Task<IReadOnlyList<string>> ParseDroppedItemsAsync(
        IEnumerable<string> paths,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var inputs = await ParseDroppedSelectionInputsAsync(paths, progress, cancellationToken).ConfigureAwait(false);
        return inputs.Select(x => x.OriginalInput).ToList();
    }

    public async Task<IReadOnlyList<ParsedSelectionInput>> ParseDroppedSelectionInputsAsync(
        IEnumerable<string> paths,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var result = new List<ParsedSelectionInput>();
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                await ParseFileAsync(path, result, cancellationToken).ConfigureAwait(false);
            }
            else if (Directory.Exists(path))
            {
                await ParseDirectoryAsync(path, result, progress, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                logService.Error($"拖入项目不存在或无法访问：{path}");
            }
        }

        return result;
    }

    private async Task ParseDirectoryAsync(
        string root,
        List<ParsedSelectionInput> result,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        long processed = 0;

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = stack.Pop();
            progress?.Report(new OperationProgress("读取客户选片", directory, processed));

            foreach (var subdirectory in TryEnumerate(() => Directory.EnumerateDirectories(directory), directory))
            {
                if (!IsHiddenOrSystem(subdirectory))
                {
                    stack.Push(subdirectory);
                }
            }

            foreach (var file in TryEnumerate(() => Directory.EnumerateFiles(directory), directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;
                if (!IsHiddenOrSystem(file) && SupportedInputExtensions.Contains(Path.GetExtension(file)))
                {
                    await ParseFileAsync(file, result, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task ParseFileAsync(string path, List<ParsedSelectionInput> result, CancellationToken cancellationToken)
    {
        try
        {
            var extension = Path.GetExtension(path);
            if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new ParsedSelectionInput(Path.GetFileName(path), Path.GetFullPath(path)));
                return;
            }

            if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                result.AddRange(ParseText(text).Select(value => new ParsedSelectionInput(value)));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            logService.Error($"无法读取客户选片文件：{path}", ex);
        }
    }

    private IReadOnlyList<string> TryEnumerate(Func<IEnumerable<string>> action, string directory)
    {
        try
        {
            return action().ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException)
        {
            logService.Error($"无法读取客户选片目录：{directory}", ex);
            return [];
        }
    }

    private static bool IsHiddenOrSystem(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return attributes.HasFlag(FileAttributes.Hidden) || attributes.HasFlag(FileAttributes.System);
        }
        catch
        {
            return true;
        }
    }
}
