using System.Text;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

public sealed record AssetSelectionExportResult(int ExportedCount, int MissingCount, IReadOnlyList<string> OutputPaths);

public sealed class AssetSelectionExportService
{
    public async Task<AssetSelectionExportResult> ExportFilesAsync(IEnumerable<AssetItem> assets, string outputDirectory, bool preferManagedCopy, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(outputDirectory); Directory.CreateDirectory(root);
        var outputs = new List<string>(); var missing = 0;
        foreach (var asset in assets.DistinctBy(item => item.AssetId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = preferManagedCopy && !string.IsNullOrWhiteSpace(asset.ManagedCopyPath) ? asset.ManagedCopyPath! : asset.SourcePath;
            if (!File.Exists(source)) { missing++; continue; }
            var target = UniquePath(root, Path.GetFileName(source));
            await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false); outputs.Add(target);
        }
        return new(outputs.Count, missing, outputs);
    }

    public async Task ExportMetadataCsvAsync(IEnumerable<AssetItem> assets, string outputPath, CancellationToken cancellationToken = default)
    {
        var target = Path.GetFullPath(outputPath); if (File.Exists(target)) throw new IOException("目标 CSV 已存在；不会覆盖。");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await using var stream = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(true));
        await writer.WriteLineAsync("AssetId,DisplayName,SourcePath,MediaType,FileSize,Width,Height,CaptureTime,AddedAt,Rating,Comment,ImportMode,IsMissing,IsArchived").ConfigureAwait(false);
        foreach (var asset in assets.DistinctBy(item => item.AssetId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = new[] { asset.AssetId.ToString("D"), asset.DisplayName, asset.SourcePath, asset.MediaType, asset.FileSize.ToString(), asset.Width?.ToString() ?? "", asset.Height?.ToString() ?? "", asset.CaptureTime?.ToString("O") ?? "", asset.AddedAt.ToString("O"), asset.Rating.ToString(), asset.Comment, asset.ImportMode.ToString(), asset.IsMissing.ToString(), asset.IsArchived.ToString() };
            await writer.WriteLineAsync(string.Join(',', values.Select(Csv))).ConfigureAwait(false);
        }
    }

    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static string UniquePath(string root, string fileName)
    {
        var candidate = Path.Combine(root, fileName); if (!File.Exists(candidate)) return candidate;
        var stem = Path.GetFileNameWithoutExtension(fileName); var extension = Path.GetExtension(fileName);
        for (var index = 2; ; index++) { candidate = Path.Combine(root, $"{stem} ({index}){extension}"); if (!File.Exists(candidate)) return candidate; }
    }
}
