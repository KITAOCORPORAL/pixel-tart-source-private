using System.ComponentModel;
using System.Globalization;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.ViewModels;

/// <summary>Row transaction for the simple editor. EndEdit emits a typed shot draft;
/// CancelEdit restores the row without publishing partially edited cells.</summary>
public sealed class PlanningShotEditRow : INotifyPropertyChanged, IEditableObject
{
    private ProjectShot _value;
    private Dictionary<string,string>? _before;
    private readonly Dictionary<string,string> _cells;
    private readonly Action<ProjectShot> _commit;
    public Guid ShotId => _value.ShotId;
    public event PropertyChangedEventHandler? PropertyChanged;
    public static IReadOnlyList<string> StatusLabels { get; } = ["待拍", "当前", "已拍", "跳过"];
    public PlanningShotEditRow(ProjectShot value, Action<ProjectShot> commit)
    {
        _value = value; _commit = commit;
        _cells = new(value.CustomValues, StringComparer.Ordinal)
        {
            ["Order"] = (value.Order + 1).ToString("00"), ["Name"] = value.Name,
            ["Scene"] = value.Scene ?? "", ["ShotSize"] = value.ShotSize, ["CameraAndLens"] = value.CameraAndLens,
            ["Action"] = value.Action, ["Remarks"] = value.Remarks, ["Props"] = value.Props, ["Lighting"] = value.Lighting,
            ["EstimatedMinutes"] = value.CustomValues.GetValueOrDefault("draft:EstimatedMinutes", value.EstimatedMinutes.ToString(CultureInfo.InvariantCulture)),
            ["Status"] = StatusLabels[(int)value.Status]
        };
    }
    public string this[string key]
    {
        get => _cells.GetValueOrDefault(key, "");
        set { _cells[key] = value; PropertyChanged?.Invoke(this, new("Item[]")); }
    }
    public void BeginEdit() => _before ??= new(_cells, StringComparer.Ordinal);
    public void CancelEdit()
    {
        if (_before is null) return;
        _cells.Clear(); foreach (var pair in _before) _cells[pair.Key] = pair.Value;
        _before = null; PropertyChanged?.Invoke(this, new("Item[]"));
    }
    public void EndEdit()
    {
        if (_before is null) return;
        // Preserve invalid input as a draft; validation at the save gate blocks navigation.
        var minutes = int.TryParse(this["EstimatedMinutes"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : -1;
        var custom = new Dictionary<string,string>(_value.CustomValues, StringComparer.Ordinal);
        if (minutes is < 0 or > 1440) custom["draft:EstimatedMinutes"] = this["EstimatedMinutes"];
        else custom.Remove("draft:EstimatedMinutes");
        foreach (var pair in _cells.Where(p => p.Key.StartsWith("custom:", StringComparison.Ordinal))) custom[pair.Key] = pair.Value;
        _value = _value with { Name = this["Name"], Scene = this["Scene"], ShotSize = this["ShotSize"], CameraAndLens = this["CameraAndLens"],
            Action = this["Action"], Remarks = this["Remarks"], Props = this["Props"], Lighting = this["Lighting"], PlanningFieldsVersion = 1,
            EstimatedMinutes = minutes, Status = (ProjectShotStatus)Math.Max(0, StatusLabels.ToList().IndexOf(this["Status"])),
            CustomValues = custom, UpdatedAt = DateTimeOffset.UtcNow };
        _before = null; _commit(_value);
    }
}
