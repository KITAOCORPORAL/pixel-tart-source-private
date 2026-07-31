using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class UnavailableLicenseProvider(string message = "授权服务尚未配置") : ILicenseProvider
{
    public string Name => "None";
    public bool IsConfigured => false;

    public Task<LicenseProviderResult> ActivateAsync(LicenseActivationRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(NotConfigured());
    public Task<LicenseProviderResult> ValidateAsync(LicenseValidationRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(NotConfigured());
    public Task<LicenseProviderResult> DeactivateAsync(LicenseValidationRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(NotConfigured());
    public LicenseProviderResult ValidateOffline(LicenseCredential credential, string anonymousDeviceFingerprint) => NotConfigured();

    private LicenseProviderResult NotConfigured() => LicenseProviderResult.Failure(
        LicenseStatus.ActivationRequired,
        message,
        LicenseFailureReason.NotConfigured);
}
