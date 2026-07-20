namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for VehicleRadioState.
/// </summary>
/// <param name="ChannelCount">The ChannelCount value.</param>
/// <param name="ChannelsRaw">The ChannelsRaw value.</param>
/// <param name="RssiPercent">The RssiPercent value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
public sealed record VehicleRadioState(
    int? ChannelCount,
    IReadOnlyList<ushort> ChannelsRaw,
    int? RssiPercent,
    DateTimeOffset? ObservedAt)
{
    /// <summary>
    /// Provides the public API for Empty.
    /// </summary>
    public static VehicleRadioState Empty { get; } = new(null, Array.Empty<ushort>(), null, null);
}
