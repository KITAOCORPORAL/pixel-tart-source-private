namespace RAWSelectionAssistant.Core.Models;

public enum OrganizeRuleType
{
    OriginalFolder,
    CaptureDate,
    CaptureYear,
    CaptureYearMonth,
    CaptureDateHour,
    CameraMake,
    CameraModel,
    LensModel,
    FileFormat,
    Landscape,
    Portrait,
    Square,
    FileNamePrefix,
    FileNameNumber,
    FileSizeRange,
    FixedCount,
    CustomKeyword,
    Manual
}

public enum OrganizeOperationType { SavePlan, Copy, Move }
public enum OrganizeConflictPolicy { AutoNumber, Skip, AddSourceFolder, AddCaptureDate, AddShortHash, Overwrite }
public enum OrganizeItemState { Pending, Copied, Moved, Skipped, Cancelled, Failed, Undone }

public sealed record OrganizeRule(OrganizeRuleType Type, string Parameter = "", int FixedCount = 100);

public sealed class OrganizePhotoItem
{
    public string SourcePath { get; init; } = string.Empty;
    public string SourceRoot { get; init; } = string.Empty;
    public string FileName => Path.GetFileName(SourcePath);
    public string Extension => Path.GetExtension(SourcePath).TrimStart('.').ToUpperInvariant();
    public long FileSizeBytes { get; init; }
    public DateTimeOffset ModifiedAt { get; init; }
    public DateTimeOffset? CaptureTime { get; init; }
    public string CameraMake { get; init; } = string.Empty;
    public string CameraModel { get; init; } = string.Empty;
    public string LensModel { get; init; } = string.Empty;
    public int PixelWidth { get; init; }
    public int PixelHeight { get; init; }
    public bool MetadataMissing { get; init; }
    public bool Excluded { get; set; }
    public string GroupName { get; set; } = "未分组";
}

public sealed class PhotoGroupDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "未命名分组";
    public string? CoverSourcePath { get; set; }
    public List<string> SourcePaths { get; init; } = [];
    public int Count => SourcePaths.Count;
}

public sealed class OrganizePlan
{
    public const string CurrentSchemaVersion = "1.0";
    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public Guid OperationId { get; init; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public List<string> SourceRoots { get; init; } = [];
    public string OutputRoot { get; init; } = string.Empty;
    public OrganizeRule Rule { get; init; } = new(OrganizeRuleType.OriginalFolder);
    public OrganizeOperationType OperationType { get; init; } = OrganizeOperationType.Copy;
    public OrganizeConflictPolicy ConflictPolicy { get; init; } = OrganizeConflictPolicy.AutoNumber;
    public bool VerifySha256 { get; init; }
    public List<PhotoGroupDefinition> Groups { get; init; } = [];
    public List<OrganizeManifestItem> Items { get; init; } = [];
    public long EstimatedOutputBytes => Items.Where(x => x.State == OrganizeItemState.Pending).Sum(x => x.ExpectedSourceSize);
    public int MetadataMissingCount { get; init; }
    public int ConflictRiskCount { get; init; }
}

public sealed class OrganizeManifest
{
    public const string CurrentSchemaVersion = "1.0";
    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public Guid OperationId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public OrganizeOperationType OperationType { get; init; }
    public OrganizeConflictPolicy ConflictPolicy { get; init; }
    public List<OrganizeManifestItem> Items { get; init; } = [];
}

public sealed class OrganizeManifestItem
{
    public string SchemaVersion { get; init; } = OrganizeManifest.CurrentSchemaVersion;
    public Guid OperationId { get; init; }
    public string SourcePath { get; init; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public OrganizeOperationType OperationType { get; init; }
    public OrganizeConflictPolicy ConflictPolicy { get; init; }
    public long ExpectedSourceSize { get; init; }
    public DateTimeOffset ExpectedSourceModifiedAt { get; init; }
    public string? OptionalSourceHash { get; set; }
    public OrganizeItemState State { get; set; } = OrganizeItemState.Pending;
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public sealed record OrganizeExecutionProgress(int Completed, int Total, string CurrentFile);

public sealed class OrganizeExecutionResult
{
    public required OrganizeManifest Manifest { get; init; }
    public int Succeeded => Manifest.Items.Count(x => x.State is OrganizeItemState.Copied or OrganizeItemState.Moved);
    public int Failed => Manifest.Items.Count(x => x.State == OrganizeItemState.Failed);
    public int Skipped => Manifest.Items.Count(x => x.State == OrganizeItemState.Skipped);
    public bool Cancelled => Manifest.Items.Any(x => x.State == OrganizeItemState.Cancelled);
}
