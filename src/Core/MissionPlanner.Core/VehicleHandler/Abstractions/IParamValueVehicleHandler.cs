using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.VehicleHandler.Abstractions;

/// <summary>
/// Handles PARAM_VALUE messages and updates the parameter registry accordingly.
/// </summary>
public interface IParamValueVehicleHandler
{
    /// <summary>
    /// Handles a PARAM_VALUE message and stores the parameter in the registry.
    /// </summary>
    /// <param name="message">The PARAM_VALUE message to handle.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Handle(ParamValueMessage message, CancellationToken cancellationToken);
}
