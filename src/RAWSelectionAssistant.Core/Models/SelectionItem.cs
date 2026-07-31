using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Models;

public sealed class SelectionItem : ObservableObject
{
    private bool _isSelected = true;
    private string _normalizedName = string.Empty;
    private string _numericId = string.Empty;
    private MatchStatus _status = MatchStatus.Waiting;
    private RawFileEntry? _selectedRaw;
    private IReadOnlyList<RawFileEntry> _candidates = [];
    private string _note = string.Empty;
    private bool _isDuplicate;
    private string _rawOutputPath = string.Empty;
    private string _errorMessage = string.Empty;
    private DateTime? _operationTime;

    public Guid Id { get; init; } = Guid.NewGuid();
    public required string OriginalInput { get; init; }

    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public string NormalizedName { get => _normalizedName; set => SetProperty(ref _normalizedName, value); }
    public string NumericId { get => _numericId; set => SetProperty(ref _numericId, value); }
    public MatchStatus Status { get => _status; set => SetProperty(ref _status, value); }
    public RawFileEntry? SelectedRaw { get => _selectedRaw; set => SetProperty(ref _selectedRaw, value); }
    public IReadOnlyList<RawFileEntry> Candidates { get => _candidates; set { if (SetProperty(ref _candidates, value)) OnPropertyChanged(nameof(CandidateCount)); } }
    public int CandidateCount => Candidates.Count;
    public string Note { get => _note; set => SetProperty(ref _note, value); }
    public bool IsDuplicate { get => _isDuplicate; set => SetProperty(ref _isDuplicate, value); }
    public string RawOutputPath { get => _rawOutputPath; set => SetProperty(ref _rawOutputPath, value); }
    public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
    public DateTime? OperationTime { get => _operationTime; set => SetProperty(ref _operationTime, value); }
    public bool IsManualConfirmation => Status == MatchStatus.ManuallyConfirmed;
    public bool CopySucceeded => Status == MatchStatus.Copied;
}
