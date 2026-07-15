namespace MissionPlanner.Core.Models.Observations;

public interface IVehicleObservation
{
    DateTimeOffset ObservedAt { get; }
}
