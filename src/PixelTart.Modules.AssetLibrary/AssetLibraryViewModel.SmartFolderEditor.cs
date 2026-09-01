using System.Globalization;
using RAWSelectionAssistant.Core.Models;

namespace PixelTart.Modules.AssetLibrary;

/// <summary>
/// State and translation helpers for the small, auditable Smart Folder editor.
/// The editor intentionally maps one control to one repository rule and keeps
/// unknown rules when an existing folder is edited.
/// </summary>
public sealed partial class AssetLibraryViewModel
{
    private const string SmartMissingAny = "Any";
    private const string SmartMissingOnly = "Missing";
    private const string SmartPresentOnly = "Present";

    private Guid? _smartFolderEditorId;
    private SmartFolder? _smartFolderEditorSnapshot;
    private IReadOnlyList<SmartFolderRule> _smartFolderEditorRules = [];
    private CancellationTokenSource? _smartFolderEditorCancellation;
    private long _smartFolderEditorGeneration;
    private bool _isSmartFolderEditorLoading;

    private string _smartFileNameValue = string.Empty;
    private string _smartExtensionValue = string.Empty;
    private string _smartMediaTypeValue = string.Empty;
    private string _smartFolderValue = string.Empty;
    private string _smartMissingValue = SmartMissingAny;
    private string _smartAddedAtFrom = string.Empty;
    private string _smartAddedAtTo = string.Empty;

    private SmartFolderOperator _smartFileNameOperator = SmartFolderOperator.Contains;
    private SmartFolderOperator _smartExtensionOperator = SmartFolderOperator.Equals;
    private SmartFolderOperator _smartMediaTypeOperator = SmartFolderOperator.Equals;
    private SmartFolderOperator _smartFolderOperator = SmartFolderOperator.Equals;
    private SmartFolderOperator _smartTagOperator = SmartFolderOperator.Equals;
    private SmartFolderOperator _smartRatingOperator = SmartFolderOperator.GreaterThanOrEqual;
    private SmartFolderOperator _smartAddedAtFromOperator = SmartFolderOperator.GreaterThanOrEqual;
    private SmartFolderOperator _smartAddedAtToOperator = SmartFolderOperator.LessThanOrEqual;
    private SmartFolderOperator _smartMissingOperator = SmartFolderOperator.IsTrue;
    private SmartFolderOperator _smartToneOperator = SmartFolderOperator.Equals;
    private SmartFolderOperator _smartAnalysisStatusOperator = SmartFolderOperator.Equals;
    private SmartFolderOperator _smartAverageSaturationOperator = SmartFolderOperator.LessThanOrEqual;
    private SmartFolderOperator _smartDominantHueOperator = SmartFolderOperator.InRange;
    private SmartFolderOperator _smartDominantColorOperator = SmartFolderOperator.Equals;

