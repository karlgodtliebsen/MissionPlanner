using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;

using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Loads and explicitly applies the active vehicle's user-assigned identifiers.</summary>
public partial class NamingViewModel(
    IActiveVehicleContext vehicle,
    IVehicleParameterService parameters,
    IVehicleParameterRegistry registry,
    IUiDispatcher dispatcher,
    IDomainEventHub eventHub,
    ILogger<NamingViewModel> logger,
    IVehicleConnectionService connections,
    IDialogService dialogs, INavigationService navigation) : OptionalHardwareBaseViewModel(logger, dispatcher, eventHub)
{
    /// <summary>Refreshes the current page state without starting a hardware operation.</summary>
    [RelayCommand]
    private Task RefreshAsync()
    {
        return applying || recovering || IsBusy ? Task.CompletedTask : LoadAsync();
    }

    /// <summary>Opens the shared Parameters Editor workspace when no operation is running.</summary>
    [RelayCommand]
    private async Task OpenFullParametersAsync()
    {
        if (IsBusy)
        {
            return;
        }
        await navigation.NavigateAsync(MissionPlannerRoutes.Configuration);
    }

    private const string systemIdName = "MAV_SYSID";
    private const string serialNumberName = "BRD_SERIAL_NUM";
    private CancellationTokenSource? operation;
    private VehicleId? loadedVehicle;
    private VehicleParameter? systemId;
    private VehicleParameter? serialNumber;
    private bool active;
    private bool applying;
    private bool recovering;
    private VehicleReconnectTarget? reconnectTarget;
    private string reconnectMessage = string.Empty;

    /// <summary>Gets or sets the pending MAVLink system ID (1–255).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial string MavSystemId { get; set; } = string.Empty;

    /// <summary>Gets or sets the pending user-defined board serial number.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial string BoardSerialNumber { get; set; } = string.Empty;

    /// <summary>Gets whether both identifiers are loaded and can be edited.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial bool CanEdit
    {
        get; private set;
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        if (!active)
        {
            active = true;
            vehicle.Changed += OnVehicleChanged;
        }
        await Dispatcher.DispatchAsync(LoadAsync);
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        active = false;
        vehicle.Changed -= OnVehicleChanged;
        operation?.Cancel();
        CanEdit = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        active = false;
        vehicle.Changed -= OnVehicleChanged;
        operation?.Cancel();
        base.Dispose();
    }

    private void OnVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        if (applying || recovering)
        {
            return; // Recovery owns the expected connection transition and reload.
        }
        // Invalidate the old target before queued UI work or delayed parameter replies run.
        operation?.Cancel();
        Dispatcher.Dispatch(() =>
        {
            if (active)
            {
                _ = LoadAsync();
            }
        });
    }

    private async Task LoadAsync()
    {
        operation?.Cancel();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(vehicle.ConnectionCancellationToken);
        operation = lifetime;
        CanEdit = false;
        loadedVehicle = null;
        systemId = null;
        serialNumber = null;
        MavSystemId = string.Empty;
        BoardSerialNumber = string.Empty;
        try
        {
            if (vehicle.VehicleId is not { } id || !vehicle.IsOnline)
            {
                SetMessages("Connect a vehicle to read or change its identifiers.");
                return;
            }

            reconnectTarget = connections.CaptureReconnectTarget() ?? reconnectTarget;
            SetMessages("Loading vehicle identifiers…");
            // Subscribe before each request: cached values must not masquerade as fresh responses.
            var loadedSystem = await ExchangeAsync(id, systemIdName, null, lifetime.Token);
            var loadedSerial = await ExchangeAsync(id, serialNumberName, null, lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            RequireTarget(id);
            systemId = loadedSystem;
            serialNumber = loadedSerial;
            loadedVehicle = id;
            MavSystemId = Format(loadedSystem.Value);
            BoardSerialNumber = Format(loadedSerial.Value);
            CanEdit = true;
            SetMessages("Vehicle identifiers loaded.");
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(operation, lifetime) && active)
            {
                SetMessages("Connect a vehicle to read or change its identifiers.");
            }
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(operation, lifetime))
            {
                Logger.LogWarning(exception, "Loading vehicle identifiers failed.");
                SetMessages(null, exception.Message);
            }
        }
        finally
        {
            if (ReferenceEquals(operation, lifetime))
            {
                operation = null;
                ApplyCommand.NotifyCanExecuteChanged();
                ReconnectCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool CanApply()
    {
        return CanEdit && operation is null && vehicle.IsOnline && loadedVehicle == vehicle.VehicleId &&
            (MavSystemId != Format(systemId!.Value) || BoardSerialNumber != Format(serialNumber!.Value));
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        if (!CanApply() || loadedVehicle is not { } id)
        {
            return;
        }
        if (!int.TryParse(MavSystemId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var requestedSystem) ||
            requestedSystem is < 1 or > 255)
        {
            SetMessages(null, "MAV_SYSID must be a whole number from 1 to 255.");
            return;
        }
        if (!int.TryParse(BoardSerialNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var requestedSerial) ||
            requestedSerial is < -8388608 or > 8388607)
        {
            SetMessages(null, "BRD_SERIAL_NUM must be a whole number from -8388608 to 8388607.");
            return;
        }

        using var lifetime = new CancellationTokenSource();
        operation = lifetime;
        applying = true;
        reconnectTarget = connections.CaptureReconnectTarget() ?? reconnectTarget;
        var attemptedWrite = false;
        CanEdit = false;
        var changedSystem = requestedSystem != systemId!.Value;
        IDisposable? overlay = null;
        try
        {
            SetMessages("Applying changed vehicle identifiers…");
            reconnectMessage = "Saving vehicle identifiers. Waiting for the vehicle to confirm the changes…";
            overlay = await dialogs.DisplayProgressCancellableAsync(() => reconnectMessage,
                new DialogOptions
                {
                    Title = "Updating vehicle identifiers",
                    RequestCancellation = () => lifetime.Cancel()
                }, lifetime.Token);
            await Task.Yield();
            lifetime.Token.ThrowIfCancellationRequested();
            // Write the system ID last because it can change how the target is addressed.
            if (requestedSerial != serialNumber!.Value)
            {
                attemptedWrite = true;
                serialNumber = await ExchangeAsync(id, serialNumberName, requestedSerial, lifetime.Token);
            }
            if (changedSystem)
            {
                attemptedWrite = true;
                systemId = await ExchangeAsync(id, systemIdName, requestedSystem, lifetime.Token);
            }
            lifetime.Token.ThrowIfCancellationRequested();
            if (changedSystem && reconnectTarget is not null)
            {
                await RecoverAsync(lifetime, requestedSystem, requestedSerial, progressAlreadyShown: true);
                return;
            }
            RequireTarget(id);
            MavSystemId = Format(systemId.Value);
            BoardSerialNumber = Format(serialNumber.Value);
            SetMessages(changedSystem
                ? "Changed identifiers confirmed. Reconnect if the vehicle starts using its new system ID."
                : "Changed identifiers confirmed by the vehicle.");
            NotificationManager?.Show(StatusMessage ?? "");
        }
        catch (OperationCanceledException) when (!lifetime.IsCancellationRequested && active && attemptedWrite && reconnectTarget is not null)
        {
            await RecoverAsync(lifetime, requestedSystem, requestedSerial, progressAlreadyShown: true);
        }
        catch (TimeoutException) when (!lifetime.IsCancellationRequested && active && attemptedWrite && reconnectTarget is not null)
        {
            await RecoverAsync(lifetime, requestedSystem, requestedSerial, progressAlreadyShown: true);
        }
        catch (OperationCanceledException)
        {
            if (active)
            {
                SetMessages(null, "Identifier update/reconnect cancelled. Reload the vehicle values before retrying.");
            }
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(operation, lifetime))
            {
                Logger.LogWarning(exception, "Applying vehicle identifiers failed.");
                SetMessages(null, exception.Message);
            }
        }
        finally
        {
            overlay?.Dispose();
            applying = false;
            if (ReferenceEquals(operation, lifetime))
            {
                operation = null;
                CanEdit = active && vehicle.IsOnline && loadedVehicle is not null && vehicle.VehicleId == loadedVehicle;
                ReconnectCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private async Task<VehicleParameter> ExchangeAsync(VehicleId id, string name, int? value, CancellationToken token)
    {
        using var connectionLifetime = CancellationTokenSource.CreateLinkedTokenSource(token, vehicle.ConnectionCancellationToken);
        token = connectionLifetime.Token;
        token.ThrowIfCancellationRequested();
        RequireTarget(id);
        var response = new TaskCompletionSource<VehicleParameter>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Changed(VehicleParameterChangedEventArgs args)
        {
            if (args.VehicleId == id && args.Parameter is { } parameter && parameter.Name == name &&
                (value is null || parameter.Value == value.Value))
            {
                response.TrySetResult(parameter);
            }
        }
        registry.Changed += Changed;
        try
        {
            var sent = value is { } updated
                ? await parameters.SetParameterAsync(id, name, updated,
                    (name == systemIdName ? systemId : serialNumber)!.Type, token)
                : await parameters.RequestParameterAsync(id, name, token);
            token.ThrowIfCancellationRequested();
            if (!sent)
            {
                throw new InvalidOperationException($"Could not {(value is null ? "request" : "send")} {name}.");
            }
            try
            {
                var confirmed = await response.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
                token.ThrowIfCancellationRequested();
                RequireTarget(id);
                return confirmed;
            }
            catch (TimeoutException)
            {
                throw new TimeoutException(value is null
                    ? $"{name} did not respond. It may not be supported by this firmware; reconnect or reopen this page to retry."
                    : $"{name} was sent, but the new value was not confirmed. Reconnect and reload before retrying; any earlier confirmed changes remain saved.");
            }
        }
        finally
        {
            registry.Changed -= Changed;
        }
    }

    private void RequireTarget(VehicleId id)
    {
        if (!active || !vehicle.IsOnline || vehicle.VehicleId != id)
        {
            throw new OperationCanceledException("The active vehicle changed.");
        }
    }

    private static string Format(float value)
    {
        return value.ToString("0", CultureInfo.InvariantCulture);
    }
}
