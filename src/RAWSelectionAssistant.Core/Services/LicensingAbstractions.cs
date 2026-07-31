using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public interface ILicenseService
{
    LicenseState Current { get; }
    LicenseConfiguration Configuration { get; }
    event EventHandler? LicenseChanged;
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<LicenseProviderResult> ActivateAsync(string activationKey, CancellationToken cancellationToken = default);
    Task<LicenseProviderResult> DeactivateAsync(CancellationToken cancellationToken = default);
    Task<LicenseProviderResult> ValidateAsync(bool forceOnline = false, CancellationToken cancellationToken = default);
}

public interface ILicenseProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<LicenseProviderResult> ActivateAsync(LicenseActivationRequest request, CancellationToken cancellationToken);
    Task<LicenseProviderResult> ValidateAsync(LicenseValidationRequest request, CancellationToken cancellationToken);
    Task<LicenseProviderResult> DeactivateAsync(LicenseValidationRequest request, CancellationToken cancellationToken);
    LicenseProviderResult ValidateOffline(LicenseCredential credential, string anonymousDeviceFingerprint);
}

public interface IFeatureGateService
{
    LicenseState CurrentLicense { get; }
    bool HasAccess(LicensedFeature feature);
    FeatureAccessResult Check(LicensedFeature feature);
}

public interface ILicenseStorageService
{
    Task<LicenseCredential?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(LicenseCredential credential, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public interface IDeviceFingerprintService
{
    string DeviceName { get; }
    string GetAnonymousFingerprint();
}
