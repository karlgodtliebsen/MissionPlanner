using MissionPlanner.Firmware;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Describes one stable Optional Hardware tab and its availability signature.</summary>
public sealed record OptionalHardwareTabDescriptor(OptionalHardwareTabKey Key, string Title, string Description, int Order, bool RequiresVehicle, bool RequiresParameters, bool SupportsOffline, IReadOnlyList<string> ParameterPrefixes, IReadOnlySet<FirmwareFamily>? FirmwareFamilies = null);
