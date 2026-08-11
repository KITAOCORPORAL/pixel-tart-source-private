using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.OnlineSelection;

public static class OnlineSelectionErrorCodes
{
    public const string ProviderNotConfigured = nameof(ProviderNotConfigured);
    public const string InvalidProject = nameof(InvalidProject);
    public const string InvalidRule = nameof(InvalidRule);
    public const string NoReadyAssets = nameof(NoReadyAssets);
    public const string UploadFailed = nameof(UploadFailed);
    public const string FinalSelectionUnavailable = nameof(FinalSelectionUnavailable);
    public const string ProxyGenerationFailed = nameof(ProxyGenerationFailed);
}

public sealed record OnlineSelectionProviderResult<T>(
    bool Success,
    T? Value,
    string? ErrorCode,
    string Message)
{
    public static OnlineSelectionProviderResult<T> Completed(T value, string message = "操作已完成。") =>
        new(true, value, null, message);

    public static OnlineSelectionProviderResult<T> Failed(string errorCode, string message) =>
        new(false, default, errorCode, message);
}

public sealed record SelectionUploadProgress(Guid AssetId, long BytesSent, long TotalBytes)
{
    public double Percent => TotalBytes <= 0 ? 0 : Math.Clamp(BytesSent * 100d / TotalBytes, 0, 100);
}

public interface IOnlineSelectionProvider
{
    OnlineSelectionProviderKind Kind { get; }
    bool IsConfigured { get; }
    string DisplayName { get; }

    Task<OnlineSelectionProviderResult<SelectionProject>> CreateProjectAsync(
        SelectionProject project,
        CancellationToken cancellationToken = default);

    Task<OnlineSelectionProviderResult<SelectionProject>> UpdateProjectAsync(
        SelectionProject project,
        CancellationToken cancellationToken = default);

    Task<OnlineSelectionProviderResult<SelectionAsset>> UploadAssetAsync(
        Guid projectId,
        SelectionAsset asset,
        Stream content,
        IProgress<SelectionUploadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<OnlineSelectionProviderResult<SelectionPublish>> PublishProjectAsync(
        Guid projectId,
        SelectionPublish publish,
        CancellationToken cancellationToken = default);

    Task<OnlineSelectionProviderResult<SelectionProject>> GetProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<OnlineSelectionProviderResult<SelectionProgress>> GetSelectionProgressAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<OnlineSelectionProviderResult<SelectionFinalResult>> GetFinalSelectionAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<OnlineSelectionProviderResult<bool>> UnpublishAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<OnlineSelectionProviderResult<bool>> DeleteCloudAssetAsync(
        Guid projectId,
        Guid assetId,
        CancellationToken cancellationToken = default);
}

public interface ISelectionProxyRenderer
{
    string Name { get; }

    Task RenderJpegAsync(
        string sourcePath,
        Stream destination,
        SelectionProxyOptions options,
        CancellationToken cancellationToken = default);
}

public interface ISelectionWorkspaceStore
{
    Task<SelectionWorkspaceSnapshot> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(SelectionWorkspaceSnapshot snapshot, CancellationToken cancellationToken = default);
}

public sealed record SelectionValidationResult(bool IsValid, string? ErrorCode, string Message)
{
    public static SelectionValidationResult Valid(string message = "可以继续。") => new(true, null, message);
    public static SelectionValidationResult Invalid(string errorCode, string message) => new(false, errorCode, message);
}
