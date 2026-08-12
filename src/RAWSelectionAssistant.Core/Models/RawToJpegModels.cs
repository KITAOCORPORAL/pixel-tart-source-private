namespace RAWSelectionAssistant.Core.Models;

public static class RawToJpegDefaults
{
    public const string TaskType = "RawToJpeg";
    public const int DefaultQuality = 90;
    public const int MinimumQuality = 40;
    public const int MaximumQuality = 100;
    public const int MaximumInputCount = 5000;

    public static IReadOnlySet<string> CandidateRawExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".ARW", ".CR2", ".CR3", ".NEF", ".NRW", ".RAF", ".DNG", ".RW2",
        ".ORF", ".ORI", ".PEF", ".3FR", ".FFF", ".IIQ", ".SRW", ".RWL"
    };
}

public sealed record RawToJpegOptions(
    int JpegQuality = RawToJpegDefaults.DefaultQuality,
    int? LongestEdge = null,
    bool UseCameraWhiteBalance = true,
    bool VerifySha256 = true,
    bool PreserveExif = true,
    bool AutoRotate = true)
{
    public RawToJpegOptions Validate()
    {
        if (JpegQuality is < RawToJpegDefaults.MinimumQuality or > RawToJpegDefaults.MaximumQuality)
            throw new ArgumentOutOfRangeException(nameof(JpegQuality));
        if (LongestEdge is < 320 or > 30000)
            throw new ArgumentOutOfRangeException(nameof(LongestEdge));
        return this;
    }
}

public sealed record RawImageMetadata(
    string? CameraMake,
    string? CameraModel,
    DateTimeOffset? CapturedAt,
    ushort Orientation,
    string ColorSpace);

public sealed record RawDecodedImage(
    int Width,
    int Height,
    int Stride,
    byte[] Rgb24Pixels,
    RawImageMetadata Metadata)
{
    public int RequiredByteCount => checked(Stride * Height);
}

public sealed record RawDecoderCapability(
    bool IsAvailable,
    string DecoderName,
    string? Version,
    IReadOnlyList<string> CandidateExtensions,
    IReadOnlyList<string> VerifiedExtensions,
    string? UnavailableReason = null);

public enum RawToJpegItemState
{
    Completed,
    NeedsAttention,
    Failed,
    Cancelled,
    PartiallyCompleted
}

public sealed record RawToJpegItemResult(
    int Sequence,
    RawToJpegItemState State,
    string SourcePath,
    string? DestinationPath,
    long BytesWritten,
    string? OutputHash,
    string? ErrorCode,
    string? ErrorMessage,
    MediaTaskFailureDetail? Failure = null);

public sealed record RawToJpegBatchRequest(
    IReadOnlyList<string> SourceFiles,
    string DestinationRoot,
    RawToJpegOptions Options,
    Guid? ProjectId = null,
    IReadOnlyList<int>? SourceSequences = null)
{
    public RawToJpegBatchRequest Validate()
    {
        if (SourceFiles.Count is 0 or > RawToJpegDefaults.MaximumInputCount)
            throw new ArgumentOutOfRangeException(nameof(SourceFiles));
        if (string.IsNullOrWhiteSpace(DestinationRoot) || !Path.IsPathFullyQualified(DestinationRoot))
            throw new ArgumentException("Destination must be an absolute path.", nameof(DestinationRoot));
        if (SourceSequences is not null &&
            (SourceSequences.Count != SourceFiles.Count || SourceSequences.Any(sequence => sequence < 0) ||
             SourceSequences.Distinct().Count() != SourceSequences.Count))
            throw new ArgumentException("Source sequence metadata is invalid.", nameof(SourceSequences));
        Options.Validate();
        return this;
    }
}

public sealed record RawToJpegBatchResult(
    Guid TaskId,
    TaskLifecycleState State,
    TaskResultSummary Summary,
    IReadOnlyList<RawToJpegItemResult> Items);

public sealed record RawToJpegRecoveryCheckpoint(
    RawToJpegBatchRequest OriginalRequest,
    IReadOnlyList<string> PendingSourceFiles,
    IReadOnlyList<RawToJpegItemResult> StableResults);
