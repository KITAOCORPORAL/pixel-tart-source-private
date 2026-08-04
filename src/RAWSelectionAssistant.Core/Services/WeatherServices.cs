using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Services;

public sealed class WeatherFeatureState
{
    private readonly object _sync = new();
    private WeatherSettings _settings = new();
    private readonly HashSet<Guid> _needsRefresh = [];

    public bool Enabled { get { lock (_sync) return _settings.Enabled; } }
    public bool AutoRefreshEnabled { get { lock (_sync) return _settings.AutoRefreshEnabled; } }

    public void Apply(WeatherSettings? settings)
    {
        lock (_sync)
        {
            _settings = settings ?? new WeatherSettings();
            _settings.BookingLocations ??= new(StringComparer.OrdinalIgnoreCase);
        }
    }

    public WeatherSettings Snapshot()
    {
        lock (_sync)
        {
            return new WeatherSettings
            {
                Enabled = _settings.Enabled,
                AutoRefreshEnabled = _settings.AutoRefreshEnabled,
                Provider = _settings.Provider,
                WeatherApiBaseUrl = _settings.WeatherApiBaseUrl,
                GeocodingApiBaseUrl = _settings.GeocodingApiBaseUrl,
                ApiKey = _settings.ApiKey,
                BookingLocations = new(_settings.BookingLocations, StringComparer.OrdinalIgnoreCase)
            };
        }
    }

    public void SetEnabled(bool enabled) { lock (_sync) _settings.Enabled = enabled; }
    public void SetAutoRefresh(bool enabled) { lock (_sync) _settings.AutoRefreshEnabled = enabled; }

    public WeatherLocation? GetLocation(Guid bookingId)
    {
        lock (_sync) return _settings.BookingLocations.TryGetValue(bookingId.ToString("D"), out var location) ? location : null;
    }

    public void ConfirmLocation(Guid bookingId, WeatherLocation location)
    {
        lock (_sync)
        {
            _settings.BookingLocations[bookingId.ToString("D")] = location;
            _needsRefresh.Remove(bookingId);
        }
    }

    public void MarkNeedsRefresh(Guid bookingId) { lock (_sync) _needsRefresh.Add(bookingId); }
    public void MarkFresh(Guid bookingId) { lock (_sync) _needsRefresh.Remove(bookingId); }
    public bool NeedsRefresh(Guid bookingId) { lock (_sync) return _needsRefresh.Contains(bookingId); }
}

public sealed class JsonWeatherCacheStore(string? rootDirectory = null) : IWeatherCacheStore
{
    private readonly string _root = rootDirectory ?? AppDataPaths.WeatherCacheDirectory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General) { WriteIndented = true };

    public async Task<WeatherCacheRecord?> ReadAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        var path = PathFor(cacheKey);
        if (!File.Exists(path)) return null;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await JsonSerializer.DeserializeAsync<WeatherCacheRecord>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { return null; }
    }

    public async Task WriteAsync(WeatherCacheRecord record, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        var path = PathFor(record.CacheKey);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, record, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_root)) return Task.CompletedTask;
        foreach (var file in Directory.EnumerateFiles(_root, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { File.Delete(file); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return Task.CompletedTask;
    }

    private string PathFor(string cacheKey)
    {
        var safe = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey)));
        return Path.Combine(_root, safe + ".json");
    }
}

public sealed record OpenMeteoOptions(
    string ForecastBaseUrl = "https://api.open-meteo.com/v1/forecast",
    string GeocodingBaseUrl = "https://geocoding-api.open-meteo.com/v1/search",
    string? ApiKey = null);

public sealed class OpenMeteoGeocodingProvider(HttpClient httpClient, OpenMeteoOptions? options = null) : IGeocodingProvider
{
    private readonly OpenMeteoOptions _options = options ?? new();
    public string Name => "Open-Meteo";

