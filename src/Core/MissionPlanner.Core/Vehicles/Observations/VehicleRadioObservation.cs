namespace MissionPlanner.Core.Models.Observations;

public sealed record VehicleRadioObservation(int ChannelCount, IReadOnlyList<ushort> ChannelsRaw, int? RssiPercent, DateTimeOffset ObservedAt) : IVehicleObservation;
