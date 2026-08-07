using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed record WeatherLocationModeOption(WeatherLocationMode Value, string Label) { public override string ToString() => Label; }

public sealed class BookingWeatherViewModel : ObservableObject
{
    private readonly IWeatherForecastService _service;
    private readonly WeatherFeatureState _state;
    private ShootBooking? _booking;
    private bool _isBusy;
    private string _searchText = string.Empty;
    private WeatherLocationCandidate? _selectedCandidate;
    private BookingWeatherSummary? _summary;
    private string _statusText = "尚未启用天气";
    private readonly ICurrentLocationService? _currentLocationService;
    private readonly IBookingTimeDisplayService _timeDisplay;
    private WeatherLocationModeOption _selectedLocationMode;
    private CurrentLocationPermission _locationPermission = CurrentLocationPermission.Unknown;
    private bool _locationAttempted;

    public BookingWeatherViewModel(IWeatherForecastService service, WeatherFeatureState state, ICurrentLocationService? currentLocationService = null,
        IBookingTimeDisplayService? timeDisplay = null)
    {
        _service = service;
        _state = state;
        _currentLocationService = currentLocationService;
        _timeDisplay = timeDisplay ?? BookingTimeDisplayService.Default;
        LocationModes =
        [
            new(WeatherLocationMode.CurrentLocation, "当前位置"),
            new(WeatherLocationMode.FollowBookingLocation, "跟随拍摄地点"),
            new(WeatherLocationMode.ManualCity, "其他城市")
        ];
        _selectedLocationMode = LocationModes[0];
        SearchCommand = new AsyncRelayCommand(_ => SearchAsync(), _ => IsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(SearchText));
        ConfirmLocationCommand = new AsyncRelayCommand(_ => ConfirmLocationAsync(), _ => IsEnabled && !IsBusy && SelectedCandidate is not null && _booking is not null);
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync(force: true), _ => IsEnabled && !IsBusy && _booking is not null);
        ClearCacheCommand = new AsyncRelayCommand(_ => ClearCacheAsync(), _ => !IsBusy);
        UseCurrentLocationCommand = new AsyncRelayCommand(_ => ResolveCurrentLocationAsync(force: true), _ => IsEnabled && !IsBusy && _booking is not null && _currentLocationService is not null);
        OpenLocationSettingsCommand = new AsyncRelayCommand(_ => OpenLocationSettingsAsync(), _ => _currentLocationService is not null && !IsBusy);
    }

    public ObservableCollection<WeatherLocationCandidate> Candidates { get; } = [];
    public IReadOnlyList<WeatherLocationModeOption> LocationModes { get; }
    public ICommand SearchCommand { get; }
    public ICommand ConfirmLocationCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ClearCacheCommand { get; }
    public ICommand UseCurrentLocationCommand { get; }
    public ICommand OpenLocationSettingsCommand { get; }
    public bool IsEnabled
    {
        get => _state.Enabled;
        set
        {
            if (value == _state.Enabled) return;
            _state.SetEnabled(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDisabled));
            StatusText = value ? "天气已启用；确认城市后才会联网查询。" : "尚未启用天气";
            RaiseCommands();
        }
    }
    public bool IsDisabled => !IsEnabled;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) RaiseCommands(); } }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value ?? string.Empty)) RaiseCommands(); } }
    public WeatherLocationModeOption SelectedLocationMode
    {
        get => _selectedLocationMode;
        set
        {
            if (value is null || !SetProperty(ref _selectedLocationMode, value)) return;
            if (_booking is not null) _state.SetLocationMode(_booking.Id, value.Value);
            StatusText = value.Value switch
            {
                WeatherLocationMode.CurrentLocation => "使用 Windows 当前位置；只在你查询时获取一次，不会持续跟踪。",
                WeatherLocationMode.FollowBookingLocation => "将根据排期中的拍摄地点搜索候选城市，请确认后查询。",
                _ => "请输入城市或地区，并从带地区和国家的候选项中确认。"
            };
            OnPropertyChanged(nameof(IsCurrentLocationMode));
            OnPropertyChanged(nameof(IsFollowBookingLocationMode));
            OnPropertyChanged(nameof(IsManualCityMode));
            if (_booking is not null && value.Value == WeatherLocationMode.FollowBookingLocation && !string.IsNullOrWhiteSpace(_booking.Location))
            {
                SearchText = _booking.Location;
                _ = SearchAsync();
            }
            RaiseCommands();
        }
    }
    public bool IsCurrentLocationMode => SelectedLocationMode.Value == WeatherLocationMode.CurrentLocation;
    public bool IsFollowBookingLocationMode => SelectedLocationMode.Value == WeatherLocationMode.FollowBookingLocation;
    public bool IsManualCityMode => SelectedLocationMode.Value == WeatherLocationMode.ManualCity;
    public WeatherLocationCandidate? SelectedCandidate { get => _selectedCandidate; set { if (SetProperty(ref _selectedCandidate, value)) RaiseCommands(); } }
    public BookingWeatherSummary? Summary { get => _summary; private set { if (SetProperty(ref _summary, value)) NotifySummary(); } }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string LocationText => Summary?.Location?.DisplayName ?? _state.GetLocation(_booking?.Id ?? Guid.Empty)?.DisplayName ?? "地点待确认";
    public string ConditionText => Summary?.RepresentativeHour is { } hour ? WeatherCodeText(hour.WeatherCode) : "暂无预报";
    public string TemperatureText => Summary?.RepresentativeHour is { } hour ? $"{hour.TemperatureC:0.#}°C（体感 {hour.ApparentTemperatureC:0.#}°C）" : "—";
    public string PrecipitationText => Summary?.RepresentativeHour is { } hour ? $"{hour.PrecipitationProbability}% · {hour.PrecipitationMm:0.#} mm" : "—";
    public string WindText => Summary?.RepresentativeHour is { } hour ? $"{hour.WindSpeedKph:0.#} km/h · 阵风 {hour.WindGustKph:0.#} km/h" : "—";
    public string AtmosphereText => Summary?.RepresentativeHour is { } hour ? $"湿度 {hour.RelativeHumidity}% · 云量 {hour.CloudCover}% · 能见度 {hour.VisibilityMeters / 1000:0.#} km" : "—";
    public string SunText => Summary?.Day is { } day ? $"日出 {BookingTime(day.SunriseUtc, "HH:mm")} · 日落 {BookingTime(day.SunsetUtc, "HH:mm")}" : "—";
    public string UpdatedText => Summary?.UpdatedAtUtc is { } at ? $"更新时间 {BookingTime(at, "yyyy-MM-dd HH:mm")}" : "尚未更新";
    public string ProviderText => $"数据来源：{Summary?.Provider ?? "Open-Meteo"}";
    public string RiskText => Summary switch
    {
        { Risks.Count: > 0 } => string.Join("；", Summary.Risks.Select(risk => risk.Message)),
        { Availability: WeatherAvailability.LocationPending } => "地点尚未确认，暂时无法判断天气风险。",
        { Availability: WeatherAvailability.Unavailable } => "天气服务暂时不可用，不能判断是否存在风险。",
        { Availability: WeatherAvailability.OutOfRange } => "拍摄日期超出可靠预报范围，不能判断是否存在风险。",
        { RepresentativeHour: not null } or { Day: not null } => "预报已获取，当前未达到天气风险阈值。",
        _ => "尚未取得可靠预报，不能判断是否存在风险。"
    };
    public string LocationPermissionText => _locationPermission switch
    {
        CurrentLocationPermission.Allowed => "Windows 位置权限已允许；仅执行一次性定位。",
        CurrentLocationPermission.Denied => "Windows 位置权限未允许。",
        CurrentLocationPermission.Unavailable => "Windows 位置服务暂时不可用。",
        _ => "首次使用当前位置时，Windows 会请求位置权限。"
    };
    public bool HasSummary => Summary?.RepresentativeHour is not null || Summary?.Day is not null;

    public async Task LoadAsync(ShootBooking booking, CancellationToken cancellationToken = default)
    {
        _booking = booking;
        _locationAttempted = false;
        _selectedLocationMode = LocationModes.First(item => item.Value == _state.GetLocationMode(booking.Id));
        OnPropertyChanged(nameof(SelectedLocationMode));
        OnPropertyChanged(nameof(IsCurrentLocationMode));
        OnPropertyChanged(nameof(IsFollowBookingLocationMode));
        OnPropertyChanged(nameof(IsManualCityMode));
        Candidates.Clear();
        SelectedCandidate = null;
        SearchText = string.Empty;
        try
        {
            Summary = await _service.TryGetCachedBookingWeatherAsync(booking.Id, booking.StartAtUtc, booking.EndAtUtc, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            Summary = null;
        }
        StatusText = !IsEnabled ? "尚未启用天气" : Summary?.StatusMessage ?? (_state.GetLocation(booking.Id) is null ? "地点待确认。" : "可以刷新天气。" );
        if (IsEnabled && Summary is null)
        {
            if (SelectedLocationMode.Value == WeatherLocationMode.CurrentLocation) await ResolveCurrentLocationAsync(force: false, cancellationToken).ConfigureAwait(true);
            else if (SelectedLocationMode.Value == WeatherLocationMode.FollowBookingLocation && !string.IsNullOrWhiteSpace(booking.Location))
            {
                SearchText = booking.Location;
                await SearchAsync().ConfigureAwait(true);
            }
        }
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(IsDisabled));
        NotifySummary();
    }

    private async Task SearchAsync()
    {
        IsBusy = true;
        try
        {
            Candidates.Clear();
            foreach (var candidate in await _service.SearchLocationsAsync(SearchText).ConfigureAwait(true)) Candidates.Add(candidate);
            StatusText = Candidates.Count == 0 ? "无法确定天气地点，请选择城市或手动搜索。" : "请选择城市、地区和国家。";
        }
        catch (Exception)
        { StatusText = "天气地点搜索暂时不可用；排期仍可正常使用。"; }
        finally { IsBusy = false; }
    }

    private async Task ConfirmLocationAsync()
    {
        if (_booking is null || SelectedCandidate is null) return;
        _service.ConfirmLocation(_booking.Id, SelectedCandidate);
        StatusText = "天气地点已确认，正在查询。";
        await RefreshAsync(force: true).ConfigureAwait(true);
    }

    private async Task RefreshAsync(bool force)
    {
        if (_booking is null) return;
        if (_state.GetLocation(_booking.Id) is null && SelectedLocationMode.Value == WeatherLocationMode.CurrentLocation)
            await ResolveCurrentLocationAsync(force: false, refreshWeather: false).ConfigureAwait(true);
        if (_state.GetLocation(_booking.Id) is null) { StatusText = "地点尚未确认；请选择当前位置、跟随拍摄地点或其他城市。"; return; }
        IsBusy = true;
        try
        {
            Summary = await _service.GetBookingWeatherAsync(_booking.Id, _booking.StartAtUtc, _booking.EndAtUtc, force).ConfigureAwait(true);
            StatusText = Summary.StatusMessage;
        }
        catch (Exception)
        {
            StatusText = "天气服务暂时不可用；排期仍可正常使用。";
        }
        finally { IsBusy = false; }
    }

    private async Task ClearCacheAsync()
    {
        IsBusy = true;
        try { await _service.ClearCacheAsync().ConfigureAwait(true); Summary = null; StatusText = "天气缓存已清除。"; }
        catch (Exception) { StatusText = "天气缓存暂时无法清除；排期仍可正常使用。"; }
        finally { IsBusy = false; }
    }

    private async Task ResolveCurrentLocationAsync(bool force, CancellationToken cancellationToken = default, bool refreshWeather = true)
    {
        if (_booking is null || _currentLocationService is null || (!force && _locationAttempted)) return;
        _locationAttempted = true;
        IsBusy = true;
        try
        {
            CurrentLocationResult result;
            try
            {
                result = await _currentLocationService.GetCurrentLocationAsync(cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _locationPermission = CurrentLocationPermission.Unavailable;
                OnPropertyChanged(nameof(LocationPermissionText));
                StatusText = "无法获取当前位置，请选择城市。";
                return;
            }
            catch (Exception)
            {
                _locationPermission = CurrentLocationPermission.Unavailable;
                OnPropertyChanged(nameof(LocationPermissionText));
                StatusText = "无法获取当前位置，请选择城市。";
                return;
            }
            _locationPermission = result.Permission;
            OnPropertyChanged(nameof(LocationPermissionText));
            if (result.Permission != CurrentLocationPermission.Allowed || result.Latitude is null || result.Longitude is null)
            {
                StatusText = "无法获取当前位置，请选择城市。";
                return;
            }
            _state.SetLocationMode(_booking.Id, WeatherLocationMode.CurrentLocation);
            _state.ConfirmLocation(_booking.Id, new WeatherLocation("当前位置", null, string.Empty, result.Latitude.Value, result.Longitude.Value, TimeZoneInfo.Local.Id, "Windows Location"));
            StatusText = result.Message ?? "当前位置已获取；不会持续跟踪。";
            OnPropertyChanged(nameof(LocationText));
            if (refreshWeather && IsEnabled)
            {
                try
                {
                    Summary = await _service.GetBookingWeatherAsync(_booking.Id, _booking.StartAtUtc, _booking.EndAtUtc, forceRefresh: true, cancellationToken: cancellationToken).ConfigureAwait(true);
                    StatusText = Summary.StatusMessage;
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    StatusText = "当前位置已获取，但天气服务暂时不可用；排期仍可正常使用。";
                }
            }
        }
        finally { IsBusy = false; }
    }

    private async Task OpenLocationSettingsAsync()
    {
        if (_currentLocationService is null) return;
        try { await _currentLocationService.OpenLocationPrivacySettingsAsync().ConfigureAwait(true); }
        catch { StatusText = "无法打开 Windows 位置设置；你仍可手动选择城市。"; }
    }

    private void NotifySummary()
    {
        foreach (var name in new[] { nameof(LocationText), nameof(ConditionText), nameof(TemperatureText), nameof(PrecipitationText), nameof(WindText), nameof(AtmosphereText), nameof(SunText), nameof(UpdatedText), nameof(ProviderText), nameof(RiskText), nameof(HasSummary) }) OnPropertyChanged(name);
    }

    private void RaiseCommands()
    {
        (SearchCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ConfirmLocationCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RefreshCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ClearCacheCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (UseCurrentLocationCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (OpenLocationSettingsCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private string BookingTime(DateTimeOffset? value, string format) => value is { } at
        ? _timeDisplay.ToBookingTime(at, _booking?.TimeZoneId).ToString(format)
        : "—";
    private static string WeatherCodeText(string code) => code switch
    {
        "0" => "晴", "1" or "2" => "少云", "3" => "阴", "45" or "48" => "雾", "51" or "53" or "55" => "毛毛雨",
        "61" or "63" or "65" or "80" or "81" or "82" => "雨", "71" or "73" or "75" or "77" or "85" or "86" => "雪",
        "95" or "96" or "99" => "雷暴", _ => "天气代码 " + code
    };
}
