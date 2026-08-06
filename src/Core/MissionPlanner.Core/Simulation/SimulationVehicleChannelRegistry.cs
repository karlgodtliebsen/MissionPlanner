using System.Collections.Concurrent;
using MissionPlanner.Core.Simulation.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Simulation;

/// <summary>Maintains exact session and vehicle routes for concurrent simulator connections.</summary>
public sealed class SimulationVehicleChannelRegistry : ISimulationVehicleChannelRegistry
{
    private readonly ConcurrentDictionary<Guid, SimulationVehicleChannel> bySession = [];
    private readonly ConcurrentDictionary<VehicleId, SimulationVehicleChannel> byVehicle = [];

    /// <inheritdoc />
    public void Register(SimulationVehicleChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        if (!bySession.TryAdd(channel.SessionId, channel))
        {
            throw new InvalidOperationException($"Simulation session {channel.SessionId} already has a vehicle channel.");
        }

        if (byVehicle.TryAdd(channel.VehicleId, channel))
        {
            return;
        }

        bySession.TryRemove(channel.SessionId, out var _);
        throw new InvalidOperationException($"Vehicle {channel.VehicleId} is already routed to another connection.");
    }

    /// <inheritdoc />
    public SimulationVehicleChannel? Find(VehicleId vehicleId)
    {
        return byVehicle.TryGetValue(vehicleId, out var channel) ? channel : null;
    }

    /// <inheritdoc />
    public SimulationVehicleChannel? Find(Guid sessionId)
    {
        return bySession.TryGetValue(sessionId, out var channel) ? channel : null;
    }

    /// <inheritdoc />
    public SimulationVehicleChannel? Remove(Guid sessionId)
    {
        if (!bySession.TryRemove(sessionId, out var channel))
        {
            return null;
        }

        byVehicle.TryRemove(new KeyValuePair<VehicleId, SimulationVehicleChannel>(channel.VehicleId, channel));
        return channel;
    }
}
