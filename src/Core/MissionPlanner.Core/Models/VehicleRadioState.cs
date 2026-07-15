namespace MissionPlanner.Core.Models;

public sealed record VehicleRadioState(
    int? ChannelCount,
    IReadOnlyList<ushort> ChannelsRaw,
    int? RssiPercent,
    DateTimeOffset? ObservedAt)
{
    public static VehicleRadioState Empty { get; } = new(null, Array.Empty<ushort>(), null, null);
}
