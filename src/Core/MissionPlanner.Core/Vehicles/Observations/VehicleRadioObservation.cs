using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

public sealed record VehicleRadioObservation(int ChannelCount, IReadOnlyList<ushort> ChannelsRaw, int? RssiPercent, DateTimeOffset ObservedAt) : IVehicleObservation;
