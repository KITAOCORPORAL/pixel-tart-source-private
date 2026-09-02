using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RAWSelectionAssistant.Core.Models;

public enum AssetQueryScope
{
    Current,
    AllAssets
}

public enum AssetQueryLogic
{
    All,
    Any
}

public enum AssetQueryNodeKind
{
    Group,
    Rule
}

public enum AssetQueryCaseSensitivity
{
    Insensitive,
    Sensitive
}

public enum AssetQueryField
{
    FileName,
    Extension,
    MediaType,
    Folder,
    Tag,
    Rating,
    Comment,
    AddedAt,
    CaptureTime,
    FileSize,
    Width,
    Height,
    LongEdge,
    ShortEdge,
    PixelCount,
    AspectRatio,
    Orientation,
    IsUncategorized,
    IsUntagged,
    IsMissing,
    IsArchived,
    VisualAnalysisStatus,
    VisualHarmony,
    VisualToneKey,
    VisualContrast,
    VisualSaturation,
    VisualWarmCool,
    VisualDominantHue,
    VisualDominantColor,
    VisualAverageLuma,
    VisualAverageSaturation,
    VisualLumaSpread,
    VisualShadowRatio,
    VisualHighlightRatio,
    VisualBlackClipRatio,
    VisualWhiteClipRatio
}

public enum AssetQueryOperator
{
    Contains,
    NotContains,
    Equals,
    NotEquals,
    StartsWith,
    EndsWith,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Between,
    IsEmpty,
    IsNotEmpty,
    IsTrue,
    IsFalse,
    Regex,
    AnyOf,
    AllOf,
    NoneOf,
    Unknown,
    Known
}

/// <summary>
/// A single versioned query node. Groups use <see cref="Children"/> and rules use
/// <see cref="Field"/>, <see cref="Operator"/> and <see cref="Values"/>. Keeping one
/// discriminated record makes the persisted JSON explicit and fail-closed without
/// relying on runtime type metadata.
/// </summary>
public sealed record AssetQueryNode
{
    public AssetQueryNodeKind Kind { get; init; } = AssetQueryNodeKind.Group;
    public AssetQueryLogic Logic { get; init; } = AssetQueryLogic.All;
    public bool Negated { get; init; }
    public bool Enabled { get; init; } = true;
    public bool Locked { get; init; }
    public AssetQueryField? Field { get; init; }
    public AssetQueryOperator? Operator { get; init; }
    public AssetQueryCaseSensitivity CaseSensitivity { get; init; } = AssetQueryCaseSensitivity.Insensitive;
    public IReadOnlyList<string> Values { get; init; } = [];
    public IReadOnlyList<AssetQueryNode> Children { get; init; } = [];

    public static AssetQueryNode Group(
        AssetQueryLogic logic,
        IEnumerable<AssetQueryNode>? children = null,
        bool negated = false,
        bool enabled = true) => new()
        {
            Kind = AssetQueryNodeKind.Group,
            Logic = logic,
            Negated = negated,
            Enabled = enabled,
            Children = children?.ToArray() ?? []
        };

    public static AssetQueryNode Rule(
        AssetQueryField field,
        AssetQueryOperator @operator,
        IEnumerable<string>? values = null,
        bool negated = false,
        bool enabled = true,
        bool locked = false,
        AssetQueryCaseSensitivity caseSensitivity = AssetQueryCaseSensitivity.Insensitive) => new()
        {
            Kind = AssetQueryNodeKind.Rule,
            Field = field,
            Operator = @operator,
            Values = values?.ToArray() ?? [],
            Negated = negated,
            Enabled = enabled,
            Locked = locked,
            CaseSensitivity = caseSensitivity
        };
}

