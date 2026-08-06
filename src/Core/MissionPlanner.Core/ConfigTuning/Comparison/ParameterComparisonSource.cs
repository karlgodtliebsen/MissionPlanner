using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Core.ConfigTuning.Comparison;

/// <summary>Identifies a parameter source and its optional firmware scope.</summary>
public sealed record ParameterComparisonSource(string Name, string Identity, DateTimeOffset Timestamp, VehicleFirmwareIdentity? Firmware);
