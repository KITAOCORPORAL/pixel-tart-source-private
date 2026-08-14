using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.OnlineSelection;

/// <summary>
/// Deterministic, local-only client experience used by the Desktop preview.
/// It deliberately does not implement IOnlineSelectionProvider and therefore
/// cannot be mistaken for a production cloud provider.
/// </summary>
public sealed class SelectionClientChoiceMock
{
    private readonly Dictionary<Guid, SelectionChoice> _choices = [];
    private readonly Dictionary<(Guid ProjectId, Guid AssetId), SelectionComment> _comments = [];
    private readonly Dictionary<Guid, int> _versions = [];
    private readonly Dictionary<Guid, FinalSelectionSnapshot> _snapshots = [];

    public IReadOnlyCollection<SelectionChoice> Choices => _choices.Values.ToArray();
    public IReadOnlyCollection<SelectionComment> Comments => _comments.Values.ToArray();

    public SelectionChoice SetChoice(
        Guid projectId,
        Guid assetId,
        bool? selected = null,
        bool? favorite = null,
        bool? extraSelected = null,
        DateTimeOffset? nowUtc = null)
    {
        if (projectId == Guid.Empty || assetId == Guid.Empty)
            throw new ArgumentException("项目和照片标识不能为空。");

        var current = _choices.GetValueOrDefault(assetId) ??
            new SelectionChoice(projectId, assetId, false, false, false, nowUtc ?? DateTimeOffset.UtcNow);
        var updated = current with
        {
            ProjectId = projectId,
            Selected = selected ?? current.Selected,
            Favorite = favorite ?? current.Favorite,
            ExtraSelected = extraSelected ?? current.ExtraSelected,
            UpdatedAtUtc = nowUtc ?? DateTimeOffset.UtcNow
        };
        _choices[assetId] = updated;
        return updated;
    }

    public SelectionComment SetComment(
        Guid projectId,
        Guid assetId,
        string? note,
        DateTimeOffset? nowUtc = null)
    {
        if (projectId == Guid.Empty || assetId == Guid.Empty)
            throw new ArgumentException("项目和照片标识不能为空。");

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var key = (projectId, assetId);
        var existing = _comments.GetValueOrDefault(key);
        var comment = existing is null
            ? new SelectionComment(Guid.NewGuid(), projectId, assetId, note?.Trim() ?? string.Empty, now, now)
            : existing with { CustomerNote = note?.Trim() ?? string.Empty, UpdatedAtUtc = now };
        _comments[key] = comment;
        return comment;
    }

    public FinalSelectionSnapshot Confirm(
        SelectionProject project,
        IReadOnlyList<SelectionAsset> assets,
        SelectionRule rule,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(rule);
        var validation = SelectionProjectValidator.ValidateRule(rule);
        if (!validation.IsValid) throw new InvalidOperationException(validation.Message);

        var selectedCount = assets.Count(asset => _choices.GetValueOrDefault(asset.Id)?.Selected == true);
        if (selectedCount < rule.MinimumCount || (!rule.AllowExtraSelections && selectedCount > rule.MaximumCount))
            throw new InvalidOperationException("客户选片数量不符合当前规则。");

        var version = _versions.TryGetValue(project.Id, out var previous) ? previous + 1 : 1;
        _versions[project.Id] = version;
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var items = assets.Select(asset =>
        {
            var choice = _choices.GetValueOrDefault(asset.Id);
            var comment = _comments.GetValueOrDefault((project.Id, asset.Id));
            return new SelectionFinalItem(
                project.Id,
                asset.Id,
                asset.OriginalFileName,
                choice?.Selected == true,
                choice?.Favorite == true,
                string.IsNullOrWhiteSpace(comment?.CustomerNote) ? null : comment.CustomerNote,
                choice?.ExtraSelected == true)
            {
                SourceAssetId = asset.SourceAssetId
            };
        }).ToArray();
        var snapshot = new FinalSelectionSnapshot(project.Id, version, items, now, true);
        _snapshots[project.Id] = snapshot;
        return snapshot;
    }

    public bool TryGetSnapshot(Guid projectId, out FinalSelectionSnapshot snapshot) =>
        _snapshots.TryGetValue(projectId, out snapshot!);

    public SelectionConfirmationState GetState(Guid projectId) =>
        _snapshots.TryGetValue(projectId, out var snapshot)
            ? new(projectId, snapshot.SelectionVersion, true, snapshot.ConfirmedAtUtc, snapshot.IsLocked)
            : new(projectId, _versions.GetValueOrDefault(projectId), false, null, false);

    public SelectionConfirmationState Reopen(Guid projectId)
    {
        if (_snapshots.TryGetValue(projectId, out var snapshot))
            _snapshots[projectId] = snapshot with { IsLocked = false };
        return GetState(projectId);
    }

    public SelectionWorkspaceSnapshot ApplyToSnapshot(
        SelectionWorkspaceSnapshot workspace,
        Guid projectId,
        FinalSelectionSnapshot? snapshot = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var selectedSnapshot = snapshot ?? (_snapshots.TryGetValue(projectId, out var existing) ? existing : null);
        var finalResults = selectedSnapshot is null
            ? workspace.FinalResults
            : workspace.FinalResults.Where(item => item.SelectionProjectId != projectId)
                .Append(selectedSnapshot.ToFinalResult()).ToArray();
        return workspace with
        {
            Choices = workspace.Choices.Where(item => item.ProjectId != projectId)
                .Concat(_choices.Values.Where(item => item.ProjectId == projectId)).ToArray(),
            Comments = workspace.Comments.Where(item => item.ProjectId != projectId)
                .Concat(_comments.Values.Where(item => item.ProjectId == projectId)).ToArray(),
            FinalResults = finalResults.ToArray()
        };
    }

    public void LoadFromSnapshot(SelectionWorkspaceSnapshot workspace, Guid projectId)
    {
        foreach (var choice in workspace.Choices.Where(item => item.ProjectId == projectId)) _choices[choice.AssetId] = choice;
        foreach (var comment in workspace.Comments.Where(item => item.ProjectId == projectId)) _comments[(comment.ProjectId, comment.AssetId)] = comment;
        var final = workspace.FinalResults.FirstOrDefault(item => item.SelectionProjectId == projectId);
        if (final is not null)
        {
            _versions[projectId] = final.SelectionVersion;
            _snapshots[projectId] = final.ToSnapshot();
        }
    }
}