    public async Task<IReadOnlyList<WeatherLocationCandidate>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var uri = $"{_options.GeocodingBaseUrl}?name={Uri.EscapeDataString(query.Trim())}&count=8&language=zh&format=json{ApiKey()}";
        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array) return [];
        var output = new List<WeatherLocationCandidate>();
        foreach (var row in results.EnumerateArray())
        {
            if (!row.TryGetProperty("latitude", out var latitude) || !row.TryGetProperty("longitude", out var longitude)) continue;
            var name = Text(row, "name") ?? "未命名地点";
            var admin = Text(row, "admin1");
            var country = Text(row, "country") ?? string.Empty;
            var timezone = Text(row, "timezone") ?? "UTC";
            var id = Text(row, "id") ?? $"{latitude.GetDouble():F4},{longitude.GetDouble():F4}";
            output.Add(new(id, name, admin, country, latitude.GetDouble(), longitude.GetDouble(), timezone, Name));
        }
        return output;
    }

    private string ApiKey() => string.IsNullOrWhiteSpace(_options.ApiKey) ? string.Empty : "&apikey=" + Uri.EscapeDataString(_options.ApiKey);
    private static string? Text(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

public sealed class OpenMeteoWeatherProvider(HttpClient httpClient, OpenMeteoOptions? options = null) : IWeatherProvider
{
    private readonly OpenMeteoOptions _options = options ?? new();
    public string Name => "Open-Meteo";
    public int MaximumForecastDays => 16;

    public async Task<WeatherProviderForecast> GetForecastAsync(WeatherLocation location, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var hourly = "temperature_2m,apparent_temperature,precipitation_probability,precipitation,weather_code,relative_humidity_2m,cloud_cover,visibility,wind_speed_10m,wind_gusts_10m";
        var daily = "weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max,sunrise,sunset";
        var uri = $"{_options.ForecastBaseUrl}?latitude={location.Latitude.ToString(CultureInfo.InvariantCulture)}&longitude={location.Longitude.ToString(CultureInfo.InvariantCulture)}&hourly={hourly}&daily={daily}&timezone=UTC&start_date={fromDate:yyyy-MM-dd}&end_date={toDate:yyyy-MM-dd}{ApiKey()}";
        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var generated = DateTimeOffset.UtcNow;
        var hours = ParseHourly(root.GetProperty("hourly"));
        var days = ParseDaily(root.GetProperty("daily"));
        return new(location, generated, hours, days, Name);
    }

    private string ApiKey() => string.IsNullOrWhiteSpace(_options.ApiKey) ? string.Empty : "&apikey=" + Uri.EscapeDataString(_options.ApiKey);

    private static IReadOnlyList<HourlyWeatherForecast> ParseHourly(JsonElement hourly)
    {
        var times = hourly.GetProperty("time").EnumerateArray().Select(value => DateTimeOffset.Parse(value.GetString()! + "Z", CultureInfo.InvariantCulture)).ToArray();
        var codes = Ints(hourly, "weather_code");
        var temperature = Doubles(hourly, "temperature_2m");
        var apparent = Doubles(hourly, "apparent_temperature");
        var probability = Ints(hourly, "precipitation_probability");
        var precipitation = Doubles(hourly, "precipitation");
        var wind = Doubles(hourly, "wind_speed_10m");
        var gust = Doubles(hourly, "wind_gusts_10m");
        var humidity = Ints(hourly, "relative_humidity_2m");
        var cloud = Ints(hourly, "cloud_cover");
        var visibility = Doubles(hourly, "visibility");
        return Enumerable.Range(0, times.Length).Select(index => new HourlyWeatherForecast(times[index], codes[index].ToString(CultureInfo.InvariantCulture), temperature[index], apparent[index], probability[index], precipitation[index], wind[index], gust[index], humidity[index], cloud[index], visibility[index])).ToArray();
    }

    private static IReadOnlyList<DailyWeatherForecast> ParseDaily(JsonElement daily)
    {
        var dates = daily.GetProperty("time").EnumerateArray().Select(value => DateOnly.Parse(value.GetString()!, CultureInfo.InvariantCulture)).ToArray();
        var codes = Ints(daily, "weather_code");
        var maximum = Doubles(daily, "temperature_2m_max");
        var minimum = Doubles(daily, "temperature_2m_min");
        var probability = Ints(daily, "precipitation_probability_max");
        var sunrise = Dates(daily, "sunrise");
        var sunset = Dates(daily, "sunset");
        return Enumerable.Range(0, dates.Length).Select(index => new DailyWeatherForecast(dates[index], codes[index].ToString(CultureInfo.InvariantCulture), minimum[index], maximum[index], probability[index], sunrise[index], sunset[index])).ToArray();
    }

    private static double[] Doubles(JsonElement parent, string name) => parent.GetProperty(name).EnumerateArray().Select(value => value.ValueKind == JsonValueKind.Number ? value.GetDouble() : 0).ToArray();
    private static int[] Ints(JsonElement parent, string name) => parent.GetProperty(name).EnumerateArray().Select(value => value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0).ToArray();
    private static DateTimeOffset?[] Dates(JsonElement parent, string name) => parent.GetProperty(name).EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String ? DateTimeOffset.Parse(value.GetString()! + "Z", CultureInfo.InvariantCulture) : (DateTimeOffset?)null).ToArray();
}

