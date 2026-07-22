namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for VehicleRadioState.
/// </summary>
/// <param name="ChannelCount">The ChannelCount value.</param>
/// <param name="ChannelsRaw">The ChannelsRaw value.</param>
/// <param name="RssiPercent">The RssiPercent value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
/// <param name="RemoteRssi">The remote RSSI in device-specific units.</param>
/// <param name="LocalRssi">The local RSSI in device-specific units.</param>
/// <param name="TransmitBufferPercent">The transmit buffer availability percentage.</param>
/// <param name="LocalNoise">The local noise level in device-specific units.</param>
/// <param name="RemoteNoise">The remote noise level in device-specific units.</param>
/// <param name="ReceiveErrors">The receive error count.</param>
/// <param name="CorrectedPackets">The corrected packet count.</param>
/// <param name="LinkObservedAt">The radio-link status reception timestamp.</param>
/// <param name="ServoOutputPort">The servo output bank.</param>
/// <param name="ServoOutputsRaw">The raw PWM servo outputs in microseconds.</param>
/// <param name="ServoObservedAt">The servo output reception timestamp.</param>
public sealed record VehicleRadioState(
    int? ChannelCount,
    IReadOnlyList<ushort> ChannelsRaw,
    int? RssiPercent,
    DateTimeOffset? ObservedAt,
    int? LocalRssi = null,
    int? RemoteRssi = null,
    int? TransmitBufferPercent = null,
    int? LocalNoise = null,
    int? RemoteNoise = null,
    ushort? ReceiveErrors = null,
    ushort? CorrectedPackets = null,
    DateTimeOffset? LinkObservedAt = null,
    byte? ServoOutputPort = null,
    IReadOnlyList<ushort>? ServoOutputsRaw = null,
    DateTimeOffset? ServoObservedAt = null)
{
    /// <summary>
    /// Provides the public API for Empty.
    /// </summary>
    public static VehicleRadioState Empty { get; } = new(null, Array.Empty<ushort>(), null, null);

    /// <summary>Returns whether RC channel data is older than <paramref name="maximumAge"/>.</summary>
    public bool IsStale(DateTimeOffset now, TimeSpan maximumAge) => ObservedAt is null || now - ObservedAt > maximumAge;
}
