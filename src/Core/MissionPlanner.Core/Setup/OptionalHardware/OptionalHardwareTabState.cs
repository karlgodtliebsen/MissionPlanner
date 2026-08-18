namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Availability result for one Optional Hardware tab.</summary>
public sealed record OptionalHardwareTabState(OptionalHardwareTabDescriptor Descriptor, bool IsAvailable, string ReasonUnavailable);
