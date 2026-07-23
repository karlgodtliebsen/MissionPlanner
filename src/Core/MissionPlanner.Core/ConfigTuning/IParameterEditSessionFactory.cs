using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Creates vehicle-scoped parameter editing sessions.</summary>
public interface IParameterEditSessionFactory
{
    /// <summary>Gets whether the shared session has unapplied edits.</summary>
    bool HasUnappliedChanges { get; }

    /// <summary>Occurs when the shared session or its dirty state changes.</summary>
    event EventHandler? Changed;

    /// <summary>Gets or creates the shared session for the given active vehicle and firmware identity.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <returns>The shared session.</returns>
    IParameterEditSession Create(VehicleId vehicleId);

    /// <summary>Reverts all unapplied edits in the shared session.</summary>
    void DiscardPendingChanges();
}
