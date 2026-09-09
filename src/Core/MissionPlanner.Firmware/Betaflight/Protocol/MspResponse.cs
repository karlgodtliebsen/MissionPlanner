namespace MissionPlanner.Firmware.Betaflight.Protocol;

/// <summary>Result of one bounded MSP request; written evidence supports reboot/disconnect handling.</summary>
public sealed record MspResponse(MspFailure Failure, MspFrame? Frame = null, bool RequestWritten = false);