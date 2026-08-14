namespace PixelTart.SelectionApi.Contracts;

public sealed record CreateSelectionProjectRequest(string Name, string ClientDisplayName, int TargetCount, DateTimeOffset? DeadlineUtc);
public sealed record SelectionProjectResponse(Guid Id, string PublicId, string Name, string Status, int TargetCount, DateTimeOffset? DeadlineUtc)
{
    public string? ClientDisplayName { get; init; }
    public int SelectionVersion { get; init; }
}
public sealed record CreateAssetUploadRequest(Guid ImageId, string OriginalFileName, long ContentLength, string ContentType)
{
    public Guid? SourceAssetId { get; init; }
}
public sealed record AssetUploadSessionResponse(Guid ImageId, Uri SignedUploadUrl, DateTimeOffset ExpiresAtUtc)
{
    public Guid? SourceAssetId { get; init; }
    public string ObjectKey { get; init; } = string.Empty;
}
public sealed record CompleteAssetUploadRequest(Guid ImageId, long ContentLength)
{
    public string Sha256 { get; init; } = string.Empty;
}
public sealed record SelectionChoiceRequest(bool Selected, bool Favorite)
{
    public bool ExtraSelected { get; init; }
    public int ExpectedVersion { get; init; }
}
public sealed record SelectionCommentRequest(string CustomerNote)
{
    public int ExpectedVersion { get; init; }
}
public sealed record ConfirmSelectionRequest(bool Confirmed, string ConfirmationNonce)
{
    public int ExpectedVersion { get; init; }
}
public sealed record FinalSelectionItemResponse(Guid SelectionProjectId, Guid ImageId, string OriginalFileName, bool Selected, bool Favorite, string? CustomerNote, bool ExtraSelected)
{
    public Guid? SourceAssetId { get; init; }
}
public sealed record FinalSelectionSnapshotResponse(Guid ProjectId, int SelectionVersion, IReadOnlyList<FinalSelectionItemResponse> Items, DateTimeOffset ConfirmedAtUtc, bool IsLocked);
public sealed record SelectionProgressResponse(Guid ProjectId, int Total, int Ready, int Selected, int Favorites, int Comments, DateTimeOffset? LastActivityUtc);
public sealed record SelectionAssetPageResponse(IReadOnlyList<SelectionAssetResponse> Items, string? NextCursor, int Limit);
public sealed record SelectionAssetResponse(Guid SelectionAssetId, Guid? SourceAssetId, string OriginalFileName, string? ThumbUrl, string? PreviewUrl, string Status, int SortOrder);
public sealed record ApiProblem(string Code, string Message, string TraceId);

public static class SelectionApiRouteNames
{
    public const string Projects = "/v1/selection-projects";
    public const string ClientProjects = "/v1/client/selection";
    public const string ProjectAssets = Projects + "/{projectId}/assets";
    public const string ProjectAssetComplete = ProjectAssets + "/{assetId}/complete";
    public const string ProjectAssetCloudCopy = ProjectAssets + "/{assetId}/cloud-copy";
    public const string ProjectPublish = Projects + "/{projectId}/publish";
    public const string ProjectUnpublish = Projects + "/{projectId}/unpublish";
    public const string ProjectProgress = Projects + "/{projectId}/progress";
    public const string ProjectFinalSelection = Projects + "/{projectId}/final-selection";
    public const string ClientAssets = ClientProjects + "/{publicId}/assets";
    public const string ClientChoices = ClientProjects + "/{publicId}/choices/{assetId}";
    public const string ClientFavorites = ClientProjects + "/{publicId}/favorites/{assetId}";
    public const string ClientComments = ClientProjects + "/{publicId}/comments/{assetId}";
    public const string ClientConfirm = ClientProjects + "/{publicId}/confirm";
}
