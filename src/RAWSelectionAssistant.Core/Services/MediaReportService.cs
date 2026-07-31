using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class MediaReportService(ILogService logService)
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task ExportAsync(
        string outputDirectory,
        CollectionCategory collectionCategory,
        IEnumerable<MediaSelectionItem> selectionItems,
        CancellationToken cancellationToken = default,
        ReportExportOptions? options = null)
    {
        Directory.CreateDirectory(outputDirectory);
        options ??= ReportExportOptions.Pro;
        var records = selectionItems.SelectMany(item => item.FormatResults.Count == 0
            ? [ToRecord(item, null, collectionCategory)]
            : item.FormatResults.Select(result => ToRecord(item, result, collectionCategory))).ToList();
        var headers = new[]
        {
            "原始输入", "标准化名称", "数字编号", "归片类别", "目标扩展名", "文件类别", "客户输入文件路径",
            "是否使用客户返回文件", "总体状态", "JPG匹配状态", "RAW匹配状态", "其他格式匹配状态",
            "匹配文件总数", "实际复制文件总数", "部分匹配原因", "匹配状态", "候选数量", "文件名",
            "输出分类目录", "原始源文件路径", "最终输出路径", "错误信息", "操作时间",
            "JpgSourceType", "JpgFileSizeBytes", "JpgPixelWidth", "JpgPixelHeight", "JpgHasExif",
            "JpgCameraMake", "JpgCameraModel", "JpgDateTimeOriginal", "JpgHasIccProfile", "JpgSoftwareTag",
            "JpgQualityWarnings", "UsedCustomerReturnedJpg", "CustomerJpgManualConfirmation", "RecommendedCandidateReason",
            "软件名称", "软件版本"
        };
        if (options.IncludeCsv)
        {
            var csv = new StringBuilder();
            csv.AppendLine(string.Join(',', headers.Select(Escape)));
            foreach (var record in records)
            {
                csv.AppendLine(string.Join(',', record.Values.Select(Escape)));
            }
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "匹配报告.csv"), csv.ToString(), new UTF8Encoding(true), cancellationToken).ConfigureAwait(false);
        }

        if (options.IncludeJson)
        {
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "匹配报告.json"), JsonSerializer.Serialize(records.Select(x => x.Json), _jsonOptions), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        }

        if (options.IncludeLog)
        {
            var log = new List<string>
            {
                $"{Branding.ProductName}操作日志",
                $"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"归片类别：{collectionCategory.ToChinese()}",
                $"客户选片记录：{selectionItems.Count()}",
                $"目标格式记录：{records.Count}",
                string.Empty
            };
            log.AddRange(records.Select(x => $"[{x.Json.总体状态}/{x.Json.匹配状态}] {x.Json.原始输入} -> {x.Json.最终输出路径} {x.Json.部分匹配原因}"));
            await File.WriteAllLinesAsync(Path.Combine(outputDirectory, "操作日志.txt"), log, new UTF8Encoding(true), cancellationToken).ConfigureAwait(false);
        }

        logService.Info($"已导出所选归片报告：CSV={options.IncludeCsv}，JSON={options.IncludeJson}，日志={options.IncludeLog}，目录={outputDirectory}");
    }

    private static ReportProjection ToRecord(MediaSelectionItem item, MediaFormatMatchResult? result, CollectionCategory category)
    {
        var copiedCount = item.FormatResults.Count(x => x.Status == MatchStatus.Copied);
        var jpegResult = item.JpegResult;
        var jpegFile = jpegResult?.SelectedFile ?? jpegResult?.RecommendedFile;
        var jpegQuality = jpegFile?.JpegQuality;
        var otherStatus = string.Join("；", item.FormatResults.Where(x => x.Category is not FileCategory.Jpeg and not FileCategory.Raw).Select(x => $"{x.DisplayName}:{x.Status.ToChinese()}"));
        var outputCategory = result?.SelectedFile?.Category switch { FileCategory.Jpeg => "JPG", FileCategory.Raw => "RAW", null => string.Empty, _ => "OTHER" };
        var json = new MediaReportRecord
        {
            原始输入 = item.OriginalInput,
            标准化名称 = item.NormalizedName,
            数字编号 = item.NumericId,
            归片类别 = category.ToChinese(),
            目标扩展名 = result is null ? string.Empty : string.Join('|', result.TargetExtensions),
            文件类别 = result?.Category.ToChinese() ?? string.Empty,
            客户输入文件路径 = item.CustomerInputFilePath,
            是否使用客户返回文件 = result?.UsedCustomerFile ?? false,
            总体状态 = item.OverallStatus.ToString(),
            JPG匹配状态 = item.JpegResult?.Status.ToChinese() ?? string.Empty,
            RAW匹配状态 = item.RawResult?.Status.ToChinese() ?? string.Empty,
            其他格式匹配状态 = otherStatus,
            匹配文件总数 = item.MatchedFileCount,
            实际复制文件总数 = copiedCount,
            部分匹配原因 = item.Note,
            匹配状态 = result?.Status.ToChinese() ?? item.OverallStatus.ToChinese(),
            候选数量 = result?.CandidateCount ?? 0,
            文件名 = result?.SelectedFile?.FileName ?? string.Empty,
            输出分类目录 = outputCategory,
            原始源文件路径 = result?.SelectedFile?.FullPath ?? string.Empty,
            最终输出路径 = result?.OutputPath ?? string.Empty,
            错误信息 = result?.ErrorMessage ?? string.Empty,
            操作时间 = result?.OperationTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
            JpgSourceType = (jpegResult?.FinalJpegSourceType ?? jpegFile?.JpegSourceType)?.ToString() ?? string.Empty,
            JpgFileSizeBytes = jpegQuality?.FileSizeBytes ?? jpegFile?.Size ?? 0,
            JpgPixelWidth = jpegQuality?.PixelWidth,
            JpgPixelHeight = jpegQuality?.PixelHeight,
            JpgHasExif = jpegQuality?.HasExif,
            JpgCameraMake = jpegQuality?.CameraMake ?? string.Empty,
            JpgCameraModel = jpegQuality?.CameraModel ?? string.Empty,
            JpgDateTimeOriginal = jpegQuality?.DateTimeOriginal?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
            JpgHasIccProfile = jpegQuality?.HasIccProfile,
            JpgSoftwareTag = jpegQuality?.SoftwareTag ?? string.Empty,
            JpgQualityWarnings = jpegQuality?.QualityWarningsText ?? string.Empty,
            UsedCustomerReturnedJpg = jpegResult?.UsedCustomerFile ?? false,
            CustomerJpgManualConfirmation = jpegResult?.CustomerJpgManualConfirmation ?? false,
            RecommendedCandidateReason = jpegResult?.RecommendedCandidateReason ?? string.Empty,
            软件名称 = Branding.ProductName,
            软件版本 = Branding.ProductVersion
        };
        return new ReportProjection(json, [
            json.原始输入, json.标准化名称, json.数字编号, json.归片类别, json.目标扩展名, json.文件类别,
            json.客户输入文件路径, json.是否使用客户返回文件 ? "是" : "否", json.总体状态, json.JPG匹配状态,
            json.RAW匹配状态, json.其他格式匹配状态, json.匹配文件总数.ToString(), json.实际复制文件总数.ToString(),
            json.部分匹配原因, json.匹配状态, json.候选数量.ToString(), json.文件名, json.输出分类目录,
            json.原始源文件路径, json.最终输出路径, json.错误信息, json.操作时间,
            json.JpgSourceType, json.JpgFileSizeBytes.ToString(), json.JpgPixelWidth?.ToString() ?? "未知", json.JpgPixelHeight?.ToString() ?? "未知",
            NullableBoolean(json.JpgHasExif), json.JpgCameraMake, json.JpgCameraModel, json.JpgDateTimeOriginal,
            NullableBoolean(json.JpgHasIccProfile), json.JpgSoftwareTag, json.JpgQualityWarnings,
            json.UsedCustomerReturnedJpg ? "是" : "否", json.CustomerJpgManualConfirmation ? "是" : "否", json.RecommendedCandidateReason,
            json.软件名称, json.软件版本
        ]);
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    private static string NullableBoolean(bool? value) => value switch { true => "是", false => "否", _ => "未知" };
    private sealed record ReportProjection(MediaReportRecord Json, IReadOnlyList<string> Values);

    private sealed class MediaReportRecord
    {
        public string 原始输入 { get; set; } = string.Empty;
        public string 标准化名称 { get; set; } = string.Empty;
        public string 数字编号 { get; set; } = string.Empty;
        public string 归片类别 { get; set; } = string.Empty;
        public string 目标扩展名 { get; set; } = string.Empty;
        public string 文件类别 { get; set; } = string.Empty;
        public string 客户输入文件路径 { get; set; } = string.Empty;
        public bool 是否使用客户返回文件 { get; set; }
        public string 总体状态 { get; set; } = string.Empty;
        public string JPG匹配状态 { get; set; } = string.Empty;
        public string RAW匹配状态 { get; set; } = string.Empty;
        public string 其他格式匹配状态 { get; set; } = string.Empty;
        public int 匹配文件总数 { get; set; }
        public int 实际复制文件总数 { get; set; }
        public string 部分匹配原因 { get; set; } = string.Empty;
        public string 匹配状态 { get; set; } = string.Empty;
        public int 候选数量 { get; set; }
        public string 文件名 { get; set; } = string.Empty;
        public string 输出分类目录 { get; set; } = string.Empty;
        public string 原始源文件路径 { get; set; } = string.Empty;
        public string 最终输出路径 { get; set; } = string.Empty;
        public string 错误信息 { get; set; } = string.Empty;
        public string 操作时间 { get; set; } = string.Empty;
        public string JpgSourceType { get; set; } = string.Empty;
        public long JpgFileSizeBytes { get; set; }
        public int? JpgPixelWidth { get; set; }
        public int? JpgPixelHeight { get; set; }
        public bool? JpgHasExif { get; set; }
        public string JpgCameraMake { get; set; } = string.Empty;
        public string JpgCameraModel { get; set; } = string.Empty;
        public string JpgDateTimeOriginal { get; set; } = string.Empty;
        public bool? JpgHasIccProfile { get; set; }
        public string JpgSoftwareTag { get; set; } = string.Empty;
        public string JpgQualityWarnings { get; set; } = string.Empty;
        public bool UsedCustomerReturnedJpg { get; set; }
        public bool CustomerJpgManualConfirmation { get; set; }
        public string RecommendedCandidateReason { get; set; } = string.Empty;
        public string 软件名称 { get; set; } = string.Empty;
        public string 软件版本 { get; set; } = string.Empty;
    }
}
