namespace RAWSelectionAssistant.Core.Models;

public enum TutorialAction
{
    BeginTutorial,
    AddSourceDirectory,
    RemoveSourceDirectory,
    SelectCollectionCategories,
    ScanSourceFiles,
    CancelSimulatedTask,
    LoadCustomerSelection,
    PasteNumbers,
    ParseNumbers,
    ClearSelections,
    MatchFiles,
    ViewDetails,
    AcknowledgeJpegQuality,
    SelectOutputDirectory,
    EnterProjectName,
    SelectOutputModes,
    CopyMatchedFiles,
    ExportReports,
    OpenOutputDirectory,
    ClearCurrentTask,
    AcknowledgeEditions,
    FinishTutorial
}

public enum TutorialTarget
{
    Welcome,
    AddSourceButton,
    RemoveSourceButton,
    CollectionCategorySelector,
    ScanButton,
    CancelButton,
    CustomerDropArea,
    PasteButton,
    ParseButton,
    ClearSelectionsButton,
    MatchButton,
    ResultsGrid,
    FirstDetailsButton,
    JpegQualityArea,
    BrowseOutputButton,
    ProjectNameInput,
    OutputModeSelector,
    CopyButton,
    ExportButton,
    OpenOutputButton,
    ClearTaskButton,
    EditionStatusArea,
    Completed
}

public enum TutorialMode
{
    Inactive,
    Required,
    Replay
}

public sealed record TutorialStep(
    int Number,
    string Title,
    string Instruction,
    TutorialAction RequiredAction,
    TutorialTarget Target,
    bool AllowBack,
    bool IsDemonstration,
    string ErrorMessage,
    string CompletionCondition,
    int? NextStep);

public sealed class TutorialState
{
    public TutorialMode Mode { get; internal set; }
    public int CurrentStep { get; internal set; } = 1;
    public string ErrorMessage { get; set; } = string.Empty;
    public HashSet<CollectionCategory> VisitedCategories { get; } = [];
    public HashSet<OutputMode> VisitedOutputModes { get; } = [];
    public bool IsActive => Mode != TutorialMode.Inactive;
    public bool IsRequired => Mode == TutorialMode.Required;
}

public sealed record TutorialValidationResult(bool Succeeded, string Message = "")
{
    public static TutorialValidationResult Success() => new(true);
    public static TutorialValidationResult Failure(string message) => new(false, message);
}

public sealed record TutorialActionContext(
    int SourceDirectoryCount = 0,
    int IndexedJpegCount = 0,
    int IndexedRawCount = 0,
    int SelectionCount = 0,
    int CompleteMatchCount = 0,
    int CopiedJpegCount = 0,
    int CopiedRawCount = 0,
    bool DetailsViewed = false,
    bool ReportsExist = false,
    bool OutputOpened = false,
    bool OutputPreserved = false,
    string ProjectName = "",
    string OutputDirectory = "",
    CollectionCategory? CollectionCategory = null,
    OutputMode? OutputMode = null);

public sealed record TutorialSandboxPaths(
    string Root,
    string SourceRoot,
    string JpegSource,
    string RawSource,
    string CustomerSelection,
    string Output,
    string CustomerJpeg,
    string SelectionText);

public sealed record TutorialSpotlightLayout(
    double TargetLeft,
    double TargetTop,
    double TargetWidth,
    double TargetHeight,
    double CardLeft,
    double CardTop);
