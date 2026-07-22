using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Firmware;

/// <summary>Coordinates manifest discovery, package verification, safe disconnect, flashing, and reconnect detection.</summary>
public interface IFirmwareUpdateCoordinator : IDisposable
{
    /// <summary>Gets the current workflow state.</summary>
    FirmwareUpdateState State { get; }

    /// <summary>Gets the prepared verified package, if any.</summary>
    FirmwarePackage? Package { get; }

    /// <summary>Occurs whenever workflow state or progress changes.</summary>
    event EventHandler<FirmwareUpdateStateChangedEventArgs>? StateChanged;

    /// <summary>Clears a prepared package when it is safe to return to the idle state.</summary>
    void Reset();

    /// <summary>Discovers releases compatible with exact board identifiers.</summary>
    /// <param name="identity">The connected vehicle identity.</param>
    /// <param name="channel">The requested channel.</param>
    /// <param name="cancellationToken">A token that cancels discovery.</param>
    /// <returns>The compatible releases.</returns>
    Task<IReadOnlyList<FirmwareManifestEntry>> DiscoverAsync(
        VehicleFirmwareIdentity identity,
        FirmwareReleaseChannel channel,
        CancellationToken cancellationToken = default);

    /// <summary>Downloads and cryptographically verifies a release.</summary>
    /// <param name="release">The selected compatible release.</param>
    /// <param name="cancellationToken">A token that cancels preparation.</param>
    /// <returns>The verified package.</returns>
    Task<FirmwarePackage> PrepareAsync(FirmwareManifestEntry release, CancellationToken cancellationToken = default);

    /// <summary>Disconnects normal MAVLink and invokes the safe platform adapter.</summary>
    /// <param name="vehicleId">The currently connected vehicle.</param>
    /// <param name="identity">Its protocol-reported identity.</param>
    /// <param name="parameterBackupConfirmed">Whether the user confirmed a parameter backup.</param>
    /// <param name="cancellationToken">A token that cancels the workflow.</param>
    /// <returns>The adapter result.</returns>
    Task<FirmwareFlashResult> FlashAsync(
        VehicleId vehicleId,
        VehicleFirmwareIdentity identity,
        bool parameterBackupConfirmed,
        CancellationToken cancellationToken = default);
}
