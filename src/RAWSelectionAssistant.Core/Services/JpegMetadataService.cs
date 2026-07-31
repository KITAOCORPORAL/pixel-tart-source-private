using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Icc;
using MetadataExtractor.Formats.Jpeg;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public interface IJpegMetadataService
{
    JpegQualityInfo Read(string filePath);
}

public sealed class JpegMetadataService(ILogService? logService = null) : IJpegMetadataService
{
    public JpegQualityInfo Read(string filePath)
    {
        var result = new JpegQualityInfo();
        try
        {
            var info = new FileInfo(filePath);
            result.FileSizeBytes = info.Exists ? info.Length : 0;
            if (!info.Exists)
            {
                result.MetadataReadError = "文件不存在或存储设备不可用。";
                return result;
            }
            if (info.Length == 0)
            {
                result.MetadataReadError = "文件大小为零，无法读取 JPG 元数据。";
                return result;
            }

            var directories = ImageMetadataReader.ReadMetadata(filePath);
            var jpeg = directories.OfType<JpegDirectory>().FirstOrDefault();
            if (jpeg is not null)
            {
                if (jpeg.TryGetInt32(JpegDirectory.TagImageWidth, out var width)) result.PixelWidth = width;
                if (jpeg.TryGetInt32(JpegDirectory.TagImageHeight, out var height)) result.PixelHeight = height;
            }

            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            result.HasExif = directories.Any(directory => directory is ExifDirectoryBase);
            result.CameraMake = ReadString(ifd0, ExifDirectoryBase.TagMake);
            result.CameraModel = ReadString(ifd0, ExifDirectoryBase.TagModel);
            result.SoftwareTag = ReadString(ifd0, ExifDirectoryBase.TagSoftware);
            if (subIfd?.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var originalTime) == true)
            {
                result.DateTimeOriginal = originalTime;
            }
            if (ifd0?.TryGetInt32(ExifDirectoryBase.TagOrientation, out var orientation) == true)
            {
                result.Orientation = OrientationText(orientation);
            }
            result.HasIccProfile = directories.Any(directory => directory is IccDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ImageProcessingException or ArgumentException or NotSupportedException)
        {
            result.MetadataReadError = FriendlyError(ex);
            logService?.Error($"无法读取 JPG 质量信息：{filePath}", ex);
        }
        return result;
    }

    private static string ReadString(MetadataExtractor.Directory? directory, int tagType)
    {
        try { return directory?.GetString(tagType)?.Trim() ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static string OrientationText(int value) => value switch
    {
        1 => "正常",
        2 => "水平翻转",
        3 => "旋转 180°",
        4 => "垂直翻转",
        5 => "转置",
        6 => "顺时针 90°",
        7 => "横向转置",
        8 => "逆时针 90°",
        _ => $"未知（{value}）"
    };

    private static string FriendlyError(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "文件被占用或没有读取权限。",
        IOException => "文件无法读取，可能被占用、损坏或存储设备不可用。",
        ImageProcessingException => "JPG 元数据损坏或文件不是有效的 JPEG。",
        _ => "无法读取 JPG 尺寸或元数据。"
    };
}
