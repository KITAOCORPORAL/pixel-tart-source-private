using System.Collections.ObjectModel;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;

namespace PixelTart.Modules.AssetLibrary;

public sealed record P3QueryOption<T>(T Value, string Label) where T : struct, Enum;
public sealed record P3QueryValueOption(string Value, string Label);

/// <summary>
/// Editable presentation node shared by the P3 query composer and Smart Folder
/// editor.  It owns no repository state; every mutation is reported to the owning
/// view model, which performs validation, cancellation and generation guarding.
/// </summary>
public sealed class P3QueryNodeView : ObservableObject
{
    private readonly Action _changed;
    private readonly string _automationScope;
    private P3QueryNodeView? _parent;
    private AssetQueryLogic _logic;
    private AssetQueryField _field;
    private AssetQueryOperator _operator;
    private AssetQueryCaseSensitivity _caseSensitivity;
    private string _valueText;
    private string _pendingReferenceValue = string.Empty;
    private bool _enabled;
    private bool _locked;
    private bool _negated;
    private string _validationMessage = string.Empty;

    private P3QueryNodeView(AssetQueryNode model, P3QueryNodeView? parent, Action changed, string automationScope)
    {
        _changed = changed;
        _parent = parent;
        _automationScope = automationScope;
        Kind = model.Kind;
        _logic = model.Logic;
        _field = model.Field ?? AssetQueryField.FileName;
        _operator = model.Operator ?? AssetQueryOperator.Contains;
        _caseSensitivity = model.CaseSensitivity;
        _valueText = FormatValues(model.Operator, model.Values);
        _enabled = model.Enabled;
        _locked = model.Locked;
        _negated = model.Negated;
        SyncReferenceValuesFromText();
        foreach (var child in model.Children) Children.Add(new(child, this, changed, automationScope));

        AddRuleCommand = new(AddRule, () => IsGroup);
        AddGroupCommand = new(AddGroup, () => IsGroup);
        RemoveCommand = new(Remove, () => _parent is not null);
        MoveUpCommand = new(() => Move(-1), () => _parent is not null);
        MoveDownCommand = new(() => Move(1), () => _parent is not null);
        AddReferenceValueCommand = new(AddReferenceValue, CanAddReferenceValue);
        RemoveReferenceValueCommand = new(RemoveReferenceValue, value => value is not null);
    }

    public AssetQueryNodeKind Kind { get; }
    public bool IsGroup => Kind == AssetQueryNodeKind.Group;
    public bool IsRule => Kind == AssetQueryNodeKind.Rule;
    public ObservableCollection<P3QueryNodeView> Children { get; } = [];
    public ObservableCollection<P3QueryReferenceValueView> ReferenceValues { get; } = [];

    public AssetQueryLogic Logic
    {
        get => _logic;
        set { if (SetProperty(ref _logic, value)) PublishChange(); }
    }

    public AssetQueryField Field
    {
        get => _field;
        set
        {
            if (!SetProperty(ref _field, value)) return;
            OnPropertyChanged(nameof(OperatorOptions));
            OnPropertyChanged(nameof(CanChooseCaseSensitivity));
            if (!CanChooseCaseSensitivity && _caseSensitivity != AssetQueryCaseSensitivity.Insensitive)
            {
                _caseSensitivity = AssetQueryCaseSensitivity.Insensitive;
                OnPropertyChanged(nameof(CaseSensitivity));
                OnPropertyChanged(nameof(IsCaseSensitive));
            }
            NotifyValueEditorChanged();
            SyncReferenceValuesFromText();
            if (!OperatorOptions.Any(option => option.Value == Operator))
                Operator = OperatorOptions[0].Value;
            PublishChange();
        }
    }

    public AssetQueryOperator Operator
    {
        get => _operator;
        set
        {
            if (!SetProperty(ref _operator, value)) return;
            OnPropertyChanged(nameof(RequiresValue));
            NotifyValueEditorChanged();
            SyncReferenceValuesFromText();
            PublishChange();
        }
    }

    public AssetQueryCaseSensitivity CaseSensitivity
    {
        get => _caseSensitivity;
        set
        {
            var effective = CanChooseCaseSensitivity && Enum.IsDefined(value)
                ? value
                : AssetQueryCaseSensitivity.Insensitive;
            if (!SetProperty(ref _caseSensitivity, effective)) return;
            OnPropertyChanged(nameof(IsCaseSensitive));
            PublishChange();
        }
    }

