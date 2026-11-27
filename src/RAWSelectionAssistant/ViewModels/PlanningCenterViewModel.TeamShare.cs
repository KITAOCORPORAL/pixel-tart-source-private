#if PLANNING_REVIEW_PILOT
namespace RAWSelectionAssistant.ViewModels;

internal sealed record PlanningTeamShareTarget(Guid ProjectId, Guid DocumentId, long EditorGeneration,
    string PlanningRoot, string ShotRoot);

internal enum PlanningTeamShareSaveResult { Saved, SaveFailed, PlanChanged }

public sealed partial class PlanningCenterViewModel
{
    internal long TeamShareEditRevision => _editRevision;

    internal PlanningTeamShareTarget? GetTeamShareTarget() =>
        !IsOverview && !IsPlanningLoading && !IsPreviewMode && ProjectId is Guid project && PlanningDocumentId is Guid document
            ? new(project, document, EditorGeneration, _planning.StorageRoot, _shots.StorageRoot) : null;

    internal bool HasTeamSharePendingChanges(PlanningTeamShareTarget target) =>
        _pendingDocuments.ContainsKey(target.ProjectId) || _pendingShots.Values.Any(s => s.ProjectId == target.ProjectId);

    internal async Task<PlanningTeamShareSaveResult> SaveForTeamShareAsync(PlanningTeamShareTarget target)
    {
        if (GetTeamShareTarget() != target) return PlanningTeamShareSaveResult.PlanChanged;
        try
        {
            if (!await FlushAsync()) return PlanningTeamShareSaveResult.SaveFailed;
            if (GetTeamShareTarget() != target) return PlanningTeamShareSaveResult.PlanChanged;

            // Legacy projects can have shots yet no saved plan. Persist through the
            // ordinary recovery journal before building any publication.
            if (_state?.Document is null && !File.Exists(Path.Combine(target.PlanningRoot, $"{target.ProjectId:N}.planning.json")))
            {
                QueueDocumentSave();
                if (!await FlushAsync()) return PlanningTeamShareSaveResult.SaveFailed;
                if (GetTeamShareTarget() != target) return PlanningTeamShareSaveResult.PlanChanged;
            }

            return PlanningTeamShareSaveResult.Saved;
        }
        catch (Exception error) when (IsRecoverableProjectError(error) || error is InvalidOperationException)
        {
            if (GetTeamShareTarget() != target) return PlanningTeamShareSaveResult.PlanChanged;
            SaveStatus = "保存失败，当前编辑已保留，请重试。";
            return PlanningTeamShareSaveResult.SaveFailed;
        }
    }
}
#endif
