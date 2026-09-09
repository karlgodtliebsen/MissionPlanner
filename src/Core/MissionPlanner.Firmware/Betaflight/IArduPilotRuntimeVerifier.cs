using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Verifies the returning serial endpoint without reusing the old protocol conversation.</summary>
public interface IArduPilotRuntimeVerifier
{
    /// <summary>Returns proven ArduPilot runtime evidence, or null when verification fails.</summary>
    Task<ArduPilotRuntimeIdentity?> VerifyAsync(SerialDeviceDescriptor device, CancellationToken cancellationToken = default);
}