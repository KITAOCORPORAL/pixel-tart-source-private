namespace RAWSelectionAssistant.Core.Models;

public static class BatchCompressionDefaults
{
    public const string TaskType = "BatchCompression";
    public const int DefaultJpegQuality = 85;
    public const int MinimumJpegQuality = 40;
    public const int MaximumJpegQuality = 100;
    public const int DefaultLongestEdge = 2400;
    public const int MinimumLongestEdge = 320;
    public const int MaximumLongestEdge = 30000;
    public const int MaximumInputCount = 5000;

    public static IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".tif", ".tiff"
    };
}

public sealed record BatchCompressionOptions(
    int JpegQuality = BatchCompressionDefaults.DefaultJpegQuality,
    int LongestEdge = BatchCompressionDefaults.DefaultLongestEdge,
    bool PreserveMetadata = true,
    bool PreserveIccProfile = true)
{
    public BatchCompressionOptions Validate()
    {
        if (JpegQuality is < BatchCompressionDefaults.MinimumJpegQuality or > BatchCompressionDefaults.MaximumJpegQuality)
            throw new ArgumentOutOfRangeException(nameof(JpegQuality));
        if (LongestEdge is < BatchCompressionDefaults.MinimumLongestEdge or > BatchCompressionDefaults.MaximumLongestEdge)
            throw new ArgumentOutOfRangeException(nameof(LongestEdge));
        return this;
    }
}

public sealed record BatchCompressionRequest(
    IReadOnlyList<string> SourceFiles,
    string DestinationDirectory,
    BatchCompressionOptions Options,
    Guid? ProjectId = null,
    IReadOnlyList<int>? SourceSequences = null)
{
    public BatchCompressionRequest Validate()
    {
        if (SourceFiles.Count is 0 or > BatchCompressionDefaults.MaximumInputCount)
            throw new ArgumentOutOfRangeException(nameof(SourceFiles));
        if (string.IsNullOrWhiteSpace(DestinationDirectory) || !Path.IsPathFullyQualified(DestinationDirectory))
            throw new ArgumentException("Destination directory must be an absolute path.", nameof(DestinationDirectory));
        if (SourceFiles.Any(path => string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)))
            throw new ArgumentException("Source paths must be absolute.", nameof(SourceFiles));
        if (SourceSequences is not null &&
            (SourceSequences.Count != SourceFiles.Count || SourceSequences.Any(sequence => sequence < 0) ||
             SourceSequences.Distinct().Count() != SourceSequences.Count))
            throw new ArgumentException("Source sequence metadata is invalid.", nameof(SourceSequences));
        Options.Validate();
        return this;
    }
}

public enum BatchCompressionItemState
{
    Completed,
    NeedsAttention,
    Failed,
    Cancelled,
    PartiallyCompleted
}

public sealed record BatchCompressionItemResult(
    int Sequence,
    BatchCompressionItemState State,
    string SourcePath,
    string? DestinationPath,
    long BytesWritten,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record BatchCompressionResult(
    Guid TaskId,
    TaskLifecycleState State,
    TaskResultSummary Summary,
    IReadOnlyList<BatchCompressionItemResult> Items);

public sealed record BatchCompressionRecoveryCheckpoint(
    BatchCompressionRequest OriginalRequest,
    IReadOnlyList<string> PendingSourceFiles,
    IReadOnlyList<BatchCompressionItemResult> StableResults);
