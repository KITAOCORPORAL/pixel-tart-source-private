using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Core.Models;

public sealed class MediaFileRecord
{
    public string FullPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string BaseName { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string NumericId { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public FileCategory Category { get; set; }
    public string SourceRoot { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public bool IsCustomerProvided { get; set; }
    public JpegFileSourceType JpegSourceType { get; set; } = JpegFileSourceType.SourceDirectory;
    public SourceDirectoryType SourceDirectoryType { get; set; } = SourceDirectoryType.Mixed;
    public int SourcePriority { get; set; }
    public JpegQualityInfo? JpegQuality { get; set; }

    public static MediaFileRecord FromFile(
        string path,
        string sourceRoot,
        FileNameNormalizer normalizer,
        IEnumerable<string> customExtensions,
        bool isCustomerProvided = false)
    {
        var info = new FileInfo(path);
        var normalized = normalizer.Normalize(info.Name);
        var extension = MediaExtensionPolicy.NormalizeExtension(info.Extension);
        return new MediaFileRecord
        {
            FullPath = info.FullName,
            FileName = info.Name,
            BaseName = Path.GetFileNameWithoutExtension(info.Name),
            NormalizedName = normalized.ComparisonName,
            NumericId = normalized.NumericId,
            Extension = extension,
            Category = MediaExtensionPolicy.Classify(extension, customExtensions),
            SourceRoot = Path.GetFullPath(sourceRoot),
            Size = info.Exists ? info.Length : 0,
            LastWriteTimeUtc = info.Exists ? info.LastWriteTimeUtc : DateTime.UtcNow,
            IsCustomerProvided = isCustomerProvided,
            JpegSourceType = isCustomerProvided ? JpegFileSourceType.CustomerReturnedFile : JpegFileSourceType.SourceDirectory
        };
    }
}
