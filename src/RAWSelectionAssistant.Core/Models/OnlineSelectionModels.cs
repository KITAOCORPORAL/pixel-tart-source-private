using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace RAWSelectionAssistant.Core.Models;

public enum OnlineSelectionProviderKind
{
    None,
    LocalDev
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

public sealed partial record SelectionAsset(
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

public sealed partial record SelectionAsset
{
    /// <summary>Stable project-scoped identity; equivalent to <see cref="Id"/>.</summary>
    [JsonIgnore]
    public Guid SelectionAssetId => Id;

    /// <summary>Optional reference to the local Asset Library identity.</summary>
    public Guid? SourceAssetId { get; init; }

    /// <summary>Filename stem retained for deterministic result matching.</summary>
    [JsonIgnore]
    public string OriginalStem => Path.GetFileNameWithoutExtension(OriginalFileName);
}

public sealed record SelectionAssetImportCandidate(
    string SourcePath,
    Guid? SourceAssetId = null,
    string? OriginalFileName = null);

public static class SelectionAssetFactory
{
    public static SelectionAsset Create(
        Guid projectId,
        SelectionAssetImportCandidate candidate,
        int sortOrder,
        SelectionAssetStatus status = SelectionAssetStatus.LocalOnly,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (projectId == Guid.Empty) throw new ArgumentException("项目标识不能为空。", nameof(projectId));
        var path = Path.GetFullPath(candidate.SourcePath);
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        return new SelectionAsset(
            Guid.NewGuid(),
            projectId,
            SelectionPrivacyFileName(candidate.OriginalFileName, path),
            path,
            null,
            status,
            sortOrder,
            false,
            now,
            now)
        {
            SourceAssetId = candidate.SourceAssetId
        };
    }

    private static string SelectionPrivacyFileName(string? requested, string path) =>
        string.IsNullOrWhiteSpace(requested) ? Path.GetFileName(path) : Path.GetFileName(requested);
}

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
    bool ExtraSelected)
{
    [JsonIgnore]
    public Guid SelectionAssetId => ImageId;

    public Guid? SourceAssetId { get; init; }
}

public sealed record SelectionFinalResult(
    Guid SelectionProjectId,
    DateTimeOffset ConfirmedAtUtc,
    IReadOnlyList<SelectionFinalItem> Items)
{
    /// <summary>Monotonic client-selection version captured at confirmation.</summary>
    public int SelectionVersion { get; init; } = 1;

    /// <summary>Confirmed snapshots are locked until the photographer reopens the project.</summary>
    public bool IsLocked { get; init; } = true;

    public FinalSelectionSnapshot ToSnapshot() => new(
        SelectionProjectId,
        SelectionVersion,
        Items,
        ConfirmedAtUtc,
        IsLocked);
}

public sealed record FinalSelectionSnapshot(
    Guid ProjectId,
    int SelectionVersion,
    IReadOnlyList<SelectionFinalItem> AssetItems,
    DateTimeOffset ConfirmedAtUtc,
    bool IsLocked = true)
{
    public IReadOnlyList<Guid> AssetIds => AssetItems
        .Where(item => item.Selected)
        .Select(item => item.ImageId)
        .ToArray();

    public SelectionFinalResult ToFinalResult() => new(ProjectId, ConfirmedAtUtc, AssetItems)
    {
        SelectionVersion = SelectionVersion,
        IsLocked = IsLocked
    };
}

public sealed record SelectionConfirmationState(
    Guid ProjectId,
    int SelectionVersion,
    bool IsConfirmed,
    DateTimeOffset? ConfirmedAtUtc,
    bool IsLocked);

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
    /// <summary>Local client mock choices retained separately from final snapshots.</summary>
    public IReadOnlyList<SelectionChoice> Choices { get; init; } = [];

    /// <summary>Local client mock comments retained separately from final snapshots.</summary>
    public IReadOnlyList<SelectionComment> Comments { get; init; } = [];

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
