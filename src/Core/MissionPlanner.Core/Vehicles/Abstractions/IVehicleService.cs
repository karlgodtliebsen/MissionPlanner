using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Models;

namespace MissionPlanner.Core.Services.Abstractions;

/// <summary>
/// Domain Service IVehicleService
/// </summary>
public interface IVehicleService
{
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    IReadOnlyCollection<VehicleState> GetVehicles();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vehicleId"></param>
    /// <returns></returns>
    VehicleState? GetVehicleState(VehicleId vehicleId);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vehicleId"></param>
    /// <returns></returns>
    VehicleSession? GetVehicle(VehicleId vehicleId);


    /// <summary>
    /// 
    /// </summary>
    /// <param name="vehicleId"></param>
    /// <returns></returns>
    IReadOnlyCollection<VehicleStatusText> GetVehicleNotifications(VehicleId vehicleId);


    /// <summary>
    /// 
    /// </summary>
    /// <param name="vehicleId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns> 
    Task<VehicleCommandResponse> ArmAsync(VehicleId vehicleId, CancellationToken cancellationToken);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vehicleId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<VehicleCommandResponse> DisarmAsync(VehicleId vehicleId, CancellationToken cancellationToken);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vehicleId"></param>
    /// <param name="mode"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<VehicleCommandResponse> SetModeAsync(VehicleId vehicleId, VehicleMode mode, CancellationToken cancellationToken);
}
