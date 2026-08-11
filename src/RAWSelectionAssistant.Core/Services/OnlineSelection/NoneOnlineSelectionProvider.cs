using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.OnlineSelection;

public sealed class NoneOnlineSelectionProvider : IOnlineSelectionProvider
{
    public OnlineSelectionProviderKind Kind => OnlineSelectionProviderKind.None;
    public bool IsConfigured => false;
    public string DisplayName => "未配置";

    public Task<OnlineSelectionProviderResult<SelectionProject>> CreateProjectAsync(SelectionProject project, CancellationToken cancellationToken = default) =>
        NotConfigured<SelectionProject>(cancellationToken);

    public Task<OnlineSelectionProviderResult<SelectionProject>> UpdateProjectAsync(SelectionProject project, CancellationToken cancellationToken = default) =>
        NotConfigured<SelectionProject>(cancellationToken);

    public Task<OnlineSelectionProviderResult<SelectionAsset>> UploadAssetAsync(Guid projectId, SelectionAsset asset, Stream content, IProgress<SelectionUploadProgress>? progress = null, CancellationToken cancellationToken = default) =>
        NotConfigured<SelectionAsset>(cancellationToken);

    public Task<OnlineSelectionProviderResult<SelectionPublish>> PublishProjectAsync(Guid projectId, SelectionPublish publish, CancellationToken cancellationToken = default) =>
        NotConfigured<SelectionPublish>(cancellationToken);

    public Task<OnlineSelectionProviderResult<SelectionProject>> GetProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        NotConfigured<SelectionProject>(cancellationToken);

    public Task<OnlineSelectionProviderResult<SelectionProgress>> GetSelectionProgressAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        NotConfigured<SelectionProgress>(cancellationToken);

    public Task<OnlineSelectionProviderResult<SelectionFinalResult>> GetFinalSelectionAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        NotConfigured<SelectionFinalResult>(cancellationToken);

    public Task<OnlineSelectionProviderResult<bool>> UnpublishAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        NotConfigured<bool>(cancellationToken);

    public Task<OnlineSelectionProviderResult<bool>> DeleteCloudAssetAsync(Guid projectId, Guid assetId, CancellationToken cancellationToken = default) =>
        NotConfigured<bool>(cancellationToken);

    private static Task<OnlineSelectionProviderResult<T>> NotConfigured<T>(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OnlineSelectionProviderResult<T>.Failed(
            OnlineSelectionErrorCodes.ProviderNotConfigured,
            "在线选片服务尚未配置。"));
    }
}

public static class OnlineSelectionProviderFactory
{
    public static IOnlineSelectionProvider CreateDefault() => new NoneOnlineSelectionProvider();
}
