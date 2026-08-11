using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Core.Services.OnlineSelection;

public sealed class SelectionResultSyncService(FileNameNormalizer normalizer)
{
    public Task<SelectionSyncResult> SynchronizeAsync(
        SelectionFinalResult finalResult,
        IEnumerable<string> rawFiles,
        string archiveDirectory,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        ArgumentNullException.ThrowIfNull(finalResult);
        var normalizedRawFiles = rawFiles
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var index = normalizedRawFiles.GroupBy(path => normalizer.Normalize(Path.GetFileName(path)).ComparisonName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var numericIndex = normalizedRawFiles.GroupBy(path => normalizer.Normalize(Path.GetFileName(path)).NumericId, StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.ToArray(), StringComparer.OrdinalIgnoreCase);

        var matches = new List<SelectionRawMatch>();
        foreach (var item in finalResult.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!item.Selected)
            {
                matches.Add(new(item, SelectionRawMatchStatus.NotSelected, null, [], "客户未选择。"));
                continue;
            }
            var normalized = normalizer.Normalize(item.OriginalFileName);
            var candidates = index.GetValueOrDefault(normalized.ComparisonName, []);
            if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(normalized.NumericId))
                candidates = numericIndex.GetValueOrDefault(normalized.NumericId, []);
            matches.Add(candidates.Count switch
            {
                0 => new(item, SelectionRawMatchStatus.NotFound, null, [], "未找到同名 RAW。"),
                1 => new(item, SelectionRawMatchStatus.Matched, candidates[0], candidates, "已匹配 RAW。"),
                _ => new(item, SelectionRawMatchStatus.Conflict, null, candidates, "找到多个 RAW 候选，需要确认。")
            });
        }

        var state = matches.Any(match => match.Status is SelectionRawMatchStatus.Conflict or SelectionRawMatchStatus.NotFound)
            ? SelectionSyncState.NeedsAttention
            : SelectionSyncState.Completed;
        var archivePath = WriteArchive(finalResult, matches, archiveDirectory, cancellationToken);
        return new SelectionSyncResult(state, finalResult.SelectionProjectId, matches, archivePath,
            state == SelectionSyncState.Completed ? "客户选片结果已同步并归档。" : "选片结果已归档，部分 RAW 匹配需要确认。");
    }, cancellationToken);

    private static string WriteArchive(
        SelectionFinalResult result,
        IReadOnlyList<SelectionRawMatch> matches,
        string archiveDirectory,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetFullPath(archiveDirectory);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"selection-{result.SelectionProjectId:N}-{result.ConfirmedAtUtc:yyyyMMddHHmmss}.json");
        var publicArchive = new
        {
            result.SelectionProjectId,
            result.ConfirmedAtUtc,
            Items = matches.Select(match => new
            {
                match.Selection.ImageId,
                FileName = SelectionPrivacyPolicy.SafeFileName(match.Selection.OriginalFileName),
                match.Selection.Selected,
                match.Selection.Favorite,
                match.Selection.CustomerNote,
                match.Selection.ExtraSelected,
                MatchStatus = match.Status.ToString()
            }).ToArray()
        };
        var json = JsonSerializer.Serialize(publicArchive, new JsonSerializerOptions { WriteIndented = true });
        cancellationToken.ThrowIfCancellationRequested();
        File.WriteAllText(path, json, new UTF8Encoding(false));
        return path;
    }
}
