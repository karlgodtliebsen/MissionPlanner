using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.ConfigTuning.Sections;

public partial class FullParametersListTabViewModel
{
    private ActiveVehicleChangedEventArgs? deferredVehicleChange;

    /// <inheritdoc />
    protected override Task OnActiveVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        if (reconnectCancellation is not null)
        {
            deferredVehicleChange = args;
            return Task.CompletedTask;
        }
        return base.OnActiveVehicleChanged(args);
    }

    /// <inheritdoc />
    protected override Task ApplyParameterLoadStatusAsync(ParameterLoadStatus? status)
    {
        return reconnectCancellation is not null ? Task.CompletedTask : base.ApplyParameterLoadStatusAsync(status);
    }

    /// <inheritdoc />
    protected override Task OnParameterRegistryChangedAsync(VehicleParameterChangedEventArgs args)
    {
        return reconnectCancellation is not null ? Task.CompletedTask : base.OnParameterRegistryChangedAsync(args);
    }

    private async Task ReconnectAfterApplyAsync(VehicleReconnectTarget? target, CancellationToken cancellationToken)
    {
        if (target is null)
        {
            SetMessages(errorMessage: "Changes require a reboot, but connection settings are unavailable. Reconnect the vehicle manually.");
            return;
        }

        // The old connection lifetime ends during teardown; it must not cancel recovery.
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        reconnectCancellation = lifetime;
        deferredVehicleChange = null;
        var applyStatus = StatusMessage;
        var applyError = ErrorMessage;
        var message = $"Parameters applied. Waiting for {target.Description} to disconnect before reconnecting…";
        IDisposable? overlay = null;
        try
        {
            overlay = await dialogService.DisplayProgressCancellableAsync(() => message,
                new DialogOptions
                {
                    Title = "Reconnecting after parameter changes",
                    RequestCancellation = () => lifetime.Cancel()
                }, lifetime.Token);
            await Task.Yield();
            lifetime.Token.ThrowIfCancellationRequested();
            var progress = new Progress<string>(value => message = value);
            var result = await connections.ReconnectAsync(target, progress, lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            if (!result.Success)
            {
                throw new InvalidOperationException(result.ErrorMessage ?? "The vehicle did not reconnect.");
            }
            applyStatus = $"{applyStatus} Reconnected. A controller reboot is still required to activate reboot-required changes.".Trim();
        }
        catch (OperationCanceledException)
        {
            applyError = $"{applyError} Reconnect cancelled. Reconnect the vehicle manually when ready.".Trim();
        }
        catch (Exception exception)
        {
            applyError = $"{applyError} Reconnect failed: {exception.Message}".Trim();
        }
        finally
        {
            overlay?.Dispose();
            reconnectCancellation = null;
            var change = deferredVehicleChange;
            deferredVehicleChange = null;
            if (!disposed && pageActive)
            {
                // Resume normal session replacement/background loading after our overlay has closed.
                if (change is not null)
                {
                    await base.OnActiveVehicleChanged(change);
                }
                SetMessages(applyStatus, applyError);
            }
        }
    }
}
