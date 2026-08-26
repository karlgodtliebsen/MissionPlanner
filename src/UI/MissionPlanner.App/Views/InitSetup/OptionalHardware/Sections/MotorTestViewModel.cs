using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.Common;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using UraniumUI.Material.Dialogs;

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
    private readonly IDispatcher dispatcher;
    private bool disposed;

    private MotorLayout? layout;


    /// <summary>
    /// The collection of motors in the current layout.
    /// </summary>
    public ObservableRangeCollection<MotorLayoutMotor> Motors { get; } = [];

    [ObservableProperty]
    public partial string FrameDisplay { get; private set; } = "Frame layout unavailable.";


    [ObservableProperty]
    public partial int ThrottlePercent { get; set; } = 10;

    [ObservableProperty]
    public partial int DurationSeconds { get; set; } = 2;




    /// <summary>Gets the current MOT_SPIN_ARM value for display.</summary>
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
    /// <param name="dispatcher">The UI Dispatcher.</param>
    public MotorTestViewModel(
        IVehicleConnectionSession connectionSession,
        IActiveVehicleContext activeVehicle,
        IParameterEditSessionFactory editSessionFactory,
        IDispatcher dispatcher,
        IVehicleParameterLoadStatusContext parameterLoadStatus,
        IExtendedDialogService dialogService,
        IDomainFactory domainFactory,
        IDomainEventHub domainEventHub,
        ILogger<MotorTestViewModel> logger,
        IVehicleParameterRegistry parameters,
        IActuatorTestService service,
        IMotorSpinParameterService spinParameters,
        MotorLayoutResolver resolver,
        IUserConfirmationService confirmation)
        : base(connectionSession, activeVehicle, editSessionFactory, dispatcher, dialogService, domainFactory, parameterLoadStatus, domainEventHub, logger)
    {
        this.activeVehicle = activeVehicle;
        this.parameters = parameters;
        this.service = service;
        this.spinParameters = spinParameters;
        this.resolver = resolver;
        this.confirmation = confirmation;
        this.dispatcher = dispatcher;
    }

    private bool canExecute = false;

    private bool CanExecuteCommand()
    {
        return canExecute;
    }

    private bool CanSetSpinMin()
    {
        return canExecute && HasSpinMin;
    }

    private bool CanSetSpinArm()
    {
        return canExecute && HasSpinArm;
    }

    [RelayCommand(CanExecute = nameof(CanSetSpinMin))]
    private async Task SetMotorSpinMin()
    {
        if (activeVehicle.VehicleId is not { } id)
        {
            return;
        }

        var recommendation = spinParameters.RecommendSpinMin(id);
        if (!recommendation.Success)
        {
            SetMessages(recommendation.Message);
            return;
        }

        if (!await confirmation.ConfirmAsync(
                "Set minimum in-flight motor output",
                $"Set MOT_SPIN_MIN to {recommendation.Percent:0.#}%? This is MOT_SPIN_ARM plus 3 percentage points.",
                "Set MOT_SPIN_MIN"))
        {
            return;
        }

        var result = await spinParameters.SetSpinMinAsync(id, activeVehicle.ConnectionCancellationToken);
        SetMessages(result.Message);
        RefreshSpinParameters(id);
    }

    [RelayCommand(CanExecute = nameof(CanSetSpinArm))]
    private async Task SetMotorSpinArm()
    {
        if (activeVehicle.VehicleId is not { } id)
        {
            return;
        }

        var recommendation = spinParameters.RecommendSpinArm(id, ThrottlePercent);
        if (!recommendation.Success)
        {
            SetMessages(recommendation.Message);
            return;
        }

        if (!await confirmation.ConfirmAsync(
                "Set armed motor spin output",
                $"Set MOT_SPIN_ARM to {recommendation.Percent:0.#}%? This is 2 percentage points above the selected motor-test throttle.",
                "Set MOT_SPIN_ARM"))
        {
            return;
        }

        var result = await spinParameters.SetSpinArmAsync(id, ThrottlePercent, activeVehicle.ConnectionCancellationToken);
        SetMessages(result.Message);
        RefreshSpinParameters(id);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private async Task TestMotor(MotorLayoutMotor motor)
    {
        if (layout is null || activeVehicle.VehicleId is not { } id || !await ConfirmAsync())
        {
            return;
        }

        var result = await service.TestMotorAsync(id,
            new MotorTestRequest(motor.TestOrder, MotorThrottleType.Percent, ThrottlePercent, DurationSeconds),
            activeVehicle.ConnectionCancellationToken);
        SetMessages(result.Message);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private async Task TestSequenceAsync()
    {
        if (layout is null || activeVehicle.VehicleId is not { } id || !await ConfirmAsync())
        {
            return;
        }

        var result = await service.TestSequenceAsync(id, ThrottlePercent, DurationSeconds, layout.Motors.Count, activeVehicle.ConnectionCancellationToken);
        SetMessages(result.Message);
    }


    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private async Task TestAllAsync()
    {
        if (layout is null || activeVehicle.VehicleId is not { } id || !await ConfirmAsync())
        {
            return;
        }

        var result = await service.TestAllAsync(id, ThrottlePercent, DurationSeconds, layout.Motors.Count, activeVehicle.ConnectionCancellationToken);
        SetMessages(result.Message);
    }


    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private Task StopAsync()
    {
        return service.EmergencyStopAsync();
    }

    private Task<bool> ConfirmAsync()
    {
        return confirmation.ConfirmAsync("Confirm motor-test safety", "Confirm ALL propellers are removed and the area is clear.", "Propellers removed – test");
    }

    private void Refresh()
    {
        if (disposed)
        {
            return;
        }

        canExecute = false;
        IsReady = false;
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
    }

    private void Changed(ActiveVehicleChangedEventArgs e)
    {
        dispatcher.Dispatch(Refresh);
    }

    private void StateChanged(MotorTestStateChangedEventArgs e)
    {
        if (disposed)
        {
            return;
        }

        dispatcher.Dispatch(() =>
        {
            if (!disposed)
            {
                SetMessages(e.Snapshot.Instruction);
            }
        });
    }

    /// <inheritdoc />
    protected override void OnEditSessionChanged()
    {
        base.OnEditSessionChanged();
        dispatcher.Dispatch(Refresh);
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        if (disposed)
        {
            return;
        }
        SetMessages("Remove all propellers before testing.");
        activeVehicle.Changed += Changed;
        service.StateChanged += StateChanged;
        await base.ActivateAsync();
        dispatcher.Dispatch(Refresh);
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
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
        activeVehicle.Changed -= Changed;
        service.StateChanged -= StateChanged;
        base.Dispose();
    }
}
