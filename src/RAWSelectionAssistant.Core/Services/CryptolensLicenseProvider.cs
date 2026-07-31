using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class CryptolensLicenseProvider : ILicenseProvider
{
    private const string ApiRoot = "https://api.cryptolens.io/api";
    private readonly LicenseConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    public CryptolensLicenseProvider(
        LicenseConfiguration configuration,
        HttpClient? httpClient = null,
        TimeProvider? timeProvider = null)
    {
        _configuration = configuration;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string Name => "Cryptolens";
    public bool IsConfigured => _configuration.IsCryptolensConfigured;

    public Task<LicenseProviderResult> ActivateAsync(LicenseActivationRequest request, CancellationToken cancellationToken) =>
        SendKeyRequestAsync("key/Activate", request.ActivationKey, request.AnonymousDeviceFingerprint, cancellationToken);

    public Task<LicenseProviderResult> ValidateAsync(LicenseValidationRequest request, CancellationToken cancellationToken) =>
        SendKeyRequestAsync("key/GetKey", request.Credential.ActivationKey, request.AnonymousDeviceFingerprint, cancellationToken, request.Credential.ActivatedAt);

    public async Task<LicenseProviderResult> DeactivateAsync(LicenseValidationRequest request, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return NotConfigured();
        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = _configuration.PublicValidationToken,
                ["ProductId"] = _configuration.ProductId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Key"] = request.Credential.ActivationKey,
                ["MachineCode"] = request.AnonymousDeviceFingerprint
            });
            using var response = await _httpClient.PostAsync($"{ApiRoot}/key/Deactivate", content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return LicenseProviderResult.Failure(LicenseStatus.Invalid, "停用请求被授权服务拒绝。", LicenseFailureReason.ProviderError);
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            return IsSuccess(json.RootElement)
                ? new LicenseProviderResult(true, LicenseStatus.Free, "本机授权已停用。")
                : MapProviderFailure(json.RootElement, "停用失败，请稍后重试。");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return NetworkFailure();
        }
    }

    public LicenseProviderResult ValidateOffline(LicenseCredential credential, string anonymousDeviceFingerprint)
    {
        if (!IsConfigured) return NotConfigured();
        if (!string.Equals(credential.DeviceFingerprint, anonymousDeviceFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return LicenseProviderResult.Failure(LicenseStatus.Invalid, "授权与当前设备不匹配。", LicenseFailureReason.DeviceMismatch);
        }
        try
        {
            using var rsa = RSA.Create();
            ImportPublicKey(rsa, _configuration.PublicKey);
            var signature = Convert.FromBase64String(credential.Signature);
            var valid = rsa.VerifyData(Encoding.UTF8.GetBytes(credential.SignedPayload), signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!valid)
            {
                return LicenseProviderResult.Failure(LicenseStatus.Invalid, "授权签名验证失败。", LicenseFailureReason.InvalidSignature);
            }
            if (credential.LicenseExpiresAt is { } expires && expires <= _timeProvider.GetUtcNow())
            {
                return LicenseProviderResult.Failure(LicenseStatus.Expired, "授权已过期。", LicenseFailureReason.Expired);
            }
            return new LicenseProviderResult(true, credential.IsTrial ? LicenseStatus.Trial : LicenseStatus.Active, "本地授权签名有效。", Credential: credential);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or InvalidOperationException)
        {
            return LicenseProviderResult.Failure(LicenseStatus.Invalid, "授权签名无法验证。", LicenseFailureReason.InvalidSignature);
        }
    }

    private async Task<LicenseProviderResult> SendKeyRequestAsync(
        string operation,
        string activationKey,
        string fingerprint,
        CancellationToken cancellationToken,
        DateTimeOffset? activatedAt = null)
    {
        if (!IsConfigured) return NotConfigured();
        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = _configuration.PublicValidationToken,
                ["ProductId"] = _configuration.ProductId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Key"] = activationKey,
                ["MachineCode"] = fingerprint,
                ["Sign"] = "true"
            });
            using var response = await _httpClient.PostAsync($"{ApiRoot}/{operation}", content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return LicenseProviderResult.Failure(LicenseStatus.Invalid, "授权服务返回错误。", LicenseFailureReason.ProviderError);
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            if (!IsSuccess(json.RootElement)) return MapProviderFailure(json.RootElement, "激活码无效或不可用。");
            if (!TryGetProperty(json.RootElement, "licenseKey", out var licenseElement) ||
                !TryGetProperty(json.RootElement, "signature", out var signatureElement))
            {
                return LicenseProviderResult.Failure(LicenseStatus.Invalid, "授权响应缺少签名，已拒绝激活。", LicenseFailureReason.InvalidSignature);
            }

            var now = _timeProvider.GetUtcNow();
            var credential = new LicenseCredential
            {
                Provider = Name,
                ActivationKey = LicenseKeyFormatter.Normalize(activationKey),
                LicenseKeySuffix = LicenseKeyFormatter.Suffix(activationKey),
                DeviceFingerprint = fingerprint,
                SignedPayload = licenseElement.GetRawText(),
                Signature = signatureElement.GetString() ?? string.Empty,
                ActivatedAt = activatedAt ?? now,
                LastValidatedAt = now,
                LastObservedUtc = now,
                LicenseExpiresAt = ReadDate(licenseElement, "expires"),
                DeviceCount = ReadArrayCount(licenseElement, "activatedMachines"),
                MaxDevices = _configuration.MaxDevices
            };
            var offline = ValidateOffline(credential, fingerprint);
            return offline.Succeeded
                ? LicenseProviderResult.Success(credential, "专业版授权验证成功。")
                : offline;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return NetworkFailure();
        }
    }

    private static bool IsSuccess(JsonElement root)
    {
        if (!TryGetProperty(root, "result", out var result)) return false;
        return result.ValueKind switch
        {
            JsonValueKind.Number => result.TryGetInt32(out var code) && code == 0,
            JsonValueKind.String => result.GetString() is "0" or "Success" or "success",
            _ => false
        };
    }

    private static LicenseProviderResult MapProviderFailure(JsonElement root, string fallback)
    {
        var message = TryGetProperty(root, "message", out var messageElement) ? messageElement.GetString() ?? fallback : fallback;
        if (message.Contains("machine", StringComparison.OrdinalIgnoreCase) || message.Contains("device", StringComparison.OrdinalIgnoreCase))
        {
            return LicenseProviderResult.Failure(LicenseStatus.ActivationRequired, "激活码已达到设备上限，请先停用旧设备。", LicenseFailureReason.DeviceLimitReached);
        }
        if (message.Contains("suspend", StringComparison.OrdinalIgnoreCase))
        {
            return LicenseProviderResult.Failure(LicenseStatus.Suspended, "该授权已暂停。", LicenseFailureReason.Suspended);
        }
        if (message.Contains("expire", StringComparison.OrdinalIgnoreCase))
        {
            return LicenseProviderResult.Failure(LicenseStatus.Expired, "该授权已过期。", LicenseFailureReason.Expired);
        }
        return LicenseProviderResult.Failure(LicenseStatus.Invalid, fallback, LicenseFailureReason.InvalidKey);
    }

    private LicenseProviderResult NotConfigured() => LicenseProviderResult.Failure(
        LicenseStatus.ActivationRequired,
        "授权服务尚未配置，软件将继续以免费版运行。",
        LicenseFailureReason.NotConfigured);

    private static LicenseProviderResult NetworkFailure() => LicenseProviderResult.Failure(
        LicenseStatus.OfflineGracePeriod,
        "无法连接授权服务，将检查本地离线授权。",
        LicenseFailureReason.NetworkUnavailable,
        true);

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static DateTimeOffset? ReadDate(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value)) return null;
        return value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var date) ? date : null;
    }

    private static int ReadArrayCount(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 1;

    private static void ImportPublicKey(RSA rsa, string publicKey)
    {
        var trimmed = publicKey.Trim();
        if (trimmed.StartsWith("<RSAKeyValue>", StringComparison.Ordinal))
        {
            var xml = XDocument.Parse(trimmed).Root ?? throw new CryptographicException("RSA 公钥格式无效。");
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = Convert.FromBase64String(xml.Element("Modulus")?.Value ?? string.Empty),
                Exponent = Convert.FromBase64String(xml.Element("Exponent")?.Value ?? string.Empty)
            });
            return;
        }
        rsa.ImportFromPem(trimmed);
    }
}
