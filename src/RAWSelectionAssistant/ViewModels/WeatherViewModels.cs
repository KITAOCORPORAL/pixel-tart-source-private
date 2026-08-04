using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

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

    public BookingWeatherViewModel(IWeatherForecastService service, WeatherFeatureState state)
    {
        _service = service;
        _state = state;
        SearchCommand = new AsyncRelayCommand(_ => SearchAsync(), _ => IsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(SearchText));
        ConfirmLocationCommand = new AsyncRelayCommand(_ => ConfirmLocationAsync(), _ => IsEnabled && !IsBusy && SelectedCandidate is not null && _booking is not null);
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync(force: true), _ => IsEnabled && !IsBusy && _booking is not null);
        ClearCacheCommand = new AsyncRelayCommand(_ => ClearCacheAsync(), _ => !IsBusy);
    }

    public ObservableCollection<WeatherLocationCandidate> Candidates { get; } = [];
    public ICommand SearchCommand { get; }
    public ICommand ConfirmLocationCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ClearCacheCommand { get; }
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
    public WeatherLocationCandidate? SelectedCandidate { get => _selectedCandidate; set { if (SetProperty(ref _selectedCandidate, value)) RaiseCommands(); } }
    public BookingWeatherSummary? Summary { get => _summary; private set { if (SetProperty(ref _summary, value)) NotifySummary(); } }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string LocationText => Summary?.Location?.DisplayName ?? _state.GetLocation(_booking?.Id ?? Guid.Empty)?.DisplayName ?? "地点待确认";
    public string ConditionText => Summary?.RepresentativeHour is { } hour ? WeatherCodeText(hour.WeatherCode) : "暂无预报";
    public string TemperatureText => Summary?.RepresentativeHour is { } hour ? $"{hour.TemperatureC:0.#}°C（体感 {hour.ApparentTemperatureC:0.#}°C）" : "—";
    public string PrecipitationText => Summary?.RepresentativeHour is { } hour ? $"{hour.PrecipitationProbability}% · {hour.PrecipitationMm:0.#} mm" : "—";
    public string WindText => Summary?.RepresentativeHour is { } hour ? $"{hour.WindSpeedKph:0.#} km/h · 阵风 {hour.WindGustKph:0.#} km/h" : "—";
    public string AtmosphereText => Summary?.RepresentativeHour is { } hour ? $"湿度 {hour.RelativeHumidity}% · 云量 {hour.CloudCover}% · 能见度 {hour.VisibilityMeters / 1000:0.#} km" : "—";
    public string SunText => Summary?.Day is { } day ? $"日出 {Local(day.SunriseUtc)} · 日落 {Local(day.SunsetUtc)}" : "—";
    public string UpdatedText => Summary?.UpdatedAtUtc is { } at ? $"更新时间 {at.ToLocalTime():yyyy-MM-dd HH:mm}" : "尚未更新";
    public string ProviderText => $"数据来源：{Summary?.Provider ?? "Open-Meteo"}";
    public string RiskText => Summary is { Risks.Count: > 0 } ? string.Join("；", Summary.Risks.Select(risk => risk.Message)) : "当前没有达到集中配置的天气风险阈值。";
    public bool HasSummary => Summary?.RepresentativeHour is not null || Summary?.Day is not null;

    public async Task LoadAsync(ShootBooking booking, CancellationToken cancellationToken = default)
    {
        _booking = booking;
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
        StatusText = !IsEnabled ? "尚未启用天气" : Summary?.StatusMessage ?? (_state.GetLocation(booking.Id) is null ? "地点待确认。" : "可以手动刷新天气。" );
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
    }

    private static string Local(DateTimeOffset? value) => value?.ToLocalTime().ToString("HH:mm") ?? "—";
    private static string WeatherCodeText(string code) => code switch
    {
        "0" => "晴", "1" or "2" => "少云", "3" => "阴", "45" or "48" => "雾", "51" or "53" or "55" => "毛毛雨",
        "61" or "63" or "65" or "80" or "81" or "82" => "雨", "71" or "73" or "75" or "77" or "85" or "86" => "雪",
        "95" or "96" or "99" => "雷暴", _ => "天气代码 " + code
    };
}
