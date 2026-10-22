using MissionPlanner.App.Services;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Library.Desktop;

/// <summary>Location service for desktop platforms without a location adapter; it never reports a position.</summary>
public sealed class UnavailablePlatformLocationService : IPlatformLocationService
{
    /// <summary>Always returns null, so callers keep their default map position.</summary>
    public ValueTask<GeoPosition?> GetLocationAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<GeoPosition?>(null);
    }
}
