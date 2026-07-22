using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.InitSetup.Tabs;

/// <summary>Projects servo output functions with live PWM and confirmed function writes into Setup controls.</summary>
public sealed partial class ServoOutputSetupViewModel : SetupWorkflowDetailViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IServoOutputConfigurationService servoService;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<ServoOutputSetupViewModel> logger;
    private CancellationTokenSource? operationCancellation;

    /// <summary>Initializes the servo output Setup workflow.</summary>
    /// <param name="descriptor">The servo output workflow descriptor.</param>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="servoService">The servo output configuration service.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public ServoOutputSetupViewModel(
        SetupWorkflowDescriptor descriptor,
        IActiveVehicleContext activeVehicle,
        IServoOutputConfigurationService servoService,
        IDispatcher dispatcher,
        ILogger<ServoOutputSetupViewModel> logger)
        : base(descriptor)
    {
        this.activeVehicle = activeVehicle;
        this.servoService = servoService;
        this.dispatcher = dispatcher;
        this.logger = logger;
        activeVehicle.Changed += OnActiveVehicleChanged;
        _ = LoadAsync();
    }

    /// <summary>Gets the discovered servo outputs.</summary>
    public ObservableCollection<ServoOutputItemViewModel> Outputs { get; } = [];

    /// <summary>Gets the workflow status.</summary>
    [ObservableProperty]
    public partial string Status { get; private set; } = "Load the connected vehicle's servo output functions.";

    /// <summary>Gets whether any servo outputs were discovered.</summary>
    public bool HasOutputs => Outputs.Count > 0;

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
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        activeVehicle.Changed -= OnActiveVehicleChanged;
        base.Dispose();
    }

    /// <summary>Writes the reviewed function for one servo output with readback confirmation.</summary>
    /// <param name="item">The output row to apply.</param>
    /// <returns>A task that completes after the write is confirmed or reported failed.</returns>
    internal async Task ApplyAsync(ServoOutputItemViewModel item)
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline || item.SelectedFunction is not { } function)
        {
            return;
        }

        var token = StartOperation();
        try
        {
            var result = await servoService.SetFunctionAsync(vehicleId, item.Output, function.Value, token);
            Status = result.Message;
            if (result.Success)
            {
                var configuration = await servoService.GetConfigurationAsync(vehicleId, token);
                dispatcher.Dispatch(() => Show(configuration, true));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Applying servo output function failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    private CancellationToken StartOperation()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        Error = null;
        return operationCancellation.Token;
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args) => dispatcher.Dispatch(RefreshLive);

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
                    Outputs.FirstOrDefault(item => item.Output == output.Output)?.UpdateLive(output);
                }
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
            Outputs.Zip(configuration.Outputs).All(pair => pair.First.Output == pair.Second.Output))
        {
            for (var index = 0; index < configuration.Outputs.Count; index++)
            {
                Outputs[index].UpdateLive(configuration.Outputs[index]);
            }
        }
        else
        {
            Outputs.Clear();
            foreach (var output in configuration.Outputs)
            {
                Outputs.Add(new ServoOutputItemViewModel(output, configuration.FunctionOptions, this));
            }
        }

        if (!preserveStatus)
        {
            Status = Outputs.Count == 0
                ? "No servo output functions were detected. Refresh after parameters load."
                : "Review or reassign servo output functions. Live PWM updates from telemetry.";
        }

        OnPropertyChanged(nameof(HasOutputs));
    }
}

/// <summary>Presents one servo output with a function picker and live PWM.</summary>
public sealed partial class ServoOutputItemViewModel : ObservableObject
{
    private readonly ServoOutputSetupViewModel parent;
    private bool suppressApply;

    /// <summary>Initializes a servo output row.</summary>
    /// <param name="info">The output projection.</param>
    /// <param name="options">The available functions.</param>
    /// <param name="parent">The owning servo workflow.</param>
    public ServoOutputItemViewModel(ServoOutputInfo info, IReadOnlyList<ServoFunctionOption> options, ServoOutputSetupViewModel parent)
    {
        this.parent = parent;
        Output = info.Output;
        Functions = options;
        UpdateLive(info);
        suppressApply = true;
        SelectedFunction = options.FirstOrDefault(option => option.Value == info.FunctionValue);
        suppressApply = false;
    }

    /// <summary>Gets the one-based output number.</summary>
    public int Output { get; }

    /// <summary>Gets the available function options.</summary>
    public IReadOnlyList<ServoFunctionOption> Functions { get; }

    /// <summary>Gets the output header.</summary>
    public string Header => $"Output {Output}";

    /// <summary>Gets the live PWM description.</summary>
    [ObservableProperty]
    public partial string LiveDescription { get; private set; } = string.Empty;

    /// <summary>Gets or sets the selected function.</summary>
    [ObservableProperty]
    public partial ServoFunctionOption? SelectedFunction { get; set; }

    /// <summary>Updates the live PWM from a new projection.</summary>
    /// <param name="info">The output projection.</param>
    public void UpdateLive(ServoOutputInfo info) =>
        LiveDescription = info.LivePwm is { } pwm ? $"{pwm} us{(info.IsStale ? " (stale)" : string.Empty)}" : "—";

    partial void OnSelectedFunctionChanged(ServoFunctionOption? value)
    {
        if (!suppressApply && value is not null)
        {
            _ = parent.ApplyAsync(this);
        }
    }
}
