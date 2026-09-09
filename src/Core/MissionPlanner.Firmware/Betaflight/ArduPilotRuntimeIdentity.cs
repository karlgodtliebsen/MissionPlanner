namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Runtime verification evidence from a newly opened isolated MAVLink conversation.</summary>
public sealed record ArduPilotRuntimeIdentity(byte SystemId, byte ComponentId, Version? Version);