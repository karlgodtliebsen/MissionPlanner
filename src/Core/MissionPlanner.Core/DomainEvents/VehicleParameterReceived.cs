using MissionPlanner.Core.Models;
using MissionPlanner.Library.EventHub.Events;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Event that is triggered when a vehicle parameter is received.
/// </summary>
public sealed class VehicleParameterReceived : DomainEvent<VehicleParameterReceivedData>
{
    /// <inheritdoc />
    public VehicleParameterReceived(VehicleParameterReceivedData data)
        : base("VehicleParameterReceived", data)
    {
    }

    /// <summary>
    /// Gets the vehicle ID associated with the domain event.
    /// </summary>
    public VehicleId VehicleId => ((VehicleParameterReceivedData)Payload!).VehicleId;

    /// <summary>
    /// Gets the parameter associated with the domain event.
    /// </summary>
    public VehicleParameter Parameter => ((VehicleParameterReceivedData)Payload!).Parameter;
}
