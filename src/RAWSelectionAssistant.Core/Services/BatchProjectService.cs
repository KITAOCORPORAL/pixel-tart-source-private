using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class BatchProjectService(IFeatureGateService featureGateService)
{
    public async Task<BatchProjectSummary> RunSequentialAsync(
        IEnumerable<PhotoProjectRecord> projects,
        Func<PhotoProjectRecord, CancellationToken, Task<BatchProjectOutcome>> processor,
        CancellationToken cancellationToken = default)
    {
        var access = featureGateService.Check(LicensedFeature.BatchProjects);
        if (!access.Allowed) return new BatchProjectSummary(false, [], access.Message);

        var outcomes = new List<BatchProjectOutcome>();
        foreach (var project in projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                outcomes.Add(await processor(project, cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                outcomes.Add(new BatchProjectOutcome(project.Id, project.Name, false, ex.Message));
            }
        }

        return new BatchProjectSummary(true, outcomes, $"批量处理完成：成功 {outcomes.Count(x => x.Succeeded)}，失败 {outcomes.Count(x => !x.Succeeded)}。");
    }
}
