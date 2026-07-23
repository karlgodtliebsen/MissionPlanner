using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning.Fences;

/// <summary>Owns the active-vehicle fence parameter session, geometry revisions, and confirmed transfers.</summary>
public interface IFenceConfigurationService
{
    /// <summary>Gets the explicitly supported fence parameter fields.</summary>
    IReadOnlyList<ParameterFieldDefinition> ParameterDefinitions { get; }

    /// <summary>Occurs when a local, synchronized, or backup revision changes.</summary>
    event EventHandler? Changed;

    /// <summary>Gets the retained workspace snapshot for a vehicle.</summary>
    /// <param name="vehicleId">The vehicle.</param>
    /// <returns>The current workspace snapshot.</returns>
    FenceConfigurationSnapshot GetSnapshot(VehicleId vehicleId);

    /// <summary>Opens the shared parameter session and loads supported fence fields.</summary>
    /// <param name="vehicleId">The active vehicle.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The shared editing session.</returns>
    Task<IParameterEditSession> OpenParameterSessionAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Determines whether typed fence geometry is supported.</summary>
    /// <param name="vehicleId">The vehicle.</param>
    /// <returns><see langword="true"/> when capabilities or live ArduPilot parameters advertise support.</returns>
    bool SupportsTypedGeometry(VehicleId vehicleId);

    /// <summary>Replaces the editable local plan and advances its revision.</summary>
    /// <param name="vehicleId">The workspace vehicle.</param>
    /// <param name="plan">The new local plan.</param>
    /// <returns>The updated snapshot.</returns>
    FenceConfigurationSnapshot SetLocalPlan(VehicleId vehicleId, FencePlan plan);

    /// <summary>Restores the last plan saved before replace or clear.</summary>
    /// <param name="vehicleId">The workspace vehicle.</param>
    /// <returns>The updated snapshot.</returns>
    FenceConfigurationSnapshot RestoreBackup(VehicleId vehicleId);

    /// <summary>Downloads and parses the vehicle fence plan.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="replaceLocal">Whether a successful download replaces local edits.</param>
    /// <param name="progress">Optional transfer progress.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed operation report.</returns>
    Task<FenceOperationReport> DownloadAsync(
        VehicleId vehicleId,
        bool replaceLocal,
        IProgress<FenceTransferProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Validates and applies fence parameters, then uploads local geometry.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="parameterSession">The shared parameter session.</param>
    /// <param name="progress">Optional transfer progress.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed operation report.</returns>
    Task<FenceOperationReport> ApplyAsync(
        VehicleId vehicleId,
        IParameterEditSession parameterSession,
        IProgress<FenceTransferProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Clears vehicle fence geometry after preserving a recoverable backup.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The acknowledged operation report.</returns>
    Task<FenceOperationReport> ClearAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);
}
