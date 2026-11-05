using Windows.Devices.Geolocation;

namespace AsiairContainer;

public sealed record LocationFix(double Latitude, double Longitude, double AccuracyMetres, DateTimeOffset Timestamp);
public static class WindowsLocation
{
    public static async Task<LocationFix> GetAsync(CancellationToken token)
    {
        if (await Geolocator.RequestAccessAsync().AsTask(token) != GeolocationAccessStatus.Allowed)
            throw new InvalidOperationException("Windows location access is unavailable. Enable location for desktop apps in Windows Settings, or enter coordinates manually.");
        var locator = new Geolocator { DesiredAccuracyInMeters = 100 };
        var position = await locator.GetGeopositionAsync(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(20)).AsTask(token);
        var point = position.Coordinate.Point.Position;
        return new(point.Latitude, point.Longitude, position.Coordinate.Accuracy, position.Coordinate.Timestamp);
    }
}
