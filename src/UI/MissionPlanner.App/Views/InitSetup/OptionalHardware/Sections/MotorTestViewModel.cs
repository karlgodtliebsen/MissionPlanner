using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Models;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>
/// Runs frame-derived, bounded motor tests for the active vehicle.
/// </summary>
public sealed partial class MotorTestViewModel : ParametersViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry parameters;
    private readonly IActuatorTestService service;
    private readonly IMotorSpinParameterService spinParameters;
    private readonly MotorLayoutResolver resolver;
    private readonly IUserConfirmationService confirmation;
    private bool disposed;
    private bool activated;
    private bool spinInputsInitialized;

    private MotorLayout? layout;


    /// <summary>
    /// The collection of motors in the current layout.
    /// </summary>
    public ObservableRangeCollection<MotorLayoutMotor> Motors { get; } = [];

    [ObservableProperty]
    public partial string FrameDisplay { get; private set; } = "Frame layout unavailable.";


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpinArmSum))]
    [NotifyPropertyChangedFor(nameof(SpinMinSum))]
    [NotifyPropertyChangedFor(nameof(SpinArmMaximum))]
    [NotifyPropertyChangedFor(nameof(SpinMinMaximum))]
    [NotifyCanExecuteChangedFor(nameof(SetMotorSpinArmCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetMotorSpinMinCommand))]
    public partial int ThrottlePercent { get; set; } = 10;

    [ObservableProperty]
    public partial int DurationSeconds { get; set; } = 2;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpinArmSum))]
    [NotifyPropertyChangedFor(nameof(SpinMinSum))]
    [NotifyPropertyChangedFor(nameof(SpinMinMaximum))]
    [NotifyCanExecuteChangedFor(nameof(SetMotorSpinArmCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetMotorSpinMinCommand))]
    public partial int SpinArm { get; set; } = 2;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpinMinSum))]
    [NotifyCanExecuteChangedFor(nameof(SetMotorSpinMinCommand))]
    public partial int SpinMin { get; set; } = 3;

    /// <summary>
    ///
    /// </summary>
    public int SpinArmSum => ThrottlePercent + SpinArm;

    /// <summary>
    ///
    /// </summary>
    public int SpinMinSum => SpinArmSum + SpinMin;

    /// <summary>
    ///
    /// </summary>
    public int SpinArmMaximum => Math.Max(1, 19 - ThrottlePercent);

    /// <summary>
    ///
    /// </summary>
    public int SpinMinMaximum => Math.Max(1, 19 - SpinArmSum);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetMotorSpinArmCommand))]
    public partial string SpinArmDisplay { get; private set; } = "MOT_SPIN_ARM: unavailable";

    /// <summary>Gets the current MOT_SPIN_MIN value for display.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetMotorSpinMinCommand))]
    public partial string SpinMinDisplay { get; private set; } = "MOT_SPIN_MIN: unavailable";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetMotorSpinArmCommand))]
    private partial bool HasSpinArm
    {
        get; set;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetMotorSpinMinCommand))]
    private partial bool HasSpinMin
    {
        get; set;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetMotorSpinMinCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetMotorSpinArmCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestSequenceCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    public partial bool IsReady
    {
        get; set;
    }
    /// <summary>
    /// Gets the latest operation or validation status.
    /// </summary>
    [ObservableProperty]
    public partial string? SpinMessage { get; set; } = null;


    /// <summary>
    /// Initializes a new instance of the <see cref="MotorTestViewModel"/> class.
    /// </summary>
    /// <param name="connectionSession">The current vehicle connection session.</param>
    /// <param name="activeVehicle">The application active-vehicle context.</param>
    /// <param name="parameterLoadStatus">The vehicle parameter load status context.</param>
    /// <param name="dialogService">The extended dialog service.</param>
    /// <param name="domainFactory">The domain view factory.</param>
    /// <param name="domainEventHub">The domain event hub.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="parameters">The vehicle parameter registry.</param>
    /// <param name="service">The actuator test service.</param>
    /// <param name="resolver">The motor layout resolver.</param>
    /// <param name="spinParameters">The normalized motor-spin parameter workflow.</param>
    /// <param name="confirmation">The user confirmation service.</param>
    /// <param name="editSessionFactory">The shared parameter editing-session factory.</param>
    public MotorTestViewModel(
        IVehicleConnectionSession connectionSession,
        IActiveVehicleContext activeVehicle,
        IParameterEditSessionFactory editSessionFactory, IVehicleParameterLoadStatusContext parameterLoadStatus,
        IDialogService dialogService,
        IDomainFactory domainFactory,
        IDomainEventHub domainEventHub,
        ILogger<MotorTestViewModel> logger,
        IVehicleParameterRegistry parameters,
        IActuatorTestService service,
        IMotorSpinParameterService spinParameters,
        MotorLayoutResolver resolver,
        IUserConfirmationService confirmation)
        : base(connectionSession, activeVehicle, editSessionFactory, dialogService, domainFactory, parameterLoadStatus, domainEventHub, logger)
    {
        this.activeVehicle = activeVehicle;
        this.parameters = parameters;
        this.service = service;
        this.spinParameters = spinParameters;
        this.resolver = resolver;
        this.confirmation = confirmation;
    }

    private bool canExecute = false;

    private bool CanExecuteCommand()
    {
        return canExecute;
    }

    private bool CanSetSpinMin()
    {
        return canExecute && HasSpinArm && HasSpinMin && SpinArm is >= 1 && SpinMin is >= 1 && SpinMinSum < 20;
    }

    private bool CanSetSpinArm()
    {
        return canExecute && HasSpinArm && ThrottlePercent is >= 0 and < 20 && SpinArm is >= 1 && SpinArmSum < 20;
    }

    [RelayCommand(CanExecute = nameof(CanSetSpinMin))]
    private async Task SetMotorSpinMin()
    {
        if (activeVehicle.VehicleId is not { } id)
        {
            return;
        }

        var state = spinParameters.GetState(id);
        if (state.SpinArmPercent is not { } currentSpinArm)
        {
            SpinMessage = "MOT_SPIN_ARM is unavailable, so MOT_SPIN_MIN cannot be calculated.";
            return;
        }

        // The formula displays the proposed SpinArmSum. Derive the margin from
        // the vehicle's current value so this operation writes that exact total
        // even if the user applies MOT_SPIN_MIN before MOT_SPIN_ARM.
        var effectiveMargin = SpinMinSum - currentSpinArm;
        var recommendation = spinParameters.RecommendSpinMin(id, effectiveMargin);
        if (!recommendation.Success)
        {
            SpinMessage = recommendation.Message;

            return;
        }

        if (!await confirmation.ConfirmAsync(
                "Set minimum in-flight motor output",
                $"Set MOT_SPIN_MIN to {recommendation.Percent:0.#}%?\nFormula: proposed\nMOT_SPIN_ARM {SpinArmSum}% plus {SpinMin}%.",
                "Set MOT_SPIN_MIN"))
        {
            return;
        }

        var result = await spinParameters.SetSpinMinAsync(id, effectiveMargin, activeVehicle.ConnectionCancellationToken);
        SpinMessage = result.Message;
        RefreshSpinParameters(id);
    }

    [RelayCommand(CanExecute = nameof(CanSetSpinArm))]
    private async Task SetMotorSpinArm()
    {
        if (activeVehicle.VehicleId is not { } id)
        {
            return;
        }

        var recommendation = spinParameters.RecommendSpinArm(id, ThrottlePercent, SpinArm);
        if (!recommendation.Success)
        {
            SpinMessage = recommendation.Message;
            return;
        }

        if (!await confirmation.ConfirmAsync(
                "Set armed motor spin output",
                $"Set MOT_SPIN_ARM to {recommendation.Percent:0.#}%?\nThis is {SpinArm} percentage points above the selected motor-test throttle.",
                "Set MOT_SPIN_ARM"))
        {
            return;
        }

        var result = await spinParameters.SetSpinArmAsync(id, ThrottlePercent, SpinArm, activeVehicle.ConnectionCancellationToken);
        SpinMessage = result.Message;

        RefreshSpinParameters(id);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private async Task TestMotor(MotorLayoutMotor motor, CancellationToken cancellationToken)
    {
        if (layout is null || activeVehicle.VehicleId is not { } id || !await ConfirmAsync(cancellationToken))
        {
            return;
        }

        var result = await service.TestMotorAsync(id,
            new MotorTestRequest(motor.TestOrder, MotorThrottleType.Percent, ThrottlePercent, DurationSeconds),
            activeVehicle.ConnectionCancellationToken);
        SetMessages(result.Message);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private async Task TestSequenceAsync(CancellationToken cancellationToken)
    {
        if (layout is null || activeVehicle.VehicleId is not { } id || !await ConfirmAsync(cancellationToken))
        {
            return;
        }

        var result = await service.TestSequenceAsync(id, ThrottlePercent, DurationSeconds, layout.Motors.Count, activeVehicle.ConnectionCancellationToken);
        SetMessages(result.Message);
    }


    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private async Task TestAllAsync(CancellationToken cancellationToken)
    {
        if (layout is null || activeVehicle.VehicleId is not { } id || !await ConfirmAsync(cancellationToken))
        {
            return;
        }

        SetMessages($"Starting all {layout.Motors.Count} motors...");
        NotificationManager?.Show(StatusMessage!);
        try
        {
            var result = await service.TestAllAsync(
                id,
                ThrottlePercent,
                DurationSeconds,
                layout.Motors.Count,
                activeVehicle.ConnectionCancellationToken);
            SetMessages(result.Success ? result.Message : null, result.Success ? null : result.Message);
            NotificationManager?.Show(StatusMessage!);
        }
        catch (OperationCanceledException)
        {
            SetMessages(errorMessage: "The motor test was cancelled before it started.");
        }
        catch (Exception exception)
        {
            SetMessages(exception);
        }
    }


    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private async Task StopAsync(CancellationToken cancellationToken)
    {
        await service.EmergencyStopAsync(cancellationToken);
    }

    private async Task<bool> ConfirmAsync(CancellationToken cancellationToken)
    {
        return await confirmation.ConfirmAsync("Confirm motor-test safety", "Confirm ALL propellers are removed and the area is clear.", "Propellers removed – test", cancellationToken);
    }

    private void Refresh()
    {
        if (disposed)
        {
            return;
        }

        canExecute = false;
        IsReady = false;
        SpinMessage = null;

        if (activeVehicle.VehicleId is { } spinVehicleId)
        {
            RefreshSpinParameters(spinVehicleId);
        }
        else
        {
            HasSpinArm = false;
            HasSpinMin = false;
            SpinArmDisplay = "MOT_SPIN_ARM: unavailable";
            SpinMinDisplay = "MOT_SPIN_MIN: unavailable";
        }

        layout = activeVehicle.VehicleId is { } id
            ? resolver.Resolve(parameters.GetAllParameters(id))
            : null;
        if (layout is null)
        {
            FrameDisplay = "Frame layout unavailable; testing is disabled.";
            return;
        }

        FrameDisplay = $"Frame: {layout.DisplayName} · {layout.Motors.Count} test positions";

        Motors.ReplaceRange(layout.Motors.OrderBy(motor => motor.TestOrder));
        canExecute = true;
        IsReady = true;
        SetMotorSpinArmCommand.NotifyCanExecuteChanged();
        SetMotorSpinMinCommand.NotifyCanExecuteChanged();
    }

    private void RefreshSpinParameters(MissionPlanner.Shared.Models.Vehicles.Models.VehicleId vehicleId)
    {
        var state = spinParameters.GetState(vehicleId);
        HasSpinArm = state.HasSpinArm;
        HasSpinMin = state.HasSpinMin;
        SpinArmDisplay = state.SpinArmPercent is { } arm ? $"MOT_SPIN_ARM: {arm:0.#}%" : "MOT_SPIN_ARM: unavailable";
        SpinMinDisplay = state.SpinMinPercent is { } min ? $"MOT_SPIN_MIN: {min:0.#}%" : "MOT_SPIN_MIN: unavailable";

        if (!spinInputsInitialized && state.SpinArmPercent is { } spinArmPercent && state.SpinMinPercent is { } spinMinPercent)
        {
            // Start with formulas that reproduce the vehicle's stored values
            // where possible. Do this only once so telemetry/session refreshes
            // never overwrite edits the user is currently making.
            SpinArm = Math.Clamp(spinArmPercent - ThrottlePercent, 1, SpinArmMaximum);
            SpinMin = Math.Clamp(spinMinPercent - SpinArmSum, 1, SpinMinMaximum);
            spinInputsInitialized = true;
        }
    }

    private void Changed(ActiveVehicleChangedEventArgs e)
    {
        Dispatcher.Dispatch(() =>
        {
            spinInputsInitialized = false;
            Refresh();
        });
    }

    private void StateChanged(MotorTestStateChangedEventArgs e)
    {
        if (disposed)
        {
            return;
        }

        Dispatcher.Dispatch(() =>
        {
            if (!disposed)
            {
                SetMessages(e.Snapshot.Instruction);
                NotificationManager?.Show(StatusMessage!);

            }
        });
    }

    /// <inheritdoc />
    protected override void OnEditSessionChanged()
    {
        // This view only needs the frame and motor-spin parameters. Building the
        // base class's full ParameterItemViewModel projection on every live
        // parameter update needlessly processes the entire parameter set.
        Dispatcher.Dispatch(Refresh);
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        if (disposed || activated)
        {
            return;
        }
        activated = true;
        SetMessages("Remove all propellers before testing.");
        NotificationManager?.Show(StatusMessage!);
        activeVehicle.Changed += Changed;
        service.StateChanged += StateChanged;
        try
        {
            await base.ActivateAsync();
            await Dispatcher.DispatchAsync(Refresh);
        }
        catch
        {
            activeVehicle.Changed -= Changed;
            service.StateChanged -= StateChanged;
            activated = false;
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        if (!activated)
        {
            return;
        }
        activated = false;
        activeVehicle.Changed -= Changed;
        service.StateChanged -= StateChanged;
        await base.DeactivateAsync();
    }


    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        activated = false;
        activeVehicle.Changed -= Changed;
        service.StateChanged -= StateChanged;
        base.Dispose();
    }
}