public sealed record AssetQueryDocument
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;
    public AssetQueryScope Scope { get; init; } = AssetQueryScope.Current;
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Optional independent global-search clauses. Each clause is ANDed with
    /// the others. This preserves the meaning of a saved Smart Folder text and
    /// a transient Current-scope text when their composed query is saved again.
    /// Older documents omit this member and continue to use <see cref="Text"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? SearchClauses { get; init; }

    public AssetQueryNode RootGroup { get; init; } = AssetQueryNode.Group(AssetQueryLogic.All);
    public AssetLibrarySortField SortField { get; init; } = AssetLibrarySortField.AddedAt;
    public AssetLibrarySortDirection SortDirection { get; init; } = AssetLibrarySortDirection.Descending;
    public bool IncludeArchived { get; init; }
}

public sealed record AssetQueryValidationIssue(string Path, string Message);

public sealed record AssetQueryValidationResult(
    bool IsValid,
    AssetQueryDocument? Document,
    IReadOnlyList<AssetQueryValidationIssue> Errors,
    IReadOnlyList<AssetQueryValidationIssue> Warnings)
{
    public string ErrorMessage => string.Join("；", Errors.Select(error => $"{error.Path}: {error.Message}"));
}

public static class AssetQueryDocumentCodec
{
    private const int MaximumTextLength = 500;
    private const int MaximumDepth = 8;
    private const int MaximumNodes = 256;
    private const int MaximumChildrenPerGroup = 64;
    private const int MaximumValuesPerRule = 128;

