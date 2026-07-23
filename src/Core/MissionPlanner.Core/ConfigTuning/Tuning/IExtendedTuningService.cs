using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Coordinates advanced descriptors with the shared safe parameter session.</summary>
public interface IExtendedTuningService
{
    /// <summary>Opens a presence-gated advanced workspace for an active vehicle.</summary>
    /// <param name="vehicleId">The active vehicle.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The workspace, or <see langword="null"/> when unsupported.</returns>
    Task<ExtendedTuningWorkspace?> OpenAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Validates metadata and coupled rules for one descriptor.</summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="descriptorKey">The descriptor key.</param>
    /// <returns>The validation issues.</returns>
    IReadOnlyList<ExtendedTuningValidationIssue> ValidateGroup(ExtendedTuningWorkspace workspace, string descriptorKey);

    /// <summary>Applies one descriptor group through confirmed shared-session writes.</summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="descriptorKey">The descriptor key.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The validation and write result.</returns>
    Task<ExtendedTuningApplyResult> ApplyGroupAsync(
        ExtendedTuningWorkspace workspace,
        string descriptorKey,
        CancellationToken cancellationToken = default);

    /// <summary>Reverts pending values in one descriptor group.</summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="descriptorKey">The descriptor key.</param>
    void RevertGroup(ExtendedTuningWorkspace workspace, string descriptorKey);

    /// <summary>Builds normalized, read-only comparisons across descriptor axes.</summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="descriptorKey">The descriptor key.</param>
    /// <returns>The comparison values.</returns>
    IReadOnlyList<AdvancedTuningComparisonValue> CompareAxes(ExtendedTuningWorkspace workspace, string descriptorKey);

    /// <summary>Creates a non-mutating axis-copy preview.</summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="descriptorKey">The descriptor key.</param>
    /// <param name="sourceAxis">The source axis.</param>
    /// <param name="targetAxis">The target axis.</param>
    /// <returns>The proposed target changes.</returns>
    AxisCopyPreview PreviewCopyAxis(
        ExtendedTuningWorkspace workspace,
        string descriptorKey,
        string sourceAxis,
        string targetAxis);

    /// <summary>Applies an explicitly reviewed, still-current preview to pending state.</summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="preview">The preview to apply.</param>
    /// <returns>The pending-state result.</returns>
    AxisCopyApplyResult ApplyCopyAxisPreview(ExtendedTuningWorkspace workspace, AxisCopyPreview preview);
}

/// <summary>Stores the latest read-only PID response metric for each connected vehicle axis.</summary>
public interface IControlResponseMetricsService
{
    /// <summary>Occurs when a PID response metric is received.</summary>
    event EventHandler<ControlResponseMetricChangedEventArgs>? Changed;

    /// <summary>Gets the latest metrics for one vehicle.</summary>
    /// <param name="vehicleId">The vehicle.</param>
    /// <returns>The latest metric per reported axis.</returns>
    IReadOnlyList<ControlResponseMetric> GetMetrics(VehicleId vehicleId);
}
