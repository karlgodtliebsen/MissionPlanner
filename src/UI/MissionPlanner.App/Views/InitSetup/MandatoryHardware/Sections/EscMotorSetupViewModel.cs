using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Projects ESC calibration guidance and bounded, safety-gated motor testing into Setup controls.</summary>
public sealed partial class EscMotorSetupViewModel : SetupWorkflowDetailViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IActuatorTestService actuatorService;
    private readonly IUserConfirmationService confirmation;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<EscMotorSetupViewModel> logger;

    /// <summary>Initializes the ESC and motor-test Setup workflow.</summary>
    /// <param name="descriptor">The ESC workflow descriptor.</param>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="actuatorService">The actuator-test service.</param>
    /// <param name="confirmation">The shared confirmation service.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public EscMotorSetupViewModel(
        SetupWorkflowDescriptor descriptor,
        IActiveVehicleContext activeVehicle,
        IActuatorTestService actuatorService,
        IUserConfirmationService confirmation,
        IDispatcher dispatcher,
        ILogger<EscMotorSetupViewModel> logger)
        : base(descriptor)
    {
        this.activeVehicle = activeVehicle;
        this.actuatorService = actuatorService;
        this.confirmation = confirmation;
        this.dispatcher = dispatcher;
        this.logger = logger;
        MaximumDuration = actuatorService.MaximumDurationSeconds;
        MaximumThrottle = actuatorService.MaximumThrottlePercent;
        actuatorService.StateChanged += OnStateChanged;
        Load();
        Show(actuatorService.Current);
    }

    /// <summary>Gets the audit log of actuator operations.</summary>
    public ObservableCollection<string> Log { get; } = [];

    /// <summary>Gets whether the connected vehicle family supports motor testing.</summary>
    [ObservableProperty]
    public partial bool SupportsMotorTest { get; private set; }

    /// <summary>Gets the ESC calibration explanation.</summary>
    [ObservableProperty]
    public partial string EscExplanation { get; private set; } = string.Empty;

    /// <summary>Gets the ESC calibration steps, when applicable.</summary>
    public ObservableCollection<string> EscSteps { get; } = [];

    /// <summary>Gets whether ESC calibration steps apply.</summary>
    [ObservableProperty]
    public partial bool EscCalibrationApplicable { get; private set; }

    /// <summary>Gets the current actuator-test state.</summary>
    [ObservableProperty]
    public partial MotorTestState TestState { get; private set; }

    /// <summary>Gets the current actuator-test instruction.</summary>
    [ObservableProperty]
    public partial string Instruction { get; private set; } = string.Empty;

    /// <summary>Gets or sets the motor index to test.</summary>
    [ObservableProperty]
    public partial int MotorIndex { get; set; } = 1;

    /// <summary>Gets or sets the throttle percentage to apply.</summary>
    [ObservableProperty]
    public partial double ThrottlePercent { get; set; } = 10;

    /// <summary>Gets or sets the bounded test duration in seconds.</summary>
    [ObservableProperty]
    public partial double DurationSeconds { get; set; } = 2;

    /// <summary>Gets the maximum permitted duration.</summary>
    public double MaximumDuration { get; }

    /// <summary>Gets the maximum permitted throttle percentage.</summary>
    public double MaximumThrottle { get; }

    /// <summary>Gets whether an actuator test is running.</summary>
    public bool IsRunning => TestState == MotorTestState.Running;

    /// <inheritdoc />
    public override void Cancel()
    {
        _ = actuatorService.EmergencyStopAsync();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        actuatorService.StateChanged -= OnStateChanged;
        actuatorService.Dispose();
        base.Dispose();
    }

    private bool CanTest()
    {
        return SupportsMotorTest && activeVehicle.IsOnline && TestState != MotorTestState.Running;
    }

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task TestMotorAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId)
        {
            return;
        }

        if (!await ConfirmSafetyAsync())
        {
            return;
        }

        try
        {
            var result = await actuatorService.TestMotorAsync(vehicleId,
                new MotorTestRequest(MotorIndex, MotorThrottleType.Percent, ThrottlePercent, DurationSeconds),
                activeVehicle.ConnectionCancellationToken);
            if (!result.Success)
            {
                Error = result.Message;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Motor test failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task TestSequenceAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId)
        {
            return;
        }

        if (!await ConfirmSafetyAsync())
        {
            return;
        }

        try
        {
            var result = await actuatorService.TestSequenceAsync(vehicleId, ThrottlePercent, DurationSeconds, MotorIndex, activeVehicle.ConnectionCancellationToken);
            if (!result.Success)
            {
                Error = result.Message;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Sequence test failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    private bool CanStop()
    {
        return TestState == MotorTestState.Running;
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task StopAsync()
    {
        return actuatorService.EmergencyStopAsync();
    }

    private async Task<bool> ConfirmSafetyAsync()
    {
        return await confirmation.ConfirmAsync(
            "Confirm actuator test safety",
            "Confirm that ALL propellers are removed and the area is clear. Motors will spin.",
            "Propellers removed – test");
    }

    private void Load()
    {
        if (activeVehicle.State is not { } state)
        {
            SupportsMotorTest = false;
            return;
        }

        SupportsMotorTest = actuatorService.SupportsMotorTest(state.Identity.Firmware.Family);
        if (activeVehicle.VehicleId is { } vehicleId)
        {
            var guidance = actuatorService.GetEscCalibrationGuidance(vehicleId);
            EscCalibrationApplicable = guidance.Applicable;
            EscExplanation = $"{guidance.ProtocolName}: {guidance.Explanation}";
            EscSteps.Clear();
            foreach (var step in guidance.Steps)
            {
                EscSteps.Add(step);
            }
        }

        TestMotorCommand.NotifyCanExecuteChanged();
        TestSequenceCommand.NotifyCanExecuteChanged();
    }

    private void OnStateChanged(object? sender, MotorTestStateChangedEventArgs args)
    {
        dispatcher.Dispatch(() => Show(args.Snapshot));
    }

    private void Show(MotorTestSnapshot snapshot)
    {
        TestState = snapshot.State;
        Instruction = snapshot.Instruction;
        Error = snapshot.FailureReason;
        Log.Clear();
        foreach (var entry in snapshot.Log.AsEnumerable().Reverse())
        {
            Log.Add($"{entry.Timestamp:HH:mm:ss} — {entry.Description}: {entry.Outcome}");
        }

        OnPropertyChanged(nameof(IsRunning));
        TestMotorCommand.NotifyCanExecuteChanged();
        TestSequenceCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }
}
