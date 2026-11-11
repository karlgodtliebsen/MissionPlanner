using MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Projects ESC calibration guidance and preparation into Setup controls.</summary>
public sealed partial class EscMotorSetupViewModel : ParameterHardwareViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IActuatorTestService actuatorService;
    private readonly IUserConfirmationService confirmation;

    /// <summary>Initializes the ESC calibration Setup workflow.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="actuatorService">The actuator-test service.</param>
    /// <param name="confirmation">The shared confirmation service.</param>
    /// <param name="navigation">The navigation service.</param>
    /// <param name="hardware">Reported protocol and endpoint configuration.</param>
    /// <param name="logger">The logger.</param>
    public EscMotorSetupViewModel(
        IActiveVehicleContext activeVehicle,
        IActuatorTestService actuatorService,
        IUserConfirmationService confirmation,
        INavigationService navigation,
        IOptionalHardwareService hardware,
        ILogger<EscMotorSetupViewModel> logger)
        : base("esc-motor", activeVehicle, hardware, logger, navigation)
    {
        this.activeVehicle = activeVehicle;
        this.actuatorService = actuatorService;
        this.confirmation = confirmation;
    }

    /// <summary>Gets the audit log of actuator operations.</summary>
    public ObservableRangeCollection<string> Log
    {
        get;
    } = [];

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

    /// <summary>Gets whether an actuator test is running.</summary>
    public bool IsRunning => TestState == MotorTestState.Running;

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
        PrepareEscCalibrationCommand.Cancel();
        ClearEscCalibrationCommand.Cancel();
        actuatorService.StateChanged -= OnStateChanged;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        await actuatorService.EmergencyStopAsync();
        await base.DeactivateAsync();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        PrepareEscCalibrationCommand.Cancel();
        ClearEscCalibrationCommand.Cancel();
        actuatorService.Dispose();
        base.Dispose();
    }

    [RelayCommand]
    private async Task RefreshSetupAsync()
    {
        Load();
        Show(actuatorService.Current);
        await base.RefreshCommand.ExecuteAsync(null);
    }

    private void Load()
    {
        SetBusy();
        EscSteps.Clear();
        EscCalibrationApplicable = false;
        EscExplanation = "Connect a vehicle to inspect ESC calibration guidance.";
        if (activeVehicle.State is null)
        {
            ResetBusy();
            return;
        }

        if (activeVehicle.VehicleId is { } vehicleId)
        {
            var guidance = actuatorService.GetEscCalibrationGuidance(vehicleId);
            EscCalibrationApplicable = guidance.Applicable;
            EscExplanation = $"{guidance.ProtocolName}: {guidance.Explanation}";
            EscSteps.AddRange(guidance.Steps);
        }

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
        SetMessages(null, snapshot.FailureReason);
        Log.Clear();
        Log.AddRange(snapshot.Log.AsEnumerable().Reverse().Select(entry => $"{entry.Timestamp:HH:mm:ss} — {entry.Description}: {entry.Outcome}"));

        OnPropertyChanged(nameof(IsRunning));
        ResetBusy();

    }
}

