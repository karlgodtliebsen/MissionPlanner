using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Missions.Models;
using Windows.Devices.Geolocation;

namespace MissionPlanner.AvaloniaUI.App.Services;

/// <summary>Gets device location from the Windows location service.</summary>
public sealed class WindowsPlatformLocationService(ILogger<WindowsPlatformLocationService> logger)
    : IPlatformLocationService
{
    private static readonly TimeSpan cachedLocationMaximumAge = TimeSpan.FromDays(30);
    private static readonly TimeSpan cachedLocationTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan currentLocationTimeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public async ValueTask<GeoPosition?> GetLocationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed)
            {
                logger.LogInformation("Windows location access is {AccessStatus}.", access);
                return null;
            }

            var locator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
            Geoposition? position = null;
            try
            {
                position = await locator
                    .GetGeopositionAsync(cachedLocationMaximumAge, cachedLocationTimeout)
                    .AsTask(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogDebug(exception, "No cached Windows location was available.");
            }

            position ??= await locator
                .GetGeopositionAsync(TimeSpan.Zero, currentLocationTimeout)
                .AsTask(cancellationToken);

            var coordinate = position.Coordinate.Point.Position;
            var result = new GeoPosition(coordinate.Latitude, coordinate.Longitude);
            return result.IsValid ? result : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Windows could not provide the device location.");
            return null;
        }
    }
}
