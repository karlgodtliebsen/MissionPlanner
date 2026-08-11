using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Payload;

/// <summary>Discovers and commands camera components.</summary>
public interface ICameraProtocolService
{
    /// <summary>Returns discovered cameras for a vehicle system.</summary>
    IReadOnlyList<CameraComponentState> GetCameras(byte systemId);

    /// <summary>Captures one image on the exact selected component.</summary>
    Task<CameraOperationResult> CaptureImageAsync(VehicleId autopilot, byte componentId, CancellationToken cancellationToken);

    /// <summary>Starts or stops video on the exact selected component.</summary>
    Task<CameraOperationResult> SetVideoAsync(VehicleId autopilot, byte componentId, bool start, CancellationToken cancellationToken);
}