    public bool IsCaseSensitive
    {
        get => CaseSensitivity == AssetQueryCaseSensitivity.Sensitive;
        set => CaseSensitivity = value ? AssetQueryCaseSensitivity.Sensitive : AssetQueryCaseSensitivity.Insensitive;
    }

    public bool CanChooseCaseSensitivity => IsRule && AssetQueryDocumentCodec.SupportsCaseSensitivity(Field);

    public string ValueText
    {
        get => _valueText;
        set
        {
            if (!SetProperty(ref _valueText, value ?? string.Empty)) return;
            OnPropertyChanged(nameof(FirstValue));
            OnPropertyChanged(nameof(SecondValue));
            OnPropertyChanged(nameof(DateValue));
            OnPropertyChanged(nameof(RangeStartDate));
            OnPropertyChanged(nameof(RangeEndDate));
            SyncReferenceValuesFromText();
            PublishChange();
        }
    }

    public string PendingReferenceValue
    {
        get => _pendingReferenceValue;
        set
        {
            if (!SetProperty(ref _pendingReferenceValue, value ?? string.Empty)) return;
            AddReferenceValueCommand.RaiseCanExecuteChanged();
        }
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (!SetProperty(ref _enabled, value)) return;
            OnPropertyChanged(nameof(IsDisabled));
            PublishChange();
        }
    }

    public bool IsDisabled
    {
        get => !Enabled;
        set => Enabled = !value;
    }

    public bool Locked
    {
        get => _locked;
        set { if (SetProperty(ref _locked, value)) PublishChange(); }
    }

    public bool Negated
    {
        get => _negated;
        set { if (SetProperty(ref _negated, value)) PublishChange(); }
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        internal set => SetProperty(ref _validationMessage, value ?? string.Empty);
    }

    public bool RequiresValue => Operator is not (
        AssetQueryOperator.IsEmpty or AssetQueryOperator.IsNotEmpty or
        AssetQueryOperator.IsTrue or AssetQueryOperator.IsFalse or
        AssetQueryOperator.Unknown or AssetQueryOperator.Known);

    public bool IsReferenceEditor => RequiresValue && Field is AssetQueryField.Folder or AssetQueryField.Tag;
    public bool IsFolderReferenceEditor => IsReferenceEditor && Field == AssetQueryField.Folder;
    public bool IsTagReferenceEditor => IsReferenceEditor && Field == AssetQueryField.Tag;
    public bool IsEnumEditor => RequiresValue && IsEnumField(Field);
    public bool IsDateEditor => RequiresValue && IsDateField(Field) && Operator != AssetQueryOperator.Between;
    public bool IsDateRangeEditor => RequiresValue && IsDateField(Field) && Operator == AssetQueryOperator.Between;
    public bool IsNumericEditor => RequiresValue && IsNumericField(Field) && Operator != AssetQueryOperator.Between;
    public bool IsNumericRangeEditor => RequiresValue && IsNumericField(Field) && Operator == AssetQueryOperator.Between;
    public bool IsColorEditor => RequiresValue && Field == AssetQueryField.VisualDominantColor;
    public bool IsTextEditor => RequiresValue && !IsReferenceEditor && !IsEnumEditor && !IsDateEditor &&
        !IsDateRangeEditor && !IsNumericEditor && !IsNumericRangeEditor && !IsColorEditor;

    public IReadOnlyList<P3QueryValueOption> ValueOptions => GetValueOptions(Field);

    public string FirstValue
    {
        get => SplitValueText().ElementAtOrDefault(0) ?? string.Empty;
        set => SetRangeValue(0, value);
    }

    public string SecondValue
    {
        get => SplitValueText().ElementAtOrDefault(1) ?? string.Empty;
        set => SetRangeValue(1, value);
    }

    public DateTime? DateValue
    {
        get => ParseDate(FirstValue);
        set => ValueText = FormatDate(value);
    }

    public DateTime? RangeStartDate
    {
        get => ParseDate(FirstValue);
        set => SetRangeValue(0, FormatDate(value));
    }

    public DateTime? RangeEndDate
    {
        get => ParseDate(SecondValue);
        set => SetRangeValue(1, FormatDate(value));
    }

    public IReadOnlyList<P3QueryOption<AssetQueryLogic>> LogicOptions { get; } =
    [
        new(AssetQueryLogic.All, "全部满足"),
        new(AssetQueryLogic.Any, "任一满足")
    ];

    public IReadOnlyList<P3QueryOption<AssetQueryField>> FieldOptions => QueryFieldOptions;
    public IReadOnlyList<P3QueryOption<AssetQueryOperator>> OperatorOptions => GetOperatorOptions(Field);

    public AssetCommand AddRuleCommand { get; }
    public AssetCommand AddGroupCommand { get; }
    public AssetCommand RemoveCommand { get; }
    public AssetCommand MoveUpCommand { get; }
    public AssetCommand MoveDownCommand { get; }
    public AssetCommand AddReferenceValueCommand { get; }
    public AssetCommand<P3QueryReferenceValueView> RemoveReferenceValueCommand { get; }

    public string AutomationId => $"P3QueryNode_{_automationScope}_{AutomationPath}";
    public string AccessibleName => IsGroup ? $"规则组，{LogicLabel(Logic)}，{Children.Count} 个子项" : $"规则，{FieldLabel(Field)}，{OperatorLabel(Operator)}";
    public string LogicAutomationId => AutomationId + "_Logic";
    public string FieldAutomationId => AutomationId + "_Field";
    public string OperatorAutomationId => AutomationId + "_Operator";
    public string CaseSensitivityAutomationId => AutomationId + "_CaseSensitivity";
    public string ValueAutomationId => AutomationId + "_Value";
    public string EnabledAutomationId => AutomationId + "_Enabled";
    public string LockedAutomationId => AutomationId + "_Locked";
    public string NegatedAutomationId => AutomationId + "_Negated";
    public string AddRuleAutomationId => AutomationId + "_AddRule";
    public string AddGroupAutomationId => AutomationId + "_AddGroup";
    public string MoveUpAutomationId => AutomationId + "_MoveUp";
    public string MoveDownAutomationId => AutomationId + "_MoveDown";
    public string RemoveAutomationId => AutomationId + "_Remove";
    public string ValidationAutomationId => AutomationId + "_Validation";
    public string ReferencePickerAutomationId => ValueAutomationId + "_Picker";
    public string AddReferenceAutomationId => ValueAutomationId + "_Add";

    public static P3QueryNodeView CreateRoot(Action changed, string automationScope = "Query") =>
        new(AssetQueryNode.Group(AssetQueryLogic.All), null, changed, automationScope);

    public static P3QueryNodeView FromModel(AssetQueryNode model, Action changed, string automationScope = "Query")
    {
        var root = model.Kind == AssetQueryNodeKind.Group ? model : AssetQueryNode.Group(AssetQueryLogic.All, [model]);
        return new(root, null, changed, automationScope);
    }

    public AssetQueryNode ToModel()
    {
        if (IsGroup)
            return AssetQueryNode.Group(Logic, Children.Select(child => child.ToModel()), Negated, Enabled);
        var values = IsReferenceEditor
            ? ReferenceValues.Select(value => value.Value).ToArray()
            : ParseValues(Field, Operator, ValueText);
        return AssetQueryNode.Rule(Field, Operator, values, Negated, Enabled, Locked, CaseSensitivity);
    }

    public IEnumerable<P3QueryNodeView> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var nested in child.DescendantsAndSelf()) yield return nested;
    }

    public void ClearUnlocked(bool keepRoot = true)
    {
        if (!IsGroup) return;
        for (var index = Children.Count - 1; index >= 0; index--)
        {
            var child = Children[index];
            if (child.IsRule && !child.Locked) Children.RemoveAt(index);
            else if (child.IsGroup)
            {
                child.ClearUnlocked(keepRoot: false);
                if (child.Children.Count == 0) Children.RemoveAt(index);
            }
        }
        if (keepRoot) PublishChange();
    }

    public void ClearAll()
    {
        if (!IsGroup) return;
        Children.Clear();
        PublishChange();
    }

    private void AddRule()
    {
        if (!IsGroup) return;
        Children.Add(new(AssetQueryNode.Rule(AssetQueryField.FileName, AssetQueryOperator.Contains, ["新条件"]), this, _changed, _automationScope));
        NotifyAutomationIdentityRecursively();
        PublishChange();
    }

    private void AddGroup()
    {
        if (!IsGroup) return;
        Children.Add(new(AssetQueryNode.Group(AssetQueryLogic.All), this, _changed, _automationScope));
        NotifyAutomationIdentityRecursively();
        PublishChange();
    }

    private void Remove()
    {
        if (_parent is null) return;
        _parent.Children.Remove(this);
        _parent.NotifyAutomationIdentityRecursively();
        _parent.PublishChange();
        _parent = null;
    }

    private void Move(int delta)
    {
        if (_parent is null) return;
        var index = _parent.Children.IndexOf(this);
        var target = Math.Clamp(index + delta, 0, _parent.Children.Count - 1);
        if (target == index) return;
        _parent.Children.Move(index, target);
        _parent.NotifyAutomationIdentityRecursively();
        _parent.PublishChange();
    }

    private void PublishChange()
    {
        OnPropertyChanged(nameof(AccessibleName));
        ValidationMessage = string.Empty;
        _changed();
    }

    private string AutomationPath
    {
        get
        {
            if (_parent is null) return "Root";
            var index = _parent.Children.IndexOf(this);
            return $"{_parent.AutomationPath}_{Math.Max(0, index)}";
        }
    }

    private void NotifyAutomationIdentityRecursively()
    {
        foreach (var property in new[]
        {
            nameof(AutomationId), nameof(LogicAutomationId), nameof(FieldAutomationId), nameof(OperatorAutomationId),
            nameof(CaseSensitivityAutomationId),
            nameof(ValueAutomationId), nameof(EnabledAutomationId), nameof(LockedAutomationId), nameof(NegatedAutomationId),
            nameof(AddRuleAutomationId), nameof(AddGroupAutomationId), nameof(MoveUpAutomationId), nameof(MoveDownAutomationId),
            nameof(RemoveAutomationId), nameof(ValidationAutomationId)
        }) OnPropertyChanged(property);
        OnPropertyChanged(nameof(ReferencePickerAutomationId));
        OnPropertyChanged(nameof(AddReferenceAutomationId));
        foreach (var reference in ReferenceValues) reference.NotifyAutomationIdentityChanged();
        foreach (var child in Children) child.NotifyAutomationIdentityRecursively();
    }

    private void NotifyValueEditorChanged()
    {
        foreach (var property in new[]
        {
            nameof(IsReferenceEditor), nameof(IsFolderReferenceEditor), nameof(IsTagReferenceEditor), nameof(IsEnumEditor),
            nameof(IsDateEditor), nameof(IsDateRangeEditor), nameof(IsNumericEditor), nameof(IsNumericRangeEditor),
            nameof(IsColorEditor), nameof(IsTextEditor), nameof(ValueOptions)
        }) OnPropertyChanged(property);
        AddReferenceValueCommand.RaiseCanExecuteChanged();
    }

    private bool CanAddReferenceValue()
    {
        if (!IsReferenceEditor || !TryNormalizeReferenceValue(PendingReferenceValue, out var value)) return false;
        return ReferenceValues.All(current => !string.Equals(current.Value, value, StringComparison.OrdinalIgnoreCase));
    }

    private void AddReferenceValue()
    {
        if (!TryNormalizeReferenceValue(PendingReferenceValue, out var value) ||
            ReferenceValues.Any(current => string.Equals(current.Value, value, StringComparison.OrdinalIgnoreCase))) return;
        ValueText = FormatValues(Operator, ReferenceValues.Select(current => current.Value).Append(value).ToArray());
        PendingReferenceValue = string.Empty;
    }

    private void RemoveReferenceValue(P3QueryReferenceValueView? value)
    {
        if (value is null) return;
        ValueText = FormatValues(Operator, ReferenceValues
            .Where(current => !ReferenceEquals(current, value))
            .Select(current => current.Value)
            .ToArray());
    }

    private void SyncReferenceValuesFromText()
    {
        var values = ParseValues(Field, Operator, _valueText)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (ReferenceValues.Select(value => value.Value).SequenceEqual(values, StringComparer.OrdinalIgnoreCase)) return;
        ReferenceValues.Clear();
        foreach (var value in values) ReferenceValues.Add(new(this, value));
        AddReferenceValueCommand?.RaiseCanExecuteChanged();
    }

    private static bool TryNormalizeReferenceValue(string? candidate, out string value)
    {
        value = string.Empty;
        var trimmed = (candidate ?? string.Empty).Trim();
        if (!trimmed.StartsWith("id:", StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(trimmed[3..], out var id) || id == Guid.Empty) return false;
        value = $"id:{id:D}";
        return true;
    }

    private string[] SplitValueText() => (ValueText ?? string.Empty)
        .Split(["..", ",", "，"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void SetRangeValue(int index, string? value)
    {
        var values = SplitValueText().Concat([string.Empty, string.Empty]).Take(2).ToArray();
        values[index] = value?.Trim() ?? string.Empty;
        ValueText = values.All(string.IsNullOrWhiteSpace) ? string.Empty : $"{values[0]} .. {values[1]}";
    }

    private static DateTime? ParseDate(string value) =>
        DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeLocal, out var date) ? date : null;

    private static string FormatDate(DateTime? value) => value?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

    private static IReadOnlyList<string> ParseValues(AssetQueryField field, AssetQueryOperator operation, string text)
    {
        if (operation is AssetQueryOperator.IsEmpty or AssetQueryOperator.IsNotEmpty or AssetQueryOperator.IsTrue or AssetQueryOperator.IsFalse or AssetQueryOperator.Unknown or AssetQueryOperator.Known)
            return [];
        var separators = operation == AssetQueryOperator.Between ? new[] { "..", ",", "，" } : new[] { ",", "，", ";", "；" };
        var values = (text ?? string.Empty).Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (field is AssetQueryField.Folder or AssetQueryField.Tag)
            return values;
        return values;
    }

    private static string FormatValues(AssetQueryOperator? operation, IReadOnlyList<string> values)
    {
        return operation == AssetQueryOperator.Between ? string.Join(" .. ", values) : string.Join("，", values);
    }

    private static bool IsDateField(AssetQueryField field) =>
        field is AssetQueryField.AddedAt or AssetQueryField.CaptureTime;

    private static bool IsNumericField(AssetQueryField field) => field is
        AssetQueryField.Rating or AssetQueryField.FileSize or AssetQueryField.Width or AssetQueryField.Height or
        AssetQueryField.LongEdge or AssetQueryField.ShortEdge or AssetQueryField.PixelCount or AssetQueryField.AspectRatio or
        AssetQueryField.VisualDominantHue or AssetQueryField.VisualAverageLuma or AssetQueryField.VisualAverageSaturation or
        AssetQueryField.VisualLumaSpread or AssetQueryField.VisualShadowRatio or AssetQueryField.VisualHighlightRatio or
        AssetQueryField.VisualBlackClipRatio or AssetQueryField.VisualWhiteClipRatio;

    private static bool IsEnumField(AssetQueryField field) => field is
        AssetQueryField.MediaType or AssetQueryField.Orientation or AssetQueryField.VisualAnalysisStatus or
        AssetQueryField.VisualHarmony or AssetQueryField.VisualToneKey or AssetQueryField.VisualContrast or
        AssetQueryField.VisualSaturation or AssetQueryField.VisualWarmCool;

    private static IReadOnlyList<P3QueryValueOption> GetValueOptions(AssetQueryField field)
    {
        string[] values = field switch
        {
            AssetQueryField.MediaType => ["Image", "Raw", "Document", "Video", "Other"],
            AssetQueryField.Orientation => ["Landscape", "Portrait", "Square"],
            AssetQueryField.VisualAnalysisStatus => ["NotAnalyzed", "Valid", "Stale", "Failed"],
            AssetQueryField.VisualHarmony => ["LowSaturationNeutral", "Monochrome", "Analogous", "Complementary", "SplitComplementary", "Triadic", "Mixed"],
            AssetQueryField.VisualToneKey => ["Low", "Mid", "High"],
            AssetQueryField.VisualContrast => ["Low", "Medium", "High"],
            AssetQueryField.VisualSaturation => ["Low", "Medium", "High"],
            AssetQueryField.VisualWarmCool => ["Cool", "Neutral", "Warm"],
            _ => []
        };
        return values.Select(value => new P3QueryValueOption(value, EnumValueLabel(value))).ToArray();
    }

    private static string EnumValueLabel(string value) => value switch
    {
        "Image" => "图片", "Raw" => "RAW", "Document" => "文档", "Video" => "视频", "Other" => "其他",
        "Landscape" => "横向", "Portrait" => "纵向", "Square" => "方形",
        "NotAnalyzed" => "未分析", "Valid" => "有效", "Stale" => "待重算", "Failed" => "失败",
        "Low" => "低", "Mid" => "中", "High" => "高", "Medium" => "中",
        "Cool" => "冷", "Neutral" => "中性", "Warm" => "暖",
        "LowSaturationNeutral" => "低饱和中性", "Monochrome" => "单色", "Analogous" => "邻近色",
        "Complementary" => "互补色", "SplitComplementary" => "分裂互补", "Triadic" => "三角色", "Mixed" => "混合",
        _ => value
    };

    private static IReadOnlyList<P3QueryOption<AssetQueryOperator>> GetOperatorOptions(AssetQueryField field) =>
        AssetQueryDocumentCodec.GetSupportedOperators(field)
            .Select(value => new P3QueryOption<AssetQueryOperator>(value, OperatorLabel(value)))
            .ToArray();

    private static readonly IReadOnlyList<P3QueryOption<AssetQueryField>> QueryFieldOptions = Enum.GetValues<AssetQueryField>()
        .Select(value => new P3QueryOption<AssetQueryField>(value, FieldLabel(value))).ToArray();

    private static string LogicLabel(AssetQueryLogic value) => value == AssetQueryLogic.All ? "全部满足" : "任一满足";
    public static string FieldLabel(AssetQueryField value) => value switch
    {
        AssetQueryField.FileName => "文件名", AssetQueryField.Extension => "扩展名", AssetQueryField.MediaType => "媒体类型",
        AssetQueryField.Folder => "文件夹", AssetQueryField.Tag => "标签", AssetQueryField.Rating => "评分",
        AssetQueryField.Comment => "备注", AssetQueryField.AddedAt => "导入日期", AssetQueryField.CaptureTime => "拍摄日期",
        AssetQueryField.FileSize => "文件大小", AssetQueryField.Width => "宽度", AssetQueryField.Height => "高度",
        AssetQueryField.LongEdge => "长边", AssetQueryField.ShortEdge => "短边", AssetQueryField.PixelCount => "像素总数",
        AssetQueryField.AspectRatio => "宽高比", AssetQueryField.Orientation => "方向", AssetQueryField.IsUncategorized => "未归类",
        AssetQueryField.IsUntagged => "未打标签", AssetQueryField.IsMissing => "缺失状态", AssetQueryField.IsArchived => "归档状态",
        AssetQueryField.VisualAnalysisStatus => "视觉分析状态", AssetQueryField.VisualHarmony => "配色和谐度",
        AssetQueryField.VisualToneKey => "影调", AssetQueryField.VisualContrast => "视觉对比",
        AssetQueryField.VisualSaturation => "饱和度分类", AssetQueryField.VisualWarmCool => "冷暖倾向",
        AssetQueryField.VisualDominantHue => "主色 Hue", AssetQueryField.VisualDominantColor => "主色",
        AssetQueryField.VisualAverageLuma => "平均亮度", AssetQueryField.VisualAverageSaturation => "平均饱和度",
        AssetQueryField.VisualLumaSpread => "亮度跨度", AssetQueryField.VisualShadowRatio => "暗部比例",
        AssetQueryField.VisualHighlightRatio => "高光比例", AssetQueryField.VisualBlackClipRatio => "暗部剪切",
        AssetQueryField.VisualWhiteClipRatio => "高光剪切", _ => value.ToString()
    };

    public static string OperatorLabel(AssetQueryOperator value) => value switch
    {
        AssetQueryOperator.Contains => "包含", AssetQueryOperator.NotContains => "不包含", AssetQueryOperator.Equals => "等于",
        AssetQueryOperator.NotEquals => "不等于", AssetQueryOperator.StartsWith => "开头是", AssetQueryOperator.EndsWith => "结尾是",
        AssetQueryOperator.GreaterThan => "大于", AssetQueryOperator.GreaterThanOrEqual => "大于等于",
        AssetQueryOperator.LessThan => "小于", AssetQueryOperator.LessThanOrEqual => "小于等于", AssetQueryOperator.Between => "区间",
        AssetQueryOperator.IsEmpty => "为空", AssetQueryOperator.IsNotEmpty => "非空", AssetQueryOperator.IsTrue => "是",
        AssetQueryOperator.IsFalse => "否", AssetQueryOperator.Regex => "正则表达式", AssetQueryOperator.AnyOf => "任一属于",
        AssetQueryOperator.AllOf => "全部属于", AssetQueryOperator.NoneOf => "均不属于", AssetQueryOperator.Unknown => "未知",
        AssetQueryOperator.Known => "已知", _ => value.ToString()
    };
}

public sealed class P3QueryReferenceValueView(P3QueryNodeView owner, string value) : ObservableObject
{
    public string Value { get; } = value;
    public string AutomationId => $"{owner.ValueAutomationId}_Reference_{Value}";
    public string AccessibleName => $"移除筛选引用 {Value}";

    internal void NotifyAutomationIdentityChanged() => OnPropertyChanged(nameof(AutomationId));
}
