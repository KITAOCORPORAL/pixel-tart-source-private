using System.IO;
using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.Duplicates;

namespace PixelTart.Modules.AssetLibrary;

public sealed record DuplicateImportPrompt(string SourcePath, AssetItem Existing);

public sealed partial class AssetLibraryViewModel
{
    public Func<DuplicateImportPrompt, CancellationToken, Task<DuplicateImportChoice>>? DuplicateImportDecision { get; set; }

    private async Task<AssetLibraryMetadataIndexResult> ImportWithDecisionsAsync(IReadOnlyList<string> paths)
    {
        var token = _lifetimeCancellation.Token;
        var known = (await LoadAllActiveAssetsAsync(token)).Where(item => !string.IsNullOrWhiteSpace(item.ContentHash))
            .GroupBy(item => item.ContentHash!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var started = DateTimeOffset.UtcNow; var imported = 0; var skipped = 0; var missing = 0;
        var warnings = new List<string>();
        foreach (var path in paths)
        {
            token.ThrowIfCancellationRequested();
            string? hash = null;
            if (File.Exists(path))
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true);
                hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
            }
            var behavior = AssetDuplicateBehavior.Skip;
            if (hash is not null && known.TryGetValue(hash, out var existing))
            {
                var choice = DuplicateImportDecision is null ? DuplicateImportChoice.Skip :
                    await DuplicateImportDecision(new(path, existing), token);
                if (choice != DuplicateImportChoice.ImportAnyway) { skipped++; continue; }
                behavior = AssetDuplicateBehavior.ImportIndependentRecord;
            }
            var result = await _repository.ImportAsync([new(path, ComputeContentHash: true, DuplicateBehavior: behavior)], token);
            imported += result.ImportedCount; skipped += result.SkippedCount; missing += result.MissingCount; warnings.AddRange(result.Warnings);
            if (result.Cancelled) return new(imported, skipped, missing, true, DateTimeOffset.UtcNow - started, warnings);
            if (hash is not null && !known.ContainsKey(hash))
            {
                var page = await _repository.QueryAsync(new AssetLibraryQuery(SearchText: Path.GetFileName(path), PageSize: 500), token);
                var added = page.Items.FirstOrDefault(item => string.Equals(item.ContentHash, hash, StringComparison.OrdinalIgnoreCase));
                if (added is not null) known[hash] = added;
            }
        }
        return new(imported, skipped, missing, false, DateTimeOffset.UtcNow - started, warnings);
    }
}
