using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Core.Services.OnlineSelection;

public sealed class SelectionResultSyncService(FileNameNormalizer normalizer)
{
    public async Task<SelectionSyncResult> SynchronizeAsync(
        SelectionFinalResult finalResult,
        IEnumerable<string> rawFiles,
        string archiveDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(finalResult);
        if (string.IsNullOrWhiteSpace(archiveDirectory))
            throw new ArgumentException("需要明确指定选片结果归档目录。", nameof(archiveDirectory));
        var normalizedRawFiles = rawFiles
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Where(File.Exists)
            .Where(path => MediaExtensionPolicy.DefaultRawExtensions.Contains(
                Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
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
        var archivePath = await WriteArchiveAsync(finalResult, matches, archiveDirectory, cancellationToken).ConfigureAwait(false);
        return new SelectionSyncResult(state, finalResult.SelectionProjectId, matches, archivePath,
            state == SelectionSyncState.Completed ? "客户选片结果已同步并归档。" : "选片结果已归档，部分 RAW 匹配需要确认。");
    }

    private static async Task<string> WriteArchiveAsync(
        SelectionFinalResult result,
        IReadOnlyList<SelectionRawMatch> matches,
        string archiveDirectory,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetFullPath(archiveDirectory);
        Directory.CreateDirectory(directory);
        var stem = $"selection-{result.SelectionProjectId:N}-{result.ConfirmedAtUtc:yyyyMMddHHmmss}";
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
        string? ownedStagingPath = null;
        try
        {
            var staging = CreateOwnedStagingFile(directory);
            ownedStagingPath = staging.Path;
            await using (staging.Stream)
            {
                var bytes = new UTF8Encoding(false).GetBytes(json);
                await staging.Stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await staging.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                staging.Stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            var archivePath = MoveToNumberedPath(ownedStagingPath, directory, stem, ".json");
            ownedStagingPath = null;
            return archivePath;
        }
        finally
        {
            SafeDeleteOwned(ownedStagingPath);
        }
    }

    private static (string Path, FileStream Stream) CreateOwnedStagingFile(string directory)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var path = Path.Combine(directory, $".selection-result-{Guid.NewGuid():N}.tmp");
            try
            {
                return (path, new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 32 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan));
            }
            catch (IOException) when (File.Exists(path))
            {
            }
        }
        throw new IOException("无法创建选片结果临时文件。");
    }

    private static string MoveToNumberedPath(string ownedStagingPath, string directory, string stem, string extension)
    {
        for (var index = 1; index < 100_000; index++)
        {
            var suffix = index == 1 ? string.Empty : $"_{index}";
            var candidate = Path.Combine(directory, stem + suffix + extension);
            try
            {
                File.Move(ownedStagingPath, candidate, overwrite: false);
                return candidate;
            }
            catch (IOException) when (File.Exists(ownedStagingPath) && File.Exists(candidate))
            {
            }
        }
        throw new IOException("无法创建唯一的选片结果文件名。");
    }

    private static void SafeDeleteOwned(string? ownedStagingPath)
    {
        if (string.IsNullOrWhiteSpace(ownedStagingPath)) return;
        try { if (File.Exists(ownedStagingPath)) File.Delete(ownedStagingPath); } catch { }
    }
}
