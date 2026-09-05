using System.Text.Json;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Services;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Library.Browser.Interop;

namespace MissionPlanner.Library.Browser;

/// <summary>Gets device location through the browser's permission-controlled Geolocation API.</summary>
public sealed class BrowserPlatformLocationService(ILogger<BrowserPlatformLocationService> logger)
    : IPlatformLocationService
{
    public async ValueTask<GeoPosition?> GetLocationAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var json = await BrowserInterop.GetLocationAsync().WaitAsync(cancellationToken);
            if (json is null) return null;
            using var document = JsonDocument.Parse(json);
            var result = new GeoPosition(document.RootElement.GetProperty("latitude").GetDouble(),
                document.RootElement.GetProperty("longitude").GetDouble());
            return result.IsValid ? result : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The browser could not provide the device location.");
            return null;
        }
    }
}
