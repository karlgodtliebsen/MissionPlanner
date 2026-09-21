using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public partial class NamingViewModel(IActiveVehicleContext vehicle, ILogger<AntennaTrackerViewModel> logger) : OptionalHardwareBaseViewModel(logger)
{


    [ObservableProperty]
    public partial string MavSystemId
    {
        get; private set;
    }
    [ObservableProperty]
    public partial string BoardSerialNumber
    {
        get; private set;
    }

    [RelayCommand]
    private void Apply()
    {

    }
}
