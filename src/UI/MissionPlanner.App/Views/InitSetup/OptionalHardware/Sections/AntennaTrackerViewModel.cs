using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles.Abstractions;

using MissionPlanner.App.Views.Navigation;

using CommunityToolkit.Mvvm.Input;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>
/// View model for the AntennaTrackerView, providing data and logic for the Antenna Tracker setup workflow. 
/// </summary>
/// <param name="vehicle"></param>
/// <param name="logger"></param>
/// <param name="navigation">The application navigation service.</param>
public sealed partial class AntennaTrackerViewModel(IActiveVehicleContext vehicle, ILogger<AntennaTrackerViewModel> logger, INavigationService navigation) : OptionalHardwareBaseViewModel(logger)
{
    /// <summary>Refreshes the current page state without starting a hardware operation.</summary>
    [RelayCommand]
    private void Refresh()
    {
        OnPropertyChanged(nameof(TargetStatus));
        OnPropertyChanged(nameof(SafetyStatus));
        SetMessages(TargetStatus);
    }

    /// <summary>Gets the text that tells whether a tracker vehicle is connected.</summary>
    public string TargetStatus => vehicle.IsOnline ? "Tracker vehicle connected. Settings are shown only when reported by its parameter metadata." : "Connect an AntennaTracker vehicle.";
    /// <summary>Gets the text that explains why the actuator test is unavailable.</summary>
    public string SafetyStatus => "Actuator test is unavailable until a bounded tracker-specific output adapter and operation gate are present.";

}

