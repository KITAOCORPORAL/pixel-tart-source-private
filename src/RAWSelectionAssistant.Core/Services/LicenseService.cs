using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class LicenseService : ILicenseService
{
    private readonly ILicenseProvider _provider;
    private readonly ILicenseStorageService _storage;
    private readonly IDeviceFingerprintService _fingerprintService;
    private readonly ILogService? _logService;
    private readonly TimeProvider _timeProvider;
    private LicenseCredential? _credential;

    public LicenseService(
        LicenseConfiguration configuration,
        ILicenseProvider provider,
        ILicenseStorageService storage,
        IDeviceFingerprintService fingerprintService,
        ILogService? logService = null,
        TimeProvider? timeProvider = null)
    {
        Configuration = configuration;
        _provider = provider;
        _storage = storage;
        _fingerprintService = fingerprintService;
        _logService = logService;
        _timeProvider = timeProvider ?? TimeProvider.System;
        Current = LicenseState.Free(provider.IsConfigured ? "未激活，当前为免费版。" : "授权服务尚未配置，当前为免费版。");
    }

    public LicenseState Current { get; private set; }
    public LicenseConfiguration Configuration { get; }
    public event EventHandler? LicenseChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _credential = await _storage.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (_credential is null)
        {
            SetState(LicenseState.Free(_provider.IsConfigured ? "未激活，当前为免费版。" : "授权服务尚未配置，当前为免费版。"));
            return;
        }
        await ValidateAsync(false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LicenseProviderResult> ActivateAsync(string activationKey, CancellationToken cancellationToken = default)
    {
        var normalized = LicenseKeyFormatter.Normalize(activationKey);
        if (!LicenseKeyFormatter.IsComplete(normalized))
        {
            return LicenseProviderResult.Failure(LicenseStatus.Invalid, "请输入完整激活码，格式为 KQGP-XXXXX-XXXXX-XXXXX。", LicenseFailureReason.InvalidKey);
        }
        if (!_provider.IsConfigured)
        {
            var notConfigured = LicenseProviderResult.Failure(LicenseStatus.ActivationRequired, "授权服务尚未配置，软件仍可继续使用免费版。", LicenseFailureReason.NotConfigured);
            SetFailureState(notConfigured);
            return notConfigured;
        }

        var request = new LicenseActivationRequest(
            normalized,
            _fingerprintService.GetAnonymousFingerprint(),
            Branding.ProductVersion,
            Configuration.ProductId,
            Environment.OSVersion.VersionString,
            Guid.NewGuid().ToString("N"));
        var result = await _provider.ActivateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Credential is null)
        {
            _logService?.Info($"授权激活失败，激活码尾号 {LicenseKeyFormatter.Suffix(normalized)}，原因：{result.FailureReason}。");
            SetFailureState(result);
            return result;
        }

        var offline = _provider.ValidateOffline(result.Credential, request.AnonymousDeviceFingerprint);
        if (!offline.Succeeded)
        {
            SetFailureState(offline);
            return offline;
        }

        var now = _timeProvider.GetUtcNow();
        _credential = result.Credential;
        _credential.LastValidatedAt = now;
        _credential.LastObservedUtc = now;
        _credential.OfflineExpiresAt = now.AddDays(Math.Max(1, Configuration.OfflineGraceDays));
        await _storage.SaveAsync(_credential, cancellationToken).ConfigureAwait(false);
        SetState(BuildProState(_credential, result.Status, result.Message));
        _logService?.Info($"专业版授权已激活，激活码尾号 {_credential.LicenseKeySuffix}。");
        return result;
    }

    public async Task<LicenseProviderResult> DeactivateAsync(CancellationToken cancellationToken = default)
    {
        if (_credential is null)
        {
            var alreadyFree = new LicenseProviderResult(true, LicenseStatus.Free, "当前没有本机专业版授权。");
            SetState(LicenseState.Free());
            return alreadyFree;
        }

        var request = CreateValidationRequest(_credential);
        var result = await _provider.DeactivateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            _logService?.Info($"停用本机授权失败，保留现有有效凭证，原因：{result.FailureReason}。");
            return result;
        }

        await _storage.ClearAsync(cancellationToken).ConfigureAwait(false);
        _credential = null;
        SetState(LicenseState.Free("本机授权已停用，项目和照片未被删除。"));
        _logService?.Info("本机专业版授权已停用，软件退回免费版。");
        return result;
    }

    public async Task<LicenseProviderResult> ValidateAsync(bool forceOnline = false, CancellationToken cancellationToken = default)
    {
        if (_credential is null)
        {
            var free = LicenseProviderResult.Failure(LicenseStatus.ActivationRequired, "当前未激活。", LicenseFailureReason.InvalidKey);
            SetState(LicenseState.Free("未激活，当前为免费版。"));
            return free;
        }

        var fingerprint = _fingerprintService.GetAnonymousFingerprint();
        var offline = _provider.ValidateOffline(_credential, fingerprint);
        if (!offline.Succeeded)
        {
            SetFailureState(offline);
            return offline;
        }

        var now = _timeProvider.GetUtcNow();
        var clockRolledBack = now.AddMinutes(5) < _credential.LastObservedUtc;
        var validationDue = now - _credential.LastValidatedAt >= TimeSpan.FromDays(Math.Max(1, Configuration.ValidationIntervalDays));
        if (!forceOnline && !clockRolledBack && !validationDue)
        {
            _credential.LastObservedUtc = now > _credential.LastObservedUtc ? now : _credential.LastObservedUtc;
            await _storage.SaveAsync(_credential, cancellationToken).ConfigureAwait(false);
            SetState(BuildProState(_credential, _credential.IsTrial ? LicenseStatus.Trial : LicenseStatus.Active, "本地授权有效。"));
            return offline;
        }

        var online = await _provider.ValidateAsync(CreateValidationRequest(_credential), cancellationToken).ConfigureAwait(false);
        if (online.Succeeded && online.Credential is not null)
        {
            var activatedAt = _credential.ActivatedAt;
            _credential = online.Credential;
            _credential.ActivatedAt = activatedAt;
            _credential.LastValidatedAt = now;
            _credential.LastObservedUtc = now;
            _credential.OfflineExpiresAt = now.AddDays(Math.Max(1, Configuration.OfflineGraceDays));
            await _storage.SaveAsync(_credential, cancellationToken).ConfigureAwait(false);
            SetState(BuildProState(_credential, online.Status, "授权在线验证成功。"));
            return online;
        }

        if (online.IsNetworkError && now <= _credential.OfflineExpiresAt)
        {
            _credential.LastObservedUtc = now > _credential.LastObservedUtc ? now : _credential.LastObservedUtc;
            await _storage.SaveAsync(_credential, cancellationToken).ConfigureAwait(false);
            var grace = new LicenseProviderResult(true, LicenseStatus.OfflineGracePeriod, clockRolledBack
                ? "检测到系统时间可能回拨；当前继续使用离线宽限并将在联网后复核。"
                : "网络不可用，当前在离线宽限期内。", Credential: _credential, IsNetworkError: true);
            SetState(BuildProState(_credential, LicenseStatus.OfflineGracePeriod, grace.Message));
            return grace;
        }

        if (online.IsNetworkError)
        {
            var expired = LicenseProviderResult.Failure(LicenseStatus.Expired, "离线宽限期已结束，软件已安全退回免费版。", LicenseFailureReason.NetworkUnavailable, true);
            SetFailureState(expired);
            return expired;
        }

        SetFailureState(online);
        return online;
    }

    private LicenseValidationRequest CreateValidationRequest(LicenseCredential credential) => new(
        credential,
        _fingerprintService.GetAnonymousFingerprint(),
        Branding.ProductVersion,
        Environment.OSVersion.VersionString,
        Guid.NewGuid().ToString("N"));

    private LicenseState BuildProState(LicenseCredential credential, LicenseStatus status, string message) => new(
        ProductEdition.Pro,
        status,
        credential.Provider,
        message,
        _fingerprintService.DeviceName,
        credential.DeviceCount,
        credential.MaxDevices,
        credential.ActivatedAt,
        credential.LastValidatedAt,
        credential.OfflineExpiresAt,
        credential.LicenseKeySuffix);

    private void SetFailureState(LicenseProviderResult result)
    {
        SetState(new LicenseState(
            ProductEdition.Free,
            result.Status,
            _provider.Name,
            result.Message,
            _fingerprintService.DeviceName,
            _credential?.DeviceCount ?? 0,
            _credential?.MaxDevices ?? Configuration.MaxDevices,
            _credential?.ActivatedAt,
            _credential?.LastValidatedAt,
            _credential?.OfflineExpiresAt,
            _credential?.LicenseKeySuffix ?? string.Empty,
            result.FailureReason));
    }

    private void SetState(LicenseState state)
    {
        Current = state;
        LicenseChanged?.Invoke(this, EventArgs.Empty);
    }
}
