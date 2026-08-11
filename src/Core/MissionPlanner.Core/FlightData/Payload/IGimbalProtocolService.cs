using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Payload;

/// <summary>Discovers and commands gimbal components.</summary>
public interface IGimbalProtocolService
{
    /// <summary>Returns discovered gimbals for a vehicle system.</summary>
    IReadOnlyList<GimbalComponentState> GetGimbals(byte systemId);

    /// <summary>Sends a bounded low-rate pitch/yaw command to the exact selected component.</summary>
    Task<GimbalOperationResult> SetPitchYawAsync(VehicleId autopilot, byte componentId, float pitchDegrees,
        float yawDegrees, bool yawLock, CancellationToken cancellationToken);
}
