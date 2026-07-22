using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Configuration.Tuning;

/// <summary>Coordinates curated tuning profiles with the shared safe parameter session.</summary>
public interface IBasicTuningService
{
    /// <summary>Opens the profile supported by an active vehicle.</summary>
    /// <param name="vehicleId">The active vehicle.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The supported workspace, or <see langword="null"/> for unsupported firmware.</returns>
    Task<BasicTuningWorkspace?> OpenAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Validates one resolved group against metadata and coupled rules.</summary>
    /// <param name="workspace">The opened workspace.</param>
    /// <param name="groupKey">The stable group key.</param>
    /// <returns>The validation issues.</returns>
    IReadOnlyList<BasicTuningValidationIssue> ValidateGroup(BasicTuningWorkspace workspace, string groupKey);

    /// <summary>Applies only the pending fields in one group with confirmed readback.</summary>
    /// <param name="workspace">The opened workspace.</param>
    /// <param name="groupKey">The stable group key.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The validation and write result.</returns>
    Task<BasicTuningApplyResult> ApplyGroupAsync(
        BasicTuningWorkspace workspace,
        string groupKey,
        CancellationToken cancellationToken = default);

    /// <summary>Reverts only the pending fields in one group.</summary>
    /// <param name="workspace">The opened workspace.</param>
    /// <param name="groupKey">The stable group key.</param>
    void RevertGroup(BasicTuningWorkspace workspace, string groupKey);

    /// <summary>Exports only parameters presented by the current profile.</summary>
    /// <param name="workspace">The opened workspace.</param>
    /// <returns>An invariant JSON document containing pending values.</returns>
    string Export(BasicTuningWorkspace workspace);

    /// <summary>Imports pending values only for parameters presented by the current profile.</summary>
    /// <param name="workspace">The opened workspace.</param>
    /// <param name="json">The Basic Tuning JSON document.</param>
    /// <returns>The atomic import result.</returns>
    BasicTuningImportResult Import(BasicTuningWorkspace workspace, string json);
}
