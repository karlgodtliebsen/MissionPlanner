namespace MissionPlanner.Core.Vehicles.Abstractions;

public interface IVehicleObservation
{
    DateTimeOffset ObservedAt { get; }
}
