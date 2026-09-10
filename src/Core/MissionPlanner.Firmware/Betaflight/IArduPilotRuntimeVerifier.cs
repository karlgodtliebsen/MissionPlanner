using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Verifies the returning serial endpoint without reusing the old protocol conversation.</summary>
public interface IArduPilotRuntimeVerifier
{
    /// <summary>Returns proven ArduPilot runtime evidence, or null when verification fails.</summary>
    Task<ArduPilotRuntimeIdentity?> VerifyAsync(SerialDeviceDescriptor device, CancellationToken cancellationToken = default);

    /// <summary>Returns runtime evidence and a diagnostic outcome without changing vehicle state.</summary>
    async Task<Workflow.FirmwareRuntimeProbeResult> ProbeAsync(SerialDeviceDescriptor device, CancellationToken cancellationToken = default)
    {
        var identity = await VerifyAsync(device, cancellationToken).ConfigureAwait(false);
        return identity is null
            ? new(Workflow.FirmwareRuntimeKind.Unknown, Workflow.FirmwareBootEnvironment.None, "runtime.mavlink-timeout")
            { Outcome = Workflow.FirmwareRuntimeProbeOutcome.Timeout }
            : new(Workflow.FirmwareRuntimeKind.ArduPilot, Workflow.FirmwareBootEnvironment.None, "runtime.ardupilot", identity.IsArmed)
            {
                Evidence = Workflow.FirmwareRuntimeEvidence.MavLinkProbe,
                Verification = Workflow.FirmwareRuntimeVerification.Verified,
                Outcome = Workflow.FirmwareRuntimeProbeOutcome.Success,
                ProtocolDetail = identity.Version?.ToString()
            };
    }
}
