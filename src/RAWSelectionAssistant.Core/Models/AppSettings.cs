using System.Text.Json.Serialization;

namespace RAWSelectionAssistant.Core.Models;

public sealed class AppSettings
{
    public AppearanceSettings Appearance { get; set; } = new();
    public WeatherSettings Weather { get; set; } = new();
    [JsonPropertyName("reportSettings")]
    public ReportSettings ReportSettings { get; set; } = new();
    public List<string> PinnedQuickTools { get; set; } = QuickToolsService.DefaultPinnedTools.ToList();
    public QuickToolLayout QuickToolLayout { get; set; } = new();
    public List<string> RecentRawDirectories { get; set; } = [];
    public string RecentOutputDirectory { get; set; } = string.Empty;
    public OutputMode OutputMode { get; set; } = OutputMode.ByFileCategory;
    public List<string> CustomRawExtensions { get; set; } = [];
    public CollectionCategory DefaultCollectionCategory { get; set; } = CollectionCategory.JpegAndRaw;
    public List<string> EnabledJpegExtensions { get; set; } = [".JPG", ".JPEG"];
    public List<string> EnabledRawExtensions { get; set; } =
    [
        ".ARW", ".CR2", ".CR3", ".NEF", ".NRW", ".RAF", ".DNG", ".RW2",
        ".ORF", ".ORI", ".PEF", ".3FR", ".FFF", ".IIQ", ".SRW", ".RWL"
    ];
    public List<string> CustomExtensions { get; set; } = [];
    public bool AllowCustomerJpegFallback { get; set; }
    public CustomerJpegHandlingMode? CustomerJpegMode { get; set; }
    public List<SourceDirectorySetting> SourceDirectories { get; set; } = [];
    public double? WindowWidth { get; set; } = 1600;
    public double? WindowHeight { get; set; } = 920;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool AutoOpenOutputDirectory { get; set; }
    public string RecentProjectName { get; set; } = string.Empty;
    [JsonPropertyName("onboardingCompleted")]
    public bool OnboardingCompleted { get; set; }
    [JsonPropertyName("onboardingVersion")]
    public string OnboardingVersion { get; set; } = Branding.ProductVersion;
    [JsonPropertyName("onboardingCompletedAt")]
    public DateTimeOffset? OnboardingCompletedAt { get; set; }
    [JsonPropertyName("onboardingCurrentStep")]
    public int OnboardingCurrentStep { get; set; } = 1;
    [JsonPropertyName("onboardingLegacyUser")]
    public bool OnboardingLegacyUser { get; set; }
    [JsonPropertyName("onboardingUpgradeOfferShown")]
    public bool OnboardingUpgradeOfferShown { get; set; }
    [JsonPropertyName("onboardingCompletionProof")]
    public string OnboardingCompletionProof { get; set; } = string.Empty;
    [JsonPropertyName("onboardingTutorialOutputDirectory")]
    public string OnboardingTutorialOutputDirectory { get; set; } = string.Empty;
}

public sealed class WeatherSettings
{
    public bool Enabled { get; set; }
    public bool AutoRefreshEnabled { get; set; }
    public string Provider { get; set; } = "OpenMeteo";
    public string WeatherApiBaseUrl { get; set; } = "https://api.open-meteo.com/v1/forecast";
    public string GeocodingApiBaseUrl { get; set; } = "https://geocoding-api.open-meteo.com/v1/search";
    public string ApiKey { get; set; } = string.Empty;
    public Dictionary<string, WeatherLocation> BookingLocations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
