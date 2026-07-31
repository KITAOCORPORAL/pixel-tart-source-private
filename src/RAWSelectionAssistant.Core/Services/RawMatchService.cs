using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class RawMatchService(FileNameNormalizer normalizer)
{
    public Task<IReadOnlyList<MatchDecision>> MatchAsync(
        IEnumerable<SelectionItem> items,
        RawIndexSnapshot index,
        CancellationToken cancellationToken) => Task.Run<IReadOnlyList<MatchDecision>>(() =>
    {
        var decisions = new List<MatchDecision>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = normalizer.Normalize(item.OriginalInput);
            var duplicateKey = normalized.NumericId.Length > 0
                ? $"N:{normalized.NumericId}"
                : $"F:{normalized.ComparisonName}";
            var isDuplicate = !seenKeys.Add(duplicateKey);

            IReadOnlyList<RawFileEntry> candidates = [];
            if (normalized.ComparisonName.Length > 0 && index.ByFullName.TryGetValue(normalized.ComparisonName, out var fullMatches))
            {
                candidates = fullMatches;
            }
            else if (normalized.NumericId.Length > 0 && index.ByNumericId.TryGetValue(normalized.NumericId, out var numberMatches))
            {
                candidates = numberMatches;
            }

            var status = candidates.Count switch
            {
                0 => MatchStatus.NotFound,
                1 => MatchStatus.Matched,
                _ => MatchStatus.Conflict
            };
            var selectedRaw = candidates.Count == 1 ? candidates[0] : null;
            var note = isDuplicate ? "重复输入，默认仅复制一次" : status switch
            {
                MatchStatus.Conflict => "请选择一个 RAW 候选文件",
                MatchStatus.NotFound => "完整名称和数字编号均未匹配",
                _ => string.Empty
            };

            decisions.Add(new MatchDecision(
                item.Id,
                normalized.ComparisonName,
                normalized.NumericId,
                status,
                selectedRaw,
                candidates,
                isDuplicate,
                note));
        }

        return decisions;
    }, cancellationToken);
}
