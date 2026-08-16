using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>Published when a connection-scoped parameter load changes state or progress.</summary>
public sealed class VehicleParameterLoadStatusChanged : DomainEvent<ParameterLoadStatus>
{
    /// <summary>Gets the parameter-load snapshot.</summary>
    public ParameterLoadStatus Status => (ParameterLoadStatus)Payload!;

    /// <summary>Initializes the event.</summary>
    public VehicleParameterLoadStatusChanged(ParameterLoadStatus status)
        : base("VehicleParameterLoadStatusChanged", status)
    {
    }
}
