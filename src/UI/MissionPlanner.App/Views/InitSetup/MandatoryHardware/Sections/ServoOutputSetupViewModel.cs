using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Projects servo output functions with live PWM and confirmed function writes into Setup controls.</summary>
public sealed partial class ServoOutputSetupViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IServoOutputConfigurationService servoService;
    private readonly IDomainEventHub domainEventHub;
    private CancellationTokenSource? operationCancellation;
    private IDisposable? vehicleStateSubscription;
    private DateTimeOffset? observedServoAt;
    private bool active;

    /// <summary>Gets the discovered servo outputs.</summary>
    public ObservableRangeCollection<ServoOutputItemViewModel> Outputs { get; } = [];


    /// <summary>Gets whether any servo outputs were discovered.</summary>
    [ObservableProperty]
    public partial bool HasOutputs
    {
        get;
        private set;
    }


    /// <summary>Initializes the servo output Setup workflow.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="servoService">The servo output configuration service.</param>
    /// <param name="domainEventHub">The domain event hub used for live servo output state.</param>
    /// <param name="logger">The logger.</param>
    public ServoOutputSetupViewModel(
        IActiveVehicleContext activeVehicle,
        IServoOutputConfigurationService servoService,
        IDomainEventHub domainEventHub, ILogger<ServoOutputSetupViewModel> logger)
        : base(logger)
    {
        this.activeVehicle = activeVehicle;
        this.servoService = servoService;
        this.domainEventHub = domainEventHub;
    }


    /// <summary>Loads the servo output configuration for the active vehicle.</summary>
    /// <returns>A task that completes after the configuration is projected.</returns>
    private async Task LoadAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            SetMessages("Connect a vehicle before loading servo outputs.");
            return;
        }
        var token = StartOperation();
        SetBusy();
        try
        {
            // Metadata projection can complete synchronously when cached. Keep that CPU work
            // off the UI thread so the selected tab and its busy indicator can render first.
            var configuration = await Task.Run(() => servoService.GetConfigurationAsync(vehicleId, token), token);

            Dispatcher.Dispatch(() =>
            {
                if (active && activeVehicle.IsOnline && activeVehicle.VehicleId == vehicleId && !token.IsCancellationRequested)
                {
                    Show(configuration);
                }
            });
        }
        catch (OperationCanceledException)
        {
            Debug.Print($"Loading servo outputs was canceled for {vehicleId}.");
        }
        catch (Exception exception)
        {
            Debug.Print($"Loading servo outputs failed for {vehicleId}: {exception.Message}");
            Logger.LogError(exception, "Loading servo outputs failed for {VehicleId}.", vehicleId);
            SetMessages(exception);
        }
        finally
        {
            if (operationCancellation is { } current && current.Token == token)
            {
                ResetBusy();
            }
        }
    }

    /// <inheritdoc />
    public void Cancel()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
        WriteCommand.NotifyCanExecuteChanged();
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        if (active)
        {
            return;
        }

        active = true;
        SetMessages("Load the connected vehicle's servo output functions.");
        SetBusy();
        activeVehicle.Changed += OnActiveVehicleChanged;
        observedServoAt = activeVehicle.State?.Radio.ServoObservedAt;
        vehicleStateSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated);
        // Complete one asynchronous boundary before starting the potentially expensive load.
        // This lets MAUI commit the selected tab and render the activity indicator.
        await Task.Yield();
        await LoadAsync();
        await base.ActivateAsync();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        if (!active)
        {
            return Task.CompletedTask;
        }

        active = false;
        Cancel();
        ResetBusy();
        activeVehicle.Changed -= OnActiveVehicleChanged;
        vehicleStateSubscription?.Dispose();
        vehicleStateSubscription = null;
        observedServoAt = null;
        return base.DeactivateAsync();
    }


    /// <summary>Writes the modified settings for one servo output with readback confirmation.</summary>
    /// <param name="item">The output row to apply.</param>
    /// <returns>A task that completes after the write is confirmed or reported failed.</returns>
    private async Task<bool> ApplyAsync(ServoOutputItemViewModel item)
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            return false;
        }

        var token = StartOperation();
        try
        {
            var result = await servoService.SetOutputAsync(vehicleId, item.Settings, token);
            SetMessages(result.Message);
            if (result.Success)
            {
                item.AcceptChanges();
            }

            return result.Success;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Applying servo output settings failed for {VehicleId}.", vehicleId);
            SetMessages(exception);
            return false;
        }
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        return LoadAsync();
    }

    private bool CanWrite()
    {
        return Outputs.Any(o => o.IsDirty);
    }

    [RelayCommand(CanExecute = nameof(CanWrite))]
    private async Task WriteAsync()
    {
        foreach (var model in Outputs.Where(output => output.IsDirty).ToArray())
        {
            if (!await ApplyAsync(model))
            {
                break;
            }
        }

        WriteCommand.NotifyCanExecuteChanged();
    }

    private CancellationToken StartOperation()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        SetMessages(null, null);
        return operationCancellation.Token;
    }

    private async void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        observedServoAt = args.Current.State?.Radio.ServoObservedAt;
        await LoadAsync();
        Dispatcher.Dispatch(() => WriteCommand.NotifyCanExecuteChanged());
    }

    private Task OnVehicleStateUpdated(VehicleStateUpdated evt, CancellationToken cancellationToken)
    {
        if (evt.VehicleId == activeVehicle.VehicleId && evt.VehicleState.Radio.ServoObservedAt != observedServoAt)
        {
            observedServoAt = evt.VehicleState.Radio.ServoObservedAt;
            var values = evt.VehicleState.Radio.ServoOutputsRaw;
            Dispatcher.Dispatch(() =>
            {
                for (var index = 0; index < Outputs.Count; index++)
                {
                    int? pwm = values is not null && index < values.Count ? values[index] : null;
                    Outputs[index].UpdateLive(pwm, false);
                }
            });
        }

        return Task.CompletedTask;
    }

    private void Show(ServoOutputConfiguration configuration, bool preserveStatus = false)
    {
        if (Outputs.Count == configuration.Outputs.Count &&
            Outputs.Zip(configuration.Outputs).All(pair => pair.First.ChannelNumber == pair.Second.ChannelNumber))
        {
            for (var index = 0; index < configuration.Outputs.Count; index++)
            {
                Outputs[index].Refresh(configuration.Outputs[index]);
            }
        }
        else
        {
            var existing = Outputs.ToDictionary(output => output.ChannelNumber);
            var outputs = new List<ServoOutputItemViewModel>();
            foreach (var output in configuration.Outputs)
            {
                if (existing.TryGetValue(output.ChannelNumber, out var item))
                {
                    item.Refresh(output);
                    outputs.Add(item);
                }
                else
                {
                    outputs.Add(new ServoOutputItemViewModel(output, configuration.FunctionOptions, _ => WriteCommand.NotifyCanExecuteChanged()));
                }
            }
            Outputs.ReplaceRange(outputs);
        }

        if (!preserveStatus)
        {
            var msg = Outputs.Count == 0
                ? "No servo output functions were detected. Refresh after parameters load."
                : "Review or reassign servo output functions. Live PWM updates from telemetry.";
            SetMessages(msg);
        }

        HasOutputs = Outputs.Any();
        WriteCommand.NotifyCanExecuteChanged();
    }
}