    public bool IsSmartFolderEditorLoading
    {
        get => _isSmartFolderEditorLoading;
        private set
        {
            if (!SetProperty(ref _isSmartFolderEditorLoading, value)) return;
            SaveSmartFolderCommand?.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(SmartFolderEditorStatus));
        }
    }

    public bool IsSmartFolderEditing => _smartFolderEditorId is not null;
    public string SmartFolderEditorStatus => IsSmartFolderEditorLoading
        ? "正在载入智能文件夹条件…"
        : IsSmartFolderEditing ? "正在编辑已保存的智能文件夹" : "新建智能文件夹";

    public string SmartFileNameValue { get => _smartFileNameValue; set => SetSmartEditorValue(ref _smartFileNameValue, value); }
    public string SmartExtensionValue { get => _smartExtensionValue; set => SetSmartEditorValue(ref _smartExtensionValue, value); }
    public string SmartMediaTypeValue { get => _smartMediaTypeValue; set => SetSmartEditorValue(ref _smartMediaTypeValue, value); }
    public string SmartFolderValue { get => _smartFolderValue; set => SetSmartEditorValue(ref _smartFolderValue, value); }
    public string SmartMissingValue { get => _smartMissingValue; set => SetSmartEditorValue(ref _smartMissingValue, value); }
    public string SmartAddedAtFrom { get => _smartAddedAtFrom; set => SetSmartEditorValue(ref _smartAddedAtFrom, value); }
    public string SmartAddedAtTo { get => _smartAddedAtTo; set => SetSmartEditorValue(ref _smartAddedAtTo, value); }

    public IReadOnlyList<string> SmartMissingOptions { get; } = [SmartMissingAny, SmartMissingOnly, SmartPresentOnly];

    /// <summary>Human-readable summary of the metadata fields represented by the editor.</summary>
    public string SmartBasicBuilderExplanation => string.Join(" AND ", new[]
    {
        string.IsNullOrWhiteSpace(SmartFileNameValue) ? null : $"文件名{OperatorLabel(_smartFileNameOperator)}{SmartFileNameValue.Trim()}",
        string.IsNullOrWhiteSpace(SmartExtensionValue) ? null : $"扩展名{OperatorLabel(_smartExtensionOperator)}{SmartExtensionValue.Trim()}",
        string.IsNullOrWhiteSpace(SmartMediaTypeValue) ? null : $"媒体类型{OperatorLabel(_smartMediaTypeOperator)}{SmartMediaTypeValue.Trim()}",
        string.IsNullOrWhiteSpace(SmartFolderValue) ? null : $"文件夹{OperatorLabel(_smartFolderOperator)}{SmartFolderValue.Trim()}",
        string.IsNullOrWhiteSpace(SmartMissingValue) || SmartMissingValue == SmartMissingAny ? null : $"文件缺失={SmartMissingValue}",
        string.IsNullOrWhiteSpace(SmartAddedAtFrom) ? null : $"添加时间≥{SmartAddedAtFrom.Trim()}",
        string.IsNullOrWhiteSpace(SmartAddedAtTo) ? null : $"添加时间≤{SmartAddedAtTo.Trim()}"
    }.Where(value => value is not null)!);

    /// <summary>
    /// Starts loading a selected Smart Folder's persisted rules. This method is
    /// called by the base SelectedSmartFolder setter so the existing P2 tree
    /// editor path does not need to know about repository details.
    /// </summary>
    private void BeginSmartFolderEditorLoad(SmartFolder? folder)
    {
        // Let the in-flight load own and dispose its CTS in its finally block.
        // Disposing it here races the repository await when a user switches
        // folders quickly and can turn a normal cancellation into an error.
        var previousCancellation = Interlocked.Exchange(ref _smartFolderEditorCancellation, null);
        previousCancellation?.Cancel();
        Interlocked.Increment(ref _smartFolderEditorGeneration);
        _smartFolderEditorId = folder?.SmartFolderId;
        _smartFolderEditorSnapshot = folder;
        OnPropertyChanged(nameof(IsSmartFolderEditing));
        OnPropertyChanged(nameof(SmartFolderEditorStatus));

        if (folder is null)
        {
            _smartFolderEditorRules = [];
            ResetSmartFolderEditorDefaults();
            IsSmartFolderEditorLoading = false;
            return;
        }

        SmartFolderName = folder.Name;
        ResetSmartFolderEditorForExisting();
        var generation = Volatile.Read(ref _smartFolderEditorGeneration);
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        _smartFolderEditorCancellation = cancellation;
        IsSmartFolderEditorLoading = true;
        _ = LoadSmartFolderEditorAsync(folder.SmartFolderId, generation, cancellation);
    }

    private async Task LoadSmartFolderEditorAsync(Guid folderId, long generation, CancellationTokenSource cancellation)
    {
        try
        {
            var rules = await _repository.ListSmartFolderRulesAsync(folderId, cancellation.Token);
            if (!IsCurrentSmartFolderEditorLoad(folderId, generation)) return;
            _smartFolderEditorRules = rules;
            ApplyPersistedSmartFolderRules(rules);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception) when (IsCurrentSmartFolderEditorLoad(folderId, generation))
        {
            Status = $"智能文件夹条件载入失败：{exception.Message}";
        }
        finally
        {
            if (IsCurrentSmartFolderEditorLoad(folderId, generation)) IsSmartFolderEditorLoading = false;
            if (ReferenceEquals(_smartFolderEditorCancellation, cancellation)) _smartFolderEditorCancellation = null;
            cancellation.Dispose();
        }
    }

    private bool IsCurrentSmartFolderEditorLoad(Guid folderId, long generation) =>
        Volatile.Read(ref _disposeStarted) == 0 && generation == Volatile.Read(ref _smartFolderEditorGeneration) && _smartFolderEditorId == folderId;

    private void ApplyPersistedSmartFolderRules(IReadOnlyList<SmartFolderRule> rules)
    {
        foreach (var rule in rules.OrderBy(item => item.SortOrder).ThenBy(item => item.RuleId))
        {
            switch (rule.Field)
            {
                case SmartFolderField.FileName:
                    SmartFileNameValue = rule.Value; _smartFileNameOperator = rule.Operator; break;
                case SmartFolderField.Extension:
                    SmartExtensionValue = rule.Value; _smartExtensionOperator = rule.Operator; break;
                case SmartFolderField.MediaType:
                    SmartMediaTypeValue = rule.Value; _smartMediaTypeOperator = rule.Operator; break;
                case SmartFolderField.Folder:
                    SmartFolderValue = rule.Value; _smartFolderOperator = rule.Operator; break;
                case SmartFolderField.Tag:
                    SmartTagValue = rule.Value; _smartTagOperator = rule.Operator; break;
                case SmartFolderField.Rating:
                    SmartRuleValue = rule.Value; _smartRatingOperator = rule.Operator; break;
                case SmartFolderField.AddedAt when rule.Operator is SmartFolderOperator.GreaterThan or SmartFolderOperator.GreaterThanOrEqual:
                    SmartAddedAtFrom = rule.Value; _smartAddedAtFromOperator = rule.Operator; break;
                case SmartFolderField.AddedAt:
                    SmartAddedAtTo = rule.Value; _smartAddedAtToOperator = rule.Operator; break;
                case SmartFolderField.IsMissing:
                    SmartMissingValue = rule.Operator is SmartFolderOperator.IsFalse or SmartFolderOperator.NotEquals ? SmartPresentOnly : SmartMissingOnly;
                    _smartMissingOperator = rule.Operator; break;
                case SmartFolderField.VisualToneKey:
                    SmartToneKey = rule.Value; _smartToneOperator = rule.Operator; break;
                case SmartFolderField.VisualAnalysisStatus:
                    SmartAnalysisStatus = rule.Value; _smartAnalysisStatusOperator = rule.Operator; break;
                case SmartFolderField.VisualAverageSaturation:
                    SmartAverageSaturationMaximum = rule.Value; _smartAverageSaturationOperator = rule.Operator; break;
                case SmartFolderField.VisualDominantHue:
                    SmartDominantHueRange = rule.Value; _smartDominantHueOperator = rule.Operator; break;
                case SmartFolderField.VisualDominantColor:
                    SmartDominantColor = rule.Value; _smartDominantColorOperator = rule.Operator; break;
            }
        }
        NotifySmartBuilderChanged();
    }

    private void ResetSmartFolderEditorForExisting()
    {
        SmartFileNameValue = string.Empty;
        SmartExtensionValue = string.Empty;
        SmartMediaTypeValue = string.Empty;
        SmartFolderValue = string.Empty;
        SmartMissingValue = SmartMissingAny;
        SmartAddedAtFrom = string.Empty;
        SmartAddedAtTo = string.Empty;
        SmartTagValue = string.Empty;
        SmartRuleValue = string.Empty;
        SmartToneKey = string.Empty;
        SmartAnalysisStatus = string.Empty;
        SmartAverageSaturationMaximum = string.Empty;
        SmartDominantHueRange = string.Empty;
        SmartDominantColor = string.Empty;
        _smartFileNameOperator = SmartFolderOperator.Contains;
        _smartExtensionOperator = SmartFolderOperator.Equals;
        _smartMediaTypeOperator = SmartFolderOperator.Equals;
        _smartFolderOperator = SmartFolderOperator.Equals;
        _smartTagOperator = SmartFolderOperator.Equals;
        _smartRatingOperator = SmartFolderOperator.GreaterThanOrEqual;
        _smartAddedAtFromOperator = SmartFolderOperator.GreaterThanOrEqual;
        _smartAddedAtToOperator = SmartFolderOperator.LessThanOrEqual;
        _smartMissingOperator = SmartFolderOperator.IsTrue;
        _smartToneOperator = SmartFolderOperator.Equals;
        _smartAnalysisStatusOperator = SmartFolderOperator.Equals;
        _smartAverageSaturationOperator = SmartFolderOperator.LessThanOrEqual;
        _smartDominantHueOperator = SmartFolderOperator.InRange;
        _smartDominantColorOperator = SmartFolderOperator.Equals;
        NotifySmartBuilderChanged();
    }

    private void ResetSmartFolderEditorDefaults()
    {
        SmartFileNameValue = string.Empty;
        SmartExtensionValue = string.Empty;
        SmartMediaTypeValue = string.Empty;
        SmartFolderValue = string.Empty;
        SmartMissingValue = SmartMissingAny;
        SmartAddedAtFrom = string.Empty;
        SmartAddedAtTo = string.Empty;
        SmartTagValue = string.Empty;
        SmartRuleValue = string.Empty;
        SmartToneKey = string.Empty;
        SmartAnalysisStatus = string.Empty;
        SmartAverageSaturationMaximum = string.Empty;
        SmartDominantHueRange = string.Empty;
        SmartDominantColor = string.Empty;
        NotifySmartBuilderChanged();
    }

    private void SetSmartEditorValue(ref string field, string? value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref field, value ?? string.Empty, propertyName)) return;
        OnPropertyChanged(nameof(SmartBuilderExplanation));
        OnPropertyChanged(nameof(SmartBasicBuilderExplanation));
    }

    private void NotifySmartBuilderChanged()
    {
        OnPropertyChanged(nameof(SmartBuilderExplanation));
        OnPropertyChanged(nameof(SmartBasicBuilderExplanation));
    }

    private bool TryBuildSmartFolderEditorSave(
        out SmartFolder folder,
        out IReadOnlyList<SmartFolderRule> rules,
        out string error)
    {
        error = string.Empty;
        var folderId = _smartFolderEditorId ?? Guid.NewGuid();
        var existing = SmartFolders.FirstOrDefault(item => item.SmartFolderId == folderId)
            ?? (SelectedSmartFolder?.SmartFolderId == folderId ? SelectedSmartFolder : null)
            ?? (_smartFolderEditorSnapshot?.SmartFolderId == folderId ? _smartFolderEditorSnapshot : null);
        var name = string.IsNullOrWhiteSpace(SmartFolderName) ? "新智能文件夹" : SmartFolderName.Trim();
        var description = string.IsNullOrWhiteSpace(SmartBasicBuilderExplanation) ? existing?.Description ?? string.Empty : SmartBasicBuilderExplanation;
        folder = existing is null
            ? new(folderId, name, SmartFolderLogic.And, description)
            : existing with { Name = name, Description = description, UpdatedAt = DateTimeOffset.UtcNow };

        if (!TryBuildEditorRules(folder.SmartFolderId, out rules, out error)) return false;
        if (rules.Count == 0)
        {
            error = "请至少填写一条 Smart Folder 条件";
            return false;
        }
        return true;
    }

    private bool TryBuildEditorRules(Guid folderId, out IReadOnlyList<SmartFolderRule> rules, out string error)
    {
        error = string.Empty;
        var represented = new HashSet<SmartFolderField>
        {
            SmartFolderField.FileName, SmartFolderField.Extension, SmartFolderField.MediaType, SmartFolderField.Folder,
            SmartFolderField.Tag, SmartFolderField.Rating, SmartFolderField.AddedAt, SmartFolderField.IsMissing,
            SmartFolderField.VisualToneKey, SmartFolderField.VisualAnalysisStatus, SmartFolderField.VisualAverageSaturation,
            SmartFolderField.VisualDominantHue, SmartFolderField.VisualDominantColor
        };
        var result = _smartFolderEditorRules.Where(rule => !represented.Contains(rule.Field)).ToList();

        AddTextRule(result, folderId, SmartFolderField.FileName, SmartFileNameValue, _smartFileNameOperator);
        AddTextRule(result, folderId, SmartFolderField.Extension, SmartExtensionValue, _smartExtensionOperator);
        AddTextRule(result, folderId, SmartFolderField.MediaType, SmartMediaTypeValue, _smartMediaTypeOperator);
        AddTextRule(result, folderId, SmartFolderField.Folder, SmartFolderValue, _smartFolderOperator);
        AddTextRule(result, folderId, SmartFolderField.Tag, SmartTagValue, _smartTagOperator);

        if (int.TryParse(SmartRuleValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rating))
        {
            if (rating is < 0 or > 5)
            {
                error = "评分条件必须是 0 到 5 的整数。"; rules = []; return false;
            }
            AddRule(result, folderId, SmartFolderField.Rating, _smartRatingOperator, rating.ToString(CultureInfo.InvariantCulture));
        }
        else if (!string.IsNullOrWhiteSpace(SmartRuleValue)) { error = "评分条件必须是 0 到 5 的整数。"; rules = []; return false; }

        if (!string.IsNullOrWhiteSpace(SmartAddedAtFrom))
        {
            if (!TryNormalizeDate(SmartAddedAtFrom, out var from)) { error = "添加时间起点格式无效，请使用 yyyy-MM-dd。"; rules = []; return false; }
            AddRule(result, folderId, SmartFolderField.AddedAt, _smartAddedAtFromOperator, from);
        }
        if (!string.IsNullOrWhiteSpace(SmartAddedAtTo))
        {
            if (!TryNormalizeDate(SmartAddedAtTo, out var to)) { error = "添加时间终点格式无效，请使用 yyyy-MM-dd。"; rules = []; return false; }
            AddRule(result, folderId, SmartFolderField.AddedAt, _smartAddedAtToOperator, to);
        }

        if (SmartMissingValue == SmartMissingOnly) AddRule(result, folderId, SmartFolderField.IsMissing, SmartFolderOperator.IsTrue, string.Empty);
        else if (SmartMissingValue == SmartPresentOnly) AddRule(result, folderId, SmartFolderField.IsMissing, SmartFolderOperator.IsFalse, string.Empty);
        else if (!string.IsNullOrWhiteSpace(SmartMissingValue) && SmartMissingValue != SmartMissingAny)
        {
            error = "文件缺失条件无效。"; rules = []; return false;
        }

        AddTextRule(result, folderId, SmartFolderField.VisualToneKey, SmartToneKey, _smartToneOperator);
        AddTextRule(result, folderId, SmartFolderField.VisualAnalysisStatus, SmartAnalysisStatus, _smartAnalysisStatusOperator);
        AddRuleIfValue(result, folderId, SmartFolderField.VisualAverageSaturation, _smartAverageSaturationOperator, SmartAverageSaturationMaximum, numeric: true, ref error);
        AddRuleIfValue(result, folderId, SmartFolderField.VisualDominantHue, _smartDominantHueOperator, SmartDominantHueRange, numeric: false, ref error);
        AddTextRule(result, folderId, SmartFolderField.VisualDominantColor, SmartDominantColor, _smartDominantColorOperator);
        if (error.Length > 0) { rules = []; return false; }

        rules = result.Select((rule, index) => rule with { SortOrder = index }).ToArray();
        return true;
    }

    private void AddRuleIfValue(List<SmartFolderRule> result, Guid folderId, SmartFolderField field, SmartFolderOperator op, string value, bool numeric, ref string error)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var normalized = value.Trim();
        if (numeric && !double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            error = "视觉数值条件格式无效。"; return;
        }
        AddRule(result, folderId, field, op, normalized);
    }

    private void AddTextRule(List<SmartFolderRule> result, Guid folderId, SmartFolderField field, string value, SmartFolderOperator op)
    {
        if (!string.IsNullOrWhiteSpace(value)) AddRule(result, folderId, field, op, value.Trim());
    }

    private void AddRule(List<SmartFolderRule> result, Guid folderId, SmartFolderField field, SmartFolderOperator op, string value)
    {
        // A field may legitimately occur more than once (for example, an
        // AddedAt lower and upper bound). Consume persisted prototypes in
        // order so each rebuilt rule keeps a distinct RuleId and its group
        // metadata; create a new id only when the editor adds a new
        // occurrence.
        var occurrence = result.Count(rule => rule.Field == field);
        var prototype = _smartFolderEditorRules.Where(rule => rule.Field == field).Skip(occurrence).FirstOrDefault();
        result.Add(prototype is null
            ? new(Guid.NewGuid(), folderId, field, op, value)
            : prototype with { SmartFolderId = folderId, Operator = op, Value = value });
    }

    private static bool TryNormalizeDate(string value, out string normalized)
    {
        if (!DateTimeOffset.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            normalized = string.Empty;
            return false;
        }
        normalized = parsed.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        return true;
    }

    private static string OperatorLabel(SmartFolderOperator op) => op switch
    {
        SmartFolderOperator.Contains => "包含",
        SmartFolderOperator.Equals => "=",
        SmartFolderOperator.StartsWith => "以…开头",
        SmartFolderOperator.EndsWith => "以…结尾",
        SmartFolderOperator.NotEquals => "≠",
        _ => op.ToString()
    };

}
