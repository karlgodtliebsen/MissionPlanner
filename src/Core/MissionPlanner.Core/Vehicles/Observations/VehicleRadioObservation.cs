using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>
/// Provides the public API for VehicleRadioObservation.
/// </summary>
public sealed record VehicleRadioObservation(int ChannelCount, IReadOnlyList<ushort> ChannelsRaw, int? RssiPercent, DateTimeOffset ObservedAt) : IVehicleObservation;
