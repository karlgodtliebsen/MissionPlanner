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

/// <summary>
/// Data for the VehicleParameterReceived event.
/// </summary>
/// <param name="VehicleId">The ID of the vehicle.</param>
/// <param name="Parameter">The parameter that was received.</param>
public sealed record VehicleParameterReceivedData(
    VehicleId VehicleId,
    VehicleParameter Parameter);
