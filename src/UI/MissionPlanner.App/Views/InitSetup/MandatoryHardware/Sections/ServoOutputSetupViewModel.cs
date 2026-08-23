using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using UraniumUI.Extensions;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Projects servo output functions with live PWM and confirmed function writes into Setup controls.</summary>
public sealed partial class ServoOutputSetupViewModel : SetupWorkflowDetailViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IServoOutputConfigurationService servoService;
    private readonly IDomainEventHub domainEventHub;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<ServoOutputSetupViewModel> logger;
    private CancellationTokenSource? operationCancellation;
    private IDisposable? vehicleStateSubscription;
    private DateTimeOffset? observedServoAt;

    /// <summary>Gets the discovered servo outputs.</summary>
    public ObservableCollection<ServoOutputItemViewModel> Outputs
    {
        get;
    } = [];

    /// <summary>Gets the workflow status.</summary>
    [ObservableProperty]
    public partial string Status
    {
        get;
        private set;
    } = "Load the connected vehicle's servo output functions.";

    /// <summary>Gets whether any servo outputs were discovered.</summary>
    [ObservableProperty]
    public partial bool HasOutputs
    {
        get;
        private set;
    }

    /// <summary>Initializes the servo output Setup workflow.</summary>
    /// <param name="workflowCatalog">The setup workflow catalog.</param>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="servoService">The servo output configuration service.</param>
    /// <param name="domainEventHub">The domain event hub used for live servo output state.</param>
    /// <param name="dispatcher">The UI Dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public ServoOutputSetupViewModel(
        ISetupWorkflowCatalog workflowCatalog,
        IActiveVehicleContext activeVehicle,
        IServoOutputConfigurationService servoService,
        IDomainEventHub domainEventHub,
        IDispatcher dispatcher,
        ILogger<ServoOutputSetupViewModel> logger)
        : base(workflowCatalog.Workflows.First(w => w.Key == SetupWorkflowKey.ServoOutput))
    {
        this.activeVehicle = activeVehicle;
        this.servoService = servoService;
        this.domainEventHub = domainEventHub;
        this.dispatcher = dispatcher;
        this.logger = logger;
        activeVehicle.Changed += OnActiveVehicleChanged;
        observedServoAt = activeVehicle.State?.Radio.ServoObservedAt;
        vehicleStateSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated);
        LoadAsync().FireAndForget();
    }


    /// <summary>Loads the servo output configuration for the active vehicle.</summary>
    /// <returns>A task that completes after the configuration is projected.</returns>
    public async Task LoadAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Status = "Connect a vehicle before loading servo outputs.";
            return;
        }

        var token = StartOperation();
        try
        {
            var configuration = await servoService.GetConfigurationAsync(vehicleId, token);
            dispatcher.Dispatch(() => Show(configuration));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Loading servo outputs failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    /// <inheritdoc />
    public override void Cancel()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
        WriteCommand.NotifyCanExecuteChanged();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        activeVehicle.Changed -= OnActiveVehicleChanged;
        vehicleStateSubscription?.Dispose();
        vehicleStateSubscription = null;
        observedServoAt = null;
        base.Dispose();
    }

    /// <summary>Writes the modified settings for one servo output with readback confirmation.</summary>
    /// <param name="item">The output row to apply.</param>
    /// <returns>A task that completes after the write is confirmed or reported failed.</returns>
    internal async Task<bool> ApplyAsync(ServoOutputItemViewModel item)
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            return false;
        }

        var token = StartOperation();
        try
        {
            var result = await servoService.SetOutputAsync(vehicleId, item.Settings, token);
            Status = result.Message;
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
            logger.LogError(exception, "Applying servo output settings failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
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
        Error = null;
        return operationCancellation.Token;
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        dispatcher.Dispatch(() =>
        {
            observedServoAt = args.Current.State?.Radio.ServoObservedAt;
            _ = LoadAsync();
            WriteCommand.NotifyCanExecuteChanged();
        });
    }

    private Task OnVehicleStateUpdated(VehicleStateUpdated evt, CancellationToken cancellationToken)
    {
        if (evt.VehicleId == activeVehicle.VehicleId && evt.VehicleState.Radio.ServoObservedAt != observedServoAt)
        {
            dispatcher.Dispatch(() =>
            {
                if (evt.VehicleId == activeVehicle.VehicleId &&
                    evt.VehicleState.Radio.ServoObservedAt != observedServoAt)
                {
                    observedServoAt = evt.VehicleState.Radio.ServoObservedAt;
                    RefreshLive();
                }
            });
        }

        return Task.CompletedTask;
    }

    private void RefreshLive()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline || Outputs.Count == 0)
        {
            return;
        }

        _ = UpdateLiveAsync(vehicleId);
    }

    private async Task UpdateLiveAsync(VehicleId vehicleId)
    {
        try
        {
            var configuration = await servoService.GetConfigurationAsync(vehicleId, activeVehicle.ConnectionCancellationToken);
            dispatcher.Dispatch(() =>
            {
                foreach (var output in configuration.Outputs)
                {
                    Outputs.FirstOrDefault(item => item.ChannelNumber == output.ChannelNumber)?.UpdateLive(output);
                }

                WriteCommand.NotifyCanExecuteChanged();
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogDebug(exception, "Live servo refresh failed for {VehicleId}.", vehicleId);
        }
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
            Outputs.Clear();
            foreach (var output in configuration.Outputs)
            {
                if (existing.TryGetValue(output.ChannelNumber, out var item))
                {
                    item.Refresh(output);
                    Outputs.Add(item);
                }
                else
                {
                    Outputs.Add(new ServoOutputItemViewModel(output, configuration.FunctionOptions, _ => WriteCommand.NotifyCanExecuteChanged()));
                }
            }
        }

        if (!preserveStatus)
        {
            Status = Outputs.Count == 0
                ? "No servo output functions were detected. Refresh after parameters load."
                : "Review or reassign servo output functions. Live PWM updates from telemetry.";
        }

        HasOutputs = Outputs.Any();
        WriteCommand.NotifyCanExecuteChanged();
    }
}
