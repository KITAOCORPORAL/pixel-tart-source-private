using System.Collections.Concurrent;
using MetadataExtractor;

namespace RAWSelectionAssistant.Core.Services.Photos;

public sealed record PhotoMetadata(string Path, string FileName, string FileType, string Dimensions, string FileSize,
    string CaptureTime, string ImportedTime, string Make, string Model, string LensModel, string FocalLength,
    string Aperture, string Shutter, string Iso, string ExposureProgram, string ExposureCompensation,
    string MaximumAperture, string MeteringMode, string WhiteBalance, string ColorSpace, string Flash, string Orientation)
{
    public static PhotoMetadata Unavailable(string path) => new(path, System.IO.Path.GetFileName(path), Value(System.IO.Path.GetExtension(path).TrimStart('.').ToUpperInvariant()), "—", "—", "—", "—", "—", "—", "—", "—", "—", "—", "—", "—", "—", "—", "—", "—", "—", "—", "—");
    private static string Value(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
}
public interface IPhotoMetadataProvider { Task<PhotoMetadata> ReadAsync(string path, CancellationToken cancellationToken = default); }

/// <summary>Reads metadata headers only and caches by physical file identity. It never decodes full image pixels.</summary>
public sealed class PhotoMetadataProvider : IPhotoMetadataProvider
{
    private readonly ConcurrentDictionary<CacheKey, Lazy<Task<PhotoMetadata>>> _cache = new();
    public Task<PhotoMetadata> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var full = System.IO.Path.GetFullPath(path); var info = new FileInfo(full);
        var key = new CacheKey(full, info.Exists ? info.Length : -1, info.Exists ? info.LastWriteTimeUtc.Ticks : 0);
        var shared = _cache.GetOrAdd(key, item => new(() => Task.Run(() => Read(item.Path), CancellationToken.None))).Value;
        return shared.WaitAsync(cancellationToken);
    }
    private static PhotoMetadata Read(string path)
    {
        var fallback = PhotoMetadata.Unavailable(path); if (!File.Exists(path)) return fallback;
        try
        {
            var info = new FileInfo(path); using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan);
            var directories = ImageMetadataReader.ReadMetadata(stream);
            string Tag(params string[] names) => directories.SelectMany(d => d.Tags).FirstOrDefault(t => names.Any(n => string.Equals(t.Name, n, StringComparison.OrdinalIgnoreCase)))?.Description?.Trim() is { Length: > 0 } value ? value : "—";
            string Dimensions() { var width = Tag("Image Width", "Exif Image Width"); var height = Tag("Image Height", "Exif Image Height"); return width == "—" || height == "—" ? "—" : $"{width} × {height}"; }
            static string Size(long size) => size >= 1024L * 1024L * 1024L ? $"{size / 1024d / 1024d / 1024d:F1} GB" : size >= 1024L * 1024L ? $"{size / 1024d / 1024d:F1} MB" : $"{size / 1024d:F0} KB";
            // File creation time is not import time. The Asset database owns that value;
            // this provider intentionally reports an unknown import time for external files.
            return new(path, info.Name, info.Extension.TrimStart('.').ToUpperInvariant(), Dimensions(), Size(info.Length), Tag("Date/Time Original", "Date/Time Digitized"), "—", Tag("Make"), Tag("Model"), Tag("Lens Model", "Lens"), Tag("Focal Length"), Tag("F-Number", "Aperture Value"), Tag("Exposure Time", "Shutter Speed Value"), Tag("ISO Speed Ratings", "ISO Speed"), Tag("Exposure Program"), Tag("Exposure Bias Value"), Tag("Max Aperture Value"), Tag("Metering Mode"), Tag("White Balance Mode", "White Balance"), Tag("Color Space"), Tag("Flash"), Tag("Orientation"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ImageProcessingException or NotSupportedException) { return fallback; }
    }
    private readonly record struct CacheKey(string Path, long Size, long ModifiedTicks);
}
