using System.Collections.ObjectModel;
using RAWSelectionAssistant.Core.Utilities;

using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Core.Models;

public sealed class MediaFormatMatchResult : ObservableObject
{
    private MatchStatus _status = MatchStatus.Waiting;
    private MediaFileRecord? _selectedFile;
    private IReadOnlyList<MediaFileRecord> _candidates = [];
    private bool _usedCustomerFile;
    private string _outputPath = string.Empty;
    private string _errorMessage = string.Empty;
    private DateTime? _operationTime;
    private bool _requiresManualConfirmation;
    private bool _customerJpgManualConfirmation;
    private MediaFileRecord? _recommendedFile;
    private string _recommendedCandidateReason = string.Empty;
    private string _jpegComparisonSummary = string.Empty;
    private JpegFileSourceType? _finalJpegSourceType;

    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required FileCategory Category { get; init; }
    public required IReadOnlyList<string> TargetExtensions { get; init; }
    public MatchStatus Status { get => _status; set => SetProperty(ref _status, value); }
    public MediaFileRecord? SelectedFile { get => _selectedFile; set => SetProperty(ref _selectedFile, value); }
    public IReadOnlyList<MediaFileRecord> Candidates { get => _candidates; set { if (SetProperty(ref _candidates, value)) OnPropertyChanged(nameof(CandidateCount)); } }
    public int CandidateCount => Candidates.Count;
    public bool UsedCustomerFile { get => _usedCustomerFile; set => SetProperty(ref _usedCustomerFile, value); }
    public string OutputPath { get => _outputPath; set => SetProperty(ref _outputPath, value); }
    public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
    public DateTime? OperationTime { get => _operationTime; set => SetProperty(ref _operationTime, value); }
    public bool RequiresManualConfirmation { get => _requiresManualConfirmation; set => SetProperty(ref _requiresManualConfirmation, value); }
    public bool CustomerJpgManualConfirmation { get => _customerJpgManualConfirmation; set => SetProperty(ref _customerJpgManualConfirmation, value); }
    public MediaFileRecord? RecommendedFile { get => _recommendedFile; set => SetProperty(ref _recommendedFile, value); }
    public string RecommendedCandidateReason { get => _recommendedCandidateReason; set => SetProperty(ref _recommendedCandidateReason, value); }
    public string JpegComparisonSummary { get => _jpegComparisonSummary; set => SetProperty(ref _jpegComparisonSummary, value); }
    public JpegFileSourceType? FinalJpegSourceType { get => _finalJpegSourceType ?? SelectedFile?.JpegSourceType; set => SetProperty(ref _finalJpegSourceType, value); }

    public void ConfirmSelection(MediaFileRecord file)
    {
        SelectedFile = file;
        Status = MatchStatus.ManuallyConfirmed;
        RequiresManualConfirmation = false;
        UsedCustomerFile = file.JpegSourceType == JpegFileSourceType.CustomerReturnedFile || file.IsCustomerProvided;
        CustomerJpgManualConfirmation = UsedCustomerFile;
        FinalJpegSourceType = JpegFileSourceType.ManuallySelectedFile;
    }
}

public sealed class MediaSelectionItem : ObservableObject
{
    private bool _isSelected = true;
    private string _normalizedName = string.Empty;
    private string _numericId = string.Empty;
    private MediaOverallStatus _overallStatus = MediaOverallStatus.Waiting;
    private string _note = string.Empty;
    private bool _isDuplicate;

    public Guid Id { get; init; } = Guid.NewGuid();
    public string OriginalInput { get; init; } = string.Empty;
    public string CustomerInputFilePath { get; init; } = string.Empty;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public string NormalizedName { get => _normalizedName; set => SetProperty(ref _normalizedName, value); }
    public string NumericId { get => _numericId; set => SetProperty(ref _numericId, value); }
    public MediaOverallStatus OverallStatus { get => _overallStatus; set => SetProperty(ref _overallStatus, value); }
    public string Note { get => _note; set => SetProperty(ref _note, value); }
    public bool IsDuplicate { get => _isDuplicate; set => SetProperty(ref _isDuplicate, value); }
    public ObservableCollection<MediaFormatMatchResult> FormatResults { get; } = [];
    public MediaFormatMatchResult? JpegResult => FormatResults.FirstOrDefault(x => x.Key == "JPG");
    public MediaFormatMatchResult? RawResult => FormatResults.FirstOrDefault(x => x.Key == "RAW");
    public int OtherFormatCount => FormatResults.Count(x => x.Category is not FileCategory.Jpeg and not FileCategory.Raw && x.SelectedFile is not null);
    public int MatchedFileCount => FormatResults.Count(x => x.SelectedFile is not null && x.Status is MatchStatus.Matched or MatchStatus.ManuallyConfirmed or MatchStatus.Copied or MatchStatus.Skipped);
    public int ConflictCount => FormatResults.Count(x => x.Status == MatchStatus.Conflict);
    public int CopiedFileCount => FormatResults.Count(x => x.Status == MatchStatus.Copied);
    public string JpegStatusText => JpegResult switch
    {
        null => "—",
        { Status: MatchStatus.Conflict } => "来源 JPG 存在冲突",
        { Status: MatchStatus.WaitingManualConfirmation } => "客户 JPG 等待确认",
        { Status: MatchStatus.NotFound, CandidateCount: > 0 } => "仅找到客户返回 JPG",
        { Status: MatchStatus.ManuallyConfirmed, CustomerJpgManualConfirmation: true } => "已手动采用客户 JPG",
        { UsedCustomerFile: true } => "使用客户返回 JPG",
        { SelectedFile.JpegSourceType: JpegFileSourceType.SourceDirectory } => "已找到来源 JPG",
        var result => result.Status.ToChinese()
    };
    public string RawStatusText => RawResult?.Status.ToChinese() ?? "—";
    public string JpegFileName => JpegResult?.SelectedFile?.FileName ?? string.Empty;
    public string RawFileName => RawResult?.SelectedFile?.FileName ?? string.Empty;

