using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Setup.MandatoryHardware;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

public sealed partial class RadioSetupViewModel
{
    private readonly RadioSwitchMovement switchMovement = new();

    /// <summary>Gets available auxiliary channel numbers for explicit selection.</summary>
    public IReadOnlyList<int> ArmingChannels { get; } = Enumerable.Range(1, 16).ToArray();

    /// <summary>Gets or sets the channel being reviewed; selection never writes a parameter.</summary>
    [ObservableProperty]
    public partial int SelectedArmingChannel { get; set; } = 8;

    /// <summary>Gets live parameter-derived flight-mode, auxiliary-arm and stick-arm configuration.</summary>
    [ObservableProperty]
    public partial string ArmingConfigurationSummary { get; private set; } = "Connect a vehicle.";

    /// <summary>Gets observed switch movement and assignment conflicts for the selected channel.</summary>
    [ObservableProperty]
    public partial string ArmingSwitchDiagnostic { get; private set; } = string.Empty;

    partial void OnSelectedArmingChannelChanged(int value) => RefreshArmingSwitch();

    private void RefreshArmingSwitch()
    {
        if (activeVehicle.VehicleId is not { } id)
        {
            ArmingConfigurationSummary = "Connect a vehicle.";
            ArmingSwitchDiagnostic = string.Empty;
            return;
        }
        ArmingConfigurationSummary = RadioArmingConfiguration.Describe(name => parameterRegistry.GetParameter(id, name)?.Value);
        if (activeVehicle.IsOnline && activeVehicle.State?.Radio is { } radio && !radio.IsStale(clock.UtcNow, TimeSpan.FromSeconds(2)))
        {
            switchMovement.Observe(radio);
        }
        var movement = switchMovement.Describe(SelectedArmingChannel) ?? $"Move the switch for RC{SelectedArmingChannel} low → high → low to observe its input.";
        var conflict = armingConfiguration.Conflict(id, SelectedArmingChannel);
        var assigned = armingConfiguration.IsAssigned(id, SelectedArmingChannel);
        ArmingSwitchDiagnostic = $"{movement}\nArm function: {(assigned ? "Assigned" : "Not assigned or unavailable")}.\n" +
            (conflict ?? (assigned
                ? $"RC{SelectedArmingChannel} is already configured for Arm/Disarm. No parameter write required."
                : "This channel is free. Assign as Arm/Disarm after reviewing the transmitter mapping.")) +
            "\nSwitch movement confirms RC input only. The flight controller heartbeat confirms armed state; assignment alone does not mean the vehicle is armed." +
            "\nEndpoint calibration checks movement during its active capture window separately from this assignment.";
    }

    [RelayCommand]
    private async Task AssignArmSwitchAsync()
    {
        if (activeVehicle.VehicleId is not { } id || CanCancelCalibration || IsWriting || bindCancellation is not null)
        {
            SetMessages(errorMessage: "Finish calibration or receiver binding before assigning an arm switch.");
            return;
        }
        var channel = SelectedArmingChannel;
        var connection = activeVehicle.ConnectionCancellationToken;
        if (armingConfiguration.Conflict(id, channel) is { } conflict)
        {
            SetMessages(errorMessage: conflict);
            return;
        }
        if (armingConfiguration.IsAssigned(id, channel))
        {
            SetMessages($"RC{channel} is already configured for Arm/Disarm. No parameter write required.");
            RefreshArmingSwitch();
            return;
        }
        if (!await confirmation.ConfirmAsync("Assign Arm/Disarm switch",
            $"Assign RC{channel} to Arm/Disarm? Confirm the transmitter mapping, remove propellers and keep the vehicle disarmed. Only RC{channel}_OPTION will be written and verified.", "Assign and verify"))
        {
            return;
        }
        try
        {
            connection.ThrowIfCancellationRequested();
            SetMessages(await armingConfiguration.AssignAsync(id, channel, connection));
        }
        catch (OperationCanceledException)
        {
            SetMessages(errorMessage: "Arm switch assignment cancelled; refresh parameters before continuing.");
        }
        catch (Exception exception)
        {
            SetMessages(exception);
        }
        RefreshArmingSwitch();
    }
}
