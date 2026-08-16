using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Represents the immutable live RC channel projection shown by the Setup UI.</summary>
/// <param name="VehicleId">The vehicle the projection belongs to.</param>
/// <param name="Channels">The live channels in ascending order.</param>
/// <param name="IsStale">Whether the RC telemetry is older than the freshness window.</param>
/// <param name="Issues">Static configuration issues detected from parameters.</param>
/// <param name="RssiPercent">RC receiver/input RSSI when reported by RC_CHANNELS.</param>
/// <param name="IsArmed">Whether the vehicle is currently armed.</param>
/// <param name="ChannelMapSummary">Honest compact or explicit pilot-channel mapping.</param>
/// <param name="ReportedChannelCount">Receiver-reported channel count, including temporarily unavailable values.</param>
public sealed record RadioChannelsView(
    VehicleId VehicleId,
    IReadOnlyList<RadioChannelInfo> Channels,
    bool IsStale,
    IReadOnlyList<RadioValidationIssue> Issues,
    int? RssiPercent = null,
    bool IsArmed = false,
    string ChannelMapSummary = "",
    int ReportedChannelCount = 0)
{
    /// <summary>Gets receiver channel availability and freshness.</summary>
    public RadioSignalState SignalState => Channels.Count == 0
        ? RadioSignalState.NoSignal
        : IsStale ? RadioSignalState.Stale : RadioSignalState.Live;

    /// <summary>Gets an empty projection for the specified vehicle.</summary>
    /// <param name="vehicleId">The vehicle identifier.</param>
    /// <returns>An empty projection.</returns>
    public static RadioChannelsView Empty(VehicleId vehicleId)
    {
        return new RadioChannelsView(vehicleId, [], true, [], null, false, "Map unavailable", 0);
    }
}
