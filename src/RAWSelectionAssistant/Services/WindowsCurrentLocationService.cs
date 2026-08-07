using System.Runtime.Versioning;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using Windows.Devices.Geolocation;
using Windows.System;

namespace RAWSelectionAssistant.Services;

[SupportedOSPlatform("windows10.0.19041.0")]
public sealed class WindowsCurrentLocationService : ICurrentLocationService
{
    public async Task<CurrentLocationResult> GetCurrentLocationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var access = await Geolocator.RequestAccessAsync().AsTask(cancellationToken).ConfigureAwait(false);
            if (access == GeolocationAccessStatus.Denied)
                return new(CurrentLocationPermission.Denied, Message: "无法获取当前位置，请选择城市。");
            if (access != GeolocationAccessStatus.Allowed)
                return new(CurrentLocationPermission.Unavailable, Message: "当前位置服务暂时不可用，请选择城市。");

            var locator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
            var position = await locator.GetGeopositionAsync(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(12)).AsTask(cancellationToken).ConfigureAwait(false);
            var point = position.Coordinate.Point.Position;
            return new(CurrentLocationPermission.Allowed, point.Latitude, point.Longitude, "已使用 Windows 当前位置一次性获取拍摄天气；不会持续跟踪。 ");
        }
        catch (OperationCanceledException) { throw; }
        catch (UnauthorizedAccessException)
        {
            return new(CurrentLocationPermission.Denied, Message: "无法获取当前位置，请选择城市。");
        }
        catch
        {
            return new(CurrentLocationPermission.Unavailable, Message: "当前位置服务暂时不可用，请选择城市。");
        }
    }

    public async Task OpenLocationPrivacySettingsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-location")).AsTask(cancellationToken).ConfigureAwait(false);
    }
}
