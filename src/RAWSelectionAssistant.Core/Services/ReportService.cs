using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class ReportService(ILogService logService)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public async Task ExportAsync(
        string outputDirectory,
        IEnumerable<SelectionItem> selectionItems,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var records = selectionItems.Select(ToRecord).ToList();

        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("原始输入,标准化名称,数字编号,匹配状态,RAW文件名,RAW原始路径,RAW输出路径,候选数量,是否手动确认,是否复制成功,错误信息,操作时间,软件名称,软件版本");
        foreach (var record in records)
        {
            csvBuilder.AppendLine(string.Join(',', new[]
            {
                Escape(record.原始输入), Escape(record.标准化名称), Escape(record.数字编号), Escape(record.匹配状态),
                Escape(record.RAW文件名), Escape(record.RAW原始路径), Escape(record.RAW输出路径), record.候选数量.ToString(),
                record.是否手动确认 ? "是" : "否", record.是否复制成功 ? "是" : "否", Escape(record.错误信息), Escape(record.操作时间),
                Escape(record.软件名称), Escape(record.软件版本)
            }));
        }

        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "匹配报告.csv"), csvBuilder.ToString(), new UTF8Encoding(true), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "匹配报告.json"), JsonSerializer.Serialize(records, _jsonOptions), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);

        var logLines = new List<string>
        {
            $"{Branding.ProductName}操作日志",
            $"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"选片记录：{records.Count}",
            string.Empty
        };
        logLines.AddRange(records.Select(x => $"[{x.匹配状态}] {x.原始输入} -> {x.RAW输出路径} {x.错误信息}"));
        await File.WriteAllLinesAsync(Path.Combine(outputDirectory, "操作日志.txt"), logLines, new UTF8Encoding(true), cancellationToken).ConfigureAwait(false);
        logService.Info($"已导出匹配报告：{outputDirectory}");
    }

    private static ReportRecord ToRecord(SelectionItem item) => new()
    {
        原始输入 = item.OriginalInput,
        标准化名称 = item.NormalizedName,
        数字编号 = item.NumericId,
        匹配状态 = item.Status.ToChinese(),
        RAW文件名 = item.SelectedRaw?.FileName ?? string.Empty,
        RAW原始路径 = item.SelectedRaw?.FullPath ?? string.Empty,
        RAW输出路径 = item.RawOutputPath,
        候选数量 = item.CandidateCount,
        是否手动确认 = item.Status == MatchStatus.ManuallyConfirmed || item.Note.Contains("手动确认", StringComparison.Ordinal),
        是否复制成功 = item.Status == MatchStatus.Copied,
        错误信息 = item.ErrorMessage,
        操作时间 = item.OperationTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
        软件名称 = Branding.ProductName,
        软件版本 = Branding.ProductVersion
    };

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private sealed class ReportRecord
    {
        public string 原始输入 { get; set; } = string.Empty;
        public string 标准化名称 { get; set; } = string.Empty;
        public string 数字编号 { get; set; } = string.Empty;
        public string 匹配状态 { get; set; } = string.Empty;
        public string RAW文件名 { get; set; } = string.Empty;
        public string RAW原始路径 { get; set; } = string.Empty;
        public string RAW输出路径 { get; set; } = string.Empty;
        public int 候选数量 { get; set; }
        public bool 是否手动确认 { get; set; }
        public bool 是否复制成功 { get; set; }
        public string 错误信息 { get; set; } = string.Empty;
        public string 操作时间 { get; set; } = string.Empty;
        public string 软件名称 { get; set; } = string.Empty;
        public string 软件版本 { get; set; } = string.Empty;
    }
}
