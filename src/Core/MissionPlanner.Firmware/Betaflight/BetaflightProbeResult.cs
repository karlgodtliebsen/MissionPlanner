using MissionPlanner.Firmware.Betaflight.Protocol;

namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Identity and typed optional diagnostics, without raw firmware payload logging.</summary>
public sealed record BetaflightProbeResult(BetaflightProbeOutcome Outcome, BetaflightDeviceInfo? Identity = null,
    MspFailure Failure = MspFailure.None, string? Diagnostic = null);