using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Simulation.Abstractions;

/// <summary>Routes vehicle operations to independently owned simulator transports.</summary>
public interface ISimulationVehicleChannelRegistry
{
    /// <summary>Registers one exact session and vehicle channel.</summary>
    /// <param name="channel">The channel to register.</param>
    void Register(SimulationVehicleChannel channel);

    /// <summary>Finds a channel by exact vehicle identity.</summary>
    /// <param name="vehicleId">Vehicle identity.</param>
    /// <returns>The channel, or <see langword="null"/>.</returns>
    SimulationVehicleChannel? Find(VehicleId vehicleId);

    /// <summary>Finds a channel by exact simulation session identity.</summary>
    /// <param name="sessionId">Simulation session identity.</param>
    /// <returns>The channel, or <see langword="null"/>.</returns>
    SimulationVehicleChannel? Find(Guid sessionId);

    /// <summary>Removes only the channel owned by an exact session.</summary>
    /// <param name="sessionId">Simulation session identity.</param>
    /// <returns>The removed channel, or <see langword="null"/>.</returns>
    SimulationVehicleChannel? Remove(Guid sessionId);
}
