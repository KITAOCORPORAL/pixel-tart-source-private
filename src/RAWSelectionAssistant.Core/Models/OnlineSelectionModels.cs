using System.Security.Cryptography;

namespace RAWSelectionAssistant.Core.Models;

public enum OnlineSelectionProviderKind
{
    None
}

public enum SelectionProjectStatus
{
    Draft,
    Uploading,
    Ready,
    Published,
    Selecting,
    ClientConfirmed,
    Closed,
    Archived
}

public enum SelectionAssetStatus
{
    LocalOnly,
    Queued,
    Uploading,
    Ready,
    Failed,
    DeletedCloudCopy
}

public enum SelectionUploadQueueState
{
    Idle,
    Running,
    Paused
}

public enum SelectionRawMatchStatus
{
    Matched,
    NotFound,
    Conflict,
    NotSelected
}

public enum SelectionProxyState
{
    Ready,
    Unsupported,
    Failed
}

public enum SelectionSyncState
{
    Completed,
    NeedsAttention,
    Failed
}

public sealed record SelectionProject(
    Guid Id,
    string PublicId,
    string Name,
    string ClientDisplayName,
    SelectionProjectStatus Status,
    int TargetCount,
    DateTimeOffset? DeadlineUtc,
    Guid? CoverAssetId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? LocalSourceDirectory = null);

public sealed record SelectionAsset(
    Guid Id,
    Guid ProjectId,
    string OriginalFileName,
    string LocalSourcePath,
    string? ProxyJpegPath,
    SelectionAssetStatus Status,
    int SortOrder,
    bool IsCover,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    long? ProxyBytes = null,
    string? CloudAssetId = null,
    string? LastErrorCode = null);

public sealed record SelectionRule(
    Guid ProjectId,
    int TargetCount,
    int MinimumCount,
    int MaximumCount,
    bool AllowExtraSelections,
    long ExtraSelectionPriceMinor,
    bool AllowComments,
    bool AllowFavorites,
    bool AllowDownload,
    bool ShowFileNames,
    bool ApplyWatermark,
    DateTimeOffset? DeadlineUtc,
    DateTimeOffset? AccessExpiresAtUtc,
    bool RequirePin,
    bool LockAfterConfirmation)
{
    public static SelectionRule Default(Guid projectId, int targetCount, DateTimeOffset? deadlineUtc = null) => new(
        projectId,
        targetCount,
        0,
        Math.Max(targetCount, 1),
        true,
        0,
        true,
        true,
        false,
        true,
        false,
        deadlineUtc,
        deadlineUtc?.AddDays(30),
        false,
        true);
}

public sealed record SelectionChoice(
    Guid ProjectId,
    Guid AssetId,
    bool Selected,
    bool Favorite,
    bool ExtraSelected,
    DateTimeOffset UpdatedAtUtc);

public sealed record SelectionComment(
    Guid Id,
    Guid ProjectId,
    Guid AssetId,
    string CustomerNote,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record SelectionPublish(
    Guid Id,
    Guid ProjectId,
    string PublicId,
    int TokenVersion,
    DateTimeOffset PublishedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc = null);

public sealed record SelectionClientSession(
    Guid Id,
    Guid ProjectId,
    string PublicId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset LastSeenAtUtc,
    DateTimeOffset? ConfirmedAtUtc = null);

public sealed record SelectionProgress(
    Guid ProjectId,
    int TotalCount,
    int ReadyCount,
    int SelectedCount,
    int FavoriteCount,
    int CommentCount,
    int ExtraSelectedCount,
    DateTimeOffset? LastActivityAtUtc);

public sealed record SelectionFinalItem(
    Guid SelectionProjectId,
    Guid ImageId,
    string OriginalFileName,
    bool Selected,
    bool Favorite,
    string? CustomerNote,
    bool ExtraSelected);

public sealed record SelectionFinalResult(
    Guid SelectionProjectId,
    DateTimeOffset ConfirmedAtUtc,
    IReadOnlyList<SelectionFinalItem> Items);

public sealed record SelectionRawMatch(
    SelectionFinalItem Selection,
    SelectionRawMatchStatus Status,
    string? RawPath,
    IReadOnlyList<string> Candidates,
    string Message);

public sealed record SelectionSyncResult(
    SelectionSyncState State,
    Guid ProjectId,
    IReadOnlyList<SelectionRawMatch> Matches,
    string? ArchivePath,
    string Message);

public sealed record SelectionProxyOptions(int LongEdge, int Quality, bool ConvertToSrgb)
{
    public static SelectionProxyOptions OnlineDefault { get; } = new(2560, 85, true);
}

public sealed record SelectionProxyResult(
    SelectionProxyState State,
    string? OutputPath,
    long Bytes,
    string Message,
    string? ErrorCode = null);

public sealed record SelectionWorkspaceSnapshot(
    IReadOnlyList<SelectionProject> Projects,
    IReadOnlyList<SelectionAsset> Assets,
    IReadOnlyList<SelectionRule> Rules,
    IReadOnlyList<SelectionFinalResult> FinalResults)
{
    public static SelectionWorkspaceSnapshot Empty { get; } = new([], [], [], []);
}

public static class SelectionProjectFactory
{
    public static SelectionProject CreateDraft(
        string name,
        string clientDisplayName,
        int targetCount,
        DateTimeOffset? deadlineUtc = null,
        string? localSourceDirectory = null,
        DateTimeOffset? nowUtc = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        return new(
            Guid.NewGuid(),
            CreatePublicId(),
            name.Trim(),
            clientDisplayName.Trim(),
            SelectionProjectStatus.Draft,
            targetCount,
            deadlineUtc,
            null,
            now,
            now,
            string.IsNullOrWhiteSpace(localSourceDirectory) ? null : Path.GetFullPath(localSourceDirectory));
    }

    public static string CreatePublicId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}

public static class SelectionDisplayText
{
    public static string ProjectStatus(SelectionProjectStatus status) => status switch
    {
        SelectionProjectStatus.Draft => "草稿",
        SelectionProjectStatus.Uploading => "上传中",
        SelectionProjectStatus.Ready => "待发布",
        SelectionProjectStatus.Published => "已发布",
        SelectionProjectStatus.Selecting => "客户选片中",
        SelectionProjectStatus.ClientConfirmed => "客户已确认",
        SelectionProjectStatus.Closed => "已关闭",
        SelectionProjectStatus.Archived => "已归档",
        _ => "未知状态"
    };

    public static string AssetStatus(SelectionAssetStatus status) => status switch
    {
        SelectionAssetStatus.LocalOnly => "仅本地",
        SelectionAssetStatus.Queued => "等待上传",
        SelectionAssetStatus.Uploading => "上传中",
        SelectionAssetStatus.Ready => "已就绪",
        SelectionAssetStatus.Failed => "上传失败",
        SelectionAssetStatus.DeletedCloudCopy => "云端副本已删除",
        _ => "未知状态"
    };
}
