using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Retains source and transition evidence; correlation never equates MCU UID with a DFU serial.</summary>
public sealed record BetaflightDfuHandoffResult(bool Succeeded, string Code, SerialDeviceDescriptor Source,
    DfuDeviceDescriptor? Device = null, string? PhysicalLocation = null);