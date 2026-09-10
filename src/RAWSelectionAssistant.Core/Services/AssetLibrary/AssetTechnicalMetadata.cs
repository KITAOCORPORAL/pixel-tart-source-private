using System.Globalization;
using System.Text.RegularExpressions;
using MetadataExtractor;
using MetadataExtractor.Formats.Bmp;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Gif;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Photoshop;
using MetadataExtractor.Formats.Png;
using MetadataExtractor.Formats.WebP;
using MetadataDirectory = MetadataExtractor.Directory;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

/// <summary>
/// Outcome of reading technical metadata. Missing optional EXIF is not a failure:
/// a valid image with dimensions and no EXIF is <see cref="Complete"/>.
/// </summary>
public enum AssetMetadataStatus
{
    NotApplicable,
    Complete,
    Partial,
    Failed
}

/// <summary>
/// The eight values defined by the EXIF orientation tag. A null value in
/// <see cref="AssetTechnicalMetadata.ExifOrientation"/> means that the file did
/// not provide a valid tag; <see cref="Normal"/> is used only for an explicit 1.
/// </summary>
public enum AssetExifOrientation
{
    Normal = 1,
    MirrorHorizontal = 2,
    Rotate180 = 3,
    MirrorVertical = 4,
    Transpose = 5,
    Rotate90Clockwise = 6,
    Transverse = 7,
    Rotate270Clockwise = 8
}

public enum AssetCaptureTimeSource
{
    Unknown,
    ExifOriginal,
    ExifDigitized,
    ExifIfd0
}

/// <summary>
/// An EXIF wall-clock timestamp and its independently known offset. EXIF files
/// frequently omit an offset; in that case <see cref="Offset"/> and
/// <see cref="Instant"/> remain null instead of assuming the computer time zone.
/// </summary>
public sealed record AssetCaptureTimestamp
{
    public AssetCaptureTimestamp(DateTime localTime, TimeSpan? offset, AssetCaptureTimeSource source)
    {
        if (source == AssetCaptureTimeSource.Unknown)
            throw new ArgumentOutOfRangeException(nameof(source), "A capture timestamp must identify its metadata source.");
        if (offset is { } value &&
            (value < TimeSpan.FromHours(-14) || value > TimeSpan.FromHours(14) ||
             (Math.Abs(value.TotalHours) == 14 && value.Minutes != 0)))
            throw new ArgumentOutOfRangeException(nameof(offset), "The UTC offset is outside the supported ISO-8601 range.");

        LocalTime = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        Offset = offset;
        Source = source;
    }

    public DateTime LocalTime { get; }
    public TimeSpan? Offset { get; }
    public AssetCaptureTimeSource Source { get; }
    public DateTimeOffset? Instant => Offset is { } value ? new DateTimeOffset(LocalTime, value) : null;
}

public sealed record AssetTechnicalMetadata(
    int? PixelWidth,
    int? PixelHeight,
    AssetExifOrientation? ExifOrientation,
    AssetCaptureTimestamp? CaptureTime,
    AssetMetadataStatus Status,
    IReadOnlyList<string> Warnings,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public bool HasPixelDimensions => PixelWidth is > 0 && PixelHeight is > 0;
    public AssetCaptureTimeSource CaptureTimeSource => CaptureTime?.Source ?? AssetCaptureTimeSource.Unknown;
    public TimeSpan? CaptureTimeOffset => CaptureTime?.Offset;
    public DateTimeOffset? CaptureInstant => CaptureTime?.Instant;
}

public interface IAssetMetadataExtractor
{
    Task<AssetTechnicalMetadata> ExtractAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only technical metadata extractor backed by MetadataExtractor. Container
/// header dimensions are preferred over EXIF dimensions so embedded thumbnails
/// and orientation never replace or rotate the stored physical pixel dimensions.
/// </summary>
public sealed partial class MetadataExtractorAssetMetadataExtractor : IAssetMetadataExtractor
{
    // EXIF 2.31 offset tags are not exposed as named constants by MetadataExtractor 2.9.0.
    private const int TagOffsetTime = 0x9010;
    private const int TagOffsetTimeOriginal = 0x9011;
    private const int TagOffsetTimeDigitized = 0x9012;
    private const int MaximumWarnings = 32;

