namespace RAWSelectionAssistant.Core.Services;

public static class ErrorCodeCatalog
{
    public const string SourceNotFound = nameof(SourceNotFound);
    public const string DestinationNotWritable = nameof(DestinationNotWritable);
    public const string DestinationDisconnected = nameof(DestinationDisconnected);
    public const string DiskSpaceInsufficient = nameof(DiskSpaceInsufficient);
    public const string FileLocked = nameof(FileLocked);
    public const string PermissionDenied = nameof(PermissionDenied);
    public const string PathTooLong = nameof(PathTooLong);
    public const string InvalidFileName = nameof(InvalidFileName);
    public const string DuplicateConflict = nameof(DuplicateConflict);
    public const string HashMismatch = nameof(HashMismatch);
    public const string SourceChanged = nameof(SourceChanged);
    public const string SourceAndDestinationSame = nameof(SourceAndDestinationSame);
    public const string DestinationInsideSource = nameof(DestinationInsideSource);
    public const string UnsupportedFormat = nameof(UnsupportedFormat);
    public const string DecodeFailed = nameof(DecodeFailed);
    public const string MetadataReadFailed = nameof(MetadataReadFailed);
    public const string CorruptedImage = nameof(CorruptedImage);
    public const string RawPreviewUnavailable = nameof(RawPreviewUnavailable);
    public const string ColorProfileUnsupported = nameof(ColorProfileUnsupported);
    public const string CancelledByUser = nameof(CancelledByUser);
    public const string InterruptedByShutdown = nameof(InterruptedByShutdown);
    public const string CheckpointInvalid = nameof(CheckpointInvalid);
    public const string RetryLimitReached = nameof(RetryLimitReached);
    public const string InvalidStateTransition = nameof(InvalidStateTransition);
    public const string NeedsUserDecision = nameof(NeedsUserDecision);
    public const string DatabaseUnavailable = nameof(DatabaseUnavailable);
    public const string DatabaseLocked = nameof(DatabaseLocked);
    public const string DatabaseCorrupted = nameof(DatabaseCorrupted);
    public const string MigrationFailed = nameof(MigrationFailed);
    public const string UnsupportedSchemaVersion = nameof(UnsupportedSchemaVersion);
    public const string BackupFailed = nameof(BackupFailed);
    public const string RestoreFailed = nameof(RestoreFailed);
    public const string Timeout = nameof(Timeout);
    public const string NetworkUnavailable = nameof(NetworkUnavailable);
    public const string DnsFailure = nameof(DnsFailure);
    public const string TlsFailure = nameof(TlsFailure);
    public const string ServerUnavailable = nameof(ServerUnavailable);
    public const string AuthenticationExpired = nameof(AuthenticationExpired);
    public const string UploadInterrupted = nameof(UploadInterrupted);

    public static string Describe(string? code) => code switch
    {
        SourceNotFound => "源文件不存在或已断开。",
        DestinationNotWritable => "目标位置不可写。",
        DestinationDisconnected => "目标磁盘或目录已断开。",
        DiskSpaceInsufficient => "目标磁盘空间不足。",
        FileLocked => "文件正被其他程序占用。",
        PermissionDenied => "当前用户没有所需权限。",
        PathTooLong => "文件路径过长，无法安全处理。",
        UnsupportedFormat => "当前文件类型不受支持。",
        DuplicateConflict => "该文件已经关联，未创建重复记录。",
        HashMismatch => "输出文件校验失败，源文件保持不变。",
        SourceChanged => "源文件在任务执行期间发生变化。",
        InvalidStateTransition => "任务状态转换不合法。",
        DatabaseCorrupted => "数据库完整性检查失败，旧数据已保留。",
        UnsupportedSchemaVersion => "数据库版本高于当前应用支持范围。",
        _ => "操作未完成，请查看任务详情并按建议重试。"
    };
}

