using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.FlightData.Actuators;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.AvaloniaUI.App.Utilities;

namespace MissionPlanner.AvaloniaUI.App.Views.FlightData.Tabs;

/// <summary>Presents observed servo outputs and explicit actuator tests.</summary>
public partial class ServoRelayTabViewModel(IActiveVehicleContext active, IVehicleActuatorService service, ILogger<ServoRelayTabViewModel> logger)
    : ViewModelBase(logger)
{
    /// <summary>Gets observed servo PWM values.</summary>
    public ObservableRangeCollection<string> ServoOutputs { get; } = [];

    /// <summary>Gets or sets target channel.</summary>
    [ObservableProperty]
    public partial int Channel { get; set; } = 1;

    /// <summary>Gets or sets requested PWM.</summary>
    [ObservableProperty]
    public partial double Pwm { get; set; } = 1500;

    /// <summary>Gets explicit confirmation.</summary>
    [ObservableProperty]
    public partial bool IsConfirmed
    {
        get; set;
    }

    /// <summary>Gets latest result.</summary>
    [ObservableProperty]
    public partial string Result { get; private set; } = "No command requested";

    /// <summary>
    /// Initializes observed outputs.
    /// </summary>
    private void Refresh()
    {
        var values = active.State?.Radio.ServoOutputsRaw ?? [];

        var servos = new List<string>();
        for (var i = 0; i < values.Count; i++)
        {
            servos.Add($"Servo {i + 1}: {values[i]} µs");
        }

        ServoOutputs.ReplaceRange(servos);
    }

    [RelayCommand]
    private async Task SetServoAsync(CancellationToken token)
    {
        if (active.State is { } state)
        {
            Result = (await service.SetServoAsync(state, Channel, Pwm, IsConfirmed, token)).Summary;
        }

        Refresh();
    }

    /// <inheritdoc />
    public override void Dispose()
    {

    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        return Task.CompletedTask;
    }
}

