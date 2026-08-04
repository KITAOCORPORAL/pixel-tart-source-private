namespace RAWSelectionAssistant.Core.Models;

public enum WeatherAvailability
{
    Disabled,
    LocationPending,
    Available,
    Cached,
    Stale,
    OutOfRange,
    Unavailable
}

public sealed record WeatherLocationCandidate(
    string Id,
    string Name,
    string? AdministrativeArea,
    string Country,
    double Latitude,
    double Longitude,
    string TimeZoneId,
    string Provider)
{
    public string DisplayName => string.Join(" · ", new[] { Name, AdministrativeArea, Country }.Where(value => !string.IsNullOrWhiteSpace(value)));
}

public sealed record WeatherLocation(
    string Name,
    string? AdministrativeArea,
    string Country,
    double Latitude,
    double Longitude,
    string TimeZoneId,
    string Provider)
{
    public string DisplayName => string.Join(" · ", new[] { Name, AdministrativeArea, Country }.Where(value => !string.IsNullOrWhiteSpace(value)));
}

public sealed record HourlyWeatherForecast(
    DateTimeOffset AtUtc,
    string WeatherCode,
    double TemperatureC,
    double ApparentTemperatureC,
    int PrecipitationProbability,
    double PrecipitationMm,
    double WindSpeedKph,
    double WindGustKph,
    int RelativeHumidity,
    int CloudCover,
    double VisibilityMeters);

public sealed record DailyWeatherForecast(
    DateOnly Date,
    string WeatherCode,
    double MinimumTemperatureC,
    double MaximumTemperatureC,
    int PrecipitationProbability,
    DateTimeOffset? SunriseUtc,
    DateTimeOffset? SunsetUtc);

public sealed record WeatherRiskNotice(string Code, string Message, NotificationSeverity Severity);

public sealed record WeatherProviderForecast(
    WeatherLocation Location,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<HourlyWeatherForecast> Hourly,
    IReadOnlyList<DailyWeatherForecast> Daily,
    string Provider);

public sealed record BookingWeatherSummary(
    Guid BookingId,
    WeatherAvailability Availability,
    WeatherLocation? Location,
    HourlyWeatherForecast? RepresentativeHour,
    DailyWeatherForecast? Day,
    IReadOnlyList<HourlyWeatherForecast> Hourly,
    IReadOnlyList<DailyWeatherForecast> Daily,
    IReadOnlyList<WeatherRiskNotice> Risks,
    DateTimeOffset? UpdatedAtUtc,
    string Provider,
    bool IsFromCache,
    bool IsStale,
    bool NeedsRefresh,
    string StatusMessage);

public sealed record WeatherCacheRecord(
    string CacheKey,
    WeatherProviderForecast Forecast,
    DateTimeOffset QueriedAtUtc);

public sealed record WeatherRiskThresholds(
    int HighPrecipitationProbability = 60,
    double StrongWindKph = 40,
    double HighTemperatureC = 35,
    double LowTemperatureC = 0,
    double LowVisibilityMeters = 2000)
{
    public static WeatherRiskThresholds Default { get; } = new();
}
