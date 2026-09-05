using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.App.Services;

/// <summary>Provides the current device location through the host operating system.</summary>
public interface IPlatformLocationService
{
    /// <summary>Gets a cached location when available, otherwise requests a current location.</summary>
    ValueTask<GeoPosition?> GetLocationAsync(CancellationToken cancellationToken = default);
}
