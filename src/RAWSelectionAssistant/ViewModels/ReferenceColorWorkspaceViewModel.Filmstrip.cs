using System.Collections.ObjectModel;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed record StudioFilterChoice(string Id, string Label);

public sealed partial class ReferenceColorWorkspaceViewModel
{
    private string _filterScope = "All";
    private int _minimumRating;
    private string _colorLabelFilter = "All";
    private bool _refreshingFilmstrip;
    private Guid? _selectionAnchorId;
    public ObservableCollection<ReferenceTargetItem> VisibleTargets { get; } = [];
    public IReadOnlyList<StudioFilterChoice> FilterScopes { get; } = [new("All", "全部照片"), new("Selected", "已选照片"), new("Raw", "RAW"), new("Jpeg", "JPEG"), new("Tiff", "TIFF"), new("Png", "PNG")];
    public IReadOnlyList<StudioFilterChoice> ColorLabelFilters { get; } = [new("All", "所有色标"), new("None", "无色标"), new("红", "红"), new("橙", "橙"), new("黄", "黄"), new("绿", "绿"), new("蓝", "蓝"), new("紫", "紫")];
    public string FilterScope { get => _filterScope; set { if (SetProperty(ref _filterScope, FilterScopes.Any(x => x.Id == value) ? value : "All")) RefreshFilmstrip(); } }
    public int MinimumRating { get => _minimumRating; set { if (SetProperty(ref _minimumRating, Math.Clamp(value, 0, 5))) RefreshFilmstrip(); } }
    public string ColorLabelFilter { get => _colorLabelFilter; set { if (SetProperty(ref _colorLabelFilter, ColorLabelFilters.Any(x => x.Id == value) ? value : "All")) RefreshFilmstrip(); } }
    public IEnumerable<ReferenceTargetItem> AllSelectedTargets => Targets.Where(item => item.IsSelected);
    public int HiddenSelectedTargetCount => AllSelectedTargets.Count(item => !VisibleTargets.Contains(item));
    public int VisibleTargetCount => VisibleTargets.Count;
    public string FilmstripSummary => $"{VisibleTargets.Count} / {Targets.Count} 张 · 已选 {SelectedTargetCount}" + (HiddenSelectedTargetCount > 0 ? $" · 隐藏已选 {HiddenSelectedTargetCount}（不参与本次操作）" : "");
    public IReadOnlyList<ReferenceTargetItem> SyncDestinationTargets => SelectedTargets.Where(item => !ReferenceEquals(item, ActiveTarget)).ToArray();
    public string SyncSummary => ActiveTarget is null ? "先选择当前源照片" : $"源：{ActiveTarget.FileName} → {SyncDestinationTargets.Count} 张可见所选照片";
    public RelayCommand SetSelectedRatingCommand { get; private set; } = null!;
    public RelayCommand SetSelectedColorLabelCommand { get; private set; } = null!;
    public RelayCommand ClearFilmstripFiltersCommand { get; private set; } = null!;

    private void InitializeFilmstrip()
    {
        SetSelectedRatingCommand = new RelayCommand(value => { if (int.TryParse(value?.ToString(), out var rating)) foreach (var target in SelectedTargets.ToArray()) target.Rating = rating; }, _ => SelectedTargets.Any());
        SetSelectedColorLabelCommand = new RelayCommand(value => { foreach (var target in SelectedTargets.ToArray()) target.ColorLabel = value?.ToString(); }, _ => SelectedTargets.Any());
        ClearFilmstripFiltersCommand = new RelayCommand(_ => { _filterScope = "All"; _minimumRating = 0; _colorLabelFilter = "All"; OnPropertyChanged(nameof(FilterScope)); OnPropertyChanged(nameof(MinimumRating)); OnPropertyChanged(nameof(ColorLabelFilter)); RefreshFilmstrip(); });
    }
    private bool MatchesFilmstrip(ReferenceTargetItem target)
    {
        if (target.Rating < MinimumRating) return false;
        if (ColorLabelFilter == "None" && !string.IsNullOrWhiteSpace(target.ColorLabel)) return false;
        if (ColorLabelFilter is not "All" and not "None" && target.ColorLabel != ColorLabelFilter) return false;
        var extension = System.IO.Path.GetExtension(target.Path).ToLowerInvariant();
        return FilterScope switch { "Selected" => target.IsSelected, "Raw" => RawMatchTiff16ProductPipeline.IsRaw(target.Path), "Jpeg" => extension is ".jpg" or ".jpeg", "Tiff" => extension is ".tif" or ".tiff", "Png" => extension == ".png", _ => true };
    }
    private void RefreshFilmstrip()
    {
        if (_refreshingFilmstrip) return;
        _refreshingFilmstrip = true;
        try
        {
            var desired = Targets.Where(MatchesFilmstrip).ToArray();
            for (var i = VisibleTargets.Count - 1; i >= 0; i--) if (!desired.Contains(VisibleTargets[i])) VisibleTargets.RemoveAt(i);
            for (var i = 0; i < desired.Length; i++)
            {
                var existing = VisibleTargets.IndexOf(desired[i]);
                if (existing < 0) VisibleTargets.Insert(i, desired[i]);
                else if (existing != i) VisibleTargets.Move(existing, i);
            }
            RefreshSyncAvailability();
        }
        finally { _refreshingFilmstrip = false; }
    }
    public int SelectVisibleFilmstripTarget(int index, bool shift = false, bool control = false)
    {
        var items = VisibleTargets.ToArray();
        if ((uint)index >= (uint)items.Length) return -1;
        var anchor = Array.FindIndex(items, x => x.Id == _selectionAnchorId);
        if (shift && anchor >= 0)
        {
            if (!control) foreach (var item in items) item.IsSelected = false;
            for (var i = Math.Min(anchor, index); i <= Math.Max(anchor, index); i++) items[i].IsSelected = true;
        }
        else
        {
            if (control) items[index].IsSelected = !items[index].IsSelected;
            else { foreach (var item in items) item.IsSelected = false; items[index].IsSelected = true; }
            _selectionAnchorId = items[index].Id;
        }
        RefreshFilmstrip();
        return VisibleTargets.ToList().FindIndex(item => item.Id == _selectionAnchorId);
    }
    public void SelectAllVisible() { foreach (var item in VisibleTargets.ToArray()) item.IsSelected = true; RefreshFilmstrip(); }
    public void SelectOnlyVisibleTarget(ReferenceTargetItem target)
    {
        foreach (var item in VisibleTargets.ToArray()) item.IsSelected = ReferenceEquals(item, target);
        _selectionAnchorId = target.Id; RefreshFilmstrip();
    }
    public async Task MoveVisibleTargetAsync(int delta, bool shift = false, bool control = false)
    {
        if (VisibleTargets.Count == 0) return;
        var index = ActiveTarget is null ? -1 : VisibleTargets.IndexOf(ActiveTarget);
        var next = Math.Clamp(index < 0 ? 0 : index + delta, 0, VisibleTargets.Count - 1);
        var target = VisibleTargets[next];
        if (shift || !control) SelectVisibleFilmstripTarget(next, shift, control);
        await ActivateTargetAsync(target);
    }
}
