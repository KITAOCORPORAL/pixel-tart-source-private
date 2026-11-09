using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.OnlineSelection;

public sealed record SelectionHttpPreparation(SelectionProject Project, IReadOnlyList<SelectionAsset> Assets);

public sealed class SelectionHttpWorkflow(HttpOnlineSelectionProvider provider)
{
    public async Task<SelectionHttpPreparation> PrepareAsync(SelectionProject project, SelectionRule rule,
        IReadOnlyList<SelectionAsset> assets, Func<SelectionHttpPreparation, Task> persist,
        CancellationToken cancellationToken = default)
    {
        var created = await provider.CreateProjectAsync(project, cancellationToken).ConfigureAwait(true);
        if (!created.Success || created.Value is null) throw new IOException(created.Message);
        project = created.Value;
        await persist(new(project, assets)).ConfigureAwait(true);
        var rules = await provider.UpdateRuleAsync(rule, cancellationToken).ConfigureAwait(true);
        if (!rules.Success) throw new IOException(rules.Message);
        var current = assets.ToArray();
        var queue = new SelectionUploadQueue(provider);
        foreach (var asset in assets.Where(item => string.IsNullOrWhiteSpace(item.CloudAssetId))) queue.Enqueue(asset);
        await queue.RunAsync(cancellationToken).ConfigureAwait(true);
        foreach (var item in queue.Items)
        {
            current[Array.FindIndex(current, asset => asset.Id == item.AssetId)] = item.Asset;
            await persist(new(project, current.ToArray())).ConfigureAwait(true);
        }
        if (current.Any(item => item.Status != SelectionAssetStatus.Ready || string.IsNullOrWhiteSpace(item.CloudAssetId)))
            throw new IOException("代理上传尚未全部完成，请重试后再发布。");
        return new(project, current);
    }

    public static void ValidateFinal(SelectionProject project, IReadOnlyList<SelectionAsset> assets, SelectionFinalResult result)
    {
        if (result.SelectionProjectId != project.Id || !result.IsLocked || result.Items.Any(item => item.SelectionProjectId != project.Id)
            || result.Items.Select(item => item.ImageId).Distinct().Count() != result.Items.Count)
            throw new InvalidDataException("客户确认结果的项目、锁定状态或素材身份无效。");
        var known = assets.ToDictionary(item => item.Id);
        if (result.Items.Count != assets.Count || result.Items.Any(item => !known.TryGetValue(item.ImageId, out var asset)
            || asset.ProjectId != project.Id || asset.SourceAssetId != item.SourceAssetId
            || !string.Equals(asset.OriginalFileName, item.OriginalFileName, StringComparison.Ordinal)))
            throw new InvalidDataException("客户结果包含其他项目或未知素材，未写入归片项目。");
    }
}
