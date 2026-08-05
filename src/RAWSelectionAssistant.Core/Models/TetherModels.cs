namespace RAWSelectionAssistant.Core.Models;

public enum CameraProviderType { None, WatchFolder }
public enum TetherSessionState { Running, Stopped, NeedsAttention }
public enum TetherMediaKind { PreviewImage, Raw, Unsupported }
public enum TetherStabilityState { Pending, Probing, Stable, TimedOut, Missing, Inaccessible }
public enum TetherProcessingState { Pending, Ready, Copied, PartiallyCompleted, NeedsAttention, Failed }
public enum TetherPreviewState { None, Pending, Ready, Placeholder, Failed }
public enum WatchFolderEventKind { Created, Changed, Renamed, Error, Reconcile }

public sealed record TetherSessionRecord(
    Guid Id,
    Guid? ProjectId,
    CameraProviderType ProviderType,
    string WatchDirectory,
    string NormalizedWatchDirectory,
    TetherSessionState State,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset DiscoveryCutoffUtc,
    bool ImportExisting,
    bool CopyToProject,
    string? ProjectDestination,
    bool CopyToBackup,
    string? BackupDestination,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? StoppedAtUtc = null,
    string? LastErrorCode = null,
    DateTimeOffset? LastReconciledAtUtc = null,
    DateTimeOffset? CreatedAtUtc = null);

public sealed record TetherAssetRecord(
    Guid Id,
    Guid SessionId,
    Guid? ProjectId,
    string SourcePath,
    string NormalizedSourcePath,
    string FileName,
    string Extension,
    TetherMediaKind MediaKind,
    long? FileSize,
    DateTimeOffset? ModifiedAtUtc,
    DateTimeOffset FirstSeenAtUtc,
    TetherStabilityState StabilityState,
    TetherProcessingState ProcessingState,
    TetherPreviewState PreviewState,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ReadyAtUtc = null,
    string? ProxyCacheKey = null,
    string? PairingKey = null,
    Guid? PairedAssetId = null,
    Guid? ProjectCopyTaskId = null,
    string? ProjectCopyPath = null,
    Guid? BackupCopyTaskId = null,
    string? BackupCopyPath = null,
    string? LastErrorCode = null);

public sealed record TetherAnnotationRecord(
    Guid Id,
    Guid AssetId,
    int Rating,
    string? ColorLabel,
    string? PhotographerNote,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool ClientFavorite = false,
    string? ClientNote = null,
    bool IsRejected = false);

public sealed record WatchFolderStartRequest(
    string Directory,
    Guid? ProjectId = null,
    bool ImportExisting = false,
    bool CopyToProject = false,
    string? ProjectDestination = null,
    bool CopyToBackup = false,
    string? BackupDestination = null,
    bool VerifySha256 = false);

public sealed record WatchFolderEvent(
    WatchFolderEventKind Kind,
    string? Path,
    string? OldPath,
    DateTimeOffset ObservedAtUtc);

public sealed record FileStabilityOptions(
    TimeSpan ProbeInterval,
    TimeSpan MinimumStableWindow,
    TimeSpan MaximumWait,
    int RequiredUnchangedSamples = 3,
    int HeaderBytes = 32)
{
    public static FileStabilityOptions Default { get; } = new(
        TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
}

public sealed record FileStabilityResult(
    TetherStabilityState State,
    long? Length,
    DateTimeOffset? ModifiedAtUtc,
    string? ErrorCode = null);

public sealed record TetherCopyResult(
    Guid AssetId,
    Guid? TaskId,
    string? DestinationPath,
    TetherProcessingState State,
    string? ErrorCode = null);

public sealed record TetherSessionSnapshot(
    TetherSessionRecord Session,
    IReadOnlyList<TetherAssetRecord> Assets,
    int QueueDepth,
    bool ReconciliationPending);
