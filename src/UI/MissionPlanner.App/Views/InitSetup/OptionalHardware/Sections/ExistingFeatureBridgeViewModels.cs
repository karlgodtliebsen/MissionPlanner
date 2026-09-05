using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Provides metadata-backed camera/gimbal setup and links to existing live payload control.</summary>
public sealed partial class CameraGimbalViewModel(IActiveVehicleContext activeVehicle, IOptionalHardwareService service, INavigationService navigation, ILogger<CameraGimbalViewModel> logger)
    : ParameterHardwareViewModel("camera-gimbal", activeVehicle, service, logger)
{
    /// <summary>Opens the existing Flight Data workspace that owns live payload control.</summary>
    [RelayCommand]
    private Task OpenPayloadControlAsync()
    {
        return navigation.NavigateAsync(MissionPlannerRoutes.FlightData);
    }
}
