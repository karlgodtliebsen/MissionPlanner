using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Projects ESC calibration guidance and bounded, safety-gated motor testing into Setup controls.</summary>
public sealed partial class EscMotorSetupViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IActuatorTestService actuatorService;
    private readonly IUserConfirmationService confirmation;

    /// <summary>Initializes the ESC and motor-test Setup workflow.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="actuatorService">The actuator-test service.</param>
    /// <param name="confirmation">The shared confirmation service.</param>
    /// <param name="logger">The logger.</param>
    public EscMotorSetupViewModel(
        IActiveVehicleContext activeVehicle,
        IActuatorTestService actuatorService,
        IUserConfirmationService confirmation, ILogger<EscMotorSetupViewModel> logger)
        : base(logger)
    {
        this.activeVehicle = activeVehicle;
        this.actuatorService = actuatorService;
        this.confirmation = confirmation;
        MaximumDuration = actuatorService.MaximumDurationSeconds;
        MaximumThrottle = actuatorService.MaximumThrottlePercent;
    }

    /// <summary>Gets the audit log of actuator operations.</summary>
    public ObservableRangeCollection<string> Log
    {
        get;
    } = [];

    /// <summary>Gets whether the connected vehicle family supports motor testing.</summary>
    [ObservableProperty]
    public partial bool SupportsMotorTest
    {
        get;
        private set;
    }

    /// <summary>Gets the ESC calibration explanation.</summary>
    [ObservableProperty]
    public partial string EscExplanation
    {
        get;
        private set;
    } = string.Empty;

    /// <summary>Gets the ESC calibration steps, when applicable.</summary>
    public ObservableRangeCollection<string> EscSteps { get; } = [];

    /// <summary>Gets whether ESC calibration steps apply.</summary>
    [ObservableProperty]
    public partial bool EscCalibrationApplicable
    {
        get;
        private set;
    }

    /// <summary>Gets the current actuator-test state.</summary>
    [ObservableProperty]
    public partial MotorTestState TestState
    {
        get; private set;
    }

    /// <summary>Gets the current actuator-test instruction.</summary>
    [ObservableProperty]
    public partial string Instruction
    {
        get;
        private set;
    } = string.Empty;

    /// <summary>Gets or sets the motor index to test.</summary>
    [ObservableProperty]
    public partial int MotorIndex
    {
        get;
        set;
    } = 1;

    /// <summary>Gets or sets the throttle percentage to apply.</summary>
    [ObservableProperty]
    public partial double ThrottlePercent
    {
        get;
        set;
    } = 10;

    /// <summary>Gets or sets the bounded test duration in seconds.</summary>
    [ObservableProperty]
    public partial double DurationSeconds
    {
        get;
        set;
    } = 2;

    /// <summary>Gets the maximum permitted duration.</summary>
    public double MaximumDuration
    {
        get;
    }

    /// <summary>Gets the maximum permitted throttle percentage.</summary>
    public double MaximumThrottle
    {
        get;
    }

    /// <summary>Gets whether an actuator test is running.</summary>
    public bool IsRunning => TestState == MotorTestState.Running;

    /// <inheritdoc />
    public void Cancel()
    {
        actuatorService.EmergencyStopAsync().SafeFireAndForget();
    }



    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        actuatorService.StateChanged += OnStateChanged;
        activeVehicle.Changed += OnActiveVehicleChanged;
        Load();
        Show(actuatorService.Current);
        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        actuatorService.StateChanged -= OnStateChanged;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        await actuatorService.EmergencyStopAsync();
        await base.DeactivateAsync();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
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
                SetMessages(null, result.Message);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogError(exception, "Motor test failed for {VehicleId}.", vehicleId);
            SetMessages(exception);
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
                SetMessages(null, result.Message);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogError(exception, "Sequence test failed for {VehicleId}.", vehicleId);
            SetMessages(exception);
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
        SetBusy();
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
            EscSteps.AddRange(guidance.Steps);
        }

        TestMotorCommand.NotifyCanExecuteChanged();
        TestSequenceCommand.NotifyCanExecuteChanged();
        ResetBusy();

    }

    private void OnStateChanged(MotorTestStateChangedEventArgs args)
    {
        Dispatcher.Dispatch(() => Show(args.Snapshot));
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        if (SetupVehicleChange.IsConnectionOrIdentityBoundary(args))
        {
            Dispatcher.Dispatch(Load);
        }
    }

    private void Show(MotorTestSnapshot snapshot)
    {
        SetBusy();

        TestState = snapshot.State;
        Instruction = snapshot.Instruction;
        SetMessages(null, snapshot.FailureReason);
        Log.AddRange(snapshot.Log.AsEnumerable().Reverse().Select(entry => $"{entry.Timestamp:HH:mm:ss} — {entry.Description}: {entry.Outcome}"));

        OnPropertyChanged(nameof(IsRunning));
        TestMotorCommand.NotifyCanExecuteChanged();
        TestSequenceCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        ResetBusy();

    }
}

