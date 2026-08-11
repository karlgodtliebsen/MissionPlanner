namespace MissionPlanner.Core.FlightData.Components;

/// <summary>Stores discovered peripheral components and component-scoped workflow state.</summary>
public interface IVehicleComponentRegistry
{
    /// <summary>Occurs when component or traffic state changes.</summary>
    event EventHandler? Changed;

    /// <summary>Returns discovered components for a system.</summary>
    IReadOnlyList<VehicleComponentState> GetComponents(byte systemId);

    /// <summary>Returns transponder states for a system.</summary>
    IReadOnlyList<TransponderComponentState> GetTransponders(byte systemId);

    /// <summary>Returns current bounded traffic tracks for a system.</summary>
    IReadOnlyList<AdsbTrafficTrack> GetTraffic(byte systemId, DateTimeOffset now);
}
