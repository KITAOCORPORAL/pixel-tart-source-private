using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class JpegQualityAssessmentService
{
    private static readonly string[] ProcessingMarkers =
    [
        "WECHAT", "微信", "WHATSAPP", "ADOBE", "PHOTOSHOP", "LIGHTROOM",
        "SNAPSEED", "美图", "MEITU", "INSTAGRAM", "FACEBOOK", "GIMP"
    ];

    public JpegQualityInfo Assess(JpegQualityInfo quality, string fileName)
    {
        var warnings = new HashSet<string>(quality.QualityWarnings, StringComparer.OrdinalIgnoreCase);
        if (quality.PixelWidth is null || quality.PixelHeight is null)
        {
            warnings.Add("像素尺寸未知");
        }
        else if (quality.TotalPixels < 2_000_000 || Math.Max(quality.PixelWidth.Value, quality.PixelHeight.Value) < 2000)
        {
            warnings.Add("像素尺寸明显偏小");
        }

        if (quality.FileSizeBytes <= 0)
        {
            warnings.Add("文件大小为零或无法读取");
        }
        else if (quality.TotalPixels is > 0 && quality.FileSizeBytes / (double)quality.TotalPixels.Value < 0.04)
        {
            warnings.Add("相对于像素尺寸，文件大小明显偏小");
        }

        if (quality.HasExif != true) warnings.Add("EXIF 信息缺失");
        if (string.IsNullOrWhiteSpace(quality.CameraModel)) warnings.Add("相机型号缺失");
        if (!quality.DateTimeOriginal.HasValue) warnings.Add("拍摄时间缺失");
        if (!string.IsNullOrWhiteSpace(quality.MetadataReadError)) warnings.Add(quality.MetadataReadError);
        if (fileName.Contains("COPY", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("副本", StringComparison.OrdinalIgnoreCase) ||
            System.Text.RegularExpressions.Regex.IsMatch(fileName, @"\(\d+\)(?=\.[^.]+$)"))
        {
            warnings.Add("文件名经过重命名");
        }

        var processingMarker = ProcessingMarkers.FirstOrDefault(marker =>
            quality.SoftwareTag.Contains(marker, StringComparison.OrdinalIgnoreCase));
        if (processingMarker is not null)
        {
            warnings.Add($"检测到聊天软件或编辑软件处理痕迹：{quality.SoftwareTag}");
            warnings.Add("图片可能经过二次编码");
        }
        else if (quality.HasExif == false && warnings.Contains("相对于像素尺寸，文件大小明显偏小"))
        {
            warnings.Add("图片可能经过二次编码");
        }

        warnings.Add("无法确认是否为原图");
        quality.QualityWarnings = warnings.ToList();
        return quality;
    }

    public void ApplyComparisonWarnings(MediaFileRecord source, MediaFileRecord customer)
    {
        if (customer.JpegQuality is null) return;
        var warnings = new HashSet<string>(customer.JpegQuality.QualityWarnings, StringComparer.OrdinalIgnoreCase);
        if (!string.Equals(source.FileName, customer.FileName, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("文件名经过重命名");
        }
        if ((source.JpegQuality?.TotalPixels ?? 0) > (customer.JpegQuality.TotalPixels ?? 0) && customer.JpegQuality.TotalPixels is > 0)
        {
            warnings.Add("与来源 JPG 相比像素尺寸明显减小");
        }
        customer.JpegQuality.QualityWarnings = warnings.ToList();
    }

    public IReadOnlyList<MediaFileRecord> RankCandidates(
        IEnumerable<MediaFileRecord> candidates,
        NormalizedFileName requested,
        MediaFileRecord? customerFile)
    {
        return candidates
            .OrderBy(file => file.JpegSourceType == JpegFileSourceType.SourceDirectory ? 0 : 1)
            .ThenBy(file => string.Equals(file.NormalizedName, requested.ComparisonName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(file => file.SourcePriority)
            .ThenByDescending(file => file.JpegQuality?.TotalPixels ?? 0)
            .ThenByDescending(file => file.JpegQuality?.ExifCompletenessScore ?? 0)
            .ThenByDescending(file => ProjectConsistencyScore(file.JpegQuality, customerFile?.JpegQuality))
            .ThenByDescending(file => file.Size)
            .ToList();
    }

    public string BuildRecommendedReason(
        MediaFileRecord recommended,
        MediaFileRecord? customerFile,
        NormalizedFileName requested)
    {
        var reasons = new List<string>();
        if (recommended.JpegSourceType == JpegFileSourceType.SourceDirectory) reasons.Add("来自用户指定的照片来源目录");
        if (string.Equals(recommended.NormalizedName, requested.ComparisonName, StringComparison.OrdinalIgnoreCase)) reasons.Add("完整标准化文件名匹配");
        reasons.Add($"来源目录优先级 {recommended.SourcePriority + 1}");

        if (customerFile is not null)
        {
            var sourceQuality = recommended.JpegQuality;
            var customerQuality = customerFile.JpegQuality;
            if ((sourceQuality?.TotalPixels ?? 0) > (customerQuality?.TotalPixels ?? 0)) reasons.Add("来源 JPG 像素尺寸更完整");
            if ((sourceQuality?.ExifCompletenessScore ?? 0) > (customerQuality?.ExifCompletenessScore ?? 0)) reasons.Add("来源 JPG 的 EXIF 信息更完整");
            if (SameNonEmpty(sourceQuality?.CameraModel, customerQuality?.CameraModel)) reasons.Add("相机型号一致");
            if (sourceQuality?.DateTimeOriginal is not null && sourceQuality.DateTimeOriginal == customerQuality?.DateTimeOriginal) reasons.Add("拍摄时间一致");
        }

        return string.Join("；", reasons.Distinct());
    }

    public string BuildComparison(MediaFileRecord source, MediaFileRecord customer)
    {
        var sourceQuality = source.JpegQuality ?? new JpegQualityInfo { FileSizeBytes = source.Size };
        var customerQuality = customer.JpegQuality ?? new JpegQualityInfo { FileSizeBytes = customer.Size };
        var sizeDifference = sourceQuality.FileSizeBytes - customerQuality.FileSizeBytes;
        var dimensions = $"来源 {sourceQuality.PixelDimensions}，客户 {customerQuality.PixelDimensions}";
        var size = $"文件大小差异 {sizeDifference:+#;-#;0} 字节";
        var exif = $"EXIF：来源 {sourceQuality.ExifStatusText}，客户 {customerQuality.ExifStatusText}";
        var camera = $"相机型号{(SameNonEmpty(sourceQuality.CameraModel, customerQuality.CameraModel) ? "一致" : "不一致或未知")}";
        var time = $"拍摄时间{(sourceQuality.DateTimeOriginal.HasValue && sourceQuality.DateTimeOriginal == customerQuality.DateTimeOriginal ? "一致" : "不一致或未知")}";
        var fileName = $"文件名{(string.Equals(source.FileName, customer.FileName, StringComparison.OrdinalIgnoreCase) ? "一致" : "不一致")}";
        var number = $"标准化编号{(string.Equals(source.NumericId, customer.NumericId, StringComparison.OrdinalIgnoreCase) ? "一致" : "不一致")}";
        return string.Join("；", dimensions, size, exif, camera, time, fileName, number);
    }

    private static int ProjectConsistencyScore(JpegQualityInfo? candidate, JpegQualityInfo? reference)
    {
        if (candidate is null || reference is null) return 0;
        var score = 0;
        if (SameNonEmpty(candidate.CameraModel, reference.CameraModel)) score++;
        if (candidate.DateTimeOriginal.HasValue && candidate.DateTimeOriginal == reference.DateTimeOriginal) score++;
        return score;
    }

    private static bool SameNonEmpty(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
