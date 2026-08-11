namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>Represents one connection's lease on the shared inbound MAVLink dispatcher.</summary>
public interface IVehicleMessagePumpLease : IAsyncDisposable
{
    /// <summary>Gets the shared message pump.</summary>
    IVehicleMessagePump Pump { get; }
}
