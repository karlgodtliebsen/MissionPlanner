using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>
/// Defines the interface for a service that performs CompassMot calibration.
/// </summary>
public interface ICompassMotorCalibrationService : IDisposable
{
    /// <summary>Gets the latest calibration snapshot.</summary>
    CompassMotorCalibrationSnapshot Current
    {
        get;
    }
    /// <summary>Occurs when the snapshot changes.</summary>
    event Action<CompassMotorCalibrationSnapshot>? Changed;
    /// <summary>Starts a CompassMot calibration and returns whether the vehicle accepted it.</summary>
    Task<bool> StartAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);
    /// <summary>Stops the calibration.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}