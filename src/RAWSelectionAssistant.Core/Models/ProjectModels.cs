namespace RAWSelectionAssistant.Core.Models;

public enum PhotoProjectStatus
{
    Draft,
    Ready,
    Matching,
    Completed,
    Failed
}

public sealed class PhotoProjectRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public PhotoProjectStatus Status { get; set; } = PhotoProjectStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public CollectionCategory Category { get; set; } = CollectionCategory.JpegAndRaw;
    public OutputMode OutputMode { get; set; } = OutputMode.Flat;
    public string OutputBaseDirectory { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public List<string> SourceDirectories { get; set; } = [];
    public List<string> SelectionInputs { get; set; } = [];
    public List<string> CustomExtensions { get; set; } = [];
    public int SelectionCount { get; set; }
    public int MatchedFileCount { get; set; }
    public int CopiedFileCount { get; set; }
    public string Summary { get; set; } = string.Empty;
    public bool ExportReports { get; set; }
    public bool ExportCsvReport { get; set; } = true;
    public bool ExportJsonReport { get; set; }
    public bool ExportLogReport { get; set; }
}

public sealed record SelectionImportLimitResult(
    IReadOnlyList<ParsedSelectionInput> Accepted,
    IReadOnlyList<ParsedSelectionInput> Rejected,
    int UniqueSelectionCount,
    bool LimitReached,
    string Message);

public sealed record SourceDirectoryLimitResult(bool Allowed, int Maximum, string Message);

public sealed class OutputPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public OutputMode OutputMode { get; set; } = OutputMode.Flat;
    public string FolderNameTemplate { get; set; } = "{Project}_{Category}_{Date}";
}

public sealed record BatchProjectOutcome(Guid ProjectId, string ProjectName, bool Succeeded, string Message);

public sealed record BatchProjectSummary(
    bool Started,
    IReadOnlyList<BatchProjectOutcome> Outcomes,
    string Message)
{
    public int SucceededCount => Outcomes.Count(x => x.Succeeded);
    public int FailedCount => Outcomes.Count(x => !x.Succeeded);
}

public sealed record ReportExportOptions(bool IncludeCsv, bool IncludeJson, bool IncludeLog)
{
    public ReportExportOptions(bool includeAdvancedReports) : this(true, includeAdvancedReports, includeAdvancedReports)
    {
    }

    public bool IncludeAdvancedReports => IncludeJson || IncludeLog;
    public static ReportExportOptions Free { get; } = new(true, false, false);
    public static ReportExportOptions Pro { get; } = new(true, true, true);
}
