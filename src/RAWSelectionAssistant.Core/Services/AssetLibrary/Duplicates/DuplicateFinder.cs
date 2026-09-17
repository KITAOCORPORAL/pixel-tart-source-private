using System.Numerics;
using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary.Duplicates;

public enum DuplicateGroupKind { Exact, Similar }

public sealed record DuplicateReferenceUsage(
    int InspirationBoardCount = 0,
    int CanvasCount = 0,
    int ProjectCount = 0,
    int PlanningCount = 0)
{
    public int Total => InspirationBoardCount + CanvasCount + ProjectCount + PlanningCount;
    public bool IsInUse => Total > 0;
}

public sealed record DuplicateCandidate(
    AssetItem Asset,
    AssetVisualAnalysisResult? Analysis = null,
    DuplicateReferenceUsage? Usage = null,
    int TagCount = 0,
    int WorkflowPriority = 0)
{
    public DuplicateReferenceUsage EffectiveUsage => Usage ?? new();
    public long PixelCount => Math.Max(0, (long)(Asset.Width ?? 0) * (Asset.Height ?? 0));
}

public sealed record DuplicateGroup(
    DuplicateGroupKind Kind,
    IReadOnlyList<DuplicateCandidate> Items,
    Guid SuggestedKeepAssetId,
    double Similarity)
{
    public int CopyCount => Items.Count;
}

public static class DuplicateFinder
{
    public static IReadOnlyList<DuplicateGroup> FindExact(IEnumerable<DuplicateCandidate> candidates) =>
        candidates
            .Where(item => !string.IsNullOrWhiteSpace(item.Asset.ContentHash))
            .GroupBy(item => item.Asset.ContentHash!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => CreateGroup(DuplicateGroupKind.Exact, group.ToArray(), 1))
            .OrderByDescending(group => group.CopyCount)
            .ThenBy(group => group.Items[0].Asset.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    public static IReadOnlyList<DuplicateGroup> FindSimilar(IEnumerable<DuplicateCandidate> source, double strictness = .65)
    {
        var candidates = source.Where(item => item.Analysis?.SimilaritySignatures.Count > 0).ToArray();
        if (candidates.Length < 2) return [];
        strictness = Math.Clamp(strictness, 0, 1);
        var minimum = .72 + strictness * .20;
        var parent = Enumerable.Range(0, candidates.Length).ToArray();
        var pairScores = new Dictionary<(int, int), double>();
        int Root(int index) { while (parent[index] != index) { parent[index] = parent[parent[index]]; index = parent[index]; } return index; }
        void Union(int left, int right) { left = Root(left); right = Root(right); if (left != right) parent[right] = left; }

        for (var left = 0; left < candidates.Length; left++)
        for (var right = left + 1; right < candidates.Length; right++)
        {
            if (!string.IsNullOrWhiteSpace(candidates[left].Asset.ContentHash) &&
                string.Equals(candidates[left].Asset.ContentHash, candidates[right].Asset.ContentHash, StringComparison.OrdinalIgnoreCase)) continue;
            var score = Similarity(candidates[left].Analysis!, candidates[right].Analysis!);
            if (score < minimum) continue;
            pairScores[(left, right)] = score;
            Union(left, right);
        }

        return Enumerable.Range(0, candidates.Length)
            .GroupBy(Root)
            .Select(group => group.ToArray())
            .Where(indices => indices.Length > 1)
            .Select(indices =>
            {
                var values = indices.Select(index => candidates[index]).ToArray();
                var scores = from a in indices from b in indices where a < b select pairScores.GetValueOrDefault((a, b), Similarity(candidates[a].Analysis!, candidates[b].Analysis!));
                return CreateGroup(DuplicateGroupKind.Similar, values, scores.Average());
            })
            .OrderByDescending(group => group.Similarity)
            .ThenByDescending(group => group.CopyCount)
            .ToArray();
    }

    public static double Similarity(AssetVisualAnalysisResult left, AssetVisualAnalysisResult right)
    {
        var signature = SignatureSimilarity(left.SimilaritySignatures, right.SimilaritySignatures);
        var visual = VisualSimilarityScorer.Score(left, right).Overall / 100d;
        return Math.Clamp(signature * .78 + visual * .22, 0, 1);
    }

    private static double SignatureSimilarity(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        var best = 0d;
        foreach (var leftText in left)
        foreach (var rightText in right)
        {
            var leftParts = leftText.Split(':', 2); var rightParts = rightText.Split(':', 2);
            if (leftParts.Length != 2 || rightParts.Length != 2 || !string.Equals(leftParts[0], rightParts[0], StringComparison.Ordinal) ||
                !ulong.TryParse(leftParts[1], System.Globalization.NumberStyles.HexNumber, null, out var leftHash) ||
                !ulong.TryParse(rightParts[1], System.Globalization.NumberStyles.HexNumber, null, out var rightHash)) continue;
            best = Math.Max(best, 1 - BitOperations.PopCount(leftHash ^ rightHash) / 64d);
        }
        return best;
    }

    private static DuplicateGroup CreateGroup(DuplicateGroupKind kind, IReadOnlyList<DuplicateCandidate> items, double similarity)
    {
        var suggested = items
            .OrderByDescending(item => item.EffectiveUsage.Total)
            .ThenByDescending(item => item.Asset.Rating)
            .ThenByDescending(item => item.TagCount)
            .ThenByDescending(item => item.WorkflowPriority)
            .ThenByDescending(item => item.PixelCount)
            .ThenByDescending(item => item.Asset.FileSize)
            .ThenByDescending(item => item.Asset.ModifiedAt)
            .ThenBy(item => item.Asset.AssetId)
            .First();
        return new(kind, items, suggested.Asset.AssetId, similarity);
    }
}

public enum DuplicateImportChoice { Skip, ViewExisting, ImportAnyway }

public sealed record DuplicateImportCheck(bool AlreadyExists, Guid? ExistingAssetId, DuplicateImportChoice DefaultChoice)
{
    public static DuplicateImportCheck NotFound { get; } = new(false, null, DuplicateImportChoice.Skip);
}

public static class DuplicateImportGuard
{
    public static async Task<DuplicateImportCheck> CheckAsync(
        string sourcePath,
        IEnumerable<AssetItem> existing,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        var match = existing.FirstOrDefault(item => string.Equals(item.ContentHash, hash, StringComparison.OrdinalIgnoreCase));
        return match is null ? DuplicateImportCheck.NotFound : new(true, match.AssetId, DuplicateImportChoice.Skip);
    }
}
