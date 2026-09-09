using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Receipt retaining source, artifact, handoff and runtime verification evidence.</summary>
public sealed record BetaflightConversionResult(bool Succeeded, string Code, SerialDeviceDescriptor Source,
    FirmwareManifestEntry Firmware, BetaflightDfuHandoffResult? Handoff = null, DfuArtifact? Artifact = null,
    DfuProgrammingResult? Programming = null, SerialDeviceDescriptor? ReturnedDevice = null,
    ArduPilotRuntimeIdentity? Runtime = null);