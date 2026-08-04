using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public interface IWeatherProvider
{
    string Name { get; }
    int MaximumForecastDays { get; }
    Task<WeatherProviderForecast> GetForecastAsync(WeatherLocation location, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
}

public interface IGeocodingProvider
{
    string Name { get; }
    Task<IReadOnlyList<WeatherLocationCandidate>> SearchAsync(string query, CancellationToken cancellationToken = default);
}

public interface IWeatherCacheStore
{
    Task<WeatherCacheRecord?> ReadAsync(string cacheKey, CancellationToken cancellationToken = default);
    Task WriteAsync(WeatherCacheRecord record, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public interface IWeatherForecastService
{
    Task<IReadOnlyList<WeatherLocationCandidate>> SearchLocationsAsync(string query, CancellationToken cancellationToken = default);
    void ConfirmLocation(Guid bookingId, WeatherLocationCandidate candidate);
    Task<BookingWeatherSummary> GetBookingWeatherAsync(Guid bookingId, DateTimeOffset startAtUtc, DateTimeOffset endAtUtc, bool forceRefresh = false, CancellationToken cancellationToken = default);
    Task<BookingWeatherSummary?> TryGetCachedBookingWeatherAsync(Guid bookingId, DateTimeOffset startAtUtc, DateTimeOffset endAtUtc, CancellationToken cancellationToken = default);
    Task ClearCacheAsync(CancellationToken cancellationToken = default);
}
