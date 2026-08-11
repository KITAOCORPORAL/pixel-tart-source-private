using System.IO;
using System.Net.Http;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version220WeatherUiTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-08-05T10:00:00Z");
    private static readonly DateTimeOffset End = DateTimeOffset.Parse("2026-08-05T12:00:00Z");

    [TestMethod]
    public void MonthCalendar_KeepsTaskCardCompactAndShowsDayWeatherGlyph()
    {
        var item = new CalendarBookingItemViewModel(Booking(), Weather(strongWind: true));
        Assert.AreEqual("🌤", item.MonthWeatherIcon);
        Assert.AreEqual("摄影项目", item.Title);
        var xaml = Text("src/RAWSelectionAssistant/Views/MonthCalendarView.xaml");
        StringAssert.Contains(xaml, "WeatherGlyph");
        Assert.IsFalse(xaml.Contains("MonthWeatherIcon", StringComparison.Ordinal));
        StringAssert.Contains(xaml, "TimeText, Mode=OneWay");
        StringAssert.Contains(xaml, "TextTrimming=\"CharacterEllipsis\"");
        Assert.IsFalse(xaml.Contains("WeatherText}" + " TextWrapping", StringComparison.Ordinal));
    }

    [TestMethod]
    public void WeekAndDayCards_ShowIconTemperatureRainAndWind()
    {
        var item = new CalendarBookingItemViewModel(Booking(), Weather());
        StringAssert.Contains(item.WeatherText, "🌤");
        StringAssert.Contains(item.WeatherText, "23°");
        StringAssert.Contains(item.WeatherText, "降雨 40%");
        StringAssert.Contains(item.WeatherText, "风 18 km/h");
        StringAssert.Contains(Text("src/RAWSelectionAssistant/Views/WeekCalendarView.xaml"), "WeatherText");
        StringAssert.Contains(Text("src/RAWSelectionAssistant/Views/DayCalendarView.xaml"), "WeatherText");
    }

    [TestMethod]
    public void CalendarMonthWeekDay_ReceiveWeatherWithoutChangingBookingCollection()
    {
        var booking = Booking();
        var weather = new Dictionary<Guid, BookingWeatherSummary?> { [booking.Id] = Weather(strongWind: true) };
        var month = new MonthCalendarViewModel(_ => { }, _ => Task.CompletedTask, _ => { });
        month.Configure(new DateTime(2026, 8, 1), [booking], new DateTime(2026, 8, 5), weather);
        Assert.IsTrue(month.Days.SelectMany(day => day.VisibleBookings).Any(item => item.Id == booking.Id && item.MonthWeatherIcon.Length > 0));
        var week = new WeekCalendarViewModel(_ => { }, _ => Task.CompletedTask, _ => { });
        week.Configure(new DateTime(2026, 8, 3), [booking], new DateTime(2026, 8, 5), weather);
        Assert.IsTrue(week.Days.SelectMany(day => day.TimedBookings).Any(item => item.WeatherText.Contains("风", StringComparison.Ordinal)));
        var dayView = new DayCalendarViewModel(_ => Task.CompletedTask, _ => { });
        dayView.Configure(new DateTime(2026, 8, 5), [booking], weather);
        Assert.IsTrue(dayView.TimeSlots.SelectMany(slot => slot.Bookings).Any(item => item.WeatherText.Contains("降雨", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void BookingDetailsWeather_ShowsAllRequiredFieldsAndProviderAttribution()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/BookingWeatherPanel.xaml");
        foreach (var value in new[] { "天气情况", "温度 / 体感", "降雨概率 / 量", "风速 / 阵风", "湿度 / 云量 / 能见度", "日出 / 日落", "UpdatedText", "ProviderText", "RiskText" })
            StringAssert.Contains(xaml, value);
        StringAssert.Contains(Text("src/RAWSelectionAssistant/Views/ShootBookingDetailsView.xaml"), "BookingWeatherPanel");
    }

    [TestMethod]
    public void WorkbenchToday_ShowsRepresentativeWeatherAndStrongWindRisk()
    {
        var item = new WorkbenchScheduleItemViewModel(WorkbenchItem(), TimeZoneInfo.Utc, new FixedTimeProvider(), Weather(strongWind: true));
        StringAssert.Contains(item.WeatherText, "🌤");
        StringAssert.Contains(item.WeatherText, "23°C");
        StringAssert.Contains(item.WeatherText, "降雨 40%");
        StringAssert.Contains(item.WeatherText, "风 45");
        StringAssert.Contains(item.WeatherText, "强风风险");
    }

    [TestMethod]
    public void WorkbenchFuture_UsesDailyMinimumMaximumAndRain()
    {
        var day = new WorkbenchScheduleDay(new(2026, 8, 5), [WorkbenchItem()]);
        var weather = new Dictionary<Guid, BookingWeatherSummary?> { [WorkbenchItem().BookingId] = Weather() };
        var viewModel = new WorkbenchScheduleDayViewModel(day, TimeZoneInfo.Utc, new FixedTimeProvider(), weather);
        StringAssert.Contains(viewModel.Items[0].WeatherText, "19–29°C");
        StringAssert.Contains(viewModel.Items[0].WeatherText, "降雨 60%");
        Assert.IsFalse(viewModel.Items[0].WeatherText.Contains("风 18", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task WeatherFailure_DoesNotPreventBookingDetailsFromLoading()
    {
        var state = new WeatherFeatureState();
        state.Apply(new WeatherSettings { Enabled = true });
        var viewModel = new BookingWeatherViewModel(new ThrowingWeatherService(), state);
        await viewModel.LoadAsync(new ShootBooking { Id = Guid.NewGuid(), Title = "仍可打开", ClientDisplayName = "客户", StartAtUtc = Start, EndAtUtc = End, TimeZoneId = TimeZoneInfo.Utc.Id, ShootingType = "Portrait" });
        Assert.IsNull(viewModel.Summary);
        StringAssert.Contains(viewModel.StatusText, "地点待确认");
    }

    [TestMethod]
    public void BookingChanges_MarkWeatherStaleAndAutoRefreshOnlyOnceWhenEnabled()
    {
        var source = Text("src/RAWSelectionAssistant/ViewModels/CalendarViewModels.cs");
        StringAssert.Contains(source, "_weatherState?.MarkNeedsRefresh(saved.Id)");
        StringAssert.Contains(source, "_weatherState?.AutoRefreshEnabled == true");
        StringAssert.Contains(source, "GetBookingWeatherAsync(saved.Id, saved.StartAtUtc, saved.EndAtUtc, true)");
        Assert.AreEqual(1, Count(source, "GetBookingWeatherAsync(saved.Id"));
    }

    [TestMethod]
    public void WeatherViewModel_DependsOnlyOnForecastServiceNotProviders()
    {
        var source = Text("src/RAWSelectionAssistant/ViewModels/WeatherViewModels.cs");
        StringAssert.Contains(source, "IWeatherForecastService");
        Assert.IsFalse(source.Contains("OpenMeteoWeatherProvider", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("HttpClient", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Sqlite", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void WeatherUi_IsAccessibleThemeAwareAndHasNoFixedScaleTransform()
    {
        var text = Text("src/RAWSelectionAssistant/Views/BookingWeatherPanel.xaml")
            + Text("src/RAWSelectionAssistant/Views/MonthCalendarView.xaml")
            + Text("src/RAWSelectionAssistant/Views/WeekCalendarView.xaml")
            + Text("src/RAWSelectionAssistant/Views/DayCalendarView.xaml")
            + Text("src/RAWSelectionAssistant/Views/WorkbenchScheduleView.xaml");
        StringAssert.Contains(text, "AutomationProperties.Name");
        StringAssert.Contains(text, "DynamicResource");
        Assert.IsFalse(text.Contains("ScaleTransform", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("#FFFFFF", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void WeatherCacheOnlyCalendarLoading_CatchesOptionalServiceFailures()
    {
        var calendar = Text("src/RAWSelectionAssistant/ViewModels/CalendarViewModels.cs");
        var workbench = Text("src/RAWSelectionAssistant/ViewModels/StageDViewModels.cs");
        StringAssert.Contains(calendar, "LoadCachedWeatherAsync");
        StringAssert.Contains(calendar, "catch");
        StringAssert.Contains(workbench, "TryGetCachedWeatherAsync");
        StringAssert.Contains(workbench, "catch { return null; }");
    }

    private static ShootBookingSummary Booking()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        return new(id, null, "摄影项目", "客户", Start, End, TimeZoneInfo.Utc.Id, false, ShootBookingStatus.Confirmed, "完整地址", "Portrait", false, false);
    }

    private static WorkbenchScheduleItem WorkbenchItem()
    {
        var booking = Booking();
        return new(booking.Id, null, booking.Title, booking.StartAtUtc, booking.EndAtUtc, booking.TimeZoneId, booking.Status, false, false, true, false, 0,
            "摄影项目", "地点已记录", 2, 3);
    }

    private static BookingWeatherSummary Weather(bool strongWind = false)
    {
        var location = new WeatherLocation("上海", "上海", "中国", 31.2304, 121.4737, "Asia/Shanghai", "FakeWeather");
        var hour = new HourlyWeatherForecast(Start.AddHours(1), "1", 23, 25, 40, 1.2, strongWind ? 45 : 18, strongWind ? 52 : 25, 65, 30, 10_000);
        var day = new DailyWeatherForecast(new(2026, 8, 5), "1", 19, 29, 60, Start.AddHours(-13), Start.AddHours(9));
        var risks = strongWind ? new[] { new WeatherRiskNotice("StrongWind", "可能有强风", NotificationSeverity.Warning) } : [];
        return new(Booking().Id, WeatherAvailability.Cached, location, hour, day, [hour], [day], risks, Start.AddMinutes(-30), "FakeWeather", true, false, false, "使用天气缓存。");
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-08-04T08:00:00Z");
    }

    private sealed class ThrowingWeatherService : IWeatherForecastService
    {
        public Task<IReadOnlyList<WeatherLocationCandidate>> SearchLocationsAsync(string query, CancellationToken cancellationToken = default) => throw new HttpRequestException();
        public void ConfirmLocation(Guid bookingId, WeatherLocationCandidate candidate) { }
        public Task<BookingWeatherSummary> GetBookingWeatherAsync(Guid bookingId, DateTimeOffset startAtUtc, DateTimeOffset endAtUtc, bool forceRefresh = false, CancellationToken cancellationToken = default) => throw new HttpRequestException();
        public Task<BookingWeatherSummary?> TryGetCachedBookingWeatherAsync(Guid bookingId, DateTimeOffset startAtUtc, DateTimeOffset endAtUtc, CancellationToken cancellationToken = default) => throw new IOException();
        public Task ClearCacheAsync(CancellationToken cancellationToken = default) => throw new IOException();
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        for (var index = 0; (index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length) count++;
        return count;
    }

    private static string Text(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
