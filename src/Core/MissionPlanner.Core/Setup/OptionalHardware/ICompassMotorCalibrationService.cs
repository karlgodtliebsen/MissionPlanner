using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>
/// Defines the interface for a service that performs CompassMot calibration.
/// </summary>
public interface ICompassMotorCalibrationService : IDisposable
{
    CompassMotorCalibrationSnapshot Current
    {
        get;
    }
    event Action<CompassMotorCalibrationSnapshot>? Changed;
    Task<bool> StartAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}