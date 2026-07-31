using System.Text.Json;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class LicenseConfigurationService(ILogService? logService = null, string? configurationPath = null)
{
    private readonly string _configurationPath = configurationPath ?? Path.Combine(AppContext.BaseDirectory, "appsettings.license.json");
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public LicenseConfiguration Load()
    {
        if (!File.Exists(_configurationPath)) return new LicenseConfiguration();
        try
        {
            return JsonSerializer.Deserialize<LicenseConfiguration>(File.ReadAllText(_configurationPath), Options)
                   ?? new LicenseConfiguration();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logService?.Error("授权配置无法读取，软件将以免费版启动。", ex);
            return new LicenseConfiguration();
        }
    }
}
