using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.Abstractions;

/// <summary>Resolves physical flight-controller outputs assigned to logical motors.</summary>
public interface IMotorOutputResolver
{
    /// <summary>Resolves the current physical output assignment for a logical motor.</summary>
    /// <param name="vehicleId">The vehicle whose live parameters are queried.</param>
    /// <param name="motorNumber">The one-based ArduPilot logical motor number.</param>
    /// <returns>A result that explicitly represents resolved, missing, or ambiguous assignments.</returns>
    MotorOutputResolution Resolve(VehicleId vehicleId, int motorNumber);
}