    private static readonly IReadOnlyDictionary<AssetQueryField, string[]> CanonicalEnumValues =
        new Dictionary<AssetQueryField, string[]>
        {
            [AssetQueryField.MediaType] = ["Image", "Raw", "Document", "Video", "Other"],
            [AssetQueryField.Orientation] = ["Landscape", "Portrait", "Square"],
            [AssetQueryField.VisualAnalysisStatus] = ["NotAnalyzed", "Valid", "Stale", "Failed"],
            [AssetQueryField.VisualHarmony] = ["LowSaturationNeutral", "Monochrome", "Analogous", "Complementary", "SplitComplementary", "Triadic", "Mixed"],
            [AssetQueryField.VisualToneKey] = ["Low", "Mid", "High"],
            [AssetQueryField.VisualContrast] = ["Low", "Medium", "High"],
            [AssetQueryField.VisualSaturation] = ["Low", "Medium", "High"],
            [AssetQueryField.VisualWarmCool] = ["Cool", "Neutral", "Warm"]
        };

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static AssetQueryValidationResult Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Invalid("$", "查询文档不能为空。");
        try
        {
            var document = JsonSerializer.Deserialize<AssetQueryDocument>(json, JsonOptions);
            return document is null ? Invalid("$", "查询文档不是有效对象。") : Normalize(document);
        }
        catch (JsonException exception)
        {
            return Invalid("$", $"查询文档 JSON 无效：{exception.Message}");
        }
    }

    public static AssetQueryValidationResult Normalize(AssetQueryDocument document)
    {
        var errors = new List<AssetQueryValidationIssue>();
        var warnings = new List<AssetQueryValidationIssue>();
        if (document.Version != AssetQueryDocument.CurrentVersion)
            errors.Add(new("$.version", $"不支持查询文档版本 {document.Version}。"));
        if (!Enum.IsDefined(document.Scope)) errors.Add(new("$.scope", "搜索范围无效。"));
        if (!Enum.IsDefined(document.SortField)) errors.Add(new("$.sortField", "排序字段无效。"));
        if (!Enum.IsDefined(document.SortDirection)) errors.Add(new("$.sortDirection", "排序方向无效。"));

        var text = NormalizeText(document.Text);
        if (text.Length > MaximumTextLength)
            errors.Add(new("$.text", $"搜索文字不能超过 {MaximumTextLength} 个字符。"));

        string[]? searchClauses = null;
        if (document.SearchClauses is not null)
        {
            searchClauses = document.SearchClauses
                .Select(NormalizeText)
                .Where(value => value.Length != 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (searchClauses.Any(value => value.Length > MaximumTextLength))
                errors.Add(new("$.searchClauses", $"每段搜索文字不能超过 {MaximumTextLength} 个字符。"));
            if (searchClauses.Length > 16)
                errors.Add(new("$.searchClauses", "搜索文字最多允许 16 段。"));
            if (searchClauses.Length == 0) searchClauses = null;
        }

        var nodeCount = 0;
        var root = NormalizeNode(document.RootGroup, "$.rootGroup", 0, ref nodeCount, errors, warnings);
        if (root is null || root.Kind != AssetQueryNodeKind.Group)
            errors.Add(new("$.rootGroup", "根节点必须是规则组。"));
        if (nodeCount > MaximumNodes)
            errors.Add(new("$.rootGroup", $"查询最多允许 {MaximumNodes} 个节点。"));

        if (errors.Count != 0) return new(false, null, errors, warnings);
        return new(true, document with
        {
            Text = text,
            SearchClauses = searchClauses,
            RootGroup = root!,
            Version = AssetQueryDocument.CurrentVersion
        }, errors, warnings);
    }

    public static string SerializeCanonical(AssetQueryDocument document)
    {
        var normalized = Normalize(document);
        if (!normalized.IsValid || normalized.Document is null)
            throw new ArgumentException(normalized.ErrorMessage, nameof(document));
        return JsonSerializer.Serialize(normalized.Document, JsonOptions);
    }

    public static string ComputeHash(AssetQueryDocument document)
    {
        var normalized = Normalize(document);
        if (!normalized.IsValid || normalized.Document is null)
            throw new ArgumentException(normalized.ErrorMessage, nameof(document));
        var semanticProjection = normalized.Document with
        {
            RootGroup = CreateSemanticHashProjection(normalized.Document.RootGroup),
            SearchClauses = normalized.Document.SearchClauses?
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray()
        };
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(semanticProjection, JsonOptions));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static AssetQueryDocument ClearUnlocked(AssetQueryDocument document)
    {
        var normalized = Normalize(document);
        if (!normalized.IsValid || normalized.Document is null)
            throw new ArgumentException(normalized.ErrorMessage, nameof(document));
        return normalized.Document with { RootGroup = KeepLocked(normalized.Document.RootGroup) };
    }

    private static AssetQueryNode KeepLocked(AssetQueryNode node)
    {
        if (node.Kind == AssetQueryNodeKind.Rule) return node.Locked ? node : AssetQueryNode.Group(AssetQueryLogic.All);
        var children = node.Children
            .Select(KeepLocked)
            .Where(child => child.Kind == AssetQueryNodeKind.Rule || child.Children.Count != 0)
            .ToArray();
        return node with { Children = children };
    }

    private static AssetQueryNode CreateSemanticHashProjection(AssetQueryNode node)
    {
        if (node.Kind == AssetQueryNodeKind.Rule) return node;
        var children = node.Children
            .Select(CreateSemanticHashProjection)
            .OrderBy(child => JsonSerializer.Serialize(child, JsonOptions), StringComparer.Ordinal)
            .ToArray();
        return node with { Children = children };
    }

    private static AssetQueryNode? NormalizeNode(
        AssetQueryNode? node,
        string path,
        int depth,
        ref int nodeCount,
        List<AssetQueryValidationIssue> errors,
        List<AssetQueryValidationIssue> warnings)
    {
        if (node is null)
        {
            errors.Add(new(path, "节点不能为空。"));
            return null;
        }
        nodeCount++;
        if (depth > MaximumDepth)
        {
            errors.Add(new(path, $"规则组最多允许 {MaximumDepth} 层嵌套。"));
            return null;
        }
        if (!Enum.IsDefined(node.Kind) || !Enum.IsDefined(node.Logic) || !Enum.IsDefined(node.CaseSensitivity))
        {
            errors.Add(new(path, "节点枚举值无效。"));
            return null;
        }

        var nodeValues = node.Values;
        var nodeChildren = node.Children;
        if (nodeValues is null)
        {
            errors.Add(new($"{path}.values", "规则值集合不能为空。"));
            nodeValues = [];
        }
        if (nodeChildren is null)
        {
            errors.Add(new($"{path}.children", "子节点集合不能为空。"));
            nodeChildren = [];
        }

        if (node.Kind == AssetQueryNodeKind.Group)
        {
            if (node.Field is not null || node.Operator is not null || nodeValues.Count != 0)
                errors.Add(new(path, "规则组不能包含字段、操作符或值。"));
            if (nodeChildren.Count > MaximumChildrenPerGroup)
                errors.Add(new(path, $"单个规则组最多允许 {MaximumChildrenPerGroup} 个子节点。"));
            var children = new List<AssetQueryNode>();
            for (var index = 0; index < nodeChildren.Count; index++)
            {
                var child = NormalizeNode(nodeChildren[index], $"{path}.children[{index}]", depth + 1, ref nodeCount, errors, warnings);
                if (child is not null) children.Add(child);
            }
            return node with
            {
                Field = null,
                Operator = null,
                Values = [],
                Locked = false,
                CaseSensitivity = AssetQueryCaseSensitivity.Insensitive,
                Children = children.ToArray()
            };
        }

        if (nodeChildren.Count != 0) errors.Add(new(path, "单条规则不能包含子节点。"));
        if (node.Field is null || node.Operator is null)
        {
            if (nodeValues.All(string.IsNullOrWhiteSpace))
            {
                warnings.Add(new(path, "已移除空规则。"));
                return null;
            }
            errors.Add(new(path, "规则必须指定字段和操作符。"));
            return null;
        }
        if (!Enum.IsDefined(node.Field.Value) || !Enum.IsDefined(node.Operator.Value))
        {
            errors.Add(new(path, "规则字段或操作符无效。"));
            return null;
        }
        if (nodeValues.Count > MaximumValuesPerRule)
            errors.Add(new(path, $"单条规则最多允许 {MaximumValuesPerRule} 个值。"));
        if (!IsOperatorSupported(node.Field.Value, node.Operator.Value))
            errors.Add(new(path, $"{node.Field} 不支持操作符 {node.Operator}。"));

        var caseSensitivity = SupportsCaseSensitivity(node.Field.Value)
            ? node.CaseSensitivity
            : AssetQueryCaseSensitivity.Insensitive;
        var values = NormalizeValues(node.Field.Value, node.Operator.Value, caseSensitivity, nodeValues, path, errors);
        ValidateValueCount(node.Operator.Value, values.Count, path, errors);
        return node with
        {
            Logic = AssetQueryLogic.All,
            Field = node.Field.Value,
            Operator = node.Operator.Value,
            CaseSensitivity = caseSensitivity,
            Values = values,
            Children = []
        };
    }

    private static IReadOnlyList<string> NormalizeValues(
        AssetQueryField field,
        AssetQueryOperator @operator,
        AssetQueryCaseSensitivity caseSensitivity,
        IReadOnlyList<string> values,
        string path,
        List<AssetQueryValidationIssue> errors)
    {
        var normalized = new List<string>();
        foreach (var raw in values)
        {
            var value = NormalizeText(raw);
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (IsIntegerField(field))
            {
                if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ||
                    number != decimal.Truncate(number) ||
                    !IsIntegerValueInDomain(field, number))
                {
                    errors.Add(new(path, $"数值“{value}”超出 {field} 的有效范围。"));
                    continue;
                }
                value = decimal.Truncate(number).ToString(CultureInfo.InvariantCulture);
            }
            else if (IsNumericField(field))
            {
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                {
                    errors.Add(new(path, $"数值“{value}”无效。"));
                    continue;
                }
                if (!IsNumericValueInDomain(field, number))
                {
                    errors.Add(new(path, $"数值“{value}”超出 {field} 的有效范围。"));
                    continue;
                }
                value = number.ToString("R", CultureInfo.InvariantCulture);
            }
            else if (IsDateField(field))
            {
                if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out var date))
                {
                    errors.Add(new(path, $"日期“{value}”无效。"));
                    continue;
                }
                value = date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            }
            else if (field is AssetQueryField.Folder or AssetQueryField.Tag)
            {
                if (Guid.TryParse(value, out var id)) value = "id:" + id.ToString("D");
                else if (value.StartsWith("id:", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(value[3..], out id)) value = "id:" + id.ToString("D");
                else if (value.StartsWith("name:", StringComparison.OrdinalIgnoreCase)) value = "name:" + NormalizeText(value[5..]);
                else
                {
                    errors.Add(new(path, $"引用“{value}”不是有效文件夹或标签标识。"));
                    continue;
                }
            }
            else if (field == AssetQueryField.VisualDominantColor)
            {
                var hex = value[0] == '#' ? value[1..] : value;
                if (hex.Length != 6 || !hex.All(Uri.IsHexDigit))
                {
                    errors.Add(new(path, $"颜色“{value}”必须是 6 位十六进制 RGB。"));
                    continue;
                }
                value = "#" + hex.ToUpperInvariant();
            }
            else if (CanonicalEnumValues.TryGetValue(field, out var allowedValues))
            {
                var canonical = allowedValues.FirstOrDefault(candidate =>
                    string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
                if (canonical is null)
                {
                    errors.Add(new(path, $"值“{value}”不是 {field} 的有效枚举值。"));
                    continue;
                }
                value = canonical;
            }
            normalized.Add(value);
        }

        IReadOnlyList<string> result = normalized;
        if (@operator is AssetQueryOperator.AnyOf or AssetQueryOperator.AllOf or AssetQueryOperator.NoneOf)
        {
            if ((caseSensitivity == AssetQueryCaseSensitivity.Insensitive && SupportsCaseSensitivity(field)) ||
                field is AssetQueryField.Folder or AssetQueryField.Tag)
            {
                normalized = normalized
                    .Select(value => CanonicalizeSqliteNoCaseSetValue(field, value))
                    .ToList();
            }
            var distinct = normalized.Distinct(StringComparer.Ordinal).ToList();
            distinct.Sort(StringComparer.Ordinal);
            result = distinct;
        }
        ValidateRangeOrder(field, @operator, result, path, errors);
        return result;
    }

    private static void ValidateRangeOrder(
        AssetQueryField field,
        AssetQueryOperator @operator,
        IReadOnlyList<string> values,
        string path,
        List<AssetQueryValidationIssue> errors)
    {
        if (@operator != AssetQueryOperator.Between || values.Count != 2 || field == AssetQueryField.VisualDominantHue)
            return;
        if (IsNumericField(field) &&
            decimal.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numericLower) &&
            decimal.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var numericUpper) &&
            numericLower > numericUpper)
        {
            errors.Add(new(path, "区间下限不能大于上限。"));
        }
        else if (IsDateField(field) &&
                 DateTimeOffset.TryParse(values[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateLower) &&
                 DateTimeOffset.TryParse(values[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateUpper) &&
                 dateLower > dateUpper)
        {
            errors.Add(new(path, "区间开始日期不能晚于结束日期。"));
        }
    }

    private static void ValidateValueCount(AssetQueryOperator @operator, int count, string path, List<AssetQueryValidationIssue> errors)
    {
        if (@operator is AssetQueryOperator.IsEmpty or AssetQueryOperator.IsNotEmpty or AssetQueryOperator.IsTrue or AssetQueryOperator.IsFalse or AssetQueryOperator.Unknown or AssetQueryOperator.Known)
        {
            if (count != 0) errors.Add(new(path, $"操作符 {@operator} 不接受值。"));
            return;
        }
        if (@operator == AssetQueryOperator.Between)
        {
            if (count != 2) errors.Add(new(path, "区间操作符必须提供两个值。"));
            return;
        }
        if (@operator is AssetQueryOperator.AnyOf or AssetQueryOperator.AllOf or AssetQueryOperator.NoneOf)
        {
            if (count == 0) errors.Add(new(path, "集合操作符至少需要一个值。"));
            return;
        }
        if (count != 1) errors.Add(new(path, $"操作符 {@operator} 必须提供一个值。"));
    }

    /// <summary>
    /// Returns the single authoritative field/operator capability matrix used by
    /// validation and presentation. Callers must not offer an operator that is
    /// absent from this list because the canonical codec will reject it.
    /// </summary>
    public static IReadOnlyList<AssetQueryOperator> GetSupportedOperators(AssetQueryField field)
    {
        if (!Enum.IsDefined(field)) return [];
        if (field is AssetQueryField.Folder or AssetQueryField.Tag)
            return [AssetQueryOperator.AnyOf, AssetQueryOperator.AllOf, AssetQueryOperator.NoneOf];
        if (field is AssetQueryField.IsUncategorized or AssetQueryField.IsUntagged or AssetQueryField.IsMissing or AssetQueryField.IsArchived)
            return [AssetQueryOperator.IsTrue, AssetQueryOperator.IsFalse];
        if (IsNumericField(field) || IsDateField(field))
            return
            [
                AssetQueryOperator.Equals, AssetQueryOperator.NotEquals,
                AssetQueryOperator.GreaterThan, AssetQueryOperator.GreaterThanOrEqual,
                AssetQueryOperator.LessThan, AssetQueryOperator.LessThanOrEqual,
                AssetQueryOperator.Between, AssetQueryOperator.Unknown, AssetQueryOperator.Known
            ];
        if (field == AssetQueryField.VisualDominantColor)
            return [AssetQueryOperator.Equals, AssetQueryOperator.NotEquals];

        var values = new List<AssetQueryOperator>
        {
            AssetQueryOperator.Contains, AssetQueryOperator.NotContains,
            AssetQueryOperator.Equals, AssetQueryOperator.NotEquals,
            AssetQueryOperator.StartsWith, AssetQueryOperator.EndsWith
        };
        if (field is AssetQueryField.FileName or AssetQueryField.Comment)
            values.Add(AssetQueryOperator.Regex);
        values.AddRange(
        [
            AssetQueryOperator.IsEmpty, AssetQueryOperator.IsNotEmpty,
            AssetQueryOperator.AnyOf, AssetQueryOperator.NoneOf,
            AssetQueryOperator.Unknown, AssetQueryOperator.Known
        ]);
        return values;
    }

    public static bool SupportsCaseSensitivity(AssetQueryField field) => field is
        AssetQueryField.FileName or AssetQueryField.Extension or AssetQueryField.Comment;

    private static bool IsOperatorSupported(AssetQueryField field, AssetQueryOperator @operator) =>
        GetSupportedOperators(field).Contains(@operator);

    private static bool IsNumericField(AssetQueryField field) => field is
        AssetQueryField.Rating or AssetQueryField.FileSize or AssetQueryField.Width or AssetQueryField.Height or
        AssetQueryField.LongEdge or AssetQueryField.ShortEdge or AssetQueryField.PixelCount or AssetQueryField.AspectRatio or
        AssetQueryField.VisualDominantHue or AssetQueryField.VisualAverageLuma or AssetQueryField.VisualAverageSaturation or
        AssetQueryField.VisualLumaSpread or AssetQueryField.VisualShadowRatio or AssetQueryField.VisualHighlightRatio or
        AssetQueryField.VisualBlackClipRatio or AssetQueryField.VisualWhiteClipRatio;

    private static bool IsIntegerField(AssetQueryField field) => field is
        AssetQueryField.Rating or AssetQueryField.FileSize or AssetQueryField.Width or AssetQueryField.Height or
        AssetQueryField.LongEdge or AssetQueryField.ShortEdge or AssetQueryField.PixelCount;

    private static bool IsIntegerValueInDomain(AssetQueryField field, decimal value) => field switch
    {
        AssetQueryField.Rating => value is >= 0 and <= 5,
        AssetQueryField.Width or AssetQueryField.Height or AssetQueryField.LongEdge or AssetQueryField.ShortEdge =>
            value is >= 0 and <= int.MaxValue,
        AssetQueryField.FileSize or AssetQueryField.PixelCount => value is >= 0 and <= long.MaxValue,
        _ => false
    };

    private static bool IsNumericValueInDomain(AssetQueryField field, double value) => field switch
    {
        AssetQueryField.AspectRatio => value > 0,
        AssetQueryField.VisualDominantHue => value is >= 0 and < 360,
        AssetQueryField.VisualAverageLuma => value is >= 0 and <= 255,
        AssetQueryField.VisualAverageSaturation or AssetQueryField.VisualLumaSpread or
        AssetQueryField.VisualShadowRatio or AssetQueryField.VisualHighlightRatio or
        AssetQueryField.VisualBlackClipRatio or AssetQueryField.VisualWhiteClipRatio => value is >= 0 and <= 1,
        _ => true
    };

    private static bool IsDateField(AssetQueryField field) => field is AssetQueryField.AddedAt or AssetQueryField.CaptureTime;

    private static string CanonicalizeSqliteNoCaseSetValue(AssetQueryField field, string value)
    {
        if (field is AssetQueryField.Folder or AssetQueryField.Tag)
            return value.StartsWith("name:", StringComparison.Ordinal)
                ? "name:" + FoldSqliteNoCaseAscii(value[5..])
                : value;
        return FoldSqliteNoCaseAscii(value);
    }

    private static string FoldSqliteNoCaseAscii(string value)
    {
        var characters = value.ToCharArray();
        var changed = false;
        for (var index = 0; index < characters.Length; index++)
        {
            if (characters[index] is < 'a' or > 'z') continue;
            characters[index] = (char)(characters[index] - ('a' - 'A'));
            changed = true;
        }
        return changed ? new string(characters) : value;
    }

    private static string NormalizeText(string? value) => (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormC);

    private static AssetQueryValidationResult Invalid(string path, string message) =>
        new(false, null, [new(path, message)], []);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}

