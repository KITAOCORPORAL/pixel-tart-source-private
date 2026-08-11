namespace PixelTart.SelectionApi.Contracts;

public sealed record CreateSelectionProjectRequest(string Name, string ClientDisplayName, int TargetCount, DateTimeOffset? DeadlineUtc);
public sealed record SelectionProjectResponse(Guid Id, string PublicId, string Name, string Status, int TargetCount, DateTimeOffset? DeadlineUtc);
public sealed record CreateAssetUploadRequest(Guid ImageId, string OriginalFileName, long ContentLength, string ContentType);
public sealed record AssetUploadSessionResponse(Guid ImageId, Uri SignedUploadUrl, DateTimeOffset ExpiresAtUtc);
public sealed record CompleteAssetUploadRequest(Guid ImageId, long ContentLength);
public sealed record SelectionChoiceRequest(bool Selected, bool Favorite);
public sealed record SelectionCommentRequest(string CustomerNote);
public sealed record ConfirmSelectionRequest(bool Confirmed, string ConfirmationNonce);
public sealed record FinalSelectionItemResponse(Guid SelectionProjectId, Guid ImageId, string OriginalFileName, bool Selected, bool Favorite, string? CustomerNote, bool ExtraSelected);
public sealed record SelectionProgressResponse(Guid ProjectId, int Total, int Ready, int Selected, int Favorites, int Comments, DateTimeOffset? LastActivityUtc);
public sealed record ApiProblem(string Code, string Message, string TraceId);

public static class SelectionApiRouteNames
{
    public const string Projects = "/v1/selection-projects";
    public const string ClientProjects = "/v1/client/selection";
}
