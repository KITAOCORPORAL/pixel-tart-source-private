using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public static class LicenseProviderFactory
{
    public static ILicenseProvider Create(
        LicenseConfiguration configuration,
        HttpClient httpClient,
        ILogService logService,
        bool allowMockProvider = false,
        Func<ILicenseProvider>? mockProviderFactory = null)
    {
        if (configuration.Provider.Equals("Cryptolens", StringComparison.OrdinalIgnoreCase))
        {
            return configuration.IsCryptolensConfigured
                ? new CryptolensLicenseProvider(configuration, httpClient)
                : new UnavailableLicenseProvider("Cryptolens 授权参数尚未配置完整。");
        }

        if (configuration.Provider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            return allowMockProvider && mockProviderFactory is not null
                ? mockProviderFactory()
                : new UnavailableLicenseProvider("正式版本禁止使用 Mock 专业版权限。");
        }

        return new UnavailableLicenseProvider("授权服务尚未配置。");
    }
}
