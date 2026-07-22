using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Firmware;

/// <summary>Implements the guarded firmware download, verification, flash, and reconnect state machine.</summary>
public sealed class FirmwareUpdateCoordinator : IFirmwareUpdateCoordinator
{
    private readonly IFirmwareManifestProvider manifestProvider;
    private readonly IFirmwareManifestSelector selector;
    private readonly IFirmwarePackageManager packageManager;
    private readonly IFirmwareFlashingService flashingService;
    private readonly IVehicleConnectionService connectionService;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly ILogger<FirmwareUpdateCoordinator> logger;
    private VehicleId? flashingVehicle;
    private bool disposed;

    /// <summary>Initializes the firmware update coordinator.</summary>
    /// <param name="manifestProvider">The release manifest provider.</param>
    /// <param name="selector">The exact board-identity selector.</param>
    /// <param name="packageManager">The download/cache/verification manager.</param>
    /// <param name="flashingService">The platform flashing adapter.</param>
    /// <param name="connectionService">The normal MAVLink connection service.</param>
    /// <param name="activeVehicle">The active-vehicle context used for reconnect detection.</param>
    /// <param name="logger">The logger.</param>
    public FirmwareUpdateCoordinator(
        IFirmwareManifestProvider manifestProvider,
        IFirmwareManifestSelector selector,
        IFirmwarePackageManager packageManager,
        IFirmwareFlashingService flashingService,
        IVehicleConnectionService connectionService,
        IActiveVehicleContext activeVehicle,
        ILogger<FirmwareUpdateCoordinator> logger)
    {
        this.manifestProvider = manifestProvider;
        this.selector = selector;
        this.packageManager = packageManager;
        this.flashingService = flashingService;
        this.connectionService = connectionService;
        this.activeVehicle = activeVehicle;
        this.logger = logger;
        activeVehicle.Changed += OnActiveVehicleChanged;
    }

    /// <inheritdoc />
    public FirmwareUpdateState State { get; private set; }

    /// <inheritdoc />
    public FirmwarePackage? Package { get; private set; }

    /// <inheritdoc />
    public event EventHandler<FirmwareUpdateStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public void Reset()
    {
        ThrowIfDisposed();
        if (State is FirmwareUpdateState.WaitingForDisconnect or FirmwareUpdateState.Flashing or FirmwareUpdateState.WaitingForReconnect)
        {
            return;
        }

        Package = null;
        flashingVehicle = null;
        SetState(FirmwareUpdateState.Idle, "Firmware workflow is idle.", 0);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FirmwareManifestEntry>> DiscoverAsync(
        VehicleFirmwareIdentity identity,
        FirmwareReleaseChannel channel,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Package = null;
        SetState(FirmwareUpdateState.Discovering, "Discovering compatible firmware…", 0);
        try
        {
            var releases = await manifestProvider.GetReleasesAsync(cancellationToken).ConfigureAwait(false);
            var compatible = selector.Select(releases, identity, channel);
            SetState(FirmwareUpdateState.Idle,
                compatible.Count == 0 ? "No firmware matches the reported board identifiers." : $"Found {compatible.Count} compatible release(s).",
                0);
            return compatible;
        }
        catch (OperationCanceledException)
        {
            SetState(FirmwareUpdateState.Cancelled, "Firmware discovery cancelled.", 0);
            throw;
        }
        catch (Exception exception)
        {
            SetState(FirmwareUpdateState.Failed, exception.Message, 0);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<FirmwarePackage> PrepareAsync(FirmwareManifestEntry release, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Package = null;
        SetState(FirmwareUpdateState.Downloading, "Downloading firmware package…", 0);
        try
        {
            var progress = new InlineProgress<double>(value => SetState(FirmwareUpdateState.Downloading, "Downloading firmware package…", value));
            var package = await packageManager.PrepareAsync(release, progress, cancellationToken).ConfigureAwait(false);
            SetState(FirmwareUpdateState.Verifying, "Verifying firmware SHA-256…", 1);
            if (!package.IsVerified)
            {
                throw new InvalidDataException("Firmware package did not pass cryptographic verification.");
            }

            Package = package;
            SetState(FirmwareUpdateState.ReadyToFlash, "Package verified and ready.", 1);
            return package;
        }
        catch (OperationCanceledException)
        {
            SetState(FirmwareUpdateState.Cancelled, "Firmware download cancelled.", 0);
            throw;
        }
        catch (Exception exception)
        {
            SetState(FirmwareUpdateState.Failed, exception.Message, 0);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<FirmwareFlashResult> FlashAsync(
        VehicleId vehicleId,
        VehicleFirmwareIdentity identity,
        bool parameterBackupConfirmed,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        try
        {
            if (Package is not { IsVerified: true } package || State != FirmwareUpdateState.ReadyToFlash)
            {
                return Fail("A firmware package must be downloaded and verified before flashing.");
            }

            if (!parameterBackupConfirmed)
            {
                return Fail("Back up parameters and confirm the backup before flashing.");
            }

            var platformSupport = flashingService.GetPlatformSupport(identity);
            if (!platformSupport.IsSupported)
            {
                return Fail(platformSupport.Reason);
            }

            var support = flashingService.GetSupport(identity, package);
            if (!support.IsSupported)
            {
                return Fail(support.Reason);
            }

            if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId)
            {
                return Fail("The selected vehicle is no longer connected.");
            }

            flashingVehicle = vehicleId;
            SetState(FirmwareUpdateState.WaitingForDisconnect, "Disconnecting normal MAVLink before bootloader access…", 0);
            await connectionService.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            if (activeVehicle.IsOnline)
            {
                return Fail("Normal MAVLink remained connected; flashing was blocked.");
            }

            SetState(FirmwareUpdateState.Flashing, "Flashing verified firmware…", 0);
            var progress = new InlineProgress<double>(value => SetState(FirmwareUpdateState.Flashing, "Flashing verified firmware…", value));
            var result = await flashingService.FlashAsync(new FirmwareFlashRequest(vehicleId, identity, package), progress, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                return Fail(result.Message);
            }

            SetState(FirmwareUpdateState.WaitingForReconnect, "Flash complete. Reconnect to verify identity and restore parameters if required.", 1);
            return result;
        }
        catch (OperationCanceledException)
        {
            SetState(FirmwareUpdateState.Cancelled, "Firmware update cancelled.", 0);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Firmware update failed for {VehicleId}.", vehicleId);
            return Fail(exception.Message);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        activeVehicle.Changed -= OnActiveVehicleChanged;
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        if (State == FirmwareUpdateState.WaitingForReconnect &&
            args.Current.IsOnline &&
            args.Current.VehicleId == flashingVehicle)
        {
            SetState(FirmwareUpdateState.Completed, "Vehicle reconnected. Verify firmware identity and review parameter restore guidance.", 1);
        }
    }

    private FirmwareFlashResult Fail(string message)
    {
        SetState(FirmwareUpdateState.Failed, message, 0);
        return new FirmwareFlashResult(false, message);
    }

    private void SetState(FirmwareUpdateState state, string status, double progress)
    {
        var stateChanged = State != state;
        State = state;
        if (stateChanged)
        {
            logger.LogInformation("Firmware update state changed to {State}: {Status}", state, status);
        }
        StateChanged?.Invoke(this, new FirmwareUpdateStateChangedEventArgs(state, status, Math.Clamp(progress, 0, 1)));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
