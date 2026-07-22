using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup;

/// <summary>Evaluates the setup workflow catalog for the current vehicle.</summary>
public interface ISetupWorkflowCatalog
{
    /// <summary>Gets the ordered setup workflow definitions.</summary>
    IReadOnlyList<SetupWorkflowDescriptor> Workflows { get; }

    /// <summary>Evaluates every workflow against connection, firmware, parameters, and local evidence.</summary>
    /// <param name="snapshot">The active vehicle snapshot.</param>
    /// <param name="parameters">The currently known vehicle parameters.</param>
    /// <param name="evidence">Locally persisted completion evidence.</param>
    /// <returns>Ordered workflow evaluations.</returns>
    IReadOnlyList<SetupWorkflowEvaluation> Evaluate(
        ActiveVehicleSnapshot snapshot,
        IReadOnlyDictionary<string, VehicleParameter> parameters,
        IReadOnlyList<SetupCompletionEvidence> evidence);

    /// <summary>Creates completion evidence for the current vehicle state.</summary>
    /// <param name="workflow">The completed workflow.</param>
    /// <param name="state">The current vehicle state.</param>
    /// <param name="parameters">The currently known parameters.</param>
    /// <param name="completedAt">The completion timestamp.</param>
    /// <returns>Fingerprint-bound local evidence.</returns>
    SetupCompletionEvidence CreateEvidence(
        SetupWorkflowKey workflow,
        VehicleState state,
        IReadOnlyDictionary<string, VehicleParameter> parameters,
        DateTimeOffset completedAt);
}
