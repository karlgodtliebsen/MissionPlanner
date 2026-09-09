namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Proves firmware identity on one exclusively owned selected serial endpoint.</summary>
public interface IBetaflightDeviceProbe
{
    /// <summary>Probes and releases the endpoint, without bootloader commands or telemetry session creation.</summary>
    Task<BetaflightProbeResult> ProbeAsync(string portName, CancellationToken cancellationToken = default);
}