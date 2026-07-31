using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class MockLicenseProvider : ILicenseProvider
{
    private readonly IReadOnlyDictionary<string, MockLicenseDefinition> _definitions;
    private readonly ConcurrentDictionary<string, HashSet<string>> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly byte[] _signingSecret;
    private readonly TimeProvider _timeProvider;

    public MockLicenseProvider(
        IEnumerable<MockLicenseDefinition> definitions,
        byte[]? signingSecret = null,
        TimeProvider? timeProvider = null)
    {
        _definitions = definitions.ToDictionary(
            definition => LicenseKeyFormatter.Normalize(definition.ActivationKey),
            StringComparer.OrdinalIgnoreCase);
        _signingSecret = signingSecret?.ToArray() ?? RandomNumberGenerator.GetBytes(32);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string Name => "Mock";
    public bool IsConfigured => true;
    public bool NetworkAvailable { get; set; } = true;

    public Task<LicenseProviderResult> ActivateAsync(LicenseActivationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!NetworkAvailable) return Task.FromResult(NetworkFailure());
        var key = LicenseKeyFormatter.Normalize(request.ActivationKey);
        if (!_definitions.TryGetValue(key, out var definition))
        {
            return Task.FromResult(LicenseProviderResult.Failure(LicenseStatus.Invalid, "激活码无效。", LicenseFailureReason.InvalidKey));
        }
        if (definition.Suspended)
        {
            return Task.FromResult(LicenseProviderResult.Failure(LicenseStatus.Suspended, "该授权已暂停。", LicenseFailureReason.Suspended));
        }
        var now = _timeProvider.GetUtcNow();
        if (definition.ExpiresAt is { } expiresAt && expiresAt <= now)
        {
            return Task.FromResult(LicenseProviderResult.Failure(LicenseStatus.Expired, "该授权已过期。", LicenseFailureReason.Expired));
        }

        var devices = _devices.GetOrAdd(key, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        lock (devices)
        {
            if (!devices.Contains(request.AnonymousDeviceFingerprint) && devices.Count >= definition.MaxDevices)
            {
                return Task.FromResult(LicenseProviderResult.Failure(
                    LicenseStatus.ActivationRequired,
                    $"该激活码已达到 {definition.MaxDevices} 台设备上限，请先停用旧设备。",
                    LicenseFailureReason.DeviceLimitReached));
            }
            devices.Add(request.AnonymousDeviceFingerprint);
            var credential = CreateCredential(key, request.AnonymousDeviceFingerprint, definition, devices.Count, now);
            return Task.FromResult(new LicenseProviderResult(
                true,
                definition.Trial ? LicenseStatus.Trial : LicenseStatus.Active,
                definition.Trial ? "试用授权已激活。" : "专业版已激活。",
                Credential: credential));
        }
    }

    public Task<LicenseProviderResult> ValidateAsync(LicenseValidationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!NetworkAvailable) return Task.FromResult(NetworkFailure());
        var offline = ValidateOffline(request.Credential, request.AnonymousDeviceFingerprint);
        if (!offline.Succeeded) return Task.FromResult(offline);
        if (!_definitions.TryGetValue(LicenseKeyFormatter.Normalize(request.Credential.ActivationKey), out var definition))
        {
            return Task.FromResult(LicenseProviderResult.Failure(LicenseStatus.Invalid, "授权记录不存在。", LicenseFailureReason.InvalidKey));
        }
        var devices = _devices.GetOrAdd(LicenseKeyFormatter.Normalize(request.Credential.ActivationKey), _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        lock (devices)
        {
            if (!devices.Contains(request.AnonymousDeviceFingerprint))
            {
                return Task.FromResult(LicenseProviderResult.Failure(LicenseStatus.Invalid, "授权与当前设备不匹配。", LicenseFailureReason.DeviceMismatch));
            }
            var credential = CreateCredential(
                request.Credential.ActivationKey,
                request.AnonymousDeviceFingerprint,
                definition,
                devices.Count,
                _timeProvider.GetUtcNow(),
                request.Credential.ActivatedAt);
            return Task.FromResult(new LicenseProviderResult(true, definition.Trial ? LicenseStatus.Trial : LicenseStatus.Active, "授权验证成功。", Credential: credential));
        }
    }

    public Task<LicenseProviderResult> DeactivateAsync(LicenseValidationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!NetworkAvailable) return Task.FromResult(NetworkFailure());
        var key = LicenseKeyFormatter.Normalize(request.Credential.ActivationKey);
        if (!_devices.TryGetValue(key, out var devices))
        {
            return Task.FromResult(LicenseProviderResult.Failure(LicenseStatus.Invalid, "没有找到本机激活记录。", LicenseFailureReason.InvalidKey));
        }
        lock (devices)
        {
            devices.Remove(request.AnonymousDeviceFingerprint);
        }
        return Task.FromResult(new LicenseProviderResult(true, LicenseStatus.Free, "本机授权已停用。"));
    }

    public LicenseProviderResult ValidateOffline(LicenseCredential credential, string anonymousDeviceFingerprint)
    {
        if (!string.Equals(credential.DeviceFingerprint, anonymousDeviceFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return LicenseProviderResult.Failure(LicenseStatus.Invalid, "授权与当前设备不匹配。", LicenseFailureReason.DeviceMismatch);
        }
        try
        {
            var expected = Sign(credential.SignedPayload);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(expected), Convert.FromBase64String(credential.Signature)))
            {
                return LicenseProviderResult.Failure(LicenseStatus.Invalid, "授权签名验证失败。", LicenseFailureReason.InvalidSignature);
            }
            var payload = JsonSerializer.Deserialize<MockSignedPayload>(credential.SignedPayload);
            if (payload is null || !string.Equals(payload.DeviceFingerprint, anonymousDeviceFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                return LicenseProviderResult.Failure(LicenseStatus.Invalid, "授权内容无效。", LicenseFailureReason.InvalidSignature);
            }
            if (payload.ExpiresAt is { } expiresAt && expiresAt <= _timeProvider.GetUtcNow())
            {
                return LicenseProviderResult.Failure(LicenseStatus.Expired, "授权已过期。", LicenseFailureReason.Expired);
            }
            return new LicenseProviderResult(true, payload.IsTrial ? LicenseStatus.Trial : LicenseStatus.Active, "本地授权签名有效。", Credential: credential);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or CryptographicException)
        {
            return LicenseProviderResult.Failure(LicenseStatus.Invalid, "授权签名验证失败。", LicenseFailureReason.InvalidSignature);
        }
    }

    private LicenseCredential CreateCredential(
        string key,
        string fingerprint,
        MockLicenseDefinition definition,
        int deviceCount,
        DateTimeOffset now,
        DateTimeOffset? activatedAt = null)
    {
        var payload = JsonSerializer.Serialize(new MockSignedPayload(fingerprint, definition.ExpiresAt, definition.MaxDevices, definition.Trial));
        return new LicenseCredential
        {
            Provider = Name,
            ActivationKey = LicenseKeyFormatter.Normalize(key),
            LicenseKeySuffix = LicenseKeyFormatter.Suffix(key),
            DeviceFingerprint = fingerprint,
            SignedPayload = payload,
            Signature = Sign(payload),
            ActivatedAt = activatedAt ?? now,
            LastValidatedAt = now,
            LastObservedUtc = now,
            LicenseExpiresAt = definition.ExpiresAt,
            DeviceCount = deviceCount,
            MaxDevices = definition.MaxDevices,
            IsTrial = definition.Trial
        };
    }

    private string Sign(string payload)
    {
        using var hmac = new HMACSHA256(_signingSecret);
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static LicenseProviderResult NetworkFailure() => LicenseProviderResult.Failure(
        LicenseStatus.OfflineGracePeriod,
        "当前无法连接授权服务，将检查本地离线授权。",
        LicenseFailureReason.NetworkUnavailable,
        true);

    private sealed record MockSignedPayload(string DeviceFingerprint, DateTimeOffset? ExpiresAt, int MaxDevices, bool IsTrial);
}