public sealed class WeatherForecastService(
    IWeatherProvider weatherProvider,
    IGeocodingProvider geocodingProvider,
    IWeatherCacheStore cacheStore,
    WeatherFeatureState featureState,
    INotificationCenter? notificationCenter = null,
    IAuditLogService? auditLog = null,
    TimeProvider? timeProvider = null,
    WeatherRiskThresholds? thresholds = null) : IWeatherForecastService
{
    public static readonly TimeSpan FreshCacheDuration = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan RequestSuppressionWindow = TimeSpan.FromSeconds(2);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly WeatherRiskThresholds _thresholds = thresholds ?? WeatherRiskThresholds.Default;
    private readonly ConcurrentDictionary<string, Task<WeatherProviderForecast>> _requests = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRequests = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<WeatherLocationCandidate>> SearchLocationsAsync(string query, CancellationToken cancellationToken = default)
    {
        if (!featureState.Enabled) return Task.FromResult<IReadOnlyList<WeatherLocationCandidate>>([]);
        return geocodingProvider.SearchAsync(query, cancellationToken);
    }

    public void ConfirmLocation(Guid bookingId, WeatherLocationCandidate candidate) =>
        featureState.ConfirmLocation(bookingId, new(candidate.Name, candidate.AdministrativeArea, candidate.Country, candidate.Latitude, candidate.Longitude, candidate.TimeZoneId, candidate.Provider));

    public async Task<BookingWeatherSummary> GetBookingWeatherAsync(Guid bookingId, DateTimeOffset startAtUtc, DateTimeOffset endAtUtc, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (!featureState.Enabled) return Empty(bookingId, WeatherAvailability.Disabled, "尚未启用天气。", needsRefresh: false);
        var location = featureState.GetLocation(bookingId);
        if (location is null) return Empty(bookingId, WeatherAvailability.LocationPending, "地点待确认。", needsRefresh: true);
        var now = _timeProvider.GetUtcNow();
        if (endAtUtc <= now || startAtUtc > now.AddDays(weatherProvider.MaximumForecastDays))
            return Empty(bookingId, WeatherAvailability.OutOfRange, "当前日期暂时没有可靠天气预报，请临近拍摄时再查看。", needsRefresh: true, location);

        var from = DateOnly.FromDateTime(startAtUtc.UtcDateTime);
        var to = DateOnly.FromDateTime(endAtUtc.UtcDateTime);
        var cacheKey = CacheKey(location, from, to, weatherProvider.Name);
        var cached = await cacheStore.ReadAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        var stale = cached is not null && now - cached.QueriedAtUtc > FreshCacheDuration;
        if (!forceRefresh && !featureState.NeedsRefresh(bookingId) && cached is not null && !stale)
            return Build(bookingId, startAtUtc, endAtUtc, cached.Forecast, cached.QueriedAtUtc, true, false, false, "使用60分钟内的天气缓存。");

        if (forceRefresh && _lastRequests.TryGetValue(cacheKey, out var last) && now - last < RequestSuppressionWindow && cached is not null)
            return Build(bookingId, startAtUtc, endAtUtc, cached.Forecast, cached.QueriedAtUtc, true, stale, featureState.NeedsRefresh(bookingId), "相同请求已合并，显示最近缓存。");

        try
        {
            _lastRequests[cacheKey] = now;
            var request = _requests.GetOrAdd(cacheKey, _ => weatherProvider.GetForecastAsync(location, from, to, cancellationToken));
            WeatherProviderForecast forecast;
            try { forecast = await request.ConfigureAwait(false); }
            finally { _requests.TryRemove(cacheKey, out _); }
            var record = new WeatherCacheRecord(cacheKey, forecast, now);
            await cacheStore.WriteAsync(record, cancellationToken).ConfigureAwait(false);
            featureState.MarkFresh(bookingId);
            await WriteAuditAsync(bookingId, "ForecastRequested", true, null, cancellationToken).ConfigureAwait(false);
            var summary = Build(bookingId, startAtUtc, endAtUtc, forecast, now, false, false, false, "天气已更新。");
            await PublishRisksAsync(summary, cancellationToken).ConfigureAwait(false);
            return summary;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await WriteAuditAsync(bookingId, "ForecastRequested", false, "WEATHER_PROVIDER_UNAVAILABLE", CancellationToken.None).ConfigureAwait(false);
            if (cached is not null) return Build(bookingId, startAtUtc, endAtUtc, cached.Forecast, cached.QueriedAtUtc, true, true, featureState.NeedsRefresh(bookingId), "天气服务暂时不可用，正在显示最近缓存；天气数据可能已过期。");
            return Empty(bookingId, WeatherAvailability.Unavailable, "天气服务暂时不可用。", needsRefresh: true, location);
        }
    }

    public async Task<BookingWeatherSummary?> TryGetCachedBookingWeatherAsync(Guid bookingId, DateTimeOffset startAtUtc, DateTimeOffset endAtUtc, CancellationToken cancellationToken = default)
    {
        if (!featureState.Enabled || featureState.GetLocation(bookingId) is not { } location) return null;
        var from = DateOnly.FromDateTime(startAtUtc.UtcDateTime);
        var to = DateOnly.FromDateTime(endAtUtc.UtcDateTime);
        var record = await cacheStore.ReadAsync(CacheKey(location, from, to, weatherProvider.Name), cancellationToken).ConfigureAwait(false);
        if (record is null) return null;
        var stale = _timeProvider.GetUtcNow() - record.QueriedAtUtc > FreshCacheDuration;
        return Build(bookingId, startAtUtc, endAtUtc, record.Forecast, record.QueriedAtUtc, true, stale, featureState.NeedsRefresh(bookingId), stale ? "天气数据可能已过期。" : "使用天气缓存。");
    }

    public Task ClearCacheAsync(CancellationToken cancellationToken = default) => cacheStore.ClearAsync(cancellationToken);

    private BookingWeatherSummary Build(Guid bookingId, DateTimeOffset startAtUtc, DateTimeOffset endAtUtc, WeatherProviderForecast forecast, DateTimeOffset updatedAtUtc, bool fromCache, bool stale, bool needsRefresh, string status)
    {
        var overlap = forecast.Hourly.Where(hour => hour.AtUtc >= startAtUtc.AddHours(-1) && hour.AtUtc <= endAtUtc.AddHours(1)).ToArray();
        var midpoint = startAtUtc + TimeSpan.FromTicks((endAtUtc - startAtUtc).Ticks / 2);
        var representative = overlap.OrderBy(hour => Math.Abs((hour.AtUtc - midpoint).Ticks)).FirstOrDefault();
        var day = forecast.Daily.FirstOrDefault(value => value.Date == DateOnly.FromDateTime(startAtUtc.UtcDateTime));
        var risks = EvaluateRisks(representative, day, stale);
        var availability = stale ? WeatherAvailability.Stale : fromCache ? WeatherAvailability.Cached : WeatherAvailability.Available;
        return new(bookingId, availability, forecast.Location, representative, day, overlap, forecast.Daily, risks, updatedAtUtc, forecast.Provider, fromCache, stale, needsRefresh, status);
    }

    private IReadOnlyList<WeatherRiskNotice> EvaluateRisks(HourlyWeatherForecast? hour, DailyWeatherForecast? day, bool stale)
    {
        var risks = new List<WeatherRiskNotice>();
        if (hour is not null)
        {
            if (hour.PrecipitationProbability >= _thresholds.HighPrecipitationProbability) risks.Add(new("HighPrecipitation", "较高降雨概率，仅供拍摄准备参考。", NotificationSeverity.Warning));
            if (Math.Max(hour.WindSpeedKph, hour.WindGustKph) >= _thresholds.StrongWindKph) risks.Add(new("StrongWind", "可能有强风，请检查灯架和户外设备。", NotificationSeverity.Warning));
            if (hour.TemperatureC >= _thresholds.HighTemperatureC) risks.Add(new("HighTemperature", "可能高温，请准备防暑措施。", NotificationSeverity.Warning));
            if (hour.TemperatureC <= _thresholds.LowTemperatureC) risks.Add(new("LowTemperature", "可能低温，请准备保暖和电池保护。", NotificationSeverity.Warning));
            if (hour.VisibilityMeters > 0 && hour.VisibilityMeters < _thresholds.LowVisibilityMeters) risks.Add(new("LowVisibility", "能见度可能较低，请预留交通和灯光方案。", NotificationSeverity.Warning));
            if (hour.WeatherCode is "95" or "96" or "99") risks.Add(new("Thunderstorm", "可能有雷暴或恶劣天气，请关注临近预报。", NotificationSeverity.Error));
        }
        if (hour is null && day is null) risks.Add(new("ForecastUnavailable", "暂无可用天气预报。", NotificationSeverity.Warning));
        if (stale) risks.Add(new("Stale", "天气数据可能已过期。", NotificationSeverity.Warning));
        return risks;
    }

    private async Task PublishRisksAsync(BookingWeatherSummary summary, CancellationToken cancellationToken)
    {
        if (notificationCenter is null || summary.Risks.Count == 0) return;
        var message = string.Join("；", summary.Risks.Select(risk => risk.Message));
        await notificationCenter.PublishAsync(new NotificationMessage(Guid.NewGuid(), NotificationType.InlineError,
            summary.Risks.Max(risk => risk.Severity), "拍摄天气风险参考", message, null, null, [], false,
            _timeProvider.GetUtcNow(), _timeProvider.GetUtcNow().AddHours(24), $"booking-weather:{summary.BookingId:D}"), cancellationToken).ConfigureAwait(false);
    }

    private Task WriteAuditAsync(Guid bookingId, string operation, bool success, string? errorCode, CancellationToken cancellationToken) =>
        auditLog?.WriteAsync("Weather", operation, success ? "Information" : "Warning",
            $"Provider={weatherProvider.Name};BookingId={bookingId:D};Operation={operation};Result={(success ? "Succeeded" : "Failed")}",
            errorCode: errorCode, cancellationToken: cancellationToken) ?? Task.CompletedTask;

    private BookingWeatherSummary Empty(Guid bookingId, WeatherAvailability availability, string message, bool needsRefresh, WeatherLocation? location = null) =>
        new(bookingId, availability, location, null, null, [], [],
            availability == WeatherAvailability.Unavailable ? [new("ForecastUnavailable", "暂无可用天气预报。", NotificationSeverity.Warning)] : [],
            null, weatherProvider.Name, false, false, needsRefresh, message);

    private static string CacheKey(WeatherLocation location, DateOnly from, DateOnly to, string provider) =>
        string.Join('|', provider, location.Latitude.ToString("F4", CultureInfo.InvariantCulture), location.Longitude.ToString("F4", CultureInfo.InvariantCulture), location.TimeZoneId, from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
}
