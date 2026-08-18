using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.Common;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.OptionalHardware;
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
    private readonly MotorLayoutResolver resolver;
    private readonly IUserConfirmationService confirmation;
    private readonly IDispatcher dispatcher;
    private bool disposed;

    private MotorLayout? layout;


    public ObservableCollection<MotorLayoutMotor> Motors { get; } = [];
    [ObservableProperty] public partial string FrameDisplay { get; private set; } = "Frame layout unavailable.";
    [ObservableProperty] public partial string Status { get; private set; } = "Remove all propellers before testing.";
    [ObservableProperty] public partial double ThrottlePercent { get; set; } = 10;
    [ObservableProperty] public partial double DurationSeconds { get; set; } = 2;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="connectionSession"></param>
    /// <param name="activeVehicle"></param>
    /// <param name="parameterLoadStatus"></param>
    /// <param name="domainEventHub"></param>
    /// <param name="logger"></param>
    /// <param name="parameters"></param>
    /// <param name="service"></param>
    /// <param name="resolver"></param>
    /// <param name="confirmation"></param>
    /// <param name="editSessionFactory"></param>
    /// <param name="dispatcher"></param>
    /// <param name="dialogService"></param>
    /// <param name="domainFactory"></param>
    public MotorTestViewModel(
        IVehicleConnectionSession connectionSession,
        IActiveVehicleContext activeVehicle,
        IParameterEditSessionFactory editSessionFactory,
        IDispatcher dispatcher,
        IExtendedDialogService dialogService,
        IDomainFactory domainFactory,
        IVehicleParameterLoadStatusContext parameterLoadStatus,
        IDomainEventHub domainEventHub,
        ILogger<MotorTestViewModel> logger,
        IVehicleParameterRegistry parameters,
        IActuatorTestService service,
        MotorLayoutResolver resolver,
        IUserConfirmationService confirmation)
        : base(
            connectionSession,
            activeVehicle,
            editSessionFactory,
            dispatcher,
            dialogService,
            domainFactory,
            parameterLoadStatus,
            domainEventHub,
            logger)
    {
        this.activeVehicle = activeVehicle;
        this.parameters = parameters;
        this.service = service;
        this.resolver = resolver;
        this.confirmation = confirmation;
        this.dispatcher = dispatcher;
        activeVehicle.Changed += Changed;
        service.StateChanged += StateChanged;

        InitializeParameters();
        QueueRefresh();
    }


    [RelayCommand]
    private async Task TestMotorAsync(MotorLayoutMotor motor)
    {
        if (layout is null || activeVehicle.VehicleId is not { } id || !await ConfirmAsync())
        {
            return;
        }

        var result = await service.TestMotorAsync(id, new MotorTestRequest(motor.TestOrder, MotorThrottleType.Percent, ThrottlePercent, DurationSeconds), activeVehicle.ConnectionCancellationToken);
        Status = result.Message;
    }

    [RelayCommand]
    private async Task TestSequenceAsync()
    {
        if (layout is null || activeVehicle.VehicleId is not { } id || !await ConfirmAsync())
        {
            return;
        }

        var result = await service.TestSequenceAsync(id, ThrottlePercent, DurationSeconds, layout.Motors.Count, activeVehicle.ConnectionCancellationToken);
        Status = result.Message;
    }

    [RelayCommand]
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

        Motors.Clear();
        layout = activeVehicle.VehicleId is { } id
            ? resolver.Resolve(parameters.GetAllParameters(id))
            : null;
        if (layout is null)
        {
            FrameDisplay = "Frame layout unavailable; testing is disabled.";
            return;
        }

        FrameDisplay = $"Frame: {layout.DisplayName} · {layout.Motors.Count} test positions";
        foreach (var motor in layout.Motors)
        {
            Motors.Add(motor);
        }
    }

    private void Changed(object? s, ActiveVehicleChangedEventArgs e)
    {
        QueueRefresh();
    }

    private void QueueRefresh()
    {
        if (disposed)
        {
            return;
        }

        dispatcher.Dispatch(() =>
        {
            if (!disposed)
            {
                Refresh();
            }
        });
    }

    private void StateChanged(object? s, MotorTestStateChangedEventArgs e)
    {
        if (disposed)
        {
            return;
        }

        dispatcher.Dispatch(() =>
        {
            if (!disposed)
            {
                Status = e.Snapshot.Instruction;
            }
        });
    }

    /// <inheritdoc />
    protected override void OnEditSessionChanged(object? sender, EventArgs e)
    {
        QueueRefresh();
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
