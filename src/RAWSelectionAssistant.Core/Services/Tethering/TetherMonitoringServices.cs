using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;

namespace RAWSelectionAssistant.Core.Services.Tethering;

public interface ITetherAnnotationService
{
    Task<TetherAnnotationRecord?> GetAsync(Guid assetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, TetherAnnotationRecord>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<TetherAnnotationSaveResult> SaveAsync(TetherAnnotationRecord annotation, Guid? projectId = null, CancellationToken cancellationToken = default);
}

public sealed class TetherAnnotationService(
    ITetherAnnotationRepository repository,
    IAuditLogService? auditLog = null,
    INotificationCenter? notificationCenter = null) : ITetherAnnotationService
{
    private static readonly HashSet<string> AllowedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        "红", "黄", "绿", "蓝", "紫"
    };

    public Task<TetherAnnotationRecord?> GetAsync(Guid assetId, CancellationToken cancellationToken = default) =>
        repository.GetByAssetAsync(assetId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, TetherAnnotationRecord>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        (await repository.ListBySessionAsync(sessionId, cancellationToken).ConfigureAwait(false)).ToDictionary(item => item.AssetId);

    public async Task<TetherAnnotationSaveResult> SaveAsync(TetherAnnotationRecord annotation, Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        if (annotation.Rating is < 0 or > 5)
            return new(false, null, ErrorCodeCatalog.InvalidStateTransition, "星级必须在0至5之间。");
        if (!string.IsNullOrWhiteSpace(annotation.ColorLabel) && !AllowedColors.Contains(annotation.ColorLabel))
            return new(false, null, ErrorCodeCatalog.InvalidStateTransition, "颜色标签不受支持。");

        var normalized = annotation with
        {
            ColorLabel = string.IsNullOrWhiteSpace(annotation.ColorLabel) ? null : annotation.ColorLabel,
            PhotographerNote = NormalizeNote(annotation.PhotographerNote),
            ClientNote = NormalizeNote(annotation.ClientNote),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        try
        {
            await repository.UpsertAsync(normalized, cancellationToken).ConfigureAwait(false);
            await WriteAuditSafelyAsync("Information", normalized.AssetId, "Success", projectId, null).ConfigureAwait(false);
            return new(true, normalized);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
        {
            await WriteAuditSafelyAsync("Error", normalized.AssetId, "Failed", projectId, ErrorCodeCatalog.DatabaseUnavailable).ConfigureAwait(false);
            await PublishFailureSafelyAsync(normalized.AssetId, projectId).ConfigureAwait(false);
            return new(false, null, ErrorCodeCatalog.DatabaseUnavailable, "标注未保存，请检查数据库状态后重试。");
        }
    }

    private async Task WriteAuditSafelyAsync(string severity, Guid assetId, string result, Guid? projectId, string? errorCode)
    {
        if (auditLog is null) return;
        try
        {
            await auditLog.WriteAsync("Tether", "AnnotationSaved", severity, $"AssetId={assetId:D}; Result={result}",
                projectId: projectId, errorCode: errorCode, cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
        {
            // Annotation persistence is authoritative; audit availability must not invert its committed result.
        }
    }

    private async Task PublishFailureSafelyAsync(Guid assetId, Guid? projectId)
    {
        if (notificationCenter is null) return;
        try
        {
            var notification = new NotificationMessage(Guid.NewGuid(), NotificationType.InlineError, NotificationSeverity.Error,
                "现场标注未保存", "数据库暂时不可用，照片和原有标注均未改变。", null, projectId, [], false, DateTimeOffset.UtcNow,
                DeduplicationKey: $"tether-annotation-{assetId:D}");
            await notificationCenter.PublishAsync(notification, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
        {
            // The explicit failure result remains available even if the notification channel is unavailable.
        }
    }

    private static string? NormalizeNote(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Replace("\0", string.Empty, StringComparison.Ordinal).Trim();
        return normalized.Length <= 4000 ? normalized : normalized[..4000];
    }
}

public sealed class LiveSelectionCoordinator
{
    private readonly object _sync = new();
    private Guid? _latestAssetId;

    public bool AutoLatest { get; set; } = true;
    public bool IsLocked { get; private set; }
    public bool IsActualSize { get; set; }
    public bool IsComparing { get; set; }
    public bool IsEditingNote { get; set; }
    public bool HasActiveInteraction { get; set; }
    public bool IsViewingOlderAsset { get; private set; }
    public Guid? SelectedAssetId { get; private set; }
    public int NewAssetCount { get; private set; }

    public Guid? OnReady(Guid assetId)
    {
        lock (_sync)
        {
            _latestAssetId = assetId;
            if (!CanAdvance()) { NewAssetCount++; return null; }
            SelectedAssetId = assetId;
            IsViewingOlderAsset = false;
            NewAssetCount = 0;
            return assetId;
        }
    }

    public void SelectManually(Guid assetId)
    {
        lock (_sync)
        {
            SelectedAssetId = assetId;
            IsViewingOlderAsset = _latestAssetId.HasValue && _latestAssetId.Value != assetId;
        }
    }

    public void SetLocked(bool value) { lock (_sync) IsLocked = value; }

    public Guid? UnlockAndSelectLatest()
    {
        lock (_sync)
        {
            IsLocked = false;
            SelectedAssetId = _latestAssetId;
            IsViewingOlderAsset = false;
            NewAssetCount = 0;
            return SelectedAssetId;
        }
    }

    private bool CanAdvance() => AutoLatest && !IsLocked && !IsActualSize && !IsComparing && !IsEditingNote && !HasActiveInteraction && !IsViewingOlderAsset;
}
