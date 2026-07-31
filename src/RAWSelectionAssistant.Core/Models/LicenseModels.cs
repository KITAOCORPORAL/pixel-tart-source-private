using System.Text.Json.Serialization;

namespace RAWSelectionAssistant.Core.Models;

public enum ProductEdition
{
    Free,
    Pro
}

public enum LicenseStatus
{
    Free,
    Trial,
    Active,
    Expired,
    Suspended,
    ActivationRequired,
    OfflineGracePeriod,
    Invalid
}

public enum LicensedFeature
{
    UnlimitedSelections,
    MultipleSourceDirectories,
    PersistentFileIndex,
    CustomFileFormats,
    AdvancedJpegQualityAssessment,
    AdvancedConflictResolution,
    UnlimitedProjectHistory,
    AdvancedReports,
    OutputPresets,
    BatchProjects
}

public enum LicenseFailureReason
{
    None,
    NotConfigured,
    InvalidKey,
    Expired,
    Suspended,
    DeviceLimitReached,
    NetworkUnavailable,
    InvalidSignature,
    DeviceMismatch,
    ProviderError
}

public sealed class LicenseConfiguration
{
    public string Provider { get; set; } = "None";
    public int ProductId { get; set; }
    public string PublicKey { get; set; } = string.Empty;
    public string PublicValidationToken { get; set; } = string.Empty;
    public string PurchaseUrl { get; set; } = string.Empty;
    public int OfflineGraceDays { get; set; } = 90;
    public int ValidationIntervalDays { get; set; } = 7;
    public int MaxDevices { get; set; } = 1;

    [JsonIgnore]
    public bool IsCryptolensConfigured =>
        Provider.Equals("Cryptolens", StringComparison.OrdinalIgnoreCase) &&
        ProductId > 0 &&
        !string.IsNullOrWhiteSpace(PublicKey) &&
        !string.IsNullOrWhiteSpace(PublicValidationToken);
}

public sealed record LicenseState(
    ProductEdition Edition,
    LicenseStatus Status,
    string Provider,
    string Message,
    string DeviceName,
    int DeviceCount,
    int MaxDevices,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? LastValidatedAt,
    DateTimeOffset? OfflineExpiresAt,
    string LicenseKeySuffix,
    LicenseFailureReason FailureReason = LicenseFailureReason.None)
{
    public bool IsPro => Edition == ProductEdition.Pro &&
                         Status is LicenseStatus.Trial or LicenseStatus.Active or LicenseStatus.OfflineGracePeriod;
    public TimeSpan? OfflineRemaining(DateTimeOffset now) => OfflineExpiresAt is null ? null : OfflineExpiresAt - now;

    public static LicenseState Free(string message = "免费版可完成基础归片") =>
        new(ProductEdition.Free, LicenseStatus.Free, "None", message, Environment.MachineName, 0, 1, null, null, null, string.Empty);
}

public sealed class LicenseCredential
{
    public string Provider { get; set; } = string.Empty;
    public string ActivationKey { get; set; } = string.Empty;
    public string LicenseKeySuffix { get; set; } = string.Empty;
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string SignedPayload { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public DateTimeOffset ActivatedAt { get; set; }
    public DateTimeOffset LastValidatedAt { get; set; }
    public DateTimeOffset OfflineExpiresAt { get; set; }
    public DateTimeOffset? LicenseExpiresAt { get; set; }
    public int DeviceCount { get; set; }
    public int MaxDevices { get; set; } = 1;
    public DateTimeOffset LastObservedUtc { get; set; }
    public bool IsTrial { get; set; }
}

public sealed record LicenseActivationRequest(
    string ActivationKey,
    string AnonymousDeviceFingerprint,
    string SoftwareVersion,
    int ProductId,
    string OperatingSystemVersion,
    string RequestId);

public sealed record LicenseValidationRequest(
    LicenseCredential Credential,
    string AnonymousDeviceFingerprint,
    string SoftwareVersion,
    string OperatingSystemVersion,
    string RequestId);

public sealed record LicenseProviderResult(
    bool Succeeded,
    LicenseStatus Status,
    string Message,
    LicenseFailureReason FailureReason = LicenseFailureReason.None,
    LicenseCredential? Credential = null,
    bool IsNetworkError = false)
{
    public static LicenseProviderResult Success(LicenseCredential credential, string message = "授权有效") =>
        new(true, LicenseStatus.Active, message, Credential: credential);

    public static LicenseProviderResult Failure(
        LicenseStatus status,
        string message,
        LicenseFailureReason reason,
        bool networkError = false) =>
        new(false, status, message, reason, IsNetworkError: networkError);
}

public sealed record FeatureAccessResult(bool Allowed, LicensedFeature Feature, string Message)
{
    public static FeatureAccessResult Permit(LicensedFeature feature) => new(true, feature, string.Empty);
    public static FeatureAccessResult Deny(LicensedFeature feature, string message) => new(false, feature, message);
}

public sealed record MockLicenseDefinition(
    string ActivationKey,
    int MaxDevices = 1,
    DateTimeOffset? ExpiresAt = null,
    bool Suspended = false,
    bool Trial = false);
