using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Diagnostics;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

public sealed partial class RadioSetupViewModel
{
    /// <summary>Gets or sets the receiver binding evidence state.</summary>
    [ObservableProperty]
    private ReceiverBindState bindState;

    /// <summary>Gets or sets the latest receiver binding explanation.</summary>
    [ObservableProperty]
    private string bindMessage = "Use Bind in the ExpressLRS Lua script if FC-initiated binding is unavailable.";

    /// <summary>Gets receiver protocol, capability evidence and safety explanation.</summary>
    public string BindAvailability
    {
        get
        {
            if (activeVehicle.VehicleId is not { } id)
            {
                return "Receiver protocol: Unknown · Bind capability: Unknown · Connect a vehicle.";
            }
            var availability = commands.GetReceiverBindAvailability(id);
            return $"Receiver protocol: {availability.ReceiverProtocol} · Bind capability: {availability.Capability} · {availability.Reason}";
        }
    }

    private void OnBindParametersChanged(MissionPlanner.Core.Vehicles.VehicleParameterChangedEventArgs args)
    {
        if (args.VehicleId == activeVehicle.VehicleId && (args.Parameter is null || args.Parameter.Name == "RC_PROTOCOLS"))
        {
            Dispatcher.Dispatch(() =>
            {
                OnPropertyChanged(nameof(BindAvailability));
                BindReceiverCommand.NotifyCanExecuteChanged();
            });
        }
    }

    private bool CanBindReceiver()
    {
        return bindCancellation is null && !CanCancelCalibration && activeVehicle.IsOnline &&
            activeVehicle.VehicleId is { } id && commands.GetReceiverBindAvailability(id).IsAvailable;
    }

    [RelayCommand(CanExecute = nameof(CanBindReceiver))]
    private async Task BindReceiverAsync()
    {
        if (!CanBindReceiver() || activeVehicle.VehicleId is not { } id)
        {
            return;
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        bindCancellation = cancellation;
        var progress = new ReceiverBindProgress();
        progress.Request();
        ShowBindProgress(progress);
        StartCommand.NotifyCanExecuteChanged();
        BindReceiverCommand.NotifyCanExecuteChanged();
        try
        {
            var result = await commands.StartReceiverBindAsync(id, cancellation.Token);
            progress.Apply(result.Response);
            ShowBindProgress(progress);
            await PublishProgressAsync();
            // Also bound iterations so a backwards clock adjustment cannot extend the wait indefinitely.
            for (var observation = 0; observation < 80 && !progress.IsComplete; observation++)
            {
                if (progress.Observe(activeVehicle.VehicleId == id ? activeVehicle.State : null, clock.UtcNow))
                {
                    ShowBindProgress(progress);
                    await PublishProgressAsync();
                }
                if (!progress.IsComplete)
                {
                    await Task.Delay(250, cancellation.Token);
                }
            }
            if (!progress.IsComplete)
            {
                progress.FinishWaiting();
                ShowBindProgress(progress);
                await PublishProgressAsync();
            }

            async Task PublishProgressAsync()
            {
                await telemetry.PublishAsync(new VehicleDiagnosticEvent(id, clock.UtcNow,
                    $"ReceiverBind.{progress.State}", progress.Message, result.CorrelationId));
            }
        }
        catch (OperationCanceledException)
        {
            if (BindState == ReceiverBindState.Requested)
            {
                BindState = ReceiverBindState.Idle;
            }
            BindMessage = "Receiver bind monitoring cancelled. No further bind command was sent.";
        }
        catch (Exception exception)
        {
            BindState = ReceiverBindState.Failed;
            BindMessage = $"Receiver bind request failed: {exception.Message}";
            SetMessages(exception);
        }
        finally
        {
            bindCancellation = null;
            BindReceiverCommand.NotifyCanExecuteChanged();
            StartCommand.NotifyCanExecuteChanged();
        }
    }

    private void ShowBindProgress(ReceiverBindProgress progress)
    {
        BindState = progress.State;
        BindMessage = progress.Message;
    }
}
