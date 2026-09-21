using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>
/// View model for the AntennaTrackerView, providing data and logic for the Antenna Tracker setup workflow. 
/// </summary>
/// <param name="vehicle"></param>
/// <param name="logger"></param>
public sealed partial class AntennaTrackerViewModel(IActiveVehicleContext vehicle, ILogger<AntennaTrackerViewModel> logger) : OptionalHardwareBaseViewModel(logger)
{
    public string TargetStatus => vehicle.IsOnline ? "Tracker vehicle connected. Settings are shown only when reported by its parameter metadata." : "Connect an AntennaTracker vehicle.";
    public string SafetyStatus => "Actuator test is unavailable until a bounded tracker-specific output adapter and operation gate are present.";

}

