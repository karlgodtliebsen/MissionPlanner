using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.FlightData.Actuators;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Presents observed servo outputs and explicit actuator tests.</summary>
public partial class ServoRelayTabViewModel(IActiveVehicleContext active, IVehicleActuatorService service) : ObservableObject, IDisposable
{
    /// <summary>Gets observed servo PWM values.</summary>
    public ObservableCollection<string> ServoOutputs { get; } = [];
    /// <summary>Gets or sets target channel.</summary>
    [ObservableProperty] public partial int Channel { get; set; } = 1;
    /// <summary>Gets or sets requested PWM.</summary>
    [ObservableProperty] public partial double Pwm { get; set; } = 1500;
    /// <summary>Gets explicit confirmation.</summary>
    [ObservableProperty] public partial bool IsConfirmed { get; set; }
    /// <summary>Gets latest result.</summary>
    [ObservableProperty] public partial string Result { get; private set; } = "No command requested";
    /// <summary>Initializes observed outputs.</summary>
    public void Refresh() { ServoOutputs.Clear(); var values = active.State?.Radio.ServoOutputsRaw ?? []; for (var i = 0; i < values.Count; i++) ServoOutputs.Add($"Servo {i + 1}: {values[i]} µs"); }
    [RelayCommand] private async Task SetServoAsync(CancellationToken token) { if (active.State is { } state) Result = (await service.SetServoAsync(state, Channel, Pwm, IsConfirmed, token)).Summary; Refresh(); }
    /// <inheritdoc />
    public void Dispose() { }
}
