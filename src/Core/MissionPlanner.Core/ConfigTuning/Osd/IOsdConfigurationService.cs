using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning.Osd;

/// <summary>Discovers and safely edits parameter-backed onboard OSD layouts.</summary>
public interface IOsdConfigurationService
{
    /// <summary>Discovers screens/items and opens the shared vehicle editing session.</summary>
    /// <param name="vehicleId">The active vehicle.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The OSD workspace, or <see langword="null"/> when no OSD parameters exist.</returns>
    Task<OsdConfigurationWorkspace?> OpenAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Validates coordinates and overlaps for one screen.</summary>
    /// <param name="workspace">The opened workspace.</param>
    /// <param name="screenNumber">The screen number.</param>
    /// <returns>The validation issues.</returns>
    IReadOnlyList<OsdValidationIssue> ValidateScreen(OsdConfigurationWorkspace workspace, int screenNumber);

    /// <summary>Moves one item to an exact character-grid position in pending state.</summary>
    /// <param name="workspace">The opened workspace.</param>
    /// <param name="screenNumber">The screen number.</param>
    /// <param name="itemKey">The item key.</param>
    /// <param name="column">The zero-based column.</param>
    /// <param name="row">The zero-based row.</param>
    /// <returns>An error message, or <see langword="null"/> when accepted.</returns>
    string? MoveItem(OsdConfigurationWorkspace workspace, int screenNumber, string itemKey, int column, int row);

    /// <summary>Applies one screen through shared-session confirmed readback.</summary>
    /// <param name="workspace">The opened workspace.</param>
    /// <param name="screenNumber">The screen number.</param>
    /// <param name="allowDynamicOverlapWarnings">Whether explicitly acknowledged dynamic overlap warnings may proceed.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The validation and write result.</returns>
    Task<OsdApplyResult> ApplyScreenAsync(
        OsdConfigurationWorkspace workspace,
        int screenNumber,
        bool allowDynamicOverlapWarnings,
        CancellationToken cancellationToken = default);

    /// <summary>Reverts the selected screen to current live values.</summary>
    /// <param name="workspace">The opened workspace.</param>
    /// <param name="screenNumber">The screen number.</param>
    void ResetScreen(OsdConfigurationWorkspace workspace, int screenNumber);

    /// <summary>Exports all discovered OSD pending values as invariant JSON.</summary>
    /// <param name="workspace">The opened workspace.</param>
    /// <returns>The layout document.</returns>
    string Export(OsdConfigurationWorkspace workspace);

    /// <summary>Imports only OSD parameters discovered for the active firmware.</summary>
    /// <param name="workspace">The opened workspace.</param>
    /// <param name="json">The layout JSON.</param>
    /// <returns>The atomic import result.</returns>
    OsdImportResult Import(OsdConfigurationWorkspace workspace, string json);

    /// <summary>Projects one screen into platform-neutral preview items.</summary>
    /// <param name="workspace">The opened workspace.</param>
    /// <param name="screenNumber">The screen number.</param>
    /// <returns>The preview items.</returns>
    IReadOnlyList<OsdPreviewItem> GetPreviewItems(OsdConfigurationWorkspace workspace, int screenNumber);
}
