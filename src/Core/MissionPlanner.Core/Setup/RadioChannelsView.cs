using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Represents the immutable live RC channel projection shown by the Setup UI.</summary>
/// <param name="VehicleId">The vehicle the projection belongs to.</param>
/// <param name="Channels">The live channels in ascending order.</param>
/// <param name="IsStale">Whether the RC telemetry is older than the freshness window.</param>
/// <param name="Issues">Static configuration issues detected from parameters.</param>
public sealed record RadioChannelsView(
    VehicleId VehicleId,
    IReadOnlyList<RadioChannelInfo> Channels,
    bool IsStale,
    IReadOnlyList<RadioValidationIssue> Issues)
{
    /// <summary>Gets an empty projection for the specified vehicle.</summary>
    /// <param name="vehicleId">The vehicle identifier.</param>
    /// <returns>An empty projection.</returns>
    public static RadioChannelsView Empty(VehicleId vehicleId)
    {
        return new RadioChannelsView(vehicleId, [], true, []);
    }
}
