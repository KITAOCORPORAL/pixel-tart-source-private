using System.Net;
using System.Text;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version220WeatherTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-04T08:00:00Z");
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-08-05T10:00:00Z");
    private static readonly DateTimeOffset End = DateTimeOffset.Parse("2026-08-05T12:00:00Z");

    [TestMethod]
    public async Task Weather_DefaultsOff_AndDoesNotCallProviders()
    {
        var setup = Create(enabled: false);
        var summary = await setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End);
        Assert.AreEqual(WeatherAvailability.Disabled, summary.Availability);
        Assert.AreEqual(0, setup.Provider.CallCount);
        Assert.IsEmpty(await setup.Service.SearchLocationsAsync("上海"));
        Assert.AreEqual(0, setup.Geocoder.CallCount);
    }

    [TestMethod]
    public async Task UserEnable_SearchConfirm_ThenManualForecastWorks()
    {
        var setup = Create(enabled: false);
        setup.State.SetEnabled(true);
        var candidates = await setup.Service.SearchLocationsAsync("上海");
        Assert.HasCount(1, candidates);
        setup.Service.ConfirmLocation(setup.BookingId, candidates[0]);
        var summary = await setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End, true);
        Assert.AreEqual(WeatherAvailability.Available, summary.Availability);
        Assert.AreEqual("上海 · 上海 · 中国", summary.Location!.DisplayName);
        Assert.AreEqual(1, setup.Provider.CallCount);
    }

    [TestMethod]
    public async Task ConfirmedForecast_ContainsHourlyDailyAndDetailFields()
    {
        var setup = Create();
        var summary = await setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End, true);
        Assert.IsNotNull(summary.RepresentativeHour);
        Assert.IsNotNull(summary.Day);
        Assert.AreEqual(23, summary.RepresentativeHour.TemperatureC);
        Assert.AreEqual(25, summary.RepresentativeHour.ApparentTemperatureC);
        Assert.AreEqual(2.5, summary.RepresentativeHour.PrecipitationMm);
        Assert.AreEqual(68, summary.RepresentativeHour.RelativeHumidity);
        Assert.AreEqual(45, summary.RepresentativeHour.CloudCover);
        Assert.AreEqual(10_000, summary.RepresentativeHour.VisibilityMeters);
        Assert.IsNotNull(summary.Day.SunriseUtc);
        Assert.IsNotNull(summary.Day.SunsetUtc);
        Assert.AreEqual("FakeWeather", summary.Provider);
    }

    [TestMethod]
    [DataRow(65, 10, 20, 10_000, "0", "HighPrecipitation")]
    [DataRow(10, 45, 20, 10_000, "0", "StrongWind")]
    [DataRow(10, 10, 36, 10_000, "0", "HighTemperature")]
    [DataRow(10, 10, -1, 10_000, "0", "LowTemperature")]
    [DataRow(10, 10, 20, 1_000, "0", "LowVisibility")]
    [DataRow(10, 10, 20, 10_000, "95", "Thunderstorm")]
    public async Task CentralRiskThresholds_DetectPhotographyRisks(int rain, int wind, int temperature, int visibility, string code, string expected)
    {
        var forecast = Forecast(rain, wind, temperature, visibility, code);
        var setup = Create(forecast: forecast);
        var summary = await setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End, true);
        Assert.IsTrue(summary.Risks.Any(risk => risk.Code == expected));
    }

    [TestMethod]
    public async Task RiskNotification_ReusesNotificationCenter_AndNeverChangesBooking()
    {
        var center = new RecordingNotificationCenter();
        var setup = Create(forecast: Forecast(rain: 90), notificationCenter: center);
        await setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End, true);
        Assert.HasCount(1, center.Messages);
        StringAssert.Contains(center.Messages[0].Title, "天气风险");
        Assert.IsFalse(center.Messages[0].Message.Contains("取消", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task FreshCache_IsUsedForSixtyMinutesWithoutNetwork()
    {
        var setup = Create();
        var first = await setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End, true);
        var second = await setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End);
        Assert.AreEqual(1, setup.Provider.CallCount);
        Assert.IsFalse(first.IsFromCache);
        Assert.IsTrue(second.IsFromCache);
        Assert.IsFalse(second.IsStale);
    }

    [TestMethod]
    public async Task ExpiredCache_FallsBackWhenProviderIsOffline()
    {
        var setup = Create();
        await setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End, true);
        setup.Time.Advance(TimeSpan.FromMinutes(61));
        setup.Provider.Exception = new HttpRequestException("offline");
        var summary = await setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End);
        Assert.IsTrue(summary.IsFromCache);
        Assert.IsTrue(summary.IsStale);
        Assert.IsTrue(summary.Risks.Any(risk => risk.Code == "Stale"));
        StringAssert.Contains(summary.StatusMessage, "可能已过期");
    }

    [TestMethod]
    public async Task BrokenCache_IsIgnoredAndReplacedByProvider()
    {
        var setup = Create(cache: new MemoryCacheStore { ReturnCorruptAsNull = true });
        var summary = await setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End);
        Assert.AreEqual(WeatherAvailability.Available, summary.Availability);
        Assert.AreEqual(1, setup.Provider.CallCount);
    }

    [TestMethod]
    public async Task TimeoutRateLimitAndFormatFailure_DegradeWithoutThrowing()
    {
        foreach (var exception in new Exception[] { new TaskCanceledException("timeout"), new HttpRequestException("429"), new InvalidDataException("format") })
        {
            var setup = Create();
            setup.Provider.Exception = exception;
            var summary = await setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End, true);
            Assert.AreEqual(WeatherAvailability.Unavailable, summary.Availability);
            StringAssert.Contains(summary.StatusMessage, "暂时不可用");
        }
    }

    [TestMethod]
    public async Task MissingLocation_DoesNotQueryAndShowsCandidateInstruction()
    {
        var setup = Create(confirmLocation: false);
        var summary = await setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End);
        Assert.AreEqual(WeatherAvailability.LocationPending, summary.Availability);
        Assert.AreEqual(0, setup.Provider.CallCount);
        StringAssert.Contains(summary.StatusMessage, "地点待确认");
    }

    [TestMethod]
    public async Task PastAndFarFutureBookings_AreNeverPresentedAsReliableForecasts()
    {
        var setup = Create();
        var past = await setup.Service.GetBookingWeatherAsync(setup.BookingId, Now.AddDays(-2), Now.AddDays(-1));
        var future = await setup.Service.GetBookingWeatherAsync(setup.BookingId, Now.AddDays(17), Now.AddDays(18));
        Assert.AreEqual(WeatherAvailability.OutOfRange, past.Availability);
        Assert.AreEqual(WeatherAvailability.OutOfRange, future.Availability);
        Assert.AreEqual(0, setup.Provider.CallCount);
        StringAssert.Contains(future.StatusMessage, "没有可靠天气预报");
    }

    [TestMethod]
    public async Task ConcurrentIdenticalRequests_AreDeduplicated()
    {
        var setup = Create();
        setup.Provider.Delay = TimeSpan.FromMilliseconds(80);
        var first = setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End, true);
        var second = setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End, true);
        await Task.WhenAll(first, second);
        Assert.AreEqual(1, setup.Provider.CallCount);
    }

    [TestMethod]
    public async Task ImmediateManualRepeat_IsSuppressedAndUsesCache()
    {
        var setup = Create();
        await setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End, true);
        var second = await setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End, true);
        Assert.AreEqual(1, setup.Provider.CallCount);
        Assert.IsTrue(second.IsFromCache);
        StringAssert.Contains(second.StatusMessage, "相同请求已合并");
    }

    [TestMethod]
    public async Task ClearWeatherCache_OnlyClearsWeatherStore()
    {
        var cache = new MemoryCacheStore();
        var setup = Create(cache: cache);
        await setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End, true);
        await setup.Service.ClearCacheAsync();
        Assert.AreEqual(1, cache.ClearCount);
        Assert.IsEmpty(cache.Records);
    }

    [TestMethod]
    public async Task FileCache_UsesHashedNameAndRecoversFromInvalidJson()
    {
        using var temp = new TempDirectory();
        var store = new JsonWeatherCacheStore(temp.Path);
        var key = "Open-Meteo|31.2304|121.4737|Asia/Shanghai|2026-08-05";
        var record = new WeatherCacheRecord(key, Forecast(), Now);
        await store.WriteAsync(record);
        var file = Directory.GetFiles(temp.Path, "*.json").Single();
        Assert.IsFalse(Path.GetFileName(file).Contains("Shanghai", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(Path.GetFileName(file).Contains("31.2304", StringComparison.Ordinal));
        File.WriteAllText(file, "{invalid", Encoding.UTF8);
        Assert.IsNull(await store.ReadAsync(key));
    }

    [TestMethod]
    public void FeatureState_PersistsConfirmedLocationAndRefreshMarker()
    {
        var id = Guid.NewGuid();
        var state = new WeatherFeatureState();
        state.Apply(new WeatherSettings { Enabled = true, AutoRefreshEnabled = true });
        state.ConfirmLocation(id, Location());
        state.MarkNeedsRefresh(id);
        var snapshot = state.Snapshot();
        Assert.IsTrue(snapshot.Enabled);
        Assert.IsTrue(snapshot.AutoRefreshEnabled);
        Assert.IsTrue(snapshot.BookingLocations.ContainsKey(id.ToString("D")));
        Assert.IsTrue(state.NeedsRefresh(id));
        state.MarkFresh(id);
        Assert.IsFalse(state.NeedsRefresh(id));
    }

    [TestMethod]
    public async Task WeatherAudit_ContainsOnlyProviderBookingOperationResultAndGenericCode()
    {
        var audit = new RecordingAudit();
        var setup = Create(audit: audit);
        await setup.Service.GetBookingWeatherAsync(setup.BookingId, Start, End, true);
        var text = string.Join('\n', audit.Messages);
        StringAssert.Contains(text, "Provider=FakeWeather");
        StringAssert.Contains(text, setup.BookingId.ToString("D"));
        Assert.IsFalse(text.Contains("上海", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("31.2304", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("121.4737", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("客户", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task OpenMeteoRequest_ContainsOnlyWeatherInputsAndConfigurableBase()
    {
        var handler = new RecordingHandler("""
            {"hourly":{"time":["2026-08-05T10:00"],"weather_code":[0],"temperature_2m":[23],"apparent_temperature":[24],"precipitation_probability":[10],"precipitation":[0],"wind_speed_10m":[8],"wind_gusts_10m":[12],"relative_humidity_2m":[60],"cloud_cover":[20],"visibility":[10000]},"daily":{"time":["2026-08-05"],"weather_code":[0],"temperature_2m_max":[28],"temperature_2m_min":[19],"precipitation_probability_max":[10],"sunrise":["2026-08-05T21:00"],"sunset":["2026-08-06T10:00"]}}
            """);
        var client = new HttpClient(handler);
        var provider = new OpenMeteoWeatherProvider(client, new OpenMeteoOptions("https://weather.invalid/forecast", "https://geo.invalid/search"));
        var result = await provider.GetForecastAsync(Location(), new(2026, 8, 5), new(2026, 8, 5));
        Assert.HasCount(1, result.Hourly);
        var uri = handler.LastUri!.ToString();
        StringAssert.StartsWith(uri, "https://weather.invalid/forecast");
        StringAssert.Contains(uri, "latitude=31.2304");
        StringAssert.Contains(uri, "start_date=2026-08-05");
        Assert.IsFalse(uri.Contains("客户", StringComparison.Ordinal));
        Assert.IsFalse(uri.Contains("apikey=", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task OpenMeteoGeocoding_UsesUserQueryOnlyAfterExplicitCall()
    {
        var handler = new RecordingHandler("""{"results":[{"id":1,"name":"上海","admin1":"上海","country":"中国","latitude":31.2304,"longitude":121.4737,"timezone":"Asia/Shanghai"}]}""");
        var provider = new OpenMeteoGeocodingProvider(new HttpClient(handler), new OpenMeteoOptions(GeocodingBaseUrl: "https://geo.invalid/search"));
        Assert.IsNull(handler.LastUri);
        var result = await provider.SearchAsync("上海");
        Assert.HasCount(1, result);
        StringAssert.Contains(handler.LastUri!.Query, "name=%E4%B8%8A%E6%B5%B7");
    }

    [TestMethod]
    public void ReleaseComposition_UsesOpenMeteoAndNeverFakeWeatherProvider()
    {
        var root = Root();
        var app = File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant", "App.xaml.cs"));
        StringAssert.Contains(app, "new OpenMeteoWeatherProvider");
        StringAssert.Contains(app, "new OpenMeteoGeocodingProvider");
        Assert.IsFalse(app.Contains("FakeWeatherProvider", StringComparison.Ordinal));
        Assert.IsFalse(app.Contains("localhost", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void WeatherArchitecture_HasNoSchemaOrBackgroundService()
    {
        var root = Root();
        var source = File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant.Core", "Services", "WeatherServices.cs"));
        foreach (var forbidden in new[] { "CREATE TABLE", "SchemaVersion", "BackgroundService", "ServiceBase", "TaskScheduler" })
            Assert.IsFalse(source.Contains(forbidden, StringComparison.OrdinalIgnoreCase), forbidden);
    }

    private static Setup Create(bool enabled = true, bool confirmLocation = true, WeatherProviderForecast? forecast = null,
        MemoryCacheStore? cache = null, INotificationCenter? notificationCenter = null, RecordingAudit? audit = null)
    {
        var id = Guid.NewGuid();
        var state = new WeatherFeatureState();
        state.Apply(new WeatherSettings { Enabled = enabled });
        if (confirmLocation) state.ConfirmLocation(id, Location());
        var provider = new FakeWeatherProvider(forecast ?? Forecast());
        var geocoder = new FakeGeocoder();
        var store = cache ?? new MemoryCacheStore();
        var time = new MutableWeatherTimeProvider(Now);
        var service = new WeatherForecastService(provider, geocoder, store, state, notificationCenter, audit, time);
        return new(id, state, provider, geocoder, store, time, service);
    }

    private static WeatherLocation Location() => new("上海", "上海", "中国", 31.2304, 121.4737, "Asia/Shanghai", "FakeWeather");

    private static WeatherProviderForecast Forecast(int rain = 20, int wind = 12, int temperature = 23, int visibility = 10_000, string code = "1")
    {
        var hour = new HourlyWeatherForecast(Start.AddHours(1), code, temperature, 25, rain, 2.5, wind, wind + 5, 68, 45, visibility);
        var day = new DailyWeatherForecast(new(2026, 8, 5), code, 19, 29, rain, DateTimeOffset.Parse("2026-08-04T21:10:00Z"), DateTimeOffset.Parse("2026-08-05T10:45:00Z"));
        return new(Location(), Now, [hour], [day], "FakeWeather");
    }

    private sealed record Setup(Guid BookingId, WeatherFeatureState State, FakeWeatherProvider Provider, FakeGeocoder Geocoder,
        MemoryCacheStore Cache, MutableWeatherTimeProvider Time, WeatherForecastService Service);

    private sealed class FakeWeatherProvider(WeatherProviderForecast forecast) : IWeatherProvider
    {
        public string Name => "FakeWeather";
        public int MaximumForecastDays => 16;
        public int CallCount => _calls;
        public Exception? Exception { get; set; }
        public TimeSpan Delay { get; set; }
        public async Task<WeatherProviderForecast> GetForecastAsync(WeatherLocation location, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            if (Delay > TimeSpan.Zero) await Task.Delay(Delay, cancellationToken);
            if (Exception is not null) throw Exception;
            return forecast;
        }
        private int _calls;
    }

    private sealed class FakeGeocoder : IGeocodingProvider
    {
        public string Name => "FakeGeocoder";
        public int CallCount { get; private set; }
        public Task<IReadOnlyList<WeatherLocationCandidate>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<WeatherLocationCandidate>>([new("shanghai", "上海", "上海", "中国", 31.2304, 121.4737, "Asia/Shanghai", Name)]);
        }
    }

    private sealed class MemoryCacheStore : IWeatherCacheStore
    {
        public Dictionary<string, WeatherCacheRecord> Records { get; } = new(StringComparer.Ordinal);
        public bool ReturnCorruptAsNull { get; init; }
        public int ClearCount { get; private set; }
        public Task<WeatherCacheRecord?> ReadAsync(string cacheKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(ReturnCorruptAsNull ? null : Records.GetValueOrDefault(cacheKey));
        public Task WriteAsync(WeatherCacheRecord record, CancellationToken cancellationToken = default) { Records[record.CacheKey] = record; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { ClearCount++; Records.Clear(); return Task.CompletedTask; }
    }

    private sealed class MutableWeatherTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan value) => _now += value;
    }

    private sealed class RecordingAudit : IAuditLogService
    {
        public List<string> Messages { get; } = [];
        public Task WriteAsync(string category, string eventType, string severity, string message, Guid? taskId = null, Guid? projectId = null, string? errorCode = null, string? correlationId = null, CancellationToken cancellationToken = default)
        { Messages.Add($"{category}|{eventType}|{message}|{errorCode}"); return Task.CompletedTask; }
    }

    private sealed class RecordingNotificationCenter : INotificationCenter
    {
        public event EventHandler<NotificationMessage>? Published;
        public List<NotificationMessage> Messages { get; } = [];
        public Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default) { Messages.Add(message); Published?.Invoke(this, message); return Task.CompletedTask; }
        public void NotifyPersisted(NotificationMessage message) { Messages.Add(message); Published?.Invoke(this, message); }
        public Task<IReadOnlyList<NotificationMessage>> GetHistoryAsync(int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NotificationMessage>>(Messages);
        public Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") });
        }
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
