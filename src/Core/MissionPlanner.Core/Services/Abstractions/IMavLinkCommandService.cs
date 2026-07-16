using MissionPlanner.Core.Models;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Services.Abstractions;

/// <summary>
/// Service for sending MAVLink commands to vehicles.
/// </summary>
public interface IMavLinkCommandService
{
    /// <summary>
    /// Requests a telemetry data stream from the vehicle.
    /// </summary>
    /// <param name="vehicleId">Target vehicle identifier</param>
    /// <param name="streamId">MAVLink data stream ID (e.g., MAV_DATA_STREAM_EXTRA1 = 10 for ATTITUDE)</param>
    /// <param name="rateHz">Requested message rate in Hz</param>
    /// <param name="start">True to start streaming, false to stop</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if command was sent successfully</returns>
    Task<bool> RequestDataStreamAsync(
        VehicleId vehicleId,
        MavDataStream streamId,
        int rateHz,
        bool start = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the vehicle to send its home position (MAV_CMD_GET_HOME_POSITION).
    /// The vehicle answers with a HOME_POSITION message once home is set.
    /// </summary>
    /// <param name="vehicleId">Target vehicle identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the command was sent successfully</returns>
    Task<bool> RequestHomePositionAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);
}
