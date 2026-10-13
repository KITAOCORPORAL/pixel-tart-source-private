using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class PlanningCenterViewModel
{
    public Guid? PlanningDocumentId => _document.Package?.DocumentId;
    public IReadOnlyList<PlanningShotColumn> ShotColumns => _document.ShotColumns.Where(c => !c.Archived).ToArray();
    public IReadOnlyList<PlanningShotColumn> ArchivedShotColumns => _document.ShotColumns.Where(c => c.Archived).ToArray();
    public ProjectShot ShotDraft(Guid id) => _pendingShots.GetValueOrDefault(id) ?? Shots.First(s => s.ShotId == id);
    public void StageShot(ProjectShot shot)
    {
        if (ProjectId != shot.ProjectId || shot.ShotId == Guid.Empty) throw new ArgumentException("镜头不属于当前策划。");
        _pendingShots[shot.ShotId] = shot;
        QueueDocumentSave();
    }
    public async Task<bool> SelectContentSafelyAsync(string page)
    {
        page = page == "自定义" ? "文档内容" : page;
        if (!ContentPages.Contains(page) || !await FlushAsync()) return false;
        ContentPage = page; return true;
    }
    public async Task<bool> AddShotColumnAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80) return false;
        if (!await FlushAsync()) return false;
        UpdateDocument(_document with { ShotColumns = _document.ShotColumns.Append(new PlanningShotColumn(Guid.NewGuid(), name.Trim())).ToArray() });
        var saved = await FlushAsync(); OnPropertyChanged(nameof(ShotColumns)); return saved;
    }
    public async Task<bool> SetShotColumnArchivedAsync(Guid id, bool archived)
    {
        if (!_document.ShotColumns.Any(c => c.Id == id) || !await FlushAsync()) return false;
        // Removing a column hides its definition; existing values remain recoverable.
        UpdateDocument(_document with { ShotColumns = _document.ShotColumns.Select(c => c.Id == id ? c with { Archived = archived } : c).ToArray() });
        var saved = await FlushAsync(); OnPropertyChanged(nameof(ShotColumns)); return saved;
    }
}
