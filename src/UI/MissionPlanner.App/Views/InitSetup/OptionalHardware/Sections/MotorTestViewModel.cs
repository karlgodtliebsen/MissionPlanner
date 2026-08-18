using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Runs frame-derived, bounded motor tests for the active vehicle.</summary>
public sealed partial class MotorTestViewModel : OptionalHardwareBaseViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry parameters;
    private readonly IActuatorTestService service;
    private readonly MotorLayoutResolver resolver;
    private readonly IUserConfirmationService confirmation;
    private readonly IDispatcher dispatcher;

    public MotorTestViewModel(IActiveVehicleContext activeVehicle, IVehicleParameterRegistry parameters, IActuatorTestService service, MotorLayoutResolver resolver, IUserConfirmationService confirmation, IDispatcher dispatcher)
    {
        this.activeVehicle = activeVehicle;
        this.parameters = parameters;
        this.service = service;
        this.resolver = resolver;
        this.confirmation = confirmation;
        this.dispatcher = dispatcher;
        activeVehicle.Changed += Changed;
        parameters.Changed += ParameterChanged;
        service.StateChanged += StateChanged;
        Refresh();
    }

    public ObservableCollection<MotorLayoutMotor> Motors { get; } = [];
    [ObservableProperty] public partial string FrameDisplay { get; private set; } = "Frame layout unavailable.";
    [ObservableProperty] public partial string Status { get; private set; } = "Remove all propellers before testing.";
    [ObservableProperty] public partial double ThrottlePercent { get; set; } = 10;
    [ObservableProperty] public partial double DurationSeconds { get; set; } = 2;
    private MotorLayout? layout;

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
        Motors.Clear();
        layout = activeVehicle.VehicleId is
        {
        }
            id
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
        dispatcher.Dispatch(Refresh);
    }

    private void ParameterChanged(object? s, VehicleParameterChangedEventArgs e)
    {
        if (e.VehicleId == activeVehicle.VehicleId)
        {
            dispatcher.Dispatch(Refresh);
        }
    }

    private void StateChanged(object? s, MotorTestStateChangedEventArgs e)
    {
        dispatcher.Dispatch(() => Status = e.Snapshot.Instruction);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        activeVehicle.Changed -= Changed;
        parameters.Changed -= ParameterChanged;
        service.StateChanged -= StateChanged;
        _ = service.EmergencyStopAsync();
    }
}