public sealed record AssetQuerySuggestion(string Kind, string Label, string Value, string Detail = "");

public sealed record AssetQueryHistoryEntry(string Text, DateTimeOffset UsedAt);

public sealed record SmartFolderQueryDocument(
    Guid SmartFolderId,
    AssetQueryDocument Document,
    string QueryHash,
    string? LegacyRulesBackupJson,
    DateTimeOffset UpdatedAt);

public sealed record AssetBatchMetadataRequest(
    IReadOnlyList<Guid> AssetIds,
    IReadOnlyList<Guid>? AddTagIds = null,
    IReadOnlyList<Guid>? RemoveTagIds = null,
    IReadOnlyList<Guid>? AddFolderIds = null,
    IReadOnlyList<Guid>? RemoveFolderIds = null,
    int? Rating = null,
    bool ClearRating = false,
    string? Comment = null,
    bool ClearComment = false,
    bool? IsArchived = null,
    bool? IsMissing = null);

public sealed record AssetBatchMetadataPreview(
    int AssetCount,
    int ExistingTagRelationships,
    int ExistingFolderRelationships,
    bool HasMixedRatings,
    bool HasMixedComments,
    int ChangedCount,
    int ConflictOverrideCount,
    IReadOnlyList<string> ConflictOverrides,
    string CanonicalRequestFingerprint,
    string BeforeStateFingerprint,
    string PreviewFingerprint,
    IReadOnlyList<string> Warnings);

public sealed record AssetQueryPlanParameter(
    string Name,
    string ValueType,
    int CanonicalLength,
    string ValueSha256);

public sealed record AssetQueryExecutionPlan(
    string SqlTemplate,
    IReadOnlyList<AssetQueryPlanParameter> Parameters,
    IReadOnlyList<string> ExplainRows);
