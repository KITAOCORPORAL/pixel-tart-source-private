using System.Text;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.OnlineSelection;

public enum SelectionResultExportFormat
{
    Txt,
    Csv
}

/// <summary>Exports only selected result metadata; never copies RAW or local paths.</summary>
public sealed class SelectionResultExportService
{
    public async Task<string> ExportTxtAsync(
        FinalSelectionSnapshot snapshot,
        string outputDirectory,
        CancellationToken cancellationToken = default) =>
        await ExportAsync(snapshot, outputDirectory, SelectionResultExportFormat.Txt, cancellationToken).ConfigureAwait(false);

    public async Task<string> ExportCsvAsync(
        FinalSelectionSnapshot snapshot,
        string outputDirectory,
        CancellationToken cancellationToken = default) =>
        await ExportAsync(snapshot, outputDirectory, SelectionResultExportFormat.Csv, cancellationToken).ConfigureAwait(false);

    public async Task<string> ExportAsync(
        FinalSelectionSnapshot snapshot,
        string outputDirectory,
        SelectionResultExportFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("需要明确的导出目录。", nameof(outputDirectory));
        var directory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(directory);
        var extension = format == SelectionResultExportFormat.Txt ? ".txt" : ".csv";
        var stem = $"selection-{snapshot.ProjectId:N}-v{snapshot.SelectionVersion}";
        var path = NextPath(directory, stem, extension);
        var content = format == SelectionResultExportFormat.Txt ? BuildTxt(snapshot) : BuildCsv(snapshot);
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
        return path;
    }

    private static string BuildTxt(FinalSelectionSnapshot snapshot) => string.Join(
        Environment.NewLine,
        snapshot.AssetItems.Where(item => item.Selected)
            .Select(item => SelectionPrivacyPolicy.SafeFileName(item.OriginalFileName))) + Environment.NewLine;

    private static string BuildCsv(FinalSelectionSnapshot snapshot)
    {
        var lines = new List<string> { "SelectionAssetId,OriginalFileName,Favorite,ExtraSelected,Comment" };
        lines.AddRange(snapshot.AssetItems.Where(item => item.Selected).Select(item => string.Join(",",
            item.ImageId,
            Csv(SelectionPrivacyPolicy.SafeFileName(item.OriginalFileName)),
            item.Favorite ? "true" : "false",
            item.ExtraSelected ? "true" : "false",
            Csv(item.CustomerNote ?? string.Empty))));
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string NextPath(string directory, string stem, string extension)
    {
        for (var index = 1; index < 100_000; index++)
        {
            var suffix = index == 1 ? string.Empty : $"_{index}";
            var path = Path.Combine(directory, stem + suffix + extension);
            if (!File.Exists(path)) return path;
        }
        throw new IOException("无法创建唯一的选片结果导出文件名。");
    }
}
