using System.Collections.Concurrent;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.OnlineSelection;

namespace RAWSelectionAssistant.Tests;

internal sealed class FakeOnlineSelectionProvider : IOnlineSelectionProvider
{
    private readonly ConcurrentDictionary<Guid, SelectionProject> _projects = new();
    private readonly ConcurrentDictionary<Guid, SelectionAsset> _assets = new();
    private readonly ConcurrentDictionary<Guid, SelectionPublish> _publishes = new();
    private readonly ConcurrentDictionary<Guid, SelectionFinalResult> _finalResults = new();

    public OnlineSelectionProviderKind Kind => OnlineSelectionProviderKind.None;
    public bool IsConfigured => true;
    public string DisplayName => "专项测试 Provider";
    public Func<SelectionAsset, bool>? FailUploadWhen { get; set; }

    public Task<OnlineSelectionProviderResult<SelectionProject>> CreateProjectAsync(SelectionProject project, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); _projects[project.Id] = project; return Completed(project);
    }

    public Task<OnlineSelectionProviderResult<SelectionProject>> UpdateProjectAsync(SelectionProject project, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); _projects[project.Id] = project; return Completed(project);
    }

    public async Task<OnlineSelectionProviderResult<SelectionAsset>> UploadAssetAsync(Guid projectId, SelectionAsset asset, Stream content, IProgress<SelectionUploadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (FailUploadWhen?.Invoke(asset) == true) return OnlineSelectionProviderResult<SelectionAsset>.Failed(OnlineSelectionErrorCodes.UploadFailed, "测试上传失败。");
        var total = content.CanSeek ? content.Length : 0; var buffer = new byte[16 * 1024]; long sent = 0;
        while (true) { var read = await content.ReadAsync(buffer, cancellationToken); if (read == 0) break; sent += read; progress?.Report(new(asset.Id, sent, total)); }
        var ready = asset with { ProjectId = projectId, Status = SelectionAssetStatus.Ready, CloudAssetId = "test-" + asset.Id.ToString("N"), UpdatedAtUtc = DateTimeOffset.UtcNow };
        _assets[asset.Id] = ready; return OnlineSelectionProviderResult<SelectionAsset>.Completed(ready);
    }

    public Task<OnlineSelectionProviderResult<SelectionPublish>> PublishProjectAsync(Guid projectId, SelectionPublish publish, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); _publishes[projectId] = publish; return Completed(publish);
    }

    public Task<OnlineSelectionProviderResult<SelectionProject>> GetProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _projects.TryGetValue(projectId, out var project) ? Completed(project) : Task.FromResult(OnlineSelectionProviderResult<SelectionProject>.Failed(OnlineSelectionErrorCodes.InvalidProject, "测试项目不存在。"));
    }

    public Task<OnlineSelectionProviderResult<SelectionProgress>> GetSelectionProgressAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); var assets = _assets.Values.Where(asset => asset.ProjectId == projectId).ToArray(); var final = _finalResults.GetValueOrDefault(projectId);
        return Completed(new SelectionProgress(projectId, assets.Length, assets.Count(asset => asset.Status == SelectionAssetStatus.Ready), final?.Items.Count(item => item.Selected) ?? 0, final?.Items.Count(item => item.Favorite) ?? 0, final?.Items.Count(item => !string.IsNullOrWhiteSpace(item.CustomerNote)) ?? 0, final?.Items.Count(item => item.ExtraSelected) ?? 0, final?.ConfirmedAtUtc));
    }

    public Task<OnlineSelectionProviderResult<SelectionFinalResult>> GetFinalSelectionAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _finalResults.TryGetValue(projectId, out var result) ? Completed(result) : Task.FromResult(OnlineSelectionProviderResult<SelectionFinalResult>.Failed(OnlineSelectionErrorCodes.FinalSelectionUnavailable, "客户尚未确认选片结果。"));
    }

    public Task<OnlineSelectionProviderResult<bool>> UnpublishAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); _publishes.TryRemove(projectId, out _); return Completed(true);
    }

    public Task<OnlineSelectionProviderResult<bool>> DeleteCloudAssetAsync(Guid projectId, Guid assetId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); _assets.TryRemove(assetId, out _); return Completed(true);
    }

    public void SetFinalResult(SelectionFinalResult result) => _finalResults[result.SelectionProjectId] = result;
    private static Task<OnlineSelectionProviderResult<T>> Completed<T>(T value) => Task.FromResult(OnlineSelectionProviderResult<T>.Completed(value));
}