    public void ApplyMatch(MediaMatchDecision decision)
    {
        NormalizedName = decision.NormalizedName;
        NumericId = decision.NumericId;
        OverallStatus = decision.OverallStatus;
        Note = decision.Note;
        IsDuplicate = decision.IsDuplicate;
        IsSelected = !decision.IsDuplicate;
        FormatResults.Clear();
        foreach (var result in decision.FormatResults) FormatResults.Add(result);
        RaiseComputedProperties();
    }

    public void RaiseComputedProperties()
    {
        OnPropertyChanged(nameof(JpegResult));
        OnPropertyChanged(nameof(RawResult));
        OnPropertyChanged(nameof(OtherFormatCount));
        OnPropertyChanged(nameof(MatchedFileCount));
        OnPropertyChanged(nameof(ConflictCount));
        OnPropertyChanged(nameof(CopiedFileCount));
        OnPropertyChanged(nameof(JpegStatusText));
        OnPropertyChanged(nameof(RawStatusText));
        OnPropertyChanged(nameof(JpegFileName));
        OnPropertyChanged(nameof(RawFileName));
    }

    public void RefreshOverallStatus()
    {
        if (FormatResults.Count == 0 || FormatResults.All(x => x.Status == MatchStatus.NotFound))
        {
            OverallStatus = MediaOverallStatus.NotFound;
        }
        else if (FormatResults.Any(x => x.Status == MatchStatus.CopyFailed))
        {
            OverallStatus = MediaOverallStatus.CopyFailed;
        }
        else if (FormatResults.All(x => x.Status == MatchStatus.Copied))
        {
            OverallStatus = MediaOverallStatus.FullyCopied;
        }
        else if (FormatResults.Any(x => x.Status == MatchStatus.Copied))
        {
            OverallStatus = MediaOverallStatus.PartiallyCopied;
        }
        else if (FormatResults.Any(x => x.Status == MatchStatus.Conflict))
        {
            OverallStatus = MediaOverallStatus.Conflict;
        }
        else if (FormatResults.Any(x => x.Status == MatchStatus.WaitingManualConfirmation))
        {
            OverallStatus = MediaOverallStatus.WaitingConfirmation;
        }
        else if (FormatResults.All(x => x.Status is MatchStatus.Matched or MatchStatus.ManuallyConfirmed))
        {
            OverallStatus = MediaOverallStatus.CompleteMatched;
        }
        else
        {
            OverallStatus = MediaOverallStatus.PartialMatched;
        }

        RaiseComputedProperties();
    }
}

public sealed record MediaMatchDecision(
    Guid ItemId,
    string NormalizedName,
    string NumericId,
    MediaOverallStatus OverallStatus,
    IReadOnlyList<MediaFormatMatchResult> FormatResults,
    bool IsDuplicate,
    string Note)
{
    public MediaFormatMatchResult? JpegResult => FormatResults.FirstOrDefault(x => x.Key == "JPG");
    public MediaFormatMatchResult? RawResult => FormatResults.FirstOrDefault(x => x.Key == "RAW");
    public int MatchedFileCount => FormatResults.Count(x => x.SelectedFile is not null);
    public int ConflictCount => FormatResults.Count(x => x.Status == MatchStatus.Conflict);
}

public sealed record MediaMatchOptions(
    CollectionCategory Category,
    IReadOnlyList<string> JpegExtensions,
    IReadOnlyList<string> RawExtensions,
    IReadOnlyList<string> CustomExtensions,
    bool AllowCustomerJpegFallback)
{
    public CustomerJpegHandlingMode? CustomerJpegMode { get; init; }
    public CustomerJpegHandlingMode EffectiveCustomerJpegMode => CustomerJpegMode ??
        (AllowCustomerJpegFallback ? CustomerJpegHandlingMode.AllowCustomerFile : CustomerJpegHandlingMode.Strict);

    public static MediaMatchOptions Default(CollectionCategory category) => new(
        category,
        MediaExtensionPolicy.DefaultJpegExtensions,
        MediaExtensionPolicy.DefaultRawExtensions,
        [],
        false);
}
