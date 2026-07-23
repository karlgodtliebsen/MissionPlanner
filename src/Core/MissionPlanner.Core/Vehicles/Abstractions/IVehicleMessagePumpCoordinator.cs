namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>Coordinates one shared inbound MAVLink dispatcher across concurrent vehicle transports.</summary>
public interface IVehicleMessagePumpCoordinator
{
    /// <summary>Acquires shared inbound dispatch for one connection lifetime.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A reference-counted message-pump lease.</returns>
    Task<IVehicleMessagePumpLease> AcquireAsync(CancellationToken cancellationToken = default);
}

/// <summary>Represents one connection's lease on the shared inbound MAVLink dispatcher.</summary>
public interface IVehicleMessagePumpLease : IAsyncDisposable
{
    /// <summary>Gets the shared message pump.</summary>
    IVehicleMessagePump Pump { get; }
}
