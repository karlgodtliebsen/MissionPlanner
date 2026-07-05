using MissionPlanner.Core.Models;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Commands;

/// <summary>
/// Tracks command acknowledgments for vehicles.
/// </summary>
public interface ICommandAckTracker
{
    /// <summary>
    /// Waits for a command acknowledgment from a specific vehicle.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle to wait for an acknowledgment from.</param>
    /// <param name="command">The command to wait for an acknowledgment for.</param>
    /// <param name="timeout">The maximum time to wait for the acknowledgment.</param>
    /// <param name="cancellationToken">A token to cancel the wait operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the command acknowledgment message.</returns>
    Task<CommandAckMessage> WaitForAckAsync(VehicleId vehicleId, ushort command, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>
    /// Handles a received command acknowledgment message.
    /// </summary>
    /// <param name="message">The command acknowledgment message to handle.</param>
    void Handle(CommandAckMessage message);
}