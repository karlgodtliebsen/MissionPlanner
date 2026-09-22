using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Utilities.Dialogs;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public partial class NamingViewModel
{
    private bool CanReconnect() => active && operation is null && reconnectTarget is not null && (!vehicle.IsOnline || loadedVehicle is null);

    [RelayCommand(CanExecute = nameof(CanReconnect))]
    private async Task ReconnectAsync()
    {
        if (!CanReconnect())
        {
            return;
        }
        using var lifetime = new CancellationTokenSource();
        operation = lifetime;
        CanEdit = false;
        ReconnectCommand.NotifyCanExecuteChanged();
        try
        {
            await RecoverAsync(lifetime);
        }
        finally
        {
            operation = null;
            CanEdit = active && vehicle.IsOnline && loadedVehicle is not null && vehicle.VehicleId == loadedVehicle;
            ReconnectCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task RecoverAsync(CancellationTokenSource lifetime, int? expectedSystem = null, int? expectedSerial = null, bool progressAlreadyShown = false)
    {
        var target = reconnectTarget!;
        recovering = true;
        CanEdit = false;
        loadedVehicle = null;
        reconnectMessage = $"The vehicle connection is restarting. Waiting for {target.Description} to disconnect before reconnecting…";
        IDisposable? overlay = null;
        try
        {
            if (!progressAlreadyShown)
            {
                overlay = await dialogs.DisplayProgressCancellableAsync(() => reconnectMessage,
                    new DialogOptions
                    {
                        Title = "Reconnecting vehicle",
                        RequestCancellation = () => lifetime.Cancel()
                    }, lifetime.Token);
            }
            await Task.Yield();
            lifetime.Token.ThrowIfCancellationRequested();
            var progress = new Progress<string>(text => reconnectMessage = text);
            var result = await connections.ReconnectAsync(target, progress, lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            if (!result.Success || result.VehicleId is not { } connectedId)
            {
                throw new InvalidOperationException(result.ErrorMessage ?? "Reconnect failed. Check the connection and use Reconnect to retry.");
            }

            reconnectTarget = connections.CaptureReconnectTarget() ?? target;
            reconnectMessage = "Connection restored. Waiting for the active vehicle and reading its identifiers…";
            using var readDeadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            readDeadline.CancelAfter(TimeSpan.FromSeconds(12));
            while (!vehicle.IsOnline || vehicle.VehicleId != connectedId)
            {
                await Task.Delay(100, readDeadline.Token);
            }
            var loadedSystem = await ExchangeAsync(connectedId, systemIdName, null, readDeadline.Token);
            var loadedSerial = await ExchangeAsync(connectedId, serialNumberName, null, readDeadline.Token);
            readDeadline.Token.ThrowIfCancellationRequested();
            RequireTarget(connectedId);
            systemId = loadedSystem;
            serialNumber = loadedSerial;
            loadedVehicle = connectedId;
            MavSystemId = Format(loadedSystem.Value);
            BoardSerialNumber = Format(loadedSerial.Value);
            var verified = (expectedSystem is null || loadedSystem.Value == expectedSystem) &&
                (expectedSerial is null || loadedSerial.Value == expectedSerial);
            SetMessages(verified ? "Reconnected. Vehicle identifiers confirmed by fresh readback." : null,
                verified ? null : "Reconnected, but not all requested identifiers were saved. The fields show the current vehicle values.");
        }
        catch (OperationCanceledException)
        {
            if (active)
            {
                SetMessages(null, lifetime.IsCancellationRequested
                    ? "Reconnect cancelled. Use Reconnect when the vehicle is ready."
                    : "Connection returned, but identifiers could not be verified. Reopen Naming to reload.");
            }
        }
        catch (Exception exception)
        {
            if (active)
            {
                SetMessages(null, $"Reconnect failed: {exception.Message}");
            }
        }
        finally
        {
            overlay?.Dispose();
            recovering = false;
        }
    }
}