    public Task<AssetTechnicalMetadata> ExtractAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        return Task.Run(() => Extract(fullPath, cancellationToken), cancellationToken);
    }

    private static AssetTechnicalMetadata Extract(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = new FileInfo(path);
        if (!info.Exists)
            return Failed(ErrorCodeCatalog.SourceNotFound, "源文件不存在或当前不可访问。");
        if (info.Length == 0)
            return Failed(ErrorCodeCatalog.MetadataReadFailed, "文件为空，无法读取图片基础元数据。");

        try
        {
            IReadOnlyList<MetadataDirectory> directories;
            using (var stream = new FileStream(
                       path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                       bufferSize: 64 * 1024, FileOptions.SequentialScan))
            {
                directories = ImageMetadataReader.ReadMetadata(stream);
            }
            cancellationToken.ThrowIfCancellationRequested();

            var warnings = ReadParserWarnings(directories);
            var (width, height) = ReadPhysicalDimensions(directories);
            var orientation = ReadOrientation(directories, warnings);
            var captureTime = ReadCaptureTime(directories, warnings);

            if ((width is null) != (height is null))
            {
                warnings.Add("图片只提供了一个像素尺寸字段，未将其视为完整尺寸。");
                width = null;
                height = null;
            }

            var hasDimensions = width is > 0 && height is > 0;
            var hasAnyMetadata = hasDimensions || orientation is not null || captureTime is not null;
            if (!hasAnyMetadata)
                return Failed(
                    ErrorCodeCatalog.MetadataReadFailed,
                    "文件中没有可确认的图片基础元数据。",
                    warnings.Count == 0 ? ["未找到可信的像素尺寸或 EXIF 技术字段。"] : warnings);

            if (!hasDimensions)
                warnings.Add("未能读取完整的原始像素尺寸；其他可用元数据仍予保留。");

            return new(
                width,
                height,
                orientation,
                captureTime,
                warnings.Count == 0 ? AssetMetadataStatus.Complete : AssetMetadataStatus.Partial,
                warnings.ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return Failed(ErrorCodeCatalog.PermissionDenied, "没有权限读取图片基础元数据。");
        }
        catch (FileNotFoundException)
        {
            return Failed(ErrorCodeCatalog.SourceNotFound, "源文件不存在或当前不可访问。");
        }
        catch (DirectoryNotFoundException)
        {
            return Failed(ErrorCodeCatalog.SourceNotFound, "源文件所在目录不存在或当前不可访问。");
        }
        catch (NotSupportedException)
        {
            return new(null, null, null, null, AssetMetadataStatus.NotApplicable, [],
                ErrorCodeCatalog.UnsupportedFormat, "当前文件格式没有可用的基础元数据读取器。");
        }
        catch (Exception exception) when (exception is IOException or ImageProcessingException or ArgumentException or OverflowException)
        {
            return Failed(ErrorCodeCatalog.MetadataReadFailed, "图片损坏或元数据块无法读取。");
        }
    }

    private static List<string> ReadParserWarnings(IReadOnlyList<MetadataDirectory> directories)
    {
        var warnings = new List<string>();
        foreach (var directory in directories.Where(directory => directory.HasError))
        {
            foreach (var error in directory.Errors.Where(error => !string.IsNullOrWhiteSpace(error)))
            {
                var warning = $"{directory.Name}: {error.Trim()}";
                if (!warnings.Contains(warning, StringComparer.Ordinal) && warnings.Count < MaximumWarnings)
                    warnings.Add(warning);
            }
        }
        return warnings;
    }

    private static (int? Width, int? Height) ReadPhysicalDimensions(IReadOnlyList<MetadataDirectory> directories)
    {
        if (TryReadPair(directories.OfType<JpegDirectory>(), JpegDirectory.TagImageWidth, JpegDirectory.TagImageHeight, out var pair) ||
            TryReadPair(directories.OfType<PngDirectory>(), PngDirectory.TagImageWidth, PngDirectory.TagImageHeight, out pair) ||
            TryReadPair(directories.OfType<WebPDirectory>(), WebPDirectory.TagImageWidth, WebPDirectory.TagImageHeight, out pair) ||
            TryReadPair(directories.OfType<BmpHeaderDirectory>(), BmpHeaderDirectory.TagImageWidth, BmpHeaderDirectory.TagImageHeight, out pair) ||
            TryReadPair(directories.OfType<GifHeaderDirectory>(), GifHeaderDirectory.TagImageWidth, GifHeaderDirectory.TagImageHeight, out pair) ||
            TryReadPair(directories.OfType<PsdHeaderDirectory>(), PsdHeaderDirectory.TagImageWidth, PsdHeaderDirectory.TagImageHeight, out pair) ||
            TryReadPair(directories.OfType<ExifSubIfdDirectory>(), ExifDirectoryBase.TagExifImageWidth, ExifDirectoryBase.TagExifImageHeight, out pair) ||
            TryReadPair(directories.OfType<ExifIfd0Directory>(), ExifDirectoryBase.TagImageWidth, ExifDirectoryBase.TagImageHeight, out pair))
            return pair;

        return (null, null);
    }

    private static bool TryReadPair<TDirectory>(
        IEnumerable<TDirectory> directories,
        int widthTag,
        int heightTag,
        out (int? Width, int? Height) pair)
        where TDirectory : MetadataDirectory
    {
        foreach (var directory in directories)
        {
            var width = ReadPositiveDimension(directory, widthTag);
            var height = ReadPositiveDimension(directory, heightTag);
            if (width is null && height is null) continue;
            pair = (width, height);
            return true;
        }

        pair = default;
        return false;
    }

    private static int? ReadPositiveDimension(MetadataDirectory directory, int tag)
    {
        if (!directory.ContainsTag(tag)) return null;
        try
        {
            var value = directory.GetObject(tag) switch
            {
                byte number => number,
                sbyte number => number,
                short number => number,
                ushort number => number,
                int number => number,
                uint number when number <= int.MaxValue => (int)number,
                long number when number is >= int.MinValue and <= int.MaxValue => (int)number,
                ulong number when number <= int.MaxValue => (int)number,
                _ => 0
            };
            if (value == int.MinValue) return null;
            value = Math.Abs(value);
            return value > 0 ? value : null;
        }
        catch (Exception exception) when (exception is MetadataException or InvalidCastException or OverflowException)
        {
            return null;
        }
    }

    private static AssetExifOrientation? ReadOrientation(
        IReadOnlyList<MetadataDirectory> directories,
        ICollection<string> warnings)
    {
        var candidates = directories.OfType<ExifIfd0Directory>().Cast<ExifDirectoryBase>()
            .Concat(directories.OfType<ExifSubIfdDirectory>());
        foreach (var directory in candidates)
        {
            if (!directory.ContainsTag(ExifDirectoryBase.TagOrientation)) continue;
            try
            {
                if (directory.TryGetInt32(ExifDirectoryBase.TagOrientation, out var value) && value is >= 1 and <= 8)
                    return (AssetExifOrientation)value;
                warnings.Add("EXIF Orientation 不在 1–8 的有效范围内，已保留为未知。");
                return null;
            }
            catch (Exception exception) when (exception is MetadataException or InvalidCastException)
            {
                warnings.Add("EXIF Orientation 无法解析，已保留为未知。");
                return null;
            }
        }
        return null;
    }

    private static AssetCaptureTimestamp? ReadCaptureTime(
        IReadOnlyList<MetadataDirectory> directories,
        ICollection<string> warnings)
    {
        var subIfds = directories.OfType<ExifSubIfdDirectory>().ToArray();
        foreach (var source in new[]
                 {
                     new CaptureCandidate(subIfds, ExifDirectoryBase.TagDateTimeOriginal, TagOffsetTimeOriginal, AssetCaptureTimeSource.ExifOriginal),
                     new CaptureCandidate(subIfds, ExifDirectoryBase.TagDateTimeDigitized, TagOffsetTimeDigitized, AssetCaptureTimeSource.ExifDigitized),
                     new CaptureCandidate(directories.OfType<ExifIfd0Directory>().ToArray(), ExifDirectoryBase.TagDateTime, TagOffsetTime, AssetCaptureTimeSource.ExifIfd0)
                 })
        {
            foreach (var directory in source.Directories)
            {
                if (!directory.ContainsTag(source.TimeTag)) continue;
                DateTime localTime;
                try
                {
                    if (!directory.TryGetDateTime(source.TimeTag, out localTime))
                    {
                        warnings.Add($"{CaptureSourceLabel(source.Source)} 无法解析，继续检查下一可信来源。");
                        break;
                    }
                }
                catch (Exception exception) when (exception is MetadataException or FormatException or InvalidCastException)
                {
                    warnings.Add($"{CaptureSourceLabel(source.Source)} 无法解析，继续检查下一可信来源。");
                    break;
                }

                var offset = ReadOffset(directory, directories, source.OffsetTag, warnings);
                return new AssetCaptureTimestamp(localTime, offset, source.Source);
            }
        }
        return null;
    }

    private static TimeSpan? ReadOffset(
        ExifDirectoryBase captureDirectory,
        IReadOnlyList<MetadataDirectory> directories,
        int sourceOffsetTag,
        ICollection<string> warnings)
    {
        var exifDirectories = new[] { captureDirectory }
            .Concat(directories.OfType<ExifSubIfdDirectory>())
            .Concat(directories.OfType<ExifIfd0Directory>())
            .Distinct();
        foreach (var tag in sourceOffsetTag == TagOffsetTime
                     ? new[] { TagOffsetTime }
                     : new[] { sourceOffsetTag, TagOffsetTime })
        {
            foreach (var directory in exifDirectories)
            {
                if (!directory.ContainsTag(tag)) continue;
                string? text;
                try { text = directory.GetString(tag)?.Trim().TrimEnd('\0'); }
                catch (Exception exception) when (exception is MetadataException or InvalidCastException)
                {
                    text = null;
                }

                if (TryParseOffset(text, out var offset)) return offset;
                warnings.Add("EXIF 拍摄时区偏移无法解析；拍摄时间仍以未知时区的本地时间保留。");
                return null;
            }
        }
        return null;
    }

    private static bool TryParseOffset(string? value, out TimeSpan offset)
    {
        offset = default;
        if (string.Equals(value, "Z", StringComparison.OrdinalIgnoreCase)) return true;
        var match = OffsetPattern().Match(value ?? string.Empty);
        if (!match.Success ||
            !int.TryParse(match.Groups["hours"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
            !int.TryParse(match.Groups["minutes"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
            minutes > 59 || hours > 14 || (hours == 14 && minutes != 0))
            return false;

        offset = new TimeSpan(hours, minutes, 0);
        if (match.Groups["sign"].Value == "-") offset = -offset;
        return true;
    }

    private static string CaptureSourceLabel(AssetCaptureTimeSource source) => source switch
    {
        AssetCaptureTimeSource.ExifOriginal => "EXIF DateTimeOriginal",
        AssetCaptureTimeSource.ExifDigitized => "EXIF DateTimeDigitized",
        AssetCaptureTimeSource.ExifIfd0 => "EXIF DateTime",
        _ => "EXIF 拍摄时间"
    };

    private static AssetTechnicalMetadata Failed(string errorCode, string message, IReadOnlyList<string>? warnings = null) =>
        new(null, null, null, null, AssetMetadataStatus.Failed, warnings ?? [], errorCode, message);

    [GeneratedRegex("^(?<sign>[+-])(?<hours>\\d{2}):(?<minutes>\\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex OffsetPattern();

    private sealed record CaptureCandidate(
        IReadOnlyList<ExifDirectoryBase> Directories,
        int TimeTag,
        int OffsetTag,
        AssetCaptureTimeSource Source);
}
